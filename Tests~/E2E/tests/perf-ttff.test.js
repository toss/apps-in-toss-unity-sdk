// @ts-check
import { test, expect } from '@playwright/test';
import { execFileSync, execSync, spawn } from 'child_process';
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
 *  - PERF_NO_GESTURE    : 1이면 첫 프레임 뒤 캔버스 클릭(사용자 제스처 1회)을 생략한다(기본 0 = 클릭). 클릭은 TTFF·전송량을 확정한 뒤에 보내므로
 *                         지표에는 영향이 없고, AudioContext 가 시작돼 오디오 디코드/미디어 요소 경로가 실제로 도는 상태를 재현한다.
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
const SEND_GESTURE = process.env.PERF_NO_GESTURE !== '1';

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

/**
 * 산출물의 압축 포맷을 Build/ 파일 확장자로 판정한다(brotli | gzip | none | unknown).
 * 페어 A/B 는 두 산출물의 압축이 같아야 한다 — 압축이 다르면 on-wire 바이트(gzip 은 brotli 보다 data+wasm 이 ~10MB 크다)와
 * 디코드 비용 차이가 ΔTTFF 에 그대로 섞인다(run 37224587514: A=brotli all0 vs B=gzip 기본 → +631ms, 같은 압축으로 다시 재면 ~0).
 */
function detectCompression(distWeb) {
  try {
    const files = fs.readdirSync(path.resolve(distWeb, 'Build')).filter((f) => /\.(data|wasm)(\.|$)/.test(f));
    if (!files.length) return 'unknown';
    const kinds = new Set(files.map((f) => (/\.br$/.test(f) ? 'brotli' : /\.gz$/.test(f) ? 'gzip' : /\.unityweb$/.test(f) ? 'unityweb' : 'none')));
    return kinds.size === 1 ? [...kinds][0] : 'mixed';
  } catch { return 'unknown'; }
}

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
  // 오디오 메모리 계측: decodeAudioData 로 풀린 PCM(float32) 바이트와 media element 경로(압축 유지) 수.
  window.__aitAudioPcmBytes = 0;
  window.__aitAudioMediaEls = 0;
  try {
    var AC = window.BaseAudioContext || window.AudioContext || window.webkitAudioContext;
    if (AC && AC.prototype.decodeAudioData) {
      var origDecode = AC.prototype.decodeAudioData;
      AC.prototype.decodeAudioData = function (data) {
        window.__aitAudioDecodeCalls = (window.__aitAudioDecodeCalls || 0) + 1;
        try { window.__aitAudioDecodeInBytes = (window.__aitAudioDecodeInBytes || 0) + (data && data.byteLength || 0); } catch (e) {}
        var r = origDecode.apply(this, arguments);
        if (r && typeof r.then === 'function') {
          r.then(function (b) { try { window.__aitAudioPcmBytes += b.length * b.numberOfChannels * 4; } catch (e) {} },
            function () { window.__aitAudioDecodeErrors = (window.__aitAudioDecodeErrors || 0) + 1; });
        }
        return r;
      };
    }
    if (AC && AC.prototype.createBuffer) {
      // _JS_Sound_Load_PCM 경로: wasm(FMOD)이 디코드한 PCM 을 AudioBuffer 로 복사한다(decodeAudioData 를 거치지 않음).
      var origCreateBuffer = AC.prototype.createBuffer;
      AC.prototype.createBuffer = function (channels, length) {
        try { window.__aitAudioPcmBytes += (channels | 0) * (length | 0) * 4; } catch (e) {}
        return origCreateBuffer.apply(this, arguments);
      };
    }
    // createMediaElementSource 는 BaseAudioContext 가 아니라 AudioContext(webkit 접두 포함) 프로토타입에 있다.
    // BaseAudioContext 만 보면 래퍼가 안 붙어 압축 재생(media element) 경로가 항상 0 으로 찍힌다.
    var mesSeen = [];
    [window.AudioContext, window.webkitAudioContext, AC].forEach(function (C) {
      try {
        var P = C && C.prototype;
        if (!P || mesSeen.indexOf(P) >= 0 || typeof P.createMediaElementSource !== 'function' || P.createMediaElementSource.__aitWrapped) return;
        mesSeen.push(P);
        var origMes = P.createMediaElementSource;
        var wrappedMes = function () { window.__aitAudioMediaEls++; return origMes.apply(this, arguments); };
        wrappedMes.__aitWrapped = true;
        P.createMediaElementSource = wrappedMes;
      } catch (e) {}
    });
  } catch (e) { /* 오디오 API 미지원 환경 — 계측만 생략 */ }
  // wasm 힙 크기: 오디오 로그 시점의 메모리 규모(압축 재생 전환의 효과 비교용). Unity 인스턴스 전역에 기대지 않고 Memory 생성자를 감싼다.
  window.__aitWasmMemories = [];
  try {
    if (window.WebAssembly && WebAssembly.Memory) {
      var OrigMemory = WebAssembly.Memory;
      var WrappedMemory = function (desc) {
        var m = new OrigMemory(desc);
        try { window.__aitWasmMemories.push(m); } catch (e) {}
        return m;
      };
      WrappedMemory.prototype = OrigMemory.prototype;
      WebAssembly.Memory = WrappedMemory;
      // 생성자를 거치지 않는 경로(모듈이 직접 만든 메모리를 export 로 받는 경우)는 grow 호출에서 잡는다.
      var origGrowH = OrigMemory.prototype.grow;
      if (typeof origGrowH === 'function') {
        OrigMemory.prototype.grow = function () {
          try { if (window.__aitWasmMemories.indexOf(this) < 0) window.__aitWasmMemories.push(this); } catch (e) {}
          return origGrowH.apply(this, arguments);
        };
      }
    }
  } catch (e) { /* 계측만 생략 */ }
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
// ---- 메모리 계측 (PAIR_MODE 기본 ON, PERF_MEMORY=1/0 으로 강제) ----
// 로드(첫 프레임) + MEM_SETTLE_MS 동안 JS 힙(CDP Runtime.getHeapUsage, 50ms 폴링)과 렌더러 프로세스 RSS 를 Node 쪽에서 잰다.
// RSS: Linux 는 /proc/<pid>/status 의 VmHWM(진짜 피크)·VmRSS, 그 외(mac)는 `ps -o rss` 폴링 최댓값으로 폴백한다.
// 페이지 쪽은 init script 로 1MB 이상 ArrayBuffer 할당만 센다(레버: exactDataBody 재포장이 데이터 크기 임시 버퍼를 없애는지).
const MEASURE_MEMORY = process.env.PERF_MEMORY ? process.env.PERF_MEMORY === '1' : PAIR_MODE;
const MEM_SETTLE_MS = 5000;
const MEM_HEAP_POLL_MS = 50;
const MEM_RSS_POLL_MS = 500;
const MEM_BIG_ALLOC_BYTES = 1048576;

const MEM_INIT_SCRIPT = `
(function () {
  if (window.__memHooked) return;
  window.__memHooked = true;
  var TH = ${MEM_BIG_ALLOC_BYTES};
  var st = window.__aitBigAllocs = { count: 0, totalBytes: 0, maxBytes: 0 };
  function rec(n) {
    if (typeof n === 'number' && n >= TH) { st.count++; st.totalBytes += n; if (n > st.maxBytes) st.maxBytes = n; }
  }
  function wrapCtor(name, sizeOf) {
    try {
      var T = window[name];
      if (typeof T !== 'function') return;
      window[name] = new Proxy(T, {
        construct: function (t, args, nt) {
          try { rec(sizeOf(args)); } catch (e) {}
          return Reflect.construct(t, args, nt === window[name] ? t : nt);
        }
      });
    } catch (e) {}
  }
  // ArrayBuffer/SharedArrayBuffer(len), TypedArray(len) — 숫자 인자 형태만(뷰 생성·복사 생성은 아래 별도 처리).
  function lenArg(a) { return typeof a[0] === 'number' ? a[0] : 0; }
  wrapCtor('ArrayBuffer', lenArg);
  wrapCtor('Uint8Array', lenArg);
  wrapCtor('Float32Array', function (a) { return lenArg(a) * 4; });
  // 다른 TypedArray 나 배열을 복사 생성하는 Uint8Array(typedArray)도 새 버퍼를 만든다.
  try {
    var U8 = window.Uint8Array;
    window.Uint8Array = new Proxy(U8, {
      construct: function (t, args, nt) {
        try {
          var a0 = args[0];
          if (typeof a0 === 'number') rec(a0);
          else if (a0 && typeof a0 === 'object' && !(a0 instanceof ArrayBuffer) && typeof a0.length === 'number') rec(a0.length);
        } catch (e) {}
        return Reflect.construct(t, args, nt === window.Uint8Array ? t : nt);
      }
    });
  } catch (e) {}
  // 네이티브가 만들어 돌려주는 버퍼: Response/Blob.arrayBuffer, ArrayBuffer/TypedArray.slice
  function wrapAsync(proto, m) {
    try {
      var o = proto && proto[m]; if (typeof o !== 'function') return;
      proto[m] = function () {
        var r = o.apply(this, arguments);
        if (r && typeof r.then === 'function') r.then(function (b) { try { rec(b && b.byteLength); } catch (e) {} }, function () {});
        return r;
      };
    } catch (e) {}
  }
  wrapAsync(window.Response && Response.prototype, 'arrayBuffer');
  wrapAsync(window.Blob && Blob.prototype, 'arrayBuffer');
  function wrapSync(proto, m) {
    try {
      var o = proto && proto[m]; if (typeof o !== 'function') return;
      proto[m] = function () {
        var r = o.apply(this, arguments);
        try { rec(r && r.byteLength); } catch (e) {}
        return r;
      };
    } catch (e) {}
  }
  wrapSync(ArrayBuffer.prototype, 'slice');
  try { wrapSync(Object.getPrototypeOf(Uint8Array.prototype), 'slice'); } catch (e) {}
})();
`;

let browserCdpPromise = null;
function getBrowserCdp(browser) {
  if (!browserCdpPromise) browserCdpPromise = browser.newBrowserCDPSession().catch(() => null);
  return browserCdpPromise;
}

/** 프로세스 RSS(바이트). Linux: /proc VmHWM/VmRSS, 그 외: ps(현재 RSS 만, 피크는 호출부가 폴링 최댓값으로 대체). */
function readProcRss(pid) {
  try {
    if (process.platform === 'linux') {
      const st = fs.readFileSync(`/proc/${pid}/status`, 'utf8');
      const kb = (k) => { const m = st.match(new RegExp(`^${k}:\\s+(\\d+)\\s*kB`, 'm')); return m ? Number(m[1]) * 1024 : null; };
      return { rss: kb('VmRSS'), hwm: kb('VmHWM'), source: '/proc VmHWM' };
    }
    const out = execFileSync('ps', ['-o', 'rss=', '-p', String(pid)], { encoding: 'utf8', timeout: 2000 }).trim();
    const kb = Number(out);
    return Number.isFinite(kb) && kb > 0 ? { rss: kb * 1024, hwm: null, source: 'ps 폴링 최댓값' } : null;
  } catch (e) { return null; }
}

/**
 * 반복 1회 동안의 메모리 샘플러. start() 는 navigation 전에, stop() 은 첫 프레임 + settle 뒤에 부른다.
 * 모든 읽기는 실패해도 측정을 깨지 않는다(null 로 남는다).
 */
async function listProcs(browser, types) {
  const browserCdp = await getBrowserCdp(browser);
  if (!browserCdp) return [];
  try {
    const info = await browserCdp.send('SystemInfo.getProcessInfo');
    // type 은 'renderer' 와 'GPU' 처럼 대소문자가 섞여 온다.
    return (info.processInfo || []).map((p) => ({ ...p, type: String(p.type).toLowerCase() })).filter((p) => types.includes(p.type));
  } catch (e) { return []; }
}
/** 컨텍스트·페이지를 만들기 전에 불러, 이번 반복의 렌더러만 가려낼 기준 집합을 만든다. */
async function captureRendererBaseline(browser) {
  return new Set((await listProcs(browser, ['renderer'])).map((p) => p.id));
}
async function startMemorySampler(browser, context, page, baseline) {
  const listRenderers = (types) => listProcs(browser, types);
  let pageCdp = null;
  try { pageCdp = await context.newCDPSession(page); } catch (e) { /* 힙 측정만 건너뛴다 */ }
  const s = { heapPeak: 0, heapTotalPeak: 0, heapFinal: null, heapSamples: 0, procs: new Map() };
  let stopped = false;

  const pollHeap = async () => {
    if (!pageCdp) return;
    try {
      const h = await pageCdp.send('Runtime.getHeapUsage');
      s.heapSamples++;
      s.heapFinal = h.usedSize;
      if (h.usedSize > s.heapPeak) s.heapPeak = h.usedSize;
      if (h.totalSize > s.heapTotalPeak) s.heapTotalPeak = h.totalSize;
    } catch (e) { /* 페이지 전환 중 등 */ }
  };
  const pollRss = async () => {
    for (const p of await listRenderers(['renderer', 'gpu'])) {
      // 렌더러는 이번 반복에서 새로 뜬 것만(이전 컨텍스트 잔재 제외). GPU 프로세스는 상주라 baseline 과 무관하게 본다.
      if (p.type === 'renderer' && baseline.has(p.id)) continue;
      const r = readProcRss(p.id);
      if (!r) continue;
      // 상주 GPU 프로세스의 VmHWM 은 이전 반복의 피크가 섞이므로 쓰지 않고 폴링 RSS 최댓값만 쓴다.
      if (p.type === 'gpu') r.hwm = null;
      const cur = s.procs.get(p.id) || { type: p.type, peak: 0, last: null, hwm: null, source: r.source };
      cur.last = r.rss;
      if (r.hwm != null) cur.hwm = r.hwm;
      if (r.rss != null && r.rss > cur.peak) cur.peak = r.rss;
      s.procs.set(p.id, cur);
    }
  };
  const loop = (fn, ms) => (async () => {
    while (!stopped) {
      const t0 = Date.now();
      await fn();
      const wait = ms - (Date.now() - t0);
      if (wait > 0 && !stopped) await new Promise((r) => setTimeout(r, wait));
    }
  })();
  const heapLoop = loop(pollHeap, MEM_HEAP_POLL_MS);
  const rssLoop = loop(pollRss, MEM_RSS_POLL_MS);

  return {
    async stop() {
      stopped = true;
      await Promise.all([heapLoop, rssLoop]);
      await pollHeap();
      await pollRss();
      const bigAllocs = await page.evaluate(() => window['__aitBigAllocs'] || null).catch(() => null);
      const pick = (type) => {
        let best = null;
        for (const v of s.procs.values()) {
          if (v.type !== type) continue;
          const peak = Math.max(v.hwm ?? 0, v.peak);
          if (!best || peak > best.peak) best = { peak, rss: v.last, source: v.hwm != null ? '/proc VmHWM' : (process.platform === 'linux' ? '/proc VmRSS 폴링 최댓값' : 'ps 폴링 최댓값') };
        }
        return best;
      };
      const rend = pick('renderer');
      const gpu = pick('gpu');
      return {
        jsHeapPeakBytes: s.heapSamples ? s.heapPeak : null,
        jsHeapTotalPeakBytes: s.heapSamples ? s.heapTotalPeak : null,
        jsHeapFinalBytes: s.heapSamples ? s.heapFinal : null,
        heapSamples: s.heapSamples,
        rendererPeakRssBytes: rend ? rend.peak : null,
        rendererRssBytes: rend ? rend.rss : null,
        gpuPeakRssBytes: gpu ? gpu.peak : null,
        rssSource: rend ? rend.source : null,
        bigAllocs: bigAllocs && typeof bigAllocs.count === 'number'
          ? { count: bigAllocs.count, totalBytes: bigAllocs.totalBytes, maxBytes: bigAllocs.maxBytes }
          : null,
      };
    },
  };
}

const MB = 1048576;
const fmtMB = (v) => (typeof v === 'number' ? (v / MB).toFixed(1) + 'MB' : 'N/A');

/** 샘플 배열에서 메모리 지표별 중앙값 객체를 만든다. */
function summarizeMemory(samples) {
  const med = (f) => median(samples.map((s) => f(s.memory)).filter((v) => typeof v === 'number'));
  return {
    rendererPeakRssBytes: med((m) => m?.rendererPeakRssBytes),
    rendererRssBytes: med((m) => m?.rendererRssBytes),
    gpuPeakRssBytes: med((m) => m?.gpuPeakRssBytes),
    jsHeapPeakBytes: med((m) => m?.jsHeapPeakBytes),
    jsHeapFinalBytes: med((m) => m?.jsHeapFinalBytes),
    bigAllocCount: med((m) => m?.bigAllocs?.count),
    bigAllocTotalBytes: med((m) => m?.bigAllocs?.totalBytes),
    bigAllocMaxBytes: med((m) => m?.bigAllocs?.maxBytes),
    rssSource: samples.map((s) => s.memory?.rssSource).find((v) => v) ?? null,
  };
}

function logMemory(label, mem) {
  console.log(`메모리 [${label}]: rendererPeakRSS=${fmtMB(mem.rendererPeakRssBytes)} rss=${fmtMB(mem.rendererRssBytes)} ` +
    `jsHeapPeak=${fmtMB(mem.jsHeapPeakBytes)} gpuPeakRSS=${fmtMB(mem.gpuPeakRssBytes)} ` +
    `bigAllocs=${mem.bigAllocCount ?? 'N/A'}(합 ${fmtMB(mem.bigAllocTotalBytes)}, 최대 ${fmtMB(mem.bigAllocMaxBytes)})` +
    (mem.rssSource ? ` [rss: ${mem.rssSource}]` : ''));
}

/** 반복별 B−A 차이를 모아 중앙값/부호 통계를 만든다. */
function deltaStat(samplesA, samplesB, pick) {
  const d = samplesA.map((a, i) => {
    const x = pick(a.memory), y = pick(samplesB[i]?.memory);
    return (typeof x === 'number' && typeof y === 'number') ? y - x : null;
  }).filter((v) => v !== null);
  return d.length
    ? { median: median(d), min: Math.min(...d), max: Math.max(...d), values: d, negativeCount: d.filter((v) => v < 0).length }
    : { median: null, min: null, max: null, values: [], negativeCount: 0 };
}
function logDelta(name, st) {
  console.log(`  Δ메모리 ${name} median: ${fmtMB(st.median)} (min=${fmtMB(st.min)}, max=${fmtMB(st.max)}, 부호일치(음수)=${st.negativeCount}/${st.values.length})`);
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

  // 사용자 제스처 1회(캔버스 중앙 클릭): TTFF·전송량·firstVisible 이 모두 확정된 뒤라 측정에는 영향이 없다. 브라우저 자동재생 정책 때문에
  // 제스처가 없으면 AudioContext 가 suspended 로 남아 외부화 오디오의 재수화/디코드 경로가 돌지 않는다(P0-1 진단 전제).
  if (gotDraw && SEND_GESTURE) {
    try {
      const box = await page.locator('#unity-canvas').boundingBox({ timeout: 2000 });
      const vp = page.viewportSize() || { width: 1280, height: 720 };
      const x = box ? box.x + box.width / 2 : vp.width / 2;
      const y = box ? box.y + box.height / 2 : vp.height / 2;
      await page.mouse.click(x, y);
    } catch (e) {
      console.warn(`  제스처 클릭 실패(무시): ${e && e.message}`);
    }
  }
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
  const waitStart = Date.now();
  const done = await page.waitForFunction(() => {
    const s = window['__aitCacheStats'];
    const need = window['__aitWasmViaHttpCache'] ? 1 : 2;
    if (!s || !Array.isArray(s.puts)) return false;
    // put 실패(QuotaExceededError 등)는 그 URL 에 대해 종결 상태다 — 기대 수를 못 채워도 75초를 더 기다리지 않는다.
    // 'put timeout' 은 느린 put 진단일 뿐 put 은 계속 진행 중이므로 종결로 보지 않는다(종결로 보면 put 완료 전에
    // 재방문을 열어 warm 이 통째로 재다운로드된다 — 6000.x 실측).
    const failed = Array.isArray(s.errors)
      ? s.errors.filter((e) => String(e).indexOf('put ') === 0 && String(e).indexOf('put timeout ') !== 0).length
      : 0;
    return s.puts.length + failed >= need;
  }, undefined, { timeout: CACHE_PUT_WAIT_MS }).then(() => true).catch(() => false);
  // 진단용 요약: 상한 안에 끝났어도 put 실패가 섞였으면 남긴다(그 URL 은 warm 에서 재다운로드된다).
  const summary = await page.evaluate(() => {
    const s = window['__aitCacheStats'];
    if (!s) return null;
    const n = (a) => (Array.isArray(a) ? a.length : -1);
    return { hits: n(s.hits), misses: n(s.misses), puts: n(s.puts), putUrls: s.puts, errors: s.errors, viaHttpCache: !!window['__aitWasmViaHttpCache'] };
  }).catch(() => null);
  const putErrors = summary && Array.isArray(summary.errors)
    ? summary.errors.filter((e) => String(e).indexOf('put ') === 0 && String(e).indexOf('put timeout ') !== 0)
    : [];
  const waited = Date.now() - waitStart;
  if (done && waited > 10000) console.log(`  페이지 캐시 put 완료까지 ${waited}ms 대기`);
  if (!done) console.warn(`  페이지 캐시 put 이 ${CACHE_PUT_WAIT_MS}ms 안에 끝나지 않음 — 재방문 값에 재다운로드가 섞일 수 있다 stats=${JSON.stringify(summary)}`);
  else if (putErrors.length) console.warn(`  페이지 캐시 put 실패 — 재방문 값에 재다운로드가 섞일 수 있다 stats=${JSON.stringify(summary)}`);
  await page.waitForTimeout(500);
}

/**
 * 오디오 메모리 진단: cold 방문에서 decodeAudioData 로 풀린 PCM 과 media element 경로 수를 남긴다.
 * 외부화 클립 재수화는 interactive 이후에 일어나므로 put 대기 뒤(첫 iter)에 읽는다.
 */
async function logAudioDecode(page, label, consoleLines) {
  const tag = PAIR_MODE ? ` [${label}]` : '';
  // BGM 프로브가 붙은 빌드면 외부화 클립 재수화(다운로드 + 디코드)가 끝날 때까지 상한 안에서 기다린다.
  const probed = consoleLines.some((l) => l.indexOf('[HeavyAudioProbe]') >= 0);
  const t0 = Date.now();
  // Unity 측 메모리 분해의 두 번째 줄(첫 프레임+30초)이 아직 안 나왔으면 같은 상한 안에서 함께 기다린다(재수화 대기와 겹쳐 돈다).
  const waitUnityMemLate = async () => {
    const has = (re) => consoleLines.some((l) => re.test(l));
    if (!has(/\[AIT-UnityMem\] t=first-frame(?!\+)/) || has(/\[AIT-UnityMem\] t=first-frame\+30s/)) return;
    const limit = Math.min(Date.now() + AUDIO_REHYDRATE_WAIT_MS, testDeadlineAt - SUMMARY_RESERVE_MS);
    while (Date.now() < limit && !has(/\[AIT-UnityMem\] t=first-frame\+30s/)) await page.waitForTimeout(500);
  };
  await Promise.all([
    probed
      ? page.waitForFunction(() => window.__aitAudioPcmBytes > 1048576 || window.__aitAudioMediaEls > 0,
        undefined, { timeout: AUDIO_REHYDRATE_WAIT_MS }).catch(() => {})
      : Promise.resolve(),
    waitUnityMemLate().catch(() => {}),
  ]);
  // wasm 적재 경로(index.html 이 기록). 첫 프레임에 enc/dec 가 채워진 뒤의 값이다.
  const wasmPath = await page.evaluate(() => window['__AIT_WASM_PATH'] || null).catch(() => null);
  if (wasmPath) console.log(`  __AIT_WASM_PATH${tag}: ${JSON.stringify(wasmPath)}`);
  const audio = await page.evaluate(() => ({
    pcm: window.__aitAudioPcmBytes, media: window.__aitAudioMediaEls,
    calls: window.__aitAudioDecodeCalls || 0, inBytes: window.__aitAudioDecodeInBytes || 0, errors: window.__aitAudioDecodeErrors || 0,
    heap: (() => {
      let mx = (window.__aitWasmMemories || []).reduce((a, m) => { try { return Math.max(a, m.buffer.byteLength); } catch (e) { return a; } }, 0);
      // 폴백: Unity 인스턴스 Module 의 힙/메모리 객체.
      try {
        const M = window.unityInstance && window.unityInstance.Module;
        if (M) {
          if (M.wasmMemory && M.wasmMemory.buffer) mx = Math.max(mx, M.wasmMemory.buffer.byteLength);
          const h = M.HEAPU8 || M.HEAP8;
          if (h && h.buffer) mx = Math.max(mx, h.buffer.byteLength);
        }
      } catch (e) { /* 폴백 실패 무시 */ }
      return mx;
    })(),
  })).catch(() => null);
  if (audio && typeof audio.pcm === 'number') {
    console.log(`  오디오 디코드${tag}: PCM=${(audio.pcm / 1048576).toFixed(2)}MB mediaElement=${audio.media}` +
      ` decode호출=${audio.calls}(입력 ${(audio.inBytes / 1048576).toFixed(2)}MB, 실패 ${audio.errors})` +
      ` wasm힙=${(audio.heap / 1048576).toFixed(0)}MB` +
      (probed ? ` (재수화 대기 ${Date.now() - t0}ms)` : ''));
  }
  // 진단 태그([AIT-WasmPath]/[AIT-UnityMem]/[AIT-Memory] 부팅 마커/[AIT-GL] 업로드)는 개수 상한에 밀리지 않게 먼저 모두 남기고, 나머지는 40줄까지.
  const diag = /AIT-WasmPath|AIT-UnityMem|AIT-CodeSize|AIT-Memory\] (first-frame|텔레메트리)|AIT-GL\] 텍스처 업로드|managed brotli/;
  const rest = consoleLines.filter((l) => !diag.test(l)).slice(0, 40);
  for (const l of [...consoleLines.filter((l) => diag.test(l)), ...rest]) console.log(`    console${tag}: ${l.slice(0, 260)}`);
}

/** 외부화 BGM 재수화를 기다리는 상한(첫 iter 에서만). */
const AUDIO_REHYDRATE_WAIT_MS = 30000;

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
  const memBaseline = MEASURE_MEMORY ? await captureRendererBaseline(browser) : null;
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await context.addInitScript(TTFF_INIT_SCRIPT);
  if (MEASURE_MEMORY) await context.addInitScript(MEM_INIT_SCRIPT);
  const page = await openThrottledPage(context);
  const memSampler = MEASURE_MEMORY ? await startMemorySampler(browser, context, page, memBaseline).catch((e) => { console.warn(`  메모리 샘플러 시작 실패: ${e.message}`); return null; }) : null;
  const audioConsole = [];
  if (iter === 0) {
    page.on('console', (m) => {
      const t = m.text();
      if (/AIT-Streaming|AIT-Audio|HeavyAudioProbe|Decode error|AIT-GL|AIT-Pacing|AIT-Memory|AIT-DataBuf|AIT-WasmPath|AIT-UnityMem|AIT-CodeSize/.test(t)) audioConsole.push(t);
    });
  }

  const navStart = Date.now();
  try {
    const metrics = await measureLoad(page, url);
    const ttff = metrics.ttff;

    // 첫 프레임 뒤 settle 동안 계속 샘플링한 뒤 멈춘다(TTFF 값은 이미 확정됐으므로 TTFF 에 영향 없음).
    let memory = null;
    if (memSampler) {
      await page.waitForTimeout(MEM_SETTLE_MS);
      memory = await memSampler.stop().catch((e) => { console.warn(`  메모리 집계 실패: ${e.message}`); return null; });
    }

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
    if (memory) sample.memory = memory;

    let warmNote = '';
    let audioLogged = false;
    const warmFits = Date.now() + WARM_DEADLINE_MS + CLOSE_DEADLINE_MS < testDeadlineAt;
    if (MEASURE_WARM && typeof ttff === 'number' && !warmFits) {
      console.warn(`  warm 측정 생략(iter ${iter + 1}): 테스트 시간 예산 부족`);
    }
    if (MEASURE_WARM && typeof ttff === 'number' && warmFits) {
      try {
        const warm = await withDeadline((async () => {
          // 오디오 재수화 대기는 put 대기와 겹치도록 먼저 한다(warm 시간 예산 안에 들도록).
          if (iter === 0) { await logAudioDecode(page, label, audioConsole); audioLogged = true; }
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

    if (iter === 0 && !audioLogged) await logAudioDecode(page, label, audioConsole);

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
    ...(MEASURE_MEMORY ? { memory: summarizeMemory(samples) } : {}),
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
    // 압축 포맷이 다른 쌍의 ΔTTFF 는 레버 효과가 아니라 전송 크기·디코드 차이다. 조용히 통과시키지 않는다.
    const compA = detectCompression(DIST_WEB);
    const compB = detectCompression(PAIR_DIST_WEB);
    if (compA !== compB) {
      const msg = `페어 A/B 압축 포맷 불일치: A=${compA} B=${compB}. 같은 compression_format 으로 다시 빌드하세요(1=Gzip, 2=Brotli).`;
      if (process.env.PERF_ALLOW_COMPRESSION_MISMATCH === '1') console.warn(`⚠️  ${msg} (PERF_ALLOW_COMPRESSION_MISMATCH=1 로 계속 진행)`);
      else expect(compA, msg).toBe(compB);
    }
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
      ...(MEASURE_MEMORY ? {
        deltaMemory: {
          rendererPeakRssBytes: deltaStat(samplesA, samplesB, (m) => m?.rendererPeakRssBytes),
          jsHeapPeakBytes: deltaStat(samplesA, samplesB, (m) => m?.jsHeapPeakBytes),
          bigAllocTotalBytes: deltaStat(samplesA, samplesB, (m) => m?.bigAllocs?.totalBytes),
        },
      } : {}),
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
    if (MEASURE_MEMORY) {
      logMemory(LABEL_A, resultA.memory);
      logMemory(LABEL_B, resultB.memory);
      logDelta('rendererPeakRSS', pairing.deltaMemory.rendererPeakRssBytes);
      logDelta('jsHeapPeak', pairing.deltaMemory.jsHeapPeakBytes);
      logDelta('bigAllocs 합', pairing.deltaMemory.bigAllocTotalBytes);
    }
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
  if (MEASURE_MEMORY && !PAIR_MODE) logMemory(LABEL_A, resultA.memory);
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
