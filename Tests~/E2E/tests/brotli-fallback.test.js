// @ts-check
import * as fs from 'fs';
import * as path from 'path';
import { test, expect } from '@playwright/test';
import { AIT_BUILD, DIST_WEB, SERVER_PORT } from './lib/env.js';
import { killServerProcess, startProductionServer } from './lib/server.js';
import { startStaticServer } from './lib/static-server.js';
import { BROTLI_FALLBACK_SHARD, getShard } from './lib/shard.js';

/**
 * Brotli + Decompression Fallback(안정 버전 기본값) 빌드 검증.
 *
 * 일반 E2E 빌드는 압축을 끄고(AIT_COMPRESSION_FORMAT=0) vite preview로만 서빙해서, 실제 배포 기본값인
 * `.unityweb`(Brotli) 경로가 CI에서 한 번도 실행되지 않는다. 이 spec은 압축 빌드 하나를 두 방식으로 서빙한다.
 *
 *   A) Content-Encoding 없이 서빙: 브라우저가 풀어 주지 못하는 환경(Brotli를 못 푸는 WebView 등).
 *      Unity loader가 마커를 보고 JS로 직접 압축을 풀어야 부팅된다.
 *   B) Content-Encoding: br로 서빙(vite preview): 브라우저가 풀어 주면 loader는 마커를 못 보고 그대로 쓴다.
 *
 * AIT_E2E_SHARD=brotli-fallback 일 때만 실행한다(playwright.config.ts).
 */

const MARKER = 'UnityWeb Compressed Content (brotli)';
const STATIC_PORT = SERVER_PORT + 100;

/** @returns {string[]} dist/web/Build 아래 .unityweb 파일 절대 경로 */
function listUnityWebFiles() {
  const buildDir = path.join(DIST_WEB, 'Build');
  if (!fs.existsSync(buildDir)) return [];
  return fs.readdirSync(buildDir).filter(f => f.endsWith('.unityweb')).map(f => path.join(buildDir, f));
}

/**
 * 페이지를 열어 Unity 부팅과 Build/ 응답 헤더를 수집한다.
 * @param {import('@playwright/test').Browser} browser
 * @param {number} port
 */
// host: 정적 서버는 127.0.0.1에 바인딩하고, vite preview는 localhost(IPv6일 수 있음)에 바인딩한다.
async function bootAndCollect(browser, port, host = '127.0.0.1') {
  const context = await browser.newContext();
  const page = await context.newPage();
  /** @type {{ url: string, status: number, encoding: string | undefined, head: Buffer | null }[]} */
  const buildResponses = [];
  /** @type {string[]} */
  const errors = [];
  let workerCount = 0;

  page.on('worker', () => { workerCount++; });
  page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
  page.on('pageerror', err => errors.push(`pageerror: ${err.message}`));
  page.on('response', async response => {
    const url = response.url();
    if (!/\/Build\/[^?]*\.unityweb(\?|$)/.test(url)) return;
    const entry = {
      url,
      status: response.status(),
      encoding: response.headers()['content-encoding'],
      /** @type {Buffer | null} */
      head: null,
    };
    buildResponses.push(entry);
    // 작은 파일만 본문 앞부분을 본다(wasm은 크다). 브라우저가 풀어 준 응답이면 마커가 보이지 않는다.
    if (/framework\.js\.unityweb/.test(url)) {
      try { entry.head = (await response.body()).subarray(0, 256); } catch {}
    }
  });

  const response = await page.goto(`http://${host}:${port}/?e2e=true`, { waitUntil: 'networkidle', timeout: 120000 });
  expect(response?.status()).toBe(200);
  await page.waitForFunction(() => window['unityInstance'] !== undefined, { timeout: 180000 });

  // 기본 SDK 호출: 브리지 없는 환경에서 모든 API가 기대한 오류를 돌려주는지(production-core 4번과 같은 경로).
  await page.waitForFunction(() => typeof window['TriggerAPITest'] === 'function', { timeout: 30000 });
  const apiResults = await page.evaluate(() => new Promise(resolve => {
    if (window['__E2E_API_TEST_DATA__']) { resolve(window['__E2E_API_TEST_DATA__']); return; }
    window.addEventListener('e2e-api-test-complete', e => resolve(/** @type {CustomEvent} */ (e).detail), { once: true });
    window['TriggerAPITest']();
    setTimeout(() => resolve(null), 120000);
  }));
  let results = apiResults;
  if (typeof results === 'string') { try { results = JSON.parse(results); } catch {} }

  await context.close();
  return { buildResponses, errors, workerCount, results };
}

test.describe.serial('Brotli + Decompression Fallback', () => {
  test.skip(getShard() !== BROTLI_FALLBACK_SHARD, `AIT_E2E_SHARD=${BROTLI_FALLBACK_SHARD} 일 때만 실행한다`);

  test('0. 빌드 산출물이 Brotli .unityweb이다', () => {
    const files = listUnityWebFiles();
    const names = files.map(f => path.basename(f));
    console.log(`📦 .unityweb files: ${names.join(', ')}`);
    for (const suffix of ['.data.unityweb', '.framework.js.unityweb', '.wasm.unityweb']) {
      expect(names.some(n => n.endsWith(suffix)), `${suffix} should exist`).toBe(true);
    }
    // Brotli 메타데이터 블록은 바이트 정렬된 평문이라 마커 문자열이 앞부분에 그대로 보인다.
    for (const file of files) {
      const fd = fs.openSync(file, 'r');
      const buf = Buffer.alloc(128);
      fs.readSync(fd, buf, 0, 128, 0);
      fs.closeSync(fd);
      expect(buf.includes(Buffer.from(MARKER)), `${path.basename(file)} should start with the brotli marker`).toBe(true);
    }
  });

  test('A. Content-Encoding 없이 서빙해도 loader가 JS로 압축을 풀어 부팅한다', async ({ browser }) => {
    test.setTimeout(420000);
    const server = await startStaticServer(DIST_WEB, STATIC_PORT);
    try {
      const { buildResponses, errors, workerCount, results } = await bootAndCollect(browser, STATIC_PORT);

      // 전송 구간: 헤더가 없고, 본문이 Brotli 마커 그대로여야 한다. 이 둘이 맞아야 브라우저가 풀어 주지 않았다는 증거다.
      expect(buildResponses.length, '.unityweb 요청이 있어야 한다').toBeGreaterThanOrEqual(3);
      for (const r of buildResponses) {
        expect(r.status, r.url).toBe(200);
        expect(r.encoding, `${r.url} must have no Content-Encoding`).toBeUndefined();
      }
      const framework = buildResponses.find(r => /framework\.js\.unityweb/.test(r.url));
      expect(framework?.head, 'framework 응답 본문을 읽어야 한다').toBeTruthy();
      expect(framework?.head?.includes(Buffer.from(MARKER)), '응답 본문이 압축된 채로 도착해야 한다').toBe(true);

      // 부팅 성공(위 bootAndCollect의 unityInstance 대기)이 곧 loader의 JS 해제 증거다.
      // Chrome은 헤더 없는 Brotli 바이트를 풀지 않으므로, 풀지 못했다면 wasm 컴파일에서 실패한다.
      const loaderErrors = errors.filter(e => /still (brotli|gzip)-compressed|Content-Encoding|Unknown data format|Failed to parse binary/i.test(e));
      expect(loaderErrors, 'loader 압축 해제 오류가 없어야 한다').toEqual([]);
      console.log(`ℹ️ scenario A: worker events=${workerCount}`);

      expect(results, 'SDK API 테스트 결과를 받아야 한다').not.toBeNull();
      expect(/** @type {any} */ (results).unexpectedErrorCount || 0).toBe(0);
    } finally {
      await new Promise(r => server.close(() => r(undefined)));
    }
  });

  test('B. Content-Encoding: br로 서빙하면 브라우저가 풀어 주고 그대로 부팅한다', async ({ browser }) => {
    test.setTimeout(420000);
    const prod = await startProductionServer(AIT_BUILD, SERVER_PORT);
    try {
      const { buildResponses, errors, results } = await bootAndCollect(browser, prod.port, 'localhost');

      expect(buildResponses.length, '.unityweb 요청이 있어야 한다').toBeGreaterThanOrEqual(3);
      for (const r of buildResponses) {
        expect(r.status, r.url).toBe(200);
        expect(r.encoding, `${r.url} should be served with Content-Encoding: br`).toBe('br');
      }
      const loaderErrors = errors.filter(e => /still (brotli|gzip)-compressed|Content-Encoding|Unknown data format|Failed to parse binary/i.test(e));
      expect(loaderErrors, 'loader 압축 해제 오류가 없어야 한다').toEqual([]);

      expect(results, 'SDK API 테스트 결과를 받아야 한다').not.toBeNull();
      expect(/** @type {any} */ (results).unexpectedErrorCount || 0).toBe(0);
    } finally {
      await killServerProcess(prod.process, [prod.port]);
    }
  });
});
