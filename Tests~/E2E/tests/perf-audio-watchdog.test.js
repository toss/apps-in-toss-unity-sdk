// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — ait-pacing.js 오디오 복구/진단 테스트 (P1-3)
 *
 *  - AudioContext 'interrupted' 복구: 1초 간격 최대 10회 resume 재시도, 첫 터치/클릭, visible 복귀
 *  - 우리가 suspend 하지 않은 plain 'suspended' context 는 건드리지 않는다
 *  - 미디어 stall watchdog: 재생 가능한데 currentTime 이 멈추면 seek → pause/play 로 깨우고 audioLog/audioStalls 에 남긴다
 *  - ?aitaudiowd=0 이면 watchdog 비활성
 *
 * Unity 빌드가 필요 없다. 가짜 origin 의 빈 페이지에 ait-pacing.js 를 올리고 page.clock 으로 setInterval 을 결정론적으로 돌린다.
 * AudioContext 는 상태를 테스트가 정하는 가짜 클래스로, HTMLMediaElement.play/pause 는 호출 횟수만 세는 스파이로 대체한다
 * (ait-pacing.js 가 로드 시점에 원본을 붙잡으므로 preScript 로 먼저 심는다).
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const RUNTIME_DIR = path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/Runtime');
const PACING_JS = fs.readFileSync(path.join(RUNTIME_DIR, 'ait-pacing.js'), 'utf8');

const HARNESS_ORIGIN = 'https://ait-harness.test';

/** ait-pacing.js 보다 먼저 실행되는 스크립트: 가짜 AudioContext + play/pause 스파이 + visibility 헬퍼. */
const PRE_SCRIPT = `
  window.__ctxs = [];
  class FakeAudioContext extends EventTarget {
    constructor() {
      super();
      this._state = 'running';
      this.resumeCalls = 0;
      this.suspendCalls = 0;
      window.__ctxs.push(this);
    }
    get state() { return this._state; }
    // 테스트가 상태를 정하고 statechange 를 쏜다.
    __set(s) { this._state = s; this.dispatchEvent(new Event('statechange')); }
    resume() { this.resumeCalls++; return Promise.resolve(); }
    suspend() { this.suspendCalls++; return Promise.resolve(); }
  }
  window.AudioContext = FakeAudioContext;
  window.webkitAudioContext = FakeAudioContext;

  window.__media = { play: 0, pause: 0 };
  HTMLMediaElement.prototype.play = function () { window.__media.play++; return Promise.resolve(); };
  HTMLMediaElement.prototype.pause = function () { window.__media.pause++; };

  window.__setHidden = function (hidden) {
    if (hidden) {
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', writable: true, configurable: true });
      Object.defineProperty(document, 'hidden', { value: true, writable: true, configurable: true });
    } else {
      delete document.visibilityState;
      delete document.hidden;
    }
    document.dispatchEvent(new Event('visibilitychange'));
  };
`;

function harnessHtml(flags, extraHead = '') {
  return [
    '<!doctype html><html><head><meta charset="utf-8"><title>ait-audio-harness</title>',
    `<script>window.__AIT_PERF = ${JSON.stringify(flags)};</script>`,
    extraHead ? `<script>${extraHead}</script>` : '',
    `<script>${PRE_SCRIPT}</script>`,
    '</head><body>',
    `<script>${PACING_JS}</script>`,
    '</body></html>',
  ].join('\n');
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {{query?: string, flags?: object, head?: string}} [opts]
 */
async function openSynthetic(page, opts = {}) {
  await page.clock.install({ time: 0 });
  await page.route(`${HARNESS_ORIGIN}/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: harnessHtml(opts.flags || {}, opts.head || '') }));
  await page.goto(`${HARNESS_ORIGIN}/index.html${opts.query || ''}`);
}

const resumeCalls = (page, i = 0) => page.evaluate((idx) => /** @type {any} */ (window).__ctxs[idx].resumeCalls, i);
const setCtxState = (page, i, s) => page.evaluate(([idx, st]) => /** @type {any} */ (window).__ctxs[idx].__set(st), [i, s]);
const pacingState = (page) => page.evaluate(() => /** @type {any} */ (window).AITPacing.getState());
const mediaCalls = (page) => page.evaluate(() => ({ .../** @type {any} */ (window).__media }));

/** 재생 중이고 currentTime 만 멈춘 <audio> 를 만들어 래핑된 play() 로 등록한다. seek 시도는 window.__seeks 로 센다. */
async function addFrozenAudio(page, { paused = false } = {}) {
  await page.evaluate((isPaused) => {
    const w = /** @type {any} */ (window);
    w.__seeks = 0;
    const el = document.createElement('audio');
    document.body.appendChild(el);
    el.play(); // 래핑된 play → trackMedia
    Object.defineProperty(el, 'paused', { get: () => isPaused, configurable: true });
    Object.defineProperty(el, 'ended', { get: () => false, configurable: true });
    Object.defineProperty(el, 'seeking', { get: () => false, configurable: true });
    Object.defineProperty(el, 'readyState', { get: () => 4, configurable: true });
    Object.defineProperty(el, 'playbackRate', { get: () => 1, configurable: true });
    Object.defineProperty(el, 'currentTime', { get: () => 12.5, set: () => { w.__seeks++; }, configurable: true });
    w.__el = el;
    w.__media.play = 0; // 등록용 play() 호출은 센 값에서 뺀다
  }, paused);
}

test.describe('AudioContext interrupted 복구', () => {
  test('visible 에서 interrupted 가 되면 1초 간격으로 resume 을 반복하고 running 이 되면 멈춘다, 클릭은 즉시 resume', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    expect(await resumeCalls(page)).toBe(0);

    await setCtxState(page, 0, 'interrupted');
    await page.clock.runFor(3000);
    expect(await resumeCalls(page)).toBe(3);

    await setCtxState(page, 0, 'running');
    await page.clock.runFor(3000);
    expect(await resumeCalls(page)).toBe(3);

    const log = (await pacingState(page)).audioLog;
    expect(log.filter((e) => e.ev === 'ctx').map((e) => e.state)).toEqual(['interrupted', 'running']);
    expect(log[0]).toMatchObject({ ev: 'ctx', vis: 'visible' });

    // 다시 interrupted: 재시도 틱이 오기 전에 클릭하면 바로 resume 한다.
    await setCtxState(page, 0, 'interrupted');
    await page.evaluate(() => document.body.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(await resumeCalls(page)).toBe(4);
  });

  test('재시도는 최대 10회에서 멈춘다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    await setCtxState(page, 0, 'interrupted');
    await page.clock.runFor(15000);
    expect(await resumeCalls(page)).toBe(10);
  });

  test('hidden 중에는 재시도하지 않고 visible 복귀(onShow)에서 interrupted 를 resume 한다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    expect((await pacingState(page)).hidden).toBe(true);

    await setCtxState(page, 0, 'interrupted');
    await page.clock.runFor(3000);
    expect(await resumeCalls(page)).toBe(0);

    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    expect(await resumeCalls(page)).toBe(1);
  });

  test('우리가 멈춘 context 가 복귀 resume 뒤에도 suspended 로 남으면 running 까지 재시도·클릭 복구하고, 그 뒤엔 게임 소유로 돌려준다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await setCtxState(page, 0, 'suspended'); // 우리 suspend 가 반영된 상태
    expect(await resumeCalls(page)).toBe(0);

    // 복귀: resume 이 거절돼(가짜 resume 은 상태를 바꾸지 않는다) suspended 로 남는다
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    expect(await resumeCalls(page)).toBe(1);
    await page.clock.runFor(3000);
    expect(await resumeCalls(page)).toBe(4);
    await page.evaluate(() => document.body.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(await resumeCalls(page)).toBe(5);

    await setCtxState(page, 0, 'running');
    await page.clock.runFor(3000);
    expect(await resumeCalls(page)).toBe(5);

    // running 에 도달한 뒤 게임이 직접 멈춘 것은 건드리지 않는다
    await setCtxState(page, 0, 'suspended');
    await page.clock.runFor(3000);
    await page.evaluate(() => document.body.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(await resumeCalls(page)).toBe(5);
  });

  test('복귀 뒤 running 인데 currentTime 이 멈춰 있으면 suspend → resume 으로 다시 열고, 움직이면 감시를 끝낸다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); /** @type {any} */ (window).__ctxs[0].currentTime = 10; });
    const suspendCalls = () => page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls);
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    expect(await suspendCalls()).toBe(1); // hide 때의 suspend

    await page.clock.runFor(1000);
    expect(await suspendCalls()).toBe(2);
    await page.clock.runFor(1000);
    expect(await suspendCalls()).toBe(3);

    // 자동 시도는 5회까지, 그 뒤로는 터치할 때만
    await page.clock.runFor(10000);
    expect(await suspendCalls()).toBe(6);
    await page.evaluate(() => document.body.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(await suspendCalls()).toBe(7);

    let t = 10;
    for (let i = 0; i < 5; i++) {
      t += 1;
      await page.evaluate((v) => { /** @type {any} */ (window).__ctxs[0].currentTime = v; }, t);
      await page.clock.runFor(500);
    }
    await page.clock.runFor(10000);
    expect(await suspendCalls()).toBe(7);
    expect((await pacingState(page)).audioLog.some((e) => e.ev === 'clockOk')).toBe(true);
  });

  test('hidden 전에 interrupted 가 온 context 도 복귀 뒤 suspended 재시도와 멈춘 시계 복구를 받는다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); /** @type {any} */ (window).__ctxs[0].currentTime = 10; });
    const suspendCalls = () => page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls);
    await setCtxState(page, 0, 'interrupted');
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    expect(await suspendCalls()).toBe(0);
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));

    // iOS 가 복귀 때 plain suspended 로 바꾸고 resume 을 거절한다
    await setCtxState(page, 0, 'suspended');
    const before = await resumeCalls(page);
    await page.clock.runFor(2000);
    expect(await resumeCalls(page)).toBeGreaterThan(before);

    await setCtxState(page, 0, 'running');
    await page.clock.runFor(1000);
    expect(await suspendCalls()).toBe(1);
  });

  test('복귀 resume 이 거절됐던 context 는 running 이 되자마자 멈춤 확인 없이 다시 연다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => {
      new AudioContext();
      const c = /** @type {any} */ (window).__ctxs[0];
      c.currentTime = 10;
      c.resume = function () { this.resumeCalls++; return Promise.reject(new DOMException('x', 'InvalidStateError')); };
    });
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await setCtxState(page, 0, 'suspended');
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    await page.evaluate(() => new Promise((r) => { queueMicrotask(r); }));
    await page.evaluate(() => { /** @type {any} */ (window).__ctxs[0].resume = function () { this.resumeCalls++; return Promise.resolve(); }; });
    await setCtxState(page, 0, 'running');
    expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls)).toBe(2);
  });

  test('짧은 효과음은 살짝 늦춰 시작하고 작은 offset 만 0 으로 두며 긴 클립·큰 offset 은 그대로 둔다', async ({ page }) => {
    await page.addInitScript(() => {
      /** @type {any} */ (window).__starts = [];
      AudioBufferSourceNode.prototype.start = function (when, offset) { /** @type {any} */ (window).__starts.push([when, offset]); };
    });
    await openSynthetic(page);
    const got = await page.evaluate(() => {
      const c = new OfflineAudioContext(1, 44100, 44100);
      const make = (sec, loop) => { const s = c.createBufferSource(); s.buffer = c.createBuffer(1, Math.round(44100 * sec), 44100); s.loop = loop; return s; };
      make(0.2, false).start(0, 0.0027);
      make(0.2, false).start(0.5, 0);
      make(0.2, false).start(0, 0.08);
      make(0.2, true).start(0, 0.0027);
      make(5, false).start(0, 0.0027);
      return /** @type {any} */ (window).__starts;
    });
    expect(got).toEqual([[0.01, 0], [0.5, 0], [0, 0.08], [0.01, 0.0027], [0, 0.0027]]);
  });

  test('복귀 뒤 currentTime 이 움직이면 다시 열지 않는다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); /** @type {any} */ (window).__ctxs[0].currentTime = 10; });
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    for (let t = 11; t < 15; t++) {
      await page.evaluate((v) => { /** @type {any} */ (window).__ctxs[0].currentTime = v; }, t);
      await page.clock.runFor(500);
    }
    expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls)).toBe(1);
  });

  test('우리가 suspend 하지 않은 plain suspended context 는 클릭·재시도·onShow 어디서도 resume 하지 않는다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); new AudioContext(); });
    await setCtxState(page, 0, 'suspended'); // 게임/Unity 소유
    await setCtxState(page, 1, 'interrupted'); // 클릭 리스너 설치용

    await page.clock.runFor(2000);
    await page.evaluate(() => document.body.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    await page.clock.runFor(3000);

    expect(await resumeCalls(page, 0)).toBe(0);
    expect(await resumeCalls(page, 1)).toBeGreaterThan(0);
  });
});

test.describe('미디어 stall watchdog', () => {
  test('재생 가능한데 currentTime 이 멈추면 stall 1회 기록 + seek, 이어서 pause/play', async ({ page }) => {
    const warns = [];
    page.on('console', (m) => { if (m.text().includes('[AIT-Audio] stall')) warns.push(m.text()); });
    await openSynthetic(page);
    await addFrozenAudio(page);
    expect((await pacingState(page)).trackedMedia).toBe(1);

    // 틱 1: 기준값 기록, 틱 2: frozen=1, 틱 3: frozen=2 → stall + seek
    await page.clock.runFor(3000);
    let s = await pacingState(page);
    expect(s.audioStalls).toBe(1);
    const stallLog = s.audioLog.filter((e) => e.ev === 'stall');
    expect(stallLog).toHaveLength(1);
    expect(stallLog[0]).toMatchObject({ ct: 12.5, rs: 4, vis: 'visible' });
    expect(warns).toHaveLength(1);
    expect(await page.evaluate(() => /** @type {any} */ (window).__seeks)).toBe(1);
    expect(await mediaCalls(page)).toEqual({ play: 0, pause: 0 });

    // 틱 5: frozen=4 → 원본 pause → play (스파이로 확인)
    await page.clock.runFor(2000);
    expect(await mediaCalls(page)).toEqual({ play: 1, pause: 1 });
    s = await pacingState(page);
    expect(s.audioStalls).toBe(1);
    expect(warns).toHaveLength(1);
    expect(s.heldMedia).toBe(0);
  });

  test('60초 안에는 3회까지만 개입한다', async ({ page }) => {
    await openSynthetic(page);
    await addFrozenAudio(page);
    // 한 번의 정지 구간에 개입 2회(seek, replay). 계속 멈춰 있어도 frozen 이 4를 넘으면 더는 개입하지 않는다.
    await page.clock.runFor(30000);
    expect(await page.evaluate(() => /** @type {any} */ (window).__seeks)).toBe(1);
    expect(await mediaCalls(page)).toEqual({ play: 1, pause: 1 });
  });

  test('paused 요소는 stall 로 보지 않는다', async ({ page }) => {
    await openSynthetic(page);
    await addFrozenAudio(page, { paused: true });
    await page.clock.runFor(6000);
    const s = await pacingState(page);
    expect(s.audioStalls).toBe(0);
    expect(await page.evaluate(() => /** @type {any} */ (window).__seeks)).toBe(0);
    expect(await mediaCalls(page)).toEqual({ play: 0, pause: 0 });
  });

  test('document 가 hidden 이면 stall 로 보지 않는다', async ({ page }) => {
    await openSynthetic(page);
    await addFrozenAudio(page);
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await page.clock.runFor(6000);
    const s = await pacingState(page);
    expect(s.audioStalls).toBe(0);
    expect(await page.evaluate(() => /** @type {any} */ (window).__seeks)).toBe(0);
    // hidden 진입 시 게이트가 요소를 pause 한다(원본 pause 1회) — 그 외 개입은 없다.
    expect((await mediaCalls(page)).play).toBe(0);
  });

  test('?aitaudiowd=0 이면 watchdog 이 동작하지 않는다', async ({ page }) => {
    await openSynthetic(page, { query: '?aitaudiowd=0' });
    await addFrozenAudio(page);
    await page.clock.runFor(8000);
    const s = await pacingState(page);
    expect(s.audioStalls).toBe(0);
    expect(s.audioLog.filter((e) => e.ev === 'stall')).toHaveLength(0);
    expect(await page.evaluate(() => /** @type {any} */ (window).__seeks)).toBe(0);
    expect(await mediaCalls(page)).toEqual({ play: 0, pause: 0 });
  });

  test('audioLog 는 최근 120건만 유지하고 복사본을 돌려준다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    for (let i = 0; i < 65; i++) {
      await setCtxState(page, 0, 'suspended');
      await setCtxState(page, 0, 'running');
    }
    const s = await pacingState(page);
    expect(s.audioLog).toHaveLength(120);
    s.audioLog.length = 0;
    expect((await pacingState(page)).audioLog).toHaveLength(120);
  });
});

test.describe('오디오 복구 옵트아웃 (__AIT_AUDIO_RESUME=false, ?aitaudio=0)', () => {
  const cases = [
    { name: 'window.__AIT_AUDIO_RESUME = false', opts: { head: 'window.__AIT_AUDIO_RESUME = false;' } },
    { name: '?aitaudio=0', opts: { query: '?aitaudio=0' } },
  ];
  for (const c of cases) {
    test(`${c.name}: AudioContext 복구는 꺼지고 hidden 게이트·미디어 pause 는 유지된다`, async ({ page }) => {
      await openSynthetic(page, c.opts);
      await page.evaluate(() => { new AudioContext(); });
      expect((await pacingState(page)).trackedContexts).toBe(0);

      // interrupted 가 돼도 재시도하지 않는다.
      await setCtxState(page, 0, 'interrupted');
      await page.clock.runFor(3000);
      expect(await resumeCalls(page)).toBe(0);

      // hidden 이어도 context 를 suspend 하지 않지만 라이프사이클 게이트는 켜져 있다.
      await setCtxState(page, 0, 'running');
      await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
      let s = await pacingState(page);
      expect(s.hidden).toBe(true);
      expect(s.audioRecovery).toBe(false);
      expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls)).toBe(0);
      await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
      expect(await resumeCalls(page)).toBe(0);
      expect((await pacingState(page)).hidden).toBe(false);
      expect(await page.evaluate(() => /** @type {any} */ (window).AITAudio.getState().enabled)).toBe(false);
    });
  }

  test('옵트아웃이 없으면 AITAudio.getState 가 enabled 이고 복구가 동작한다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    await setCtxState(page, 0, 'interrupted');
    await page.clock.runFor(2000);
    expect(await resumeCalls(page)).toBe(2);
    expect(await page.evaluate(() => /** @type {any} */ (window).AITAudio.getState().enabled)).toBe(true);
  });
});
