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
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
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
  const serverInfo = await startViteServer(AIT_BUILD, `dist/web-${COMPRESSION}`, PORT);
  console.log(`[benchmark] vite preview ready on port ${serverInfo.port}`);

  try {
    const url = `http://localhost:${serverInfo.port}?e2e=true`;
    for (const network of networksToRun) {
      for (const cpu of cpusToRun) {
        for (let iter = 1; iter <= ITERATIONS; iter++) {
          await measureCell({ network, cpu, iter, url });
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

async function measureCell({ network, cpu, iter, url }) {
  const netProfile = NETWORK_PROFILES[network];
  const cpuRate = CPU_PROFILES[cpu].rate;
  const label = `${UNITY_VER}|${COMPRESSION}|${network}|${cpu}|iter${iter}`;
  console.log(`[cell ${label}] launching browser`);

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

    // Vite의 /Build/ → Cache-Control: no-store 우회. Warm 측정용.
    await page.route('**/*', async (route) => {
      const response = await route.fetch();
      const headers = { ...response.headers() };
      if (headers['cache-control']) delete headers['cache-control'];
      headers['cache-control'] = 'public, max-age=3600';
      route.fulfill({
        response,
        headers,
      });
    });

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

    // Warm — 같은 page에서 reload
    const warmStartMs = Date.now();
    await page.reload({ waitUntil: 'commit', timeout: TIMEOUT_MS });
    await page.waitForFunction(() => window['unityInstance'] !== undefined, null, {
      timeout: TIMEOUT_MS,
    });
    const warmEndMs = Date.now();
    result.warm_load_ms = warmEndMs - warmStartMs;
    const warmResources = await collectResources(page);
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

  fs.appendFileSync(RESULTS_PATH, JSON.stringify(result) + '\n');
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

function startViteServer(cwd, outDir, port) {
  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', 'preview', '--outDir', outDir, '--port', String(port)], {
      cwd,
      stdio: 'pipe',
      shell: true,
      env: { ...process.env, NODE_OPTIONS: '' },
    });

    let started = false;
    let actualPort = port;

    const onReady = () => {
      if (started) return;
      started = true;
      resolve({ process: server, port: actualPort });
    };

    server.stdout.on('data', (data) => {
      const output = data.toString();
      const portMatch = output.match(/(?:Local:\s+http:\/\/localhost:|listening.*?port\s*|:)(\d+)/i);
      if (portMatch) actualPort = parseInt(portMatch[1], 10);
      if (
        output.includes('Local:') ||
        output.includes('listening') ||
        output.includes('Accepting connections') ||
        output.includes('ready')
      ) {
        onReady();
      }
    });

    server.stderr.on('data', (data) => {
      const txt = data.toString();
      if (txt.trim()) console.error('[vite stderr]', txt.trim());
    });

    server.on('error', reject);

    setTimeout(() => {
      if (!started) reject(new Error('vite preview start timeout'));
    }, 30000);
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
