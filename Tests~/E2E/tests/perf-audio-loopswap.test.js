// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — 압축 재생 루프 BGM 이음새 제거(media-loop-swap) 테스트
 *
 * 대상: Editor/FrameworkPatch~/ait-audio-runtime.js 의 WEBAudio.aitLoopHook (+ AITFrameworkPatcher.cs 의 LoopSwapFrom/To 앵커).
 *  - loop=true media element 는 끝 직전에 예비 element 로 교대해 네이티브 loop 의 재시작 무음을 없앤다.
 *  - Unity 의미 보존: pause/resume, stop(해제), time get/set, loop 를 중간에 끔, 옵트아웃(?aitloopswap=0, __AIT_PERF.loopSwap=false).
 *  - ait-pacing.js 의 hidden/visible 미디어 pause/resume 이 예비·옛 element 를 되살리지 않는다.
 *
 * Unity 빌드가 필요 없다. Unity 의 createSourceNode 텍스트를 복사하지 않고(공개 저장소), 같은 인터페이스를 가진 최소 소스 객체를
 * 테스트가 직접 만들고 C# 파일에서 읽은 패치 앵커·치환 문자열을 그대로 끼워 넣는다(앵커가 바뀌면 이 테스트가 먼저 깨진다).
 * 실제 Chrome 의 Audio/AudioContext 와 짧은(2초) WAV 로 실시간 타이머를 쓴다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const ROOT = path.resolve(__dirname, '../../..');
const RUNTIME_JS = fs.readFileSync(path.join(ROOT, 'Editor/FrameworkPatch~/ait-audio-runtime.js'), 'utf8');
const PACING_JS = fs.readFileSync(path.join(ROOT, 'WebGLTemplates/AITTemplate/Runtime/ait-pacing.js'), 'utf8');
const PATCHER_CS = fs.readFileSync(path.join(ROOT, 'Editor/AITFrameworkPatcher.cs'), 'utf8');

/** C# verbatim 문자열 상수(@"...", "" = ")를 읽는다. */
function csConst(name) {
  const m = new RegExp(`const string ${name}\\s*=\\s*@"((?:[^"]|"")*)";`).exec(PATCHER_CS);
  if (!m) throw new Error(`const ${name} not found in AITFrameworkPatcher.cs`);
  return m[1].replace(/""/g, '"');
}
const LOOP_FROM = csConst('LoopSwapFrom');
const LOOP_TO = csConst('LoopSwapTo');

const HARNESS_ORIGIN = 'https://ait-harness.test';

test.use({ launchOptions: { args: ['--autoplay-policy=no-user-gesture-required'] } });

/**
 * 하네스: 실제 AudioContext + 2초 WAV + Unity 형 소스 객체(앵커 = 패치 대상 문장).
 * 소스 객체는 Unity 와 같은 속성(mediaElement 를 매번 읽는 loop/currentTime/playbackRate/onended, _pauseMediaElement, start, stop)을 가진다.
 */
function harnessHtml(flags, withPacing) {
  const sourceFactory = `
    window.__makeSource = function (WEBAudio, clip) {
      var self = clip;
      var mediaElement = new Audio();
      mediaElement.preload = 'metadata';
      mediaElement.src = clip.url;
      var source = WEBAudio.audioContext.createMediaElementSource(mediaElement);
      ${LOOP_TO}
      source.playbackRate = {};
      Object.defineProperty(source.playbackRate, 'value', {
        get: function () { return source.mediaElement.playbackRate; },
        set: function (v) { source.mediaElement.playbackRate = v; } });
      Object.defineProperty(source, 'currentTime', {
        get: function () { return source.mediaElement.currentTime; },
        set: function (v) { source.mediaElement.currentTime = v; } });
      Object.defineProperty(source, 'onended', {
        get: function () { return source.mediaElement.onended; },
        set: function (f) { source.mediaElement.onended = f; } });
      source.isStopped = false;
      source._pauseMediaElement = function () { source.mediaElement.pause(); };
      source.start = function (startTime, offset) {
        source.mediaElement.currentTime = offset || 0;
        return source.mediaElement.play();
      };
      source.stop = function () { source._pauseMediaElement(); source.isStopped = true; };
      source.estimatePlaybackPosition = function () { return source.mediaElement.currentTime; };
      return source;
    };
  `;
  const setup = `
    window.__wav = function (sec) {
      var sr = 22050, n = Math.floor(sec * sr), buf = new ArrayBuffer(44 + n * 2), v = new DataView(buf);
      function s(o, t) { for (var i = 0; i < t.length; i++) v.setUint8(o + i, t.charCodeAt(i)); }
      s(0, 'RIFF'); v.setUint32(4, 36 + n * 2, true); s(8, 'WAVE'); s(12, 'fmt '); v.setUint32(16, 16, true);
      v.setUint16(20, 1, true); v.setUint16(22, 1, true); v.setUint32(24, sr, true); v.setUint32(28, sr * 2, true);
      v.setUint16(32, 2, true); v.setUint16(34, 16, true); s(36, 'data'); v.setUint32(40, n * 2, true);
      for (var i = 0; i < n; i++) v.setInt16(44 + i * 2, Math.round(Math.sin(2 * Math.PI * 440 * i / sr) * 0.4 * 32767), true);
      return URL.createObjectURL(new Blob([buf], { type: 'audio/wav' }));
    };
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
    window.__boot = function (loopSec) {
      var WEBAudio = { audioContext: new AudioContext(), audioCache: [], audioInstances: {} };
      window.WEBAudio = WEBAudio;
      new Function('WEBAudio', ${JSON.stringify(RUNTIME_JS)})(WEBAudio);
      var clip = { url: window.__wav(loopSec) };
      var source = window.__makeSource(WEBAudio, clip);
      var an = WEBAudio.audioContext.createAnalyser();
      an.fftSize = 1024;
      var g = WEBAudio.audioContext.createGain();
      source.connect(g); g.connect(an); an.connect(WEBAudio.audioContext.destination);
      var ended = 0;
      source.onended = function () { ended++; };
      var buf = new Float32Array(1024);
      var t = {
        WEBAudio: WEBAudio, clip: clip, source: source, orig: source.mediaElement, gain: g,
        endedCount: function () { return ended; },
        level: function () { an.getFloatTimeDomainData(buf); var m = 0; for (var i = 0; i < buf.length; i++) m = Math.max(m, Math.abs(buf[i])); return m; },
        els: function () {
          var l = [t.orig];
          (clip.aitPool || []).forEach(function (e) { l.push(e.el); });
          return l;
        },
        playingCount: function () { return t.els().filter(function (e) { return !e.paused && !e.ended; }).length; },
        stats: function () { return Object.assign({}, WEBAudio.aitLoopStats); },
        state: function () { var s = source.aitLoopState ? source.aitLoopState() : null; return s && { on: s.on, ready: s.ready, swapped: s.swapped, old: s.old, failed: s.failed, anch: s.anch }; },
        waitFor: function (fn, ms) {
          return new Promise(function (res, rej) {
            var t0 = performance.now();
            (function poll() {
              var v; try { v = fn(); } catch (e) { return rej(e); }
              if (v) return res(v);
              if (performance.now() - t0 > ms) return rej(new Error('timeout: ' + fn));
              setTimeout(poll, 20);
            })();
          });
        },
      };
      window.__t = t;
      return true;
    };
  `;
  return [
    '<!doctype html><html><head><meta charset="utf-8"><title>ait-loopswap-harness</title>',
    `<script>window.__AIT_PERF = ${JSON.stringify(flags)};</script>`,
    `<script>${sourceFactory}${setup}</script>`,
    '</head><body>',
    withPacing ? `<script>${PACING_JS}</script>` : '',
    '</body></html>',
  ].join('\n');
}

/** @param {import('@playwright/test').Page} page */
async function open(page, { query = '', flags = {}, pacing = false, loopSec = 2 } = {}) {
  await page.route(`${HARNESS_ORIGIN}/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: harnessHtml(flags, pacing) }));
  await page.goto(`${HARNESS_ORIGIN}/index.html${query}`);
  await page.evaluate((sec) => /** @type {any} */ (window).__boot(sec), loopSec);
}

/** 재생을 시작하고(Unity 의 playSoundClip 순서: loop 지정 → start) 첫 교대 준비(예비 element 로드)까지 기다린다. */
async function startLooping(page, offset = 0) {
  await page.evaluate(async (off) => {
    const t = /** @type {any} */ (window).__t;
    t.source.loop = true;
    await t.source.start(undefined, off);
    await t.waitFor(() => t.state() && t.state().ready, 5000);
  }, offset);
}

test.describe('루프 교대(media-loop-swap)', () => {
  test('끝 직전에 예비 element 로 교대하고 source 는 하나로 보이며 소리가 이어진다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      const before = t.source.mediaElement;
      await t.waitFor(() => t.stats().swaps >= 1, 8000);
      await t.waitFor(() => t.state() && t.state().swapped && !t.state().old, 4000);
      const after = t.source.mediaElement;
      const pos1 = t.source.currentTime;
      await new Promise((res) => setTimeout(res, 300));
      const pos2 = t.source.currentTime;
      return {
        changed: before !== after, orig: after === t.orig, level: t.level(), pos1, pos2, stats: t.stats(),
        playing: t.playingCount(), origPaused: t.orig.paused, ended: t.endedCount(), loop: after.loop,
      };
    });
    expect(r.changed).toBe(true);
    expect(r.orig).toBe(false);
    expect(r.stats.swaps).toBeGreaterThanOrEqual(1);
    expect(r.stats.fails).toBe(0);
    expect(r.playing).toBe(1);       // 옛 element 는 멈추고 새 element 하나만 재생 중
    expect(r.origPaused).toBe(true);
    expect(r.ended).toBe(0);         // Unity onended 는 루프 중간에 불리지 않는다
    expect(r.level).toBeGreaterThan(0.05);
    expect(r.pos2).toBeGreaterThan(r.pos1);   // time 이 교대 뒤에도 앞으로 간다
    expect(r.pos2).toBeLessThan(2.1);
  });

  test('여러 바퀴 돌아도 한 번에 재생 중인 element 는 하나이고 교대가 반복된다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      let maxPlaying = 0;
      const iv = setInterval(() => { maxPlaying = Math.max(maxPlaying, t.playingCount()); }, 10);
      await t.waitFor(() => t.stats().swaps >= 3, 14000);
      clearInterval(iv);
      return { swaps: t.stats().swaps, fails: t.stats().fails, maxPlaying, pool: (t.clip.aitPool || []).length, ended: t.endedCount() };
    });
    expect(r.swaps).toBeGreaterThanOrEqual(3);
    expect(r.fails).toBe(0);
    expect(r.maxPlaying).toBeLessThanOrEqual(2);   // 교대 순간의 겹침만 허용
    expect(r.pool).toBe(1);                        // 소리 하나당 추가 element 는 최대 1개
    expect(r.ended).toBe(0);
  });

  test('pause/resume: 교대 뒤 pause 하면 모든 element 가 멈추고 resume 하면 현재 element 만 이어 재생한다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      await t.waitFor(() => t.stats().swaps >= 1 && t.state().swapped && !t.state().old, 8000);
      t.source._pauseMediaElement();
      await new Promise((res) => setTimeout(res, 400));
      const pausedPlaying = t.playingCount();
      const posPaused = t.source.currentTime;
      await new Promise((res) => setTimeout(res, 400));
      const stillPaused = t.source.currentTime === posPaused;
      // Unity channel.resume: source.start(undefined, source.currentTime)
      await t.source.start(undefined, t.source.currentTime);
      await new Promise((res) => setTimeout(res, 500));
      const resumed = { playing: t.playingCount(), pos: t.source.currentTime, paused: t.source.mediaElement.paused };
      return { pausedPlaying, stillPaused, resumed, swaps: t.stats().swaps };
    });
    expect(r.pausedPlaying).toBe(0);
    expect(r.stillPaused).toBe(true);
    expect(r.resumed.paused).toBe(false);
    expect(r.resumed.playing).toBe(1);
    expect(r.resumed.pos).toBeGreaterThan(0);
  });

  test('pause 한 채로 한참 두면 예비 element 가 놓이고, resume 하면 다시 준비해 교대한다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      t.source._pauseMediaElement();
      await t.waitFor(() => t.els().every((e) => !e.getAttribute('src') || e === t.source.mediaElement), 9000);
      const released = t.clip.aitPool[0].el.getAttribute('src') === null;
      const swapsBefore = t.stats().swaps;
      await t.source.start(undefined, 0.2);
      await t.waitFor(() => t.stats().swaps > swapsBefore, 9000);
      return { released, swaps: t.stats().swaps };
    });
    expect(r.released).toBe(true);
    expect(r.swaps).toBeGreaterThanOrEqual(1);
  });

  test('stop + 소스 해제(disconnect): 예비 element 의 src 가 비워지고 재사용 슬롯이 풀린다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      await t.waitFor(() => t.stats().swaps >= 1, 8000);
      t.source.stop();
      t.source.disconnect();   // Unity channel.disconnectSource
      await new Promise((res) => setTimeout(res, 1500));
      return {
        playing: t.playingCount(),
        busy: t.clip.aitPool.some((e) => e.busy),
        srcs: t.clip.aitPool.map((e) => e.el.getAttribute('src')),
        ended: t.endedCount(),
      };
    });
    expect(r.playing).toBe(0);
    expect(r.busy).toBe(false);
    expect(r.srcs).toEqual([null]);
  });

  test('time 설정(seek): 설정한 위치부터 재생하고 새 끝 시점에 맞춰 교대한다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      t.source.currentTime = 1.0;
      await new Promise((res) => setTimeout(res, 150));
      const p = t.source.currentTime;
      const t0 = performance.now();
      await t.waitFor(() => t.stats().swaps >= 1, 5000);
      return { p, ms: performance.now() - t0, swaps: t.stats().swaps };
    });
    expect(r.p).toBeGreaterThanOrEqual(1.0);
    expect(r.p).toBeLessThan(1.6);
    expect(r.ms).toBeLessThan(2500);   // 남은 ~1초 + 여유. 2초 처음부터 기다린 값이 아니다
    expect(r.swaps).toBeGreaterThanOrEqual(1);
  });

  test('loop 를 중간에 끄면 예비 element 를 놓고 교대 없이 끝에서 onended 가 한 번 불린다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      t.source.loop = false;
      const st = t.state();
      const spareSrc = t.clip.aitPool && t.clip.aitPool[0] ? t.clip.aitPool[0].el.getAttribute('src') : null;
      await t.waitFor(() => t.endedCount() >= 1, 6000);
      await new Promise((res) => setTimeout(res, 600));
      return { on: st.on, spareSrc, ended: t.endedCount(), swaps: t.stats().swaps, playing: t.playingCount() };
    });
    expect(r.on).toBe(false);
    expect(r.spareSrc).toBeNull();
    expect(r.ended).toBe(1);
    expect(r.swaps).toBe(0);
    expect(r.playing).toBe(0);
  });

  test('교대 대기 중(네이티브 loop 를 꺼 둔 뒤)에 loop 를 끄면 끝에서 소리가 멈추고 onended 가 불린다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      // 교대 직전(남은 ~0.5초)까지 기다린 뒤 끈다: 이 시점에 stock 의 loop 는 이미 꺼져 있다.
      await t.waitFor(() => t.source.mediaElement.currentTime > 1.4, 5000);
      t.source.loop = false;
      await t.waitFor(() => t.endedCount() >= 1, 4000);
      await new Promise((res) => setTimeout(res, 600));
      return { ended: t.endedCount(), swaps: t.stats().swaps, playing: t.playingCount() };
    });
    expect(r.ended).toBe(1);
    expect(r.swaps).toBe(0);
    expect(r.playing).toBe(0);
  });

  test('다시 loop 를 켜면 다시 교대한다', async ({ page }) => {
    await open(page);
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      t.source.loop = false;
      t.source.loop = true;
      await t.waitFor(() => t.stats().swaps >= 1, 9000);
      return { on: t.state().on, swaps: t.stats().swaps, ended: t.endedCount() };
    });
    expect(r.on).toBe(true);
    expect(r.swaps).toBeGreaterThanOrEqual(1);
    expect(r.ended).toBe(0);
  });
});

test.describe('옵트아웃', () => {
  for (const [name, opts] of /** @type {[string, any][]} */ ([
    ['?aitloopswap=0', { query: '?aitloopswap=0' }],
    ['__AIT_PERF.loopSwap=false', { flags: { loopSwap: false } }],
  ])) {
    test(`${name}: hook 이 아무것도 만들지 않고 네이티브 loop 그대로 동작한다`, async ({ page }) => {
      await open(page, { ...opts, loopSec: 1 });
      const r = await page.evaluate(async () => {
        const t = /** @type {any} */ (window).__t;
        t.source.loop = true;
        await t.source.start(undefined, 0);
        await new Promise((res) => setTimeout(res, 2500));
        return {
          hasHook: typeof t.source.aitLoop, pool: t.clip.aitPool, native: t.source.mediaElement.loop, same: t.source.mediaElement === t.orig,
          swaps: t.stats().swaps, ended: t.endedCount(), playing: t.playingCount(), pos: t.source.currentTime,
        };
      });
      expect(r.hasHook).toBe('undefined');
      expect(r.pool).toBeUndefined();
      expect(r.native).toBe(true);
      expect(r.same).toBe(true);
      expect(r.swaps).toBe(0);
      expect(r.ended).toBe(0);
      expect(r.playing).toBe(1);
    });
  }

  test('loop=false 로만 쓰는 소스(비루프)는 hook 이 개입하지 않는다', async ({ page }) => {
    await open(page, { loopSec: 1 });
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      t.source.loop = false;
      await t.source.start(undefined, 0);
      await t.waitFor(() => t.endedCount() >= 1, 4000);
      return { pool: t.clip.aitPool, swaps: t.stats().swaps, ended: t.endedCount(), state: t.state() };
    });
    expect(r.pool).toBeUndefined();
    expect(r.swaps).toBe(0);
    expect(r.ended).toBe(1);
    expect(r.state).toMatchObject({ on: false });
  });
});

test.describe('ait-pacing.js 와의 상호작용', () => {
  test('hidden → visible 에서 현재 element 만 되살아나고 예비·옛 element 는 되살아나지 않는다', async ({ page }) => {
    await open(page, { pacing: true });
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      const w = /** @type {any} */ (window);
      await t.waitFor(() => t.stats().swaps >= 1 && t.state().swapped && !t.state().old, 8000);
      w.__setHidden(true);
      await new Promise((res) => setTimeout(res, 300));
      const hiddenPlaying = t.playingCount();
      w.__setHidden(false);
      await new Promise((res) => setTimeout(res, 600));
      const cur = t.source.mediaElement;
      const out = {
        hiddenPlaying, visiblePlaying: t.playingCount(), curPlaying: !cur.paused,
        othersPlaying: t.els().filter((e) => e !== cur && !e.paused).length,
      };
      // 복귀 뒤에도 교대가 이어진다.
      const s0 = t.stats().swaps;
      await t.waitFor(() => t.stats().swaps > s0, 9000);
      out.swapsAfter = t.stats().swaps - s0;
      out.playingAfter = t.playingCount();
      return out;
    });
    expect(r.hiddenPlaying).toBe(0);
    expect(r.visiblePlaying).toBe(1);
    expect(r.curPlaying).toBe(true);
    expect(r.othersPlaying).toBe(0);
    expect(r.swapsAfter).toBeGreaterThanOrEqual(1);
    expect(r.playingAfter).toBeLessThanOrEqual(2);
  });

  test('교대가 진행되는 도중(옛 element 가 아직 남은 순간)에 hidden → visible 이 와도 옛 element 가 되살아나지 않는다', async ({ page }) => {
    await open(page, { pacing: true });
    await startLooping(page);
    const r = await page.evaluate(async () => {
      const t = /** @type {any} */ (window).__t;
      const w = /** @type {any} */ (window);
      await t.waitFor(() => t.state().old, 8000);   // 교대 직후: 옛 element 가 꼬리를 재생 중
      w.__setHidden(true);
      await new Promise((res) => setTimeout(res, 300));
      w.__setHidden(false);
      await new Promise((res) => setTimeout(res, 800));
      const cur = t.source.mediaElement;
      return { cur: !cur.paused, others: t.els().filter((e) => e !== cur && !e.paused).length, ended: t.endedCount() };
    });
    expect(r.cur).toBe(true);
    expect(r.others).toBe(0);
    expect(r.ended).toBe(0);
  });
});

test.describe('패치 앵커 계약', () => {
  test('치환 문장은 stock 의 loop 위임을 그대로 포함하고 hook 호출 둘만 더한다', () => {
    expect(LOOP_TO.startsWith('if(WEBAudio.aitLoopHook)WEBAudio.aitLoopHook(source,self);')).toBe(true);
    expect(LOOP_TO.replace('if(WEBAudio.aitLoopHook)WEBAudio.aitLoopHook(source,self);', '')
      .replace(';if(source.aitLoop)source.aitLoop(v)', '')).toBe(LOOP_FROM);
  });

  test('payload 에 hook 이 없으면(prologue 건너뜀) 치환 문장은 stock 처럼 동작한다', async ({ page }) => {
    await page.route(`${HARNESS_ORIGIN}/**`, (route) =>
      route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: harnessHtml({}, false) }));
    await page.goto(`${HARNESS_ORIGIN}/index.html`);
    const r = await page.evaluate(() => {
      const w = /** @type {any} */ (window);
      const WEBAudio = { audioContext: new AudioContext(), audioCache: [] };   // aitLoopHook 없음
      const source = w.__makeSource(WEBAudio, { url: w.__wav(0.5) });
      source.loop = true;
      return { loop: source.mediaElement.loop, hook: typeof source.aitLoop };
    });
    expect(r.hook).toBe('undefined');
  });
});
