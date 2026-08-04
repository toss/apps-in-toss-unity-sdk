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
 * 기본 매트릭스 전부)에는 이 매니페스트 자체가 없거나 lazyTag 엔트리가 없다 — e2e-ce-serving.test.js
 * 의 hasCeBuild 관용구와 동일하게, 서버를 기동하기 전에 로컬 파일(디스크 상의
 * ait-build/dist/web/StreamingAssets/ait-stream-font/manifest.json)을 먼저 읽어 ja lazyTag 엔트리
 * 유무를 판정하고 describe 레벨 test.skip() 으로 올린다(F2). 이렇게 하면 비-probe 빌드에서는
 * beforeAll(vite preview 서버 기동) 자체가 실행되지 않아 표준 E2E 레그가 서버 기동 실패에 오염되지
 * 않는다 — 이 파일이 존재해도 기존 워크플로우의 pnpm test 실행에는 영향이 없다.
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

// lazy 대상 존재 여부 — 서버 기동 없이 로컬 파일로 먼저 판정한다(F2, e2e-ce-serving.test.js 의
// hasCeBuild 관용구와 동일). 파일 부재/파싱 실패도 "비-probe 빌드"로 간주해 skip 처리한다.
const LAZY_MANIFEST_PATH = path.resolve(
  AIT_BUILD, 'dist/web/StreamingAssets/ait-stream-font/manifest.json');

function readLocalLazyManifest() {
  try {
    if (!fs.existsSync(LAZY_MANIFEST_PATH)) {
      return null;
    }
    const raw = fs.readFileSync(LAZY_MANIFEST_PATH, 'utf-8');
    return JSON.parse(raw);
  } catch (e) {
    console.log(`[lazy-font] 로컬 manifest.json 읽기/파싱 실패(${e.message}) — 비-probe 빌드로 간주, skip`);
    return null;
  }
}

const localManifest = readLocalLazyManifest();
const localJaEntry = (localManifest?.entries || []).find((e) => e && e.lazyTag === 'ja') || null;
const hasLazyManifest = !!localJaEntry;

// F2: probe_build=true 로 실행된 워크플로우(beta-release.yml e2e-beta-*)가 AIT_EXPECT_DEPLOY_PROBE 를
// 전달하면, manifest 부재를 skip 이 아니라 명시적 실패로 처리한다(e2e-ce-serving.test.js 의
// mustHaveCeBuild 관용구와 동일) — 프로브 빌드인데 lazy 산출물이 안 생긴 "조용한 green" 을 막는다.
// AIT_EXPECT_DEPLOY_PROBE 가 false/0/빈값(기본값)이면 기존과 완전히 동일하게 동작(가드 비활성).
const AIT_EXPECT_DEPLOY_PROBE = ['1', 'true'].includes(
  (process.env.AIT_EXPECT_DEPLOY_PROBE || '').toLowerCase());

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

// 벽시계-바운드 unityInstance 폴링. Playwright page.waitForFunction 은 제품 워치독의
// location.reload() 를 만나면 새 navigation마다 재무장(re-arm)되어 지정한 timeout 을 사실상
// 무시한다(e2e-full-pipeline.test.js 의 waitForUnityBounded 주석 참조 — 관측: 90s 지정에도
// 363s 실행). 이 헬퍼는 내가 제어하는 벽시계 deadline 으로 실제 예산을 강제한다(고정 sleep 없이
// 폴링 간격 + Date.now() 체크만 사용, F3).
async function waitForUnityInstanceBounded(page, budgetMs) {
  const deadline = Date.now() + budgetMs;
  while (Date.now() < deadline) {
    try {
      const ready = await page.evaluate(() => typeof window !== 'undefined' && window['unityInstance'] !== undefined);
      if (ready) return true;
    } catch (e) {
      // 재로드로 인한 컨텍스트 파괴 등 — 무시하고 계속 폴링.
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return false;
}

test.describe('Deploy Probe: fontSubsetLazyLanguages 런타임 검증 (opt-in)', () => {
  test.skip(TEST_LEVEL < 2, `TEST_LEVEL=${TEST_LEVEL} (<2) — full e2e 레벨에서만 실행`);
  // F2: 로컬 파일로 이미 판정된 결과로 describe 전체를 skip — 비-probe 빌드에서는 beforeAll(서버
  // 기동)이 아예 실행되지 않는다(e2e-ce-serving.test.js 의 hasCeBuild 관용구와 동일). 단,
  // AIT_EXPECT_DEPLOY_PROBE 가 참이면(probe 빌드가 기대되는 실행) manifest 가 없어도 skip 하지 않고
  // 아래 test() 내부에서 명시적으로 실패시킨다 — "조용한 green" 방지.
  test.skip(!hasLazyManifest && !AIT_EXPECT_DEPLOY_PROBE,
    'StreamingAssets/ait-stream-font/manifest.json 에 ja lazyTag 엔트리 없음(또는 파일 없음) — deploy probe 빌드가 아님(정상)');

  /** @type {import('child_process').ChildProcess | null} */
  let serverProcess = null;

  test.beforeAll(async () => {
    serverProcess = await startPreviewServer();
  });

  test.afterAll(async () => {
    if (serverProcess) {
      try { serverProcess.kill('SIGKILL'); } catch {}
      serverProcess = null;
    }
  });

  test('ja lazy 폰트가 tofu 감지 후 온디맨드 다운로드/주입된다', async ({ browser }) => {
    // F3: 예산 재배분 — goto 30s + 부팅 90s + 마커 30s + lazy 완료 45s = 195s 내부 폴링 합계,
    // test.setTimeout 210s 로 컨텍스트 생성/어서션 등 나머지 오버헤드 여유(15s)를 확보한다(구 배분:
    // goto60+부팅90+마커30+lazy45=225s > setTimeout 210s 로 이미 예산 초과 상태였음).
    test.setTimeout(210000);

    // F2: AIT_EXPECT_DEPLOY_PROBE=1 인데 로컬 manifest 에 ja lazy 엔트리가 없으면 즉시 명시적으로
    // 실패시킨다(조용한 skip 방지) — 위 describe skip 가드가 이 조합은 통과시켜 beforeAll(서버
    // 기동)까지 실행된 상태다(e2e-ce-serving.test.js 의 mustHaveCeBuild 어서션과 동일 패턴).
    expect(
      hasLazyManifest,
      `AIT_EXPECT_DEPLOY_PROBE=1 인데 manifest.json(${LAZY_MANIFEST_PATH})에 ja lazyTag 엔트리가 없음 — probe 빌드인데 lazy 산출물 누락(조용한 green 방지)`
    ).toBe(true);

    const jaEntry = localJaEntry;
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

    // 예산 30s(F3).
    await page.goto(`http://localhost:${SERVER_PORT}`, { waitUntil: 'domcontentloaded', timeout: 30000 });

    // Unity 부팅 확인 (DeployProbeLazyTextSpawner의 AfterSceneLoad 부트스트랩 전제조건). 예산 90s(F3).
    const bootReady = await waitForUnityInstanceBounded(page, 90000);
    expect(bootReady, 'unityInstance 가 90초 예산 내에 준비되지 않음').toBe(true);
    console.log('[lazy-font] Unity 인스턴스 초기화 완료');

    // (a) 마커 로그 대기: 스포너가 8초 지연 후 tofu 텍스트를 표시했다는 신호. 예산 30s(F3).
    await expect.poll(() => consoleLogs.some((l) => l.includes('[DeployProbe] lazy 텍스트 표시')), {
      timeout: 30000,
      message: '마커 로그 "[DeployProbe] lazy 텍스트 표시"가 30초 내에 나타나지 않음',
    }).toBe(true);
    const markerSeenAt = Date.now();
    // F9: lazy 로드 완료 로그가 마커 로그 "이후" 발생했는지까지 고정한다(온디맨드 성질 자체를
    // 검증 — 마커 이전에 우연히 찍힌 완료 로그를 통과시키지 않는다).
    const markerIndex = consoleLogs.findIndex((l) => l.includes('[DeployProbe] lazy 텍스트 표시'));
    console.log(`[lazy-font] 마커 로그 확인(index=${markerIndex}) — lazy 텍스트 표시됨`);

    // (b) lazy 폰트 로드 완료 로그를 마커 후 45초 예산 내 대기(F3) — 실제 문자열은
    // Runtime/Helpers/AIT.StreamingFont.cs LoadLazyEntry 참조. markerIndex 이후 슬라이스만 검사(F9).
    await expect.poll(
      () => consoleLogs.slice(markerIndex + 1).some((l) => l.includes('[AIT-StreamingFont] lazy 폰트 로드 완료: ja')),
      {
        timeout: 45000,
        message: '"[AIT-StreamingFont] lazy 폰트 로드 완료: ja" 로그가 마커 이후 45초 내에 나타나지 않음',
      }
    ).toBe(true);
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
