// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — ait-audio.js 단위 테스트 (iOS 앱 복귀 오디오 복구)
 *
 *  - AudioContext 'interrupted' 복구: 1초 간격 최대 10회 resume 재시도, 첫 터치/클릭, visible 복귀
 *  - hidden 에서 running context suspend + 재생 중 미디어 pause, visible 에서 우리가 멈춘 것만 재개
 *  - running 인데 currentTime 이 멈춘 context 는 suspend → resume 으로 다시 연다(자동 5회, 이후 터치마다)
 *  - 복귀 마스터 gain 40ms 페이드 인, 짧은 효과음 start 보정
 *  - 끄는 법: window.__AIT_AUDIO_RESUME=false / __AIT_PERF.mobileLifecycle=false / ?aitaudio=0
 *
 * Unity 빌드가 필요 없다. 가짜 origin 의 빈 페이지에 ait-audio.js 를 올리고 page.clock 으로 setInterval 을 결정론적으로 돌린다.
 * AudioContext 는 상태를 테스트가 정하는 가짜 클래스로, HTMLMediaElement.play/pause 는 호출 횟수만 세는 스파이로 대체한다
 * (ait-audio.js 가 로드 시점에 원본을 붙잡으므로 preScript 로 먼저 심는다).
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const RUNTIME_DIR = path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/Runtime');
const AUDIO_JS = fs.readFileSync(path.join(RUNTIME_DIR, 'ait-audio.js'), 'utf8');

const HARNESS_ORIGIN = 'https://ait-harness.test';

/** ait-audio.js 보다 먼저 실행되는 스크립트: 가짜 AudioContext + play/pause 스파이 + visibility 헬퍼. */
const PRE_SCRIPT = `
  window.__ctxs = [];
  class FakeAudioContext extends EventTarget {
    constructor() {
      super();
      this._state = 'running';
      this.resumeCalls = 0;
      this.suspendCalls = 0;
      this.destination = { kind: 'destination' };
      window.__ctxs.push(this);
    }
    // 마스터 gain 용: 호출을 [종류, 값, 시각] 로 남긴다.
    createGain() {
      const calls = [];
      const param = {
        cancelScheduledValues() { calls.push(['cancel']); },
        setValueAtTime(v, t) { calls.push(['set', v, t]); },
        linearRampToValueAtTime(v, t) { calls.push(['ramp', v, t]); },
      };
      return { gain: param, __calls: calls, connect() {} };
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

function harnessHtml(flags, pre) {
  return [
    '<!doctype html><html><head><meta charset="utf-8"><title>ait-audio-harness</title>',
    `<script>window.__AIT_PERF = ${JSON.stringify(flags)};${pre || ''}</script>`,
    `<script>${PRE_SCRIPT}</script>`,
    '</head><body>',
    `<script>${AUDIO_JS}</script>`,
    '</body></html>',
  ].join('\n');
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {{query?: string, flags?: object, pre?: string}} [opts]
 */
async function openSynthetic(page, opts = {}) {
  await page.clock.install({ time: 0 });
  await page.route(`${HARNESS_ORIGIN}/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: harnessHtml(opts.flags || {}, opts.pre) }));
  await page.goto(`${HARNESS_ORIGIN}/index.html${opts.query || ''}`);
}

const resumeCalls = (page, i = 0) => page.evaluate((idx) => /** @type {any} */ (window).__ctxs[idx].resumeCalls, i);
const setCtxState = (page, i, s) => page.evaluate(([idx, st]) => /** @type {any} */ (window).__ctxs[idx].__set(st), [i, s]);
const audioState = (page) => page.evaluate(() => /** @type {any} */ (window).AITAudio.getState());
const mediaCalls = (page) => page.evaluate(() => ({ .../** @type {any} */ (window).__media }));

/** 재생 중인 <audio> 를 만들어 래핑된 play() 로 등록한다. window.__paused 로 paused 상태를 정한다. */
async function addPlayingAudio(page) {
  await page.evaluate(() => {
    const w = /** @type {any} */ (window);
    w.__paused = false;
    const el = document.createElement('audio');
    document.body.appendChild(el);
    el.play(); // 래핑된 play → trackMedia
    Object.defineProperty(el, 'paused', { get: () => w.__paused, configurable: true });
    Object.defineProperty(el, 'ended', { get: () => false, configurable: true });
    w.__el = el;
    w.__media.play = 0; // 등록용 play() 호출은 센 값에서 뺀다
  });
}

const masterCalls = (page, i = 0) => page.evaluate((idx) => /** @type {any} */ (window).__ctxs[idx].destination.__calls, i);

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

    const log = (await audioState(page)).audioLog;
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
    expect((await audioState(page)).hidden).toBe(true);

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
    expect((await audioState(page)).audioLog.some((e) => e.ev === 'clockOk')).toBe(true);
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

test.describe('hidden / visible 전이', () => {
  test('hidden 에서 running context 를 suspend 하고 미디어를 pause, visible 에서 우리가 멈춘 것만 재개한다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    await addPlayingAudio(page);
    expect((await audioState(page)).trackedMedia).toBe(1);

    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    let s = await audioState(page);
    expect(s.hidden).toBe(true);
    expect(s.hiddenCount).toBe(1);
    expect(s.suspendedContexts).toBe(1);
    expect(s.heldMedia).toBe(1);
    expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls)).toBe(1);
    expect(await mediaCalls(page)).toEqual({ play: 0, pause: 1 });

    await page.evaluate(() => { /** @type {any} */ (window).__paused = true; });
    await setCtxState(page, 0, 'suspended');
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    s = await audioState(page);
    expect(s.hidden).toBe(false);
    expect(s.heldMedia).toBe(0);
    expect(await resumeCalls(page)).toBe(1);
    expect(await mediaCalls(page)).toEqual({ play: 1, pause: 1 });
    expect(s.audioLog.map((e) => e.ev).filter((ev) => ev === 'hide' || ev === 'show')).toEqual(['hide', 'show']);
  });

  test('게임이 hidden 중에 직접 pause 한 미디어는 복귀 때 재개하지 않는다', async ({ page }) => {
    await openSynthetic(page);
    await addPlayingAudio(page);
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    await page.evaluate(() => { const w = /** @type {any} */ (window); w.__paused = true; w.__el.pause(); }); // 래핑된 pause → 게임 소유
    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    expect(await mediaCalls(page)).toMatchObject({ play: 0 });
  });

  test('pagehide / pageshow 도 같은 전이를 일으킨다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    await page.evaluate(() => window.dispatchEvent(new Event('pagehide')));
    expect((await audioState(page)).hidden).toBe(true);
    expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls)).toBe(1);
    await page.evaluate(() => window.dispatchEvent(new Event('pageshow')));
    expect((await audioState(page)).hidden).toBe(false);
    expect(await resumeCalls(page)).toBe(1);
  });
});

test.describe('마스터 gain 페이드', () => {
  test('destination 이 마스터 gain 으로 바뀌고, hidden 에서 0 으로 내려 복귀 때 40ms 로 올린다', async ({ page }) => {
    await openSynthetic(page);
    // currentTime 을 준 context 는 복귀 뒤 시계 감시 대상이 되지만, 이 테스트는 page.clock 을 돌리지 않아 틱이 오지 않는다.
    await page.evaluate(() => { new AudioContext(); /** @type {any} */ (window).__ctxs[0].currentTime = 0; });
    expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].destination.__calls !== undefined)).toBe(true);

    await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
    let calls = await masterCalls(page);
    expect(calls[calls.length - 1]).toEqual(['set', 0, 0]);

    await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
    calls = await masterCalls(page);
    expect(calls.slice(-3)).toEqual([['cancel'], ['set', 0, 0], ['ramp', 1, 0.04]]);
  });

  test('?aitaudiofade=0 이면 마스터 gain 을 만들지 않는다', async ({ page }) => {
    await openSynthetic(page, { query: '?aitaudiofade=0' });
    await page.evaluate(() => { new AudioContext(); });
    expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].destination.kind)).toBe('destination');
  });

  test('?aitaudiofade=0 이면 짧은 효과음 start 도 그대로 둔다', async ({ page }) => {
    await page.addInitScript(() => {
      /** @type {any} */ (window).__starts = [];
      AudioBufferSourceNode.prototype.start = function (when, offset) { /** @type {any} */ (window).__starts.push([when, offset]); };
    });
    await openSynthetic(page, { query: '?aitaudiofade=0' });
    const got = await page.evaluate(() => {
      const c = new OfflineAudioContext(1, 44100, 44100);
      const s = c.createBufferSource();
      s.buffer = c.createBuffer(1, 8820, 44100);
      s.start(0, 0.0027);
      return /** @type {any} */ (window).__starts;
    });
    expect(got).toEqual([[0, 0.0027]]);
  });
});

test.describe('끄기', () => {
  const variants = [
    ['window.__AIT_AUDIO_RESUME = false', { pre: 'window.__AIT_AUDIO_RESUME = false;' }],
    ['__AIT_PERF.mobileLifecycle = false', { flags: { mobileLifecycle: false } }],
    ['?aitaudio=0', { query: '?aitaudio=0' }],
  ];
  for (const [label, opts] of variants) {
    test(`${label} 이면 아무것도 설치하지 않는다`, async ({ page }) => {
      await openSynthetic(page, /** @type {any} */ (opts));
      await page.evaluate(() => { new AudioContext(); });
      expect((await audioState(page)).enabled).toBe(false);
      expect(await page.evaluate(() => /** @type {any} */ (window).AudioContext.__aitAudio === true)).toBe(false);
      expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].destination.kind)).toBe('destination');

      await page.evaluate(() => /** @type {any} */ (window).__setHidden(true));
      await setCtxState(page, 0, 'interrupted');
      await page.evaluate(() => /** @type {any} */ (window).__setHidden(false));
      await page.clock.runFor(5000);
      expect(await page.evaluate(() => /** @type {any} */ (window).__ctxs[0].suspendCalls)).toBe(0);
      expect(await resumeCalls(page)).toBe(0);
    });
  }

  test('기본(플래그 없음)은 켜짐이고 AudioContext 생성자가 감싸진다', async ({ page }) => {
    await openSynthetic(page);
    expect((await audioState(page)).enabled).toBe(true);
    expect(await page.evaluate(() => /** @type {any} */ (window).AudioContext.__aitAudio === true)).toBe(true);
    expect(await page.evaluate(() => new AudioContext() instanceof /** @type {any} */ (window).__ctxs[0].constructor)).toBe(true);
  });
});

test.describe('진단', () => {
  test('audioLog 는 최근 120건만 유지하고 복사본을 돌려준다', async ({ page }) => {
    await openSynthetic(page);
    await page.evaluate(() => { new AudioContext(); });
    for (let i = 0; i < 65; i++) {
      await setCtxState(page, 0, 'suspended');
      await setCtxState(page, 0, 'running');
    }
    const s = await audioState(page);
    expect(s.audioLog).toHaveLength(120);
    s.audioLog.length = 0;
    expect((await audioState(page)).audioLog).toHaveLength(120);
  });
});
