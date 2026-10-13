// @ts-check
import { test, expect, chromium, webkit } from '@playwright/test';
import * as http from 'http';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { emitPageCacheScript, emitEarlyFetchModern, emitEarlyFetchLegacy, readRuntimeScript } from './lib/emitted-scripts.js';

/**
 * Apps in Toss Unity SDK — 저메모리 tier(P0-4)와 WebKit 지연 페이지 캐시 put(P0-3) 검증
 *
 * 대상:
 *  - AITPageCacheEmitter 가 내보내는 페이지 캐시 인터셉터(지연 put, 저메모리 tier put 생략)
 *  - WebGLBuildCopier.EarlyFetch 의 legacy/modern 인라인 스크립트(data 선시작 생략, 지연 put)
 *  - 템플릿 런타임 ait-mem.js(tier 결정), ait-gl.js(DPR 상한), ait-databuf.js(data 요청 보류)
 *
 * Unity 빌드 없이 돈다. C# 생성기의 verbatim 문자열 상수를 lib/emitted-scripts.js 가 그대로 읽어 조립하므로, 생성기 소스를 고치면
 * 이 테스트가 곧바로 새 출력을 본다(조립 결과는 mono 로 컴파일한 실제 생성기 출력과 바이트 단위로 같음을 확인했다).
 *
 * 실제 엔진으로 검증한다: WebKit(Playwright webkit)과 Chromium 을 각각 직접 띄우고, localhost HTTP 서버를 둔다(secure context + 진짜 HTTP 캐시).
 * 엔진을 못 띄우는 환경(webkit 미설치 등)에서는 해당 엔진의 테스트만 건너뛴다.
 *
 * 핵심 단언:
 *  - WebKit(자동): 부팅 중 Response.clone 0회, Cache.put 0회. 첫 프레임 + 지연 뒤 only-if-cached 로 다시 읽어 put. wasm 은 put 대상이 아니다.
 *  - HTTP 캐시 미스(no-store): put 을 생략하고 네트워크 요청이 늘지 않는다(재다운로드 0).
 *  - tier 1: 엔진과 무관하게 부팅 중 clone/put 0, 첫 프레임 + 지연 뒤 한 파일씩 only-if-cached 재읽기 put(크기 상한·메모리 압력 가드, 네트워크 추가 0, wasm 제외), 재방문은 캐시 서빙. data 선시작은 생략.
 *  - tier 2: put 생략, data 선시작 생략.
 *  - 페이지 캐시 스니펫이 계산하는 tier(__aitPeekLowTier)와 ait-mem.js 의 AITMemory.lowMemTier 가 같은 시드에서 같다.
 */

const INDEX_HTML = fs.readFileSync(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../WebGLTemplates/AITTemplate/index.html'), 'utf8');

/**
 * index.html 의 자동 DPR 코드(aitGlTierCap ~ AIT-AUTO-DPR:END: AIT_AUTO_DPR_CAP, getOptimalDevicePixelRatio, computeAutoDevicePixelRatio)를 그대로 잘라 쓴다.
 * 이 구간 앞에 aitDevicePixelRatioSetting 만 있으면 단독으로 실행된다.
 */
const DPR_CODE = (() => {
  const b = INDEX_HTML.indexOf('function aitGlTierCap()');
  const endMarker = '/* AIT-AUTO-DPR:END */';
  const e = INDEX_HTML.indexOf(endMarker);
  if (b < 0 || e < b) throw new Error('index.html 에서 자동 DPR 코드 구간을 찾지 못했다');
  return INDEX_HTML.slice(b, e + endMarker.length);
})();

/**
 * 기기 환경을 흉내 내는 init script(엔진 독립: navigator/screen/window 의 getter 를 덮는다).
 * @param {{ ua: string, screen: [number, number], dpr: number, memory?: number, cores?: number }} d
 */
function deviceSpoof(d) {
  return `(() => {
    const def = (o, k, v) => { try { Object.defineProperty(o, k, { get: () => v, configurable: true }); } catch (e) {} };
    def(navigator, 'userAgent', ${JSON.stringify(d.ua)});
    def(navigator, 'deviceMemory', ${JSON.stringify(d.memory)});
    def(navigator, 'hardwareConcurrency', ${d.cores === undefined ? 6 : d.cores});
    def(screen, 'width', ${d.screen[0]});
    def(screen, 'height', ${d.screen[1]});
    def(window, 'devicePixelRatio', ${d.dpr});
  })();`;
}

const IOS_UA = (/** @type {string} */ ver, /** @type {string} */ device = 'iPhone') =>
  `Mozilla/5.0 (${device}; CPU ${device} OS ${ver} like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/${ver.split('_')[0]}.0 Mobile/15E148 Safari/604.1`;
const ANDROID_UA = 'Mozilla/5.0 (Linux; Android 13; SM-G991N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36';

// ============================================================================
// 서버: localhost HTTP(secure context) + 요청 기록
// ============================================================================

let tokenSeq = 0;
/** 테스트마다 파일명을 바꿔 HTTP 캐시가 테스트 사이에서 공유되지 않게 한다. */
function newToken() {
  return `t${Date.now().toString(36)}${(tokenSeq++).toString(36)}`;
}

/** @param {number} n */
function patternBytes(n) {
  const b = Buffer.alloc(n);
  for (let i = 0; i < n; i++) b[i] = (i * 31 + 7) & 0xff;
  return b;
}
const DATA_BYTES = patternBytes(300 * 1024);
const FRAMEWORK_JS = Buffer.from('/* framework */\nwindow.__fw = 1;\n' + '// pad\n'.repeat(4000));
const WASM_BYTES = Buffer.concat([Buffer.from([0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00]), Buffer.alloc(64 * 1024)]);

async function startServer() {
  /** @type {Map<string, string>} */
  const pages = new Map();
  /** @type {{ path: string, conditional: boolean }[]} */
  const hits = [];
  const state = { noStore: false };
  const server = http.createServer((req, res) => {
    const p = (req.url || '/').split('?')[0];
    if (p.endsWith('.html') && pages.has(p)) {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
      res.end(pages.get(p));
      return;
    }
    if (p.startsWith('/Build/')) {
      const conditional = !!(req.headers['if-none-match'] || req.headers['if-modified-since']);
      hits.push({ path: p, conditional });
      const body = p.endsWith('.wasm') ? WASM_BYTES : p.endsWith('.js') ? FRAMEWORK_JS : DATA_BYTES;
      const type = p.endsWith('.wasm') ? 'application/wasm' : p.endsWith('.js') ? 'application/javascript' : 'application/octet-stream';
      res.writeHead(200, {
        'Content-Type': type,
        'Content-Length': String(body.length),
        'Cache-Control': state.noStore ? 'no-store' : 'public, max-age=3600',
      });
      res.end(body);
      return;
    }
    res.writeHead(404);
    res.end('not found');
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve(undefined)));
  const addr = /** @type {import('net').AddressInfo} */ (server.address());
  // WebKit 은 localhost 를 secure context 로 취급한다.
  const origin = `http://localhost:${addr.port}`;
  return {
    origin,
    pages,
    hits,
    state,
    /** @param {string} suffix */
    count(suffix) { return hits.filter((h) => h.path.endsWith(suffix)).length; },
    close() { return new Promise((resolve) => server.close(() => resolve(undefined))); },
  };
}

// ============================================================================
// 페이지 구성
// ============================================================================

/**
 * 모든 페이지 스크립트보다 먼저: Response.clone / Cache.put 호출 기록, localStorage 시드(seed), sessionStorage 시드(session).
 * @param {{ seed?: Record<string, string>, session?: Record<string, string> }} opt
 */
function initScript(opt) {
  const seed = opt.seed || {};
  const session = opt.session || {};
  return `(() => {
    window.__spy = { clones: 0, puts: [], putTimes: [], inflight: 0, maxInflight: 0 };
    try {
      const c = Response.prototype.clone;
      Response.prototype.clone = function () { window.__spy.clones++; return c.apply(this, arguments); };
    } catch (e) {}
    try {
      const p = Cache.prototype.put;
      // 동시에 진행 중인 put 수(maxInflight)와 시작 시각(putTimes, performance.now 기준)을 기록한다. 반환값은 원래 Promise 그대로다.
      Cache.prototype.put = function (req) {
        const spy = window.__spy;
        spy.puts.push(String((req && req.url) || req));
        spy.putTimes.push(performance.now());
        spy.inflight++;
        if (spy.inflight > spy.maxInflight) spy.maxInflight = spy.inflight;
        const r = p.apply(this, arguments);
        const done = () => { spy.inflight--; };
        try { Promise.resolve(r).then(done, done); } catch (e) { done(); }
        return r;
      };
    } catch (e) {}
    try {
      // 네트워크 저장(networkPut)은 cache.add 를 쓴다. 저장 횟수로 put 과 함께 센다(네이티브 add 는 JS put 을 거치지 않는다).
      const a = Cache.prototype.add;
      Cache.prototype.add = function (req) { window.__spy.puts.push(String((req && req.url) || req)); return a.apply(this, arguments); };
    } catch (e) {}
    try {
      const seed = ${JSON.stringify(seed)};
      for (const k of Object.keys(seed)) { if (localStorage.getItem(k) === null) localStorage.setItem(k, seed[k]); }
    } catch (e) {}
    try {
      const session = ${JSON.stringify(session)};
      for (const k of Object.keys(session)) { if (sessionStorage.getItem(k) === null) sessionStorage.setItem(k, session[k]); }
    } catch (e) {}
  })();`;
}

/** 로더가 하는 fetch 순서를 흉내 낸다(framework → data → wasm). @param {string} token */
function bootScript(token) {
  return `window.__boot = async () => {
    const out = {};
    for (const f of ['Build/${token}.framework.js', 'Build/${token}.data', 'Build/${token}.wasm']) {
      const r = await fetch(f);
      out[f] = (await r.arrayBuffer()).byteLength;
    }
    return out;
  };`;
}

/**
 * @param {Awaited<ReturnType<typeof startServer>>} server
 * @param {string} token
 * @param {{ perf?: object, head?: string[] }} opt head 스크립트(JS 본문) 목록은 __AIT_PERF 다음에 순서대로 들어간다.
 */
function registerPage(server, token, opt) {
  const perf = JSON.stringify(opt.perf || {});
  const heads = (opt.head || []).map((js) => `<script>${js}</script>`).join('\n');
  server.pages.set(`/${token}.html`, `<!doctype html><html><head><meta charset="utf-8">
<script>window.__AIT_PERF = ${perf};</script>
${heads}
<script>${bootScript(token)}</script>
</head><body>harness</body></html>`);
  return `${server.origin}/${token}.html`;
}

/** @param {import('@playwright/test').Page} page */
async function fireFirstFrame(page) {
  await page.evaluate(() => { window['__ffAt'] = performance.now(); window.dispatchEvent(new Event('ait:firstframe')); });
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {string} expr
 * @param {number} [timeout]
 */
async function waitFor(page, expr, timeout = 15000) {
  await page.waitForFunction(expr, undefined, { timeout });
}

const ENGINES = /** @type {const} */ (['webkit', 'chromium']);

// ============================================================================
// 엔진별 스위트
// ============================================================================

for (const engine of ENGINES) {
  test.describe(`[${engine}] 저메모리 tier / 지연 put`, () => {
    /** @type {import('@playwright/test').Browser | null} */
    let browser = null;
    /** @type {Awaited<ReturnType<typeof startServer>>} */
    let server;
    let launchError = '';
    let webkitReady = false;

    test.beforeAll(async () => {
      server = await startServer();
      try {
        if (engine === 'webkit') {
          // 가용성 확인만 한다(실제 페이지는 newPage 가 영속 컨텍스트로 띄운다 — 아래 설명).
          const probe = await webkit.launch({ channel: undefined, args: [] });
          await probe.close();
          webkitReady = true;
        } else {
          try { browser = await chromium.launch({ channel: 'chrome' }); } catch { browser = await chromium.launch(); }
        }
      } catch (e) {
        launchError = String(e && /** @type {Error} */ (e).message || e).split('\n')[0]; console.log('[launch-error]', engine, String(e && /** @type {Error} */ (e).message || e));
      }
    });

    test.afterAll(async () => {
      if (browser) await browser.close();
      if (server) await server.close();
    });

    test.beforeEach(() => {
      test.skip(!browser && !webkitReady, `${engine} 를 띄울 수 없음: ${launchError}`);
      server.hits.length = 0;
      server.state.noStore = false;
    });

    /** @param {{ seed?: Record<string, string>, session?: Record<string, string> }} [opt] */
    async function newPage(opt = {}) {
      // Playwright 의 WebKit 은 비영속(incognito) 컨텍스트에서 HTTP 캐시가 아예 꺼져 있어(force-cache 도 서버로 간다) only-if-cached 가 항상 실패한다.
      // 실제 Safari/WKWebView 처럼 디스크 캐시가 있는 영속 컨텍스트를 테스트마다 새 임시 디렉터리로 띄운다.
      // args: [] 는 playwright.config 의 Chromium 전용 launch 플래그(--enable-webgl 등)를 비운다 — WebKit 은 모르는 옵션이면 뜨자마자 종료한다.
      // userAgent: undefined 는 playwright.config 의 Desktop Chrome UA 기본값을 덮어 엔진 고유 UA 를 쓰게 한다(UA 로 엔진을 판정하는 코드 검증용).
      const context = engine === 'webkit'
        ? await webkit.launchPersistentContext(fs.mkdtempSync(path.join(os.tmpdir(), 'ait-wk-')), { channel: undefined, args: [], userAgent: undefined })
        : await /** @type {import('@playwright/test').Browser} */ (browser).newContext({ userAgent: undefined });
      await context.addInitScript(initScript(opt));
      const page = await context.newPage();
      return { context, page };
    }

    const NOW = () => Date.now();
    /** @param {number} tier */
    const tierSeed = (tier) => ({ __ait_lowmem_v1: JSON.stringify({ tier, ts: NOW() }) });

    // ------------------------------------------------------------------
    test('페이지 캐시 자동: WebKit 만 부팅 중 clone/put 없이 지연, 첫 프레임 뒤 only-if-cached 로 put (wasm 제외, 네트워크 추가 0)', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheDeferDelayMs: 300 },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage();
      try {
        await page.goto(url);
        const info0 = await page.evaluate(() => JSON.parse(JSON.stringify(window['__aitCacheDeferred'])));
        expect(info0.webkit, 'UA 판정').toBe(engine === 'webkit');
        expect(info0.enabled, '자동(-1)은 WebKit 만 지연').toBe(engine === 'webkit');

        await page.evaluate(() => window['__boot']());
        // 첫 프레임 신호 전에는 지연 작업이 돌지 않는다.
        await page.waitForTimeout(700);
        expect(server.count(`${token}.data`)).toBe(1);
        expect(server.count(`${token}.framework.js`)).toBe(1);
        expect(server.count(`${token}.wasm`)).toBe(1);

        if (engine !== 'webkit') {
          // Chromium: 지연 없이 부팅 중 put(기존 경로).
          await waitFor(page, `window.__aitCacheStats.puts.length >= 2`);
          const info = await page.evaluate(() => JSON.parse(JSON.stringify(window['__aitCacheDeferred'])));
          expect(info.queued).toEqual([]);
          return;
        }

        const before = await page.evaluate(() => ({
          clones: window['__spy'].clones,
          puts: window['__spy'].puts.length,
          info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])),
        }));
        expect(before.clones, '부팅 중 Response.clone 0회').toBe(0);
        expect(before.puts, '부팅 중 Cache.put 0회').toBe(0);
        expect(before.info.queued.map((/** @type {string} */ u) => u.replace(server.origin, '')).sort()).toEqual([
          `/Build/${token}.data`,
          `/Build/${token}.framework.js`,
        ]);
        expect(before.info.queued.some((/** @type {string} */ u) => u.endsWith('.wasm')), 'wasm 은 큐에 없다').toBe(false);

        await fireFirstFrame(page);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const after = await page.evaluate(() => ({
          puts: window['__spy'].puts.slice(),
          info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])),
        }));
        expect(after.info.errors).toEqual([]);
        // HTTP 캐시가 있으면 put, 없으면(엔진이 only-if-cached 를 미스로 돌려주면) 생략. 어느 쪽이든 재다운로드는 없다.
        expect(after.info.put.length + after.info.miss.length).toBe(2);
        expect(after.puts.length).toBe(after.info.put.length);
        expect(after.puts.some((/** @type {string} */ u) => u.endsWith('.wasm')), 'wasm 은 put 되지 않는다').toBe(false);
        expect(server.count(`${token}.data`), '지연 put 이 data 를 다시 받지 않는다').toBe(1);
        expect(server.count(`${token}.framework.js`)).toBe(1);
        expect(server.count(`${token}.wasm`)).toBe(1);
        // 이 테스트 서버는 max-age=3600 이므로 WebKit 의 HTTP 캐시가 동작하면 두 파일 모두 put 되어야 한다.
        expect(after.info.put.length, `HTTP 캐시 히트 put 수(miss=${after.info.miss.length})`).toBe(2);
        const keys = await page.evaluate(async () => {
          const names = await caches.keys();
          const out = [];
          for (const n of names) { for (const r of await (await caches.open(n)).keys()) out.push(r.url); }
          return out;
        });
        expect(keys.filter((u) => u.endsWith('.wasm'))).toEqual([]);
        expect(keys.length).toBe(2);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('지연 put 강제(pageCacheDeferredPut=1): 엔진과 무관하게 동작하고 wasm 은 제외된다', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheDeferDelayMs: 200, pageCacheDeferredPut: 1 },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage();
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        const before = await page.evaluate(() => ({ clones: window['__spy'].clones, puts: window['__spy'].puts.length }));
        expect(before).toEqual({ clones: 0, puts: 0 });
        await fireFirstFrame(page);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const info = await page.evaluate(() => JSON.parse(JSON.stringify(window['__aitCacheDeferred'])));
        expect(info.enabled).toBe(true);
        expect(info.put.length + info.miss.length).toBe(2);
        expect(info.put.some((/** @type {string} */ u) => u.endsWith('.wasm'))).toBe(false);
        expect(server.count(`${token}.data`)).toBe(1);
        expect(server.count(`${token}.wasm`)).toBe(1);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('HTTP 캐시 미스(no-store): 첫 프레임+지연 뒤에만 네트워크로 한 파일씩 받아 저장한다(wasm 제외, 부팅 중 요청 증가 0)', async () => {
      server.state.noStore = true;
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheDeferDelayMs: 300, pageCacheDeferredPut: 1 },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage();
      const logs = [];
      page.on('console', (m) => { const t = m.text(); if (/\[AIT-PageCache\]/.test(t)) logs.push(t); });
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        expect(server.hits.length, '부팅 fetch 3건').toBe(3);
        // 첫 프레임 신호 전에는 네트워크 폴백도 없다.
        await page.waitForTimeout(700);
        expect(server.hits.length, '첫 프레임 전 추가 요청 없음').toBe(3);
        await fireFirstFrame(page);
        await page.waitForTimeout(100);
        expect(server.hits.length, '지연(300ms) 이전에는 추가 요청 없음').toBe(3);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const info = await page.evaluate(() => JSON.parse(JSON.stringify(window['__aitCacheDeferred'])));
        expect(info.errors).toEqual([]);
        // no-store 응답에 대한 only-if-cached 동작은 엔진·플랫폼마다 다르다(macOS WebKit 은 미스 → 폴백, Linux WebKit 은 이 모드를 무시하고 네트워크로 받는다).
        // 그래서 폴백 건수 대신 불변식을 본다: put 2건, 미스 0, 부팅 뒤 추가 요청은 data/framework 파일당 최대 1건, wasm 은 다시 받지 않는다.
        const net = /** @type {string[]} */ (info.net);
        expect(info.put.length).toBe(2);
        expect(info.miss).toEqual([]);
        expect(net.some((u) => u.endsWith('.wasm'))).toBe(false);
        expect(server.count(`${token}.data`), 'data 추가 요청은 최대 1건').toBeLessThanOrEqual(2);
        expect(server.count(`${token}.framework.js`), 'framework 추가 요청은 최대 1건').toBeLessThanOrEqual(2);
        expect(server.count(`${token}.wasm`)).toBe(1);
        expect(server.hits.length).toBeGreaterThanOrEqual(3 + net.length);
        expect(server.hits.length).toBeLessThanOrEqual(5);
        const puts = await page.evaluate(() => window['__spy'].puts.length);
        expect(puts).toBe(2);
        if (net.length > 0) expect(logs.some((l) => /HTTP 캐시 miss → 네트워크로 저장 /.test(l))).toBe(true);
        else test.info().annotations.push({ type: 'note', description: 'only-if-cached 가 no-store 응답을 찾아 네트워크 폴백이 실행되지 않음' });
        expect(logs.some((l) => new RegExp(`지연 put 완료: put=2 net=${net.length} miss=0`).test(l))).toBe(true);
        const keys = await page.evaluate(async () => {
          const out = [];
          for (const n of await caches.keys()) { for (const r of await (await caches.open(n)).keys()) out.push(r.url); }
          return out;
        });
        expect(keys.length).toBe(2);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('HTTP 캐시 미스(no-store) + tier>=1: 네트워크 저장도 하지 않는다(요청 증가 0)', async () => {
      server.state.noStore = true;
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheDeferDelayMs: 100, pageCacheDeferredPut: 1 },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage({ seed: tierSeed(1) });
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        await fireFirstFrame(page);
        await page.waitForTimeout(800);
        expect(server.hits.length, 'tier>=1 은 첫 프레임 뒤에도 추가 요청 없음').toBe(3);
        expect(await page.evaluate(() => window['__spy'].puts.length)).toBe(0);
        expect(await page.evaluate(() => window['__aitCacheDeferred'].net.length)).toBe(0);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('tier 2: 지연/즉시 어느 쪽이든 put 을 만들지 않는다(clone 0, put 0, 큐 없음)', async () => {
      for (const deferFlag of [-1, 1, 0]) {
        server.hits.length = 0;
        const token = newToken();
        const url = registerPage(server, token, {
          perf: { pageCacheDeferDelayMs: 100, pageCacheDeferredPut: deferFlag },
          head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
        });
        const { context, page } = await newPage({ seed: tierSeed(2) });
        try {
          await page.goto(url);
          expect(await page.evaluate(() => window['__aitPeekLowTier']())).toBe(2);
          await page.evaluate(() => window['__boot']());
          await fireFirstFrame(page);
          await page.waitForTimeout(800);
          const r = await page.evaluate(() => ({
            clones: window['__spy'].clones,
            puts: window['__spy'].puts.length,
            statPuts: window['__aitCacheStats'].puts.length,
            queued: window['__aitCacheDeferred'].queued.length,
            skipped: window['__aitCacheDeferred'].skipped.filter((/** @type {string} */ s) => s.startsWith('lowmem:')).length,
          }));
          expect(r.clones, `clone (deferFlag=${deferFlag})`).toBe(0);
          expect(r.puts, `put (deferFlag=${deferFlag})`).toBe(0);
          expect(r.statPuts).toBe(0);
          expect(r.queued, 'tier 2 는 큐에도 넣지 않는다').toBe(0);
          expect(r.skipped).toBeGreaterThan(0);
        } finally {
          await context.close();
        }
      }
    });

    // ------------------------------------------------------------------
    // tier 1 지연 put: 부팅 중 본문 이중 보관 없음(clone 0, put 0) → 첫 프레임 + 지연 뒤 한 파일씩 only-if-cached 재읽기 → 재방문은 캐시 서빙.
    /** @type {{ name: string, seed: Record<string, string>, spoof?: { ua: string, screen: [number, number], dpr: number, memory?: number } }[]} */
    const TIER1_VARIANTS = [
      { name: '사망 이력으로 저장된 tier 1', seed: tierSeed(1) },
      { name: 'RAM 부족 기기 하한 tier 1 (Android deviceMemory 2, 저장값 없음)', seed: {}, spoof: { ua: ANDROID_UA, screen: [412, 915], dpr: 2.625, memory: 2 } },
    ];
    for (const v of TIER1_VARIANTS) {
      test(`tier 1 지연 put (${v.name}): 부팅 중 clone/put 0, 첫 프레임+지연 뒤 한 파일씩 put, 재다운로드 0, 재방문은 캐시 서빙`, async () => {
        const token = newToken();
        const url = registerPage(server, token, {
          perf: { pageCacheLowTierDeferDelayMs: 400 },
          head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
        });
        const { context, page } = await newPage({ seed: v.seed });
        if (v.spoof) await context.addInitScript(deviceSpoof(v.spoof));
        try {
          await page.goto(url);
          expect(await page.evaluate(() => window['__aitPeekLowTier']()), 'tier').toBe(1);
          const info0 = await page.evaluate(() => JSON.parse(JSON.stringify(window['__aitCacheDeferred'])));
          expect(info0.lowTierPut).toBe(true);
          expect(info0.lowDelayMs).toBe(400);

          await page.evaluate(() => window['__boot']());
          // 첫 프레임 신호 전: 본문을 clone/put 으로 한 번 더 들고 있지 않는다(피크 중복 보관 없음). 큐에는 URL 만 있다.
          await page.waitForTimeout(700);
          const before = await page.evaluate(() => ({
            clones: window['__spy'].clones,
            puts: window['__spy'].puts.length,
            info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])),
          }));
          expect(before.clones, '부팅 중 Response.clone 0회').toBe(0);
          expect(before.puts, '부팅 중 Cache.put 0회').toBe(0);
          expect(before.info.state, '첫 프레임 전에는 무장만 한다').toBe('armed');
          expect(before.info.queued.map((/** @type {string} */ u) => u.replace(server.origin, '')).sort()).toEqual([
            `/Build/${token}.data`,
            `/Build/${token}.framework.js`,
          ]);
          expect(before.info.skipped.some((/** @type {string} */ s) => s.startsWith('lowmem:')), 'tier 1 은 lowmem 건너뜀이 아니라 지연 put 이다').toBe(false);
          expect(server.count(`${token}.data`)).toBe(1);
          expect(server.count(`${token}.framework.js`)).toBe(1);
          expect(server.count(`${token}.wasm`)).toBe(1);

          await fireFirstFrame(page);
          await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
          const after = await page.evaluate(() => ({
            clones: window['__spy'].clones,
            puts: window['__spy'].puts.slice(),
            putTimes: window['__spy'].putTimes.slice(),
            maxInflight: window['__spy'].maxInflight,
            ffAt: window['__ffAt'],
            info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])),
          }));
          expect(after.info.errors).toEqual([]);
          expect(after.info.net, '네트워크로 다시 받아 저장하지 않는다').toEqual([]);
          expect(after.info.put.length, `HTTP 캐시 히트 put 수(miss=${after.info.miss.length})`).toBe(2);
          expect(after.puts.length).toBe(2);
          expect(after.puts.some((/** @type {string} */ u) => u.endsWith('.wasm')), 'wasm 은 put 되지 않는다').toBe(false);
          expect(after.clones, '지연 put 도 clone 을 만들지 않는다').toBe(0);
          expect(after.maxInflight, 'put 은 한 번에 하나씩').toBe(1);
          for (const t of after.putTimes) expect(t - after.ffAt, 'put 은 첫 프레임 + 지연(400ms) 뒤').toBeGreaterThanOrEqual(350);
          expect(server.count(`${token}.data`), '지연 put 이 data 를 다시 받지 않는다').toBe(1);
          expect(server.count(`${token}.framework.js`)).toBe(1);
          expect(server.count(`${token}.wasm`)).toBe(1);

          // 재방문: 같은 컨텍스트에서 다시 탐색하면 data/framework 는 CacheStorage 에서 서빙된다.
          await page.goto(url);
          await page.evaluate(() => window['__boot']());
          const revisit = await page.evaluate(() => ({
            hits: window['__aitCacheStats'].hits.map((/** @type {string} */ u) => u.split('/').pop()),
            misses: window['__aitCacheStats'].misses.map((/** @type {string} */ u) => u.split('/').pop()),
            puts: window['__spy'].puts.length,
          }));
          expect(revisit.hits.sort()).toEqual([`${token}.data`, `${token}.framework.js`].sort());
          expect(revisit.misses.filter((/** @type {string} */ f) => !f.endsWith('.wasm')), 'data/framework 는 캐시 미스가 없다(wasm 은 tier 1 에서 저장하지 않는다)').toEqual([]);
          expect(server.count(`${token}.data`), '재방문 data 네트워크 0').toBe(1);
          expect(server.count(`${token}.framework.js`), '재방문 framework 네트워크 0').toBe(1);
        } finally {
          await context.close();
        }
      });
    }

    // ------------------------------------------------------------------
    test('tier 1 지연 put: pageCacheLowTierPut=false 면 종전처럼 put 을 만들지 않는다(큐 없음)', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheLowTierDeferDelayMs: 100, pageCacheLowTierPut: false },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage({ seed: tierSeed(1) });
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        await fireFirstFrame(page);
        await page.waitForTimeout(600);
        const r = await page.evaluate(() => ({ clones: window['__spy'].clones, puts: window['__spy'].puts.length, info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])) }));
        expect(r.clones).toBe(0);
        expect(r.puts).toBe(0);
        expect(r.info.queued).toEqual([]);
        expect(r.info.skipped.filter((/** @type {string} */ s) => s.startsWith('lowmem:')).length).toBeGreaterThan(0);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('tier 1 지연 put: 메모리 압력이 high 이상이면 저장하지 않는다(재다운로드 0)', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheLowTierDeferDelayMs: 100 },
        head: [
          `window.AITMemory = { lowMemTier: 1, bootStage: 'boot-start', getLevel: function () { return 'high'; } };`,
          emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` }),
        ],
      });
      const { context, page } = await newPage();
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        await fireFirstFrame(page);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const r = await page.evaluate(() => ({ puts: window['__spy'].puts.length, info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])) }));
        expect(r.puts).toBe(0);
        expect(r.info.put).toEqual([]);
        expect(r.info.skipped.filter((/** @type {string} */ s) => s.startsWith('pressure:')).length, '큐의 두 파일 모두 압력으로 건너뜀').toBe(2);
        expect(server.hits.length, '추가 요청 없음').toBe(3);
      } finally {
        await context.close();
      }
    });

    test('tier 1 지연 put: 첫 파일을 저장한 뒤 압력이 high 로 오르면 남은 파일은 포기한다', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheLowTierDeferDelayMs: 100 },
        head: [
          `window.AITMemory = { lowMemTier: 1, bootStage: 'boot-start', getLevel: function () { return (window.__spy && window.__spy.puts.length >= 1) ? 'high' : 'ok'; } };`,
          emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` }),
        ],
      });
      const { context, page } = await newPage();
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        await fireFirstFrame(page);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const r = await page.evaluate(() => ({ puts: window['__spy'].puts.length, info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])) }));
        expect(r.puts, '압력이 오르기 전 첫 파일만 저장').toBe(1);
        expect(r.info.skipped.filter((/** @type {string} */ s) => s.startsWith('pressure:')).length).toBe(1);
        expect(server.hits.length, '추가 요청 없음').toBe(3);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('tier 1 지연 put: 크기 상한(pageCacheLowTierMaxBytes)을 넘는 파일은 읽지도 저장하지도 않는다', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        // data 300KB 는 넘고 framework(약 30KB)는 안 넘는 상한.
        perf: { pageCacheLowTierDeferDelayMs: 100, pageCacheLowTierMaxBytes: 100 * 1024 },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage({ seed: tierSeed(1) });
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        await fireFirstFrame(page);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const r = await page.evaluate(() => ({ puts: window['__spy'].puts.slice(), info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])) }));
        expect(r.info.skipped.filter((/** @type {string} */ s) => s.startsWith('size:')).map((/** @type {string} */ s) => s.replace(server.origin, ''))).toEqual([`size:/Build/${token}.data`]);
        expect(r.puts.map((/** @type {string} */ u) => u.replace(server.origin, ''))).toEqual([`/Build/${token}.framework.js`]);
        expect(server.hits.length, '추가 요청 없음').toBe(3);
        const keys = await page.evaluate(async () => {
          const out = [];
          for (const n of await caches.keys()) { for (const r2 of await (await caches.open(n)).keys()) out.push(r2.url); }
          return out;
        });
        expect(keys.map((u) => u.replace(server.origin, ''))).toEqual([`/Build/${token}.framework.js`]);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('tier 1 + 레거시 early-fetch: 완결 버퍼 즉시 put 대신 페이지 캐시 지연 put 큐로만 저장한다(wasm 제외, data 선시작 없음)', async () => {
      const token = newToken();
      const urls = [`Build/${token}.data`, `Build/${token}.wasm`, `Build/${token}.framework.js`];
      const url = registerPage(server, token, {
        perf: { pageCacheLowTierDeferDelayMs: 300 },
        head: [
          emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` }),
          emitEarlyFetchLegacy({ urls, kickUrls: urls.slice(0, 2), cacheName: `ait-unity-${token}`, wasmUrl: `Build/${token}.wasm`, dataUrl: `Build/${token}.data` }),
        ],
      });
      const { context, page } = await newPage({ seed: tierSeed(1) });
      try {
        await page.goto(url);
        await page.waitForTimeout(500);
        expect(server.count(`${token}.data`), 'tier 1: data 선시작 없음').toBe(0);
        await page.evaluate(() => window['__boot']());
        await page.waitForTimeout(500);
        const before = await page.evaluate(() => ({ clones: window['__spy'].clones, puts: window['__spy'].puts.length }));
        expect(before, '부팅 중 clone/put 없음').toEqual({ clones: 0, puts: 0 });
        await fireFirstFrame(page);
        await waitFor(page, `window.__spy.puts.length >= 1`);
        await waitFor(page, `window.__aitCacheDeferred.state === 'done'`);
        const r = await page.evaluate(() => ({ puts: window['__spy'].puts.slice(), maxInflight: window['__spy'].maxInflight, info: JSON.parse(JSON.stringify(window['__aitCacheDeferred'])) }));
        expect(r.puts.some((/** @type {string} */ u) => u.endsWith('.wasm')), 'wasm 은 저장하지 않는다').toBe(false);
        expect(r.puts.some((/** @type {string} */ u) => u.endsWith('.data')), 'data 는 지연 put 으로 저장').toBe(true);
        expect(r.maxInflight).toBe(1);
        expect(r.info.net).toEqual([]);
        expect(server.count(`${token}.data`), '재다운로드 0').toBe(1);
        expect(server.count(`${token}.wasm`)).toBe(1);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('tier 0 컨트롤: 같은 설정에서 put 이 생긴다(위 단언이 우연히 통과하는 게 아님을 보인다)', async () => {
      const token = newToken();
      const url = registerPage(server, token, {
        perf: { pageCacheDeferDelayMs: 100, pageCacheDeferredPut: 0 },
        head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
      });
      const { context, page } = await newPage();
      try {
        await page.goto(url);
        await page.evaluate(() => window['__boot']());
        await waitFor(page, `window.__aitCacheStats.puts.length >= 2`);
        const puts = await page.evaluate(() => window['__spy'].puts.length);
        expect(puts).toBeGreaterThanOrEqual(2);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    const PEEK_SEEDS = (() => {
      const now = NOW();
      const MIN = 60000;
      const H = 3600000;
      const memFg = { __ait_mem_v1: JSON.stringify({ phase: 'fg', crashes: 0, bgKills: 0 }) };
      /** @type {{ name: string, seed: Record<string, string>, session?: Record<string, string>, perf?: object, expected: number }[]} */
      const list = [
        { name: '마커 없음', seed: {}, expected: 0 },
        { name: '직전 부팅 사망(boot-start) 1회는 설명 근거가 없어 올리지 않는다', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, expected: 0 },
        { name: '직전 사망 + 최근 실패 1회(10분 내 2회) → 1', seed: { __ait_boot_v1: JSON.stringify({ stage: 'first-frame', t: now - 2000, fails: [now - MIN] }) }, expected: 1 },
        { name: '직전 사망 + 최근 실패 2회(10분 내 3회) → 2', seed: { __ait_boot_v1: JSON.stringify({ stage: 'first-frame', t: now - 2000, fails: [now - MIN, now - 2 * MIN] }) }, expected: 2 },
        { name: '직전 사망 + prev.lvl=1 → 1', seed: { __ait_boot_v1: JSON.stringify({ stage: 'first-frame', t: now - 2000, lvl: 1 }) }, expected: 1 },
        { name: '직전 사망 + sessionStorage phase=fg → 1', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, session: memFg, expected: 1 },
        { name: 'sessionStorage phase=fg 만 있고 부팅 마커가 없으면 0', seed: {}, session: memFg, expected: 0 },
        { name: '직전 사망 + phase=fg + 저장 tier 1 → 2', seed: { ...tierSeedAt(1, now - 1000), __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, session: memFg, expected: 2 },
        { name: '직전 사망 + phase=bg 는 근거가 아니다 → 0', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, session: { __ait_mem_v1: JSON.stringify({ phase: 'bg' }) }, expected: 0 },
        { name: 'stable + post.end 비어 있음 + post.lvl=2 → 늦은 사망 → 1', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, lvl: 2, post: { end: '', t: now - 3000, lvl: 2 } }) }, expected: 1 },
        { name: 'stable + post.end 비어 있음 + post.lvl=0 → 0', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, lvl: 0, post: { end: '', t: now - 3000, lvl: 0 } }) }, expected: 0 },
        { name: 'stable + post.end 비어 있음 + post.lvl=0 + phase=fg → 1', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, post: { end: '', t: now - 3000, lvl: 0 } }) }, session: memFg, expected: 1 },
        { name: 'stable + post.end=bg + post.lvl=2 → 0', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, lvl: 2, post: { end: 'bg', t: now - 3000, lvl: 2 } }) }, expected: 0 },
        { name: 'stable + post.end=exit + phase=fg → 0', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, post: { end: 'exit', t: now - 3000, lvl: 2 } }) }, session: memFg, expected: 0 },
        { name: 'stable + 늦은 사망 + 저장 tier 2 → 2 유지', seed: { ...tierSeedAt(2, now - 1000), __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, post: { end: '', t: now - 3000, lvl: 2 } }) }, expected: 2 },
        { name: '직전 stable 이면 실패 기록 무시', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, fails: [now - 1000] }) }, expected: 0 },
        { name: '저장 tier 2(24시간 이내)', seed: tierSeedAt(2, now - 1000), expected: 2 },
        { name: '저장 tier 1 이 24시간 지남', seed: tierSeedAt(1, now - 25 * H), expected: 0 },
        { name: '사망(설명 안 됨) + 저장 tier 1 → 1 유지', seed: { ...tierSeedAt(1, now - 1000), __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, expected: 1 },
        { name: '정상 종료(end=exit)는 사망이 아님', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', end: 'exit', t: now - 2000 }) }, expected: 0 },
        { name: '10분 창 밖 실패 2회', seed: { __ait_boot_v1: JSON.stringify({ fails: [now - 11 * MIN, now - 12 * MIN] }) }, expected: 0 },
        { name: 'lowMemoryTier=false 면 항상 0', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000, lvl: 2 }) }, session: memFg, perf: { lowMemoryTier: false }, expected: 0 },
      ];
      return list;
    })();

    for (const c of PEEK_SEEDS) {
      test(`tier 결정 일치(페이지 캐시 peek == ait-mem.js): ${c.name}`, async () => {
        const token = newToken();
        const url = registerPage(server, token, {
          perf: c.perf || {},
          head: [
            emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` }),
            'window.__pre = window.__aitPeekLowTier();',
            readRuntimeScript('ait-mem.js'),
            'window.__post = window.AITMemory && window.AITMemory.lowMemTier;',
          ],
        });
        const { context, page } = await newPage({ seed: c.seed, session: c.session });
        try {
          await page.goto(url);
          const r = await page.evaluate(() => ({ pre: window['__pre'], post: window['__post'], after: window['__aitPeekLowTier']() }));
          expect(r.pre, 'ait-mem.js 이전 peek').toBe(c.expected);
          expect(r.post, 'AITMemory.lowMemTier').toBe(c.expected);
          expect(r.after, 'ait-mem.js 이후 peek').toBe(c.expected);
        } finally {
          await context.close();
        }
      });
    }

    // ------------------------------------------------------------------
    for (const hidden of [true, false]) {
      test(`stable 타이머: 페이지가 ${hidden ? '숨김' : '표시'} 상태면 post.end 를 '${hidden ? 'bg' : ''}' 로 기록`, async () => {
        const token = newToken();
        const url = registerPage(server, token, {
          perf: { bootStableMs: 50 },
          head: [readRuntimeScript('ait-mem.js')],
        });
        const { context, page } = await newPage();
        if (hidden) {
          // Android WebView 는 숨겨져도 타이머가 돈다. stable 시점에 숨김이었던 세션을 흉내 낸다.
          await context.addInitScript(`(() => {
            Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
            Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
          })();`);
        }
        try {
          await page.goto(url);
          await page.evaluate(() => window['AITMemory'].markFirstFrame());
          await page.waitForTimeout(150);
          const boot = await page.evaluate(() => JSON.parse(localStorage.getItem('__ait_boot_v1') || 'null'));
          expect(boot && boot.stage).toBe('stable');
          expect(boot.post.end).toBe(hidden ? 'bg' : '');
        } finally {
          await context.close();
        }
      });
    }

    // ------------------------------------------------------------------
    test('modern early-fetch: tier 0 은 data 를 선시작하고 tier>=1 은 data 선시작을 생략(wasm 은 그대로)', async () => {
      for (const tier of [0, 1]) {
        server.hits.length = 0;
        const token = newToken();
        const kick = [`Build/${token}.data`, `Build/${token}.wasm`];
        const url = registerPage(server, token, {
          head: [emitEarlyFetchModern(kick, `Build/${token}.data`)],
        });
        const { context, page } = await newPage({ seed: tier ? tierSeed(tier) : {} });
        try {
          await page.goto(url);
          await page.waitForTimeout(700);
          expect(server.count(`${token}.wasm`), `wasm 선시작 (tier=${tier})`).toBe(1);
          expect(server.count(`${token}.data`), `data 선시작 (tier=${tier})`).toBe(tier ? 0 : 1);
          // 로더가 나중에 data 를 요청하면 그때 받는다(래퍼가 살아 있어 위임 — 요청 정확히 1건).
          await page.evaluate(async (t) => { await (await fetch(`Build/${t}.data`)).arrayBuffer(); }, token);
          expect(server.count(`${token}.data`)).toBe(1);
        } finally {
          await context.close();
        }
      }
    });

    // ------------------------------------------------------------------
    test('legacy early-fetch: tier>=1 은 data 선시작과 put 을 생략, WebKit 자동은 지연 put(wasm 제외, plain 버퍼)', async () => {
      // tier 1
      {
        server.hits.length = 0;
        const token = newToken();
        const urls = [`Build/${token}.data`, `Build/${token}.wasm`, `Build/${token}.framework.js`];
        const url = registerPage(server, token, {
          perf: { pageCacheDeferDelayMs: 100 },
          head: [emitEarlyFetchLegacy({ urls, kickUrls: urls.slice(0, 2), cacheName: `ait-unity-${token}`, wasmUrl: `Build/${token}.wasm`, dataUrl: `Build/${token}.data` })],
        });
        const { context, page } = await newPage({ seed: tierSeed(1) });
        try {
          await page.goto(url);
          await page.waitForTimeout(700);
          expect(server.count(`${token}.data`), 'tier 1: data 선시작 없음').toBe(0);
          expect(server.count(`${token}.wasm`), 'wasm 은 선시작').toBe(1);
          await page.evaluate(() => window['__boot']());
          expect(server.count(`${token}.data`)).toBe(1);
          await fireFirstFrame(page);
          await page.waitForTimeout(800);
          const puts = await page.evaluate(() => window['__spy'].puts.length);
          expect(puts, 'tier 1: put 없음').toBe(0);
        } finally {
          await context.close();
        }
      }
      // tier 0 자동
      {
        server.hits.length = 0;
        const token = newToken();
        const urls = [`Build/${token}.data`, `Build/${token}.wasm`, `Build/${token}.framework.js`];
        const url = registerPage(server, token, {
          perf: { pageCacheDeferDelayMs: 200 },
          head: [emitEarlyFetchLegacy({ urls, kickUrls: urls.slice(0, 2), cacheName: `ait-unity-${token}`, wasmUrl: `Build/${token}.wasm`, dataUrl: `Build/${token}.data` })],
        });
        const { context, page } = await newPage();
        try {
          await page.goto(url);
          await page.evaluate(() => window['__boot']());
          if (engine === 'webkit') {
            await page.waitForTimeout(600);
            const before = await page.evaluate(() => window['__spy'].puts.slice());
            expect(before, 'WebKit: 부팅 중 put 없음(첫 프레임 전)').toEqual([]);
            await fireFirstFrame(page);
            await waitFor(page, `window.__spy.puts.length >= 2`);
            const puts = await page.evaluate(() => window['__spy'].puts.slice());
            expect(puts.some((/** @type {string} */ u) => u.endsWith('.wasm')), 'wasm 은 put 하지 않는다').toBe(false);
            expect(puts.some((/** @type {string} */ u) => u.endsWith('.data'))).toBe(true);
            expect(server.count(`${token}.data`), '지연 put 이 data 를 다시 받지 않는다').toBe(1);
          } else {
            await waitFor(page, `window.__spy.puts.length >= 1`);
            const puts = await page.evaluate(() => window['__spy'].puts.slice());
            expect(puts.some((/** @type {string} */ u) => u.endsWith('.data')), 'Chromium: 부팅 중 put(기존 경로)').toBe(true);
          }
        } finally {
          await context.close();
        }
      }
    });

    // ------------------------------------------------------------------
    test('ait-databuf.js: tier>=1 이면 data GET 을 wasm 컴파일 완료까지 보류하고 tier 0 은 즉시 보낸다', async () => {
      for (const tier of [0, 1]) {
        server.hits.length = 0;
        const token = newToken();
        const url = registerPage(server, token, {
          head: [
            readRuntimeScript('ait-mem.js'),
            // 실제 컴파일 대신 수동 해제 가능한 스텁(컴파일 완료 시점을 테스트가 정한다).
            `window.__release = null;
             WebAssembly.instantiate = function () { return new Promise(function (r) { window.__release = function () { r({ instance: {}, module: {} }); }; }); };
             WebAssembly.instantiateStreaming = undefined;`,
            readRuntimeScript('ait-databuf.js'),
            `window.__AIT_DATABUF.configure({ dataUrl: 'Build/${token}.data' });`,
          ],
        });
        const { context, page } = await newPage({ seed: tier ? tierSeed(tier) : {} });
        try {
          await page.goto(url);
          expect(await page.evaluate(() => window['AITMemory'].lowMemTier)).toBe(tier);
          // 로더의 data 요청.
          const done = page.evaluate(async (t) => { const r = await fetch(`Build/${t}.data`); return (await r.arrayBuffer()).byteLength; }, token);
          await page.waitForTimeout(900);
          if (tier === 0) {
            expect(server.count(`${token}.data`), 'tier 0: 보류 없음').toBe(1);
            expect(await done).toBe(DATA_BYTES.length);
            expect(await page.evaluate(() => window['__AIT_DATABUF'].getState().deferInstalled)).toBe(false);
            continue;
          }
          expect(server.count(`${token}.data`), 'tier 1: 컴파일 전에는 data 요청이 서버에 가지 않는다').toBe(0);
          expect(await page.evaluate(() => window['__AIT_DATABUF'].getState().deferred)).toBe(1);
          // 1MB 이상 바이트 배열 instantiate 가 성공하면 풀린다.
          await page.evaluate(() => { WebAssembly.instantiate(new Uint8Array(1.5 * 1024 * 1024), {}); window['__release'](); });
          expect(await done).toBe(DATA_BYTES.length);
          expect(server.count(`${token}.data`)).toBe(1);
          const st = await page.evaluate(() => window['__AIT_DATABUF'].getState());
          expect(st.compiled).toBe(true);
          expect(st.deferReason).toBe('compiled');
        } finally {
          await context.close();
        }
      }
    });


    // ------------------------------------------------------------------
    // RAM 부족 기기 판별 + 자동 DPR + 부팅 tier 하한: 실제 ait-mem.js / ait-gl.js / index.html DPR 코드를 기기 환경만 바꿔 돌린다.
    /** @type {{ name: string, d: { ua: string, screen: [number, number], dpr: number, memory?: number }, low: boolean, tier: number, reason: string, dpr: number }[]} */
    const DEVICE_MATRIX = [
      { name: 'iPhone 8 iOS 16 375x667@2', d: { ua: IOS_UA('16_7'), screen: [375, 667], dpr: 2 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'iPhone 8 가로 화면(667x375)도 세로 기준으로 정규화', d: { ua: IOS_UA('16_7'), screen: [667, 375], dpr: 2 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'iPhone SE2 iOS 17 375x667@2', d: { ua: IOS_UA('17_4'), screen: [375, 667], dpr: 2 }, low: false, tier: 0, reason: '', dpr: 2 },
      { name: 'iPhone 7 Plus iOS 15 414x736@3', d: { ua: IOS_UA('15_8'), screen: [414, 736], dpr: 3 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'iPhone 8 Plus iOS 16 414x736@3', d: { ua: IOS_UA('16_7'), screen: [414, 736], dpr: 3 }, low: false, tier: 0, reason: '', dpr: 3 },
      { name: 'iPhone 15 iOS 18 393x852@3', d: { ua: IOS_UA('18_0'), screen: [393, 852], dpr: 3 }, low: false, tier: 0, reason: '', dpr: 3 },
      { name: 'iPhone SE 1세대 320x568@2 (iOS 15)', d: { ua: IOS_UA('15_8'), screen: [320, 568], dpr: 2 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'iPhone 320x568 은 iOS 버전과 무관하게 low-RAM', d: { ua: IOS_UA('18_0'), screen: [320, 568], dpr: 2 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'iOS 버전을 못 읽으면 low-RAM 아님', d: { ua: 'Mozilla/5.0 (iPhone) AppleWebKit/605.1.15 Mobile/15E148', screen: [375, 667], dpr: 2 }, low: false, tier: 0, reason: '', dpr: 2 },
      { name: 'iPad(UA)는 iPhone 규칙 대상이 아님', d: { ua: IOS_UA('15_8', 'iPad'), screen: [768, 1024], dpr: 2 }, low: false, tier: 0, reason: '', dpr: 2 },
      { name: 'Android deviceMemory 2 @2.625', d: { ua: ANDROID_UA, screen: [412, 915], dpr: 2.625, memory: 2 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'Android deviceMemory 0.5 @2.625', d: { ua: ANDROID_UA, screen: [412, 915], dpr: 2.625, memory: 0.5 }, low: true, tier: 1, reason: 'low-ram', dpr: 2 },
      { name: 'Android deviceMemory 4 @2.625', d: { ua: ANDROID_UA, screen: [412, 915], dpr: 2.625, memory: 4 }, low: false, tier: 0, reason: '', dpr: 2.625 },
      { name: 'Android deviceMemory 8 @3.5 (WQHD+) → 상한 3', d: { ua: ANDROID_UA, screen: [412, 915], dpr: 3.5, memory: 8 }, low: false, tier: 0, reason: '', dpr: 3 },
      { name: 'Android deviceMemory 미노출', d: { ua: ANDROID_UA, screen: [412, 915], dpr: 2.625 }, low: false, tier: 0, reason: '', dpr: 2.625 },
    ];

    /**
     * @param {{ ua: string, screen: [number, number], dpr: number, memory?: number }} d
     * @param {{ seed?: Record<string, string>, perf?: object, setting?: number }} [o]
     */
    async function runDevice(d, o = {}) {
      const token = newToken();
      server.pages.set(`/${token}.html`, `<!doctype html><html><head><meta charset="utf-8">
<script>window.__AIT_PERF = ${JSON.stringify(o.perf || {})};</script>
<script>${readRuntimeScript('ait-mem.js')}</script>
<script>${readRuntimeScript('ait-gl.js')}</script>
</head><body><canvas id="unity-canvas" width="320" height="480"></canvas><script>
var aitDevicePixelRatioSetting = ${o.setting === undefined ? -1 : o.setting};
${DPR_CODE}
window.__result = {
  low: window.AITMemory.lowRamDevice, tier: window.AITMemory.lowMemTier, reason: window.AITMemory.lowMemReason,
  dpr: getOptimalDevicePixelRatio(), stored: localStorage.getItem('__ait_lowmem_v1')
};
</script></body></html>`);
      const { context, page } = await newPage({ seed: o.seed });
      try {
        await context.addInitScript(deviceSpoof(d));
        await page.goto(`${server.origin}/${token}.html`);
        return await page.evaluate(() => window['__result']);
      } finally {
        await context.close();
      }
    }

    for (const c of DEVICE_MATRIX) {
      test(`기기 매트릭스: ${c.name}`, async () => {
        const r = await runDevice(c.d);
        expect(r.low, 'AITMemory.lowRamDevice').toBe(c.low);
        expect(r.tier, 'AITMemory.lowMemTier').toBe(c.tier);
        expect(r.reason, 'AITMemory.lowMemReason').toBe(c.reason);
        expect(r.dpr, '최종 DPR').toBe(c.dpr);
        expect(r.stored, 'low-ram 하한은 저장하지 않는다').toBeNull();
      });
    }

    test('low-RAM + 사망으로 저장된 tier 1 → 사유는 low-ram 이 아니고 DPR 상한 1.5', async () => {
      const d = { ua: IOS_UA('16_7'), screen: /** @type {[number, number]} */ ([375, 667]), dpr: 2 };
      const r = await runDevice(d, { seed: tierSeed(1) });
      expect(r.low).toBe(true);
      expect(r.tier).toBe(1);
      expect(r.reason).toBe('stored');
      expect(r.dpr).toBe(1.5);
    });

    test('low-RAM + 저장 tier 2 → tier 2 가 이기고 DPR 1', async () => {
      const d = { ua: IOS_UA('16_7'), screen: /** @type {[number, number]} */ ([375, 667]), dpr: 2 };
      const r = await runDevice(d, { seed: tierSeed(2) });
      expect(r.tier).toBe(2);
      expect(r.dpr).toBe(1);
    });

    test('low-RAM 이어도 lowMemoryTier=false 면 tier 0, forceLowMemTier=0 은 하한을 덮고, 판별 값 자체는 유지', async () => {
      const d = { ua: ANDROID_UA, screen: /** @type {[number, number]} */ ([412, 915]), dpr: 2.625, memory: 2 };
      const off = await runDevice(d, { perf: { lowMemoryTier: false } });
      expect(off.low).toBe(true);
      expect(off.tier).toBe(0);
      expect(off.dpr, 'tier 하한이 없어도 auto DPR 의 low-RAM 분기는 적용').toBe(2);
      const forced = await runDevice(d, { perf: { forceLowMemTier: 0 } });
      expect(forced.tier).toBe(0);
      expect(forced.reason).toBe('');
    });

    test('명시 DPR 설정은 low-RAM 이어도 그대로(기기 DPR 이하)', async () => {
      const d = { ua: IOS_UA('15_8'), screen: /** @type {[number, number]} */ ([414, 736]), dpr: 3 };
      const r = await runDevice(d, { setting: 3 });
      expect(r.low).toBe(true);
      expect(r.dpr).toBe(3);
    });

    for (const c of [
      { name: 'low-RAM iPhone 8 iOS 16', d: { ua: IOS_UA('16_7'), screen: /** @type {[number, number]} */ ([375, 667]), dpr: 2 }, expected: 1 },
      { name: 'low-RAM Android deviceMemory 2', d: { ua: ANDROID_UA, screen: /** @type {[number, number]} */ ([412, 915]), dpr: 2.625, memory: 2 }, expected: 1 },
      { name: '일반 iPhone 15', d: { ua: IOS_UA('18_0'), screen: /** @type {[number, number]} */ ([393, 852]), dpr: 3 }, expected: 0 },
    ]) {
      test(`tier 일치(페이지 캐시 peek == ait-mem.js, low-RAM 하한 포함): ${c.name}`, async () => {
        const token = newToken();
        const url = registerPage(server, token, {
          head: [
            emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` }),
            'window.__pre = window.__aitPeekLowTier();',
            readRuntimeScript('ait-mem.js'),
            'window.__post = window.AITMemory && window.AITMemory.lowMemTier;',
          ],
        });
        const { context, page } = await newPage();
        try {
          await context.addInitScript(deviceSpoof(c.d));
          await page.goto(url);
          const r = await page.evaluate(() => ({ pre: window['__pre'], post: window['__post'], after: window['__aitPeekLowTier']() }));
          expect(r.pre, 'ait-mem.js 이전 peek').toBe(c.expected);
          expect(r.post, 'AITMemory.lowMemTier').toBe(c.expected);
          expect(r.after, 'ait-mem.js 이후 peek').toBe(c.expected);
        } finally {
          await context.close();
        }
      });
    }

    test('peek: low-RAM 이어도 lowMemoryTier=false 면 0, 저장 tier 2 는 하한보다 우선', async () => {
      const d = { ua: ANDROID_UA, screen: /** @type {[number, number]} */ ([412, 915]), dpr: 2.625, memory: 2 };
      for (const [perf, seed, expected] of /** @type {[object, Record<string, string>, number][]} */ ([[{ lowMemoryTier: false }, {}, 0], [{}, tierSeed(2), 2]])) {
        const token = newToken();
        const url = registerPage(server, token, {
          perf,
          head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` }), 'window.__pre = window.__aitPeekLowTier();', readRuntimeScript('ait-mem.js'), 'window.__post = window.AITMemory.lowMemTier;'],
        });
        const { context, page } = await newPage({ seed });
        try {
          await context.addInitScript(deviceSpoof(d));
          await page.goto(url);
          expect(await page.evaluate(() => [window['__pre'], window['__post']])).toEqual([expected, expected]);
        } finally {
          await context.close();
        }
      }
    });

    test('modern early-fetch: low-RAM 기기는 사망 이력이 없어도 data 선시작을 생략(wasm 은 선시작)', async () => {
      const token = newToken();
      const kick = [`Build/${token}.data`, `Build/${token}.wasm`];
      const url = registerPage(server, token, { head: [emitEarlyFetchModern(kick, `Build/${token}.data`)] });
      const { context, page } = await newPage();
      try {
        await context.addInitScript(deviceSpoof({ ua: ANDROID_UA, screen: [412, 915], dpr: 2.625, memory: 2 }));
        await page.goto(url);
        await page.waitForTimeout(700);
        expect(server.count(`${token}.wasm`)).toBe(1);
        expect(server.count(`${token}.data`)).toBe(0);
      } finally {
        await context.close();
      }
    });

    // ------------------------------------------------------------------
    test('ait-gl.js: tierCap 이 lowMemTier 를 반영(사망 tier1 → 1.5, tier2 → 1, 0 → 0)하고 crash/저장값과는 낮은 쪽을 쓴다', async () => {
      const token = newToken();
      server.pages.set(`/${token}.html`, `<!doctype html><html><head><meta charset="utf-8">
<script>window.__AIT_PERF = {};</script>
<script>${readRuntimeScript('ait-mem.js')}</script>
<script>${readRuntimeScript('ait-gl.js')}</script>
</head><body><canvas id="unity-canvas" width="320" height="480"></canvas></body></html>`);
      for (const [tier, expected] of /** @type {[number, number][]} */ ([[0, 0], [1, 1.5], [2, 1]])) {
        const { context, page } = await newPage({ seed: tier ? tierSeed(tier) : {} });
        try {
          await page.goto(`${server.origin}/${token}.html`);
          expect(await page.evaluate(() => window['AITMemory'].lowMemTier)).toBe(tier);
          expect(await page.evaluate(() => window['__AIT_GL'].tierCap()), `tier=${tier}`).toBe(expected);
          if (tier === 1) {
            // 저장값 1 이 더 낮으면 그쪽(둘 중 작은 양수).
            const lower = await page.evaluate(() => {
              localStorage.setItem('__ait_gl_tier', JSON.stringify({ cap: 1, ts: Date.now() }));
              return window['__AIT_GL'].tierCap();
            });
            expect(lower).toBe(1);
          }
        } finally {
          await context.close();
        }
      }
    });
  });
}

/**
 * @param {number} tier
 * @param {number} ts
 * @returns {Record<string, string>}
 */
function tierSeedAt(tier, ts) {
  return { __ait_lowmem_v1: JSON.stringify({ tier, ts }) };
}
