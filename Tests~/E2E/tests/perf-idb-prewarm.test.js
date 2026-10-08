// @ts-check
import { test, expect, chromium, webkit } from '@playwright/test';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — IDBFS 프리워밍(index.html head) 검증
 *
 * 콜드 첫 실행에서 Unity(Emscripten IDBFS)가 부팅 중에 만드는 IndexedDB("/idbfs" v21, FILE_DATA, timestamp 인덱스)를
 * head 에서 미리 만들어 두는 스크립트를 검증한다. Unity 빌드 없이 index.html 의 AIT-IDB-PREWARM 마커 블록만 잘라
 * 가짜 origin(page.route)에서 실행한다. IndexedDB 는 opaque origin 에서 못 쓰므로 http origin 이 필요하다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const INDEX_HTML = fs.readFileSync(path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/index.html'), 'utf8');
const BEGIN = '/* AIT-IDB-PREWARM:BEGIN */';
const END = '/* AIT-IDB-PREWARM:END */';
const b = INDEX_HTML.indexOf(BEGIN);
const e = INDEX_HTML.indexOf(END);
if (b < 0 || e < b) throw new Error('index.html 에 AIT-IDB-PREWARM 마커 블록이 없다');
const PREWARM_BLOCK = INDEX_HTML.slice(b + BEGIN.length, e);

// 템플릿이 head 맨 위 __AIT_PERF 스크립트 바로 뒤에 프리워밍을 둔다는 순서 계약.
test('프리워밍 스크립트는 __AIT_PERF 뒤, 페이지 캐시·조기 fetch 플레이스홀더 앞에 있다', () => {
  const iPerf = INDEX_HTML.indexOf('window.__AIT_PERF = __AIT_PERF');
  const iPrewarm = INDEX_HTML.indexOf(BEGIN);
  const iCache = INDEX_HTML.indexOf('%AIT_PAGE_CACHE_SCRIPT%');
  const iEarly = INDEX_HTML.indexOf('%AIT_EARLY_FETCH_SCRIPT%');
  expect(iPerf).toBeGreaterThan(-1);
  expect(iPrewarm).toBeGreaterThan(iPerf);
  expect(iCache).toBeGreaterThan(iPrewarm);
  expect(iEarly).toBeGreaterThan(iPrewarm);
});

const ORIGIN = 'http://ait-idb.test';

/**
 * @param {import('@playwright/test').Page} page
 * @param {{ perf?: string | null }} [o] perf: __AIT_PERF 초기화 JS 식(null 이면 __AIT_PERF 없음)
 */
async function routePages(page, o = {}) {
  const perfScript = o.perf == null ? '' : `<script>window.__AIT_PERF = ${o.perf};</script>`;
  await page.route(`${ORIGIN}/**`, (route) => {
    const url = new URL(route.request().url());
    const body = url.pathname === '/seed'
      ? '<!DOCTYPE html><html><body>seed</body></html>'
      : `<!DOCTYPE html><html><head>${perfScript}<script>${PREWARM_BLOCK}</script></head><body>ok</body></html>`;
    return route.fulfill({ status: 200, contentType: 'text/html', body });
  });
}

/** 브라우저 안에서 "/idbfs" DB 의 현재 상태를 읽는다(없으면 null; 읽기가 DB 를 만들지 않도록 databases() 로 먼저 확인). */
async function readIdbfs(/** @type {import('@playwright/test').Page} */ page) {
  return page.evaluate(async () => {
    const list = await indexedDB.databases();
    if (!list.some((d) => d.name === '/idbfs')) return null;
    return await new Promise((resolve, reject) => {
      const r = indexedDB.open('/idbfs');
      r.onsuccess = () => {
        const db = r.result;
        const stores = Array.from(db.objectStoreNames);
        let indexes = [];
        let keyPath = null;
        if (stores.includes('FILE_DATA')) {
          const st = db.transaction('FILE_DATA', 'readonly').objectStore('FILE_DATA');
          indexes = Array.from(st.indexNames);
          keyPath = st.keyPath;
        }
        const out = { version: db.version, stores, indexes, keyPath };
        db.close();
        resolve(out);
      };
      r.onerror = () => reject(r.error);
    });
  });
}

const settle = (/** @type {import('@playwright/test').Page} */ page) => page.waitForTimeout(600);

for (const engine of /** @type {const} */ (['chromium', 'webkit'])) {
  test.describe(`[${engine}] IDBFS 프리워밍`, () => {
    /** @type {import('@playwright/test').Browser | null} */
    let browser = null;
    let launchError = '';
    let webkitReady = false;

    test.beforeAll(async () => {
      try {
        if (engine === 'webkit') {
          // 가용성 확인만 한다. args: [] 는 playwright.config 의 Chromium 전용 launch 플래그가 WebKit 을 죽이지 않게 비운다.
          const probe = await webkit.launch({ channel: undefined, args: [] });
          await probe.close();
          webkitReady = true;
        } else {
          try { browser = await chromium.launch({ channel: 'chrome' }); } catch { browser = await chromium.launch(); }
        }
      } catch (err) {
        launchError = String(err && /** @type {Error} */ (err).message || err).split('\n')[0];
      }
    });

    test.afterAll(async () => {
      if (browser) await browser.close();
    });

    test.beforeEach(() => {
      test.skip(!browser && !webkitReady, `${engine} 를 띄울 수 없음: ${launchError}`);
    });

    async function newPage(/** @type {{ perf?: string | null }} */ o = {}) {
      const context = engine === 'webkit'
        ? await webkit.launchPersistentContext(fs.mkdtempSync(path.join(os.tmpdir(), 'ait-idb-wk-')), { channel: undefined, args: [], userAgent: undefined })
        : await /** @type {import('@playwright/test').Browser} */ (browser).newContext({ userAgent: undefined });
      const page = await context.newPage();
      /** @type {string[]} */
      const errors = [];
      page.on('pageerror', (err) => errors.push(String(err)));
      await routePages(page, o);
      return { context, page, errors };
    }

    test('기본(플래그 없음): 로드 후 v21 / FILE_DATA / timestamp 인덱스가 만들어지고 ait:idb-prewarm 마크가 있다', async () => {
      const { context, page, errors } = await newPage({ perf: null });
      try {
        await page.goto(`${ORIGIN}/`);
        await page.waitForFunction(() => performance.getEntriesByName('ait:idb-prewarm').length > 0, undefined, { timeout: 10000 });
        const info = await readIdbfs(page);
        expect(info).toEqual({ version: 21, stores: ['FILE_DATA'], indexes: ['timestamp'], keyPath: null });
        expect(errors).toEqual([]);
      } finally {
        await context.close();
      }
    });

    test('__AIT_PERF.idbPrewarm=true/1 이나 빈 {} 도 켜진다', async () => {
      for (const perf of ['{ idbPrewarm: true }', '{ idbPrewarm: 1 }', '{}']) {
        const { context, page } = await newPage({ perf });
        try {
          await page.goto(`${ORIGIN}/`);
          await page.waitForFunction(() => performance.getEntriesByName('ait:idb-prewarm').length > 0, undefined, { timeout: 10000 });
          expect((await readIdbfs(page))?.version, perf).toBe(21);
        } finally {
          await context.close();
        }
      }
    });

    for (const perf of ['{ idbPrewarm: false }', '{ idbPrewarm: 0 }']) {
      test(`__AIT_PERF = ${perf} 이면 "/idbfs" 를 만들지 않는다`, async () => {
        const { context, page, errors } = await newPage({ perf });
        try {
          await page.goto(`${ORIGIN}/`);
          await settle(page);
          expect(await readIdbfs(page)).toBeNull();
          expect(await page.evaluate(() => performance.getEntriesByName('ait:idb-prewarm').length)).toBe(0);
          expect(errors).toEqual([]);
        } finally {
          await context.close();
        }
      });
    }

    test('?aitidbprewarm=0 이면 "/idbfs" 를 만들지 않는다', async () => {
      const { context, page, errors } = await newPage({ perf: '{ idbPrewarm: true }' });
      try {
        await page.goto(`${ORIGIN}/?aitidbprewarm=0`);
        await settle(page);
        expect(await readIdbfs(page)).toBeNull();
        expect(await page.evaluate(() => performance.getEntriesByName('ait:idb-prewarm').length)).toBe(0);
        expect(errors).toEqual([]);
      } finally {
        await context.close();
      }
    });

    test('이미 더 높은 버전(22)의 "/idbfs" 가 있으면 오류 없이 무시하고 버전은 22 로 유지된다', async () => {
      const { context, page, errors } = await newPage({ perf: null });
      try {
        await page.goto(`${ORIGIN}/seed`);
        await page.evaluate(() => new Promise((resolve, reject) => {
          const r = indexedDB.open('/idbfs', 22);
          r.onupgradeneeded = () => { r.result.createObjectStore('OTHER'); };
          r.onsuccess = () => { r.result.close(); resolve(undefined); };
          r.onerror = () => reject(r.error);
        }));
        await page.goto(`${ORIGIN}/`);
        await settle(page);
        expect(await page.evaluate(() => document.body.textContent)).toBe('ok');
        const info = await readIdbfs(page);
        expect(info?.version).toBe(22);
        expect(info?.stores).toEqual(['OTHER']);
        expect(await page.evaluate(() => performance.getEntriesByName('ait:idb-prewarm').length)).toBe(0);
        expect(errors).toEqual([]);
      } finally {
        await context.close();
      }
    });

    test('프리워밍 뒤 Emscripten 업그레이드 로직으로 v21 을 열 수 있고, 이후 v22 업그레이드도 blocked 없이 진행된다', async () => {
      const { context, page, errors } = await newPage({ perf: null });
      try {
        await page.goto(`${ORIGIN}/`);
        await page.waitForFunction(() => performance.getEntriesByName('ait:idb-prewarm').length > 0, undefined, { timeout: 10000 });

        const emscripten = await page.evaluate(() => new Promise((resolve, reject) => {
          // Emscripten IDBFS.getDB 와 동일한 업그레이드 로직
          const r = indexedDB.open('/idbfs', 21);
          let upgraded = false;
          r.onupgradeneeded = (ev) => {
            upgraded = true;
            const db = /** @type {any} */ (ev.target).result;
            const tx = /** @type {any} */ (ev.target).transaction;
            const store = db.objectStoreNames.contains('FILE_DATA') ? tx.objectStore('FILE_DATA') : db.createObjectStore('FILE_DATA');
            if (!store.indexNames.contains('timestamp')) store.createIndex('timestamp', 'timestamp', { unique: false });
          };
          r.onsuccess = () => {
            const db = r.result;
            const tx = db.transaction('FILE_DATA', 'readwrite');
            const st = tx.objectStore('FILE_DATA');
            st.put({ timestamp: new Date(), mode: 33206, contents: new Uint8Array([1, 2, 3]) }, '/idbfs/probe');
            tx.oncomplete = () => { db.close(); resolve({ upgraded, version: db.version }); };
            tx.onerror = () => reject(tx.error);
          };
          r.onerror = () => reject(r.error);
        }));
        expect(emscripten).toEqual({ upgraded: false, version: 21 });

        // 프리워밍 연결은 이미 닫혀 있어야 하므로 더 높은 버전 업그레이드가 blocked 없이 끝난다.
        const upgrade = await page.evaluate(() => new Promise((resolve, reject) => {
          let blocked = false;
          const r = indexedDB.open('/idbfs', 22);
          r.onblocked = () => { blocked = true; };
          r.onupgradeneeded = () => { /* 스키마 변경 없음 */ };
          r.onsuccess = () => { const v = r.result.version; r.result.close(); resolve({ blocked, version: v }); };
          r.onerror = () => reject(r.error);
        }));
        expect(upgrade).toEqual({ blocked: false, version: 22 });
        expect(errors).toEqual([]);
      } finally {
        await context.close();
      }
    });
  });
}
