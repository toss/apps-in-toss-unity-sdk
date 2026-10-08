// @ts-check
import { test, expect, chromium, devices } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as os from 'os';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK: 실제 게임형 기능 검증(game posture) 플레이 테스트
 *
 * 목적: SDK 가 자동으로 켜는 성능 최적화(텍스처/오디오 외부화, 코드 크기 레버, IDBFS 프리워밍 등)가 실제 게임 기능을 깨지 않는지 증명한다.
 * 대상은 HeavyBuildRunner 의 `game` posture 로 빌드한 브레이크아웃 픽스처(SharedScripts/Runtime/Game/AITGame*.cs)다.
 * 게임은 상태를 window.__AIT_GAME 으로 보고하고(AITGameBridge.jslib), unityInstance.SendMessage('AITGameDriver','Command', json) 으로 제어된다.
 *
 * 시나리오(프로젝트 1개당 새 persistent context, 같은 context 안에서 IndexedDB 유지):
 *   1. 로드 → GameTitle 보고 대기   2. 결정론 체크섬 확보   3. 실제 마우스 클릭으로 Start → GamePlay(Resources 텍스처·StreamingAssets JSON 확인)
 *   4. 포인터 봇으로 라운드 플레이(패들 추종·점수·벽돌·SFX·BGM·AudioContext·프레임 정체·컨텍스트 로스트)
 *   5. 백그라운드/포그라운드 전환   6. GameResult → Retry 클릭 → GamePlay → endRound   7. 리로드 후 하이스코어 유지
 *   8. 오류 예산   9. JSON 결과 기록   10. 페어 모드(PERF_PAIR_PROJECT_PATH)에서는 A/B 체크섬이 같아야 한다.
 *
 * 환경변수:
 *  - UNITY_PROJECT_PATH       : A 산출물 프로젝트 경로(ait-build/dist/web 포함).
 *  - PERF_PAIR_PROJECT_PATH   : B 산출물 프로젝트 경로(지정 시 페어 모드).
 *  - PERF_UNITY_VERSION       : 결과 JSON 파일명에 쓸 버전(미지정 시 경로에서 추출).
 *  - PERF_LABEL_A / PERF_LABEL_B : 결과 라벨.
 *  - GAME_REQUIRE             : 1이면 game 빌드가 아닐 때 skip 대신 실패(CI 용).
 *  - GAME_CHROME_CHANNEL      : Playwright channel(기본 chrome, 빈 값이면 번들 chromium).
 *  - GAME_ROUND_SECONDS       : 플레이 라운드 길이(기본 20).
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PAIR_PROJECT = process.env.PERF_PAIR_PROJECT_PATH || '';
const PAIR_MODE = !!PAIR_PROJECT;
const LABEL_A = process.env.PERF_LABEL_A || 'A';
const LABEL_B = process.env.PERF_LABEL_B || 'B';
const REQUIRE_GAME = process.env.GAME_REQUIRE === '1';
const CHANNEL = process.env.GAME_CHROME_CHANNEL === undefined ? 'chrome' : process.env.GAME_CHROME_CHANNEL;
const ROUND_SECONDS = Math.max(10, parseInt(process.env.GAME_ROUND_SECONDS || '20', 10));

function findProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) return envPath;
  for (const version of ['6000.3', '6000.0', '6000.2', '2022.3', '2021.3']) {
    const p = path.resolve(__dirname, `../HeavySampleUnityProject-${version}`);
    if (fs.existsSync(path.resolve(p, 'ait-build/dist/web'))) return p;
  }
  return path.resolve(__dirname, '../HeavySampleUnityProject-6000.3');
}
const PROJECT = findProject();

function detectVersion(projectPath) {
  if (process.env.PERF_UNITY_VERSION) return process.env.PERF_UNITY_VERSION;
  const m = projectPath.match(/(?:Heavy)?SampleUnityProject-(\d+\.\d+)/);
  return m ? m[1] : 'unknown';
}
const UNITY_VERSION = detectVersion(PROJECT);

function portOffset(version) {
  const m = version.match(/(\d+)\.(\d+)/);
  if (!m) return 0;
  const major = parseInt(m[1], 10);
  const minor = parseInt(m[2], 10);
  if (major === 2021) return 0;
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  if (major === 6000 && minor === 3) return 4;
  return 0;
}
// full-pipeline(4173+) / perf A(4223+) / perf B(4273+) / ce-serving(4323+) 과 겹치지 않는 대역
const PORT_A = 4373 + portOffset(UNITY_VERSION);
const PORT_B = 4423 + portOffset(UNITY_VERSION);

const RESULT_PATH = path.resolve(__dirname, `game-results-${UNITY_VERSION}.json`);

// ---- 알려진 무해 메시지 허용 목록(Toss 앱 밖에서 도는 환경에서만 나는 것) ----
// 여기에 없는 console error / pageerror / 동일 출처 요청 실패는 전부 실패로 센다. 모든 메시지는 결과 JSON 에도 남는다.
const BENIGN_PATTERNS = [
  /favicon/i,
  /^Failed to load resource/i,                       // 상세 URL 은 response/requestfailed 핸들러가 별도로 판정한다
  /net::ERR_ABORTED/i,                               // 리로드/네비게이션 중 취소된 요청
  /apps-?in-?toss.*(webview|native|environment|app)/i,
  /(not|outside).{0,40}(toss|apps-?in-?toss)\s*(app|webview|environment)/i,
  /(toss|apps-?in-?toss)\s*(app|webview|environment).{0,40}(not|unavailable|missing)/i,
  /native\s*(app|bridge|module)\s*(info|not|unavailable)/i,
  /getAppsInTossGlobals|__GRANITE_NATIVE_EMITTER|ReactNativeWebView/i,
  /analytics.{0,60}(read-?only|not allowed|unavailable|disabled|not supported)/i,
  /(read-?only).{0,60}analytics/i,
  /sentry.{0,60}(dsn|disabled|not initialized)/i,
];
function isBenign(text) {
  return BENIGN_PATTERNS.some((re) => re.test(text));
}

// ---- 유틸리티 ----
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function isPortAvailable(port) {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once('error', () => resolve(false));
    server.once('listening', () => server.close(() => resolve(true)));
    server.listen(port, '127.0.0.1');
  });
}
async function waitForPortRelease(port, timeoutMs = 8000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPortAvailable(port)) return true;
    await sleep(200);
  }
  return false;
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
async function startProductionServer(aitBuildDir, defaultPort) {
  freePort(defaultPort);
  await waitForPortRelease(defaultPort, 5000);
  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(defaultPort)], {
      cwd: aitBuildDir, stdio: 'pipe', shell: true, env: { ...process.env, NODE_OPTIONS: '' },
    });
    let started = false;
    let actualPort = defaultPort;
    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[vite preview]', output.trim());
      const portMatch = output.match(/(?:Local:\s+http:\/\/localhost:|listening.*?port\s*|:)(\d+)/i);
      if (portMatch) actualPort = parseInt(portMatch[1], 10);
      if (/Local:|listening|Accepting connections|ready/.test(output) && !started) {
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });
    server.stderr.on('data', (data) => console.error('[vite preview error]', data.toString().trim()));
    server.on('error', reject);
    setTimeout(() => { if (!started) { started = true; resolve({ process: server, port: actualPort }); } }, 10000);
  });
}
async function killServer(proc, port) {
  if (!proc) return;
  try { proc.kill('SIGTERM'); } catch {}
  await new Promise((resolve) => {
    if (proc.exitCode !== null) return resolve(true);
    const t = setTimeout(() => resolve(false), 3000);
    proc.once('exit', () => { clearTimeout(t); resolve(true); });
  });
  freePort(port);
  await waitForPortRelease(port, 5000);
}
async function waitServerReady(port) {
  for (let i = 0; i < 30; i++) {
    try {
      const res = await fetch(`http://localhost:${port}/`, { method: 'HEAD' });
      if (res.ok) return true;
    } catch {}
    await sleep(500);
  }
  return false;
}

// ---- 페이지 init script: AudioContext 추적, webglcontextlost 감지, 첫 보고 시각 ----
const INIT_SCRIPT = `
(function () {
  if (window.__aitGameHooked) return;
  window.__aitGameHooked = true;
  window.__aitAudioCtxs = [];
  window.__ctxLost = 0;
  window.__aitFirstReport = null;
  try {
    ['AudioContext', 'webkitAudioContext'].forEach(function (k) {
      var Orig = window[k];
      if (typeof Orig !== 'function' || Orig.__aitProxied) return;
      var P = new Proxy(Orig, {
        construct: function (target, args, newTarget) {
          var inst = Reflect.construct(target, args, newTarget === P ? target : newTarget);
          try { window.__aitAudioCtxs.push(inst); } catch (e) {}
          return inst;
        },
        get: function (target, prop, recv) { if (prop === '__aitProxied') return true; return Reflect.get(target, prop, target); }
      });
      window[k] = P;
    });
  } catch (e) {}
  window.addEventListener('webglcontextlost', function () { window.__ctxLost++; }, true);
  var _g;
  Object.defineProperty(window, '__AIT_GAME', {
    configurable: true,
    get: function () { return _g; },
    set: function (v) { if (!_g) window.__aitFirstReport = performance.now(); _g = v; }
  });
})();
`;

const getState = (page) => page.evaluate(() => window['__AIT_GAME'] || null).catch(() => null);

async function sendCmd(page, obj) {
  await page.evaluate((j) => { window['unityInstance'].SendMessage('AITGameDriver', 'Command', j); }, JSON.stringify(obj));
}

async function waitForState(page, predicateSrc, timeoutMs, what) {
  // predicateSrc: (s) => boolean 형태의 함수 소스 문자열. 페이지 CSP 와 무관하게 CDP 표현식으로 평가한다.
  const ok = await page.waitForFunction(
    `!!window.__AIT_GAME && (${predicateSrc})(window.__AIT_GAME)`,
    undefined,
    { timeout: timeoutMs, polling: 100 },
  ).then(() => true).catch(() => false);
  if (!ok) {
    const s = await getState(page);
    throw new Error(`대기 실패: ${what} (${timeoutMs}ms). 마지막 상태 scene=${s && s.scene} ready=${s && s.ready}`);
  }
  return getState(page);
}

async function canvasBox(page) {
  const box = await page.locator('#unity-canvas').boundingBox({ timeout: 5000 }).catch(() => null);
  if (box) return box;
  const b2 = await page.locator('canvas').first().boundingBox({ timeout: 5000 });
  if (!b2) throw new Error('캔버스를 찾지 못했다');
  return b2;
}

async function waitOverlayGone(page) {
  await page.waitForFunction(() => {
    const e = document.querySelector('#ait-loading-wrapper');
    return !e || e.style.display === 'none' || getComputedStyle(e).display === 'none' || getComputedStyle(e).visibility === 'hidden';
  }, undefined, { timeout: 20000 }).catch(() => {});
}

async function clickButton(page, state, name) {
  const b = (state.buttons || []).find((x) => x.name === name);
  if (!b) throw new Error(`버튼 '${name}' 이 보고에 없다: ${JSON.stringify(state.buttons)}`);
  const box = await canvasBox(page);
  const x = box.x + b.x * box.width;
  const y = box.y + b.y * box.height;
  await page.mouse.move(x - 3, y - 3);
  await page.mouse.click(x, y);
  return { x, y };
}

/** 프레임 정체 추적: 프레임 값이 안 바뀐 가장 긴 구간(ms). */
class StallTracker {
  constructor() { this.lastFrame = -1; this.lastChange = Date.now(); this.maxGap = 0; this.samples = 0; }
  observe(s) {
    const now = Date.now();
    this.samples++;
    if (s.frame !== this.lastFrame) {
      if (this.lastFrame >= 0) this.maxGap = Math.max(this.maxGap, now - this.lastChange);
      this.lastFrame = s.frame;
      this.lastChange = now;
    } else {
      this.maxGap = Math.max(this.maxGap, now - this.lastChange);
    }
  }
}

function makeResult(label, projectPath) {
  return { label, project: projectPath, passed: false, checks: [], skipped: [], metrics: {}, checksum: null, console: { unexpected: [], benign: [] }, fatal: null };
}
function check(res, name, ok, detail) {
  res.checks.push({ name, pass: !!ok, detail: detail === undefined ? '' : String(detail) });
  console.log(`  ${ok ? '✓' : '✗'} [${res.label}] ${name}${detail !== undefined && detail !== '' ? ` (${detail})` : ''}`);
  return !!ok;
}

/** 한 프로젝트에 대한 전체 시나리오. 결과 객체를 돌려준다(예외는 res.fatal 에 기록). */
async function runScenario(label, projectPath, port) {
  const res = makeResult(label, projectPath);
  const url = `http://localhost:${port}?e2e=true`;
  const userDataDir = fs.mkdtempSync(path.join(os.tmpdir(), `ait-game-${label.replace(/[^a-z0-9]/gi, '_')}-`));
  const { defaultBrowserType, ...device } = devices['Pixel 7'];
  void defaultBrowserType;
  const context = await chromium.launchPersistentContext(userDataDir, {
    ...device,
    channel: CHANNEL || undefined,
    headless: true,
    args: ['--enable-webgl', '--use-angle=default', '--enable-features=VaapiVideoDecoder'],
  });
  const origin = `http://localhost:${port}`;
  const page = context.pages()[0] || await context.newPage();
  await page.addInitScript(INIT_SCRIPT);

  // ---- 콘솔/요청 오류 수집 ----
  const record = (kind, text) => {
    const line = `${kind}: ${String(text).slice(0, 400)}`;
    if (isBenign(text)) res.console.benign.push(line);
    else res.console.unexpected.push(line);
  };
  let warnCount = 0;
  page.on('console', (m) => { if (m.type() === 'error') record('console.error', m.text()); else if (m.type() === 'warning') warnCount++; });
  page.on('pageerror', (e) => record('pageerror', e && e.message));
  page.on('crash', () => record('crash', 'page crashed'));
  page.on('requestfailed', (r) => { if (r.url().startsWith(origin)) record('requestfailed', `${r.url()} ${r.failure() && r.failure().errorText}`); });
  page.on('response', (r) => { if (r.url().startsWith(origin) && r.status() >= 400) record('http', `${r.status()} ${r.url()}`); });

  const stall = new StallTracker();
  const m = res.metrics;
  try {
    // ================= 1. 로드 =================
    const t0 = Date.now();
    const resp = await page.goto(url, { waitUntil: 'commit', timeout: 120000 });
    check(res, '페이지 200', resp && resp.status() === 200, resp && resp.status());
    await page.waitForFunction(() => window['unityInstance'] !== undefined, undefined, { timeout: 120000 })
      .catch(() => { throw new Error('unityInstance 가 120초 안에 생기지 않았다(빌드/로드 실패)'); });
    const reported = await page.waitForFunction(() => !!window['__AIT_GAME'], undefined, { timeout: 60000 }).then(() => true).catch(() => false);
    if (!reported) {
      res.notGameBuild = true;
      res.fatal = 'game 빌드가 아님: unityInstance 는 있으나 __AIT_GAME 보고가 없다';
      return res;
    }
    let s = await waitForState(page, 's => s.scene === "GameTitle" && s.ready && s.buttons.length > 0', 60000, 'GameTitle 준비');
    m.titleReadyMs = Date.now() - t0;
    m.firstReportMs = Math.round(await page.evaluate(() => window['__aitFirstReport']) || 0);
    m.unityVersion = s.unityVersion;
    m.screen = `${s.screenW}x${s.screenH}`;
    check(res, 'GameTitle 도달', s.scene === 'GameTitle', `titleReady ${m.titleReadyMs}ms, firstReport ${m.firstReportMs}ms`);

    // ================= 2. 체크섬 =================
    s = await waitForState(page, 's => s.checksum && s.checksum.done', 60000, '체크섬 계산 완료');
    res.checksum = s.checksum;
    const ck = s.checksum;
    check(res, '체크섬 성분 존재', ['rng', 'math', 'hash', 'collections', 'json', 'physics', 'combined'].every((k) => typeof ck[k] === 'string' && ck[k].length > 0), JSON.stringify(ck));
    s = await waitForState(page, 's => s.coroutineTicks >= 3 && s.asyncDone && s.jsonLoaded', 20000, 'coroutine/async/StreamingAssets 완료').catch(async (e) => { check(res, 'coroutine/async/StreamingAssets', false, e.message); return getState(page); });
    check(res, 'coroutine ticks', s.coroutineTicks >= 3, s.coroutineTicks);
    check(res, 'async Task.Yield 동작', s.asyncDone && s.asyncTicks >= 3, `ticks=${s.asyncTicks}`);
    check(res, '명시적 예외 try/catch', s.caught.indexOf('ait-game-expected-exception') >= 0, s.caught);
    check(res, 'StreamingAssets JSON 값 일치', s.jsonLoaded && s.jsonValue === 'streaming-ok-7391', `loaded=${s.jsonLoaded} value=${s.jsonValue} err=${s.jsonError}`);
    m.titleHighScoreAtBoot = s.titleHighScore;

    await sendCmd(page, { cmd: 'ping' });
    s = await waitForState(page, 's => s.lastCommand === "ping" && s.commandCount >= 1', 5000, 'SendMessage ping').catch(() => null);
    check(res, 'SendMessage Command(ping) 도달', !!s);
    s = await getState(page);

    // ================= 3. Start 클릭(실제 마우스) =================
    await waitOverlayGone(page);
    const bgmBeforeClick = s.audio.bgmTime;
    m.bgmBeforeGesture = bgmBeforeClick;
    await clickButton(page, s, 'start');
    s = await waitForState(page, 's => s.scene === "GamePlay" && s.ready', 15000, 'GamePlay 진입').catch((e) => { check(res, 'Start 클릭 후 GamePlay', false, e.message); return null; });
    if (!s) throw new Error('Start 클릭이 GamePlay 로 이어지지 않았다');
    check(res, 'Start 클릭 → GamePlay', true);
    check(res, 'Resources 대형 텍스처 2048x2048', s.resTexW === 2048 && s.resTexH === 2048, `${s.resTexW}x${s.resTexH}`);
    check(res, 'SFX(UI 클릭) 재생 호출', s.audio.sfxCount >= 1, s.audio.sfxCount);

    // ================= 4. 플레이 =================
    await sendCmd(page, { cmd: 'setRoundSeconds', value: ROUND_SECONDS });
    const box = await canvasBox(page);
    const px = (nx) => box.x + nx * box.width;
    const py = (ny) => box.y + ny * box.height;
    const paddleY = 0.9;

    // 패들이 포인터를 따라가는지: 두 지점으로 보낸다(공이 지나가도 상관없는 짧은 구간).
    let followOk = 0;
    let followDetail = [];
    for (const target of [0.35, 0.65]) {
      await page.mouse.move(px(target), py(paddleY), { steps: 3 });
      const hit = await page.waitForFunction((t) => { const g = window['__AIT_GAME']; return g && g.scene === 'GamePlay' && Math.abs(g.paddle.x - t) < 0.06; }, target, { timeout: 3000, polling: 50 })
        .then(() => true).catch(() => false);
      const g = await getState(page);
      followDetail.push(`target=${target} paddle=${g && g.paddle.x.toFixed(3)} pointer=${g && g.pointer.x.toFixed(3)}`);
      if (hit) followOk++;
    }
    check(res, '패들이 포인터를 추종', followOk >= 1, followDetail.join('; '));

    const play = { maxScore: 0, maxBricks: 0, maxSfx: 0, maxCombo: 0, bgmAccum: 0, firstFrame: -1, lastFrame: 0, firstT: 0, lastT: 0 };
    let prevBgm = null;
    let bgmLen = 64;
    let touchTaps = 0;
    let touchPointerOk = null;
    const loopStart = Date.now();
    let lastSeenPlay = null;
    while (Date.now() - loopStart < (ROUND_SECONDS + 40) * 1000) {
      const g = await getState(page);
      if (!g) { await sleep(50); continue; }
      stall.observe(g);
      if (g.scene === 'GameResult') break;
      if (g.scene === 'GamePlay') {
        lastSeenPlay = g;
        play.maxScore = Math.max(play.maxScore, g.score);
        play.maxBricks = Math.max(play.maxBricks, g.bricksDestroyed);
        play.maxCombo = Math.max(play.maxCombo, g.maxCombo);
        play.maxSfx = Math.max(play.maxSfx, g.audio.sfxCount);
        if (play.firstFrame < 0) { play.firstFrame = g.frame; play.firstT = g.time; }
        play.lastFrame = g.frame;
        play.lastT = g.time;
        bgmLen = g.audio.bgmLength || bgmLen;
        if (prevBgm !== null) {
          let d = g.audio.bgmTime - prevBgm;
          if (d < -1) d += bgmLen;     // 루프 되감김
          if (d > 0 && d < 5) play.bgmAccum += d;
        }
        prevBgm = g.audio.bgmTime;
        // 터치 탭 2회(라운드 중간): hasTouch 일 때만. 탭 직후 Unity 가 본 포인터 위치를 기록한다(정보용).
        if (touchTaps < 2 && g.timeLeft > 0 && g.timeLeft < ROUND_SECONDS * (touchTaps === 0 ? 0.7 : 0.45)) {
          try {
            const tx = 0.3 + 0.4 * touchTaps;
            await page.touchscreen.tap(px(tx), py(0.5));
            await sleep(150);
            const after = await getState(page);
            if (after) touchPointerOk = (touchPointerOk !== false) && Math.abs(after.pointer.x - tx) < 0.08;
            touchTaps++;
          } catch (e) {
            touchTaps = 2;
            res.skipped.push(`touchscreen.tap 불가: ${e && e.message}`);
          }
        }
        // 봇: 공 아래로 패들을 옮긴다(실제 포인터 이벤트).
        const bx = Math.min(0.92, Math.max(0.08, g.ball.x));
        await page.mouse.move(px(bx), py(paddleY));
      }
      await sleep(50);
    }
    // 라운드 종료 후 결과 씬
    s = await waitForState(page, 's => s.scene === "GameResult" && s.ready', 20000, 'GameResult 진입');
    const ctxStates = await page.evaluate(() => (window['__aitAudioCtxs'] || []).map((c) => c.state));
    const ctxLost = await page.evaluate(() => window['__ctxLost'] || 0);
    m.play = play;
    m.audioContextStates = ctxStates;
    m.touchTaps = touchTaps;
    m.touchPointerOk = touchPointerOk;
    m.fps = play.lastT > play.firstT ? Math.round(((play.lastFrame - play.firstFrame) / (play.lastT - play.firstT)) * 10) / 10 : null;
    m.maxFrameStallMs = stall.maxGap;
    check(res, '점수 > 0', play.maxScore > 0, play.maxScore);
    check(res, '파괴한 벽돌 > 0', play.maxBricks > 0, play.maxBricks);
    check(res, 'SFX 호출 > 1 (클릭 외 벽돌/패들)', play.maxSfx > 1, play.maxSfx);
    check(res, 'BGM 재생 중', !!(lastSeenPlay && lastSeenPlay.audio.bgmPlaying), lastSeenPlay && lastSeenPlay.audio.bgmPlaying);
    check(res, 'BGM 시간 5초 이상 진행', play.bgmAccum >= 5, `advanced=${play.bgmAccum.toFixed(1)}s`);
    check(res, 'AudioContext running', ctxStates.some((st) => st === 'running'), JSON.stringify(ctxStates));
    check(res, '프레임 정체 2초 미만', stall.maxGap < 2000, `maxGap=${stall.maxGap}ms fps=${m.fps}`);
    check(res, 'webglcontextlost 없음', ctxLost === 0, ctxLost);
    if (touchTaps === 0) res.skipped.push('터치 탭 미수행');

    // ================= 5. 백그라운드/포그라운드 =================
    // 먼저 GamePlay 로 돌아가 있는 편이 상태 관찰에 낫다: Result 에서 수행해도 BGM/프레임은 관찰된다.
    const bg = await backgroundCycle(context, page);
    m.background = bg;
    if (bg.triggered) check(res, `생명주기 이벤트 발생(${bg.method})`, true, JSON.stringify(bg.delta));
    else res.skipped.push(`OnApplicationPause/Focus 를 Chromium 에서 유발하지 못함: ${bg.reason}`);
    check(res, '백그라운드 복귀 후 프레임 진행', bg.framesAdvanced, `frames +${bg.frameDelta}`);
    check(res, '백그라운드 복귀 후 BGM 재생', bg.bgmPlaying && bg.bgmAdvanced, `playing=${bg.bgmPlaying} advanced=${bg.bgmAdvanced}`);

    // ================= 6. 결과 → Retry → endRound =================
    s = await getState(page);
    const savedHigh = s.highScore;
    m.resultScore = s.lastScore;
    m.highScoreAfterRound1 = savedHigh;
    check(res, '결과 씬 점수/하이스코어 저장', s.lastScore > 0 && savedHigh >= s.lastScore, `last=${s.lastScore} high=${savedHigh}`);
    await sleep(2500);   // PlayerPrefs.Save → IDBFS 동기화 시간
    await clickButton(page, s, 'retry');
    s = await waitForState(page, 's => s.scene === "GamePlay" && s.ready', 15000, 'Retry 후 GamePlay').catch((e) => { check(res, 'Retry 클릭 → GamePlay', false, e.message); return null; });
    if (!s) throw new Error('Retry 클릭이 GamePlay 로 이어지지 않았다');
    check(res, 'Retry 클릭 → GamePlay', true);
    await sleep(2000);
    await sendCmd(page, { cmd: 'endRound' });
    s = await waitForState(page, 's => s.scene === "GameResult" && s.ready && s.rounds >= 2', 15000, 'endRound 후 GameResult');
    check(res, 'endRound 명령으로 GameResult', true, `rounds=${s.rounds}`);
    const finalHigh = s.highScore;
    m.finalHighScore = finalHigh;
    await sleep(3000);   // IDBFS 플러시

    // ================= 7. 리로드 후 하이스코어 =================
    await page.reload({ waitUntil: 'commit', timeout: 120000 });
    s = await waitForState(page, 's => s.scene === "GameTitle" && s.ready', 90000, '리로드 후 GameTitle');
    check(res, '리로드 후 Title 하이스코어 유지', finalHigh > 0 && s.bootHighScore === finalHigh && s.titleHighScore === finalHigh, `saved=${finalHigh} boot=${s.bootHighScore} title=${s.titleHighScore}`);
    s = await waitForState(page, 's => s.checksum && s.checksum.done', 60000, '리로드 후 체크섬');
    check(res, '리로드 전후 체크섬 동일', s.checksum.combined === res.checksum.combined, `${res.checksum.combined} vs ${s.checksum.combined}`);

    // ================= 8. 오류 예산 =================
    const finalState = await getState(page);
    m.unityErrorCount = finalState.errorCount;
    m.unityWarnCount = finalState.warnCount;
    m.browserWarnCount = warnCount;
    const unityErrs = (finalState.lastErrors || []).filter((e) => !isBenign(e));
    check(res, 'Unity error/exception 0', finalState.errorCount === 0 || unityErrs.length === 0, `count=${finalState.errorCount} ${JSON.stringify(finalState.lastErrors)}`);
  } catch (e) {
    res.fatal = res.fatal || (e && e.message) || String(e);
    console.error(`  ✗ [${label}] 시나리오 중단: ${res.fatal}`);
  } finally {
    // 오류 예산(콘솔/페이지) 판정은 중단 여부와 무관하게 수행
    if (!res.notGameBuild) {
      check(res, '브라우저 콘솔/요청 오류 없음(허용 목록 제외)', res.console.unexpected.length === 0, res.console.unexpected.slice(0, 5).join(' | '));
    }
    console.log(`  [${label}] 허용된 무해 메시지 ${res.console.benign.length}건, 예기치 않은 메시지 ${res.console.unexpected.length}건`);
    for (const l of res.console.benign.slice(0, 20)) console.log(`    benign  ${l}`);
    for (const l of res.console.unexpected.slice(0, 20)) console.log(`    UNEXPECTED ${l}`);
    await context.close().catch(() => {});
    try { fs.rmSync(userDataDir, { recursive: true, force: true }); } catch {}
  }
  res.passed = !res.fatal && res.checks.every((c) => c.pass);
  return res;
}

/** 백그라운드→포그라운드 전환. Unity 의 OnApplicationPause/Focus 카운터가 움직이는 방법을 순서대로 시도한다. */
async function backgroundCycle(context, page) {
  const out = { triggered: false, method: null, reason: '', delta: null, framesAdvanced: false, frameDelta: 0, bgmPlaying: false, bgmAdvanced: false };
  const before = await getState(page);
  const counters = (g) => ({ pause: g.pauseEvents, focus: g.focusEvents });
  const moved = async () => {
    const a = await getState(page);
    return a && (a.pauseEvents > before.pauseEvents || a.focusEvents > before.focusEvents) ? { pause: a.pauseEvents - before.pauseEvents, focus: a.focusEvents - before.focusEvents } : null;
  };
  try {
    // 방법 1: 다른 탭을 앞으로 가져왔다가 되돌린다.
    const other = await context.newPage();
    await other.goto('about:blank');
    await other.bringToFront();
    await sleep(1500);
    await page.bringToFront();
    await other.close();
    await sleep(1500);
    let d = await moved();
    if (d) { out.triggered = true; out.method = 'bringToFront'; out.delta = d; }

    // 방법 2: visibilitychange/blur/focus 를 합성한다(document.hidden 을 덮어씀).
    if (!out.triggered) {
      await page.evaluate(() => {
        Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
        Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
        document.dispatchEvent(new Event('visibilitychange'));
        window.dispatchEvent(new Event('blur'));
      });
      await sleep(1500);
      await page.evaluate(() => {
        delete document['hidden'];
        delete document['visibilityState'];
        document.dispatchEvent(new Event('visibilitychange'));
        window.dispatchEvent(new Event('focus'));
      });
      await sleep(1500);
      d = await moved();
      if (d) { out.triggered = true; out.method = 'synthetic-visibility'; out.delta = d; }
    }
    if (!out.triggered) out.reason = `카운터 변화 없음(pause=${counters(before).pause}, focus=${counters(before).focus})`;
  } catch (e) {
    out.reason = `전환 중 예외: ${e && e.message}`;
  }
  // 게임이 계속 도는지
  const g1 = await getState(page);
  await sleep(2500);
  const g2 = await getState(page);
  if (g1 && g2) {
    out.frameDelta = g2.frame - g1.frame;
    out.framesAdvanced = out.frameDelta > 10;
    out.bgmPlaying = g2.audio.bgmPlaying;
    out.bgmAdvanced = g2.audio.bgmTime !== g1.audio.bgmTime;
  }
  return out;
}

function compareTable(a, b) {
  const rows = [
    ['firstReportMs', (r) => r.metrics.firstReportMs],
    ['titleReadyMs', (r) => r.metrics.titleReadyMs],
    ['score(max)', (r) => r.metrics.play && r.metrics.play.maxScore],
    ['bricks(max)', (r) => r.metrics.play && r.metrics.play.maxBricks],
    ['fps', (r) => r.metrics.fps],
    ['maxStallMs', (r) => r.metrics.maxFrameStallMs],
    ['bgmAdvanced(s)', (r) => r.metrics.play && Number(r.metrics.play.bgmAccum.toFixed(1))],
    ['unityErrors', (r) => r.metrics.unityErrorCount],
    ['checks passed', (r) => `${r.checks.filter((c) => c.pass).length}/${r.checks.length}`],
    ['checksum', (r) => r.checksum && r.checksum.combined],
  ];
  const w = 18;
  const line = (cols) => cols.map((c, i) => String(c === undefined ? '-' : c).padEnd(i === 0 ? w : 22)).join('');
  console.log('\n' + '='.repeat(62));
  console.log(line(['metric', `A[${a.label}]`, `B[${b.label}]`]));
  for (const [name, pick] of rows) console.log(line([name, pick(a), pick(b)]));
  console.log('='.repeat(62));
}

test.describe.configure({ mode: 'serial' });

test('game play 기능 검증 (최적화 ON/OFF 페어 비교)', async () => {
  test.setTimeout(PAIR_MODE ? 1500000 : 900000);

  const dirA = path.resolve(PROJECT, 'ait-build');
  expect(fs.existsSync(path.resolve(dirA, 'dist/web')), `dist/web 이 있어야 한다: ${dirA}`).toBe(true);
  if (PAIR_MODE) {
    expect(fs.existsSync(path.resolve(PAIR_PROJECT, 'ait-build/dist/web')), `페어(B) 산출물이 필요하다: ${PAIR_PROJECT}`).toBe(true);
  }
  console.log(`📦 game project A: ${PROJECT}${PAIR_MODE ? `\n📦 game project B: ${PAIR_PROJECT}` : ''}\n🏷️  Unity ${UNITY_VERSION}, round=${ROUND_SECONDS}s`);

  const results = [];
  const servers = [];
  try {
    const targets = [{ label: LABEL_A, project: PROJECT, port: PORT_A }];
    if (PAIR_MODE) targets.push({ label: LABEL_B, project: PAIR_PROJECT, port: PORT_B });
    for (const t of targets) {
      const srv = await startProductionServer(path.resolve(t.project, 'ait-build'), t.port);
      servers.push({ proc: srv.process, port: srv.port });
      expect(await waitServerReady(srv.port), `preview 서버 응답: :${srv.port}`).toBe(true);
      t.port = srv.port;
    }
    for (const t of targets) {
      console.log(`\n▶ 시나리오 [${t.label}] ${t.project}`);
      results.push(await runScenario(t.label, t.project, t.port));
    }
  } finally {
    for (const s of servers) await killServer(s.proc, s.port);
  }

  // ---- 결과 JSON ----
  const out = { unityVersion: UNITY_VERSION, pairMode: PAIR_MODE, generatedAt: new Date().toISOString(), runnerCpu: os.cpus()[0] && os.cpus()[0].model, A: results[0] || null, B: results[1] || null, pairing: null };
  const failures = [];
  const notGame = results.filter((r) => r.notGameBuild);
  if (notGame.length > 0 && !REQUIRE_GAME) {
    fs.writeFileSync(RESULT_PATH, JSON.stringify(out, null, 2));
    test.skip(true, `game 빌드가 아니다(AITGameDriver/__AIT_GAME 없음): ${notGame.map((r) => r.label).join(', ')}`);
    return;
  }
  for (const r of results) {
    if (!r.passed) {
      failures.push(`[${r.label}] ${r.fatal ? `중단: ${r.fatal}; ` : ''}${r.checks.filter((c) => !c.pass).map((c) => `${c.name} (${c.detail})`).join('; ')}`);
    }
  }
  if (PAIR_MODE && results.length === 2) {
    const [a, b] = results;
    const keys = ['rng', 'math', 'hash', 'collections', 'json', 'physics', 'combined'];
    const diffs = [];
    if (!a.checksum || !b.checksum) diffs.push('체크섬 없음');
    else for (const k of keys) if (a.checksum[k] !== b.checksum[k]) diffs.push(`${k}: A=${a.checksum[k]} B=${b.checksum[k]}`);
    out.pairing = { checksumEqual: diffs.length === 0, diffs, bothPassed: a.passed && b.passed };
    console.log(`\n🔗 결정론 체크섬 A/B ${diffs.length === 0 ? '동일' : '불일치: ' + diffs.join(', ')}`);
    compareTable(a, b);
    if (diffs.length > 0) failures.push(`A/B 결정론 체크섬 불일치(최적화가 프로그램 의미를 바꿨다): ${diffs.join(', ')}`);
  }
  fs.writeFileSync(RESULT_PATH, JSON.stringify(out, null, 2));
  console.log(`📝 결과 기록: ${RESULT_PATH}`);
  for (const r of results) {
    if (r.skipped.length) console.log(`  ℹ️  [${r.label}] skipped: ${r.skipped.join(' / ')}`);
  }
  expect(failures, failures.join('\n')).toEqual([]);
});
