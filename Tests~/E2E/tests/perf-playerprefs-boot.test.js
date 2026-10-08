// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — ait-playerprefs.js 스토리지 해석 타이밍 검증
 *
 * resolveStorage() 는 unity-bridge 가 window.AppsInToss.Storage 를 설치하기 전에 호출될 수 있다.
 * 이 파일은 다음을 확인한다.
 *  (a) `ait:bridge-ready` 이벤트가 오면 50ms 폴링 틱을 기다리지 않고 즉시 해석한다.
 *  (b) 이벤트가 없으면(구 브리지) 기존 폴링으로 해석한다.
 *  (c) 스크립트 로드 시점에 이미 설치돼 있으면 같은 task 안에서 해석한다.
 *  (d) 이벤트가 두 번 오거나 폴링 해석 뒤에 와도 중복 해석·예외가 없다.
 *
 * Unity 빌드 없이 빈 페이지에 ait-playerprefs.js 를 주입해 performance.mark 로 측정한다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PP_SRC = fs.readFileSync(
  path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js'),
  'utf8'
);

/**
 * 페이지 안에서 시나리오를 실행하고 마크 시각을 돌려준다.
 * @param {import('@playwright/test').Page} page
 * @param {{ mode: 'event' | 'poll' | 'preinstalled' | 'double-event' | 'late-event' }} opts
 */
async function run(page, opts) {
  await page.setContent('<!doctype html><html><body></body></html>');
  return page.evaluate(async ({ src, mode }) => {
    /** @type {any} */
    const w = window;
    performance.clearMarks(); // 같은 페이지에서 여러 시나리오를 돌릴 때 이전 마크가 섞이지 않게
    w.__AIT_PLAYERPREFS = { enabled: true, bootTimeoutMs: 2500 };
    const storage = {
      getItem: () => Promise.resolve(null),
      setItem: () => Promise.resolve(),
    };
    const errors = [];
    window.addEventListener('error', (e) => errors.push(String(e.message)));
    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

    if (mode === 'preinstalled') w.AppsInToss = { Storage: storage };

    const evalStart = performance.now();
    const el = document.createElement('script');
    el.textContent = src;
    document.head.appendChild(el); // 인라인 스크립트는 동기 실행
    const evalEnd = performance.now();

    let dispatchAt = 0;
    if (mode === 'event') {
      await sleep(5);
      w.AppsInToss = { Storage: storage };
      dispatchAt = performance.now();
      window.dispatchEvent(new Event('ait:bridge-ready'));
    } else if (mode === 'poll') {
      await sleep(5);
      w.AppsInToss = { Storage: storage };
      dispatchAt = performance.now(); // 이벤트 없이 설치 시각만 기록
    } else if (mode === 'double-event') {
      await sleep(5);
      w.AppsInToss = { Storage: storage };
      dispatchAt = performance.now();
      window.dispatchEvent(new Event('ait:bridge-ready'));
      window.dispatchEvent(new Event('ait:bridge-ready'));
    } else if (mode === 'late-event') {
      await sleep(5);
      w.AppsInToss = { Storage: storage }; // 이벤트 없이 설치 → 폴링이 해석
      dispatchAt = performance.now();
    }

    // 폴링 해석(50ms)과 스냅샷 처리가 끝날 때까지 여유를 둔다
    await sleep(mode === 'poll' || mode === 'late-event' ? 250 : 120);
    if (mode === 'late-event') {
      // 이미 해석된 뒤 늦게 도착한 이벤트는 무시돼야 한다
      window.dispatchEvent(new Event('ait:bridge-ready'));
      window.dispatchEvent(new Event('ait:bridge-ready'));
      await sleep(80);
    }

    const resolvedMarks = performance.getEntriesByName('ait:pp-storage-resolved');
    return {
      evalStart,
      evalEnd,
      dispatchAt,
      resolvedCount: resolvedMarks.length,
      resolvedAt: resolvedMarks.length ? resolvedMarks[0].startTime : -1,
      backend: resolvedMarks.length ? /** @type {any} */ (resolvedMarks[0]).detail?.backend ?? null : null,
      snapshotSettled: performance.getEntriesByName('ait:pp-snapshot-settled').length,
      errors,
    };
  }, { src: PP_SRC, mode: opts.mode });
}

test.describe('PlayerPrefs 스토리지 해석 타이밍', () => {
  test('(a) ait:bridge-ready 이벤트로 즉시 해석한다 (폴링 틱을 기다리지 않음)', async ({ page }) => {
    const r = await run(page, { mode: 'event' });
    expect(r.resolvedCount).toBe(1);
    expect(r.backend).toBe('platform');
    expect(r.resolvedAt - r.dispatchAt).toBeGreaterThanOrEqual(0);
    expect(r.resolvedAt - r.dispatchAt).toBeLessThan(10);
    expect(r.snapshotSettled).toBe(1);
    expect(r.errors).toEqual([]);
  });

  test('(b) 이벤트가 없으면 폴링으로 해석한다 (50~120ms)', async ({ page }) => {
    const r = await run(page, { mode: 'poll' });
    expect(r.resolvedCount).toBe(1);
    expect(r.backend).toBe('platform');
    // 설치 5ms 시점 이후 첫 폴링 틱(스크립트 로드 + 50ms)에서 해석된다
    const sinceEval = r.resolvedAt - r.evalEnd;
    expect(sinceEval).toBeGreaterThanOrEqual(45);
    expect(sinceEval).toBeLessThan(120);
    expect(r.errors).toEqual([]);
  });

  test('(c) 로드 시점에 이미 설치돼 있으면 같은 task 안에서 해석한다', async ({ page }) => {
    const r = await run(page, { mode: 'preinstalled' });
    expect(r.resolvedCount).toBe(1);
    expect(r.backend).toBe('platform');
    expect(r.resolvedAt).toBeGreaterThanOrEqual(r.evalStart);
    expect(r.resolvedAt - r.evalEnd).toBeLessThan(5);
    expect(r.errors).toEqual([]);
  });

  test('(d) 이벤트를 두 번 쏘거나 해석 뒤에 쏴도 중복 해석·예외가 없다', async ({ page }) => {
    const twice = await run(page, { mode: 'double-event' });
    expect(twice.resolvedCount).toBe(1);
    expect(twice.backend).toBe('platform');
    expect(twice.snapshotSettled).toBe(1);
    expect(twice.errors).toEqual([]);

    const late = await run(page, { mode: 'late-event' });
    expect(late.resolvedCount).toBe(1);
    expect(late.backend).toBe('platform');
    expect(late.snapshotSettled).toBe(1);
    expect(late.errors).toEqual([]);
  });
});
