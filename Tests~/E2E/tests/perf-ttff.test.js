// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as os from 'os';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — 로딩 성능 실측(perf) 테스트
 *
 * 목적: WebGL 로드타임 최적화 레버(L2~L12) 각각의 효과(Δ)를 격리 측정하기 위한
 * **TTFF(Time To First Frame)** 실측 하네스. 일반 E2E(e2e-full-pipeline.test.js)는
 * pageLoad/unityLoad를 *기록만* 하고 첫 WebGL draw 시점을 측정하지 않으며, 픽스처가
 * 가벼워 레버 효과가 노이즈에 묻힌다. 이 스펙은 무거운 픽스처(HeavySampleUnityProject-*,
 * HeavyBuildRunner로 빌드)를 모바일 수준 스로틀(CPU 4× + 네트워크) 아래에서 로드하고
 * **기본 프레임버퍼로의 첫 draw 시각**을 navStart 기준으로 잰다.
 *
 * 측정 방식:
 *  - TTFF: addInitScript로 HTMLCanvasElement.getContext → drawElements/drawArrays(+instanced)
 *    를 후킹, FRAMEBUFFER_BINDING === null(= 화면 기본 프레임버퍼)인 첫 draw의 performance.now().
 *  - 스로틀: CDP Emulation.setCPUThrottlingRate + Network.emulateNetworkConditions.
 *  - on-wire 바이트: Resource Timing transferSize를 .wasm / .data / 기타로 분리.
 *  - 화면 노출(firstVisible): 첫 draw 와 로딩 오버레이(#ait-loading-wrapper) 숨김 중 늦은 시각.
 *    TTFF 가 같아도 오버레이가 늦게 걷히면 사용자는 그만큼 늦게 게임을 본다.
 *  - 재방문(warm): cold 측정 뒤 캐시 저장을 기다렸다가 같은 컨텍스트에서 한 번 더 연다.
 *    TTFF 와 data+wasm 전송 바이트(캐시가 서빙하면 0)를 기록한다.
 *  - median-of-N: 매 반복마다 새 BrowserContext(콜드 캐시)로 측정, 중앙값 기록.
 *
 * **기록 전용(record-only).** self-hosted 러너 부하 변동으로 하드 임계 게이트는 flaky하므로
 * 임계 단언을 두지 않는다. 회귀 게이트는 안정 baseline 확보 후 후속 작업으로 분리.
 * (단, "측정 자체가 실패"한 경우 — 페이지 미로드/draw 미검출 — 는 명시적으로 실패시켜
 * 깨진 빌드를 false-green으로 통과시키지 않는다.)
 *
 * 환경변수:
 *  - UNITY_PROJECT_PATH : 빌드 산출물(ait-build/dist/web 포함) 경로. 미지정 시 HeavySampleUnityProject-* 자동탐지.
 *  - PERF_UNITY_VERSION : 결과 JSON에 기록할 버전(미지정 시 경로에서 추출).
 *  - PERF_ITERATIONS    : 반복 횟수(기본 5).
 *  - PERF_CPU_THROTTLE  : CPU 스로틀 배율(기본 4).
 *  - PERF_NET_DOWN_MBPS : 다운로드 Mbps(기본 100). 0이면 네트워크 스로틀 비활성.
 *  - PERF_NET_UP_MBPS   : 업로드 Mbps(기본 50).
 *  - PERF_NET_RTT_MS    : RTT ms(기본 50).
 *  - PERF_WARM          : 0이면 재방문(warm) 측정 생략(기본 1).
 *  - PERF_PAIR_PROJECT_PATH : (페어 A/B 모드) B 산출물의 프로젝트 경로. 미지정 시 기존 단일 측정과 동일.
 *  - PERF_LABEL_A / PERF_LABEL_B : 페어 모드에서 결과 JSON pairing 섹션에 남길 라벨(기본 'A'/'B').
 *
 * 페어 A/B 모드(PERF_PAIR_PROJECT_PATH 지정 시): 같은 measure 잡 안에서 A/B 두 산출물을 각자
 * 별도 preview 서버로 띄우고 반복마다 A→B/B→A 를 교대하며 인터리브 측정한다(러너 개체 편차 상쇄).
 * 집계는 "중앙값의 차"가 아니라 "반복별 차의 중앙값"(paired delta) — 잡 내부 드리프트까지 상쇄된다.
 * 단일 모드 결과 JSON(perf-results-<version>.json)의 스키마는 기존과 완전히 동일하게 유지되고,
 * B 쪽 결과와 pairing 메타데이터는 페어 모드에서만 별도 파일(perf-results-<version>-pair.json)로 기록된다.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// ---- 설정값 (env override) ----
const ITERATIONS = Math.max(1, parseInt(process.env.PERF_ITERATIONS || '5', 10));
const CPU_THROTTLE = Math.max(0, parseInt(process.env.PERF_CPU_THROTTLE || '4', 10));
const NET_DOWN_MBPS = parseFloat(process.env.PERF_NET_DOWN_MBPS || '100');
const NET_UP_MBPS = parseFloat(process.env.PERF_NET_UP_MBPS || '50');
const NET_RTT_MS = parseFloat(process.env.PERF_NET_RTT_MS || '50');
const MEASURE_WARM = process.env.PERF_WARM !== '0';

// ---- 페어 A/B 모드 (미지정 시 기존 단일 측정과 완전 동일) ----
const PAIR_PROJECT = process.env.PERF_PAIR_PROJECT_PATH || '';
const PAIR_MODE = !!PAIR_PROJECT;
const LABEL_A = process.env.PERF_LABEL_A || 'A';
const LABEL_B = process.env.PERF_LABEL_B || 'B';

// ---- 프로젝트 경로 탐지 (Heavy 우선, UNITY_PROJECT_PATH 존중) ----
function findHeavyProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) return envPath;

  const versionPatterns = ['6000.3', '6000.0', '6000.2', '2022.3', '2021.3'];
  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../HeavySampleUnityProject-${version}`);
    if (fs.existsSync(path.resolve(projectPath, 'ait-build/dist/web'))) {
      console.log(`📁 Auto-detected heavy project: HeavySampleUnityProject-${version}`);
      return projectPath;
    }
  }
  // 폴백: 일반 샘플(무거운 빌드가 없을 때 로컬 스모크용)
  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../SampleUnityProject-${version}`);
    if (fs.existsSync(path.resolve(projectPath, 'ait-build/dist/web'))) {
      console.log(`📁 Fallback to SampleUnityProject-${version} (no heavy build found)`);
      return projectPath;
    }
  }
  return path.resolve(__dirname, '../HeavySampleUnityProject-6000.3');
}

const PROJECT = findHeavyProject();
const AIT_BUILD = path.resolve(PROJECT, 'ait-build');
const DIST_WEB = path.resolve(AIT_BUILD, 'dist/web');

// ---- 페어(B) 산출물 경로 (PAIR_MODE 일 때만 사용) ----
const PAIR_AIT_BUILD = PAIR_MODE ? path.resolve(PAIR_PROJECT, 'ait-build') : null;
const PAIR_DIST_WEB = PAIR_MODE ? path.resolve(PAIR_AIT_BUILD, 'dist/web') : null;

function detectVersion(projectPath) {
  if (process.env.PERF_UNITY_VERSION) return process.env.PERF_UNITY_VERSION;
  const m = projectPath.match(/(?:Heavy)?SampleUnityProject-(\d+\.\d+)/);
  return m ? m[1] : 'unknown';
}
const UNITY_VERSION = detectVersion(PROJECT);

// Unity 버전별 포트 오프셋 (E2EBuildRunner.GetPortOffsetForUnityVersion 와 동일 규약)
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
const PORT_OFFSET = portOffset(UNITY_VERSION);
// e2e-full-pipeline 의 production 서버 포트 규약(4173 + offset)과 분리: perf는 +50 오프셋으로 충돌 회피
let serverPort = 4223 + PORT_OFFSET;
// 페어(B) 전용 포트 대역: full-pipeline(4173+)/perf A(4223+)/ce-serving(4323+)와 충돌하지 않는 별도 대역
let pairPort = 4273 + PORT_OFFSET;

console.log(`📦 Heavy project: ${PROJECT}`);
console.log(`🏷️  Unity version: ${UNITY_VERSION}`);
console.log(`🔌 Perf server port: ${serverPort}`);
console.log(`🎚️  Throttle: CPU ${CPU_THROTTLE}×, net ${NET_DOWN_MBPS}/${NET_UP_MBPS} Mbps, RTT ${NET_RTT_MS}ms, iters=${ITERATIONS}`);
// 호스티드 러너는 같은 이미지라도 CPU 가 달라 run 간 TTFF 가 1초 가까이 벌어진다. 비교할 때 기준으로 남긴다.
console.log(`🖥️  Runner CPU: ${os.cpus()[0]?.model ?? 'unknown'} × ${os.cpus().length}`);
if (PAIR_MODE) {
  console.log(`🔀 Pair mode: A[${LABEL_A}]=${PROJECT} vs B[${LABEL_B}]=${PAIR_PROJECT}`);
  console.log(`🔌 Pair(B) server port: ${pairPort}`);
}

// ---- TTFF 후킹 스크립트 (페이지 스크립트보다 먼저 실행) ----
const TTFF_INIT_SCRIPT = `
(function () {
  if (window.__ttffHooked) return;
  window.__ttffHooked = true;
  window.__TTFF__ = null;
  window.__firstDrawCount = 0;
  function mark(gl) {
    try {
      // 기본(화면) 프레임버퍼로의 draw만 첫 프레임으로 인정 (오프스크린 FBO 제외)
      var fb = gl.getParameter(gl.FRAMEBUFFER_BINDING);
      if (fb !== null) return;
    } catch (e) { /* 일부 컨텍스트는 getParameter 불가 — 보수적으로 인정 */ }
    window.__firstDrawCount++;
    if (window.__TTFF__ === null) window.__TTFF__ = performance.now();
  }
  var origGet = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function (type) {
    var ctx = origGet.apply(this, arguments);
    if (ctx && (type === 'webgl' || type === 'webgl2' || type === 'experimental-webgl')) {
      var methods = ['drawElements', 'drawArrays', 'drawElementsInstanced', 'drawArraysInstanced', 'drawRangeElements'];
      for (var i = 0; i < methods.length; i++) {
        (function (m) {
          if (typeof ctx[m] === 'function' && !ctx['__w_' + m]) {
            var orig = ctx[m];
            ctx[m] = function () { mark(ctx); return orig.apply(ctx, arguments); };
            ctx['__w_' + m] = true;
          }
        })(methods[i]);
      }
    }
    return ctx;
  };
  // 로딩 오버레이가 사라진 시각. 첫 draw 뒤에도 오버레이가 덮고 있으면 사용자는 게임 화면을 보지 못한다.
  window.__overlayHidden__ = null;
  function watchOverlay() {
    var el = document.querySelector('#ait-loading-wrapper');
    if (!el) return;
    var check = function () {
      if (window.__overlayHidden__ === null && el.style.display === 'none') window.__overlayHidden__ = performance.now();
    };
    new MutationObserver(check).observe(el, { attributes: true, attributeFilter: ['style'] });
    check();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watchOverlay);
  else watchOverlay();
})();
`;

// ============================================================================
// 유틸리티 (e2e-full-pipeline.test.js 패턴 재사용)
// ============================================================================
function directoryExists(p) {
  try { return fs.existsSync(p) && fs.statSync(p).isDirectory(); } catch { return false; }
}
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
    await new Promise(r => setTimeout(r, 200));
  }
  return false;
}
function freePort(port) {
  const isWindows = process.platform === 'win32';
  try {
    if (isWindows) {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      // LISTEN 소켓만 kill: ESTABLISHED 소켓을 가진 Chrome 프로세스까지 kill되는 것을 막는다
      execSync(`lsof -ti tcp:${port} -sTCP:LISTEN | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {}
}
async function startProductionServer(aitBuildDir, defaultPort) {
  freePort(defaultPort);
  await waitForPortRelease(defaultPort, 5000);
  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(defaultPort)], {
      cwd: aitBuildDir, stdio: 'pipe', shell: true, env: { ...process.env, NODE_OPTIONS: '' }
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
function median(values) {
  if (!values.length) return null;
  const s = [...values].sort((a, b) => a - b);
  const mid = Math.floor(s.length / 2);
  return s.length % 2 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
}

/** 새 페이지를 열고 CDP 스로틀을 건다(네비게이션 이전). 스로틀은 페이지 세션 단위라 페이지마다 다시 건다. */
async function openThrottledPage(context) {
  const page = await context.newPage();
  const client = await context.newCDPSession(page);
  if (CPU_THROTTLE > 0) {
    await client.send('Emulation.setCPUThrottlingRate', { rate: CPU_THROTTLE });
  }
  if (NET_DOWN_MBPS > 0) {
    await client.send('Network.enable');
    await client.send('Network.emulateNetworkConditions', {
      offline: false,
      downloadThroughput: (NET_DOWN_MBPS * 1024 * 1024) / 8,
      uploadThroughput: (NET_UP_MBPS * 1024 * 1024) / 8,
      latency: NET_RTT_MS,
    });
  }
  return page;
}

/** 한 페이지 로드를 재서 지표를 돌려준다(cold/warm 공용). */
async function measureLoad(page, url) {
  let unityReady = null;
  const resp = await page.goto(url, { waitUntil: 'commit', timeout: 120000 });
  expect(resp?.status(), 'navigation should return 200').toBe(200);

  // 첫 화면 draw 대기 (= TTFF). 미검출 시 unityInstance ready로 폴백.
  const gotDraw = await page.waitForFunction(
    () => window['__TTFF__'] !== null,
    undefined, // waitForFunction 의 두 번째 인자는 pageFunction 에 넘길 arg 다. timeout 은 세 번째(options)에 둔다.
    { timeout: 480000 }
  ).then(() => true).catch(() => false);

  if (!gotDraw) {
    // 폴백: Unity 인스턴스 준비라도 확인 (측정은 무효 처리하되 진단 기록)
    await page.waitForFunction(() => window['unityInstance'] !== undefined, undefined, { timeout: 60000 })
      .then(() => { unityReady = true; })
      .catch(() => { unityReady = false; });
  }

  // 전송 바이트는 첫 draw 직후에 읽는다 — 뒤로 미루면 첫 프레임 뒤에 시작되는 다운로드가 섞인다.
  const metrics = await page.evaluate(() => {
    const navEntry = performance.getEntriesByType('navigation')[0] || {};
    const res = performance.getEntriesByType('resource');
    let wasm = 0, data = 0, total = 0, framework = 0;
    for (const e of res) {
      const ts = e.transferSize || 0;
      total += ts;
      if (/\.wasm(\b|\.|\?|$)/.test(e.name)) wasm += ts;
      else if (/\.data(\b|\.|\?|$)/.test(e.name)) data += ts;
      else if (/\.framework\.js|loader\.js|\.js(\.|\b|\?|$)/.test(e.name)) framework += ts;
    }
    return {
      ttff: window['__TTFF__'],
      firstDrawCount: window['__firstDrawCount'] || 0,
      domContentLoaded: navEntry.domContentLoadedEventEnd || null,
      loadEvent: navEntry.loadEventEnd || null,
      onWire: { wasm, data, framework, total },
    };
  });

  // 게임 화면이 실제로 보이기 시작한 시각: 첫 draw 와 오버레이 숨김 중 늦은 쪽.
  // 오버레이가 걷힐 때까지 기다린다(템플릿 상한은 인스턴스 준비 + 500ms). 오버레이가 없는 빌드는 null 로 남는다.
  let overlayHidden = null;
  if (gotDraw) {
    await page.waitForFunction(() => window['__overlayHidden__'] !== null, undefined, { timeout: 5000 }).catch(() => {});
    overlayHidden = await page.evaluate(() => window['__overlayHidden__']);
  }
  const firstVisible = (typeof metrics.ttff === 'number' && typeof overlayHidden === 'number')
    ? Math.max(metrics.ttff, overlayHidden)
    : null;
  return { ...metrics, firstVisible, unityReady };
}

/**
 * 재방문 측정 전에 페이지 캐시 저장이 끝나기를 기다린다. 캐시가 꺼진 빌드는 기다릴 신호가 없으므로
 * 상한까지만 기다리고 그대로 잰다(그 경우 warm 값은 네트워크 재다운로드를 포함한다).
 */
async function waitForCachePuts(page) {
  // 기대 put 수: data + wasm. Chromium 에서는 wasm 이 HTTP 캐시로 빠져(__aitWasmViaHttpCache) 페이지 캐시에 put 되지 않으므로 1이다.
  // timeout 은 반드시 세 번째 인자(options)로 넘긴다. 두 번째(arg)에 두면 상한 없이 영구 대기해
  // put 이 기대 수에 못 미치는 빌드에서 재방문 측정이 120s 상한까지 멈춘다(실측 회귀).
  // 상한은 느린 러너에서도 put 이 끝날 만큼 넉넉해야 한다. put 은 디코드된 본문(wasm ~37MB, data ~30MB)을 렌더러 메인 스레드가
  // 읽어 쓰는 작업이라 CPU 스로틀 아래서는 TTFF 뒤로 수 초~수십 초 밀린다(6000.0 은 wasm 이 커서 15s 상한을 넘겨 재방문마다
  // 통째로 다시 받았다). 상한 안에 못 끝나면 warm 값이 네트워크 재다운로드를 포함하므로 경고를 남긴다.
  const done = await page.waitForFunction(() => {
    const s = window['__aitCacheStats'];
    const need = window['__aitWasmViaHttpCache'] ? 1 : 2;
    return !!s && Array.isArray(s.puts) && s.puts.length >= need;
  }, undefined, { timeout: CACHE_PUT_WAIT_MS }).then(() => true).catch(() => false);
  if (!done) console.warn(`  페이지 캐시 put 이 ${CACHE_PUT_WAIT_MS}ms 안에 끝나지 않음 — 재방문 값에 재다운로드가 섞일 수 있다`);
  await page.waitForTimeout(500);
}

/** 재방문 측정 전에 페이지 캐시 put 완료를 기다리는 상한. WARM_DEADLINE_MS 안에서 재방문 로드 자체가 들어갈 여유를 남긴다. */
const CACHE_PUT_WAIT_MS = 75000;

/** 재방문 측정 전체와 컨텍스트 정리의 벽시계 상한. 렌더러가 멈춘 러너에서 테스트 timeout 을 다 먹지 않게 한다. */
const WARM_DEADLINE_MS = 120000;
const CLOSE_DEADLINE_MS = 30000;

// 테스트 timeout 과 집계·서버 정리에 남겨 둘 여유. 느린 러너에서는 반복·재방문 측정을 줄여 timeout 전에 집계한다.
const TEST_TIMEOUT_MS = PAIR_MODE ? 1190000 : 590000;
const SUMMARY_RESERVE_MS = 60000;
let testDeadlineAt = Infinity;

function withDeadline(promise, ms, what) {
  let timer;
  const deadline = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`${what}: ${ms}ms 상한 초과`)), ms);
  });
  return Promise.race([promise, deadline]).finally(() => clearTimeout(timer));
}

/**
 * 반복 1회 측정 (콜드 캐시 새 BrowserContext + 동일 CDP 스로틀). A/B 공용 — 페어 모드에서는
 * 이 함수를 A/B 각각의 url 로 호출해 같은 로직으로 공정하게 잰다.
 * PERF_WARM 이 켜져 있으면 같은 컨텍스트에서 한 번 더 열어 재방문(warm) 값도 잰다.
 */
async function measureIteration(browser, url, iter, label) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await context.addInitScript(TTFF_INIT_SCRIPT);
  const page = await openThrottledPage(context);

  const navStart = Date.now();
  try {
    const metrics = await measureLoad(page, url);
    const ttff = metrics.ttff;

    const sample = {
      iteration: iter,
      ttffMs: ttff,
      firstVisibleMs: metrics.firstVisible,
      unityReady: metrics.unityReady,
      firstDrawCount: metrics.firstDrawCount,
      domContentLoadedMs: metrics.domContentLoaded,
      loadEventMs: metrics.loadEvent,
      onWireBytes: metrics.onWire,
      wallClockMs: Date.now() - navStart,
    };
    // 페어 모드 전용 필드 — 단일 모드 결과 JSON 스키마는 기존과 완전히 동일해야 하므로 반드시 가드 안에서만 추가.
    if (PAIR_MODE) sample.label = label;

    let warmNote = '';
    const warmFits = Date.now() + WARM_DEADLINE_MS + CLOSE_DEADLINE_MS < testDeadlineAt;
    if (MEASURE_WARM && typeof ttff === 'number' && !warmFits) {
      console.warn(`  warm 측정 생략(iter ${iter + 1}): 테스트 시간 예산 부족`);
    }
    if (MEASURE_WARM && typeof ttff === 'number' && warmFits) {
      try {
        const warm = await withDeadline((async () => {
          await waitForCachePuts(page);
          await page.close();
          const warmPage = await openThrottledPage(context);
          return measureLoad(warmPage, url);
        })(), WARM_DEADLINE_MS, 'warm 측정');
        sample.warm = {
          ttffMs: warm.ttff,
          firstVisibleMs: warm.firstVisible,
          onWireBytes: warm.onWire,
        };
        warmNote = ` warm=${typeof warm.ttff === 'number' ? warm.ttff.toFixed(0) + 'ms' : 'N/A'}` +
          `(${((warm.onWire.wasm + warm.onWire.data) / 1048576).toFixed(2)}MB)`;
      } catch (e) {
        // warm 측정 실패는 cold 결과를 무효로 만들지 않는다.
        console.warn(`  warm 측정 실패(iter ${iter + 1}): ${e && e.message}`);
      }
    }

    console.log(`  iter ${iter + 1}/${ITERATIONS}${PAIR_MODE ? ` [${label}]` : ''}: TTFF=${ttff !== null ? ttff.toFixed(0) + 'ms' : 'N/A'} ` +
      `visible=${metrics.firstVisible !== null ? metrics.firstVisible.toFixed(0) + 'ms' : 'N/A'} ` +
      `wasm=${(metrics.onWire.wasm / 1048576).toFixed(2)}MB data=${(metrics.onWire.data / 1048576).toFixed(2)}MB${warmNote}`);

    return sample;
  } finally {
    await withDeadline(context.close(), CLOSE_DEADLINE_MS, 'context.close')
      .catch(e => console.warn(`  컨텍스트 정리 실패(iter ${iter + 1}): ${e.message}`));
  }
}

/** 반복 샘플들을 집계해 결과 JSON 객체를 만든다(단일/페어 A/B 공용, pairing 필드는 호출부에서 추가). */
function summarize(samples, projectPath) {
  const ttffValues = samples.map(s => s.ttffMs).filter(v => typeof v === 'number' && v > 0);
  const wasmValues = samples.map(s => s.onWireBytes?.wasm).filter(v => typeof v === 'number' && v > 0);
  const dataValues = samples.map(s => s.onWireBytes?.data).filter(v => typeof v === 'number' && v > 0);
  const totalValues = samples.map(s => s.onWireBytes?.total).filter(v => typeof v === 'number' && v > 0);
  const visibleValues = samples.map(s => s.firstVisibleMs).filter(v => typeof v === 'number' && v > 0);
  const warmTtffValues = samples.map(s => s.warm?.ttffMs).filter(v => typeof v === 'number' && v > 0);
  const warmVisibleValues = samples.map(s => s.warm?.firstVisibleMs).filter(v => typeof v === 'number' && v > 0);
  // warm 전송량은 0 이 정상값(캐시 서빙)이라 0 을 걸러내지 않는다.
  const warmCodeDataValues = samples
    .filter(s => s.warm?.onWireBytes)
    .map(s => (s.warm.onWireBytes.wasm || 0) + (s.warm.onWireBytes.data || 0));

  return {
    schemaVersion: 1,
    unityVersion: UNITY_VERSION,
    buildType: process.env.AIT_DEVELOPMENT_BUILD === 'true' ? 'development' : 'release',
    compressionFormat: process.env.AIT_COMPRESSION_FORMAT ?? null,
    posture: process.env.AIT_PERF_POSTURE ?? null,
    project: projectPath,
    timestamp: new Date().toISOString(),
    throttle: {
      cpuRate: CPU_THROTTLE,
      netDownMbps: NET_DOWN_MBPS,
      netUpMbps: NET_UP_MBPS,
      rttMs: NET_RTT_MS,
    },
    iterations: ITERATIONS,
    ttffMs: { median: median(ttffValues), values: ttffValues },
    // 아래 세 항목은 추가 지표다(없던 시절의 baseline JSON 에는 없다 — 소비 측은 부재를 허용해야 한다).
    firstVisibleMs: { median: median(visibleValues), values: visibleValues },
    warmTtffMs: { median: median(warmTtffValues), values: warmTtffValues },
    warmFirstVisibleMs: { median: median(warmVisibleValues), values: warmVisibleValues },
    warmCodeDataBytes: { median: median(warmCodeDataValues), values: warmCodeDataValues },
    onWireBytes: {
      wasm: { median: median(wasmValues) },
      data: { median: median(dataValues) },
      total: { median: median(totalValues) },
    },
    samples,
  };
}

// ============================================================================
// Perf 측정 테스트
// ============================================================================
let serverProcess = null;
let pairServerProcess = null;

test.afterAll(async () => {
  await killServer(serverProcess, serverPort);
  serverProcess = null;
  await killServer(pairServerProcess, pairPort);
  pairServerProcess = null;
});

test('TTFF 실측 (median-of-N, record-only)', async ({ browser }) => {
  // 무거운 빌드 + 스로틀 + N회 반복이므로 넉넉한 타임아웃 (config 600s와 정합).
  // 페어 모드는 한 테스트가 A/B 두 산출물을 인터리브로 재므로 시간 예산 2배(config 1200s와 정합).
  test.setTimeout(TEST_TIMEOUT_MS);
  const testStart = Date.now();
  testDeadlineAt = testStart + TEST_TIMEOUT_MS - SUMMARY_RESERVE_MS;

  expect(directoryExists(DIST_WEB), `dist/web/ should exist for perf measurement: ${DIST_WEB}`).toBe(true);
  if (PAIR_MODE) {
    expect(directoryExists(PAIR_DIST_WEB), `페어(B) 산출물이 필요합니다: ${PAIR_DIST_WEB}`).toBe(true);
  }

  // 두 서버 모두 측정 시작 전에 기동 + HEAD 워밍 → OS 페이지 캐시/서버 웜업 상태를 대칭화.
  const prod = await startProductionServer(AIT_BUILD, serverPort);
  serverProcess = prod.process;
  const port = prod.port;

  let prodPair = null;
  if (PAIR_MODE) {
    prodPair = await startProductionServer(PAIR_AIT_BUILD, pairPort);
    pairServerProcess = prodPair.process;
  }

  async function waitServerReady(p) {
    for (let i = 0; i < 30; i++) {
      try {
        const res = await fetch(`http://localhost:${p}/`, { method: 'HEAD' });
        if (res.ok) return true;
      } catch {}
      await new Promise(r => setTimeout(r, 500));
    }
    return false;
  }

  const ready = await waitServerReady(port);
  expect(ready, `preview server should be reachable on :${port}`).toBe(true);
  if (PAIR_MODE) {
    const readyPair = await waitServerReady(prodPair.port);
    expect(readyPair, `pair(B) preview server should be reachable on :${prodPair.port}`).toBe(true);
  }

  const url = `http://localhost:${port}?e2e=true`;
  const urlPair = PAIR_MODE ? `http://localhost:${prodPair.port}?e2e=true` : null;
  const samplesA = [];
  const samplesB = [];
  const order = [];

  let slowestIterMs = 0;
  for (let iter = 0; iter < ITERATIONS; iter++) {
    // 직전까지 가장 느렸던 반복이 남은 예산에 들어가지 않으면 여기서 멈추고 모은 샘플로 집계한다.
    if (iter > 0 && Date.now() + slowestIterMs > testDeadlineAt) {
      console.warn(`  시간 예산 부족: ${iter}/${ITERATIONS}회에서 측정 중단(가장 느린 반복 ${(slowestIterMs / 1000).toFixed(0)}s)`);
      break;
    }
    const iterStart = Date.now();
    if (!PAIR_MODE) {
      samplesA.push(await measureIteration(browser, url, iter, LABEL_A));
      slowestIterMs = Math.max(slowestIterMs, Date.now() - iterStart);
      continue;
    }
    // 순서 편향(브라우저/디스크 웜업) 상쇄: 반복마다 A→B / B→A 교대(짝수 i는 A 먼저)
    const aFirst = iter % 2 === 0;
    order.push(aFirst ? 'AB' : 'BA');
    if (aFirst) {
      samplesA.push(await measureIteration(browser, url, iter, LABEL_A));
      samplesB.push(await measureIteration(browser, urlPair, iter, LABEL_B));
    } else {
      samplesB.push(await measureIteration(browser, urlPair, iter, LABEL_B));
      samplesA.push(await measureIteration(browser, url, iter, LABEL_A));
    }
    slowestIterMs = Math.max(slowestIterMs, Date.now() - iterStart);
  }

  // 집계 — A(이번 실행/기존 단일 측정과 동일한 결과 JSON)
  const resultA = summarize(samplesA, PROJECT);

  if (PAIR_MODE) {
    const resultB = summarize(samplesB, PAIR_PROJECT);

    // 핵심: "중앙값의 차"가 아니라 "반복별 차의 중앙값"(paired delta) — 잡 내부 드리프트까지 상쇄한다.
    const dTtff = samplesA
      .map((a, i) => {
        const b = samplesB[i];
        return (typeof a.ttffMs === 'number' && typeof b?.ttffMs === 'number') ? b.ttffMs - a.ttffMs : null;
      })
      .filter((v) => v !== null);

    const wasmA = resultA.onWireBytes.wasm.median;
    const wasmB = resultB.onWireBytes.wasm.median;
    const totalA = resultA.onWireBytes.total.median;
    const totalB = resultB.onWireBytes.total.median;

    const pairing = {
      labelA: LABEL_A,
      labelB: LABEL_B,
      order,
      deltaTtffMs: dTtff.length
        ? {
            median: median(dTtff),
            min: Math.min(...dTtff),
            max: Math.max(...dTtff),
            values: dTtff,
            negativeCount: dTtff.filter((v) => v < 0).length,
          }
        : { median: null, min: null, max: null, values: [], negativeCount: 0 },
      // on-wire 바이트 Δ는 결정론적이라 페어 모드의 plumbing 정합성 체크섬 역할을 한다(A/A 널 테스트는 0바이트여야 함).
      deltaWasmBytes: (wasmA != null && wasmB != null) ? wasmB - wasmA : null,
      deltaTotalBytes: (totalA != null && totalB != null) ? totalB - totalA : null,
      peerProject: PAIR_PROJECT,
    };
    resultA.pairing = { role: 'A', ...pairing };
    resultB.pairing = { role: 'B', ...pairing };

    const outNameB = `perf-results-${UNITY_VERSION}-pair.json`;
    const outPathB = path.resolve(__dirname, outNameB);
    fs.writeFileSync(outPathB, JSON.stringify(resultB, null, 2));

    console.log('\n' + '━'.repeat(72));
    console.log(`🔀 페어 Δ  B[${LABEL_B}] − A[${LABEL_A}]  (반복별 차의 중앙값)`);
    console.log('━'.repeat(72));
    console.log(`  ΔTTFF median: ${pairing.deltaTtffMs.median !== null ? pairing.deltaTtffMs.median.toFixed(0) + ' ms' : 'N/A'} ` +
      `(min=${pairing.deltaTtffMs.min ?? 'N/A'}, max=${pairing.deltaTtffMs.max ?? 'N/A'}, 부호일치(음수)=${pairing.deltaTtffMs.negativeCount}/${dTtff.length})`);
    console.log(`  Δwasm total:  ${pairing.deltaWasmBytes != null ? (pairing.deltaWasmBytes / 1048576).toFixed(3) + ' MB' : 'N/A'}`);
    console.log(`  order: ${order.join(' ')}`);
    console.log(`  → ${outPathB}`);
    console.log('━'.repeat(72) + '\n');
  }

  const outName = `perf-results-${UNITY_VERSION}.json`;
  const outPath = path.resolve(__dirname, outName);
  fs.writeFileSync(outPath, JSON.stringify(resultA, null, 2));

  console.log('\n' + '━'.repeat(72));
  console.log(`📊 Perf TTFF — Unity ${UNITY_VERSION} (${resultA.buildType}, posture=${resultA.posture ?? 'default'})`);
  console.log('━'.repeat(72));
  console.log(`  TTFF median:      ${resultA.ttffMs.median !== null ? resultA.ttffMs.median.toFixed(0) + ' ms' : 'N/A'} ` +
    `(${resultA.ttffMs.values.length}/${ITERATIONS} valid)`);
  console.log(`  화면 노출 median: ${resultA.firstVisibleMs.median !== null ? resultA.firstVisibleMs.median.toFixed(0) + ' ms' : 'N/A'}`);
  console.log(`  재방문 TTFF:      ${resultA.warmTtffMs.median !== null ? resultA.warmTtffMs.median.toFixed(0) + ' ms' : 'N/A'} ` +
    `(data+wasm 전송 ${resultA.warmCodeDataBytes.median !== null ? (resultA.warmCodeDataBytes.median / 1048576).toFixed(2) + ' MB' : 'N/A'})`);
  console.log(`  on-wire wasm:     ${resultA.onWireBytes.wasm.median ? (resultA.onWireBytes.wasm.median / 1048576).toFixed(2) + ' MB' : 'N/A'}`);
  console.log(`  on-wire data:     ${resultA.onWireBytes.data.median ? (resultA.onWireBytes.data.median / 1048576).toFixed(2) + ' MB' : 'N/A'}`);
  console.log(`  on-wire total:    ${resultA.onWireBytes.total.median ? (resultA.onWireBytes.total.median / 1048576).toFixed(2) + ' MB' : 'N/A'}`);
  console.log(`  → ${outPath}`);
  console.log('━'.repeat(72) + '\n');

  // 측정 자체 실패만 가드(임계 게이트 아님): 최소 1회 유효 TTFF가 잡혀야 함(페어 모드는 A/B 모두).
  expect(resultA.ttffMs.values.length,
    `at least one valid TTFF sample required for A (got ${resultA.ttffMs.values.length}/${ITERATIONS}). ` +
    `Draw counts: ${samplesA.map(s => s.firstDrawCount).join(',')}`).toBeGreaterThan(0);
  if (PAIR_MODE) {
    const bTtffValues = samplesB.map(s => s.ttffMs).filter(v => typeof v === 'number' && v > 0);
    expect(bTtffValues.length,
      `at least one valid TTFF sample required for B (got ${bTtffValues.length}/${ITERATIONS}). ` +
      `Draw counts: ${samplesB.map(s => s.firstDrawCount).join(',')}`).toBeGreaterThan(0);
  }
});
