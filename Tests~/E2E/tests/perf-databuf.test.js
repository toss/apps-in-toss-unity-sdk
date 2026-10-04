// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK — data 응답 정확 크기 재포장(ait-databuf.js) 할당 트레이스 테스트
 *
 * 배경: Unity 로더는 .data 를 받을 때 Content-Length 로 버퍼 u 를 한 번 잡는다(u = new Uint8Array(d)).
 * .data.br 를 Content-Encoding: br 로 서빙하면 Content-Length 는 압축 크기라 u 가 실제 크기와 어긋나고,
 * 디코드된 스트림이 넘치면 새 버퍼에 복사(new Uint8Array(f)), 모자라면 slice 로 복사한다 → data 크기만큼의
 * 일시 피크가 한 번 더 생긴다. ait-databuf.js 가 응답을 Content-Length=RAW(압축 해제 크기)로 다시 감싸면
 * 로더는 정확한 크기로 한 번만 잡는다.
 *
 * 측정 방식(페이지 스크립트보다 먼저 실행되는 init script):
 *  - 전역 Uint8Array 를 Proxy 로 감싸 생성자 호출을 센다. 숫자 길이/배열/타입드 배열로 만드는 '새 할당'만 기록하고
 *    ArrayBuffer 위의 뷰(HEAPU8 등)는 할당이 아니므로 제외한다.
 *  - %TypedArray%.prototype.slice 와 ArrayBuffer.prototype.slice 도 감싼다(로더의 u.slice(0, f) 복사 경로).
 *  - 64KB 이상 할당만 {kind, len, 호출 위치}로 남긴다.
 *
 * 기대: .data 크기(RAW = __AIT_PERF.dataRawSize)급 할당이 정확히 1건이고 그 길이가 RAW 와 같다.
 *  - 컨트롤: 같은 빌드에서 exactDataBody 를 끄면(__AIT_PERF 를 init script 로 덮어씀) 압축 전송 응답에서는
 *    이 조건이 깨진다(overflow 복사 또는 slice). 트레이스가 복사를 실제로 잡는다는 증거다.
 *
 * 건너뜀: 빌드 산출물(dist/web)이 없거나, 빌드가 재포장을 쓰지 않는 구성(exactDataBody 꺼짐, dataRawSize 미상,
 * .unityweb)이면 skip. 무압축 서빙(E2E CI 의 AIT_COMPRESSION_FORMAT=0)은 Content-Length 가 이미 정확해 재포장 없이도
 * 통과한다(그 경우 재포장 단언은 'already-exact' 로 바뀐다).
 *
 * 환경변수:
 *  - UNITY_PROJECT_PATH : 빌드 산출물(ait-build/dist/web 포함) 경로. 미지정 시 HeavySampleUnityProject-* → SampleUnityProject-* 자동탐지.
 *  - DATABUF_SKIP_CONTROL=1 : 컨트롤(exactDataBody 끈 로드) 생략.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const TRACE_MIN_BYTES = 64 * 1024;
const LOAD_TIMEOUT_MS = 240000;
const SKIP_CONTROL = process.env.DATABUF_SKIP_CONTROL === '1';

function findProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) return envPath;
  const versions = ['6000.3', '6000.0', '6000.2', '2022.3', '2021.3'];
  for (const prefix of ['HeavySampleUnityProject', 'SampleUnityProject']) {
    for (const v of versions) {
      const p = path.resolve(__dirname, `../${prefix}-${v}`);
      if (fs.existsSync(path.resolve(p, 'ait-build/dist/web'))) return p;
    }
  }
  return path.resolve(__dirname, '../HeavySampleUnityProject-6000.3');
}

const PROJECT = findProject();
const AIT_BUILD = path.resolve(PROJECT, 'ait-build');
const DIST_WEB = path.resolve(AIT_BUILD, 'dist/web');

function portOffset(projectPath) {
  const m = projectPath.match(/(\d+)\.(\d+)$/);
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
// full-pipeline(4173+)/perf(4223+)/pair(4273+)/ce-serving(4323+)와 겹치지 않는 대역
const DEFAULT_PORT = 4373 + portOffset(PROJECT);

// ---- 할당 트레이스 init script (페이지 스크립트보다 먼저 실행) ----
const ALLOC_TRACE_INIT_SCRIPT = `
(function () {
  if (window.__aitAllocTraceHooked) return;
  window.__aitAllocTraceHooked = true;
  var MIN = ${TRACE_MIN_BYTES};
  var trace = [];
  window.__aitAllocTrace = trace;

  function where() {
    try {
      var lines = String(new Error().stack || '').split('\\n');
      // 0: "Error", 1: where(), 2: record(), 3~: 호출자
      for (var i = 3; i < lines.length; i++) {
        if (lines[i].indexOf('__aitAllocTrace') === -1) return lines[i].trim().slice(0, 200);
      }
    } catch (e) {}
    return '';
  }
  function record(kind, len) {
    if (len >= MIN) trace.push({ kind: kind, len: len, at: where(), t: performance.now() });
  }

  // 1) new Uint8Array(...) — 숫자 길이 / 배열 / 타입드 배열 인자만 새 할당이다. ArrayBuffer·SharedArrayBuffer 인자는 뷰.
  try {
    var OrigU8 = window.Uint8Array;
    var U8Proxy = new Proxy(OrigU8, {
      construct: function (target, args, newTarget) {
        var obj = Reflect.construct(target, args, newTarget === U8Proxy ? target : newTarget);
        try {
          var a0 = args[0];
          if (typeof a0 === 'number') record('new', obj.length);
          else if (a0 && typeof a0 === 'object' && !(a0 instanceof ArrayBuffer)
                   && !(typeof SharedArrayBuffer === 'function' && a0 instanceof SharedArrayBuffer)) record('new-from', obj.length);
        } catch (e) {}
        return obj;
      }
    });
    window.Uint8Array = U8Proxy;
  } catch (e) {}

  // 2) %TypedArray%.prototype.slice (로더의 u.slice(0, f))
  try {
    var TAProto = Object.getPrototypeOf(Uint8Array.prototype);
    var origSlice = TAProto.slice;
    TAProto.slice = function () {
      var r = origSlice.apply(this, arguments);
      try { record('slice', r.byteLength); } catch (e) {}
      return r;
    };
  } catch (e) {}

  // 3) ArrayBuffer.prototype.slice
  try {
    var origABSlice = ArrayBuffer.prototype.slice;
    ArrayBuffer.prototype.slice = function () {
      var r = origABSlice.apply(this, arguments);
      try { record('ab-slice', r.byteLength); } catch (e) {}
      return r;
    };
  } catch (e) {}
})();
`;

// 컨트롤용: index.html head 의 인라인 스크립트가 window.__AIT_PERF 에 대입한 값을 받아 exactDataBody 만 끈 사본을 돌려준다.
const DISABLE_EXACT_BODY_INIT_SCRIPT = `
(function () {
  var real;
  try {
    Object.defineProperty(window, '__AIT_PERF', {
      configurable: true,
      get: function () {
        if (!real || typeof real !== 'object') return real;
        var c = {};
        for (var k in real) { if (Object.prototype.hasOwnProperty.call(real, k)) c[k] = real[k]; }
        c.exactDataBody = false;
        return c;
      },
      set: function (v) { real = v; }
    });
  } catch (e) {}
})();
`;

// ---- 유틸리티 (perf-ttff.test.js 패턴) ----
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
  try {
    if (process.platform === 'win32') {
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
async function waitServerReady(port) {
  for (let i = 0; i < 30; i++) {
    try {
      const res = await fetch(`http://localhost:${port}/`, { method: 'HEAD' });
      if (res.ok) return true;
    } catch {}
    await new Promise(r => setTimeout(r, 500));
  }
  return false;
}

/**
 * 페이지를 열어 Unity 인스턴스가 준비될 때까지 로드하고 할당 트레이스와 data 응답 헤더를 돌려준다.
 * @param {import('@playwright/test').Browser} browser
 * @param {string} url
 * @param {{ disableExactBody?: boolean }} opts
 */
async function loadAndTrace(browser, url, opts = {}) {
  const context = await browser.newContext();
  try {
    const page = await context.newPage();
    await page.addInitScript(ALLOC_TRACE_INIT_SCRIPT);
    if (opts.disableExactBody) await page.addInitScript(DISABLE_EXACT_BODY_INIT_SCRIPT);

    /** @type {{url:string, encoding:string|null, contentLength:string|null, status:number}|null} */
    let dataResponse = null;
    page.on('response', (r) => {
      const u = r.url();
      // framework/loader 는 제외하고 .data(.br/.gz/무압축)만. 쿼리 앞 경로 기준.
      const p = u.split('?')[0];
      if (/\.data(\.br|\.gz)?$/.test(p) && !dataResponse) {
        const h = r.headers();
        dataResponse = { url: u, encoding: h['content-encoding'] || null, contentLength: h['content-length'] || null, status: r.status() };
      }
    });
    const consoleLines = [];
    page.on('console', (m) => { const t = m.text(); if (t.indexOf('[AIT-DataBuf]') >= 0) consoleLines.push(t); });

    const resp = await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
    expect(resp?.status(), 'navigation should return 200').toBe(200);

    const perf = await page.evaluate(() => window['__AIT_PERF'] || null);

    await page.waitForFunction(() => window['unityInstance'] !== undefined, undefined, { timeout: LOAD_TIMEOUT_MS });

    const trace = await page.evaluate(() => window['__aitAllocTrace'] || []);
    const state = await page.evaluate(() => (window['__AIT_DATABUF'] && window['__AIT_DATABUF'].getState) ? window['__AIT_DATABUF'].getState() : null);
    return { perf, trace, state, dataResponse, consoleLines };
  } finally {
    await context.close().catch(() => {});
  }
}

/** RAW 의 절반 이상인 할당 = data 버퍼(또는 그 복사본). 같은 크기급 할당이 둘 이상이면 복사가 있었다는 뜻이다. */
function dataSized(trace, raw) {
  return trace.filter((a) => a.len >= Math.floor(raw / 2));
}

function describe(trace) {
  return trace.map((a) => `${a.kind}:${a.len} @ ${a.at}`).join('\n    ');
}

let serverProcess = null;
let serverPort = DEFAULT_PORT;

test.afterAll(async () => {
  await killServer(serverProcess, serverPort);
  serverProcess = null;
});

async function startServer() {
  const prod = await startProductionServer(AIT_BUILD, DEFAULT_PORT);
  serverProcess = prod.process;
  serverPort = prod.port;
  const ready = await waitServerReady(prod.port);
  expect(ready, `preview server should be reachable on :${prod.port}`).toBe(true);
  return `http://localhost:${prod.port}?e2e=true`;
}

test.describe('data 응답 정확 크기 재포장 (ait-databuf)', () => {
  test.setTimeout(LOAD_TIMEOUT_MS * 2 + 60000);

  test('data 버퍼 할당이 정확히 1건(길이 == dataRawSize)', async ({ browser }) => {
    test.skip(!directoryExists(DIST_WEB), `빌드 산출물이 없어 건너뜀: ${DIST_WEB}`);

    const url = await startServer();
    const r = await loadAndTrace(browser, url);

    console.log(`  __AIT_PERF.exactDataBody=${r.perf && r.perf.exactDataBody} dataRawSize=${r.perf && r.perf.dataRawSize} unityweb=${r.perf && r.perf.unityweb}`);
    console.log(`  data 응답: ${JSON.stringify(r.dataResponse)}`);
    console.log(`  databuf 상태: ${JSON.stringify(r.state)}`);
    for (const l of r.consoleLines) console.log(`  ${l}`);
    console.log(`  >= ${TRACE_MIN_BYTES / 1024}KB 할당 ${r.trace.length}건:\n    ${describe(r.trace)}`);

    test.skip(!r.perf || r.perf.exactDataBody === false, '빌드가 exactDataBody 를 끈 구성이라 건너뜀');
    test.skip(!r.perf || r.perf.unityweb === true, '.unityweb(Decompression Fallback) 빌드는 재포장 대상이 아님');
    test.skip(!r.perf || !(r.perf.dataRawSize > 0), 'dataRawSize 를 측정하지 못한 빌드(Node 미가용 등)라 건너뜀');

    const raw = r.perf.dataRawSize;

    // 래퍼가 설치됐고 data 응답을 실제로 만났다.
    expect(r.state, '__AIT_DATABUF.getState 가 있어야 한다').not.toBeNull();
    expect(r.state.installed, `재포장 래퍼 설치(reason=${r.state && r.state.reason})`).toBe(true);
    expect(r.state.matched, 'data 응답 1건을 가로채야 한다').toBeGreaterThanOrEqual(1);

    // 핵심 단언: data 크기급 할당이 정확히 1건이고, 길이가 RAW 와 같다.
    const sized = dataSized(r.trace, raw);
    expect(sized.length, `data 크기급 할당이 정확히 1건이어야 한다(복사 없음). 트레이스:\n    ${describe(r.trace)}`).toBe(1);
    expect(sized[0].len, 'data 버퍼 길이는 압축 해제 크기(RAW)와 같아야 한다').toBe(raw);

    // 응답 처리 결과: 압축 전송이면 재포장, 이미 정확한 길이면 건드리지 않는다.
    if (r.dataResponse && r.dataResponse.encoding) {
      expect(r.state.rewrapped, `Content-Encoding=${r.dataResponse.encoding} 응답은 재포장돼야 한다`).toBe(1);
      expect(r.consoleLines.some((l) => l.indexOf('data 응답 재포장') >= 0), '재포장 로그가 1회 남아야 한다').toBe(true);
    } else if (r.dataResponse && r.dataResponse.contentLength) {
      expect(r.state.rewrapped, '무압축 + Content-Length 응답은 이미 정확하므로 재포장하지 않는다').toBe(0);
    }
  });

  test('컨트롤: exactDataBody 를 끄면 압축 전송에서 복사가 잡힌다', async ({ browser }) => {
    test.skip(SKIP_CONTROL, 'DATABUF_SKIP_CONTROL=1');
    test.skip(!directoryExists(DIST_WEB), `빌드 산출물이 없어 건너뜀: ${DIST_WEB}`);

    const url = await startServer();
    const r = await loadAndTrace(browser, url, { disableExactBody: true });

    console.log(`  [control] data 응답: ${JSON.stringify(r.dataResponse)}`);
    console.log(`  [control] databuf 상태: ${JSON.stringify(r.state)}`);
    console.log(`  [control] >= ${TRACE_MIN_BYTES / 1024}KB 할당 ${r.trace.length}건:\n    ${describe(r.trace)}`);

    // 우리 래퍼가 꺼졌는지부터 확인한다(안 꺼졌으면 컨트롤이 무의미).
    expect(r.state && r.state.installed, '컨트롤에서는 재포장 래퍼가 설치되지 않아야 한다').not.toBe(true);

    test.skip(!r.perf || !(r.perf.dataRawSize > 0) || r.perf.unityweb === true, 'RAW 미상/.unityweb 빌드는 컨트롤 비교 불가');
    test.skip(!r.dataResponse || !r.dataResponse.encoding, '서버가 data 를 압축 전송(Content-Encoding)하지 않아 컨트롤 비교 불가');

    const raw = r.perf.dataRawSize;
    const sized = dataSized(r.trace, raw);
    const exactlyOneExact = sized.length === 1 && sized[0].len === raw;
    expect(exactlyOneExact,
      `압축 전송 + 재포장 꺼짐이면 overflow 복사/slice 로 '정확히 1건·길이==RAW' 가 깨져야 한다(트레이스가 복사를 잡는다는 증거).\n    ${describe(r.trace)}`).toBe(false);
  });
});
