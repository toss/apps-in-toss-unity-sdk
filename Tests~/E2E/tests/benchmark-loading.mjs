// @ts-check
// 한 페어(unity_ver × pillar)의 전체 매트릭스 측정.
// 압축/전송 설정의 세 가지 현실 시나리오(3-필러)를 측정한다:
//   pillar1 — Unity 압축 Disabled. 평문 산출물을 정적 서버가 on-the-fly gzip
//             압축해 Content-Encoding: gzip 전송 → 브라우저 네이티브 gzip 디코딩.
//   pillar2 — Unity Brotli .unityweb 산출물을 Content-Encoding 헤더 없이 전송
//             → Unity 로더 JS decompressionFallback 디코딩.
//   pillar3 — Unity Brotli .unityweb 산출물을 Content-Encoding: br 전송
//             → 브라우저 네이티브 Brotli 디코딩. (정석 설정)
// network × cpu × iter cell마다 Cold(브라우저 완전 재시작) 로딩 시간을 측정한다.
// Warm 측정은 제외했다: CDP Network.emulateNetworkConditions가 HTTP 디스크
// 캐시를 우회시켜 throttling과 캐시 측정을 동시에 만족할 수 없기 때문.
// 결과를 JSONL 한 줄씩 append.
//
// 환경변수:
//   BENCHMARK_UNITY        — Unity 버전 (예: 6000.2). 필수.
//   BENCHMARK_PILLAR       — 'pillar1' | 'pillar2' | 'pillar3'. 필수.
//   BENCHMARK_ITERATIONS   — 반복 횟수 (기본 5).
//   BENCHMARK_NETWORKS     — 복수 네트워크 측정 (comma-separated, 우선순위 최상).
//   BENCHMARK_NETWORK      — 단일 네트워크만 측정 (생략 시 NETWORK_KEYS 전체).
//   BENCHMARK_CPUS         — 복수 CPU rate 측정 (comma-separated, 우선순위 최상).
//   BENCHMARK_CPU          — 단일 CPU rate만 측정 (생략 시 CPU_KEYS 전체).
//   BENCHMARK_RESULTS_PATH — 결과 JSONL 경로 (기본 ./benchmark-loading-results.jsonl).
//   BENCHMARK_TIMEOUT_MS   — 로딩 timeout (기본 180000).

import { chromium } from '@playwright/test';
import { execSync } from 'child_process';
import * as fs from 'fs';
import * as http from 'http';
import * as net from 'net';
import * as os from 'os';
import * as path from 'path';
import * as zlib from 'zlib';
import { fileURLToPath } from 'url';
import {
  NETWORK_PROFILES,
  CPU_PROFILES,
  UNITY_VERSION_PORTS,
  NETWORK_KEYS,
  CPU_KEYS,
  PILLARS,
} from './lib/throttling-profiles.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const UNITY_VER = required('BENCHMARK_UNITY');
// 필러: 압축/전송 설정의 세 가지 현실 시나리오. PILLARS 정의(throttling-profiles.js)
// 참조. 각 필러는 (빌드 산출물 dist) × (정적 서버 Content-Encoding 동작) 조합.
const PILLAR = required('BENCHMARK_PILLAR');
const ITERATIONS = parseInt(process.env.BENCHMARK_ITERATIONS || '5', 10);
const TIMEOUT_MS = parseInt(process.env.BENCHMARK_TIMEOUT_MS || '180000', 10);
// 정리 패스: 기존 JSONL에서 이 페어의 에러 cell만 재측정하고 그 줄을 교체한다.
// 에러 없는 cell은 건너뛴다. (전체 매트릭스 재실행 없이 누락 메트릭만 보충)
const RETRY_ERRORS = process.env.BENCHMARK_RETRY_ERRORS === '1';
const RESULTS_PATH = path.resolve(
  process.env.BENCHMARK_RESULTS_PATH || path.join(__dirname, 'benchmark-loading-results.jsonl'),
);

if (!UNITY_VERSION_PORTS[UNITY_VER]) {
  fail(`Unknown Unity version: ${UNITY_VER}. Expected one of ${Object.keys(UNITY_VERSION_PORTS).join(', ')}`);
}
if (!PILLARS[PILLAR]) {
  fail(`Unknown pillar: ${PILLAR}. Expected one of ${Object.keys(PILLARS).join(', ')}`);
}
// 이 필러의 정적 서버 인코딩 동작: 'cdn-gzip' | 'none' | 'native'.
const ENCODING_MODE = PILLARS[PILLAR].encoding;

const PROJECT_PATH = path.resolve(__dirname, `../SampleUnityProject-${UNITY_VER}`);
const AIT_BUILD = path.resolve(PROJECT_PATH, 'ait-build');
// 필러별 빌드 산출물: pillar1=web-disabled(평문), pillar2/3=web-brotli(.unityweb).
const DIST_WEB = path.resolve(AIT_BUILD, 'dist', PILLARS[PILLAR].dist);
const PORT = UNITY_VERSION_PORTS[UNITY_VER];

// 측정 축 선택 우선순위: 복수형(comma-separated) > 단수형 > 기본 키 집합.
// 가설 벤치마크는 BENCHMARK_NETWORKS/BENCHMARK_CPUS로 다축을 한 번에 돌린다.
const networksToRun = process.env.BENCHMARK_NETWORKS
  ? process.env.BENCHMARK_NETWORKS.split(',').map((s) => s.trim()).filter(Boolean)
  : process.env.BENCHMARK_NETWORK
    ? [process.env.BENCHMARK_NETWORK]
    : NETWORK_KEYS;
const cpusToRun = process.env.BENCHMARK_CPUS
  ? process.env.BENCHMARK_CPUS.split(',').map((s) => s.trim()).filter(Boolean)
  : process.env.BENCHMARK_CPU
    ? [process.env.BENCHMARK_CPU]
    : CPU_KEYS;

for (const k of networksToRun) {
  if (!NETWORK_PROFILES[k]) fail(`Unknown network: ${k}`);
}
for (const k of cpusToRun) {
  if (!CPU_PROFILES[k]) fail(`Unknown cpu: ${k}`);
}

if (!fs.existsSync(DIST_WEB)) {
  fail(`Build output not found: ${DIST_WEB} (pillar=${PILLAR}). Run benchmark.sh Phase 1 first.`);
}

const MACHINE = {
  cpu_model: os.cpus()[0]?.model || 'unknown',
  cpu_count: os.cpus().length,
  arch: os.arch(),
  platform: os.platform(),
  node_version: process.version,
};

// 2021.3/2022.3은 webGLDataCaching=false, 6000.x는 true (ProjectSettings 실측)
const WEBGL_DATA_CACHING = UNITY_VER.startsWith('6000');

main().catch((err) => {
  console.error('[benchmark] fatal:', err);
  process.exit(1);
});

async function main() {
  console.log(`[benchmark] Unity ${UNITY_VER} | pillar=${PILLAR} (${PILLARS[PILLAR].label}) | port=${PORT}`);
  console.log(`[benchmark] dist=${DIST_WEB} | encoding-mode=${ENCODING_MODE}`);
  console.log(`[benchmark] results=${RESULTS_PATH}`);
  console.log(`[benchmark] networks=${networksToRun.join(',')} cpus=${cpusToRun.join(',')} iter=${ITERATIONS}`);

  await killPort(PORT);
  const serverInfo = await startViteServer(DIST_WEB, PORT, ENCODING_MODE);
  console.log(`[benchmark] static server ready on port ${serverInfo.port}`);

  try {
    const url = `http://localhost:${serverInfo.port}?e2e=true`;
    if (RETRY_ERRORS) {
      await retryErrorCells(url);
    } else {
      for (const network of networksToRun) {
        for (const cpu of cpusToRun) {
          for (let iter = 1; iter <= ITERATIONS; iter++) {
            await measureCellWithRetry({ network, cpu, iter, url });
          }
        }
      }
    }
  } finally {
    console.log('[benchmark] shutting down vite preview');
    try {
      serverInfo.process.kill('SIGTERM');
    } catch {}
    await sleep(500);
    await killPort(PORT);
  }
}

// cell이 측정 오염(머신 부하 등)으로 비정상적으로 느린지 판정.
// pillar1(비압축 ~92MB 평문, 단 CDN gzip으로 전송량 감소)도 wifi/cpu-4x에서
// 정상값은 ~수십 초 범위. 120s를 넘으면 측정 오염으로 보고 재측정 대상으로 삼는다.
function isOutlier(r) {
  if (r.error) return true;
  const limit = 120000;
  const cold = r.cold_load_ms || 0;
  return cold > limit;
}

// 정리 패스: 기존 JSONL에서 이 페어의 에러/이상치 cell만 골라 재측정하고 그 줄을 교체.
async function retryErrorCells(url) {
  if (!fs.existsSync(RESULTS_PATH)) {
    console.log('[retry] no results file — nothing to retry');
    return;
  }
  const lines = fs.readFileSync(RESULTS_PATH, 'utf8').split('\n').filter((l) => l.trim());
  const rows = lines.map((l) => JSON.parse(l));
  const cpuRateToKey = Object.fromEntries(
    Object.entries(CPU_PROFILES).map(([k, v]) => [v.rate, k]),
  );

  let fixed = 0;
  let stillFailing = 0;
  for (let i = 0; i < rows.length; i++) {
    const r = rows[i];
    if (r.unity_version !== UNITY_VER || r.pillar !== PILLAR) continue;
    if (!isOutlier(r)) continue;
    const cpuKey = cpuRateToKey[r.cpu_rate];
    if (!cpuKey) {
      console.error(`[retry] unknown cpu_rate ${r.cpu_rate}; skipping`);
      continue;
    }
    const replacement = await measureCellWithRetry({
      network: r.network,
      cpu: cpuKey,
      iter: r.iteration,
      url,
      append: false,
    });
    rows[i] = replacement;
    if (isOutlier(replacement)) stillFailing++;
    else fixed++;
  }
  fs.writeFileSync(RESULTS_PATH, rows.map((r) => JSON.stringify(r)).join('\n') + '\n');
  console.log(`[retry] ${UNITY_VER}/${PILLAR}: fixed=${fixed} stillFailing=${stillFailing}`);
}

// cell당 최대 CELL_MAX_ATTEMPTS회 시도. Chromium이 무거운 로드에서 간헐적으로
// 죽어(page/context closed) warm 측정이 누락되므로, 에러 없는 결과를 얻을 때까지
// 재시도한다. 모두 실패하면 마지막(에러 포함) 결과를 기록해 매트릭스는 계속 진행.
const CELL_MAX_ATTEMPTS = 4;

async function measureCellWithRetry({ network, cpu, iter, url, append = true }) {
  const label = `${UNITY_VER}|${PILLAR}|${network}|${cpu}|iter${iter}`;
  let result;
  for (let attempt = 1; attempt <= CELL_MAX_ATTEMPTS; attempt++) {
    result = await measureCellOnce({ network, cpu, iter, url, attempt });
    // 에러뿐 아니라 측정 오염(머신 부하로 비정상적으로 느린 값)도 재시도 대상.
    const bad = result.error
      ? result.error
      : isOutlier(result)
        ? `outlier cold=${result.cold_load_ms}`
        : null;
    if (!bad) break;
    if (attempt < CELL_MAX_ATTEMPTS) {
      console.error(`[cell ${label}] attempt ${attempt} failed (${bad}); retrying`);
      await sleep(2000);
    } else {
      console.error(`[cell ${label}] gave up after ${CELL_MAX_ATTEMPTS} attempts`);
    }
  }
  if (append) fs.appendFileSync(RESULTS_PATH, JSON.stringify(result) + '\n');
  return result;
}

async function measureCellOnce({ network, cpu, iter, url, attempt }) {
  const netProfile = NETWORK_PROFILES[network];
  const cpuRate = CPU_PROFILES[cpu].rate;
  const label = `${UNITY_VER}|${PILLAR}|${network}|${cpu}|iter${iter}`;
  console.log(`[cell ${label}] launching browser (attempt ${attempt})`);

  let browser;
  let result = {
    timestamp: new Date().toISOString(),
    unity_version: UNITY_VER,
    pillar: PILLAR,
    network,
    cpu_rate: cpuRate,
    iteration: iter,
    cold_load_ms: null,
    cold_transfer_bytes: null,
    cold_resource_count: null,
    webgl_data_caching: WEBGL_DATA_CACHING,
    machine: MACHINE,
    error: null,
  };

  try {
    browser = await chromium.launch({
      args: ['--enable-webgl', '--use-angle=default'],
    });
    const context = await browser.newContext({
      viewport: { width: 1280, height: 720 },
    });
    const page = await context.newPage();

    // 정적 서버가 Cache-Control: max-age를 직접 주므로 page.route() 헤더 우회 불필요.
    // page.route()는 모든 요청을 Node로 routing해 CDP throttling 측정을 왜곡할 수
    // 있어 의도적으로 사용하지 않는다.
    const cdp = await context.newCDPSession(page);
    if (cpuRate > 1) {
      await cdp.send('Emulation.setCPUThrottlingRate', { rate: cpuRate });
    }
    await cdp.send('Network.emulateNetworkConditions', {
      offline: false,
      ...netProfile,
    });

    // Cold
    const coldStartMs = Date.now();
    await page.goto(url, { waitUntil: 'commit', timeout: TIMEOUT_MS });
    // 로딩 완료 신호: window.TriggerAPITest 함수 등록 시점.
    // 이 함수는 jslib RegisterTriggerFunctions가 등록하며, 호출 주체는
    // E2ETestTrigger.Awake()다. Awake()는 첫 씬 로드 후 BenchmarkManager가
    // 생성되는 프레임에 실행되므로 — 즉 "번들 디코딩 + 엔진 부팅 + 첫 씬 로드 +
    // 첫 프레임 게임 코드 실행"이 모두 끝난 시점이다. unityInstance(엔진 부팅
    // 완료)보다 downstream이라 실제 유저가 겪는 전체 게임 로딩 시간을 포착한다.
    await page.waitForFunction(() => typeof window['TriggerAPITest'] === 'function', null, {
      timeout: TIMEOUT_MS,
    });
    const coldEndMs = Date.now();
    result.cold_load_ms = coldEndMs - coldStartMs;
    const coldResources = await collectResources(page);
    result.cold_transfer_bytes = coldResources.totalBytes;
    result.cold_resource_count = coldResources.count;

    console.log(`[cell ${label}] cold=${result.cold_load_ms}ms`);
  } catch (err) {
    result.error = err && err.message ? err.message : String(err);
    console.error(`[cell ${label}] error: ${result.error}`);
  } finally {
    if (browser) {
      try {
        await browser.close();
      } catch {}
    }
  }

  return result;
}

async function collectResources(page) {
  try {
    return await page.evaluate(() => {
      const entries = performance.getEntriesByType('resource');
      let totalBytes = 0;
      for (const e of entries) {
        if (typeof e['transferSize'] === 'number') totalBytes += e['transferSize'];
      }
      return { count: entries.length, totalBytes };
    });
  } catch {
    return { count: null, totalBytes: null };
  }
}

// 빌드 산출물(dist/web-*)을 서빙하는 Node 정적 서버.
// vite preview를 쓰지 않는 이유: artifact에는 dist/만 들어 있고 vite.config.ts/
// package.json이 없으며, SDK vite.config.ts의 미들웨어는 outDir이 'dist/web'으로
// 하드코딩돼 있어 web-brotli/web-disabled를 못 본다. 대신 SDK vite.config.ts의
// .unityweb 압축 감지 로직(헤더 64바이트의 '(brotli)'/'(gzip)')을 그대로 차용한다.
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.mjs': 'application/javascript',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.css': 'text/css',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.data': 'application/octet-stream',
};

function detectUnityWebCompression(filePath) {
  try {
    const fd = fs.openSync(filePath, 'r');
    const buffer = Buffer.alloc(64);
    fs.readSync(fd, buffer, 0, 64, 0);
    fs.closeSync(fd);
    const header = buffer.toString('ascii');
    if (header.includes('(brotli)')) return 'br';
    if (header.includes('(gzip)')) return 'gzip';
    return null;
  } catch {
    return null;
  }
}

// gzip on-the-fly 압축 대상 — 압축 이득이 있는 텍스트/바이너리 자산.
// 텍스처·오디오가 들어간 .data도 포함한다: 실제 CDN도 MIME 화이트리스트 기준
// 으로 압축하며, .data는 application/octet-stream으로 보통 압축 대상에 든다.
const CDN_GZIP_EXTS = new Set(['.js', '.mjs', '.json', '.wasm', '.data', '.html', '.css', '.svg']);

// 정적 서버. encodingMode는 필러에 따라 .unityweb / 평문 자산의 Content-Encoding
// 동작을 결정한다 (PILLARS 정의 참조):
//   'native'   — pillar3. .unityweb의 내장 압축('(brotli)'/'(gzip)' 헤더 감지)에
//                맞춰 Content-Encoding 부여 → 브라우저 네이티브 디코딩.
//   'none'     — pillar2. .unityweb이지만 Content-Encoding 생략 → 브라우저는
//                압축 바이트를 그대로 전달, Unity 로더가 JS decompressionFallback.
//   'cdn-gzip' — pillar1. 산출물은 평문(비압축 빌드)이고, 서버(=CDN 근사)가
//                압축성 자산을 zlib으로 on-the-fly gzip해 Content-Encoding: gzip
//                전송 → 브라우저 네이티브 gzip 디코딩. Unity 로더는 평문을 받음.
function startViteServer(rootDir, port, encodingMode) {
  return new Promise((resolve, reject) => {
    const compressionCache = new Map();

    const server = http.createServer((req, res) => {
      let urlPath = (req.url || '/').split('?')[0];
      if (urlPath === '/') urlPath = '/index.html';
      const decoded = decodeURIComponent(urlPath);
      const filePath = path.join(rootDir, decoded);

      // 디렉토리 탈출 방지
      if (!filePath.startsWith(rootDir)) {
        res.writeHead(403);
        res.end('forbidden');
        return;
      }

      fs.stat(filePath, (err, stat) => {
        if (err || !stat.isFile()) {
          res.writeHead(404);
          res.end('not found');
          return;
        }

        const ext = path.extname(filePath);
        const headers = {};
        // 이 응답을 서버가 on-the-fly gzip할지 여부 (pillar1 경로).
        let cdnGzip = false;

        if (decoded.endsWith('.unityweb')) {
          // pillar2/3: 빌드 산출물이 이미 .unityweb(내장 압축)인 경우.
          let encoding = compressionCache.get(filePath);
          if (encoding === undefined) {
            encoding = detectUnityWebCompression(filePath);
            compressionCache.set(filePath, encoding);
          }
          if (encoding && encodingMode === 'native') headers['Content-Encoding'] = encoding;
          if (decoded.includes('.wasm')) headers['Content-Type'] = 'application/wasm';
          else if (decoded.includes('.js')) headers['Content-Type'] = 'application/javascript';
          else headers['Content-Type'] = 'application/octet-stream';
        } else {
          headers['Content-Type'] = MIME[ext] || 'application/octet-stream';
          // pillar1: 평문 자산을 CDN처럼 on-the-fly gzip. 클라이언트가
          // gzip을 수락하고(Accept-Encoding) 압축성 확장자일 때만.
          const acceptsGzip = (req.headers['accept-encoding'] || '').includes('gzip');
          if (encodingMode === 'cdn-gzip' && acceptsGzip && CDN_GZIP_EXTS.has(ext)) {
            cdnGzip = true;
            headers['Content-Encoding'] = 'gzip';
          }
        }

        // 정적 자산 표준 헤더. Cold 측정만 하므로 캐시 적중에 의존하지 않지만,
        // 실제 배포 서버와 동일한 응답 형태를 유지하기 위해 그대로 둔다.
        headers['Cache-Control'] = 'public, max-age=3600';

        const etag = `"${stat.size}-${stat.mtimeMs}"`;
        const lastModified = stat.mtime.toUTCString();
        headers['ETag'] = etag;
        headers['Last-Modified'] = lastModified;

        // 조건부 요청 — 클라이언트 캐시가 유효하면 304로 본문 전송 생략.
        const inm = req.headers['if-none-match'];
        const ims = req.headers['if-modified-since'];
        if (inm === etag || (ims && new Date(ims).getTime() >= Math.floor(stat.mtimeMs / 1000) * 1000)) {
          res.writeHead(304, headers);
          res.end();
          return;
        }

        if (cdnGzip) {
          // on-the-fly gzip: Content-Length는 압축 후 결정되므로 생략하고
          // (Node가 chunked로 전송) 파일 스트림을 gzip 변환기에 통과시킨다.
          res.writeHead(200, headers);
          fs.createReadStream(filePath).pipe(zlib.createGzip()).pipe(res);
          return;
        }

        res.writeHead(200, headers);
        fs.createReadStream(filePath).pipe(res);
      });
    });

    server.on('error', reject);
    server.listen(port, '127.0.0.1', () => {
      resolve({
        process: { kill: () => server.close() },
        port,
      });
    });
  });
}

async function killPort(port) {
  try {
    if (process.platform === 'win32') {
      execSync(
        `for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`,
        { stdio: 'ignore', shell: true },
      );
    } else {
      execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {}
  await waitForPortRelease(port, 5000);
}

function waitForPortRelease(port, timeoutMs) {
  return new Promise((resolve) => {
    const deadline = Date.now() + timeoutMs;
    const tryOnce = () => {
      const s = net.createServer();
      s.once('error', () => {
        if (Date.now() > deadline) resolve();
        else setTimeout(tryOnce, 200);
      });
      s.once('listening', () => {
        s.close(() => resolve());
      });
      s.listen(port, '127.0.0.1');
    };
    tryOnce();
  });
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

function required(name) {
  const v = process.env[name];
  if (!v) fail(`Missing required environment variable: ${name}`);
  return v;
}

function fail(msg) {
  console.error('[benchmark]', msg);
  process.exit(2);
}
