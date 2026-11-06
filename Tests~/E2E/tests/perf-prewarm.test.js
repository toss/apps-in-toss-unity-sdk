// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { hasBuild, startFixtureServer, TEMPLATE_DIR } from './lib/prewarm-fixture.js';

/**
 * Apps in Toss Unity SDK — 프리웜(ait-prewarm.js) 검증
 *
 * 프리웜: 설정(빌드 시 index.html 주입)이 켜져 있고 페이지가 숨김 상태로 로드되면 wasm 다운로드·컴파일과 data 수신까지만 하고
 * 엔진 시작(createUnityInstance 이후)을 미룬다. 보이게 되거나 시작 신호(postMessage / AITPrewarm.start())가 오면 미리 컴파일한
 * Module 로 바로 인스턴스화한다. 켜져 있어도 보이는 상태로 로드되면 기존 경로와 같다.
 *
 * 두 부분으로 나뉜다.
 *
 * 1) 합성 페이지(synthetic): 진짜 Runtime/ait-prewarm.js 만 올린 가짜 페이지(빈 wasm 모듈 + 임의 data)로 상태 기계를 검증한다.
 *    진입 판단, 시작 트리거 3종, ready 신호(postMessage + ReactNativeWebView), 컴파일·data 실패 폴백, 대기 중 시작. 항상 실행된다.
 * 2) 빌드 산출물(build): 실제 Unity WebGL 산출물(dist/web)에 '현재 소스의' 템플릿(index.html + Runtime)을 치환해 얹어(lib/prewarm-fixture.js)
 *    엔진이 시작되지 않는지, 시작 신호 뒤 첫 프레임까지 걸리는 시간, 바로 로드한 TTFF 와의 비교, 대기 중 메모리를 잰다. 산출물이 없으면 건너뛴다.
 *
 * 숨김 로드 흉내: Playwright 페이지는 실제로는 보이는 상태라서, context init script 가 Document.prototype 의 visibilityState/hidden 게터를
 * 덮어쓰고 window.__setVis('visible') 로 값을 바꾸며 visibilitychange 를 직접 발행한다(CDP Emulation 은 문서 가시성을 바꾸는 API 가 없다).
 * 따라서 '숨김 상태에서 브라우저가 rAF/타이머를 멈추는 효과'는 재현되지 않는다 — 우리 코드가 숨김을 보고 하는 동작만 검증한다.
 *
 * 환경변수:
 *  - PREWARM_BUILD_DIR      : dist/web 경로. 미지정 시 UNITY_PROJECT_PATH/ait-build/dist/web → Heavy/SampleUnityProject-* 순으로 자동탐지.
 *  - PREWARM_ITERATIONS     : 측정 반복 횟수(기본 3, 중앙값).
 *  - PREWARM_THROTTLE_KBPS  : 지정하면 Build/* 응답을 그 속도(KB/s)로 흘린다(저속 망 흉내). 기본 무제한.
 *  - PREWARM_RESULT_PATH    : 지정하면 측정 결과 JSON 을 그 경로에 쓴다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PREWARM_JS = fs.readFileSync(path.join(TEMPLATE_DIR, 'Runtime/ait-prewarm.js'), 'utf8');

// 숨김 로드 흉내(위 주석). hiddenAtLoad 일 때만 설치한다.
const HIDDEN_INIT = `
(function () {
  var vis = 'hidden';
  Object.defineProperty(Document.prototype, 'visibilityState', { configurable: true, get: function () { return vis; } });
  Object.defineProperty(Document.prototype, 'hidden', { configurable: true, get: function () { return vis === 'hidden'; } });
  window.__setVis = function (v) { vis = v; document.dispatchEvent(new Event('visibilitychange')); };
})();`;

// 엔진 흔적 계측: WebAssembly.Memory 생성, AudioContext 생성, 호스트로 나간 메시지.
const PROBE_INIT = `
(function () {
  window.__probe = { memCount: 0, mems: [], acCount: 0, msgs: [] };
  try {
    var OM = WebAssembly.Memory;
    WebAssembly.Memory = new Proxy(OM, { construct: function (t, a) { var m = new t(a[0]); window.__probe.memCount++; window.__probe.mems.push(m); return m; } });
  } catch (e) {}
  ['AudioContext', 'webkitAudioContext'].forEach(function (n) {
    var O = window[n];
    if (!O) return;
    window[n] = new Proxy(O, { construct: function (t, a) { window.__probe.acCount++; return new t(...a); } });
  });
  window.addEventListener('message', function (e) { if (e.data && typeof e.data === 'object' && e.data.type) window.__probe.msgs.push(e.data); });
})();`;

// 호스트가 RN WebView 일 때의 postMessage 흉내. 합성 페이지에서만 쓴다 — 실제 빌드 페이지는 ReactNativeWebView 가 있으면
// 호스트 앱 안이라고 보고 PlayerPrefs 응답을 기다리므로(부팅 지연) 빌드 측정에는 넣지 않는다.
const RN_INIT = `window.__probe.msgs = window.__probe.msgs || [];
window.ReactNativeWebView = { postMessage: function (s) { window.__probe.msgs.push({ rn: String(s) }); } };`;

// ============================================================================
// 1) 합성 페이지
// ============================================================================

const ORIGIN = 'https://ait-prewarm.test';
// 빈 wasm 모듈(헤더만): compileStreaming 이 받아들이는 가장 작은 유효 모듈.
const EMPTY_WASM = Buffer.from([0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00]);
const DATA_BYTES = Buffer.alloc(300 * 1024, 7);

const SYNTH_HTML = `<!DOCTYPE html><html><head><meta charset="utf-8">
<script>
  var q = new URLSearchParams(location.search);
  window.__AIT_PREWARM_ENABLED = q.get('on') === '1';
  window.__gated = 0;
  window.__aitMark = function () {};
</script>
<script src="/ait-prewarm.js"></script>
</head><body><script>
  // index.html 이 하는 호출 순서를 흉내 낸다: begin → 엔진 시작 블록을 aitStartBoot 로 감싼다.
  window.__AIT_PREWARM.begin({ codeUrl: '/a.wasm', dataUrl: '/a.data' });
  var p = window.__AIT_PREWARM;
  var run = function () { window.__gated++; };
  if (p.isWaiting()) p.onStart(run); else run();
</script></body></html>`;

/**
 * @param {import('@playwright/test').BrowserContext} context
 * @param {{ hidden?: boolean, on?: boolean, wasm?: 'ok'|'404'|'bad'|'slow', data?: 'ok'|'404', rn?: boolean }} o
 */
async function openSynthetic(context, o = {}) {
  const hits = { wasm: 0, data: 0 };
  if (o.hidden !== false) await context.addInitScript(HIDDEN_INIT);
  await context.addInitScript(PROBE_INIT);
  await context.addInitScript(RN_INIT);
  await context.route(`${ORIGIN}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/ait-prewarm.js') return route.fulfill({ status: 200, contentType: 'application/javascript', body: PREWARM_JS });
    if (url.pathname === '/a.wasm') {
      hits.wasm++;
      if (o.wasm === '404') return route.fulfill({ status: 404, body: 'no' });
      if (o.wasm === 'bad') return route.fulfill({ status: 200, contentType: 'application/wasm', body: Buffer.from('not wasm at all') });
      if (o.wasm === 'slow') await new Promise((r) => setTimeout(r, 800));
      return route.fulfill({ status: 200, contentType: 'application/wasm', body: EMPTY_WASM });
    }
    if (url.pathname === '/a.data') {
      hits.data++;
      if (o.data === '404') return route.fulfill({ status: 404, body: 'no' });
      return route.fulfill({ status: 200, contentType: 'application/octet-stream', body: DATA_BYTES });
    }
    return route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: SYNTH_HTML });
  });
  const page = await context.newPage();
  await page.goto(`${ORIGIN}/index.html?on=${o.on === false ? 0 : 1}`);
  return { page, hits };
}

const phaseOf = (page) => page.evaluate(() => window['AITPrewarm'].getState().phase);
const waitReady = (page) => page.waitForFunction(() => window['AITPrewarm'].getState().phase === 'ready', null, { timeout: 15000 });

test.describe('ait-prewarm.js 합성 페이지', () => {
  test('숨김 로드: 엔진 시작 블록을 미루고, 컴파일·data 수신 뒤 ready 를 postMessage 와 ReactNativeWebView 로 낸다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, hits } = await openSynthetic(context);
      await waitReady(page);
      const r = await page.evaluate(() => ({
        gated: window['__gated'],
        st: window['AITPrewarm'].getState(),
        msgs: window['__probe'].msgs,
      }));
      expect(r.gated).toBe(0); // 시작 전에는 엔진 블록이 실행되지 않는다
      expect(r.st.waiting).toBe(true);
      expect(r.st.compiled).toBe(true);
      expect(r.st.dataReady).toBe(true);
      expect(r.st.dataBytes).toBe(DATA_BYTES.length);
      const post = r.msgs.find((m) => m.type === 'ait:prewarm:ready');
      expect(post).toBeTruthy();
      expect(post.compiled).toBe(true);
      expect(post.dataReady).toBe(true);
      const rn = r.msgs.find((m) => m.rn);
      expect(rn).toBeTruthy();
      expect(JSON.parse(rn.rn).type).toBe('ait:prewarm:ready');
      // ready 는 한 번만
      expect(r.msgs.filter((m) => m.type === 'ait:prewarm:ready').length).toBe(1);
      expect(hits.wasm).toBe(1);
      expect(hits.data).toBe(1);
    } finally {
      await context.close();
    }
  });

  for (const trigger of ['visible', 'message', 'api']) {
    test(`시작 트리거(${trigger}): 엔진 블록이 한 번 실행되고 Module/data 를 넘긴다`, async ({ browser }) => {
      const context = await browser.newContext();
      try {
        const { page, hits } = await openSynthetic(context);
        await waitReady(page);
        if (trigger === 'visible') await page.evaluate(() => window['__setVis']('visible'));
        else if (trigger === 'message') await page.evaluate(() => window.postMessage({ type: 'ait:prewarm:start' }, '*'));
        else expect(await page.evaluate(() => window['AITPrewarm'].start())).toBe(true);
        await page.waitForFunction(() => window['__gated'] === 1);
        // 두 번째 신호는 무시된다
        await page.evaluate(() => { window['__setVis']('visible'); window['AITPrewarm'].start(); });
        const r = await page.evaluate(async () => {
          const mod = await window['__AIT_PREWARM'].takeModule();
          const second = window['__AIT_PREWARM'].takeModule();
          // 로더의 data 요청: 선수신 본문이 정확한 Content-Length 로 서빙된다
          const res = await fetch('/a.data');
          const buf = await res.arrayBuffer();
          return {
            gated: window['__gated'],
            isModule: mod instanceof WebAssembly.Module,
            second,
            len: buf.byteLength,
            cl: res.headers.get('content-length'),
            st: window['AITPrewarm'].getState(),
          };
        });
        expect(r.gated).toBe(1);
        expect(r.isModule).toBe(true);
        expect(r.second).toBeNull(); // 한 번만 넘긴다
        expect(r.len).toBe(DATA_BYTES.length);
        expect(r.cl).toBe(String(DATA_BYTES.length));
        expect(r.st.phase).toBe('started');
        expect(r.st.startSource).toBe(trigger === 'visible' ? 'visible' : trigger);
        expect(r.st.dataServed).toBe(true);
        expect(r.st.moduleUsed).toBe(true);
        expect(hits.data).toBe(1); // 로더의 요청이 네트워크로 다시 나가지 않았다
        expect(hits.wasm).toBe(1);
      } finally {
        await context.close();
      }
    });
  }

  test('보이는 상태로 로드: 대기 없이 즉시 실행, wasm/data 요청 없음(켜져 있어도 기존 경로)', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, hits } = await openSynthetic(context, { hidden: false });
      const r = await page.evaluate(() => ({ gated: window['__gated'], st: window['AITPrewarm'].getState(), tm: window['__AIT_PREWARM'].takeModule() }));
      expect(r.gated).toBe(1); // 동기 실행
      expect(r.st.phase).toBe('off');
      expect(r.st.reason).toBe('visible-at-load');
      expect(r.st.waiting).toBe(false);
      expect(r.tm).toBeNull();
      expect(await page.evaluate(() => window['AITPrewarm'].start())).toBe(false);
      expect(hits.wasm + hits.data).toBe(0);
      expect((await page.evaluate(() => window['__probe'].msgs)).filter((m) => m.type === 'ait:prewarm:ready').length).toBe(0);
    } finally {
      await context.close();
    }
  });

  test('설정 꺼짐 + 숨김 로드: 대기하지 않고 즉시 실행(기존 동작)', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, hits } = await openSynthetic(context, { on: false });
      const r = await page.evaluate(() => ({ gated: window['__gated'], st: window['AITPrewarm'].getState() }));
      expect(r.gated).toBe(1);
      expect(r.st.phase).toBe('off');
      expect(r.st.enabled).toBe(false);
      expect(r.st.reason).toBe('disabled');
      expect(hits.wasm + hits.data).toBe(0);
      // 시작 신호가 와도 아무 일도 없다
      await page.evaluate(() => { window.postMessage({ type: 'ait:prewarm:start' }, '*'); window['__setVis']('visible'); });
      expect(await page.evaluate(() => window['__gated'])).toBe(1);
    } finally {
      await context.close();
    }
  });

  for (const wasm of /** @type {const} */ (['404', 'bad'])) {
    test(`wasm 선컴파일 실패(${wasm}): Module 을 보관하지 않고 ready 는 compiled=false 로 나간다`, async ({ browser }) => {
      const context = await browser.newContext();
      try {
        const { page } = await openSynthetic(context, { wasm });
        await waitReady(page);
        const r = await page.evaluate(async () => ({
          st: window['AITPrewarm'].getState(),
          ready: window['__probe'].msgs.find((m) => m.type === 'ait:prewarm:ready'),
        }));
        expect(r.st.compiled).toBe(false);
        expect(r.st.compileError).not.toBe('');
        expect(r.st.dataReady).toBe(true);
        expect(r.ready.compiled).toBe(false);
        await page.evaluate(() => window['AITPrewarm'].start());
        await page.waitForFunction(() => window['__gated'] === 1);
        expect(await page.evaluate(async () => await window['__AIT_PREWARM'].takeModule())).toBeNull(); // 정상 경로로 폴백
      } finally {
        await context.close();
      }
    });
  }

  test('data 수신 실패: 보관 없이 로더의 요청이 원래 경로(네트워크)로 간다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page, hits } = await openSynthetic(context, { data: '404' });
      await waitReady(page);
      const st = await page.evaluate(() => window['AITPrewarm'].getState());
      expect(st.dataReady).toBe(false);
      expect(st.dataError).toContain('404');
      expect(st.compiled).toBe(true);
      await page.evaluate(() => window['AITPrewarm'].start());
      const status = await page.evaluate(async () => (await fetch('/a.data')).status);
      expect(status).toBe(404);
      expect(hits.data).toBe(2); // 선수신 1회 + 로더 요청 1회(재사용 불가라 네트워크로 위임)
    } finally {
      await context.close();
    }
  });

  test('준비가 끝나기 전에 시작: ready 신호는 내지 않고, takeModule 이 진행 중인 컴파일을 기다린다', async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const { page } = await openSynthetic(context, { wasm: 'slow' });
      expect(await phaseOf(page)).toBe('preparing');
      await page.evaluate(() => window['AITPrewarm'].start());
      await page.waitForFunction(() => window['__gated'] === 1);
      const r = await page.evaluate(async () => {
        const mod = await window['__AIT_PREWARM'].takeModule();
        return { isModule: mod instanceof WebAssembly.Module, msgs: window['__probe'].msgs.filter((m) => m.type === 'ait:prewarm:ready').length };
      });
      expect(r.isModule).toBe(true);
      expect(r.msgs).toBe(0);
    } finally {
      await context.close();
    }
  });
});

// ============================================================================
// 2) 빌드 산출물
// ============================================================================

function findBuildDir() {
  const candidates = [process.env.PREWARM_BUILD_DIR];
  if (process.env.UNITY_PROJECT_PATH) candidates.push(path.join(process.env.UNITY_PROJECT_PATH, 'ait-build/dist/web'));
  for (const prefix of ['HeavySampleUnityProject', 'SampleUnityProject']) {
    for (const v of ['6000.3', '6000.0', '6000.6', '2022.3', '2021.3']) {
      candidates.push(path.resolve(__dirname, `../${prefix}-${v}/ait-build/dist/web`));
    }
  }
  return candidates.find((c) => c && hasBuild(c)) || '';
}

const BUILD_DIR = findBuildDir();
const ITERATIONS = Math.max(1, parseInt(process.env.PREWARM_ITERATIONS || '3', 10));
const THROTTLE = parseInt(process.env.PREWARM_THROTTLE_KBPS || '0', 10) || 0;
const FRAME_TIMEOUT_MS = 90000;

const median = (a) => { const s = a.filter((v) => typeof v === 'number').sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : null; };

/**
 * @param {import('@playwright/test').Browser} browser
 * @param {string} url
 * @param {{ hidden: boolean }} o
 */
async function newBuildPage(browser, url, o) {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  if (o.hidden) await context.addInitScript(HIDDEN_INIT);
  await context.addInitScript(PROBE_INIT);
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto(`${url}/index.html`);
  return { context, page, errors };
}

const marksOf = (page) => page.evaluate(() => {
  const o = {};
  for (const m of performance.getEntriesByType('mark')) if (m.name.startsWith('ait:')) o[m.name.slice(4)] = Math.round(m.startTime);
  return o;
});
const waitFirstFrame = (page) => page.waitForFunction(() => performance.getEntriesByName('ait:first-frame').length > 0, null, { timeout: FRAME_TIMEOUT_MS });

/** CDP 가 있는 Chromium 만 JS 힙을 잰다. WebKit 은 null. */
async function jsHeapBytes(page, browserName) {
  if (browserName !== 'chromium') return null;
  const cdp = await page.context().newCDPSession(page);
  try {
    await cdp.send('Performance.enable');
    await cdp.send('HeapProfiler.collectGarbage').catch(() => {});
    const { metrics } = await cdp.send('Performance.getMetrics');
    const m = metrics.find((x) => x.name === 'JSHeapUsedSize');
    return m ? m.value : null;
  } catch { return null; } finally { await cdp.detach().catch(() => {}); }
}
/** 엔진이 만든 wasm 힙 크기(Module.HEAPU8). 엔진이 없으면 0 — 대기 중에는 0 이어야 한다. */
const wasmMemBytes = (page) => page.evaluate(() => {
  const u = window['unityInstance'];
  const fromEngine = u && u.Module && u.Module.HEAPU8 ? u.Module.HEAPU8.length : 0;
  const fromHook = window['__probe'].mems.reduce((s, m) => s + m.buffer.byteLength, 0);
  return Math.max(fromEngine, fromHook);
});

test.describe('프리웜 (빌드 산출물)', () => {
  test.skip(!BUILD_DIR, '빌드 산출물(dist/web)이 없다 — PREWARM_BUILD_DIR 지정');
  test.setTimeout(900000);

  /** @type {Awaited<ReturnType<typeof startFixtureServer>>} */
  let on, off;
  test.beforeAll(async () => {
    if (!BUILD_DIR) return;
    on = await startFixtureServer(BUILD_DIR, { prewarm: true, throttleKBps: THROTTLE });
    off = await startFixtureServer(BUILD_DIR, { prewarm: false, throttleKBps: THROTTLE });
  });
  test.afterAll(async () => {
    await on?.close();
    await off?.close();
  });

  test('설정 꺼짐 + 숨김 로드: 프리웜 없이 기존처럼 엔진이 바로 시작된다', async ({ browser }) => {
    const { context, page } = await newBuildPage(browser, off.url, { hidden: true });
    try {
      // 숨김인데도 시작 신호 없이 wasm 인스턴스화·런타임 초기화까지 간다(첫 프레임은 ait-pacing 의 hidden 게이트가 보류한다 — 기존 동작).
      await page.waitForFunction(() => performance.getEntriesByName('ait:runtime-init').length > 0, null, { timeout: FRAME_TIMEOUT_MS });
      const marks = await marksOf(page);
      expect(Object.keys(marks).some((k) => k.startsWith('prewarm'))).toBe(false);
      expect(marks['wasm-instantiated']).toBeGreaterThan(0);
      const st = await page.evaluate(() => window['AITPrewarm'].getState());
      expect(st.enabled).toBe(false);
      expect(st.phase).toBe('off');
      await page.evaluate(() => window['__setVis']('visible'));
      await waitFirstFrame(page);
    } finally {
      await context.close();
    }
  });

  test('설정 켜짐 + 보이는 상태로 로드: 프리웜 마크 없이 기존 경로로 부팅한다', async ({ browser }) => {
    const { context, page } = await newBuildPage(browser, on.url, { hidden: false });
    try {
      await waitFirstFrame(page);
      const marks = await marksOf(page);
      expect(Object.keys(marks).some((k) => k.startsWith('prewarm'))).toBe(false);
      const st = await page.evaluate(() => window['AITPrewarm'].getState());
      expect(st.enabled).toBe(true);
      expect(st.phase).toBe('off');
      expect(st.reason).toBe('visible-at-load');
    } finally {
      await context.close();
    }
  });

  for (const trigger of ['visible', 'message', 'api']) {
    test(`설정 켜짐 + 숨김 로드 (${trigger} 시작): 대기 중 엔진·오디오 없음 → 신호 뒤 첫 프레임`, async ({ browser, browserName }) => {
      const { context, page, errors } = await newBuildPage(browser, on.url, { hidden: true });
      try {
        await page.waitForFunction(() => window['AITPrewarm'].getState().phase === 'ready', null, { timeout: 60000 });
        // 엔진이 시작됐다면 이 사이에 일어났을 일을 기다려 본다.
        await page.waitForTimeout(1500);
        const waiting = await page.evaluate(() => ({
          unity: !!window['unityInstance'],
          probe: { mem: window['__probe'].memCount, ac: window['__probe'].acCount, ready: window['__probe'].msgs.filter((m) => m.type === 'ait:prewarm:ready').length, },
          st: window['AITPrewarm'].getState(),
        }));
        const waitMarks = await marksOf(page);
        expect(waiting.unity).toBe(false);
        for (const k of ['prerun', 'runtime-init', 'engine-main', 'first-frame', 'wasm-instantiated']) expect(waitMarks[k], `대기 중 ${k} 마크`).toBeUndefined();
        expect(waiting.probe.mem).toBe(0); // 엔진이 wasm Memory 를 만들지 않았다
        expect(waiting.probe.ac).toBe(0);
        expect(waiting.probe.ready).toBe(1);
        expect(waiting.st.compiled).toBe(true);
        expect(waiting.st.dataReady).toBe(true);
        const heapWait = await jsHeapBytes(page, browserName);

        // 메시지/API 시작은 문서 가시성을 바꾸지 않는다 — 엔진은 시작하지만 첫 프레임은 ait-pacing 의 hidden 게이트가 보류하므로,
        // 시작이 확인된 직후 가시 전환을 따로 보낸다(실기기에서 호스트가 WebView 를 화면에 올리는 동작).
        const t = await page.evaluate(async (trig) => {
          const t0 = performance.now();
          if (trig === 'visible') {
            window['__setVis']('visible');
          } else {
            if (trig === 'message') window.postMessage({ type: 'ait:prewarm:start' }, '*');
            else window['AITPrewarm'].start();
            while (window['AITPrewarm'].getState().phase !== 'started') await new Promise((r) => setTimeout(r, 1));
            window['__setVis']('visible');
          }
          return t0;
        }, trigger);
        await waitFirstFrame(page);
        const marks = await marksOf(page);
        const startToFrame = marks['first-frame'] - t;
        expect(marks['prewarm-start']).toBeGreaterThanOrEqual(Math.floor(t));
        expect(await page.evaluate(() => window['AITPrewarm'].getState().startSource)).toBe(trigger);
        expect(await page.evaluate(() => window['AITPrewarm'].getState().moduleUsed)).toBe(true);
        expect(await page.evaluate(() => window['AITPrewarm'].getState().dataServed)).toBe(true);
        console.log(`[prewarm] ${browserName} ${trigger}: 시작→첫 프레임 ${Math.round(startToFrame)}ms, 대기 중 JS 힙 ${heapWait === null ? 'n/a' : (heapWait / 1048576).toFixed(1) + 'MB'}, wasm Memory ${await wasmMemBytes(page)}B(시작 후)`);
        expect(errors.filter((e) => !/appsInTossGlobals/.test(e))).toEqual([]);
      } finally {
        await context.close();
      }
    });
  }

  test('시작 뒤 wasm·data 를 다시 내려받지 않는다(요청 수 1회씩)', async ({ browser }) => {
    const server = await startFixtureServer(BUILD_DIR, { prewarm: true, throttleKBps: THROTTLE });
    const { context, page } = await newBuildPage(browser, server.url, { hidden: true });
    try {
      await page.waitForFunction(() => window['AITPrewarm'].getState().phase === 'ready', null, { timeout: 60000 });
      await page.evaluate(() => window['__setVis']('visible'));
      await waitFirstFrame(page);
      expect(server.requests.filter((p) => /\.wasm(\.gz|\.br)?$/.test(p)).length).toBe(1);
      expect(server.requests.filter((p) => /\.data(\.gz|\.br)?$/.test(p)).length).toBe(1);
    } finally {
      await context.close();
      await server.close();
    }
  });

  test('측정: 바로 로드한 TTFF vs 시작 신호→첫 프레임, 대기 중 메모리', async ({ browser, browserName }) => {
    /** @type {Record<string, number[]>} */
    const s = { directOff: [], directOn: [], startToFrame: [], prewarmReadyAt: [] };
    const heap = { wait: /** @type {number[]} */ ([]), after: /** @type {number[]} */ ([]), baselineLoadedDirect: /** @type {number[]} */ ([]) };
    const wasmMem = { wait: 0, after: /** @type {number[]} */ ([]) };
    for (let i = 0; i < ITERATIONS; i++) {
      for (const [key, srv] of /** @type {const} */ ([['directOff', off], ['directOn', on]])) {
        const { context, page } = await newBuildPage(browser, srv.url, { hidden: false });
        try {
          await waitFirstFrame(page);
          const marks = await marksOf(page);
          s[key].push(marks['first-frame']);
          if (key === 'directOff') {
            await page.waitForTimeout(500);
            const h = await jsHeapBytes(page, browserName);
            if (h !== null) heap.baselineLoadedDirect.push(h);
          }
        } finally { await context.close(); }
      }
      const { context, page } = await newBuildPage(browser, on.url, { hidden: true });
      try {
        await page.waitForFunction(() => window['AITPrewarm'].getState().phase === 'ready', null, { timeout: 60000 });
        s.prewarmReadyAt.push(await page.evaluate(() => window['AITPrewarm'].getState().readyAt));
        await page.waitForTimeout(1000);
        const hw = await jsHeapBytes(page, browserName);
        if (hw !== null) heap.wait.push(hw);
        wasmMem.wait = await wasmMemBytes(page);
        const t = await page.evaluate(() => { const t0 = performance.now(); window['__setVis']('visible'); return t0; });
        await waitFirstFrame(page);
        const marks = await marksOf(page);
        s.startToFrame.push(marks['first-frame'] - t);
        await page.waitForTimeout(500);
        const ha = await jsHeapBytes(page, browserName);
        if (ha !== null) heap.after.push(ha);
        wasmMem.after.push(await wasmMemBytes(page));
      } finally { await context.close(); }
    }
    const result = {
      browser: browserName, iterations: ITERATIONS, throttleKBps: THROTTLE,
      ttffDirectOffMs: median(s.directOff), ttffDirectOnMs: median(s.directOn),
      startToFirstFrameMs: median(s.startToFrame), prewarmReadyAtMs: median(s.prewarmReadyAt),
      raw: s,
      waitJsHeapMB: heap.wait.length ? median(heap.wait) / 1048576 : null,
      afterStartJsHeapMB: heap.after.length ? median(heap.after) / 1048576 : null,
      loadedDirectJsHeapMB: heap.baselineLoadedDirect.length ? median(heap.baselineLoadedDirect) / 1048576 : null,
      waitWasmMemoryBytes: wasmMem.wait, afterStartWasmMemoryBytes: median(wasmMem.after),
    };
    console.log('[prewarm-result] ' + JSON.stringify(result));
    if (process.env.PREWARM_RESULT_PATH) {
      const p = process.env.PREWARM_RESULT_PATH.replace(/(\.json)?$/, `.${browserName}.json`);
      fs.writeFileSync(p, JSON.stringify(result, null, 2));
    }
    expect(result.startToFirstFrameMs).not.toBeNull();
    expect(result.waitWasmMemoryBytes).toBe(0);
    // 프리웜의 이득: 시작 신호 뒤 첫 프레임이 바로 로드한 TTFF 보다 빨라야 한다.
    expect(result.startToFirstFrameMs).toBeLessThan(/** @type {number} */ (result.ttffDirectOnMs));
  });
});
