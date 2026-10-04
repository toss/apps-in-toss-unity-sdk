// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — WebGL 컨텍스트 레이어(ait-gl.js) 검증
 *
 * 두 부분으로 나뉜다.
 *
 * 1) 합성 페이지(synthetic): Unity 빌드 없이 실제 WebGLTemplates/AITTemplate/Runtime/ait-gl.js 를 그대로 올린 가짜 템플릿으로
 *    getContext 훅·antialias 해제·probe 해제·context 손실 복구(reload 1회, 루프 가드, hidden 지연)·tier 를 검증한다.
 *    항상 실행된다(브라우저만 있으면 된다).
 * 2) 빌드 산출물(build): 실제 ait-build/dist/web 을 띄워 모바일 UA·DPR 3 에뮬레이션에서 requested/actual 기록, DPR 상한,
 *    loseContext 복구, ?aitglprobe=1 RT 인벤토리를 확인한다. 산출물이 없으면 건너뛴다.
 *
 * 환경변수(빌드 부분):
 *  - UNITY_PROJECT_PATH           : 빌드 산출물(ait-build/dist/web 포함) 경로. 미지정 시 HeavySampleUnityProject-* / SampleUnityProject-* 자동탐지.
 *  - PERF_GL_EXPECT_AA_DROP=1     : antialias 해제 변형(webglAntialiasOpt=1) 빌드일 때 모바일에서 actual.antialias=false, samples=0 을 단언한다.
 *  - PERF_GL_EXPECT_NO_RBMS=1     : AA=4 변형 빌드에서 ?aitglprobe=1 인벤토리에 rbMS(Unity 자체 MSAA RT) 행이 없음을 단언한다.
 *                                   없으면 결과를 로그/annotation 으로만 남긴다(context AA 를 끄면 Unity 가 자체 MSAA RT 를 만드는지 확인하는 용도).
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const AIT_GL_PATH = path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/Runtime/ait-gl.js');
const AIT_GL_JS = fs.readFileSync(AIT_GL_PATH, 'utf8');

const IPHONE_UA = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1';
const ORIGIN = 'https://ait-gl.test';

// ============================================================================
// 1) 합성 페이지
// ============================================================================

/** index.html 의 관련 부분만 흉내 낸 가짜 템플릿. ait-gl.js 는 진짜를 쓴다. */
function fakeTemplateHtml() {
  return `<!DOCTYPE html>
<html><head><meta charset="utf-8">
<script>
  var __q = new URLSearchParams(location.search);
  window.__AIT_PERF = JSON.parse(__q.get('perf') || '{}');
</script>
<script src="/ait-gl.js"></script>
</head>
<body>
<canvas id="unity-canvas" width="320" height="480"></canvas>
<script>
(function () {
  var q = __q;
  try { sessionStorage.setItem('__t_boot', String(Number(sessionStorage.getItem('__t_boot') || 0) + 1)); } catch (e) {}

  // Unity 로더가 기기 정보를 읽는 probe 모사: DOM 에 붙지 않은 캔버스에서 만들고 같은 태스크에서 읽고 버린다.
  var f = document.createElement('canvas');
  var h = f.getContext('webgl2') || f.getContext('webgl');
  if (h) { h.getExtension('WEBGL_debug_renderer_info'); h.getParameter(h.VERSION); }
  window.__loaderProbe = h;

  // 템플릿 computeAutoDevicePixelRatio 의 testCanvas 모사.
  var t = document.createElement('canvas');
  window.__testCanvasCtx = t.getContext('webgl2') || t.getContext('webgl');

  // 게임이 크기를 바꿔 쓰는 detached 캔버스(probe 로 오인해 해제하면 안 된다).
  if (q.get('extraProbe') === '1') {
    var c2 = document.createElement('canvas');
    c2.width = 64; c2.height = 64;
    window.__resizedCtx = c2.getContext('webgl2') || c2.getContext('webgl');
  }

  window.__AIT_EFFECTIVE_DPR = Number(q.get('dpr') || window.devicePixelRatio || 1);
  window.__tierCap = window.__AIT_GL.tierCap();

  var canvas = document.getElementById('unity-canvas');
  window.__bound = window.__AIT_GL.bindContextLoss(canvas);
  window.__unityGl = canvas.getContext('webgl2', { alpha: false, antialias: true, depth: true, stencil: true,
    powerPreference: 'default', preserveDrawingBuffer: false });
})();
</script>
</body></html>`;
}

/**
 * 합성 페이지를 연다. 이 context 의 모든 요청은 route 로 응답하므로 서버가 필요 없다.
 * @param {import('@playwright/test').BrowserContext} context
 * @param {string} search 쿼리 문자열('?perf=...' 형태)
 */
async function openSynthetic(context, search = '') {
  await context.route(`${ORIGIN}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/ait-gl.js') {
      await route.fulfill({ status: 200, contentType: 'application/javascript', body: AIT_GL_JS });
    } else {
      await route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: fakeTemplateHtml() });
    }
  });
  const page = await context.newPage();
  const consoleLines = [];
  page.on('console', (m) => consoleLines.push(m.text()));
  const nav = { count: 0 };
  page.on('framenavigated', (f) => { if (f === page.mainFrame()) nav.count++; });
  await page.goto(`${ORIGIN}/index.html${search}`);
  await page.waitForFunction(() => !!window['__unityGl']);
  return { page, nav, consoleLines };
}

function perfQuery(flags, extra = '') {
  return `?perf=${encodeURIComponent(JSON.stringify(flags))}${extra}`;
}

/** @param {import('@playwright/test').Page} page */
async function loseUnityContext(page) {
  await page.evaluate(() => { window['__unityGl'].getExtension('WEBGL_lose_context').loseContext(); });
}

test.describe('ait-gl.js 합성 페이지', () => {
  test('요청/실제 속성을 기록하고 [AIT-GL] 로그를 한 번 남긴다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, consoleLines } = await openSynthetic(context);
      const state = await page.evaluate(() => {
        const gl = window['__AIT_GL'];
        const unity = gl.contexts.filter((c) => c.role === 'unity');
        return {
          unityCount: unity.length,
          requested: gl.requested,
          actual: gl.actual,
          samples: gl.samples,
          real: window['__unityGl'].getContextAttributes(),
          aaDecision: gl.aaDecision,
        };
      });
      expect(state.unityCount).toBe(1);
      expect(state.requested.antialias).toBe(true);
      expect(state.requested.powerPreference).toBe('default');
      expect(state.actual).toBeTruthy();
      expect(state.actual.antialias).toBe(state.real.antialias);
      expect(state.aaDecision).toBe('flag-off');
      const logs = consoleLines.filter((l) => l.startsWith('[AIT-GL] context '));
      expect(logs.length).toBe(1);
      console.log(`  ${logs[0]}`);
    } finally {
      await context.close();
    }
  });

  test('모바일 + glDropAntialias=true 이면 antialias 요청을 끈다(DPR 3)', async ({ browser }) => {
    const context = await browser.newContext({ userAgent: IPHONE_UA, deviceScaleFactor: 3, viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    try {
      const { page } = await openSynthetic(context, perfQuery({ glDropAntialias: true }));
      const s = await page.evaluate(() => {
        const gl = window['__AIT_GL'];
        const rec = gl.contexts.find((c) => c.role === 'unity');
        return { decision: gl.aaDecision, requested: gl.requested, actual: window['__unityGl'].getContextAttributes(), samples: gl.samples, dropped: rec.aaDropped };
      });
      expect(s.decision).toBe('dropped');
      expect(s.dropped).toBe(true);
      expect(s.requested.antialias).toBe(true); // 기록하는 것은 Unity 가 요청한 원래 값
      expect(s.actual.antialias).toBe(false);
      expect(s.samples).toBe(0);
    } finally {
      await context.close();
    }
  });

  test('데스크톱에서는 glDropAntialias=true 여도 antialias 를 끄지 않는다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page } = await openSynthetic(context, perfQuery({ glDropAntialias: true }));
      expect(await page.evaluate(() => window['__AIT_GL'].aaDecision)).toBe('not-mobile');
    } finally {
      await context.close();
    }
  });

  test('로더 probe 와 testCanvas 컨텍스트는 해제하고 Unity 컨텍스트와 크기를 바꿔 쓰는 캔버스는 건드리지 않는다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page } = await openSynthetic(context, '?extraProbe=1');
      await page.waitForTimeout(200);
      const s = await page.evaluate(() => ({
        probeLost: window['__loaderProbe'].isContextLost(),
        testLost: window['__testCanvasCtx'].isContextLost(),
        unityLost: window['__unityGl'].isContextLost(),
        resizedLost: window['__resizedCtx'].isContextLost(),
        released: window['__AIT_GL'].probeReleased,
      }));
      expect(s.probeLost).toBe(true);
      expect(s.testLost).toBe(true);
      expect(s.unityLost).toBe(false);
      expect(s.resizedLost).toBe(false);
      expect(s.released).toBe(2);

      // Unity 컨텍스트가 생긴 뒤에 만든 detached 컨텍스트는 probe 가 아니다.
      const lateLost = await page.evaluate(() => new Promise((resolve) => {
        const c = document.createElement('canvas');
        const g = c.getContext('webgl2') || c.getContext('webgl');
        setTimeout(() => resolve(g.isContextLost()), 100);
      }));
      expect(lateLost).toBe(false);
    } finally {
      await context.close();
    }
  });

  test('glHook=false 이면 getContext 를 건드리지 않는다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page } = await openSynthetic(context, perfQuery({ glHook: false }));
      await page.waitForTimeout(200);
      const s = await page.evaluate(() => ({
        contexts: window['__AIT_GL'].contexts.length,
        probeLost: window['__loaderProbe'].isContextLost(),
        decision: window['__AIT_GL'].aaDecision,
      }));
      expect(s.contexts).toBe(0);
      expect(s.probeLost).toBe(false);
      expect(s.decision).toBe('hook-off');
    } finally {
      await context.close();
    }
  });

  test('context 손실 → reload 1회, 다시 손실되면 overlay(루프 가드)', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, nav } = await openSynthetic(context, '?dpr=2');
      expect(await page.evaluate(() => window['__bound'])).toBe(true);
      expect(nav.count).toBe(1);

      await loseUnityContext(page);
      await expect.poll(() => nav.count, { timeout: 10000 }).toBe(2);
      await page.waitForFunction(() => !!window['__unityGl']);
      await page.waitForTimeout(1500);
      expect(nav.count).toBe(2); // reload 는 한 번뿐

      // 현재 DPR(2) 한 단계 아래로 tier 가 저장되고, 다음 부팅에 반영된다.
      const tier = await page.evaluate(() => ({ stored: JSON.parse(localStorage.getItem('__ait_gl_tier')), cap: window['__tierCap'] }));
      expect(tier.stored.cap).toBe(1.5);
      expect(tier.cap).toBe(1.5);

      // 120초 안 두 번째 손실 → reload 대신 안내 overlay
      await loseUnityContext(page);
      await expect(page.locator('#ait-gl-lost-overlay')).toBeVisible({ timeout: 10000 });
      await page.waitForTimeout(1000);
      expect(nav.count).toBe(2);
      await expect(page.locator('#ait-gl-lost-overlay')).toContainText('다시 시도');
    } finally {
      await context.close();
    }
  });

  test('hidden 이면 reload 를 미루고 visible 이 되면 한다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, nav, consoleLines } = await openSynthetic(context);
      await page.evaluate(() => {
        Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
      });
      await loseUnityContext(page);
      await page.waitForTimeout(800);
      expect(nav.count).toBe(1);
      expect(consoleLines.some((l) => l.includes('[AIT-GL] 백그라운드 상태'))).toBe(true);

      await page.evaluate(() => {
        Object.defineProperty(document, 'hidden', { configurable: true, get: () => false });
        document.dispatchEvent(new Event('visibilitychange'));
      });
      await expect.poll(() => nav.count, { timeout: 10000 }).toBe(2);
    } finally {
      await context.close();
    }
  });

  test('손실 이력 저장소를 쓸 수 없으면 reload 하지 않고 overlay 를 띄운다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      await context.addInitScript(() => {
        const orig = Storage.prototype.setItem;
        Storage.prototype.setItem = function (k, v) {
          if (k === '__ait_gl_loss__') throw new Error('blocked');
          return orig.call(this, k, v);
        };
      });
      const { page, nav } = await openSynthetic(context);
      await loseUnityContext(page);
      await expect(page.locator('#ait-gl-lost-overlay')).toBeVisible({ timeout: 10000 });
      await page.waitForTimeout(800);
      expect(nav.count).toBe(1);
    } finally {
      await context.close();
    }
  });

  test('glContextRecovery=false 이면 bindContextLoss 는 false, tierCap 은 0, reload 하지 않는다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, nav } = await openSynthetic(context, perfQuery({ glContextRecovery: false }));
      const s = await page.evaluate(() => ({ bound: window['__bound'], cap: window['__tierCap'] }));
      expect(s.bound).toBe(false);
      expect(s.cap).toBe(0);
      await loseUnityContext(page);
      await page.waitForTimeout(800);
      expect(nav.count).toBe(1);
      expect(await page.locator('#ait-gl-lost-overlay').count()).toBe(0);
    } finally {
      await context.close();
    }
  });

  test('tierCap: 저장값 24시간 만료, crashCount 2 → 1.5, 3 이상 → 1', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page } = await openSynthetic(context);
      expect(await page.evaluate(() => window['__tierCap'])).toBe(0);

      // 유효한 저장값
      await page.evaluate(() => localStorage.setItem('__ait_gl_tier', JSON.stringify({ cap: 1.5, ts: Date.now() })));
      expect(await page.evaluate(() => window['__AIT_GL'].tierCap())).toBe(1.5);
      // 만료된 저장값(25시간 전)은 무시하고 지운다
      await page.evaluate(() => localStorage.setItem('__ait_gl_tier', JSON.stringify({ cap: 1, ts: Date.now() - 25 * 3600 * 1000 })));
      expect(await page.evaluate(() => window['__AIT_GL'].tierCap())).toBe(0);
      expect(await page.evaluate(() => localStorage.getItem('__ait_gl_tier'))).toBeNull();
      // 손상된 값은 fail-open
      await page.evaluate(() => localStorage.setItem('__ait_gl_tier', '{not json'));
      expect(await page.evaluate(() => window['__AIT_GL'].tierCap())).toBe(0);

      // crashCount 기반, 저장값과는 낮은 쪽을 쓴다
      expect(await page.evaluate(() => { window['AITMemory'] = { crashCount: 1 }; return window['__AIT_GL'].tierCap(); })).toBe(0);
      expect(await page.evaluate(() => { window['AITMemory'] = { crashCount: 2 }; return window['__AIT_GL'].tierCap(); })).toBe(1.5);
      expect(await page.evaluate(() => { window['AITMemory'] = { crashCount: 3 }; return window['__AIT_GL'].tierCap(); })).toBe(1);
      expect(await page.evaluate(() => {
        window['AITMemory'] = { crashCount: 2 };
        localStorage.setItem('__ait_gl_tier', JSON.stringify({ cap: 1, ts: Date.now() }));
        return window['__AIT_GL'].tierCap();
      })).toBe(1);
    } finally {
      await context.close();
    }
  });

  test('?aitglprobe=1 이면 RT 인벤토리를 모으고, 없으면 rt 는 null', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const off = await openSynthetic(context);
      expect(await off.page.evaluate(() => window['__AIT_GL'].rt)).toBeNull();
      await off.page.close();

      const { page } = await openSynthetic(context, '?aitglprobe=1');
      const s = await page.evaluate(() => {
        const gl = window['__unityGl'];
        const rb = gl.createRenderbuffer();
        gl.bindRenderbuffer(gl.RENDERBUFFER, rb);
        gl.renderbufferStorageMultisample(gl.RENDERBUFFER, 4, gl.RGBA8, 256, 128);
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT16, 256, 128);
        const tex = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, tex);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 64, 64, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        // 픽셀 데이터가 있는 업로드는 인벤토리에 넣지 않는다
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 2, 2, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(16));
        return window['__AIT_GL'].rt.summary();
      });
      expect(s.rbMSCount).toBe(1);
      const ms = s.rows.find((r) => r.kind === 'rbMS');
      expect(ms.w).toBe(256);
      expect(ms.h).toBe(128);
      expect(ms.samples).toBe(4);
      expect(ms.bytes).toBe(256 * 128 * 4 * 4);
      expect(s.rows.some((r) => r.kind === 'rb' && r.w === 256)).toBe(true);
      expect(s.rows.filter((r) => r.kind === 'texNull').length).toBe(1);
    } finally {
      await context.close();
    }
  });
});

// ============================================================================
// 2) 빌드 산출물
// ============================================================================

function findProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(path.resolve(envPath, 'ait-build/dist/web'))) return envPath;
  const versions = ['6000.3', '6000.0', '6000.2', '2022.3', '2021.3'];
  for (const prefix of ['HeavySampleUnityProject', 'SampleUnityProject']) {
    for (const v of versions) {
      const p = path.resolve(__dirname, `../${prefix}-${v}`);
      if (fs.existsSync(path.resolve(p, 'ait-build/dist/web'))) return p;
    }
  }
  return null;
}

const PROJECT = findProject();
const AIT_BUILD = PROJECT ? path.resolve(PROJECT, 'ait-build') : '';
const BUILD_PORT = 4373;

function isPortAvailable(port) {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once('error', () => resolve(false));
    server.once('listening', () => server.close(() => resolve(true)));
    server.listen(port, '127.0.0.1');
  });
}
async function waitForPortRelease(port, timeoutMs = 8000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPortAvailable(port)) return true;
    await new Promise((r) => setTimeout(r, 200));
  }
  return false;
}
function freePort(port) {
  try {
    if (process.platform === 'win32') {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      // LISTEN 소켓만 kill: ESTABLISHED 소켓을 가진 Chrome 프로세스까지 kill되는 것을 막는다
      execSync(`lsof -ti tcp:${port} -sTCP:LISTEN | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {}
}
async function startProductionServer(aitBuildDir, defaultPort) {
  freePort(defaultPort);
  await waitForPortRelease(defaultPort, 5000);
  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(defaultPort)], {
      cwd: aitBuildDir, stdio: 'pipe', shell: true, env: { ...process.env, NODE_OPTIONS: '' },
    });
    let started = false;
    let actualPort = defaultPort;
    server.stdout.on('data', (data) => {
      const output = data.toString();
      const portMatch = output.match(/(?:Local:\s+http:\/\/localhost:|listening.*?port\s*|:)(\d+)/i);
      if (portMatch) actualPort = parseInt(portMatch[1], 10);
      if (/Local:|listening|Accepting connections|ready/.test(output) && !started) {
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });
    server.stderr.on('data', (data) => console.error('[vite preview error]', data.toString().trim()));
    server.on('error', reject);
    setTimeout(() => { if (!started) { started = true; resolve({ process: server, port: actualPort }); } }, 10000);
  });
}
async function killServer(proc, port) {
  if (!proc) return;
  try { proc.kill('SIGTERM'); } catch {}
  await new Promise((resolve) => {
    if (proc.exitCode !== null) return resolve(true);
    const t = setTimeout(() => resolve(false), 3000);
    proc.once('exit', () => { clearTimeout(t); resolve(true); });
  });
  freePort(port);
  await waitForPortRelease(port, 5000);
}

const UNITY_CONTEXT_WAIT_MS = 240000;

/** @param {import('@playwright/test').Page} page */
async function waitForUnityContext(page) {
  await page.waitForFunction(
    () => !!(window['__AIT_GL'] && window['__AIT_GL'].contexts && window['__AIT_GL'].contexts.some((c) => c.role === 'unity')),
    undefined,
    { timeout: UNITY_CONTEXT_WAIT_MS }
  );
}

test.describe('빌드 산출물', () => {
  test.skip(!PROJECT, 'ait-build/dist/web 산출물이 없어 건너뜁니다(UNITY_PROJECT_PATH 로 지정하거나 HeavySampleUnityProject-* 를 빌드하세요).');
  test.describe.configure({ mode: 'serial', timeout: 600000 });

  /** @type {{ process: any, port: number } | null} */
  let server = null;
  let baseUrl = '';

  test.beforeAll(async () => {
    server = await startProductionServer(AIT_BUILD, BUILD_PORT);
    baseUrl = `http://localhost:${server.port}/`;
    console.log(`[perf-gl] project=${PROJECT} url=${baseUrl}`);
  });
  test.afterAll(async () => {
    if (server) await killServer(server.process, server.port);
  });

  function mobileContextOptions() {
    return { userAgent: IPHONE_UA, deviceScaleFactor: 3, viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true };
  }

  test('모바일(DPR 3): requested/actual 기록, DPR 상한 2, [AIT-GL] 로그', async ({ browser }) => {
    const context = await browser.newContext(mobileContextOptions());
    try {
      const page = await context.newPage();
      const lines = [];
      page.on('console', (m) => { if (m.text().startsWith('[AIT-GL]')) lines.push(m.text()); });
      await page.goto(baseUrl, { waitUntil: 'commit' });
      await waitForUnityContext(page);

      const s = await page.evaluate(() => ({
        flags: window['__AIT_GL'].flags,
        requested: window['__AIT_GL'].requested,
        actual: window['__AIT_GL'].actual,
        samples: window['__AIT_GL'].samples,
        aaDecision: window['__AIT_GL'].aaDecision,
        cfgDpr: window['unityConfig'] && window['unityConfig'].devicePixelRatio,
        effDpr: window['__AIT_EFFECTIVE_DPR'],
        tier: window['__AIT_GL'].tierCap(),
      }));
      console.log(`  [perf-gl] ${JSON.stringify(s)}`);
      for (const l of lines.slice(0, 6)) console.log(`    ${l.slice(0, 400)}`);

      expect(s.flags.hook).toBe(true);
      expect(s.requested).toBeTruthy();
      expect(s.actual).toBeTruthy();
      expect(lines.some((l) => l.startsWith('[AIT-GL] context '))).toBe(true);
      // 자동 DPR 은 2 가 상한이다(tier 가 더 낮으면 그 값).
      expect(s.cfgDpr).toBeLessThanOrEqual(2);
      expect(s.effDpr).toBeLessThanOrEqual(2);

      if (process.env.PERF_GL_EXPECT_AA_DROP === '1') {
        expect(s.aaDecision).toBe('dropped');
        expect(s.actual.antialias).toBe(false);
        expect(s.samples).toBe(0);
      }
    } finally {
      await context.close();
    }
  });

  test('loseContext → reload 1회, 다시 손실되면 overlay', async ({ browser }) => {
    const context = await browser.newContext(mobileContextOptions());
    try {
      const page = await context.newPage();
      let navs = 0;
      page.on('framenavigated', (f) => { if (f === page.mainFrame()) navs++; });
      await page.goto(baseUrl, { waitUntil: 'commit' });
      await waitForUnityContext(page);
      const navsBefore = navs;

      const firstLossAt = Date.now();
      await page.evaluate(() => {
        const canvas = document.querySelector('#unity-canvas');
        const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
        gl.getExtension('WEBGL_lose_context').loseContext();
      });
      await expect.poll(() => navs, { timeout: 15000 }).toBe(navsBefore + 1);
      await waitForUnityContext(page);
      await page.waitForTimeout(1500);
      expect(navs).toBe(navsBefore + 1);

      if (Date.now() - firstLossAt > 110000) {
        test.info().annotations.push({ type: 'note', description: '재부팅이 120초 루프 가드 창을 넘겨 overlay 단언을 건너뜀' });
        return;
      }
      await page.evaluate(() => {
        const canvas = document.querySelector('#unity-canvas');
        const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
        gl.getExtension('WEBGL_lose_context').loseContext();
      });
      await expect(page.locator('#ait-gl-lost-overlay')).toBeVisible({ timeout: 15000 });
      await page.waitForTimeout(1000);
      expect(navs).toBe(navsBefore + 1);
    } finally {
      await context.close();
    }
  });

  test('?aitglprobe=1 RT 인벤토리(rbMS 행 확인)', async ({ browser }) => {
    const context = await browser.newContext(mobileContextOptions());
    try {
      const page = await context.newPage();
      await page.goto(`${baseUrl}?aitglprobe=1`, { waitUntil: 'commit' });
      await waitForUnityContext(page);
      // 첫 프레임 이후 RT 할당이 끝나도록 잠시 기다린다.
      await page.waitForTimeout(8000);
      const inv = await page.evaluate(() => {
        const gl = window['__AIT_GL'];
        return { rt: gl.rt ? gl.rt.summary() : null, aaDecision: gl.aaDecision, actual: gl.actual, samples: gl.samples };
      });
      expect(inv.rt, '?aitglprobe=1 인데 __AIT_GL.rt 가 없습니다').toBeTruthy();
      const rbMs = inv.rt.rows.filter((r) => r.kind === 'rbMS');
      console.log(`  [perf-gl] aa=${inv.aaDecision} actual=${JSON.stringify(inv.actual)} samples=${inv.samples} rbMS=${rbMs.length}행 합계=${(inv.rt.totalBytes / 1048576).toFixed(1)}MB`);
      for (const r of inv.rt.rows.slice(0, 10)) console.log(`    ${r.kind} ${r.w}x${r.h} fmt=${r.format} samples=${r.samples} x${r.count} = ${(r.bytes / 1048576).toFixed(2)}MB`);
      test.info().annotations.push({ type: 'rbMS', description: `${rbMs.length} rows` });
      if (process.env.PERF_GL_EXPECT_NO_RBMS === '1') {
        expect(rbMs, 'context antialias 를 끈 뒤 Unity 가 자체 MSAA RT(rbMS)를 만들었습니다').toHaveLength(0);
      }
    } finally {
      await context.close();
    }
  });
});
