// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * 폰트 subset 선택 언어 lazy 확장(fontSubsetLazyLanguages) — deploy probe 런타임 검증
 *
 * 대상: DeployProbeBuildRunner(beta-release.yml probe_build=true)가 만든 빌드에만 존재하는
 * StreamingAssets/ait-stream-font/manifest.json 의 ja lazy 엔트리. 표준 E2E 빌드(probe_build 미사용,
 * 기본 매트릭스 전부)에는 이 매니페스트 자체가 없거나 lazyTag 엔트리가 없으므로 test.skip()으로
 * 자동 무해화된다 — 이 파일이 존재해도 기존 워크플로우의 pnpm test 실행에는 영향이 없다.
 *
 * 검증 대상 제품 경로: DeployProbeLazyTextSpawner(Runtime, AIT_E2E_DEPLOY_PROBE 게이트)가 부팅
 * 8초 후 TMP 기본 폰트(글리프 없음)로 일본어 문자열을 표시 → tofu 렌더 → TMP 글로벌 fallback 조회 →
 * AITStreamingFont(런타임 재수화 컴포넌트)가 ja lazy 번들을 온디맨드 다운로드해 fallback 에 주입.
 *
 * TEST_LEVEL>=2(full e2e)에서만 실행 — beta-release.yml의 e2e-beta-* 잡은 TEST_LEVEL=2 고정.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const TEST_LEVEL = parseInt(process.env.TEST_LEVEL || '2', 10);

// UNITY_PROJECT_PATH 환경변수로 프로젝트 경로 지정 가능 (e2e-full-pipeline/e2e-ce-serving과 동일 규약)
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

// 포트 대역: full-pipeline(4173+·8081+)/perf-ttff(4223+)/ce-serving(4323+)와 충돌하지 않는 별도 대역
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
const SERVER_PORT = 4423 + PORT_OFFSET;

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

// vite preview 기동 (e2e-ce-serving.test.js와 동일 관용구) — 서빙 루트는 ait-build/dist/web,
// StreamingAssets/도 이 트리 밑에 정적으로 포함되어 있다(WebGLBuildCopier → public/StreamingAssets
// → vite build가 dist/web/StreamingAssets로 그대로 복사).
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
    console.log('[vite preview:lazy-font]', data.toString());
  });
  server.stderr.on('data', (data) => {
    console.error('[vite preview:lazy-font error]', data.toString());
  });

  try {
    await waitForPortListening(SERVER_PORT, 90000);
  } catch (e) {
    try { server.kill('SIGKILL'); } catch {}
    throw e;
  }
  return server;
}

test.describe('Deploy Probe: fontSubsetLazyLanguages 런타임 검증 (opt-in)', () => {
  test.skip(TEST_LEVEL < 2, `TEST_LEVEL=${TEST_LEVEL} (<2) — full e2e 레벨에서만 실행`);

  /** @type {import('child_process').ChildProcess | null} */
  let serverProcess = null;
  /** @type {any} 서빙 루트 StreamingAssets/ait-stream-font/manifest.json (probe 빌드가 아니면 null) */
  let manifest = null;
  /** @type {any} manifest.entries 중 lazyTag === 'ja' 엔트리 */
  let jaEntry = null;

  test.beforeAll(async () => {
    if (!fs.existsSync(AIT_BUILD)) {
      // ait-build 자체가 없으면(예: 이 Unity 버전 레그가 아직 안 빌드됨) 서버 기동 없이 skip 처리.
      return;
    }

    serverProcess = await startPreviewServer();

    const manifestUrl = `http://localhost:${SERVER_PORT}/StreamingAssets/ait-stream-font/manifest.json`;
    try {
      const res = await fetch(manifestUrl);
      if (res.ok) {
        manifest = await res.json();
        jaEntry = (manifest?.entries || []).find((e) => e && e.lazyTag === 'ja') || null;
      } else {
        console.log(`[lazy-font] manifest.json 없음(HTTP ${res.status}) — 비-probe 빌드로 간주, skip`);
      }
    } catch (e) {
      console.log(`[lazy-font] manifest.json fetch 실패(${e.message}) — 비-probe 빌드로 간주, skip`);
    }
  });

  test.afterAll(async () => {
    if (serverProcess) {
      try { serverProcess.kill('SIGKILL'); } catch {}
      serverProcess = null;
    }
  });

  test('ja lazy 폰트가 tofu 감지 후 온디맨드 다운로드/주입된다', async ({ browser }) => {
    test.setTimeout(180000);

    // 비-probe 빌드(표준 E2E 매트릭스)에서는 manifest 자체가 없거나 ja lazy 엔트리가 없다 — 자동 무해화.
    test.skip(!jaEntry, 'StreamingAssets/ait-stream-font/manifest.json 에 ja lazyTag 엔트리 없음 — deploy probe 빌드가 아님(정상)');

    console.log(`[lazy-font] ja lazy 엔트리 확인: bundle=${jaEntry.bundle}, ranges=${jaEntry.lazyRanges}`);

    const context = await browser.newContext();
    const page = await context.newPage();

    const consoleLogs = [];
    page.on('console', (msg) => {
      const text = msg.text();
      consoleLogs.push(text);
      if (text.includes('[DeployProbe]') || text.includes('[AIT-StreamingFont]')) {
        console.log('  [console]', text.slice(0, 200));
      }
    });

    /** @type {string[]} StreamingAssets/ait-stream-font/ 밑으로 실제 발생한 응답 URL */
    const streamFontResponses = [];
    page.on('response', (res) => {
      const url = res.url();
      if (url.includes('/ait-stream-font/')) {
        streamFontResponses.push(url);
      }
    });

    await page.goto(`http://localhost:${SERVER_PORT}`, { waitUntil: 'domcontentloaded', timeout: 60000 });

    // Unity 부팅 확인 (DeployProbeLazyTextSpawner의 AfterSceneLoad 부트스트랩 전제조건)
    await page.waitForFunction(() => window['unityInstance'] !== undefined, { timeout: 120000 });
    console.log('[lazy-font] Unity 인스턴스 초기화 완료');

    // (a) 마커 로그 대기: 스포너가 8초 지연 후 tofu 텍스트를 표시했다는 신호.
    // Unity 부팅 자체가 오래 걸릴 수 있으므로 넉넉한 예산(60초)을 둔다.
    await expect.poll(() => consoleLogs.some((l) => l.includes('[DeployProbe] lazy 텍스트 표시')), {
      timeout: 60000,
      message: '마커 로그 "[DeployProbe] lazy 텍스트 표시"가 60초 내에 나타나지 않음',
    }).toBe(true);
    const markerSeenAt = Date.now();
    console.log('[lazy-font] 마커 로그 확인 — lazy 텍스트 표시됨');

    // (b) lazy 폰트 로드 완료 로그를 마커 후 45초 예산 내 대기 (실제 문자열은
    // Runtime/Helpers/AIT.StreamingFont.cs LoadLazyEntry 참조).
    await expect.poll(() => consoleLogs.some((l) => l.includes('[AIT-StreamingFont] lazy 폰트 로드 완료: ja')), {
      timeout: 45000,
      message: '"[AIT-StreamingFont] lazy 폰트 로드 완료: ja" 로그가 마커 후 45초 내에 나타나지 않음',
    }).toBe(true);
    console.log(`[lazy-font] ja lazy 폰트 로드 완료 확인 (마커 후 ${Date.now() - markerSeenAt}ms)`);

    // (c) 로드 실패 로그가 없어야 한다.
    const failureLogs = consoleLogs.filter((l) => l.includes('lazy 폰트 로드 실패') && l.includes('ja'));
    expect(failureLogs, `lazy 폰트 로드 실패 로그가 존재함: ${JSON.stringify(failureLogs)}`).toEqual([]);

    // (d) manifest의 ja entry.bundle에 대한 실제 네트워크 요청이 발생했는지 확인.
    const bundleRequested = streamFontResponses.some((url) => url.includes(jaEntry.bundle));
    expect(
      bundleRequested,
      `ja lazy 번들(${jaEntry.bundle})에 대한 네트워크 요청을 확인하지 못함. 관측된 ait-stream-font 요청: ${JSON.stringify(streamFontResponses)}`
    ).toBe(true);

    console.log('[lazy-font] 검증 통과: 마커 → lazy 로드 완료 → 실패 없음 → 번들 네트워크 요청 확인');

    await context.close();
  });
});
