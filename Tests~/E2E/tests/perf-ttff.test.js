// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
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
      execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
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

/**
 * 반복 1회 측정 (콜드 캐시 새 BrowserContext + 동일 CDP 스로틀). A/B 공용 — 페어 모드에서는
 * 이 함수를 A/B 각각의 url 로 호출해 같은 로직으로 공정하게 잰다.
 */
async function measureIteration(browser, url, iter, label) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await context.addInitScript(TTFF_INIT_SCRIPT);
  const page = await context.newPage();

  // 스로틀 적용 (네비게이션 이전)
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

  let ttff = null;
  let unityReady = null;
  let drawCount = 0;
  const navStart = Date.now();
  try {
    const resp = await page.goto(url, { waitUntil: 'commit', timeout: 120000 });
    expect(resp?.status(), 'navigation should return 200').toBe(200);

    // 첫 화면 draw 대기 (= TTFF). 미검출 시 unityInstance ready로 폴백.
    const gotDraw = await page.waitForFunction(
      () => window['__TTFF__'] !== null,
      { timeout: 480000 }
    ).then(() => true).catch(() => false);

    if (!gotDraw) {
      // 폴백: Unity 인스턴스 준비라도 확인 (측정은 무효 처리하되 진단 기록)
      await page.waitForFunction(() => window['unityInstance'] !== undefined, { timeout: 60000 })
        .then(() => { unityReady = true; })
        .catch(() => { unityReady = false; });
    }

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

    ttff = metrics.ttff;
    drawCount = metrics.firstDrawCount;

    const sample = {
      iteration: iter,
      ttffMs: ttff,
      unityReady,
      firstDrawCount: drawCount,
      domContentLoadedMs: metrics.domContentLoaded,
      loadEventMs: metrics.loadEvent,
      onWireBytes: metrics.onWire,
      wallClockMs: Date.now() - navStart,
    };
    // 페어 모드 전용 필드 — 단일 모드 결과 JSON 스키마는 기존과 완전히 동일해야 하므로 반드시 가드 안에서만 추가.
    if (PAIR_MODE) sample.label = label;

    console.log(`  iter ${iter + 1}/${ITERATIONS}${PAIR_MODE ? ` [${label}]` : ''}: TTFF=${ttff !== null ? ttff.toFixed(0) + 'ms' : 'N/A'} ` +
      `wasm=${(metrics.onWire.wasm / 1048576).toFixed(2)}MB data=${(metrics.onWire.data / 1048576).toFixed(2)}MB`);

    return sample;
  } finally {
    await context.close();
  }
}

/** 반복 샘플들을 집계해 결과 JSON 객체를 만든다(단일/페어 A/B 공용, pairing 필드는 호출부에서 추가). */
function summarize(samples, projectPath) {
  const ttffValues = samples.map(s => s.ttffMs).filter(v => typeof v === 'number' && v > 0);
  const wasmValues = samples.map(s => s.onWireBytes?.wasm).filter(v => typeof v === 'number' && v > 0);
  const dataValues = samples.map(s => s.onWireBytes?.data).filter(v => typeof v === 'number' && v > 0);
  const totalValues = samples.map(s => s.onWireBytes?.total).filter(v => typeof v === 'number' && v > 0);

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
  test.setTimeout(PAIR_MODE ? 1190000 : 590000);

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

  for (let iter = 0; iter < ITERATIONS; iter++) {
    if (!PAIR_MODE) {
      samplesA.push(await measureIteration(browser, url, iter, LABEL_A));
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
