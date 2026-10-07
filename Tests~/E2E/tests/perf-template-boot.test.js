// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — index.html 부팅 경로 검증
 *
 * 1) 다운로드 정체 워치독(aitStallShouldReload): 백그라운드·타이머 정지 구간을 무진행으로 세지 않는다.
 * 2) computeAutoDevicePixelRatio: iOS 에서는 일회용 WebGL 프로브를 건너뛰고, ANGLE 형식 Adreno 렌더러 문자열을 인식한다.
 *
 * Unity 빌드 없이 index.html 의 마커 블록(AIT-STALL-WATCHDOG, AIT-AUTO-DPR)만 문자열로 잘라 실행한다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const INDEX_HTML = fs.readFileSync(path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/index.html'), 'utf8');

/** @param {string} name */
function extractBlock(name) {
  const begin = `/* ${name}:BEGIN */`;
  const end = `/* ${name}:END */`;
  const b = INDEX_HTML.indexOf(begin);
  const e = INDEX_HTML.indexOf(end);
  if (b < 0 || e < b) throw new Error(`index.html 에 ${name} 마커 블록이 없다`);
  return INDEX_HTML.slice(b + begin.length, e);
}

const STALL_BLOCK = extractBlock('AIT-STALL-WATCHDOG');
const DPR_BLOCK = extractBlock('AIT-AUTO-DPR');

const IPHONE_UA = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1';
const ANDROID_UA = 'Mozilla/5.0 (Linux; Android 13; SM-G991N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36';

// ============================================================================
// 1) 정체 워치독 (Node 전용)
// ============================================================================

// eslint-disable-next-line no-new-func
const stall = new Function(STALL_BLOCK + '; return aitStallShouldReload;')();

test.describe('다운로드 정체 워치독', () => {
  test('포그라운드에서 61초 무진행이면 reload', () => {
    const t0 = 1_000_000;
    let s = { lastProgress: 0.3, lastProgressTs: t0, lastTick: t0 };
    let reload = false;
    // 10초 간격 틱
    for (let now = t0 + 10000; now <= t0 + 70000 && !reload; now += 10000) {
      const r = stall(s, now, false);
      s = r.s;
      reload = r.reload;
      if (now - t0 <= 60000) expect(r.reload).toBe(false);
    }
    expect(reload).toBe(true);
    expect(stall({ lastProgress: 0.3, lastProgressTs: t0, lastTick: t0 + 51000 }, t0 + 61000, false).reload).toBe(true);
  });

  test('90초 hidden 뒤 visible 첫 틱은 reload 하지 않는다', () => {
    const t0 = 1_000_000;
    let s = { lastProgress: 0.3, lastProgressTs: t0, lastTick: t0 };
    for (let now = t0 + 10000; now <= t0 + 90000; now += 10000) {
      const r = stall(s, now, true);
      expect(r.reload).toBe(false);
      s = r.s;
    }
    const first = stall(s, t0 + 100000, false);
    expect(first.reload).toBe(false);
    // 복귀 후 새로 60초를 세므로 10초 틱으로 60초까지는 유지, 그 뒤 reload
    let cur = first.s;
    let now = t0 + 100000;
    for (let i = 0; i < 5; i++) {
      now += 10000;
      const r = stall(cur, now, false);
      expect(r.reload).toBe(false);
      cur = r.s;
    }
    expect(stall(cur, now + 10000, false).reload).toBe(true);
  });

  test('틱 간격이 90초(타이머 정지)면 reload 하지 않고 기준을 당긴다', () => {
    const t0 = 1_000_000;
    const s = { lastProgress: 0.3, lastProgressTs: t0, lastTick: t0 };
    const r = stall(s, t0 + 90000, false);
    expect(r.reload).toBe(false);
    expect(r.s.lastProgressTs).toBe(t0 + 90000);
    expect(r.s.lastTick).toBe(t0 + 90000);
  });

  test('progress >= 0.9 면 reload 하지 않는다', () => {
    const t0 = 1_000_000;
    for (const p of [0.9, 0.95, 1]) {
      expect(stall({ lastProgress: p, lastProgressTs: t0, lastTick: t0 + 590000 }, t0 + 600000, false).reload).toBe(false);
    }
  });

  test('입력 상태를 변경하지 않는다', () => {
    const s = { lastProgress: 0.3, lastProgressTs: 1, lastTick: 2 };
    stall(s, 100000, true);
    expect(s).toEqual({ lastProgress: 0.3, lastProgressTs: 1, lastTick: 2 });
  });
});

// ============================================================================
// 2) computeAutoDevicePixelRatio (브라우저)
// ============================================================================

/**
 * @param {import('@playwright/test').Browser} browser
 * @param {{ userAgent: string, memory?: number, cores?: number, renderer?: string }} o
 */
async function newDprPage(browser, o) {
  const context = await browser.newContext({
    userAgent: o.userAgent,
    deviceScaleFactor: 3,
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
  });
  await context.addInitScript((cfg) => {
    // @ts-ignore
    window.__getContextCalls = 0;
    const orig = HTMLCanvasElement.prototype.getContext;
    // @ts-ignore
    HTMLCanvasElement.prototype.getContext = function (...args) {
      // @ts-ignore
      window.__getContextCalls++;
      if (cfg.renderer) {
        return {
          getExtension: () => ({ UNMASKED_RENDERER_WEBGL: 0x9246 }),
          getParameter: () => cfg.renderer,
        };
      }
      return orig.apply(this, args);
    };
    Object.defineProperty(navigator, 'deviceMemory', { get: () => cfg.memory, configurable: true });
    Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => cfg.cores, configurable: true });
  }, { renderer: o.renderer || null, memory: o.memory, cores: o.cores === undefined ? 8 : o.cores });
  const page = await context.newPage();
  await page.setContent(`<!DOCTYPE html><html><body><script>
    var aitDevicePixelRatioSetting = -1;
    ${DPR_BLOCK}
    window.__dpr = computeAutoDevicePixelRatio();
    window.__probeMs = aitDprProbeMs;
  </script></body></html>`);
  return { context, page };
}

test.describe('computeAutoDevicePixelRatio', () => {
  test('iOS UA 는 WebGL 프로브를 건너뛰고 결과는 그대로(min(dpr,2))', async ({ browser }) => {
    // iOS 는 deviceMemory 미지원 + 코어 4 → 구형 추정 분기 min(baseRatio, 2)
    const { context, page } = await newDprPage(browser, { userAgent: IPHONE_UA, memory: undefined, cores: 4 });
    expect(await page.evaluate(() => window.__getContextCalls)).toBe(0);
    expect(await page.evaluate(() => window.__dpr)).toBe(2);
    expect(await page.evaluate(() => window.__probeMs)).toBe(0);
    await context.close();
  });

  test('Android + ANGLE 형식 Adreno 430 은 저사양 GPU 로 DPR 1', async ({ browser }) => {
    const { context, page } = await newDprPage(browser, {
      userAgent: ANDROID_UA, memory: 8, cores: 8,
      renderer: 'ANGLE (Qualcomm, Adreno (TM) 430, OpenGL ES 3.2)',
    });
    expect(await page.evaluate(() => window.__getContextCalls)).toBeGreaterThan(0);
    expect(await page.evaluate(() => window.__dpr)).toBe(1);
    await context.close();
  });

  test('Android + Adreno 640 은 저사양이 아니다', async ({ browser }) => {
    const { context, page } = await newDprPage(browser, {
      userAgent: ANDROID_UA, memory: 8, cores: 8,
      renderer: 'ANGLE (Qualcomm, Adreno (TM) 640, OpenGL ES 3.2)',
    });
    const dpr = await page.evaluate(() => window.__dpr);
    expect(dpr).not.toBe(1);
    expect(dpr).toBe(3);
    expect(await page.evaluate(() => typeof window.__probeMs)).toBe('number');
    await context.close();
  });
});
