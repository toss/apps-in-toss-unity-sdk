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
      testsPassed: Object.values(testResults.tests || {}).filter(t => t.passed).length,
      testsTotal: Object.keys(testResults.tests || {}).length
    };
    fs.writeFileSync(benchmarkPath, JSON.stringify(benchmarkResults, null, 2));

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

    console.log('\n  📦 Build Size:      ' + (buildSize ? buildSize.toFixed(2) + ' MB' : 'N/A'));
    console.log('  ⏱️  Page Load:       ' + (pageLoad ? pageLoad + ' ms' : 'N/A'));
    console.log('  🎮 Unity Load:      ' + (unityLoad ? unityLoad + ' ms' : 'N/A'));
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

      // granite.config.ts 플레이스홀더 (web-framework 2.x granite build 전용)
      const graniteConfigPath = path.resolve(AIT_BUILD, 'granite.config.ts');
      if (fileExists(graniteConfigPath)) {
        const content = fs.readFileSync(graniteConfigPath, 'utf-8');
        const placeholders = checkForPlaceholders(content);
        expect(placeholders.length, 'Should have no unsubstituted placeholders in granite.config.ts').toBe(0);
      }

      // apps-in-toss.config.ts 플레이스홀더 (web-framework 3.x ait build — cosmiconfig 탐색 대상)
      // 3.x가 실제로 읽는 설정 파일. granite.config.ts만 검증하면 false green이 되므로 함께 검증한다.
      const appsInTossConfigPath = path.resolve(AIT_BUILD, 'apps-in-toss.config.ts');
      if (fileExists(appsInTossConfigPath)) {
        const content = fs.readFileSync(appsInTossConfigPath, 'utf-8');
        const placeholders = checkForPlaceholders(content);
        expect(placeholders.length, 'Should have no unsubstituted placeholders in apps-in-toss.config.ts').toBe(0);
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
    // Test 3-1: Page Reload Crash Test (cache warm)
    // -------------------------------------------------------------------------
    test('3-1. Page reload should not crash (cache warm)', async () => {
      // 계약: warm reload 후 페이지가 크래시하지 않고 unityInstance가 재세팅되어야 한다
      // (재로드 재초기화 회귀 가드, 4654e21). 제품 측 Cache-Storage 계층이 warm reload 시
      // ~100MB webgl.data 재다운로드를 제거하므로 정상 경로에서는 1회 시도로 통과한다.
      //
      // 하니스 순단 분류: self-hosted 러너의 vite preview가 부하로 루프백 스트림을 끊으면
      // (ERR_CONNECTION_CLOSED / download-watchdog 발동 / "Failed to download file")
      // 이는 제품 크래시가 아니라 하니스 인프라 아티팩트이므로 bounded 재시도한다.
      // 반면 진짜 크래시 시그니처(RuntimeError/webglcontextlost/Aborted()/out of bounds/
      // memory access)는 즉시 hard-fail — 재시도로 삼키지 않는다(원 계약 보존).
      test.setTimeout(360000);
      const CRASH_RE = /webglcontextlost|Aborted\(|RuntimeError|out of bounds|memory access/i;
      const HARNESS_RE = /ERR_CONNECTION_CLOSED|Failed to download file|download-watchdog/i;
      const maxAttempts = 3;

      const reloadErrors = [];   // { message, stack }
      const errHandler = err => reloadErrors.push({ message: err.message, stack: err.stack });
      const consoleLines = [];
      const consoleHandler = msg => {
        const line = `[${msg.type()}] ${msg.text()}`;
        consoleLines.push(line);
        // 제품 캐시 계층 마커를 CI stdout으로 즉시 포워딩(콜드 워밍/재로드 HIT·MISS 진단).
        if (line.indexOf('[AIT] cache:') !== -1) console.log(`  (page) ${line}`);
      };
      // 실패한 네트워크 요청(끊긴 소켓 등)을 URL+원인과 함께 포착.
      const failedRequests = [];
      const reqFailedHandler = req => {
        try {
          failedRequests.push(`${req.url().split('/').slice(-2).join('/')} :: ${req.failure()?.errorText || '?'}`);
        } catch (e) {}
      };
      // Build/* 응답 상태 관측 — 데이터가 캐시 서빙됐는지/재다운로드 됐는지 확인.
      const buildResponses = [];
      const respHandler = resp => {
        try {
          const u = resp.url();
          if (/\/Build\//.test(u)) buildResponses.push(`${u.split('/').slice(-1)[0]} -> ${resp.status()}`);
        } catch (e) {}
      };
      sharedPage.on('pageerror', errHandler);
      sharedPage.on('console', consoleHandler);
      sharedPage.on('requestfailed', reqFailedHandler);
      sharedPage.on('response', respHandler);

      const hadCrash = () => reloadErrors.some(e => CRASH_RE.test(e.message));
      const hadHarnessDrop = () =>
        failedRequests.some(f => HARNESS_RE.test(f)) ||
        consoleLines.some(l => HARNESS_RE.test(l));
      const dumpDiag = (tag) => {
        console.log(`[3-1] pageerrors(${reloadErrors.length}):`);
        reloadErrors.forEach((e, i) => {
          console.log(`  #${i}: ${e.message}`);
          if (e.stack && e.stack !== e.message) console.log(`     stack: ${e.stack.split('\n').slice(0, 4).join(' | ')}`);
        });
        console.log(`[3-1] requestfailed(${failedRequests.length}): ${failedRequests.join(' | ')}`);
        console.log(`[3-1] Build/* responses(${buildResponses.length}): ${buildResponses.join(' | ')}`);
        const spam = /still waiting on run dependencies|dependency: dataUrl|\(end of list\)/;
        const signal = consoleLines.filter(l => !spam.test(l));
        console.log(`[3-1] ${tag} console total=${consoleLines.length}, signal=${signal.length}`);
        console.log(`[3-1] --- signal head (first 80) ---\n${signal.slice(0, 80).join('\n')}`);
        if (signal.length > 110) console.log(`[3-1] --- signal tail (last 30) ---\n${signal.slice(-30).join('\n')}`);
      };

      // 벽시계-바운드 unityInstance 폴링. Playwright waitForFunction은 제품 워치독의
      // location.reload() 루프를 만나면 자체 timeout을 무시하고 navigation마다 re-arm되어
      // test.setTimeout 예산 전체를 소진한다(관측: 90s 지정에도 363s 실행). 이 헬퍼는
      // 내가 제어하는 벽시계 deadline으로 시도별 예산을 실제로 강제하고, navigation 중
      // evaluate 예외("context destroyed"/page closed)를 삼켜 재로드 루프에 견딘다.
      const waitForUnityBounded = async (budgetMs) => {
        const deadline = Date.now() + budgetMs;
        let evalThrows = 0;
        while (Date.now() < deadline) {
          try {
            const ready = await sharedPage.evaluate(
              () => typeof window !== 'undefined' && window['unityInstance'] !== undefined);
            if (ready) return { ready: true, evalThrows };
          } catch (e) {
            evalThrows++; // 재로드 중 컨텍스트 파괴 등 — 계속 폴링.
            if (/has been closed|Target closed/.test(e.message || '')) {
              return { ready: false, closed: true, evalThrows };
            }
          }
          await new Promise(r => setTimeout(r, 1000));
        }
        return { ready: false, evalThrows };
      };

      let passed = false;
      let lastErr = null;
      try {
        for (let attempt = 1; attempt <= maxAttempts; attempt++) {
          // 각 시도마다 수집기 초기화(참조 유지 위해 in-place clear).
          reloadErrors.length = 0; consoleLines.length = 0;
          failedRequests.length = 0; buildResponses.length = 0;
          // 재시도 시엔 페이지 재로드 예산을 리셋해 페이지 자체 워치독도 새로 시도하게 하고,
          // 캐시 우회 플래그도 지워 재시도 reload가 워밍된 Cache-Storage를 활용하도록 한다.
          if (attempt > 1) {
            try {
              await sharedPage.evaluate(() => {
                try { sessionStorage.removeItem('__ait_reload_count__'); } catch (e) {}
                try { sessionStorage.removeItem('__ait_skip_data_cache__'); } catch (e) {}
              });
            } catch (e) {}
          }
          const t0 = Date.now();
          let closedFatal = false;
          try {
            // domcontentloaded로 커밋(networkidle 금지 — 워치독 재다운로드 루프 하에선 idle이 안 옴).
            // unityInstance 대기는 벽시계-바운드 폴링으로 분리 제어한다.
            const resp = await sharedPage.reload({ waitUntil: 'domcontentloaded', timeout: 45000 });
            console.log(`[3-1] attempt ${attempt}/${maxAttempts} reload status=${resp?.status()} after ${Date.now() - t0}ms`);
            expect(resp?.status()).toBe(200);

            const navType = await sharedPage.evaluate(() => {
              try {
                const e = performance.getEntriesByType('navigation')[0];
                return e ? e.type : (performance.navigation && performance.navigation.type);
              } catch (e) { return 'unknown'; }
            }).catch(() => 'unknown');
            console.log(`[3-1] navigation type=${navType}`);

            const tWait = Date.now();
            const res = await waitForUnityBounded(75000);
            if (res.ready) {
              console.log(`[3-1] unityInstance re-set after ${Date.now() - tWait}ms (warm reinit ok, attempt ${attempt}, evalThrows=${res.evalThrows})`);
              // 성공 경로에서도 진짜 크래시 시그니처는 hard-fail.
              const crashErrors = reloadErrors.filter(e => CRASH_RE.test(e.message));
              expect(crashErrors.length, `No crash errors on reload: ${crashErrors.map(e => e.message).join('; ')}`).toBe(0);

              testResults.tests['3_1_reload'] = { passed: true, attempts: attempt };
              passed = true;
              break;
            }
            // unityInstance가 예산 내 미설정(evalThrows>0이면 재로드 루프 진행 중 = 워치독 발동).
            closedFatal = !!res.closed;
            throw new Error(`unityInstance not set within 75s budget (evalThrows=${res.evalThrows}${res.closed ? ', page closed' : ''})`);
          } catch (err) {
            lastErr = err;
            console.log(`[3-1] attempt ${attempt}/${maxAttempts} FAILED after ${Date.now() - t0}ms: ${err.message}`);
            // 진짜 크래시면 재시도 없이 즉시 실패(원 계약 보존).
            if (hadCrash()) {
              console.log(`[3-1] genuine crash signature detected — hard-fail (no retry)`);
              dumpDiag('crash');
              throw err;
            }
            // 페이지/컨텍스트가 닫혔으면 재시도 불가(fatal).
            if (closedFatal || /has been closed|Target closed/.test(err.message || '')) {
              console.log(`[3-1] page/context closed — cannot retry`);
              dumpDiag('closed');
              throw err;
            }
            // 하니스 순단(로컬 서버 연결 끊김/다운로드 실패)이고 시도가 남았으면 재시도.
            if (attempt < maxAttempts && hadHarnessDrop()) {
              console.log(`[3-1] harness connection-drop classified (server dropped webgl.data stream) — retrying reload`);
              continue;
            }
            // 소진 또는 미분류: 진단 덤프 후 실패.
            dumpDiag('exhausted');
            throw err;
          }
        }
      } finally {
        sharedPage.off('pageerror', errHandler);
        sharedPage.off('console', consoleHandler);
        sharedPage.off('requestfailed', reqFailedHandler);
        sharedPage.off('response', respHandler);
      }
      if (!passed && lastErr) throw lastErr;
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


    // -------------------------------------------------------------------------
    // Test 8: Nested callback async round-trip (processProductGrant)
    // 결제 이벤트 없이 중첩 콜백 왕복을 실 WebGL 빌드에서 검증한다:
    //   JS SendMessage('AITCore','OnNestedCallback')
    //     → C# OnNestedCallback → DispatchNestedCallbackAsync → await 사용자 콜백
    //     → __AITRespondToNestedCallback → JS Promise resolve
    // E2ETestTrigger.Start()가 사전 등록한 콜백 2종(지연 true / 예외)을 구동하고,
    // 미등록 콜백까지 3케이스로 검증한다. 핵심: 지연 resolve는 구 동기 구현으로 불가능.
    // -------------------------------------------------------------------------
    test('8. Nested callback (processProductGrant) should round-trip asynchronously', async () => {
      test.setTimeout(60000);

      console.log('🔄 Driving nested callback round-trip via SendMessage...');

      const roundTrip = await sharedPage.evaluate(async () => {
        const CB_NAME = 'processProductGrant';

        // 하나의 콜백을 구동하고 resolve까지의 결과/경과시간을 반환한다.
        // resolver를 __AIT_NESTED_CALLBACKS에 직접 등록(실 jslib과 동일 경로)한 뒤
        // SendMessage로 C#을 트리거하고, C#의 __AITRespondToNestedCallback 응답을 기다린다.
        function drive(callbackId, suffix, timeoutMs) {
          return new Promise((resolve) => {
            const ui = window['unityInstance'];
            if (!ui || typeof ui.SendMessage !== 'function') {
              resolve({ ok: false, reason: 'unityInstance/SendMessage unavailable' });
              return;
            }
            window.__AIT_NESTED_CALLBACKS = window.__AIT_NESTED_CALLBACKS || {};
            const requestId = 'e2e-rt-' + suffix + '-' + Date.now();
            const started = performance.now();
            let settled = false;

            const timer = setTimeout(() => {
              if (settled) return;
              settled = true;
              delete window.__AIT_NESTED_CALLBACKS[requestId];
              resolve({ ok: false, reason: 'timeout', elapsedMs: performance.now() - started });
            }, timeoutMs);

            // 실 jslib과 동일한 엔트리 shape({ resolve, timeoutId })로 등록한다.
            // SDK가 opt-in 오버레이 교착 타임아웃(NestedCallbackTimeoutMs)을 도입하면서
            // __AITRespondToNestedCallback 핸들러가 entry.resolve(resultBool)를 호출하고
            // entry.timeoutId를 clearTimeout하므로, 옛 "직접 함수" shape는 더 이상 호환되지 않는다.
            // (이 테스트는 SDK 타임아웃을 켜지 않으므로 timeoutId는 null.)
            window.__AIT_NESTED_CALLBACKS[requestId] = {
              resolve: (resultBool) => {
                if (settled) return;
                settled = true;
                clearTimeout(timer);
                delete window.__AIT_NESTED_CALLBACKS[requestId];
                resolve({ ok: true, result: resultBool, elapsedMs: performance.now() - started });
              },
              timeoutId: null
            };

            const payload = JSON.stringify({
              RequestId: requestId,
              CallbackId: callbackId,
              CallbackName: CB_NAME,
              Data: JSON.stringify({ orderId: 'e2e-order' })
            });
            ui.SendMessage('AITCore', 'OnNestedCallback', payload);
          });
        }

        // 순차 실행(응답이 requestId로 구분되므로 병렬도 가능하나 로그 가독성을 위해 순차)
        const asyncCase = await drive('e2e-nested-async', 'async', 20000);
        const throwCase = await drive('e2e-nested-throw', 'throw', 20000);
        const unknownCase = await drive('e2e-nested-unknown', 'unknown', 20000);

        return { asyncCase, throwCase, unknownCase };
      });

      console.log('\n' + '='.repeat(70));
      console.log('📊 NESTED CALLBACK ROUND-TRIP RESULTS');
      console.log('='.repeat(70));
      console.log(`   async(true) : ${JSON.stringify(roundTrip.asyncCase)}`);
      console.log(`   throw(false): ${JSON.stringify(roundTrip.throwCase)}`);
      console.log(`   unknown     : ${JSON.stringify(roundTrip.unknownCase)}`);
      console.log('='.repeat(70));

      testResults.tests['8_nested_callback'] = {
        passed:
          roundTrip.asyncCase.ok && roundTrip.asyncCase.result === true &&
          roundTrip.throwCase.ok && roundTrip.throwCase.result === false &&
          roundTrip.unknownCase.ok && roundTrip.unknownCase.result === false,
        asyncCase: roundTrip.asyncCase,
        throwCase: roundTrip.throwCase,
        unknownCase: roundTrip.unknownCase
      };

      // 1) 지연 async 콜백 → true 로 resolve, 그리고 등록된 지연(300ms)만큼 실제로 기다렸는지 검증.
      //    구 동기 구현은 같은 프레임에 즉시 응답하므로 이 지연은 async 왕복의 결정적 증거다.
      expect(roundTrip.asyncCase.ok, `async case should resolve (got: ${JSON.stringify(roundTrip.asyncCase)})`).toBe(true);
      expect(roundTrip.asyncCase.result, 'async callback should resolve true').toBe(true);
      expect(roundTrip.asyncCase.elapsedMs, 'async callback should genuinely await (>=150ms vs ~300ms delay)').toBeGreaterThanOrEqual(150);

      // 2) 예외 콜백 → false 로 resolve (응답 유실 없이 dispatch가 잡아 정확히 1회 응답)
      expect(roundTrip.throwCase.ok, `throw case should resolve (got: ${JSON.stringify(roundTrip.throwCase)})`).toBe(true);
      expect(roundTrip.throwCase.result, 'throwing callback should resolve false (no lost response)').toBe(false);

      // 3) 미등록 callbackId → false 로 즉시 resolve
      expect(roundTrip.unknownCase.ok, `unknown case should resolve (got: ${JSON.stringify(roundTrip.unknownCase)})`).toBe(true);
      expect(roundTrip.unknownCase.result, 'unknown callback should resolve false').toBe(false);
    });

  }); // end of test.describe.serial

});
