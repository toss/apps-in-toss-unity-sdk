// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — 라이프사이클 게이트 / 프레임 governor / 메모리 텔레메트리 테스트
 *
 * 대상 런타임 스크립트(WebGLTemplates/AITTemplate/Runtime):
 *  - ait-pacing.js : hidden/pagehide/freeze 때 preMainLoop 정지 + AudioContext/미디어 일시정지·재개(P0-4),
 *                    rAF 간격으로 주사율을 재서 100Hz 이상이면 60fps 로 캡(P1-5)
 *  - ait-mem.js    : WebAssembly.Memory.grow 기록, sessionStorage 크래시 마커, ait:memory 이벤트(P1-6)
 *
 * 두 부분으로 나뉜다.
 *  1) 하네스 테스트(항상 실행, Unity 빌드 불필요): 두 스크립트를 가짜 origin 의 빈 페이지에 그대로 올리고
 *     가상 시계(performance.now / requestAnimationFrame 대체)로 120Hz·90Hz·60Hz 패널과 emscripten runIter 를 흉내 낸다.
 *     결정론적이라 러너 부하에 흔들리지 않는다.
 *  2) 빌드 페이지 테스트(AIT_PACING_BUILT=1 일 때만): 실제 Unity 산출물(ait-build/dist/web)에서 확인한다.
 *     UNITY_PROJECT_PATH 로 경로를 줄 수 있고, 없으면 HeavySampleUnityProject-* → SampleUnityProject-* 순으로 찾는다.
 *     산출물이 없으면 건너뛴다.
 *
 * 기록 전용 지표(주사율 시계열 등)는 임계 단언 없이 로그만 남긴다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const RUNTIME_DIR = path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/Runtime');
const PACING_JS = fs.readFileSync(path.join(RUNTIME_DIR, 'ait-pacing.js'), 'utf8');
const MEM_JS = fs.readFileSync(path.join(RUNTIME_DIR, 'ait-mem.js'), 'utf8');

const HARNESS_ORIGIN = 'https://ait-harness.test';

// ============================================================================
// 하네스
// ============================================================================

/** 페이지 스크립트보다 먼저 실행: 가상 시계, 가짜 rAF, emscripten runIter 흉내, visibility 오버라이드 헬퍼. */
function harnessInit() {
  const vt = { now: 1000, queue: [], nextId: 1, seed: 12345 };
  // @ts-ignore
  window.__vt = vt;
  // @ts-ignore
  performance.now = () => vt.now;
  window.requestAnimationFrame = (cb) => { const id = vt.nextId++; vt.queue.push({ id, cb }); return id; };
  window.cancelAnimationFrame = (id) => { vt.queue = vt.queue.filter((e) => e.id !== id); };
  // @ts-ignore
  window.__game = { ran: 0, ticks: 0 };
  // @ts-ignore
  window.__cfg = {};
  /**
   * n 번의 vsync 를 진행한다. 매 vsync: 시계 전진 → rAF 콜백(주사율 probe 등) → 게임 루프(emscripten runIter 와 같은 규칙).
   * @param {number} n
   * @param {number} periodMs
   * @param {number} [jitterMs] 각 간격에 ±jitterMs 균등 잡음(결정론적 LCG)
   */
  // @ts-ignore
  window.__tick = (n, periodMs, jitterMs) => {
    for (let i = 0; i < n; i++) {
      let step = periodMs;
      if (jitterMs) {
        vt.seed = (vt.seed * 1103515245 + 12345) & 0x7fffffff;
        step += ((vt.seed / 0x7fffffff) * 2 - 1) * jitterMs;
      }
      vt.now += step;
      const q = vt.queue;
      vt.queue = [];
      for (const e of q) e.cb(vt.now);
      // @ts-ignore
      window.__game.ticks++;
      // @ts-ignore
      const cfg = window.__cfg;
      if (cfg && typeof cfg.preMainLoop === 'function' && cfg.preMainLoop() === false) continue;
      // @ts-ignore
      window.__game.ran++;
    }
  };
  // E2ETestBridge.jslib 의 E2E_SimulateFocusChange 와 같은 방식(visibilityState/hidden 오버라이드 + visibilitychange)
  // @ts-ignore
  window.__setHidden = (hidden) => {
    if (hidden) {
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', writable: true, configurable: true });
      Object.defineProperty(document, 'hidden', { value: true, writable: true, configurable: true });
    } else {
      // @ts-ignore
      delete document.visibilityState;
      // @ts-ignore
      delete document.hidden;
    }
    document.dispatchEvent(new Event('visibilitychange'));
  };
}

/** 하네스 HTML. flags 는 window.__AIT_PERF 로 들어간다(index.html 의 head 인라인 스크립트와 같은 역할). */
function harnessHtml(flags, opts = {}) {
  const pre = opts.preScript || '';
  const parts = [
    '<!doctype html><html><head><meta charset="utf-8"><title>ait-harness</title>',
    `<script>window.__AIT_PERF = ${JSON.stringify(flags)};</script>`,
    pre ? `<script>${pre}</script>` : '',
    opts.mem === false ? '' : `<script>${MEM_JS}</script>`,
    '</head><body>',
    opts.pacing === false ? '' : `<script>${PACING_JS}</script>`,
    '</body></html>',
  ];
  return parts.join('\n');
}

/**
 * 하네스 페이지를 연다.
 * @param {import('@playwright/test').Page} page
 * @param {object} flags window.__AIT_PERF
 * @param {{preScript?: string, mem?: boolean, pacing?: boolean, query?: string, virtualClock?: boolean}} [opts]
 */
async function openHarness(page, flags, opts = {}) {
  if (opts.virtualClock !== false) await page.addInitScript(harnessInit);
  await page.route(`${HARNESS_ORIGIN}/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: harnessHtml(flags, opts) }));
  await page.goto(`${HARNESS_ORIGIN}/index.html${opts.query || ''}`);
}

/** 하네스 페이지에서 pacing.configure 를 호출해 preMainLoop 를 설치한다. */
async function configurePacing(page) {
  await page.evaluate(() => {
    // @ts-ignore
    window.AITPacing.configure(window.__cfg);
  });
}

async function pacingState(page) {
  return page.evaluate(() => {
    // @ts-ignore
    return window.AITPacing.getState();
  });
}

/** ran(실행된 프레임) 증가량을 n 틱 동안 잰다. */
async function ranDuring(page, n, periodMs, jitterMs = 0) {
  return page.evaluate(([n, p, j]) => {
    // @ts-ignore
    const before = window.__game.ran;
    // @ts-ignore
    window.__tick(n, p, j);
    // @ts-ignore
    return window.__game.ran - before;
  }, [n, periodMs, jitterMs]);
}

/** 무음 PCM WAV(8-bit mono 8kHz)를 data URL 로 만든다. 요소 재생 테스트용. */
function silentWavDataUrl(seconds) {
  const rate = 8000;
  const n = rate * seconds;
  const buf = Buffer.alloc(44 + n, 0x80);
  buf.write('RIFF', 0); buf.writeUInt32LE(36 + n, 4); buf.write('WAVE', 8);
  buf.write('fmt ', 12); buf.writeUInt32LE(16, 16); buf.writeUInt16LE(1, 20); buf.writeUInt16LE(1, 22);
  buf.writeUInt32LE(rate, 24); buf.writeUInt32LE(rate, 28); buf.writeUInt16LE(1, 32); buf.writeUInt16LE(8, 34);
  buf.write('data', 36); buf.writeUInt32LE(n, 40);
  return 'data:audio/wav;base64,' + buf.toString('base64');
}

// ============================================================================
// 1) 프레임 governor (P1-5)
// ============================================================================
test.describe('프레임 governor', () => {
  test('120Hz 패널에서 frameRateCap=60 이면 실행 프레임이 약 60/s', async ({ page }) => {
    await openHarness(page, { frameRateCap: 60, mobileLifecycle: true });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__tick(60, 1000 / 120); }); // 주사율 판정 구간
    const st = await pacingState(page);
    expect(st.hzDecided).toBe(true);
    expect(st.hz).toBeGreaterThanOrEqual(118);
    expect(st.hz).toBeLessThanOrEqual(122);
    expect(st.capFps).toBe(60);

    const ran = await ranDuring(page, 240, 1000 / 120); // 2초 분량
    expect(ran).toBeGreaterThanOrEqual(118);
    expect(ran).toBeLessThanOrEqual(122);
  });

  test('120Hz 에 간격 잡음(±1ms)이 있어도 약 60/s 로 수렴', async ({ page }) => {
    await openHarness(page, { frameRateCap: 60, mobileLifecycle: false });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__tick(60, 1000 / 120, 1); });
    expect((await pacingState(page)).capFps).toBe(60);
    const ran = await ranDuring(page, 480, 1000 / 120, 1); // 4초 분량
    expect(ran / 4).toBeGreaterThanOrEqual(57);
    expect(ran / 4).toBeLessThanOrEqual(63);
  });

  test('144Hz 에서도 60 으로 수렴', async ({ page }) => {
    await openHarness(page, { frameRateCap: 60, mobileLifecycle: false });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__tick(60, 1000 / 144); });
    expect((await pacingState(page)).capFps).toBe(60);
    const ran = await ranDuring(page, 576, 1000 / 144); // 4초 분량
    expect(ran / 4).toBeGreaterThanOrEqual(58);
    expect(ran / 4).toBeLessThanOrEqual(62);
  });

  test('frameRateCap=0 이면 120Hz 그대로(상한 없음)', async ({ page }) => {
    await openHarness(page, { frameRateCap: 0, mobileLifecycle: false });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__tick(60, 1000 / 120); });
    const st = await pacingState(page);
    expect(st.capFps).toBe(0);
    // mobileLifecycle 도 꺼져 있으면 preMainLoop 자체가 설치되지 않는다.
    const hasPre = await page.evaluate(() => { /* @ts-ignore */ return typeof window.__cfg.preMainLoop; });
    expect(hasPre).toBe('undefined');
    const ran = await ranDuring(page, 240, 1000 / 120);
    expect(ran).toBe(240);
  });

  test('frameRateCap=0 이라도 라이프사이클 게이트가 켜져 있으면 120Hz 를 건드리지 않는다', async ({ page }) => {
    await openHarness(page, { frameRateCap: 0, mobileLifecycle: true });
    await configurePacing(page);
    const ran = await ranDuring(page, 240, 1000 / 120);
    expect(ran).toBe(240);
    expect((await pacingState(page)).skippedCap).toBe(0);
  });

  test('60Hz / 90Hz 패널에서는 상한을 걸지 않는다', async ({ page }) => {
    await openHarness(page, { frameRateCap: 60, mobileLifecycle: false });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__tick(60, 1000 / 60); });
    let st = await pacingState(page);
    expect(st.hzDecided).toBe(false); // 100Hz 급이 아니면 관찰 한도까지 계속 본다
    expect(st.capFps).toBe(0);
    expect(await ranDuring(page, 120, 1000 / 60)).toBe(120);

    const page2 = await page.context().newPage();
    await openHarness(page2, { frameRateCap: 60, mobileLifecycle: false });
    await configurePacing(page2);
    await page2.evaluate(() => { /* @ts-ignore */ window.__tick(400, 1000 / 90); }); // 관찰 한도(360 콜백)를 넘겨 확정시킨다
    st = await pacingState(page2);
    expect(st.hzDecided).toBe(true);
    expect(st.hz).toBeGreaterThanOrEqual(88);
    expect(st.hz).toBeLessThanOrEqual(92);
    expect(st.capFps).toBe(0);
    expect(await ranDuring(page2, 180, 1000 / 90)).toBe(180);
  });

  test('로딩 jank 로 긴 간격이 섞여도 60Hz 를 120Hz 로 오판하지 않는다', async ({ page }) => {
    await openHarness(page, { frameRateCap: 60, mobileLifecycle: false });
    await configurePacing(page);
    await page.evaluate(() => {
      // 60Hz 에 가끔 40~90ms 정지가 섞인 시계열
      for (let i = 0; i < 400; i++) {
        // @ts-ignore
        window.__tick(1, i % 7 === 0 ? 40 + (i % 5) * 12 : 1000 / 60);
      }
    });
    expect((await pacingState(page)).capFps).toBe(0);
  });

  test('adaptiveFrameRate 는 기본(꺼짐)에서 힌트를 받아도 무시한다', async ({ page }) => {
    await openHarness(page, { frameRateCap: 60, mobileLifecycle: false, adaptiveFrameRate: false });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__tick(60, 1000 / 60); });
    await page.evaluate(() => {
      // @ts-ignore
      window.AITPacing.setHint('thermal', 'critical');
      // @ts-ignore
      window.AITPacing.setHint('memory', { level: 'critical' });
      // @ts-ignore
      window.AITPacing.setHint('unknown', 1);
    });
    expect((await pacingState(page)).capFps).toBe(0);
    expect(await ranDuring(page, 120, 1000 / 60)).toBe(120);
  });

  test('adaptiveFrameRate 를 켜면 압력 힌트에서만 30fps', async ({ page }) => {
    await openHarness(page, { frameRateCap: 0, mobileLifecycle: false, adaptiveFrameRate: true });
    await configurePacing(page);
    expect(await ranDuring(page, 120, 1000 / 60)).toBe(120);
    await page.evaluate(() => { /* @ts-ignore */ window.AITPacing.setHint('memory', { level: 'critical' }); });
    expect((await pacingState(page)).capFps).toBe(30);
    const ran = await ranDuring(page, 240, 1000 / 60); // 60Hz 로 4초
    expect(ran / 4).toBeGreaterThanOrEqual(28);
    expect(ran / 4).toBeLessThanOrEqual(32);
    await page.evaluate(() => { /* @ts-ignore */ window.AITPacing.setHint('memory', { level: 'ok' }); });
    expect((await pacingState(page)).capFps).toBe(0);
  });

  test('config.preMainLoop 가 이미 있으면 앞단에서 호출하고 false 를 존중한다', async ({ page }) => {
    await openHarness(page, { frameRateCap: 0, mobileLifecycle: true });
    await page.evaluate(() => {
      // @ts-ignore
      window.__userCalls = 0;
      // @ts-ignore
      window.__cfg.preMainLoop = () => { window.__userCalls++; return window.__userCalls % 2 ? true : false; };
      // @ts-ignore
      window.AITPacing.configure(window.__cfg);
    });
    const ran = await ranDuring(page, 100, 1000 / 60);
    const calls = await page.evaluate(() => { /* @ts-ignore */ return window.__userCalls; });
    expect(calls).toBe(100);
    expect(ran).toBe(50);
  });
});

// ============================================================================
// 2) 라이프사이클 게이트 (P0-4)
// ============================================================================
test.describe('라이프사이클 게이트', () => {
  test('hidden 이면 프레임이 0, visible 이 되면 재개', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 });
    await configurePacing(page);
    expect(await ranDuring(page, 30, 1000 / 60)).toBe(30);

    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(true); });
    expect(await ranDuring(page, 120, 1000 / 60)).toBe(0);
    let st = await pacingState(page);
    expect(st.hidden).toBe(true);
    expect(st.skippedHidden).toBe(120);
    expect(st.hiddenCount).toBe(1);

    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(false); });
    expect(await ranDuring(page, 30, 1000 / 60)).toBe(30);
    st = await pacingState(page);
    expect(st.hidden).toBe(false);
  });

  test('mobileLifecycle=false 면 hidden 이어도 프레임이 돈다(기존 동작)', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: false, frameRateCap: 60 });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(true); });
    expect(await ranDuring(page, 60, 1000 / 60)).toBe(60);
    expect((await pacingState(page)).hiddenCount).toBe(0);
  });

  test('?aitlifecycle=0 탈출구는 게이트만 끈다', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 }, { query: '?aitlifecycle=0' });
    await configurePacing(page);
    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(true); });
    expect(await ranDuring(page, 30, 1000 / 60)).toBe(30);
    expect((await pacingState(page)).lifecycle).toBe(false);
  });

  test('pagehide 는 pageshow 전까지, freeze 는 resume 전까지 게이트', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 });
    await configurePacing(page);

    await page.evaluate(() => window.dispatchEvent(new Event('pagehide')));
    expect(await ranDuring(page, 20, 1000 / 60)).toBe(0);
    await page.evaluate(() => window.dispatchEvent(new Event('pageshow')));
    expect(await ranDuring(page, 20, 1000 / 60)).toBe(20);

    await page.evaluate(() => document.dispatchEvent(new Event('freeze')));
    expect(await ranDuring(page, 20, 1000 / 60)).toBe(0);
    await page.evaluate(() => document.dispatchEvent(new Event('resume')));
    expect(await ranDuring(page, 20, 1000 / 60)).toBe(20);
  });

  test('이벤트를 놓쳐도 프레임 점검이 상태를 되돌린다(자가 치유)', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 });
    await configurePacing(page);
    await page.evaluate(() => {
      // visibilitychange 를 발행하지 않고 값만 바꾼다.
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
    });
    expect(await ranDuring(page, 10, 1000 / 60)).toBe(0);
    expect((await pacingState(page)).hidden).toBe(true);
    await page.evaluate(() => {
      // @ts-ignore
      delete document.visibilityState;
      // @ts-ignore
      delete document.hidden;
    });
    expect(await ranDuring(page, 10, 1000 / 60)).toBe(10);
    expect((await pacingState(page)).hidden).toBe(false);
  });

  test('AudioContext: running 이던 것만 suspend 하고 visible 에서 우리가 멈춘 것만 resume', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 });
    await configurePacing(page);
    await page.mouse.click(10, 10); // 사용자 활성화(자동 재생 정책)

    const setup = await page.evaluate(async () => {
      const a = new AudioContext();
      const b = new AudioContext();
      await a.resume();
      await b.resume();
      await b.suspend(); // 게임이 스스로 멈춘 context
      return {
        aState: a.state,
        bState: b.state,
        instanceOk: a instanceof AudioContext && a instanceof BaseAudioContext,
        // @ts-ignore
        keep: ((window.__ctxs = [a, b]), true),
      };
    });
    test.skip(setup.aState !== 'running', '이 환경에서는 AudioContext 가 running 이 되지 않는다(오디오 장치 없음)');
    expect(setup.instanceOk).toBe(true);
    expect((await pacingState(page)).trackedContexts).toBe(2);

    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(true); });
    await expect.poll(() => page.evaluate(() => { /* @ts-ignore */ return window.__ctxs[0].state; })).toBe('suspended');
    expect((await pacingState(page)).suspendedContexts).toBe(1);

    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(false); });
    await expect.poll(() => page.evaluate(() => { /* @ts-ignore */ return window.__ctxs[0].state; })).toBe('running');
    // 게임이 멈춰 둔 b 는 그대로
    await page.waitForTimeout(100);
    expect(await page.evaluate(() => { /* @ts-ignore */ return window.__ctxs[1].state; })).toBe('suspended');
  });

  test('HTMLMediaElement: 재생 중이던 것만 pause 하고 visible 에서 우리가 멈춘 것만 재개', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 });
    await configurePacing(page);
    await page.mouse.click(10, 10);

    const wav = silentWavDataUrl(20);
    const started = await page.evaluate(async (src) => {
      const mk = () => { const el = new Audio(); el.src = src; el.loop = true; return el; };
      const playingEl = mk(); // 우리가 멈췄다 재개해야 하는 요소
      const userPaused = mk(); // 사용자가 이미 멈춘 요소(재개 금지)
      const gamePausedWhileHidden = mk(); // hidden 중 게임이 직접 pause 한 요소(재개 금지)
      try {
        await playingEl.play();
        await userPaused.play();
        userPaused.pause();
        await gamePausedWhileHidden.play();
      } catch (e) {
        return { ok: false, err: String(e) };
      }
      // @ts-ignore
      window.__els = { playingEl, userPaused, gamePausedWhileHidden };
      return { ok: true };
    }, wav);
    test.skip(!started.ok, '이 환경에서는 미디어 재생이 거부된다: ' + (started.err || ''));

    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(true); });
    const hiddenState = await page.evaluate(() => {
      // @ts-ignore
      const e = window.__els;
      // 게임이 hidden 중에 직접 pause
      e.gamePausedWhileHidden.pause();
      return { playing: e.playingEl.paused, user: e.userPaused.paused, game: e.gamePausedWhileHidden.paused };
    });
    expect(hiddenState).toEqual({ playing: true, user: true, game: true });
    expect((await pacingState(page)).heldMedia).toBe(1);

    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(false); });
    await expect.poll(() => page.evaluate(() => { /* @ts-ignore */ return window.__els.playingEl.paused; })).toBe(false);
    const after = await page.evaluate(() => {
      // @ts-ignore
      const e = window.__els;
      return { user: e.userPaused.paused, game: e.gamePausedWhileHidden.paused };
    });
    expect(after).toEqual({ user: true, game: true });
  });

  test('visibilitychange 리스너(AITVisibilityHelper 경로)는 게이트와 무관하게 호출된다', async ({ page }) => {
    await openHarness(page, { mobileLifecycle: true, frameRateCap: 0 });
    await configurePacing(page);
    const seen = await page.evaluate(() => {
      const log = [];
      document.addEventListener('visibilitychange', () => log.push(document.visibilityState));
      // @ts-ignore
      window.__setHidden(true);
      // @ts-ignore
      window.__setHidden(false);
      return log;
    });
    expect(seen).toEqual(['hidden', 'visible']);
  });
});

// ============================================================================
// 3) 메모리 텔레메트리 (P1-6)
// ============================================================================
test.describe('메모리 텔레메트리', () => {
  test('Memory.grow 를 기록하고 반환값을 바꾸지 않으며 이벤트는 비동기로만 발행', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: true }, { pacing: false, virtualClock: false });
    const r = await page.evaluate(async () => {
      const events = [];
      window.addEventListener('ait:memory', (e) => events.push(e.detail));
      const mem = new WebAssembly.Memory({ initial: 512, maximum: 4096 }); // 32MB
      const ret1 = mem.grow(512); // → 64MB
      const ret2 = mem.grow(1024); // → 128MB
      const syncEvents = events.length;
      await new Promise((res) => setTimeout(res, 50));
      // @ts-ignore
      const g = window.__AIT_HEAP_GROW;
      return {
        ret1, ret2, syncEvents,
        asyncEvents: events.length,
        lastType: events.length ? events[events.length - 1].type : null,
        count: g.count, failures: g.failures, seq: g.sequenceMB, initial: g.initialBytes, peak: g.peakBytes,
        bytes: mem.buffer.byteLength,
        // @ts-ignore
        enabled: window.AITMemory.enabled,
      };
    });
    expect(r.enabled).toBe(true);
    expect(r.ret1).toBe(512);
    expect(r.ret2).toBe(1024);
    expect(r.syncEvents).toBe(0); // wasm 호출 스택 위에서 동기 발행 금지
    expect(r.asyncEvents).toBe(1); // 한 태스크에 합쳐서 1회
    expect(r.count).toBe(2);
    expect(r.failures).toBe(0);
    expect(r.seq).toEqual([64, 128]);
    expect(r.initial).toBe(32 * 1048576);
    expect(r.peak).toBe(128 * 1048576);
    expect(r.bytes).toBe(128 * 1048576);
  });

  test('grow 실패는 같은 예외를 다시 던지고 failures 로 기록한다', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: true }, { pacing: false, virtualClock: false });
    const r = await page.evaluate(async () => {
      const mem = new WebAssembly.Memory({ initial: 1, maximum: 2 });
      let name = null;
      try { mem.grow(10); } catch (e) { name = e && e.name; }
      await new Promise((res) => setTimeout(res, 20));
      // @ts-ignore
      const g = window.__AIT_HEAP_GROW;
      // @ts-ignore
      return { name, failures: g.failures, count: g.count, seq: g.sequenceMB, level: window.AITMemory.getLevel(), ev: g.events[0] };
    });
    expect(r.name).toBe('RangeError');
    expect(r.failures).toBe(1);
    expect(r.count).toBe(1);
    expect(r.seq).toEqual([]);
    expect(r.level).toBe('high');
    expect(r.ev.ok).toBe(false);
  });

  test('임계 크기를 넘으면 pressure 이벤트와 AITPacing 힌트', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: true, memHighMB: 64, memCriticalMB: 96, adaptiveFrameRate: true, frameRateCap: 0, mobileLifecycle: false },
      { virtualClock: true });
    await configurePacing(page);
    const r = await page.evaluate(async () => {
      const events = [];
      window.addEventListener('ait:memory', (e) => events.push(e.detail));
      const mem = new WebAssembly.Memory({ initial: 256, maximum: 4096 }); // 16MB
      mem.grow(768); // 64MB → high
      await new Promise((res) => setTimeout(res, 20));
      mem.grow(512); // 96MB → critical
      await new Promise((res) => setTimeout(res, 20));
      // @ts-ignore
      return { levels: events.map((e) => e.level), types: events.map((e) => e.type), hint: window.AITPacing.getState().hints.memory };
    });
    expect(r.levels).toEqual(['high', 'critical']);
    expect(r.types).toEqual(['pressure', 'pressure']);
    expect(r.hint.level).toBe('critical');
    // adaptive 가 켜져 있으므로 60Hz 가정에서 30fps 상한이 걸린다.
    expect((await pacingState(page)).capFps).toBe(30);
  });

  test('memoryTelemetry=false 면 아무것도 설치하지 않고 crashCount 는 0', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: false }, { pacing: false, virtualClock: false,
      preScript: "try{sessionStorage.setItem('__ait_mem_v1', JSON.stringify({phase:'fg',crashes:5}))}catch(e){}" });
    const r = await page.evaluate(() => {
      const mem = new WebAssembly.Memory({ initial: 1, maximum: 8 });
      mem.grow(1);
      // @ts-ignore
      return { enabled: window.AITMemory.enabled, crash: window.AITMemory.crashCount, grow: typeof window.__AIT_HEAP_GROW, wrapped: !!WebAssembly.Memory.prototype.grow.__aitMem };
    });
    expect(r).toEqual({ enabled: false, crash: 0, grow: 'undefined', wrapped: false });
  });

  test('이전 세션이 포그라운드에서 정상 종료 신호 없이 끝났으면 crashCount 가 늘고 연속 횟수를 누적', async ({ page }) => {
    // ?seed=fg:N — 부팅 직전에 "이전 세션이 포그라운드에서 죽은" 마커를 심는다(실제 크래시는 Playwright 에서 복구할 수 없다).
    const seedScript = "(function(){var m=/[?&]seed=(fg|bg|exit):(\\d+)/.exec(location.search);" +
      "if(m)try{sessionStorage.setItem('__ait_mem_v1',JSON.stringify({phase:m[1],crashes:+m[2],bgKills:0}))}catch(e){}})();";
    const events = [];
    await page.exposeFunction('__recordMemEvent', (d) => events.push(d));
    await page.addInitScript(() => {
      window.addEventListener('ait:memory', (e) => {
        // @ts-ignore
        window.__recordMemEvent(e.detail);
      });
    });

    await openHarness(page, { memoryTelemetry: true }, { pacing: false, virtualClock: false, preScript: seedScript, query: '?seed=fg:1' });
    let s = await page.evaluate(() => { /* @ts-ignore */ return window.AITMemory.getState(); });
    expect(s.crashCount).toBe(2);
    expect(s.prevSession).toBe('foreground');
    await expect.poll(() => events.some((e) => e.type === 'crash' && e.crashCount === 2)).toBe(true);

    // 백그라운드에서 죽은 세션은 crashCount 에 넣지 않는다
    await page.goto(`${HARNESS_ORIGIN}/index.html?seed=bg:1`);
    s = await page.evaluate(() => { /* @ts-ignore */ return window.AITMemory.getState(); });
    expect(s.crashCount).toBe(1);
    expect(s.bgKillCount).toBe(1);
    expect(s.prevSession).toBe('background');

    // 정상 종료(pagehide) 뒤 재방문은 크래시가 아니다
    await page.goto(`${HARNESS_ORIGIN}/index.html?seed=exit:0`);
    s = await page.evaluate(() => { /* @ts-ignore */ return window.AITMemory.getState(); });
    expect(s.crashCount).toBe(0);
    expect(s.prevSession).toBe('exit');
  });

  test('일반 reload 는 크래시로 세지 않는다(pagehide 가 마커를 정리)', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: true }, { pacing: false, virtualClock: false });
    expect(await page.evaluate(() => { /* @ts-ignore */ return window.AITMemory.crashCount; })).toBe(0);
    await page.reload();
    const s = await page.evaluate(() => { /* @ts-ignore */ return window.AITMemory.getState(); });
    expect(s.crashCount).toBe(0);
    expect(s.prevSession).toBe('exit');
  });

  test('visibilitychange 로 hidden 이 되면 마커가 bg 로 바뀐다', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: true }, { pacing: false, virtualClock: true });
    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(true); });
    const phase = await page.evaluate(() => JSON.parse(sessionStorage.getItem('__ait_mem_v1') || '{}').phase);
    expect(phase).toBe('bg');
    await page.evaluate(() => { /* @ts-ignore */ window.__setHidden(false); });
    expect(await page.evaluate(() => JSON.parse(sessionStorage.getItem('__ait_mem_v1') || '{}').phase)).toBe('fg');
  });

  test('C# 브릿지가 등록되면 unityInstance.SendMessage 로 요약 JSON 을 보낸다', async ({ page }) => {
    await openHarness(page, { memoryTelemetry: true }, { pacing: false, virtualClock: false });
    const r = await page.evaluate(async () => {
      const sent = [];
      // @ts-ignore
      window.unityInstance = { SendMessage: (obj, method, payload) => sent.push({ obj, method, payload }) };
      // 등록 전에는 아무것도 보내지 않는다
      const mem = new WebAssembly.Memory({ initial: 16, maximum: 1024 });
      mem.grow(16);
      await new Promise((res) => setTimeout(res, 20));
      const before = sent.length;
      // @ts-ignore
      window.AITMemory.__bridgeRegister();
      await new Promise((res) => setTimeout(res, 20));
      return { before, sent };
    });
    expect(r.before).toBe(0);
    expect(r.sent.length).toBeGreaterThanOrEqual(1);
    expect(r.sent[0].obj).toBe('AITMemoryBridge');
    expect(r.sent[0].method).toBe('OnMemoryEvent');
    const payload = JSON.parse(r.sent[0].payload);
    expect(payload.growCount).toBe(1);
    expect(typeof payload.heapBytes).toBe('number');
  });
});

// ============================================================================
// 4) 빌드 페이지 (AIT_PACING_BUILT=1)
// ============================================================================
const BUILT = process.env.AIT_PACING_BUILT === '1';

function directoryExists(p) {
  try { return fs.existsSync(p) && fs.statSync(p).isDirectory(); } catch { return false; }
}

function findBuiltProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && directoryExists(path.resolve(envPath, 'ait-build/dist/web'))) return envPath;
  const versions = ['6000.3', '6000.0', '6000.2', '2022.3', '2021.3'];
  for (const prefix of ['HeavySampleUnityProject', 'SampleUnityProject']) {
    for (const v of versions) {
      const p = path.resolve(__dirname, `../${prefix}-${v}`);
      if (directoryExists(path.resolve(p, 'ait-build/dist/web'))) return p;
    }
  }
  return null;
}

const BUILT_PROJECT = BUILT ? findBuiltProject() : null;
const BUILT_AIT_BUILD = BUILT_PROJECT ? path.resolve(BUILT_PROJECT, 'ait-build') : null;
const BUILT_PORT = 4423;

function isPortAvailable(port) {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once('error', () => resolve(false));
    server.once('listening', () => server.close(() => resolve(true)));
    server.listen(port, '127.0.0.1');
  });
}

function freePort(port) {
  try {
    if (process.platform === 'win32') {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      execSync(`lsof -ti tcp:${port} -sTCP:LISTEN | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch { /* 정리 실패 무시 */ }
}

/** @type {import('child_process').ChildProcess | null} */
let builtServer = null;

async function startBuiltServer() {
  freePort(BUILT_PORT);
  for (let i = 0; i < 25 && !(await isPortAvailable(BUILT_PORT)); i++) await new Promise((r) => setTimeout(r, 200));
  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(BUILT_PORT)], {
      cwd: BUILT_AIT_BUILD, stdio: 'pipe', shell: true, env: { ...process.env, NODE_OPTIONS: '' },
    });
    builtServer = server;
    let started = false;
    server.stdout.on('data', (d) => {
      if (!started && /Local:|listening|Accepting connections|ready/.test(d.toString())) { started = true; resolve(true); }
    });
    server.on('error', reject);
    setTimeout(() => { if (!started) { started = true; resolve(true); } }, 10000);
  });
}

test.describe('빌드 페이지', () => {
  test.skip(!BUILT || !BUILT_PROJECT, 'AIT_PACING_BUILT=1 이고 빌드 산출물(ait-build/dist/web)이 있을 때만 실행');
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(async () => {
    if (!BUILT_PROJECT) return;
    await startBuiltServer();
    for (let i = 0; i < 30; i++) {
      try { const res = await fetch(`http://localhost:${BUILT_PORT}/`, { method: 'HEAD' }); if (res.ok) return; } catch { /* 재시도 */ }
      await new Promise((r) => setTimeout(r, 500));
    }
  });

  test.afterAll(async () => {
    if (builtServer) { try { builtServer.kill('SIGTERM'); } catch { /* 무시 */ } builtServer = null; }
    freePort(BUILT_PORT);
  });

  async function openBuilt(page, query = '') {
    const logs = [];
    page.on('console', (m) => { const t = m.text(); if (/\[AIT-(Pacing|Memory)\]/.test(t)) logs.push(t); });
    await page.goto(`http://localhost:${BUILT_PORT}/?e2e=true${query}`, { waitUntil: 'commit', timeout: 120000 });
    await page.waitForFunction(() => window['unityInstance'] !== undefined, undefined, { timeout: 240000 });
    return logs;
  }

  test('런타임 스텁이 실제 구현으로 대체되어 있고 플래그가 주입됨', async ({ page }) => {
    const logs = await openBuilt(page);
    const info = await page.evaluate(() => ({
      perf: window['__AIT_PERF'],
      pacing: window['AITPacing'] && Object.keys(window['AITPacing']),
      stubP: !!(window['AITPacing'] && window['AITPacing'].__stub),
      stubM: !!(window['AITMemory'] && window['AITMemory'].__stub),
      mem: window['AITMemory'] && window['AITMemory'].getState && window['AITMemory'].getState(),
      pacingState: window['AITPacing'] && window['AITPacing'].getState && window['AITPacing'].getState(),
    }));
    console.log(`  __AIT_PERF=${JSON.stringify(info.perf)}`);
    console.log(`  pacing=${JSON.stringify(info.pacingState)}`);
    console.log(`  memory=${JSON.stringify(info.mem)}`);
    for (const l of logs.slice(0, 20)) console.log(`    console: ${l}`);
    expect(info.stubP).toBe(false);
    expect(info.stubM).toBe(false);
    expect(info.pacing).toContain('getState');
    expect(info.mem && info.mem.wrapped).toBe(true);
  });

  test('hidden 동안 postMainLoop 가 0, visible 이면 재개', async ({ page }) => {
    await openBuilt(page);
    // 메인 루프가 돌 때까지 대기 후 postMainLoop 카운터를 건다(emscripten 이 프레임마다 Module.postMainLoop 를 읽는다).
    const hooked = await page.evaluate(() => {
      const u = window['unityInstance'];
      const M = u && u.Module;
      if (!M) return false;
      window['__pml'] = 0;
      const prev = M.postMainLoop;
      M.postMainLoop = function () { window['__pml']++; if (prev) return prev.apply(this, arguments); };
      return true;
    });
    expect(hooked, 'unityInstance.Module 을 찾을 수 없음').toBe(true);

    const count = () => page.evaluate(() => window['__pml']);
    await expect.poll(count, { timeout: 30000 }).toBeGreaterThan(5);

    await page.evaluate(() => {
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    await page.waitForTimeout(500); // 진행 중이던 프레임 정리
    const a = await count();
    await page.waitForTimeout(1500);
    const b = await count();
    expect(b - a, 'hidden 동안 postMainLoop 증가량').toBe(0);

    await page.evaluate(() => {
      delete document['visibilityState'];
      delete document['hidden'];
      document.dispatchEvent(new Event('visibilitychange'));
    });
    await expect.poll(count, { timeout: 15000 }).toBeGreaterThan(b);
    const st = await page.evaluate(() => window['AITPacing'].getState());
    console.log(`  pacing after resume=${JSON.stringify(st)}`);
    expect(st.hiddenCount).toBeGreaterThanOrEqual(1);
  });

  test('Memory.grow 시퀀스 기록(기록 전용)', async ({ page }) => {
    await openBuilt(page);
    const g = await page.evaluate(() => {
      const h = window['__AIT_HEAP_GROW'];
      const M = window['unityInstance'] && window['unityInstance'].Module;
      return h ? { count: h.count, failures: h.failures, totalMs: h.totalMs, maxMs: h.maxMs, seq: h.sequenceMB, initial: h.initialBytes, heap: M && M.HEAPU8 ? M.HEAPU8.length : null } : null;
    });
    console.log(`  heap grow: ${JSON.stringify(g)}`);
    expect(g, '__AIT_HEAP_GROW 없음').not.toBeNull();
    expect(g.failures).toBe(0);
    for (let i = 1; i < g.seq.length; i++) expect(g.seq[i]).toBeGreaterThan(g.seq[i - 1]);
    if (g.count > 0 && g.heap) expect(Math.abs(g.heap / 1048576 - g.seq[g.seq.length - 1])).toBeLessThan(1);
  });
});
