// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * CE(Content-Encoding) 네이티브 서빙 회귀 테스트
 *
 * 대상: decompressionFallback OFF + 압축(brotli/gzip) 빌드 — Build/*.br(.gz)를
 * 서버가 Content-Encoding 헤더로 서빙하고 브라우저가 네이티브 해제하는 경로.
 * 기본 E2E 매트릭스는 압축 비활성(AIT_COMPRESSION_FORMAT=0)이라 이 경로가
 * 무검증이었고, 그 사이 레거시(2021/2022) 버퍼링 fetch 인터셉터가 CE 응답의
 * 해제된 byteLength를 압축 크기 Content-Length와 대조해 short read로 오판 →
 * 콜드 부트마다 data+wasm을 통째로 2회 다운로드하는 결함이 있었다(수정:
 * WebGLBuildCopier.cs bufferedFetch — CE 응답은 길이 대조 생략).
 *
 * 검증 항목:
 * 1. 서빙 계층이 .br/.gz에 Content-Encoding을 붙이는가 (vite.config.ts 플러그인)
 * 2. 콜드 부트에서 Build/* 각 URL이 정확히 1회만 다운로드되는가 (이중 다운로드 회귀)
 * 3. 버퍼링 인터셉터의 무결성 재시도(short read/retry/giveup)가 발화하지 않는가
 * 4. Unity 인스턴스가 정상 부팅하는가
 * 5. (2021/2022 레거시 한정) 인터셉터가 실제로 개입해 캐시 저장까지 성공하는가
 *
 * Build/에 CE 서빙 산출물(.br/.gz)이 없으면 전체 skip — 압축 비활성 레그에는
 * 영향 없음. .unityweb(fallback ON)은 로더가 자체 해제하므로 대상이 아님.
 */

// ES Module에서 __dirname 대체
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// UNITY_PROJECT_PATH 환경변수로 프로젝트 경로 지정 가능 (e2e-full-pipeline과 동일 규약)
function findSampleProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) {
    return envPath;
  }
  const versionPatterns = ['6000.3', '6000.2', '6000.0', '2022.3', '2021.3'];
  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../SampleUnityProject-${version}`);
    const distPath = path.resolve(projectPath, 'ait-build/dist/web');
    if (fs.existsSync(distPath)) {
      return projectPath;
    }
  }
  return path.resolve(__dirname, '../SampleUnityProject');
}

const SAMPLE_PROJECT = findSampleProject();
const AIT_BUILD = path.resolve(SAMPLE_PROJECT, 'ait-build');
const DIST_WEB = path.resolve(AIT_BUILD, 'dist/web');
const BUILD_DIR = path.resolve(DIST_WEB, 'Build');

// CE 네이티브 서빙 대상 존재 여부 — 없으면 suite 전체 skip
const buildFiles = fs.existsSync(BUILD_DIR) ? fs.readdirSync(BUILD_DIR) : [];
const ceFiles = buildFiles.filter((f) => f.endsWith('.br') || f.endsWith('.gz'));
const hasCeBuild = ceFiles.length > 0;

// 압축 요구 판정: env가 gzip(1)/brotli(2)/auto(-1→brotli)를 지시한 release 빌드는
// .br/.gz가 반드시 존재해야 한다 — 이때 산출물이 없으면 skip이 아니라 명시적 실패로
// 처리해 "압축이 깨졌는데 CE 레그가 조용히 skip되는" false green을 막는다.
// 단, Unity는 Development Build에서 WebGL 압축을 생략하므로(2026-07 로컬 실측:
// dev+brotli → 비압축 .data/.wasm) dev 빌드는 요구 대상에서 제외.
const envCompressionFormat = process.env.AIT_COMPRESSION_FORMAT || '';
const isDevBuild = process.env.AIT_DEVELOPMENT_BUILD === 'true';
const mustHaveCeBuild = !isDevBuild && ['1', '2', '-1'].includes(envCompressionFormat);

// 포트 대역: full-pipeline(4173+·8081+)/perf-ttff(4223+)와 충돌하지 않는 별도 대역
function getPortOffsetFromUnityVersion(projectPath) {
  const match = projectPath.match(/SampleUnityProject-(\d+)\.(\d+)/);
  if (!match) return 0;
  const major = parseInt(match[1], 10);
  const minor = parseInt(match[2], 10);
  if (major === 2021) return 0;
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  if (major === 6000 && minor === 3) return 4;
  return 0;
}

const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
const SERVER_PORT = 4323 + PORT_OFFSET;

function isPortAvailable(port) {
  return new Promise((resolve) => {
    const tester = net.createServer()
      .once('error', () => resolve(false))
      .once('listening', () => {
        tester.close(() => resolve(true));
      })
      .listen(port, '127.0.0.1');
  });
}

async function waitForPortRelease(port, timeoutMs = 10000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPortAvailable(port)) {
      return true;
    }
    await new Promise((r) => setTimeout(r, 200));
  }
  return false;
}

// 포트에 실제 TCP 연결이 될 때까지 폴링 — stdout 'Local:' 신호나 고정 대기는 리슨
// 보장이 아니라서, 콜드 기동(pnpx 해석 등)이 늦으면 page.goto가
// ERR_CONNECTION_REFUSED로 선행 실패한다 (2026-07 로컬 실측 flake).
// vite preview가 127.0.0.1/::1 중 어느 패밀리에 바인드하든 잡히도록 둘 다 시도.
function tryConnect(port, host) {
  return new Promise((resolve) => {
    const sock = net.connect({ port, host });
    sock.once('connect', () => { sock.destroy(); resolve(true); });
    sock.once('error', () => { sock.destroy(); resolve(false); });
  });
}

async function waitForPortListening(port, timeoutMs = 90000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    for (const host of ['127.0.0.1', '::1']) {
      if (await tryConnect(port, host)) {
        return;
      }
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`vite preview가 ${timeoutMs}ms 내에 포트 ${port}를 열지 않음`);
}

// vite preview 기동 — ait-build/vite.config.ts의 unityWebContentEncodingPlugin이
// configurePreviewServer 훅으로 CE/Content-Type 미들웨어를 등록한다.
async function startPreviewServer() {
  const isWindows = process.platform === 'win32';
  try {
    if (isWindows) {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${SERVER_PORT} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      execSync(`lsof -ti:${SERVER_PORT} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {}
  await waitForPortRelease(SERVER_PORT, 5000);

  const server = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(SERVER_PORT), '--strictPort'], {
    cwd: AIT_BUILD,
    stdio: 'pipe',
    shell: true,
    env: { ...process.env, NODE_OPTIONS: '' }
  });
  server.stdout.on('data', (data) => {
    console.log('[vite preview:ce]', data.toString());
  });
  server.stderr.on('data', (data) => {
    console.error('[vite preview:ce error]', data.toString());
  });

  // 준비 확인은 stdout이 아니라 실제 TCP 연결로 — waitForPortListening 주석 참조
  try {
    await waitForPortListening(SERVER_PORT, 90000);
  } catch (e) {
    try { server.kill('SIGKILL'); } catch {}
    throw e;
  }
  return server;
}

test.describe('CE Native Serving (brotli/gzip, decompressionFallback OFF)', () => {
  test.skip(!hasCeBuild && !mustHaveCeBuild, 'Build/에 CE 네이티브 서빙 산출물(.br/.gz) 없음 — 압축 비활성/fallback ON/dev 빌드는 대상 아님');

  /** @type {import('child_process').ChildProcess | null} */
  let serverProcess = null;

  test.afterAll(async () => {
    if (serverProcess) {
      try { serverProcess.kill('SIGKILL'); } catch {}
      serverProcess = null;
    }
  });

  test('CE 콜드 부트: 단일 다운로드 + 무결성 재시도 미발화 + 부팅 성공', async ({ browser }) => {
    test.setTimeout(300000);

    // 0. 압축을 지시한 release 빌드인데 산출물이 비압축이면 즉시 실패 (조용한 skip 방지)
    expect(
      hasCeBuild,
      `AIT_COMPRESSION_FORMAT=${envCompressionFormat}(release)인데 Build/에 .br/.gz 없음 — 압축 미적용 빌드 (Build/: ${buildFiles.join(', ')})`
    ).toBe(true);

    console.log(`📦 CE 서빙 대상: ${ceFiles.join(', ')}`);
    serverProcess = await startPreviewServer();

    // 콜드 캐시 보장: 공유 세션과 무관한 새 컨텍스트
    const context = await browser.newContext();
    const page = await context.newPage();

    /** @type {Map<string, number>} Build URL별 네트워크 요청 횟수 */
    const buildRequestCounts = new Map();
    /** @type {Map<string, string>} Build 응답의 Content-Encoding 헤더 */
    const buildResponseEncodings = new Map();
    /** @type {string[]} 인터셉터 진단용 콘솔 라인 */
    const cacheLogs = [];
    // wasm 스트리밍 컴파일 폴백 감시. index.html의 instantiateWasm 훅이 응답 body를
    // 네이티브 new Response(application/wasm)로 재포장하므로, dev console(vConsole)이
    // window.fetch를 감싸 네이티브가 아닌 "bound Response"를 반환해도 스트리밍 컴파일이
    // 성공한다(2026-07 실측: 재포장 전엔 매 콜드 부트 폴백 → wasm 재페치·이중 다운로드).
    // 따라서 이 폴백은 발생하면 안 되며, true면 재포장 회귀로 간주해 실패시킨다.
    let wasmStreamingFallback = false;

    page.on('request', (req) => {
      const url = req.url();
      if (url.includes('/Build/')) {
        const name = url.split('/').pop() || url;
        buildRequestCounts.set(name, (buildRequestCounts.get(name) || 0) + 1);
      }
    });
    page.on('response', (res) => {
      const url = res.url();
      if (url.includes('/Build/')) {
        const name = url.split('/').pop() || url;
        if (!buildResponseEncodings.has(name)) {
          buildResponseEncodings.set(name, res.headers()['content-encoding'] || '');
        }
      }
    });
    page.on('console', (msg) => {
      const text = msg.text();
      if (text.includes('[AIT] cache:') || text.includes('short read')) {
        cacheLogs.push(text);
        console.log('  [console]', text.slice(0, 160));
      }
      if (/스트리밍 컴파일 실패|ArrayBuffer 폴백|instantiateStreaming|compileStreaming/.test(text)) {
        wasmStreamingFallback = true;
        console.log('  [console:wasm-fallback]', text.slice(0, 160));
      }
    });

    await page.goto(`http://localhost:${SERVER_PORT}?e2e=true`, { waitUntil: 'domcontentloaded', timeout: 60000 });

    // 4. 부팅 성공 (CE 헤더 누락 시 fallback OFF 로더는 여기서 실패한다)
    await page.waitForFunction(() => window['unityInstance'] !== undefined, { timeout: 240000 });

    // 5. 레거시(2021/2022) 인터셉터 개입 시: 캐시 저장(fire-and-forget) 완료까지 잠시 대기
    const legacyActive = cacheLogs.some((l) => l.includes('cache: legacy active'));
    if (legacyActive) {
      const deadline = Date.now() + 15000;
      while (Date.now() < deadline && !cacheLogs.some((l) => l.includes('cache: stored'))) {
        await page.waitForTimeout(500);
      }
    }

    // 1. 서빙 계층: .br/.gz 응답에 Content-Encoding이 붙었는가
    //    (vite.config.ts가 아티팩트에서 누락되면 여기서 명확히 실패 — 부팅 타임아웃보다 진단 우위)
    for (const [name, encoding] of buildResponseEncodings) {
      if (name.endsWith('.br')) {
        expect(encoding, `${name} 응답에 Content-Encoding: br 필요 (vite.config.ts CE 플러그인 미적용?)`).toBe('br');
      } else if (name.endsWith('.gz')) {
        expect(encoding, `${name} 응답에 Content-Encoding: gzip 필요`).toBe('gzip');
      }
    }

    // 2. 이중 다운로드 회귀: Build/* 각 URL(wasm 포함)은 정확히 1회만 요청되어야 한다.
    //    wasm은 instantiateStreaming 재포장(index.html 훅)으로 단일 다운로드 + 스트리밍
    //    컴파일이 보장되므로 예외를 두지 않는다. 2회 이상이면 (a) 스트리밍 재포장 회귀
    //    또는 (b) CE 응답 길이 오판(short read)에 의한 재다운로드다.
    const duplicated = [...buildRequestCounts.entries()].filter(([, n]) => n > 1);
    expect(
      duplicated,
      `Build 리소스 중복 다운로드 감지: ${JSON.stringify(duplicated)} ` +
      `(wasmStreamingFallback=${wasmStreamingFallback}) — 스트리밍 재포장/short read 회귀 의심`
    ).toEqual([]);

    // 2-1. wasm 스트리밍 컴파일 폴백 미발화 — 재포장 훅이 fetch 래퍼(vConsole) Response
    //      오염과 MIME 문제를 무력화해 스트리밍이 성공해야 한다. 이 폴백이 곧 wasm
    //      이중 다운로드의 원인이었으므로, 발생 자체를 회귀로 본다.
    expect(
      wasmStreamingFallback,
      'wasm 스트리밍 컴파일이 ArrayBuffer 폴백으로 강등됨 — index.html instantiateWasm 재포장 회귀(CE wasm 이중 다운로드)'
    ).toBe(false);

    // 3. 버퍼링 인터셉터 무결성 재시도 미발화 — short read 버그의 직접 지문.
    const retryLogs = cacheLogs.filter((l) => /cache: (retry|giveup)|short read/.test(l));
    expect(retryLogs, `버퍼링 fetch 재시도/포기 발생: ${JSON.stringify(retryLogs)}`).toEqual([]);

    // 5. 레거시 인터셉터가 개입했다면 저장까지 성공해야 한다 (해제된 bytes가 캐시에 안착)
    if (legacyActive) {
      const storedLogs = cacheLogs.filter((l) => l.includes('cache: stored'));
      expect(storedLogs.length, '레거시 버퍼링 인터셉터 개입 시 cache: stored 1건 이상 필요').toBeGreaterThan(0);
    }

    console.log(`✅ CE 서빙 검증 완료 — 요청 ${buildRequestCounts.size}종, legacy=${legacyActive}, wasmStreamingFallback=${wasmStreamingFallback}`);

    await context.close();
  });
});
