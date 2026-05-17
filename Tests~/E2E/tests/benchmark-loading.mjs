// @ts-check
// 한 빌드 페어(unity_ver × compression)의 전체 매트릭스 측정.
// 4 network × 4 cpu × 5 iter = 80 cell. 각 cell마다 Cold(브라우저 재시작) + Warm(reload) 페어 측정.
// 결과를 JSONL 한 줄씩 append.
//
// 환경변수:
//   BENCHMARK_UNITY        — Unity 버전 (예: 6000.2). 필수.
//   BENCHMARK_COMPRESSION  — 'disabled' | 'brotli'. 필수.
//   BENCHMARK_ITERATIONS   — 반복 횟수 (기본 5).
//   BENCHMARK_NETWORK      — 단일 네트워크만 측정 (생략 시 전체 4개).
//   BENCHMARK_CPU          — 단일 CPU rate만 측정 (생략 시 전체 4개).
//   BENCHMARK_RESULTS_PATH — 결과 JSONL 경로 (기본 ./benchmark-loading-results.jsonl).
//   BENCHMARK_TIMEOUT_MS   — 로딩 timeout (기본 180000).

import { chromium } from '@playwright/test';
import { execSync } from 'child_process';
import * as fs from 'fs';
import * as http from 'http';
import * as net from 'net';
import * as os from 'os';
import * as path from 'path';
import { fileURLToPath } from 'url';
import {
  NETWORK_PROFILES,
  CPU_PROFILES,
  UNITY_VERSION_PORTS,
  NETWORK_KEYS,
  CPU_KEYS,
} from './lib/throttling-profiles.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const UNITY_VER = required('BENCHMARK_UNITY');
const COMPRESSION = required('BENCHMARK_COMPRESSION');
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
if (!['disabled', 'brotli'].includes(COMPRESSION)) {
  fail(`Unknown compression: ${COMPRESSION}. Expected 'disabled' or 'brotli'`);
}

const PROJECT_PATH = path.resolve(__dirname, `../SampleUnityProject-${UNITY_VER}`);
const AIT_BUILD = path.resolve(PROJECT_PATH, 'ait-build');
const DIST_WEB = path.resolve(AIT_BUILD, `dist/web-${COMPRESSION}`);
const PORT = UNITY_VERSION_PORTS[UNITY_VER];

const networksToRun = process.env.BENCHMARK_NETWORK ? [process.env.BENCHMARK_NETWORK] : NETWORK_KEYS;
const cpusToRun = process.env.BENCHMARK_CPU ? [process.env.BENCHMARK_CPU] : CPU_KEYS;

for (const k of networksToRun) {
  if (!NETWORK_PROFILES[k]) fail(`Unknown network: ${k}`);
}
for (const k of cpusToRun) {
  if (!CPU_PROFILES[k]) fail(`Unknown cpu: ${k}`);
}

if (!fs.existsSync(DIST_WEB)) {
  fail(`Build output not found: ${DIST_WEB}. Run benchmark.sh Phase 1 first.`);
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
  console.log(`[benchmark] Unity ${UNITY_VER} | compression=${COMPRESSION} | port=${PORT}`);
  console.log(`[benchmark] dist=${DIST_WEB}`);
  console.log(`[benchmark] results=${RESULTS_PATH}`);
  console.log(`[benchmark] networks=${networksToRun.join(',')} cpus=${cpusToRun.join(',')} iter=${ITERATIONS}`);

  await killPort(PORT);
  const serverInfo = await startViteServer(DIST_WEB, PORT);
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
// disabled×regular-4g는 92~110MB를 4Mbps로 받아 정상적으로 ~180-230s가 걸리므로
// 그 조합만 600s 임계, 나머지(brotli 또는 wifi)는 100s 임계를 적용한다.
function isOutlier(r) {
  if (r.error) return true;
  const slowCombo = r.compression === 'disabled' && r.network === 'regular-4g';
  const limit = slowCombo ? 600000 : 100000;
  const cold = r.cold_load_ms || 0;
  const warm = r.warm_load_ms || 0;
  return cold > limit || warm > limit;
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
    if (r.unity_version !== UNITY_VER || r.compression !== COMPRESSION) continue;
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
  console.log(`[retry] ${UNITY_VER}/${COMPRESSION}: fixed=${fixed} stillFailing=${stillFailing}`);
}

// cell당 최대 CELL_MAX_ATTEMPTS회 시도. Chromium이 무거운 로드에서 간헐적으로
// 죽어(page/context closed) warm 측정이 누락되므로, 에러 없는 결과를 얻을 때까지
// 재시도한다. 모두 실패하면 마지막(에러 포함) 결과를 기록해 매트릭스는 계속 진행.
const CELL_MAX_ATTEMPTS = 4;

async function measureCellWithRetry({ network, cpu, iter, url, append = true }) {
  const label = `${UNITY_VER}|${COMPRESSION}|${network}|${cpu}|iter${iter}`;
  let result;
  for (let attempt = 1; attempt <= CELL_MAX_ATTEMPTS; attempt++) {
    result = await measureCellOnce({ network, cpu, iter, url, attempt });
    // 에러뿐 아니라 측정 오염(머신 부하로 비정상적으로 느린 값)도 재시도 대상.
    const bad = result.error
      ? result.error
      : isOutlier(result)
        ? `outlier cold=${result.cold_load_ms} warm=${result.warm_load_ms}`
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
  const label = `${UNITY_VER}|${COMPRESSION}|${network}|${cpu}|iter${iter}`;
  console.log(`[cell ${label}] launching browser (attempt ${attempt})`);

  let browser;
  let result = {
    timestamp: new Date().toISOString(),
    unity_version: UNITY_VER,
    compression: COMPRESSION,
    network,
    cpu_rate: cpuRate,
    iteration: iter,
    cold_load_ms: null,
    warm_load_ms: null,
    cold_transfer_bytes: null,
    warm_transfer_bytes: null,
    cold_resource_count: null,
    warm_resource_count: null,
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
    await page.waitForFunction(() => window['unityInstance'] !== undefined, null, {
      timeout: TIMEOUT_MS,
    });
    const coldEndMs = Date.now();
    result.cold_load_ms = coldEndMs - coldStartMs;
    const coldResources = await collectResources(page);
    result.cold_transfer_bytes = coldResources.totalBytes;
    result.cold_resource_count = coldResources.count;

    // Cold 로드 후 캐시 쓰기(HTTP 디스크 캐시 + UnityCache IndexedDB)가 완료될
    // 시간을 준다. 너무 빨리 재방문하면 캐시 기록 전이라 Warm 이득이 사라진다.
    await page.waitForTimeout(2000);

    // Warm — 같은 컨텍스트의 새 탭에서 재방문. reload()는 Chromium에서 캐시된
    // 리소스도 재검증/재요청하므로, 캐시 활용을 측정하려면 새 탭 navigate가 정확.
    await page.close();
    const warmPage = await context.newPage();
    const warmCdp = await context.newCDPSession(warmPage);
    if (cpuRate > 1) {
      await warmCdp.send('Emulation.setCPUThrottlingRate', { rate: cpuRate });
    }
    await warmCdp.send('Network.emulateNetworkConditions', {
      offline: false,
      ...netProfile,
    });

    const warmStartMs = Date.now();
    await warmPage.goto(url, { waitUntil: 'commit', timeout: TIMEOUT_MS });
    await warmPage.waitForFunction(() => window['unityInstance'] !== undefined, null, {
      timeout: TIMEOUT_MS,
    });
    const warmEndMs = Date.now();
    result.warm_load_ms = warmEndMs - warmStartMs;
    const warmResources = await collectResources(warmPage);
    result.warm_transfer_bytes = warmResources.totalBytes;
    result.warm_resource_count = warmResources.count;

    console.log(`[cell ${label}] cold=${result.cold_load_ms}ms warm=${result.warm_load_ms}ms`);
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

function startViteServer(rootDir, port) {
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

        if (decoded.endsWith('.unityweb')) {
          let encoding = compressionCache.get(filePath);
          if (encoding === undefined) {
            encoding = detectUnityWebCompression(filePath);
            compressionCache.set(filePath, encoding);
          }
          if (encoding) headers['Content-Encoding'] = encoding;
          if (decoded.includes('.wasm')) headers['Content-Type'] = 'application/wasm';
          else if (decoded.includes('.js')) headers['Content-Type'] = 'application/javascript';
          else headers['Content-Type'] = 'application/octet-stream';
        } else {
          headers['Content-Type'] = MIME[ext] || 'application/octet-stream';
        }

        // 측정용: 캐싱 허용 (vite.config.ts의 /Build/ no-store와 달리 Warm 측정 가능).
        headers['Cache-Control'] = 'public, max-age=3600';

        // ETag/Last-Modified — HTTP 캐시 검증과 Unity 로더의 UnityCache(IndexedDB)
        // 적중 판정에 필수. 이 헤더가 없으면 Warm reload가 매번 전체를 다시 받는다.
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
