// @ts-check
import { test, expect, chromium, webkit } from '@playwright/test';
import * as http from 'http';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
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
 *  - tier >= 1: 엔진과 무관하게 put 생략, data 선시작 생략.
 *  - 페이지 캐시 스니펫이 계산하는 tier(__aitPeekLowTier)와 ait-mem.js 의 AITMemory.lowMemTier 가 같은 시드에서 같다.
 */

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
 * 모든 페이지 스크립트보다 먼저: Response.clone / Cache.put 호출 기록, localStorage 시드.
 * @param {{ seed?: Record<string, string> }} opt
 */
function initScript(opt) {
  const seed = opt.seed || {};
  return `(() => {
    window.__spy = { clones: 0, puts: [] };
    try {
      const c = Response.prototype.clone;
      Response.prototype.clone = function () { window.__spy.clones++; return c.apply(this, arguments); };
    } catch (e) {}
    try {
      const p = Cache.prototype.put;
      Cache.prototype.put = function (req) { window.__spy.puts.push(String((req && req.url) || req)); return p.apply(this, arguments); };
    } catch (e) {}
    try {
      const seed = ${JSON.stringify(seed)};
      for (const k of Object.keys(seed)) { if (localStorage.getItem(k) === null) localStorage.setItem(k, seed[k]); }
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
  await page.evaluate(() => { window.dispatchEvent(new Event('ait:firstframe')); });
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
          const probe = await webkit.launch({ channel: undefined });
          await probe.close();
          webkitReady = true;
        } else {
          try { browser = await chromium.launch({ channel: 'chrome' }); } catch { browser = await chromium.launch(); }
        }
      } catch (e) {
        launchError = String(e && /** @type {Error} */ (e).message || e).split('\n')[0]; console.log('[launch-error]', engine, launchError);
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

    /** @param {{ seed?: Record<string, string> }} [opt] */
    async function newPage(opt = {}) {
      // Playwright 의 WebKit 은 비영속(incognito) 컨텍스트에서 HTTP 캐시가 아예 꺼져 있어(force-cache 도 서버로 간다) only-if-cached 가 항상 실패한다.
      // 실제 Safari/WKWebView 처럼 디스크 캐시가 있는 영속 컨텍스트를 테스트마다 새 임시 디렉터리로 띄운다.
      // userAgent: undefined 는 playwright.config 의 Desktop Chrome UA 기본값을 덮어 엔진 고유 UA 를 쓰게 한다(UA 로 엔진을 판정하는 코드 검증용).
      const context = engine === 'webkit'
        ? await webkit.launchPersistentContext(fs.mkdtempSync(path.join(os.tmpdir(), 'ait-wk-')), { channel: undefined, userAgent: undefined })
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
        expect(info.net.length, 'data/framework 2건만 네트워크 저장').toBe(2);
        expect(info.put.length).toBe(2);
        expect(info.miss).toEqual([]);
        expect(info.net.some((/** @type {string} */ u) => u.endsWith('.wasm'))).toBe(false);
        expect(server.hits.length, '네트워크 저장은 파일당 정확히 1건씩').toBe(5);
        expect(server.count(`${token}.data`)).toBe(2);
        expect(server.count(`${token}.framework.js`)).toBe(2);
        expect(server.count(`${token}.wasm`)).toBe(1);
        const puts = await page.evaluate(() => window['__spy'].puts.length);
        expect(puts).toBe(2);
        expect(logs.some((l) => /HTTP 캐시 miss → 네트워크로 저장 /.test(l))).toBe(true);
        expect(logs.some((l) => /지연 put 완료: put=2 net=2 miss=0/.test(l))).toBe(true);
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
    test('tier>=1: 지연/즉시 어느 쪽이든 put 을 만들지 않는다(clone 0, put 0)', async () => {
      for (const deferFlag of [-1, 1, 0]) {
        server.hits.length = 0;
        const token = newToken();
        const url = registerPage(server, token, {
          perf: { pageCacheDeferDelayMs: 100, pageCacheDeferredPut: deferFlag },
          head: [emitPageCacheScript({ dataFile: `${token}.data`, frameworkFile: `${token}.framework.js`, wasmFile: `${token}.wasm` })],
        });
        const { context, page } = await newPage({ seed: tierSeed(1) });
        try {
          await page.goto(url);
          expect(await page.evaluate(() => window['__aitPeekLowTier']())).toBe(1);
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
          expect(r.queued, '저메모리 tier 는 큐에도 넣지 않는다').toBe(0);
          expect(r.skipped).toBeGreaterThan(0);
        } finally {
          await context.close();
        }
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
      /** @type {{ name: string, seed: Record<string, string>, perf?: object, expected: number }[]} */
      const list = [
        { name: '마커 없음', seed: {}, expected: 0 },
        { name: '직전 부팅 사망(boot-start)', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, expected: 1 },
        { name: '직전 사망 + 최근 실패 1회', seed: { __ait_boot_v1: JSON.stringify({ stage: 'first-frame', t: now - 2000, fails: [now - MIN] }) }, expected: 2 },
        { name: '직전 stable 이면 실패 기록 무시', seed: { __ait_boot_v1: JSON.stringify({ stage: 'stable', t: now - 2000, fails: [now - 1000] }) }, expected: 0 },
        { name: '저장 tier 2(24시간 이내)', seed: tierSeedAt(2, now - 1000), expected: 2 },
        { name: '저장 tier 1 이 24시간 지남', seed: tierSeedAt(1, now - 25 * H), expected: 0 },
        { name: '사망 + 저장 tier 1 → 2', seed: { ...tierSeedAt(1, now - 1000), __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, expected: 2 },
        { name: '정상 종료(end=exit)는 사망이 아님', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', end: 'exit', t: now - 2000 }) }, expected: 0 },
        { name: '10분 창 밖 실패 2회', seed: { __ait_boot_v1: JSON.stringify({ fails: [now - 11 * MIN, now - 12 * MIN] }) }, expected: 0 },
        { name: 'lowMemoryTier=false 면 항상 0', seed: { __ait_boot_v1: JSON.stringify({ stage: 'boot-start', t: now - 2000 }) }, perf: { lowMemoryTier: false }, expected: 0 },
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
        const { context, page } = await newPage({ seed: c.seed });
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
    test('ait-gl.js: tierCap 이 lowMemTier 를 반영(tier1 → 1.5, tier2 → 1, 0 → 0)하고 crash/저장값과는 낮은 쪽을 쓴다', async () => {
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
