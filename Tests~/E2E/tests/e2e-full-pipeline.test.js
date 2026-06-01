// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK - E2E Full Pipeline Tests
 *
 * 5개 테스트 케이스 (빠른 테스트 → 느린 테스트 순서):
 * 1. Build Validation (build-validation.json 확인 + 메트릭 수집)
 * 2. AIT Dev Server (Vite dev 서버 + Unity 초기화)
 * 3-5. Production Tests (세션 공유로 초기화 1회):
 *   3. Production Server + Preload Metrics (Unity 초기화 + Resource Timing)
 *   4. Runtime API Error Validation (SDK API 에러 검증)
 *   5. Serialization Round-trip Tests (C# ↔ JavaScript 직렬화 검증)
 *
 * Test 3-5 세션 공유:
 * - 서버 1회 시작, Unity 1회 초기화로 반복 초기화 방지
 * - JavaScript 트리거 함수로 테스트 실행 (TriggerAPITest, TriggerSerializationTest)
 */

// ES Module에서 __dirname 대체
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 모바일 에뮬레이션 활성화 여부 (macOS CI에서만 true)
const isMobileEmulation = process.env.MOBILE_EMULATION === 'true';

// CPU 쓰로틀링 배율 (환경변수로 제어, 기본값: 0 = 비활성화)
const cpuThrottleRate = parseInt(process.env.CPU_THROTTLE_RATE || '0', 10);

// 경로 상수
const PROJECT_ROOT = path.resolve(__dirname, '../../..');

// UNITY_PROJECT_PATH 환경변수로 프로젝트 경로 지정 가능
function findSampleProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) {
    return envPath;
  }

  const versionPatterns = ['6000.2', '6000.0', '2022.3', '2021.3'];
  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../SampleUnityProject-${version}`);
    const distPath = path.resolve(projectPath, 'ait-build/dist/web');
    if (fs.existsSync(distPath)) {
      console.log(`📁 Auto-detected project: SampleUnityProject-${version}`);
      return projectPath;
    }
  }

  const legacyPath = path.resolve(__dirname, '../SampleUnityProject');
  if (fs.existsSync(legacyPath)) {
    return legacyPath;
  }

  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../SampleUnityProject-${version}`);
    if (fs.existsSync(projectPath)) {
      return projectPath;
    }
  }

  return path.resolve(__dirname, '../SampleUnityProject');
}

const SAMPLE_PROJECT = findSampleProject();
const AIT_BUILD = path.resolve(SAMPLE_PROJECT, 'ait-build');
const DIST_WEB = path.resolve(AIT_BUILD, 'dist/web');

// 벤치마크 기준
// E2E CI는 AIT_DEVELOPMENT_BUILD=true + AIT_COMPRESSION_FORMAT=0(Disabled)로 빌드하여
// 빌드 wallclock을 단축한다(unity-build.yml 참조).
// Dev Build는 wasm 크기가 Release 대비 2-3배 커지고 압축도 비활성화되므로
// MAX_BUILD_SIZE_MB 단언을 사용할 수 없어 isDevBuild일 때 스킵한다.
const isDevBuild = process.env.AIT_DEVELOPMENT_BUILD === 'true';
const BENCHMARKS = isMobileEmulation ? {
  MAX_LOAD_TIME_MS: 30000,
  MAX_BUILD_SIZE_MB: 50,
} : {
  MAX_LOAD_TIME_MS: 10000,
  MAX_BUILD_SIZE_MB: 50,
};

// 결과 저장용
let testResults = {
  timestamp: new Date().toISOString(),
  tests: {}
};

/**
 * Unity 버전에서 고유 포트 오프셋 계산
 */
function getPortOffsetFromUnityVersion(projectPath) {
  const match = projectPath.match(/SampleUnityProject-(\d+)\.(\d+)/);
  if (!match) return 0;

  const major = parseInt(match[1], 10);
  const minor = parseInt(match[2], 10);

  if (major === 2021) return 0;
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  if (major === 6000 && minor === 3) return 4;
  return 0;
}

const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
const VITE_DEV_PORT = 8081 + PORT_OFFSET;

let serverProcess = null;
let serverPort = 4173 + PORT_OFFSET;
console.log(`📦 Unity project: ${SAMPLE_PROJECT}`);
console.log(`🔌 Server port: ${serverPort} (offset: ${PORT_OFFSET})`);

// ============================================================================
// 유틸리티 함수
// ============================================================================

function directoryExists(dirPath) {
  try {
    return fs.existsSync(dirPath) && fs.statSync(dirPath).isDirectory();
  } catch {
    return false;
  }
}

function fileExists(filePath) {
  try {
    return fs.existsSync(filePath) && fs.statSync(filePath).isFile();
  } catch {
    return false;
  }
}

function getDirectorySizeMB(dirPath) {
  let totalSize = 0;
  function walkDir(currentPath) {
    const files = fs.readdirSync(currentPath);
    for (const file of files) {
      const filePath = path.join(currentPath, file);
      const stats = fs.statSync(filePath);
      if (stats.isDirectory()) {
        walkDir(filePath);
      } else {
        totalSize += stats.size;
      }
    }
  }
  if (directoryExists(dirPath)) {
    walkDir(dirPath);
  }
  return totalSize / (1024 * 1024);
}

function isPortAvailable(port) {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once('error', () => resolve(false));
    server.once('listening', () => {
      server.close(() => resolve(true));
    });
    server.listen(port, '127.0.0.1');
  });
}

async function waitForPortRelease(port, timeoutMs = 10000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPortAvailable(port)) {
      return true;
    }
    await new Promise(r => setTimeout(r, 200));
  }
  return false;
}

async function killServerProcess(proc, ports = []) {
  if (!proc) return;

  const isWindows = process.platform === 'win32';

  try {
    proc.kill('SIGTERM');
  } catch {
    // already exited
  }

  const exited = await new Promise((resolve) => {
    if (proc.exitCode !== null) {
      resolve(true);
      return;
    }
    const timer = setTimeout(() => resolve(false), 3000);
    proc.once('exit', () => {
      clearTimeout(timer);
      resolve(true);
    });
  });

  if (!exited) {
    try {
      proc.kill('SIGKILL');
    } catch {}
    await new Promise(r => setTimeout(r, 1000));
  }

  for (const port of ports) {
    try {
      if (isWindows) {
        execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
      } else {
        execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
      }
    } catch {}
  }

  for (const port of ports) {
    await waitForPortRelease(port, 5000);
  }
}

async function startDevServer(aitBuildDir, defaultPort) {
  const vitePort = VITE_DEV_PORT;

  const myPorts = [serverPort, vitePort];
  const isWindows = process.platform === 'win32';
  for (const port of myPorts) {
    try {
      if (isWindows) {
        execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
      } else {
        execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
      }
    } catch {}
  }

  for (const port of myPorts) {
    await waitForPortRelease(port, 5000);
  }

  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', '--host', '--port', String(vitePort)], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      shell: true,
      env: { ...process.env, NODE_OPTIONS: '' }
    });

    let started = false;
    let actualPort = defaultPort;

    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[vite dev]', output);

      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');
      const portMatch = cleanOutput.match(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });

    server.stderr.on('data', (data) => {
      console.error('[vite dev error]', data.toString());
    });

    server.on('error', reject);

    setTimeout(() => {
      if (!started) {
        started = true;
        resolve({ process: server, port: actualPort });
      }
    }, 10000);
  });
}

async function startProductionServer(aitBuildDir, defaultPort) {
  const isWindows = process.platform === 'win32';
  const myPort = serverPort;
  try {
    if (isWindows) {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${myPort} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      execSync(`lsof -ti:${myPort} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {}

  await waitForPortRelease(myPort, 5000);

  return new Promise((resolve, reject) => {
    const server = spawn('pnpx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(defaultPort)], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      shell: true,
      env: { ...process.env, NODE_OPTIONS: '' }
    });

    let started = false;
    let actualPort = defaultPort;

    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[vite preview]', output);

      const portMatch = output.match(/(?:Local:\s+http:\/\/localhost:|listening.*?port\s*|:)(\d+)/i);
      if (portMatch) {
        actualPort = parseInt(portMatch[1], 10);
      }

      if (output.includes('Local:') || output.includes('listening') || output.includes('Accepting connections') || output.includes('ready')) {
        if (!started) {
          started = true;
          resolve({ process: server, port: actualPort });
        }
      }
    });

    server.stderr.on('data', (data) => {
      console.error('[vite preview error]', data.toString());
    });

    server.on('error', reject);

    setTimeout(() => {
      if (!started) {
        started = true;
        resolve({ process: server, port: actualPort });
      }
    }, 10000);
  });
}

function checkForPlaceholders(content) {
  const placeholderPatterns = [
    /%UNITY_[A-Z_]+%/g,
    /%AIT_[A-Z_]+%/g
  ];

  const found = [];
  for (const pattern of placeholderPatterns) {
    const matches = content.match(pattern);
    if (matches) {
      found.push(...matches);
    }
  }
  return [...new Set(found)];
}

async function applyMobileThrottling(page, overrideRate = undefined) {
  const rate = overrideRate !== undefined ? overrideRate :
               (isMobileEmulation ? 4 : cpuThrottleRate);

  if (rate <= 0 && !isMobileEmulation) {
    return null;
  }

  const client = await page.context().newCDPSession(page);

  if (rate > 0) {
    await client.send('Emulation.setCPUThrottlingRate', { rate });
  }

  if (isMobileEmulation) {
    await client.send('Network.emulateNetworkConditions', {
      offline: false,
      downloadThroughput: 12 * 1024 * 1024 / 8,
      uploadThroughput: 6 * 1024 * 1024 / 8,
      latency: 70
    });
  }

  return client;
}


// ============================================================================
// 로딩 프로파일러 (첫 프레임 렌더까지의 로드 타임라인 측정)
// ============================================================================

/**
 * 페이지 컨텍스트에 주입되는 로딩 프로파일러.
 * page.addInitScript로 navigation 이전에 설치되어 다음을 정밀 측정한다(performance.now 기준):
 *  - instanceReadyMs : 템플릿이 `window.unityInstance = ...`를 세팅한 시각(엔진 준비 완료)
 *  - firstFrameMs    : Unity 엔진이 캔버스에 최초로 그린 시각(첫 프레임 렌더 성공) = 헤드라인 지표
 *
 * 첫 프레임은 WebGL draw 콜을 프로토타입 레벨에서 후킹해 감지한다(최초 1회 기록 후 자가 제거 →
 * 이후 오버헤드 0). AIT 템플릿의 로딩 화면은 HTML 오버레이라 캔버스에 그리지 않으므로,
 * 최초 draw 콜 = 엔진의 첫 프레임이다.
 *
 * ⚠️ 이 함수는 브라우저 컨텍스트에서 실행되므로 Node 변수에 클로저로 접근하면 안 된다(self-contained).
 */
function installLoadProfiler() {
  if (window['__AIT_LOAD_PROFILE__']) return;
  const profile = {
    timeOrigin: performance.timeOrigin,
    instanceReadyMs: null,
    firstFrameMs: null,
    firstDrawMethod: null,
    progress: [],
  };
  window['__AIT_LOAD_PROFILE__'] = profile;
  const now = () => +performance.now().toFixed(1);

  // 1) window.unityInstance 세터 트랩 → 엔진 인스턴스 준비 시각.
  //    템플릿의 `window.unityInstance = unityInstance`는 [[Set]]이라 접근자 세터로 가로챌 수 있다.
  try {
    let _inst;
    Object.defineProperty(window, 'unityInstance', {
      configurable: true,
      get() { return _inst; },
      set(v) {
        _inst = v;
        if (profile.instanceReadyMs == null) profile.instanceReadyMs = now();
      },
    });
  } catch (e) { /* 이미 정의된 경우 무시 */ }

  // 2) WebGL draw 콜 후킹 → 최초 프레임 렌더 시각(자가 제거).
  //    ⚠️ WebGL2 컨텍스트의 drawArrays/drawElements는 WebGL2RenderingContext.prototype의
  //    고유 메서드라 WebGLRenderingContext.prototype 후킹만으로는 가로채지지 않는다 →
  //    기본 draw 메서드를 두 프로토타입 모두에 후킹한다(가장 derived가 우선 적용됨).
  try {
    const targets = [];
    const collect = (proto, names) => {
      if (!proto) return;
      for (const n of names) {
        if (typeof proto[n] === 'function') targets.push([proto, n, proto[n]]);
      }
    };
    collect(window.WebGLRenderingContext && window.WebGLRenderingContext.prototype,
      ['drawArrays', 'drawElements']);
    collect(window.WebGL2RenderingContext && window.WebGL2RenderingContext.prototype,
      ['drawArrays', 'drawElements', 'drawArraysInstanced', 'drawElementsInstanced', 'drawRangeElements']);

    const restore = () => {
      for (const [p, n, orig] of targets) {
        try { p[n] = orig; } catch (e) { /* noop */ }
      }
    };

    for (const [proto, name, orig] of targets) {
      proto[name] = function (...args) {
        if (profile.firstFrameMs == null) {
          profile.firstFrameMs = now();
          profile.firstDrawMethod = name;
          restore();
        }
        return orig.apply(this, args);
      };
    }
  } catch (e) { /* 후킹 실패 시 firstFrameMs는 null로 남고 테스트가 실패시킨다 */ }
}

/**
 * 페이지에서 로딩 프로파일 + Resource/Navigation Timing을 읽어 구조화된 객체로 반환.
 * Unity 빌드 자산의 on-wire 전송 바이트(transferSize)·압축/비압축 바디·다운로드 구간을 포함한다.
 */
async function captureLoadProfile(page) {
  return await page.evaluate(() => {
    const prof = window['__AIT_LOAD_PROFILE__'] || {};
    const nav = performance.getEntriesByType('navigation')[0];
    const isUnityAsset = (name) =>
      /\/Build\//.test(name) ||
      /\.(unityweb|wasm|data|mem)$/.test(name) ||
      /\.(loader|framework)\.js$/.test(name);

    const resources = performance.getEntriesByType('resource')
      .filter((r) => isUnityAsset(r.name))
      .map((r) => ({
        name: r.name.split('/').pop().split('?')[0],
        initiatorType: r.initiatorType,
        startMs: +r.startTime.toFixed(1),
        responseEndMs: +r.responseEnd.toFixed(1),
        durationMs: +r.duration.toFixed(1),
        ttfbMs: +Math.max(0, r.responseStart - r.requestStart).toFixed(1),
        transferSize: r.transferSize,        // on-wire(헤더 포함)
        encodedBodySize: r.encodedBodySize,  // 압축 바디
        decodedBodySize: r.decodedBodySize,  // 비압축 바디
      }))
      .sort((a, b) => a.startMs - b.startMs);

    const sum = (key) => resources.reduce((s, r) => s + (r[key] || 0), 0);

    return {
      firstFrameMs: prof.firstFrameMs ?? null,
      instanceReadyMs: prof.instanceReadyMs ?? null,
      firstDrawMethod: prof.firstDrawMethod ?? null,
      navigation: nav ? {
        domContentLoadedMs: +nav.domContentLoadedEventEnd.toFixed(1),
        domCompleteMs: +nav.domComplete.toFixed(1),
        loadEventEndMs: +nav.loadEventEnd.toFixed(1),
        responseEndMs: +nav.responseEnd.toFixed(1),
      } : null,
      resources,
      totalTransferBytes: sum('transferSize'),
      totalEncodedBytes: sum('encodedBodySize'),
      totalDecodedBytes: sum('decodedBodySize'),
    };
  });
}

function formatBytes(n) {
  if (n == null) return 'N/A';
  if (n >= 1024 * 1024) return (n / (1024 * 1024)).toFixed(2) + ' MB';
  if (n >= 1024) return (n / 1024).toFixed(1) + ' KB';
  return n + ' B';
}

/** 베이스라인(이전 profile) 대비 before/after 델타를 콘솔에 출력. */
function printLoadDiff(baseline, current) {
  if (!baseline) return;
  const b = baseline.profile || baseline;
  const fmtDelta = (before, after, unit, lowerIsBetter = true) => {
    if (before == null || after == null) return `${after ?? 'N/A'} (baseline N/A)`;
    const d = after - before;
    const pct = before !== 0 ? (d / before) * 100 : 0;
    const sign = d > 0 ? '+' : '';
    const better = lowerIsBetter ? (d < 0) : (d > 0);
    const tag = d === 0 ? '=' : (better ? '✓ 개선' : '✗ 회귀');
    return `${before}${unit} → ${after}${unit}  (${sign}${d.toFixed(1)}${unit}, ${sign}${pct.toFixed(1)}%) ${tag}`;
  };
  console.log('\n  ── Before → After (baseline diff) ──');
  console.log('  첫 프레임:        ' + fmtDelta(b.firstFrameMs, current.firstFrameMs, 'ms'));
  console.log('  인스턴스 준비:    ' + fmtDelta(b.instanceReadyMs, current.instanceReadyMs, 'ms'));
  console.log('  on-wire 전송:     ' + fmtDelta(
    b.totalTransferBytes != null ? +(b.totalTransferBytes / 1048576).toFixed(2) : null,
    current.totalTransferBytes != null ? +(current.totalTransferBytes / 1048576).toFixed(2) : null,
    'MB'));
}


// ============================================================================
// Test Suite
// ============================================================================

test.describe('Apps in Toss Unity SDK E2E Pipeline', () => {

  test.beforeAll(async () => {
    console.log('🚀 E2E Pipeline Tests Starting...');
    console.log(`📁 Project Root: ${PROJECT_ROOT}`);
    console.log(`📁 Sample Project: ${SAMPLE_PROJECT}`);
    console.log(`📁 AIT Build: ${AIT_BUILD}`);
  });

  test.afterAll(async () => {
    if (serverProcess) {
      serverProcess.kill();
      serverProcess = null;
    }

    // 1. 전체 테스트 결과
    const resultsPath = path.resolve(__dirname, 'e2e-test-results.json');
    fs.writeFileSync(resultsPath, JSON.stringify(testResults, null, 2));

    // 2. 벤치마크 결과 (workflow에서 업로드하는 파일)
    const benchmarkPath = path.resolve(__dirname, 'benchmark-results.json');
    const benchmarkResults = {
      timestamp: testResults.timestamp,
      unityProject: SAMPLE_PROJECT,
      buildSize: testResults.tests['1_build_validation']?.buildSizeMB,
      pageLoadTime: testResults.tests['3_production_server']?.pageLoadTimeMs,
      unityLoadTime: testResults.tests['3_production_server']?.unityLoadTimeMs,
      webgl: testResults.tests['3_production_server']?.webgl,
      apiTestResults: testResults.tests['4_runtime_api'] ? {
        totalAPIs: testResults.tests['4_runtime_api'].totalAPIs,
        successCount: testResults.tests['4_runtime_api'].successCount,
        unexpectedErrorCount: testResults.tests['4_runtime_api'].unexpectedErrorCount
      } : null,
      compressionValidation: testResults.tests['1_build_validation']?.compressionValidation || null,
      // 로딩 프로파일 헤드라인 (before/after diff용)
      firstFrameMs: testResults.tests['3-2_load_profile']?.firstFrameMs ?? null,
      instanceReadyMs: testResults.tests['3-2_load_profile']?.instanceReadyMs ?? null,
      onWireTransferBytes: testResults.tests['3-2_load_profile']?.totalTransferBytes ?? null,
      testsPassed: Object.values(testResults.tests || {}).filter(t => t.passed).length,
      testsTotal: Object.keys(testResults.tests || {}).length
    };
    fs.writeFileSync(benchmarkPath, JSON.stringify(benchmarkResults, null, 2));

    // 3. 로딩 프로파일 (before/after diff 베이스라인용 전용 산출물)
    const loadProfile = testResults.tests['3-2_load_profile'];
    if (loadProfile) {
      const loadProfilePath = path.resolve(__dirname, 'load-profile.json');
      const { passed, ...profile } = loadProfile;
      fs.writeFileSync(loadProfilePath, JSON.stringify({
        timestamp: testResults.timestamp,
        unityProject: SAMPLE_PROJECT,
        isDevBuild,
        profile,
      }, null, 2));
    }

    // stdout으로 결과 출력
    console.log('\n');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📊 E2E Test Results');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');

    const tests = testResults.tests || {};
    const passed = Object.values(tests).filter(t => t.passed).length;
    const total = Object.keys(tests).length;

    console.log(`\n  ✅ Tests Passed: ${passed}/${total}`);

    const buildSize = tests['1_build_validation']?.buildSizeMB;
    const pageLoad = tests['3_production_server']?.pageLoadTimeMs;
    const unityLoad = tests['3_production_server']?.unityLoadTimeMs;
    const renderer = tests['3_production_server']?.webgl?.renderer;
    const firstFrame = tests['3-2_load_profile']?.firstFrameMs;
    const onWire = tests['3-2_load_profile']?.totalTransferBytes;

    console.log('\n  📦 Build Size:      ' + (buildSize ? buildSize.toFixed(2) + ' MB' : 'N/A'));
    console.log('  ⏱️  Page Load:       ' + (pageLoad ? pageLoad + ' ms' : 'N/A'));
    console.log('  🎮 Unity Load:      ' + (unityLoad ? unityLoad + ' ms' : 'N/A'));
    console.log('  🎬 First Frame:     ' + (firstFrame != null ? firstFrame + ' ms' : 'N/A') + '  ← headline');
    console.log('  📡 On-wire:         ' + (onWire != null ? formatBytes(onWire) : 'N/A'));
    console.log('  🖥️  GPU Renderer:    ' + (renderer || 'N/A'));

    console.log('\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📄 Full Results (JSON):');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log(JSON.stringify(testResults, null, 2));
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n');
  });


  // -------------------------------------------------------------------------
  // Test 1: Build Validation (build-validation.json 확인 + 메트릭 수집)
  // 기존 Tests 1, 3, 4를 통합 - C# BuildOutputValidator가 생성한 결과를 확인
  // -------------------------------------------------------------------------
  test('1. Build validation should pass', async () => {
    test.setTimeout(60000);

    // build-validation.json 확인 (C# BuildOutputValidator가 빌드 후 생성)
    const validationPath = path.resolve(AIT_BUILD, 'build-validation.json');

    if (fileExists(validationPath)) {
      const validation = JSON.parse(fs.readFileSync(validationPath, 'utf-8'));
      console.log(`📋 Build validation: ${validation.passed ? 'PASSED' : 'FAILED'}`);
      console.log(`   Build size: ${validation.buildSizeMB?.toFixed(2)} MB`);
      console.log(`   Compression: ${validation.compressionFormat}`);
      console.log(`   Files: ${validation.fileCount}`);

      if (validation.errors?.length > 0) {
        console.log(`   Errors:`);
        validation.errors.forEach(e => console.log(`     ❌ ${e}`));
      }
      if (validation.warnings?.length > 0) {
        console.log(`   Warnings:`);
        validation.warnings.forEach(w => console.log(`     ⚠️ ${w}`));
      }

      // AIT_COMPRESSION_FORMAT 매핑: 0=disabled, 1=gzip, 2=brotli, -1=auto(brotli),
      // 미설정 시 사용자 PlayerSettings 따름 — null로 기록.
      const compressionFormatEnvMap = { '0': 'disabled', '1': 'gzip', '2': 'brotli', '-1': 'brotli' };
      const expectedCompressionFormat = compressionFormatEnvMap[process.env.AIT_COMPRESSION_FORMAT] ?? null;

      testResults.tests['1_build_validation'] = {
        passed: validation.passed,
        buildSizeMB: validation.buildSizeMB,
        compressionFormat: validation.compressionFormat,
        fileCount: validation.fileCount,
        compressionValidation: {
          detectedFormat: validation.compressionFormat,
          expectedFormat: expectedCompressionFormat
        }
      };

      expect(validation.passed, 'Build validation should pass').toBe(true);
      // Dev Build + 압축 비활성화 조합에서는 산출물이 50MB 한도를 초과하므로 size 단언 스킵.
      // 배포 빌드에는 영향 없음 (production 워크플로우는 사용자 PlayerSettings/압축 따름).
      if (!isDevBuild) {
        expect(validation.buildSizeMB).toBeLessThanOrEqual(BENCHMARKS.MAX_BUILD_SIZE_MB);
      }
    } else {
      // build-validation.json이 없는 경우 직접 검증 (이전 버전 호환)
      console.log('⚠️ build-validation.json not found, performing direct validation...');

      expect(directoryExists(AIT_BUILD), 'ait-build/ should exist').toBe(true);
      expect(directoryExists(DIST_WEB), 'ait-build/dist/web/ should exist').toBe(true);

      // package.json
      expect(fileExists(path.resolve(AIT_BUILD, 'package.json')), 'package.json should exist').toBe(true);

      // granite.config.ts 플레이스홀더
      const graniteConfigPath = path.resolve(AIT_BUILD, 'granite.config.ts');
      if (fileExists(graniteConfigPath)) {
        const content = fs.readFileSync(graniteConfigPath, 'utf-8');
        const placeholders = checkForPlaceholders(content);
        expect(placeholders.length, 'Should have no unsubstituted placeholders in granite.config.ts').toBe(0);
      }

      // node_modules
      expect(directoryExists(path.resolve(AIT_BUILD, 'node_modules')), 'node_modules/ should exist').toBe(true);

      // index.html 플레이스홀더
      const indexPath = path.resolve(DIST_WEB, 'index.html');
      expect(fileExists(indexPath), 'index.html should exist').toBe(true);
      const indexContent = fs.readFileSync(indexPath, 'utf-8');
      const indexPlaceholders = checkForPlaceholders(indexContent);
      expect(indexPlaceholders.length, 'index.html should have no unsubstituted placeholders').toBe(0);

      // Build 폴더
      const buildPath = path.resolve(DIST_WEB, 'Build');
      expect(directoryExists(buildPath), 'Build/ folder should exist').toBe(true);

      const distSizeMB = getDirectorySizeMB(DIST_WEB);

      testResults.tests['1_build_validation'] = {
        passed: true,
        buildSizeMB: distSizeMB,
      };
    }
  });


  // -------------------------------------------------------------------------
  // Test 2: AIT Dev Server (vite)
  // -------------------------------------------------------------------------
  test('2. AIT dev server should start and load Unity', async ({ page }) => {
    test.setTimeout(120000);

    await applyMobileThrottling(page);

    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist for dev server').toBe(true);

    console.log('🚀 Starting dev server (vite)...');
    const devServer = await startDevServer(AIT_BUILD, serverPort);
    serverProcess = devServer.process;
    const actualPort = devServer.port;

    // 서버가 준비될 때까지 대기
    let serverReady = false;
    for (let i = 0; i < 30; i++) {
      try {
        const response = await fetch(`http://localhost:${actualPort}/`, { method: 'HEAD' });
        if (response.ok) {
          serverReady = true;
          break;
        }
      } catch {}
      await new Promise(r => setTimeout(r, 500));
    }

    if (!serverReady) {
      const tryPorts = [5173, 8081, 3000];
      for (const port of tryPorts) {
        if (port === actualPort) continue;
        try {
          const response = await fetch(`http://localhost:${port}/`, { method: 'HEAD' });
          if (response.ok) {
            serverReady = true;
            break;
          }
        } catch {}
      }
    }

    const workingPort = serverReady ? actualPort : await (async () => {
      const tryPorts = [actualPort, 5173, 8081, 3000];
      for (const port of tryPorts) {
        try {
          const response = await fetch(`http://localhost:${port}/`, { method: 'HEAD' });
          if (response.ok) return port;
        } catch {}
      }
      return null;
    })();

    if (!workingPort) {
      throw new Error(`Dev server failed to start on any port (tried: ${actualPort}, 5173, 8081, 3000)`);
    }

    const startTime = Date.now();
    const response = await page.goto(`http://localhost:${workingPort}?e2e=true`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000
    });

    expect(response?.status()).toBe(200);

    const hasUnityLoader = await page.evaluate(() => {
      return typeof window['createUnityInstance'] === 'function' ||
             document.querySelector('script[src*="loader.js"]') !== null ||
             document.body.innerHTML.includes('createUnityInstance');
    });

    console.log(`🎮 Unity loader present: ${hasUnityLoader}`);

    try {
      await page.waitForFunction(() => {
        return window['unityInstance'] !== undefined ||
               document.querySelector('canvas') !== null;
      }, { timeout: 60000 });
      console.log('✅ Unity instance initialized');
    } catch {
      console.log('⚠️ Unity instance not initialized within timeout (may be expected in CI)');
    }

    const loadTime = Date.now() - startTime;

    await killServerProcess(serverProcess, [VITE_DEV_PORT, serverPort]);
    serverProcess = null;

    testResults.tests['2_dev_server'] = {
      passed: true,
      loadTimeMs: loadTime
    };
  });


  // -------------------------------------------------------------------------
  // Tests 3-5: Production Server + Runtime Tests (세션 공유)
  // -------------------------------------------------------------------------
  test.describe.serial('Production Tests (shared session)', () => {
    /** @type {import('@playwright/test').Page} */
    let sharedPage = null;
    let sharedServerProcess = null;
    let sharedPort = serverPort;
    let pageLoadTime = 0;
    let unityLoadTime = 0;
    /** @type {Awaited<ReturnType<typeof captureLoadProfile>> | null} */
    let loadProfile = null;
    const preloadWarnings = [];

    test.beforeAll(async ({ browser }) => {
      console.log('\n' + '='.repeat(70));
      console.log('🚀 STARTING SHARED SESSION FOR TESTS 3-5');
      console.log('='.repeat(70));

      expect(directoryExists(DIST_WEB), 'dist/web/ should exist for production server').toBe(true);

      // 1. Production 서버 시작
      const prodServer = await startProductionServer(AIT_BUILD, serverPort);
      sharedServerProcess = prodServer.process;
      sharedPort = prodServer.port;

      let serverReady = false;
      for (let i = 0; i < 20; i++) {
        try {
          const response = await fetch(`http://localhost:${sharedPort}/`, { method: 'HEAD' });
          if (response.ok) {
            serverReady = true;
            break;
          }
        } catch {}
        await new Promise(r => setTimeout(r, 500));
      }

      if (!serverReady) {
        throw new Error(`Server failed to start on port ${sharedPort}`);
      }

      // 2. 페이지 생성 + Unity 초기화
      sharedPage = await browser.newPage();

      // 로딩 프로파일러를 navigation 이전에 주입 (첫 프레임/인스턴스 준비 시각 정밀 측정)
      await sharedPage.addInitScript(installLoadProfiler);

      sharedPage.on('console', msg => {
        if (msg.type() === 'warning' && msg.text().includes('credentials mode')) {
          preloadWarnings.push(msg.text());
        }
      });

      const startTime = Date.now();
      const response = await sharedPage.goto(`http://localhost:${sharedPort}?e2e=true`, {
        waitUntil: 'networkidle',
        timeout: 90000
      });

      expect(response?.status()).toBe(200);
      pageLoadTime = Date.now() - startTime;

      const unityStartTime = Date.now();
      try {
        await sharedPage.waitForFunction(() => {
          return window['unityInstance'] !== undefined;
        }, { timeout: 120000 });
        unityLoadTime = Date.now() - unityStartTime;
        console.log(`✅ Unity instance ready in ${unityLoadTime}ms`);
      } catch {
        unityLoadTime = Date.now() - unityStartTime;
        console.log('⚠️ Unity initialization timeout');
      }

      // 첫 프레임 렌더(최초 WebGL draw) 감지 — 헤드라인 로드 지표. 미감지 시 로드 실패로 간주.
      try {
        await sharedPage.waitForFunction(
          () => window['__AIT_LOAD_PROFILE__'] && window['__AIT_LOAD_PROFILE__'].firstFrameMs != null,
          { timeout: 30000 }
        );
      } catch {
        console.log('⚠️ First frame(WebGL draw) not detected within timeout');
      }

      // 로딩 프로파일 캡처 (resource/navigation timing 포함)
      try {
        loadProfile = await captureLoadProfile(sharedPage);
        if (loadProfile?.firstFrameMs != null) {
          console.log(`🎬 First frame rendered at ${loadProfile.firstFrameMs}ms (nav start 기준, via ${loadProfile.firstDrawMethod})`);
        }
      } catch (e) {
        console.log('⚠️ Load profile capture failed:', e?.message);
      }

      try {
        await sharedPage.waitForFunction(() => {
          return typeof window['TriggerAPITest'] === 'function';
        }, { timeout: 10000 });
        console.log('✅ Trigger functions registered');
      } catch {
        console.log('⚠️ Trigger functions not found (tests may use auto-run)');
      }

      console.log('='.repeat(70) + '\n');
    });

    test.afterAll(async () => {
      if (sharedPage) {
        await sharedPage.close();
        sharedPage = null;
      }

      await killServerProcess(sharedServerProcess, [sharedPort]);
      sharedServerProcess = null;
    });


    // -------------------------------------------------------------------------
    // Test 3: Production Server + Load Metrics
    // 기존 Tests 5, 9 통합
    // -------------------------------------------------------------------------
    test('3. Production build should load with correct metrics', async () => {
      test.setTimeout(60000);

      // WebGL 지원 확인
      const webglInfo = await sharedPage.evaluate(() => {
        const canvas = document.createElement('canvas');
        const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
        if (!gl) return { supported: false };

        const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
        return {
          supported: true,
          renderer: debugInfo ? gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) : 'unknown',
          vendor: debugInfo ? gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) : 'unknown'
        };
      });

      console.log(`🎨 WebGL: ${JSON.stringify(webglInfo)}`);
      console.log(`⏱️ Page load: ${pageLoadTime}ms, Unity load: ${unityLoadTime}ms`);

      expect(webglInfo.supported, 'WebGL should be supported').toBe(true);

      expect(preloadWarnings.length,
        'Early fetch should not cause credentials mode mismatch warnings').toBe(0);

      testResults.tests['3_production_server'] = {
        passed: true,
        pageLoadTimeMs: pageLoadTime,
        unityLoadTimeMs: unityLoadTime,
        webgl: webglInfo
      };
    });


    // -------------------------------------------------------------------------
    // Test 3-2: Load Profiling — 첫 프레임 렌더까지의 로드 타임라인
    // before/after diff용 정밀 프로파일을 자동 수집 (헤드라인 = firstFrameMs)
    // -------------------------------------------------------------------------
    test('3-2. Load profiling: first-frame render time', async () => {
      test.setTimeout(60000);

      expect(loadProfile, 'load profile should have been captured in beforeAll').not.toBeNull();

      // 첫 프레임이 감지되어야 한다(미감지 = WebGL 렌더 실패 = 로드 실패).
      // wasm2023 미지원 브라우저 하드페일도 여기서 잡힌다.
      expect(
        loadProfile.firstFrameMs,
        'first frame(WebGL draw)가 감지되어야 합니다 — 미감지 시 엔진 렌더 실패/하드페일'
      ).not.toBeNull();

      // 구조화된 프로파일 표 출력
      console.log('\n  ━━━━━━━━━━━━━━━━ 🎬 Load Profile (nav start = 0ms) ━━━━━━━━━━━━━━━━');
      console.log(`  첫 프레임 렌더(headline): ${loadProfile.firstFrameMs} ms  (via ${loadProfile.firstDrawMethod})`);
      console.log(`  엔진 인스턴스 준비:       ${loadProfile.instanceReadyMs ?? 'N/A'} ms`);
      if (loadProfile.navigation) {
        console.log(`  DOMContentLoaded:         ${loadProfile.navigation.domContentLoadedMs} ms`);
        console.log(`  load 이벤트:              ${loadProfile.navigation.loadEventEndMs} ms`);
      }
      console.log(`  on-wire 전송 합계:        ${formatBytes(loadProfile.totalTransferBytes)} (압축 ${formatBytes(loadProfile.totalEncodedBytes)} / 비압축 ${formatBytes(loadProfile.totalDecodedBytes)})`);
      console.log('  ── 자산별 다운로드 ──');
      console.log('  ' + 'asset'.padEnd(34) + 'on-wire'.padStart(11) + 'decoded'.padStart(11) + 'start'.padStart(9) + 'dur'.padStart(8));
      for (const r of loadProfile.resources) {
        console.log(
          '  ' + r.name.slice(0, 33).padEnd(34) +
          formatBytes(r.transferSize).padStart(11) +
          formatBytes(r.decodedBodySize).padStart(11) +
          (r.startMs + 'ms').padStart(9) +
          (r.durationMs + 'ms').padStart(8)
        );
      }
      console.log('  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');

      // 베이스라인이 주어지면 before/after 델타 자동 출력
      const baselinePath = process.env.AIT_LOAD_BASELINE;
      if (baselinePath && fs.existsSync(baselinePath)) {
        try {
          const baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
          console.log(`\n  📐 Baseline: ${baselinePath}`);
          printLoadDiff(baseline, loadProfile);
        } catch (e) {
          console.log(`  ⚠️ Baseline 읽기 실패: ${e?.message}`);
        }
      }

      // 합리적 상한 단언 (모바일 30s / 데스크톱 10s). 회귀 가드.
      expect(
        loadProfile.firstFrameMs,
        `first-frame가 ${BENCHMARKS.MAX_LOAD_TIME_MS}ms 이내여야 합니다`
      ).toBeLessThan(BENCHMARKS.MAX_LOAD_TIME_MS);

      testResults.tests['3-2_load_profile'] = {
        passed: true,
        ...loadProfile,
      };
    });


    // -------------------------------------------------------------------------
    // Test 3-1: Page Reload Crash Test (cache warm)
    // -------------------------------------------------------------------------
    test('3-1. Page reload should not crash (cache warm)', async () => {
      test.setTimeout(120000);
      const reloadErrors = [];
      const handler = err => reloadErrors.push(err.message);
      sharedPage.on('pageerror', handler);

      try {
        const resp = await sharedPage.reload({ waitUntil: 'networkidle', timeout: 90000 });
        expect(resp?.status()).toBe(200);

        await sharedPage.waitForFunction(
          () => window['unityInstance'] !== undefined, { timeout: 120000 });

        const abortErrors = reloadErrors.filter(e => e.includes('ERR_ABORTED'));
        expect(abortErrors.length, 'No ERR_ABORTED errors on reload').toBe(0);
        testResults.tests['3_1_reload'] = { passed: true };
      } finally {
        sharedPage.off('pageerror', handler);
      }
    });


    // -------------------------------------------------------------------------
    // Test 4: Runtime API Error Validation
    // -------------------------------------------------------------------------
    test('4. All SDK APIs should return correct errors in dev environment', async () => {
      test.setTimeout(180000);

      console.log('🔄 Triggering API tests via JavaScript...');

      const apiResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          if (window['__E2E_API_TEST_DATA__']) {
            resolve(window['__E2E_API_TEST_DATA__']);
            return;
          }

          const handler = (event) => {
            window.removeEventListener('e2e-api-test-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-api-test-complete', handler);

          if (typeof window['TriggerAPITest'] === 'function') {
            window['TriggerAPITest']();
          }

          setTimeout(() => resolve(null), 120000);
        });
      });

      if (apiResults) {
        let results = apiResults;
        if (typeof results === 'string') {
          try { results = JSON.parse(results); } catch {}
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 SDK API ERROR VALIDATION RESULTS');
        console.log('='.repeat(70));
        console.log(`   Total APIs Tested: ${results.totalAPIs}`);
        console.log(`   Success: ${results.successCount}`);
        console.log(`   Unexpected Errors: ${results.unexpectedErrorCount || 0}`);
        console.log('='.repeat(70));

        if (results.results) {
          const unexpectedErrors = results.results.filter(r => !r.success);
          if (unexpectedErrors.length > 0) {
            console.log('\n❌ APIs with UNEXPECTED Errors:');
            unexpectedErrors.forEach(r => {
              console.log(`   [FAIL] ${r.apiName}: ${r.error}`);
            });
          }
        }

        const unexpectedErrorCount = results.unexpectedErrorCount || 0;

        testResults.tests['4_runtime_api'] = {
          passed: unexpectedErrorCount === 0,
          totalAPIs: results.totalAPIs,
          successCount: results.successCount,
          expectedErrorCount: results.expectedErrorCount || 0,
          unexpectedErrorCount: unexpectedErrorCount,
          results: results.results || []
        };

        expect(unexpectedErrorCount, 'All APIs should return expected errors or succeed').toBe(0);
      } else {
        testResults.tests['4_runtime_api'] = {
          passed: false,
          reason: 'RuntimeAPITester results not received'
        };
        expect(apiResults, 'RuntimeAPITester should return results').not.toBeNull();
      }
    });


    // -------------------------------------------------------------------------
    // Test 5: Serialization Round-trip Tests
    // -------------------------------------------------------------------------
    test('5. Serialization round-trip should succeed for all types', async () => {
      test.setTimeout(180000);

      console.log('🔄 Triggering serialization tests via JavaScript...');

      const serializationResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          if (window['__E2E_SERIALIZATION_TEST_DATA__']) {
            resolve(window['__E2E_SERIALIZATION_TEST_DATA__']);
            return;
          }

          const handler = (event) => {
            window.removeEventListener('e2e-serialization-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-serialization-complete', handler);

          if (typeof window['TriggerSerializationTest'] === 'function') {
            window['TriggerSerializationTest']();
          }

          setTimeout(() => resolve(null), 90000);
        });
      });

      if (serializationResults) {
        let results = serializationResults;
        if (typeof results === 'string') {
          try { results = JSON.parse(results); } catch {}
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 SERIALIZATION ROUND-TRIP TEST RESULTS');
        console.log('='.repeat(70));
        console.log(`   Total Tests: ${results.totalTests}`);
        console.log(`   Success: ${results.successCount}`);
        console.log(`   Failed: ${results.failCount}`);
        console.log('='.repeat(70));

        if (results.results && Array.isArray(results.results)) {
          const failed = results.results.filter(r => !r.success);
          if (failed.length > 0) {
            console.log('\n❌ Failed Tests:');
            failed.forEach(r => {
              console.log(`   [FAIL] ${r.testName}: ${r.error || 'unknown error'}`);
            });
          }
        }

        testResults.tests['5_serialization'] = {
          passed: results.failCount === 0,
          totalTests: results.totalTests,
          successCount: results.successCount,
          failCount: results.failCount
        };

        expect(results.failCount, 'All serialization tests should pass').toBe(0);
      } else {
        testResults.tests['5_serialization'] = {
          passed: false,
          reason: 'SerializationTester results not received'
        };
        expect(serializationResults, 'SerializationTester should return results').not.toBeNull();
      }
    });

    // -------------------------------------------------------------------------
    // Test 6: Build Customization Tutorial #1 — canvas-confetti
    // BuildConfig~/src/main.ts 가 번들링되어 confetti 가 발사되었는지 검증
    // (Docs~/BuildCustomization.md 튜토리얼 #1)
    // -------------------------------------------------------------------------
    test('6. Tutorial #1: canvas-confetti should fire after page load', async () => {
      test.setTimeout(30000);

      const confettiFired = await sharedPage.waitForFunction(
        () => window['__TUTORIAL_CONFETTI_FIRED__'] === true,
        { timeout: 15000 }
      ).then(() => true).catch(() => false);

      console.log(`🎉 Confetti fired: ${confettiFired}`);

      testResults.tests['6_tutorial_confetti'] = {
        passed: confettiFired
      };

      expect(confettiFired, 'window.__TUTORIAL_CONFETTI_FIRED__ should become true (main.ts bundled and load handler executed)').toBe(true);
    });


    // -------------------------------------------------------------------------
    // Test 7: Build Customization Tutorial #2 — Firebase
    // VITE_FIREBASE_* 환경변수가 주입되어 firebase/app 이 초기화되었는지 검증
    // (Docs~/BuildCustomization.md 튜토리얼 #2)
    //
    // 환경변수가 없으면(로컬 개발) 초기화 시도를 건너뛰므로 skip 처리.
    // CI 에서는 GitHub Secret 으로 주입되어 모든 단계가 통과해야 한다.
    // -------------------------------------------------------------------------
    test('7. Tutorial #2: Firebase should initialize when secrets are provided', async () => {
      test.setTimeout(30000);

      const state = await sharedPage.evaluate(() => ({
        initialized: window['__TUTORIAL_FIREBASE_INITIALIZED__'] === true,
        analyticsReady: window['__TUTORIAL_FIREBASE_ANALYTICS_READY__'] === true,
        error: window['__TUTORIAL_FIREBASE_ERROR__'] || null,
      }));

      console.log(`🔥 Firebase state: ${JSON.stringify(state)}`);

      const secretsProvided = !state.error || !state.error.includes('VITE_FIREBASE_*');

      if (!secretsProvided) {
        console.log('⏭️ Firebase secrets not provided, skipping initialization assertion (expected in local runs without .env)');
        testResults.tests['7_tutorial_firebase'] = {
          passed: true,
          skipped: true,
          reason: 'VITE_FIREBASE_* env vars not provided'
        };
        test.skip();
        return;
      }

      testResults.tests['7_tutorial_firebase'] = {
        passed: state.initialized,
        analyticsReady: state.analyticsReady,
        error: state.error,
      };

      expect(state.initialized, `Firebase initializeApp should succeed when VITE_FIREBASE_* are set (error: ${state.error})`).toBe(true);
    });

  }); // end of test.describe.serial

});
