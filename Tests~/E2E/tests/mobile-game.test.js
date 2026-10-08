// @ts-check
import { test, expect, chromium, devices } from '@playwright/test';
import { execSync, execFileSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK: 모바일 웹 게임(mobilegame posture) 플레이 벤치마크
 *
 * 대상은 HeavyBuildRunner 의 `mobilegame` posture 로 빌드한 세로 화면 탭 점프 러너(SharedScripts/Runtime/MobileGame/AITRun*.cs)다.
 * 게임은 상태를 window.__AIT_RUN 으로 보고하고(AITRunBridge.jslib), unityInstance.SendMessage('AITRunScene', 'Cmd', ...) 로 제어된다.
 *
 * 한 세션(새 브라우저, Pixel 7 터치 에뮬레이션, CPU 감속)마다:
 *   1. 로드 → 타이틀 보고까지 시간(내비게이션 시작 기준)
 *   2. 실제 터치 탭으로 START → 탭 점프(점프 수 증가·효과음) → 강제 종료 → 최고 점수 저장 확인
 *   3. 자동 플레이로 RETRY(실제 탭) → 고정 스텝 1500(30초)까지 진행: 결정론 체크섬 + 플레이 중 프레임 시간 + 메모리(렌더러 RSS·JS 힙·wasm 힙)
 *   4. BGM 진행(첫 세션만), 리로드 후 최고 점수 유지, 오류 예산
 * 페어 모드(PERF_PAIR_PROJECT_PATH)는 A/B 세션을 번갈아 돌리고(순서 효과 상쇄), 지표별 중앙값과 B−A 를 남긴다.
 * 같은 Unity 버전이면 A/B 체크섬이 같아야 한다(SDK 가 게임 동작을 바꾸지 않는다는 증거).
 *
 * 환경변수:
 *  - UNITY_PROJECT_PATH / PERF_PAIR_PROJECT_PATH : A/B 산출물 프로젝트 경로(ait-build/dist/web 포함)
 *  - PERF_UNITY_VERSION, PERF_LABEL_A, PERF_LABEL_B
 *  - RUN_REQUIRE=1        : 실행을 켜고, 산출물이 없으면 skip 대신 실패(CI 용). RUN_MOBILEGAME=1 은 켜기만 한다.
 *  - RUN_ROUNDS           : 프로젝트당 세션 수(기본 3)
 *  - PERF_CPU_THROTTLE    : CDP CPU 감속 배율(기본 4)
 *  - RUN_CHROME_CHANNEL   : Playwright channel(기본 chrome, 빈 값이면 번들 chromium)
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PAIR_PROJECT = process.env.PERF_PAIR_PROJECT_PATH || '';
const PAIR_MODE = !!PAIR_PROJECT;
const LABEL_A = process.env.PERF_LABEL_A || 'A';
const LABEL_B = process.env.PERF_LABEL_B || 'B';
const REQUIRE = process.env.RUN_REQUIRE === '1';
const ROUNDS = Math.max(1, parseInt(process.env.RUN_ROUNDS || '3', 10));
const RUN_DPR = parseFloat(process.env.RUN_DPR || '0') || 0;
const CPU_THROTTLE = Math.max(1, parseFloat(process.env.PERF_CPU_THROTTLE || '4'));
const CHANNEL = process.env.RUN_CHROME_CHANNEL === undefined ? 'chrome' : process.env.RUN_CHROME_CHANNEL;
// 기본 설정(pnpm test)은 모든 *.test.js 를 돌린다. mobilegame 빌드를 지목한 실행에서만 돈다.
const ENABLED = REQUIRE || process.env.RUN_MOBILEGAME === '1' || process.env.AIT_PERF_POSTURE === 'mobilegame';

function findProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) return envPath;
  for (const version of ['6000.3', '6000.0', '2021.3']) {
    const p = path.resolve(__dirname, `../HeavySampleUnityProject-${version}`);
    if (fs.existsSync(path.resolve(p, 'ait-build/dist/web'))) return p;
  }
  return path.resolve(__dirname, '../HeavySampleUnityProject-6000.3');
}
const PROJECT = findProject();
const UNITY_VERSION = process.env.PERF_UNITY_VERSION
  || ((PROJECT.match(/SampleUnityProject-(\d+\.\d+)/) || [])[1]) || 'unknown';

function portOffset(version) {
  const m = version.match(/(\d+)\.(\d+)/);
  if (!m) return 0;
  const major = parseInt(m[1], 10);
  const minor = parseInt(m[2], 10);
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  if (major === 6000 && minor === 3) return 4;
  return 0;
}
// full-pipeline(4173+) / perf(4223+, 4273+) / ce-serving(4323+) / game(4373+, 4423+) 와 겹치지 않는 대역
const PORT_A = 4473 + portOffset(UNITY_VERSION);
const PORT_B = 4523 + portOffset(UNITY_VERSION);
const RESULT_PATH = path.resolve(__dirname, `mobile-game-results-${UNITY_VERSION}.json`);

// Toss 앱 밖에서만 나는 무해 메시지. 여기에 없는 console error / pageerror / 동일 출처 요청 실패는 실패로 센다.
const BENIGN_PATTERNS = [
  /favicon/i,
  /^Failed to load resource/i,
  /net::ERR_ABORTED/i,
  /apps-?in-?toss.*(webview|native|environment|app)/i,
  /(not|outside).{0,40}(toss|apps-?in-?toss)\s*(app|webview|environment)/i,
  /(toss|apps-?in-?toss)\s*(app|webview|environment).{0,40}(not|unavailable|missing)/i,
  /native\s*(app|bridge|module)\s*(info|not|unavailable)/i,
  /getAppsInTossGlobals|__GRANITE_NATIVE_EMITTER|ReactNativeWebView/i,
  /analytics.{0,60}(read-?only|not allowed|unavailable|disabled|not supported)/i,
  /(read-?only).{0,60}analytics/i,
  /sentry.{0,60}(dsn|disabled|not initialized)/i,
];
const isBenign = (text) => BENIGN_PATTERNS.some((re) => re.test(text));

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const median = (xs) => {
  const v = xs.filter((x) => typeof x === 'number' && Number.isFinite(x)).sort((a, b) => a - b);
  if (!v.length) return null;
  const mid = Math.floor(v.length / 2);
  return v.length % 2 ? v[mid] : (v[mid - 1] + v[mid]) / 2;
};

// ---- 서버 ----
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
  } catch {}
}
async function startServer(projectPath, port) {
  freePort(port);
  for (let i = 0; i < 25 && !(await isPortAvailable(port)); i++) await sleep(200);
  const proc = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--host', '127.0.0.1', '--port', String(port), '--strictPort'], {
    cwd: path.resolve(projectPath, 'ait-build'), stdio: 'pipe', shell: true, env: { ...process.env, NODE_OPTIONS: '' },
  });
  proc.stderr.on('data', (d) => console.error('[vite preview error]', d.toString().trim()));
  for (let i = 0; i < 60; i++) {
    try {
      const res = await fetch(`http://127.0.0.1:${port}/`, { method: 'HEAD' });
      if (res.ok) return proc;
    } catch {}
    await sleep(500);
  }
  throw new Error(`preview 서버가 ${port} 에서 뜨지 않았다 (${projectPath})`);
}
async function stopServer(proc, port) {
  if (!proc) return;
  try { proc.kill('SIGTERM'); } catch {}
  await sleep(300);
  freePort(port);
}

// ---- 메모리(렌더러·GPU RSS: Linux /proc VmHWM, 그 외 ps 폴링 최댓값) ----
function readProcRss(pid) {
  try {
    if (process.platform === 'linux') {
      const st = fs.readFileSync(`/proc/${pid}/status`, 'utf8');
      const kb = (k) => { const m = st.match(new RegExp(`^${k}:\\s+(\\d+)\\s*kB`, 'm')); return m ? Number(m[1]) * 1024 : null; };
      return { rss: kb('VmRSS'), hwm: kb('VmHWM') };
    }
    const kb = Number(execFileSync('ps', ['-o', 'rss=', '-p', String(pid)], { encoding: 'utf8', timeout: 2000 }).trim());
    return Number.isFinite(kb) && kb > 0 ? { rss: kb * 1024, hwm: null } : null;
  } catch { return null; }
}
function startRssSampler(browserCdp) {
  const peaks = { renderer: 0, gpu: 0 };
  let stopped = false;
  const poll = async () => {
    let procs = [];
    try { procs = (await browserCdp.send('SystemInfo.getProcessInfo')).processInfo || []; } catch { return; }
    for (const p of procs) {
      const type = String(p.type).toLowerCase();
      if (type !== 'renderer' && type !== 'gpu') continue;
      const r = readProcRss(p.id);
      if (!r) continue;
      const v = Math.max(r.rss || 0, type === 'renderer' ? (r.hwm || 0) : 0);
      if (v > peaks[type]) peaks[type] = v;
    }
  };
  const loop = (async () => { while (!stopped) { await poll(); await sleep(500); } })();
  return { async stop() { stopped = true; await loop; await poll(); return { ...peaks }; } };
}

// ---- 페이지 헬퍼 ----
const INIT_SCRIPT = `
(function () {
  if (window.__aitRunHooked) return;
  window.__aitRunHooked = true;
  window.__aitMedia = [];
  try {
    var _play = HTMLMediaElement.prototype.play;
    HTMLMediaElement.prototype.play = function () {
      try { if (window.__aitMedia.indexOf(this) < 0) window.__aitMedia.push(this); } catch (e) {}
      return _play.apply(this, arguments);
    };
  } catch (e) {}
  window.__ctxLost = 0;
  window.addEventListener('webglcontextlost', function () { window.__ctxLost++; }, true);
})();
`;
const getState = (page) => page.evaluate(() => window['__AIT_RUN'] || null).catch(() => null);
const getMediaBgmTime = (page) => page.evaluate(() => {
  const els = (window['__aitMedia'] || []).filter((m) => !m.paused && m.duration >= 30);
  return els.length ? els[0].currentTime : null;
}).catch(() => null);
const sendCmd = (page, cmd) => page.evaluate((c) => { window['unityInstance'].SendMessage('AITRunScene', 'Cmd', c); }, cmd);

async function waitForRun(page, predicateSrc, timeoutMs, what) {
  const ok = await page.waitForFunction(`!!window.__AIT_RUN && (${predicateSrc})(window.__AIT_RUN)`, undefined, { timeout: timeoutMs, polling: 100 })
    .then(() => true).catch(() => false);
  const s = await getState(page);
  if (!ok) throw new Error(`대기 실패: ${what} (${timeoutMs}ms). 마지막 상태 ${JSON.stringify(s && { state: s.state, step: s.step, runs: s.runs })}`);
  return s;
}

async function canvasBox(page) {
  const box = await page.locator('#unity-canvas').boundingBox({ timeout: 5000 }).catch(() => null)
    || await page.locator('canvas').first().boundingBox({ timeout: 5000 });
  if (!box) throw new Error('캔버스를 찾지 못했다');
  return box;
}

/** 게임이 보고한 버튼 사각형(화면 픽셀, 좌상단 원점)의 중심을 실제 터치로 탭한다. */
// 실제 손가락처럼 누른 채로 잠깐 머문 뒤 뗀다. page.touchscreen.tap 은 touchstart/touchend 를 연달아 보내
// 프레임이 느린 빌드(~6fps)에서는 둘이 한 Unity 프레임에 들어가 uGUI 클릭이 사라진다.
const touchCdp = new WeakMap();
async function touchTap(page, x, y, holdMs = 120) {
  let cdp = touchCdp.get(page);
  if (!cdp) { cdp = await page.context().newCDPSession(page); touchCdp.set(page, cdp); }
  const pt = [{ x, y, id: 1, radiusX: 4, radiusY: 4, force: 1 }];
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: pt });
  await sleep(holdMs);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

async function tapButton(page, s, name) {
  const b = s[name];
  if (!b || !b.visible) throw new Error(`버튼 '${name}' 이 보이지 않는다: ${JSON.stringify(b)}`);
  const box = await canvasBox(page);
  const sx = box.width / s.screenW;
  const sy = box.height / s.screenH;
  const x = box.x + (b.x + b.w / 2) * sx;
  const y = box.y + (b.y + b.h / 2) * sy;
  await touchTap(page, x, y);
  return { x: Math.round(x), y: Math.round(y) };
}

async function waitOverlayGone(page) {
  await page.waitForFunction(() => {
    const e = document.querySelector('#ait-loading-wrapper');
    return !e || getComputedStyle(e).display === 'none' || getComputedStyle(e).visibility === 'hidden';
  }, undefined, { timeout: 20000 }).catch(() => {});
}

function check(res, name, ok, detail) {
  res.checks.push({ name, pass: !!ok, detail: detail === undefined ? '' : String(detail) });
  console.log(`  ${ok ? '✓' : '✗'} [${res.label}#${res.round}] ${name}${detail !== undefined && detail !== '' ? ` (${detail})` : ''}`);
  return !!ok;
}

/** 세션 1회. full=true 면 BGM 진행과 리로드 영속성까지 본다. */
async function runSession(label, projectPath, port, round, full) {
  const res = { label, round, project: projectPath, checks: [], metrics: {}, checksum: null, console: { unexpected: [], benign: [] }, fatal: null };
  const browser = await chromium.launch({
    channel: CHANNEL || undefined,
    headless: true,
    args: ['--enable-webgl', '--use-angle=default', '--autoplay-policy=no-user-gesture-required'],
  });
  const { defaultBrowserType, ...device } = devices['Pixel 7'];
  void defaultBrowserType;
  // RUN_DPR 로 기기 DPR 을 바꿀 수 있다. 2 이하면 SDK 의 자동 DPR 상한이 걸리지 않아 A/B 가 같은 픽셀 수를 그린다.
  const context = await browser.newContext({ ...device, ...(RUN_DPR > 0 ? { deviceScaleFactor: RUN_DPR } : {}) });
  const page = await context.newPage();
  await page.addInitScript(INIT_SCRIPT);
  const origin = `http://127.0.0.1:${port}`;
  const record = (kind, text) => {
    const line = `${kind}: ${String(text).slice(0, 400)}`;
    (isBenign(String(text)) ? res.console.benign : res.console.unexpected).push(line);
  };
  page.on('console', (m) => { if (m.type() === 'error') record('console.error', m.text()); });
  page.on('pageerror', (e) => record('pageerror', e && e.message));
  page.on('crash', () => record('crash', 'page crashed'));
  page.on('requestfailed', (r) => { if (r.url().startsWith(origin)) record('requestfailed', `${r.url()} ${r.failure() && r.failure().errorText}`); });
  page.on('response', (r) => { if (r.url().startsWith(origin) && r.status() >= 400) record('http', `${r.status()} ${r.url()}`); });

  const m = res.metrics;
  let rss = null;
  let browserCdp = null;
  try {
    const pageCdp = await context.newCDPSession(page);
    await pageCdp.send('Emulation.setCPUThrottlingRate', { rate: CPU_THROTTLE });
    browserCdp = await browser.newBrowserCDPSession().catch(() => null);
    if (browserCdp) rss = startRssSampler(browserCdp);

    // ---- 1. 로드 → 타이틀 ----
    const resp = await page.goto(`${origin}/?e2e=true`, { waitUntil: 'commit', timeout: 120000 });
    check(res, '페이지 200', resp && resp.status() === 200, resp && resp.status());
    let s = await waitForRun(page, '(s) => s.state === "title"', 120000, '타이틀 보고');
    m.titleMs = await page.evaluate(() => window['__AIT_RUN_FIRST_TITLE_MS'] ?? null);
    check(res, '타이틀 화면', s.state === 'title' && s.startButton && s.startButton.visible, `titleMs=${m.titleMs && m.titleMs.toFixed(0)} unity=${s.unity}`);
    const bootBest = s.bootBest;
    await waitOverlayGone(page);
    await sleep(500);

    // ---- 2. 실제 탭으로 시작 → 탭 점프 ----
    s = await getState(page);
    await tapButton(page, s, 'startButton');
    s = await waitForRun(page, '(s) => s.state === "playing" && s.runs >= 1', 10000, 'START 탭 → playing');
    check(res, 'START 버튼 탭으로 시작', s.state === 'playing', `runs=${s.runs}`);
    const box = await canvasBox(page);
    for (let i = 0; i < 4; i++) {
      await sleep(900);
      await touchTap(page, box.x + box.width * 0.5, box.y + box.height * 0.75);
    }
    await sleep(600);
    s = await getState(page);
    // 장애물에 부딪혀 게임오버가 되면 그 뒤 탭은 세지 않는다(플레이 중 탭만 센다). 그 경우 받은 탭 수 요건은 면제하고 점프 1회 이상만 본다.
    check(res, '탭으로 점프', s.jumps >= 1 && (s.taps >= 4 || s.state === 'over'), `taps=${s.taps} jumps=${s.jumps} state=${s.state}`);
    check(res, '효과음 재생', s.sfxCount >= 3, `sfxCount=${s.sfxCount}`);
    if (s.state === 'playing') await sendCmd(page, 'over');
    s = await waitForRun(page, '(s) => s.state === "over"', 10000, '게임오버');
    const manualScore = s.score;
    check(res, '게임오버 + 최고 점수 갱신', s.best >= Math.max(manualScore, bootBest) && s.retryButton && s.retryButton.visible,
      `score=${manualScore} best=${s.best} bootBest=${bootBest}`);
    const bestAfterManual = s.best;

    // ---- 3. 자동 플레이(RETRY 실제 탭) → 체크섬 + 프레임·메모리 ----
    await sendCmd(page, 'autoplay');
    await sleep(200);
    s = await getState(page);
    await tapButton(page, s, 'retryButton');
    s = await waitForRun(page, '(s) => s.state === "playing" && s.runs >= 2', 10000, 'RETRY 탭 → playing');
    check(res, 'RETRY 버튼 탭으로 재시작', s.state === 'playing' && s.autoplay, `runs=${s.runs}`);
    await sendCmd(page, 'stats');
    const bgm0 = s.bgmTime;
    const media0 = await getMediaBgmTime(page);
    const tPlay = Date.now();
    s = await waitForRun(page, '(s) => s.checksumAt > 0', 180000, '체크섬(1500 스텝 또는 사망)');
    m.playWallMs = Date.now() - tPlay;
    res.checksum = s.checksum;
    m.checksumAt = s.checksumAt;
    m.score = s.score;
    m.coins = s.coins;
    m.jumps = s.jumps;
    m.frames = s.frames;
    m.renderPixels = (s.screenW || 0) * (s.screenH || 0); // 렌더 해상도(DPR 상한 반영) — 프레임 시간·메모리 차이 해석용
    m.avgFrameMs = s.avgMs;
    m.p50FrameMs = s.p50Ms;
    m.p95FrameMs = s.p95Ms;
    m.p99FrameMs = s.p99Ms;
    m.maxFrameMs = s.maxMs;
    m.longFrames = s.longFrames;
    m.fps = s.avgMs ? 1000 / s.avgMs : null;
    check(res, '자동 플레이 진행', s.checksumAt > 0 && s.checksum.length === 8, `checksum=${s.checksum}@${s.checksumAt} score=${s.score} coins=${s.coins} jumps=${s.jumps}`);
    check(res, '플레이 프레임 통계', s.frames > 100, `frames=${s.frames} avg=${s.avgMs}ms p95=${s.p95Ms}ms long=${s.longFrames}`);
    const heap = await pageCdp.send('Runtime.getHeapUsage').catch(() => null);
    m.jsHeapUsedBytes = heap ? heap.usedSize : null;
    m.wasmHeapBytes = await page.evaluate(() => {
      const mod = window['unityInstance'] && window['unityInstance'].Module;
      const h = mod && (mod.HEAPU8 || (mod.wasmMemory && { length: mod.wasmMemory.buffer.byteLength }));
      return h ? h.length : null;
    }).catch(() => null);
    if (full) {
      const bgmAdv = Math.max(0, (s.bgmTime || 0) - (bgm0 || 0));
      const media1 = await getMediaBgmTime(page);
      const mediaAdv = media0 !== null && media1 !== null ? Math.max(0, media1 - media0) : 0;
      m.bgmAdvancedSec = Math.max(bgmAdv, mediaAdv);
      check(res, 'BGM 재생 진행', s.bgmPlaying && m.bgmAdvancedSec >= 5,
        `advanced=${m.bgmAdvancedSec.toFixed(1)}s (AudioSource.time ${bgmAdv.toFixed(1)}s, media ${mediaAdv.toFixed(1)}s)`);
    }
    if (rss) {
      const peaks = await rss.stop();
      rss = null;
      m.rendererPeakRssBytes = peaks.renderer || null;
      m.gpuPeakRssBytes = peaks.gpu || null;
    }
    m.ctxLost = await page.evaluate(() => window['__ctxLost'] || 0).catch(() => null);
    check(res, 'WebGL 컨텍스트 유지', m.ctxLost === 0, `ctxLost=${m.ctxLost}`);

    // ---- 4. 영속성(리로드 후 최고 점수) ----
    if (s.state === 'playing') await sendCmd(page, 'over');
    s = await waitForRun(page, '(s) => s.state === "over"', 10000, '자동 플레이 종료');
    const bestFinal = s.best;
    check(res, '최고 점수 단조 증가', bestFinal >= bestAfterManual, `best=${bestFinal}`);
    m.errorCount = s.errorCount;
    if (full) {
      await sleep(1500);   // PlayerPrefs.Save → IDBFS 동기화 여유
      await page.reload({ waitUntil: 'commit', timeout: 120000 });
      const s2 = await waitForRun(page, '(s) => s.state === "title"', 120000, '리로드 후 타이틀');
      check(res, '리로드 후 최고 점수 유지(PlayerPrefs)', s2.bootBest === bestFinal, `bootBest=${s2.bootBest} expected=${bestFinal}`);
      m.errorCount += s2.errorCount;
    }
    check(res, 'Unity 오류 로그 0', m.errorCount === 0, `errorCount=${m.errorCount} last=${s.lastError}`);
  } catch (e) {
    res.fatal = String((e && e.stack) || e);
    console.log(`  ✗ [${label}#${round}] 치명적 오류: ${res.fatal.split('\n')[0]}`);
  } finally {
    if (rss) await rss.stop().catch(() => {});
    check(res, '예상 밖 콘솔/요청 오류 0', res.console.unexpected.length === 0, res.console.unexpected.slice(0, 3).join(' | '));
    await browser.close().catch(() => {});
  }
  res.passed = !res.fatal && res.checks.every((c) => c.pass);
  return res;
}

const METRICS = [
  ['titleMs', 'ms', 0], ['avgFrameMs', 'ms', 2], ['p95FrameMs', 'ms', 2], ['p99FrameMs', 'ms', 2], ['longFrames', '', 0],
  ['fps', '', 1], ['renderPixels', '', 0], ['rendererPeakRssBytes', 'MB', 1], ['gpuPeakRssBytes', 'MB', 1], ['jsHeapUsedBytes', 'MB', 1], ['wasmHeapBytes', 'MB', 1],
];
function summarize(sessions) {
  const out = {};
  for (const [k] of METRICS) out[k] = median(sessions.map((r) => r.metrics[k]));
  return out;
}
const fmt = (k, v) => {
  if (v === null || v === undefined) return 'N/A';
  const spec = METRICS.find((x) => x[0] === k);
  return spec && spec[1] === 'MB' ? (v / 1048576).toFixed(spec[2]) + 'MB' : v.toFixed(spec ? spec[2] : 1) + (spec ? spec[1] : '');
};

test.describe('Mobile web game (tap runner) play benchmark', () => {
  test('mobilegame: 기능 검증 + 플레이 지표', async () => {
    test.skip(!ENABLED, 'mobilegame 실행이 아니다(RUN_REQUIRE=1 또는 RUN_MOBILEGAME=1 로 켠다)');
    const sides = [{ label: LABEL_A, project: PROJECT, port: PORT_A }];
    if (PAIR_MODE) sides.push({ label: LABEL_B, project: PAIR_PROJECT, port: PORT_B });
    for (const side of sides) {
      const has = fs.existsSync(path.resolve(side.project, 'ait-build/dist/web'));
      if (!has) {
        if (REQUIRE) throw new Error(`산출물 없음: ${side.project}`);
        test.skip(true, `산출물 없음: ${side.project}`);
      }
    }

    const servers = [];
    try {
      for (const side of sides) servers.push({ proc: await startServer(side.project, side.port), port: side.port });
      const sessions = [];
      for (let round = 0; round < ROUNDS; round++) {
        const order = round % 2 === 0 ? sides : [...sides].reverse();
        for (const side of order) {
          console.log(`\n▶ ${side.label} round ${round + 1}/${ROUNDS}`);
          sessions.push(await runSession(side.label, side.project, side.port, round, round === 0));
        }
      }
      const bySide = {};
      for (const side of sides) {
        const rs = sessions.filter((r) => r.label === side.label);
        bySide[side.label] = { project: side.project, summary: summarize(rs), checksums: [...new Set(rs.map((r) => r.checksum))] };
      }
      console.log(`\n📊 Mobile game — Unity ${UNITY_VERSION}, CPU x${CPU_THROTTLE}${RUN_DPR ? `, DPR ${RUN_DPR}` : ''}, ${ROUNDS} rounds/side (중앙값)`);
      const header = ['metric', ...sides.map((s) => s.label), ...(PAIR_MODE ? ['B−A'] : [])];
      console.log('  ' + header.join(' | '));
      for (const [k] of METRICS) {
        const a = bySide[LABEL_A].summary[k];
        const row = [k, fmt(k, a)];
        if (PAIR_MODE) {
          const b = bySide[LABEL_B].summary[k];
          row.push(fmt(k, b));
          row.push(a !== null && b !== null ? (b - a >= 0 ? '+' : '') + fmt(k, b - a) : 'N/A');
        }
        console.log('  ' + row.join(' | '));
      }

      const out = {
        unityVersion: UNITY_VERSION, cpuThrottle: CPU_THROTTLE, dpr: RUN_DPR || null, rounds: ROUNDS, pairMode: PAIR_MODE,
        labels: { a: LABEL_A, b: PAIR_MODE ? LABEL_B : null }, sides: bySide, sessions,
      };
      fs.writeFileSync(RESULT_PATH, JSON.stringify(out, null, 2));
      console.log(`결과: ${RESULT_PATH}`);

      const failed = sessions.filter((r) => !r.passed);
      for (const r of failed) {
        console.log(`✗ ${r.label}#${r.round}: ${r.fatal ? r.fatal.split('\n')[0] : r.checks.filter((c) => !c.pass).map((c) => `${c.name}(${c.detail})`).join(', ')}`);
      }
      // pair 모드의 A 는 비교 기준(base)이다. A 의 실패는 측정 결과로 남기고 경고만 하며, 단언은 검증 대상인 B 에만 건다.
      const gated = PAIR_MODE ? failed.filter((r) => r.label === LABEL_B) : failed;
      if (PAIR_MODE && failed.length > gated.length) {
        console.log(`⚠️ 기준(A=${LABEL_A}) 세션 ${failed.length - gated.length}건 실패 — 비교 기준이라 단언하지 않는다`);
      }
      expect(gated.length, '검증 대상 세션의 기능 검사 통과').toBe(0);
      const target = PAIR_MODE ? LABEL_B : LABEL_A;
      expect(bySide[target].checksums.length, `${target}: 세션 간 체크섬 결정론`).toBe(1);
      if (PAIR_MODE) {
        expect(bySide[LABEL_A].checksums.length, `${LABEL_A}: 세션 간 체크섬 결정론`).toBeLessThanOrEqual(1);
        if (bySide[LABEL_A].checksums.length === 1) {
          expect(bySide[LABEL_B].checksums[0], 'A/B 체크섬 동일(게임 동작이 같다)').toBe(bySide[LABEL_A].checksums[0]);
        }
      }
    } finally {
      for (const s of servers) await stopServer(s.proc, s.port);
    }
  });
});
