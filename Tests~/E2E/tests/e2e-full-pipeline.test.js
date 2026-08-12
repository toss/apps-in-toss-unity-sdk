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

async function startDevServer(aitBuildDir, defaultPort, extraEnv = {}) {
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
      env: { ...process.env, NODE_OPTIONS: '', ...extraEnv }
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

/**
 * window.unityInstance가 세팅될 때까지 대기 (신규 격리 page용 헬퍼).
 * 기존 shared-session beforeAll의 인라인 폴링과 동일한 조건.
 */
async function waitForUnityInstance(page, timeoutMs = 60000) {
  await page.waitForFunction(() => window['unityInstance'] !== undefined, { timeout: timeoutMs });
}

/**
 * window.__E2E_PLAYERPREFS_DATA__를 지운 뒤 triggerFn()을 실행하고,
 * 지정한 op으로 결과가 도착할 때까지 폴링한다 (PlayerPrefsTester → E2ETestBridge.jslib 계약).
 * 같은 page를 여러 케이스에서 재사용할 때 이전 결과 잔재를 읽지 않도록 매번 지우고 시작한다.
 */
async function triggerPlayerPrefsAndWait(page, triggerFn, expectedOp, timeoutMs = 10000) {
  await page.evaluate(() => { delete window['__E2E_PLAYERPREFS_DATA__']; });
  await triggerFn();
  await page.waitForFunction((op) => {
    const d = window['__E2E_PLAYERPREFS_DATA__'];
    return d !== undefined && d !== null && d.op === op;
  }, expectedOp, { timeout: timeoutMs });
  return page.evaluate(() => window['__E2E_PLAYERPREFS_DATA__']);
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
    // devtools mock 초기화(cold optimizeDeps ~2-4초) + TriggerAPITest allowlist 대기가
    // 추가되어 기존 120000보다 여유를 둔다.
    // 300000 산정 근거: 평시 소요 2.0분(run 1058 실측) 대비 200000은 여유가 40%뿐이라
    // GH-hosted 러너 성능 편차(피크 시간대 wasm 인스턴스화 80초→150초+ 실측)를 흡수하지
    // 못한다. 이 테스트는 기능 검증이 목적이고 소요 시간은 리포트의 per-test duration으로
    // 계속 관측하므로 예산은 편차를 흡수할 만큼 여유 있게 둔다.
    test.setTimeout(300000);

    // 패널/mock 주입 실패는 브라우저 콘솔에만 남고 테스트 실패로 드러나지 않을 수 있어,
    // CI 로그에서 바로 확인할 수 있도록 pageerror/console을 캡처한다.
    page.on('pageerror', error => {
      console.log('[Page Error]', error.message);
    });
    page.on('console', msg => {
      const type = msg.type();
      const text = msg.text();
      if (type === 'error' || type === 'warning' || text.includes('@apps-in-toss/devtools')) {
        console.log('[Browser Console]', text);
      }
    });

    await applyMobileThrottling(page);

    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist for dev server').toBe(true);

    console.log('🚀 Starting dev server (vite) with devtools mock enabled...');
    // AIT_DEVTOOLS=1: Editor가 AIT/Dev Server 실행 시 항상 명시하는 값과 동일한 계약
    // (vite.config.ts가 AIT_DEVTOOLS='1'일 때만 devtools unplugin + 패널을 활성화).
    const devServer = await startDevServer(AIT_BUILD, serverPort, { AIT_DEVTOOLS: '1' });
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

    // -------------------------------------------------------------------------
    // devtools mock 통합 단언 (AIT_DEVTOOLS=1 계약 검증)
    // -------------------------------------------------------------------------

    // ① devtools unplugin이 @apps-in-toss/web-framework를 mock으로 alias했다는 직접 증거:
    //    window.AppsInToss.getPlatformOS 함수가 주입되어 있어야 한다.
    // unity-bridge.ts 모듈 실행(window.AppsInToss 생성)은 devtools mock의 cold optimizeDeps(~2-4초) 및
    // Unity 캔버스 로딩과 병렬·독립 경로라 단발 체크가 레이스할 수 있어 폴링으로 먼저 대기한다.
    await page.waitForFunction(() => typeof window['AppsInToss']?.getPlatformOS === 'function', { timeout: 15000 }).catch(() => {});
    const hasGetPlatformOS = await page.evaluate(() => {
      return typeof window['AppsInToss']?.getPlatformOS === 'function';
    });
    expect(hasGetPlatformOS, 'window.AppsInToss.getPlatformOS should be injected by devtools unplugin').toBe(true);

    // ② mock 함수가 reject 없이 문자열을 반환하는지 (mock이 실제로 동작한다는 증명)
    const platformOSResult = await page.evaluate(async () => {
      try {
        const os = await window['AppsInToss'].getPlatformOS();
        return { ok: true, type: typeof os };
      } catch (e) {
        return { ok: false, error: e?.message || String(e) };
      }
    });
    expect(platformOSResult.ok, `getPlatformOS() should not reject (got: ${JSON.stringify(platformOSResult)})`).toBe(true);
    expect(platformOSResult.type, 'getPlatformOS() should resolve to a string').toBe('string');

    // ③ 패널 호스트 엘리먼트 존재 (AIT_DEVTOOLS_PANEL 기본 on, 명시 미설정 시 활성).
    //    셀렉터 `.ait-panel-toggle`은 devtools 패키지 자신이
    //    "The CSS-class / attribute contract relied on by e2e/panel.test.ts"로 문서화한
    //    안정 계약(dist/panel/index.js 주석) — 내부 React 트리 구조가 바뀌어도
    //    devtools가 마이너 업데이트에서 지키기로 약속한 셀렉터라 관대하게 안전하다.
    // 패널 모듈 로드도 Unity 캔버스 등장과 병렬 경로라 카운트 확인 전에 짧게 폴링한다.
    await page.waitForSelector('.ait-panel-toggle', { timeout: 10000 }).catch(() => {});
    const panelToggleCount = await page.locator('.ait-panel-toggle').count();
    expect(panelToggleCount, 'devtools floating panel toggle button should be mounted').toBeGreaterThan(0);

    // ④ 소규모 allowlist API가 mock에서 실제로 성공하는지, 기존 TriggerAPITest 하네스
    //    (Test 4와 동일한 트리거 + 결과 수집 코드)를 재사용해 확인한다 — 새 하네스를
    //    만들지 않는다.
    //    allowlist 근거: RuntimeAPITester.cs가 호출하는 apiName과
    //    node_modules/@apps-in-toss/devtools/dist/mock/3x.js의 export를 대조해,
    //    aitState 기본값(권한 allowed, deviceModes mock 등)으로 예외 없이 즉시 성공
    //    반환하는 항목만 선정했다.
    //    - getPlatformOS/getOperationalEnvironment/getDeviceId/getLocale: 파라미터 없음
    //    - env.getDeploymentId/getAppsInTossGlobals/getServerTime:
    //      SDK 3.0 신규 표면(2026-08 감사로 RuntimeAPITester에 편입). mock이 각각
    //      aitState 값을 동기/즉시 Promise로 반환 — 예외 경로 없음.
    //    - isMinVersionSupported: SDK 3.0 신규 표면(2026-08 감사로 RuntimeAPITester에 편입).
    //      실제 SDK 타입(@apps-in-toss/web-framework)과 devtools mock 구현 모두 boolean을
    //      "동기" 반환하는 함수라 Promise가 아니다 — 생성된 jslib(__isMinVersionSupported_Internal)도
    //      window.AppsInToss.isMinVersionSupported(...) 반환값을 await 없이 그대로 읽어 즉시
    //      SendMessage 콜백을 보낸다. (Unity 쪽 AIT.IsMinVersionSupported가 Task/Awaitable인 것은
    //      SendMessage 콜백 브리지의 공통 패턴일 뿐이며, 이 API 자체가 비동기라서가 아니다.)
    //      예외 경로 없음.
    //    - Storage.getItem/setItem/removeItem/clearItems: 권한 게이트 없이 localStorage에
    //      직접 위임 — 예외 경로 없음(devtools#770/#775에서 이미 실측 확정).
    //    - partner.addAccessoryButton/removeAccessoryButton: console.log만 하고 즉시
    //      resolve하는 스텁 — 예외 경로 없음.
    //    - fetchAlbumItems: photos 권한 기본값 allowed + deviceModes.photos 기본값
    //      mock이라 file picker 없이 즉시 목업 배열을 반환.
    //    - SafeAreaInsets.get: aitState.state.safeAreaInsets 스냅샷을 동기 반환.
    const MOCK_SUCCESS_ALLOWLIST = [
      'API_GetPlatformOS',
      'API_GetOperationalEnvironment',
      'API_GetDeviceId',
      'API_GetLocale',
      'API_EnvGetDeploymentId',
      'API_GetAppsInTossGlobals',
      'API_IsMinVersionSupported',
      'API_GetServerTime',
      'API_StorageSetItem',
      'API_StorageGetItem',
      'API_StorageRemoveItem',
      'API_StorageClearItems',
      'API_PartnerAddAccessoryButton',
      'API_PartnerRemoveAccessoryButton',
      'API_FetchAlbumItems',
      'API_SafeAreaInsetsGet',
    ];

    try {
      await page.waitForFunction(() => typeof window['TriggerAPITest'] === 'function', { timeout: 10000 });
    } catch {
      console.log('⚠️ TriggerAPITest not found on dev server page (mock allowlist assertion may fail)');
    }

    // 원인: 아래 TriggerAPITest 스윕이 CloseView를 호출하면 devtools mock의 closeView()가
    // window.history.back()을 실행해 page.goto 직후 페이지를 실제로 이탈시키고, 이를
    // 기다리는 evaluate()의 실행 컨텍스트를 파괴한다. same-document 히스토리 엔트리를
    // 미리 쌓아 back()을 컨텍스트 보존형 popstate 이동으로 바꿔 방지한다.
    await page.evaluate(() => {
      history.pushState({ aitE2eGuard: true }, '', location.href);
    });

    const devApiResults = await page.evaluate(() => {
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

        setTimeout(() => resolve(null), 60000);
      });
    });

    if (devApiResults) {
      let devResults = devApiResults;
      if (typeof devResults === 'string') {
        try { devResults = JSON.parse(devResults); } catch {}
      }

      const byName = new Map((devResults.results || []).map(r => [r.apiName, r]));
      for (const name of MOCK_SUCCESS_ALLOWLIST) {
        const r = byName.get(name);
        expect(r, `${name} should be present in TriggerAPITest results`).toBeTruthy();
        expect(r.success, `${name} should succeed under devtools mock (got: ${JSON.stringify(r)})`).toBe(true);
        expect(r.isExpectedError, `${name} should be a genuine mock success, not an expected-error pass-through (got: ${JSON.stringify(r)})`).toBe(false);
      }
    } else {
      console.log('⚠️ TriggerAPITest results not received on dev server (mock allowlist assertion skipped)');
    }

    // ⑤ 회귀: 삭제된 자체 mock 브리지(appsintoss-unity-bridge.js)의 흔적이
    //    devtools 경로로 재유입되지 않았는지 확인.
    const legacyBridgeGlobals = await page.evaluate(() => ({
      unityBridge: typeof window['AppsInTossUnityBridge'],
      googleAdMob: typeof window['GoogleAdMob'],
      aitShowToast: typeof window['aitShowToast'],
    }));
    expect(legacyBridgeGlobals.unityBridge, 'window.AppsInTossUnityBridge should not exist (legacy mock bridge removed)').toBe('undefined');
    expect(legacyBridgeGlobals.googleAdMob, 'window.GoogleAdMob should not exist (legacy mock bridge removed)').toBe('undefined');
    expect(legacyBridgeGlobals.aitShowToast, 'window.aitShowToast should not exist (legacy mock bridge removed)').toBe('undefined');

    await killServerProcess(serverProcess, [VITE_DEV_PORT, serverPort]);
    serverProcess = null;

    testResults.tests['2_dev_server'] = {
      passed: true,
      loadTimeMs: loadTime
    };
  });


  // -------------------------------------------------------------------------
  // Test 2b: SDK dev 서버 커맨드 스모크 (granite bin collision 회귀 방지)
  // -------------------------------------------------------------------------
  // Unity Editor의 Dev Server 메뉴는 vite가 아니라 web-framework의
  // granite CLI 파일을 node로 직접 실행한다 (DevServerCommandResolver —
  // node_modules/.bin/granite 이름 충돌 우회). test 2의 vite 경로는 이 커맨드를
  // 전혀 거치지 않으므로, 실제 커맨드가 즉사하지 않고 리슨 포트를 여는지만
  // 짧게 검증한다 (Unity 로드 검증은 test 2가 담당).
  test('2b. SDK dev server command (granite bin direct) should boot', async () => {
    test.setTimeout(90000);

    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist').toBe(true);

    const wfPkgPath = path.resolve(AIT_BUILD, 'node_modules/@apps-in-toss/web-framework/package.json');
    test.skip(!fs.existsSync(wfPkgPath), 'web-framework not installed in ait-build');

    const wfPkg = JSON.parse(fs.readFileSync(wfPkgPath, 'utf8'));
    const graniteBin = wfPkg.bin && wfPkg.bin.granite;
    // 3.x: granite bin 없음 — Editor는 vite 경로를 쓰므로 test 2가 커버
    test.skip(!graniteBin, 'web-framework has no granite bin (3.x) — vite path covered by test 2');

    const binRel = path.join('node_modules/@apps-in-toss/web-framework', graniteBin.replace(/^\.\//, ''));
    console.log(`🚀 Booting SDK dev command: node ${binRel} dev`);

    // Editor 커맨드(pnpm exec -- node <bin> dev)와 동일한 실행 (pnpm exec는 PATH 추가뿐)
    const child = spawn(process.execPath, [binRel, 'dev'], {
      cwd: AIT_BUILD,
      stdio: 'pipe',
      env: { ...process.env, CI: 'true', NODE_OPTIONS: '' }
    });

    let output = '';
    const seenPorts = new Set();
    const result = await new Promise((resolve) => {
      let settled = false;
      const settle = (value) => {
        if (!settled) {
          settled = true;
          resolve(value);
        }
      };
      const onData = (data) => {
        const clean = data.toString().replace(/\x1B\[[0-9;]*[mGKH]/g, '');
        output += clean;
        for (const m of clean.matchAll(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/g)) {
          seenPorts.add(parseInt(m[1], 10));
        }
        if (seenPorts.size > 0) {
          settle('listening');
        }
      };
      child.stdout.on('data', onData);
      child.stderr.on('data', onData);
      child.on('exit', (code) => settle(`exited:${code}`));
      setTimeout(() => settle('timeout'), 60000);
    });

    // 정리: 프로세스 트리 + 감지된 포트(Metro가 띄운 vite 자식 포함) + Metro 기본 포트
    await killServerProcess(child, [...seenPorts, 8081]);

    console.log(`SDK dev command result: ${result}, ports: ${[...seenPorts].join(',')}`);
    expect(
      result,
      `SDK dev command should open a listen port, got: ${result}\n--- output tail ---\n${output.slice(-2000)}`
    ).toBe('listening');

    testResults.tests['2b_sdk_dev_command'] = {
      passed: true,
      ports: [...seenPorts]
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

      // Storage 브릿지 존재 확인 (생성기가 unity-bridge.ts에서 Storage 네임스페이스를
      // 드롭하는 회귀를 감지 — PlayerPrefs 영속화 레이어가 이 함수에 의존한다)
      const storageGetItemType = await sharedPage.evaluate(
        () => typeof (window['AppsInToss'] && window['AppsInToss'].Storage && window['AppsInToss'].Storage.getItem)
      );
      expect(storageGetItemType, 'window.AppsInToss.Storage.getItem should be a function').toBe('function');

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
      // (ERR_CONNECTION_CLOSED 등 Chromium net 에러) 이는 제품 크래시가 아니라 하니스
      // 인프라 아티팩트이므로 bounded 재시도한다.
      // 반면 진짜 크래시 시그니처(RuntimeError/webglcontextlost/Aborted()/out of bounds/
      // memory access)는 즉시 hard-fail — 재시도로 삼키지 않는다(원 계약 보존).
      // 제품 hang 시그니처("Failed to download file" = 로더의 .data 다운로드 실패)도 마찬가지로
      // 즉시 hard-fail — 과거 이 문구가 HARNESS_RE에 들어 있어 제품 결함이 조용한 재시도로
      // 은폐됐다(dev 빌드 warm reload에서 fetch 계측이 로더 다운로드를 깨뜨린 회귀).
      test.setTimeout(360000);
      const CRASH_RE = /webglcontextlost|Aborted\(|RuntimeError|out of bounds|memory access/i;
      // 하니스 전용 패턴만 남긴다: 아래 넷은 모두 Chromium이 requestfailed에 싣는 전송 계층
      // 순단(루프백 스트림 끊김/서버 종료)으로, 제품 코드가 절대 만들어내지 않는 문구다.
      // ERR_INCOMPLETE_CHUNKED_ENCODING: vite preview는 .data를 chunked로 서빙하므로
      // 본문 스트리밍 중 끊기면 Chromium이 CLOSED/RESET 대신 이 코드를 보고한다.
      // 제거된 것: "Failed to download file"(Unity 로더의 제품 결함 지문) 및
      // "download-watchdog"(그 실패를 받은 제품 워치독의 진단 마커 — 즉 같은 제품 결함).
      const HARNESS_RE = /ERR_CONNECTION_CLOSED|ERR_CONNECTION_RESET|ERR_EMPTY_RESPONSE|ERR_INCOMPLETE_CHUNKED_ENCODING/i;
      // 제품 hang 지문: 로더가 .data 다운로드 실패 시 남기는 유일한 콘솔 신호.
      // 동반 pageerror("...reading 'subarray'")는 진단용으로만 수집한다(판정 조건 아님 —
      // 로더가 실패를 삼켜 subarray 예외 없이 조용히 매다는 변종도 있기 때문).
      const PRODUCT_HANG_RE = /Failed to download file/i;
      const HANG_PAGEERROR_RE = /reading ['"]subarray['"]/i;
      // 예산 근거(실측): 성공 경로는 warm reload 후 1.1~5.8초에 unityInstance가 재설정되고,
      // 실패(제품 hang) 경로는 3.5초 안에 "Failed to download file"로 확정된다.
      // 25s면 성공 상한의 ~4배 마진이라 느린 러너에서도 오탐하지 않으면서, 예전 75s처럼
      // 실패 케이스에서 시도당 1분 이상을 버리지 않는다.
      const UNITY_WAIT_BUDGET_MS = 25000;
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
      const hadProductHang = () => consoleLines.some(l => PRODUCT_HANG_RE.test(l));
      const productHangDetail = () => {
        const sig = consoleLines.filter(l => PRODUCT_HANG_RE.test(l)).slice(0, 3);
        const sub = reloadErrors.filter(e => HANG_PAGEERROR_RE.test(e.message)).map(e => e.message).slice(0, 2);
        return `console=[${sig.join(' | ')}] pageerror(subarray)=[${sub.join(' | ') || '없음'}]`;
      };
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
      // abortIf: 매 폴링 사이클 앞에서 평가되는 조기 종료 술어(제품 hang 지문 관측 등).
      // 예산을 끝까지 태우지 않고 즉시 { aborted: true }로 빠져나온다.
      const waitForUnityBounded = async (budgetMs, abortIf) => {
        const deadline = Date.now() + budgetMs;
        let evalThrows = 0;
        while (Date.now() < deadline) {
          if (abortIf && abortIf()) return { ready: false, aborted: true, evalThrows };
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
            const res = await waitForUnityBounded(UNITY_WAIT_BUDGET_MS, hadProductHang);
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
            if (res.aborted) {
              // 제품 hang 지문 관측 — 예산 소진을 기다리지 않고 즉시 실패로 넘긴다(분류는 catch에서).
              throw new Error(`제품 hang 지문 조기 감지로 대기 중단 (${Date.now() - tWait}ms 경과)`);
            }
            throw new Error(`unityInstance not set within ${UNITY_WAIT_BUDGET_MS / 1000}s budget (evalThrows=${res.evalThrows}${res.closed ? ', page closed' : ''})`);
          } catch (err) {
            lastErr = err;
            console.log(`[3-1] attempt ${attempt}/${maxAttempts} FAILED after ${Date.now() - t0}ms: ${err.message}`);
            // 진짜 크래시면 재시도 없이 즉시 실패(원 계약 보존).
            if (hadCrash()) {
              console.log(`[3-1] genuine crash signature detected — hard-fail (no retry)`);
              dumpDiag('crash');
              throw err;
            }
            // 제품 hang 시그니처면 재시도 없이 즉시 실패 — 재시도로 삼키면 회귀가 다시 은폐된다.
            // (제품 워치독이 최대 2회 자동 reload 하지만, 첫 관측에서 바로 종료하므로 그 루프와
            //  경합하지 않는다. 리스너는 아래 finally에서 한 번에 해제된다.)
            // 단, 로더는 진짜 전송 계층 순단(fetch reject)에도 같은 "Failed to download file"을
            // 남기므로 net 에러 시그니처가 공존하면 순단이 원인 — 하니스 재시도 경로로 넘긴다.
            // 제품 결함(fetch 계측 예외)은 요청 자체는 성공해 net 에러가 절대 없다는 점이 지문이다.
            if (hadProductHang() && !hadHarnessDrop()) {
              console.log(`[3-1] product hang signature detected (Failed to download file) — hard-fail (no retry)`);
              dumpDiag('product-hang');
              throw new Error(
                'Unity 로더 .data 다운로드가 fetch 계측 예외로 깨진 제품 결함 시그니처 ' +
                `(런북: dev 빌드 warm reload hang): ${productHangDetail()} :: ${err.message}`);
            }
            // 페이지/컨텍스트가 닫혔으면 재시도 불가(fatal).
            if (closedFatal || /has been closed|Target closed/.test(err.message || '')) {
              console.log(`[3-1] page/context closed — cannot retry`);
              dumpDiag('closed');
              throw err;
            }
            // 하니스 순단(로컬 서버 연결 끊김 — 전송 계층 net 에러)이고 시도가 남았으면 재시도.
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
    test('4. All SDK APIs should return correct errors in production preview (no Toss bridge)', async () => {
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
    // (Documentation~/BuildCustomization.md 튜토리얼 #1)
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
    // (Documentation~/BuildCustomization.md 튜토리얼 #2)
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
    // Test 8: Nested callback synchronous round-trip (processProductGrant)
    // 결제 이벤트 없이 중첩 콜백 왕복을 실 WebGL 빌드에서 검증한다:
    //   JS SendMessage('AITCore','OnNestedCallback')
    //     → C# OnNestedCallback → 동기 콜백 실행 → __AITRespondToNestedCallback
    //     → JS Promise resolve
    // E2ETestTrigger.Start()가 사전 등록한 콜백 2종(즉시 true / 예외)을 구동하고,
    // 미등록 콜백까지 3케이스로 검증한다. 응답은 SendMessage와 같은 스택에서 나간다.
    // -------------------------------------------------------------------------
    test('8. Nested callback (processProductGrant) should round-trip synchronously', async () => {
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

            // jslib의 __AITRespondToNestedCallback은 저장된 resolver를 동기 호출하므로,
            // 동기 dispatch라면 SendMessage가 리턴한 시점에 이미 응답이 도착해 있어야 한다.
            // syncSettled가 그 사실을 기록한다 — dispatch가 fire-and-forget(비동기 1틱 지연)
            // 으로 되돌아가는 회귀를 타이밍 임계값 없이 결정적으로 잡는다.
            let outcome = null;
            let sendReturned = false;
            window.__AIT_NESTED_CALLBACKS[requestId] = (resultBool) => {
              if (settled) return;
              settled = true;
              clearTimeout(timer);
              delete window.__AIT_NESTED_CALLBACKS[requestId];
              outcome = {
                ok: true,
                result: resultBool,
                elapsedMs: performance.now() - started,
                syncSettled: !sendReturned
              };
              if (sendReturned) resolve(outcome); // 비동기 도착 경로 (회귀 시)
            };

            const payload = JSON.stringify({
              RequestId: requestId,
              CallbackId: callbackId,
              CallbackName: CB_NAME,
              Data: JSON.stringify({ orderId: 'e2e-order' })
            });
            ui.SendMessage('AITCore', 'OnNestedCallback', payload);
            sendReturned = true;
            if (outcome) resolve(outcome); // 동기 도착 경로 (정상)
          });
        }

        // 순차 실행(응답이 requestId로 구분되므로 병렬도 가능하나 로그 가독성을 위해 순차)
        const grantCase = await drive('e2e-nested-grant', 'grant', 20000);
        const throwCase = await drive('e2e-nested-throw', 'throw', 20000);
        const unknownCase = await drive('e2e-nested-unknown', 'unknown', 20000);

        return { grantCase, throwCase, unknownCase };
      });

      console.log('\n' + '='.repeat(70));
      console.log('📊 NESTED CALLBACK ROUND-TRIP RESULTS');
      console.log('='.repeat(70));
      console.log(`   grant(true) : ${JSON.stringify(roundTrip.grantCase)}`);
      console.log(`   throw(false): ${JSON.stringify(roundTrip.throwCase)}`);
      console.log(`   unknown     : ${JSON.stringify(roundTrip.unknownCase)}`);
      console.log('='.repeat(70));

      testResults.tests['8_nested_callback'] = {
        passed:
          roundTrip.grantCase.ok && roundTrip.grantCase.result === true &&
          roundTrip.throwCase.ok && roundTrip.throwCase.result === false &&
          roundTrip.unknownCase.ok && roundTrip.unknownCase.result === false,
        grantCase: roundTrip.grantCase,
        throwCase: roundTrip.throwCase,
        unknownCase: roundTrip.unknownCase
      };

      // 1) 즉시 승인 콜백 → true 로 resolve, 그리고 SendMessage와 같은 스택에서 응답이
      //    나갔는지(syncSettled) 검증 — dispatch가 비동기로 되돌아가는 회귀를 결정적으로 잡는다.
      expect(roundTrip.grantCase.ok, `grant case should resolve (got: ${JSON.stringify(roundTrip.grantCase)})`).toBe(true);
      expect(roundTrip.grantCase.result, 'grant callback should resolve true').toBe(true);
      expect(roundTrip.grantCase.syncSettled, 'grant response must arrive on the SendMessage stack (sync dispatch)').toBe(true);

      // 2) 예외 콜백 → false 로 resolve (응답 유실 없이 dispatch가 잡아 정확히 1회 응답)
      expect(roundTrip.throwCase.ok, `throw case should resolve (got: ${JSON.stringify(roundTrip.throwCase)})`).toBe(true);
      expect(roundTrip.throwCase.result, 'throwing callback should resolve false (no lost response)').toBe(false);
      expect(roundTrip.throwCase.syncSettled, 'exception path must also respond synchronously').toBe(true);

      // 3) 미등록 callbackId → false 로 즉시 resolve
      expect(roundTrip.unknownCase.ok, `unknown case should resolve (got: ${JSON.stringify(roundTrip.unknownCase)})`).toBe(true);
      expect(roundTrip.unknownCase.result, 'unknown callback should resolve false').toBe(false);
      expect(roundTrip.unknownCase.syncSettled, 'unregistered path must also respond synchronously').toBe(true);
    });


    // -------------------------------------------------------------------------
    // Test 9: PlayerPrefs → 앱인토스 Storage 영속화 (platform Storage mock)
    // sharedPage를 오염시키지 않도록 각 케이스는 browser.newPage()로 격리된
    // page를 사용한다. 케이스 간 상태 승계가 필요한 조합(9-1→9-2, 9-3→9-4)만
    // page를 재사용하고, serial 실행 순서로 이를 보장한다.
    // -------------------------------------------------------------------------
    test.describe.serial('9. PlayerPrefs Persistence (platform Storage mock)', () => {
      /** @type {import('@playwright/test').Page} */
      let mockPage = null;
      /** @type {import('@playwright/test').Page} */
      let failPage = null;
      /** @type {import('@playwright/test').Page} */
      let noMockPage = null;

      test.afterAll(async () => {
        if (mockPage) { await mockPage.close(); mockPage = null; }
        if (failPage) { await failPage.close(); failPage = null; }
        if (noMockPage) { await noMockPage.close(); noMockPage = null; }
      });

      // -----------------------------------------------------------------------
      // 9-1: localStorage 기반 mock을 오버라이드 훅에 설치 → Set+Save →
      //      mock 백킹(localStorage)에 manifest가 기록되는지 확인
      // -----------------------------------------------------------------------
      test('9-1. mirrors PlayerPrefs.Save to platform Storage', async ({ browser }) => {
        test.setTimeout(120000);

        mockPage = await browser.newPage();

        // addInitScript는 reload마다 재실행되고, localStorage는 reload에도 살아남는다
        // (9-2가 IndexedDB만 지우고 이 mock 백킹은 보존되는 전제).
        await mockPage.addInitScript(() => {
          var PREFIX = 'PW_PP_MOCK_';
          window['__AIT_PLAYERPREFS_STORAGE__'] = {
            getItem: function (key) {
              return Promise.resolve(window.localStorage.getItem(PREFIX + key));
            },
            setItem: function (key, value) {
              return Promise.resolve(window.localStorage.setItem(PREFIX + key, value));
            }
          };
        });

        const response = await mockPage.goto(`http://localhost:${sharedPort}?e2e=true`, {
          waitUntil: 'domcontentloaded',
          timeout: 60000
        });
        expect(response?.status()).toBe(200);
        await waitForUnityInstance(mockPage);

        const result = await triggerPlayerPrefsAndWait(
          mockPage,
          () => mockPage.evaluate((json) => window['TriggerPlayerPrefsSet'](json),
            JSON.stringify({ key: 'ait_e2e_pp', value: 'v1' })),
          'set'
        );
        console.log(`[9-1] TriggerPlayerPrefsSet result: ${JSON.stringify(result)}`);
        expect(result.success, 'PlayerPrefs.SetString + Save should succeed').toBe(true);

        // mock 백킹(localStorage)에 manifest가 비어있지 않게 기록되었는지 확인
        const backingValue = await mockPage.evaluate(
          () => window.localStorage.getItem('PW_PP_MOCK_AITUnityFS_v1_manifest')
        );
        expect(backingValue, 'mock backing storage should have a manifest entry').toBeTruthy();
        expect(backingValue.length, 'manifest entry should not be empty').toBeGreaterThan(0);

        // manifest를 파싱해 PlayerPrefs 파일이 실제로 스냅샷에 수집됐는지 확인한다.
        // (빈 {"files":{}} 승격 push만으로도 backingValue가 truthy가 되는 구멍을 막는다 —
        //  경로 규칙 미스 등으로 실제 미러링이 0건이어도 이 단언 없이는 통과할 수 있었다)
        const manifestCheck = await mockPage.evaluate(() => {
          var raw = window.localStorage.getItem('PW_PP_MOCK_AITUnityFS_v1_manifest');
          var manifest = JSON.parse(raw);
          var snapshot = JSON.parse(manifest.inline);
          var keys = Object.keys(snapshot.files || {});
          var ppKeys = keys.filter(function (k) { return /\/PlayerPrefs$/.test(k); });
          var hasNonEmptyData = ppKeys.some(function (k) {
            var d = snapshot.files[k] && snapshot.files[k].d;
            return typeof d === 'string' && d.length > 0;
          });
          return { ppKeyCount: ppKeys.length, hasNonEmptyData: hasNonEmptyData };
        });
        expect(manifestCheck.ppKeyCount, 'snapshot must contain at least one /PlayerPrefs file entry').toBeGreaterThan(0);
        expect(manifestCheck.hasNonEmptyData, 'PlayerPrefs file entry must carry non-empty base64 data').toBe(true);

        const ppState = await mockPage.evaluate(() => ({
          preRunRan: window['__AIT_PP'].preRunRan,
          captured: window['__AIT_PP'].captured
        }));
        expect(ppState.preRunRan, '__AIT_PP.preRunRan should be true').toBe(true);
        expect(ppState.captured, '__AIT_PP.captured should be true').toBe(true);

        const status91 = await mockPage.evaluate(() => window['AITPlayerPrefs'].status());
        expect(status91.mirrorCount, 'status().mirrorCount should be > 0 after a successful mirror').toBeGreaterThan(0);
      });

      // -----------------------------------------------------------------------
      // 9-2 (핵심): IndexedDB만 CDP로 wipe하고 reload → 값이 앱인토스 Storage
      //      (mock 백킹 localStorage)로부터 복원되는지 확인
      // -----------------------------------------------------------------------
      test('9-2. value survives reload with IndexedDB wiped', async () => {
        test.setTimeout(90000);
        expect(mockPage, '9-1 should have created mockPage').not.toBeNull();

        const cdp = await mockPage.context().newCDPSession(mockPage);
        const origin = new URL(mockPage.url()).origin;
        // localStorage(mock 백킹)는 보존, IndexedDB(IDBFS 미러)만 제거
        await cdp.send('Storage.clearDataForOrigin', {
          origin,
          storageTypes: 'indexeddb,cache_storage'
        });

        // CDP wipe가 실제로 IndexedDB를 비웠는지 확인한다. IDBFS.getDB는 IndexedDB
        // 커넥션을 dbs 캐시에 열어둔 채 유지하므로, 이 커넥션이 살아있으면
        // clearDataForOrigin이 에러 없이 resolve되면서도 조용히 부분 실패할 수 있다.
        //
        // 단, 이 열린 커넥션 때문에 헤드리스 Chrome CI 환경에서 indexedDB.databases()
        // 자체가 내부적으로 멈춰(hang) Playwright의 evaluate promise가 GC되며
        // "Resulting promise was garbage collected" 에러로 죽는 사례가 관측됐다
        // (모든 OS/Unity 버전 조합에서 재현). 이 검증은 부가적인 안전장치이므로,
        // 실패/행에도 본 테스트의 핵심 단언(재로드 후 앱인토스 Storage 경로로
        // 복원되는지)을 막지 않도록 soft-skip 처리한다.
        let dbsAfterWipe = null;
        try {
          dbsAfterWipe = await mockPage.evaluate(async () => {
            if (typeof indexedDB.databases !== 'function') return null; // 미지원 브라우저는 스킵
            var dbs = await indexedDB.databases();
            return dbs.map(function (d) { return d.name; });
          });
        } catch (e) {
          console.log(`[9-2] indexedDB.databases() verification failed/hung (${e.message}) — IDBFS의 열린 커넥션으로 인한 알려진 환경 제약으로 보고 skip`);
          dbsAfterWipe = null;
        }
        if (dbsAfterWipe !== null) {
          expect(dbsAfterWipe, 'IndexedDB should be empty after Storage.clearDataForOrigin').toEqual([]);
        }

        const response = await mockPage.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
        expect(response?.status()).toBe(200);
        await waitForUnityInstance(mockPage);

        // 이 테스트가 검증하려는 기능(앱인토스 Storage 경로 복원)이 실제로 실행됐는지
        // status()로 먼저 확인한다 — 원본 IDBFS populate만으로 우연히 값이 살아남아도
        // (예: CDP wipe 부분 실패) mode/restoredBytes 단언이 없으면 이 구멍을 못 잡는다.
        const status92 = await mockPage.evaluate(() => window['AITPlayerPrefs'].status());
        expect(status92.mode, 'restore should have gone through the AIT overlay path (mode===ait)').toBe('ait');
        expect(status92.restoredBytes, 'restoredBytes should be > 0 after an AIT snapshot restore').toBeGreaterThan(0);

        const result = await triggerPlayerPrefsAndWait(
          mockPage,
          () => mockPage.evaluate((key) => window['TriggerPlayerPrefsGet'](key), 'ait_e2e_pp'),
          'get'
        );
        console.log(`[9-2] TriggerPlayerPrefsGet result: ${JSON.stringify(result)}`);
        expect(result.success, 'PlayerPrefs.GetString should succeed').toBe(true);
        expect(result.value, 'value should survive IndexedDB wipe via platform Storage restore').toBe('v1');
      });

      // -----------------------------------------------------------------------
      // 9-3: 항상 reject하는 mock → 부트가 막히면 안 되고, disabled=true로
      //      보고되어야 하며, 처리되지 않은 예외/거부가 없어야 한다
      // -----------------------------------------------------------------------
      test('9-3. platform Storage failure must not block boot', async ({ browser }) => {
        test.setTimeout(120000);

        failPage = await browser.newPage();

        const pageErrors = [];
        failPage.on('pageerror', (err) => pageErrors.push(err.message));

        await failPage.addInitScript(() => {
          window['__AIT_PLAYERPREFS_STORAGE__'] = {
            getItem: function () { return Promise.reject(new Error('mock storage getItem failure')); },
            setItem: function () { return Promise.reject(new Error('mock storage setItem failure')); }
          };
          window['__unhandledRejections'] = [];
          window.addEventListener('unhandledrejection', function (e) {
            var reason = e && e.reason;
            window['__unhandledRejections'].push(reason && reason.message ? reason.message : String(reason));
          });
        });

        const response = await failPage.goto(`http://localhost:${sharedPort}?e2e=true`, {
          waitUntil: 'domcontentloaded',
          timeout: 60000
        });
        expect(response?.status()).toBe(200);
        await waitForUnityInstance(failPage);

        const status = await failPage.evaluate(() => window['AITPlayerPrefs'].status());
        console.log(`[9-3] AITPlayerPrefs.status(): ${JSON.stringify(status)}`);
        expect(status.disabled, 'status().disabled should be true when platform Storage always fails').toBe(true);

        const unhandled = await failPage.evaluate(() => window['__unhandledRejections'] || []);
        expect(unhandled.length, `no unhandled rejections: ${JSON.stringify(unhandled)}`).toBe(0);
        expect(pageErrors.length, `no page errors: ${JSON.stringify(pageErrors)}`).toBe(0);
      });

      // -----------------------------------------------------------------------
      // 9-4: 9-3 상태(disabled)에서 IndexedDB는 그대로 두고 reload —
      //      IDBFS 경로 무회귀 확인 (Set+Save→reload→Get)
      // -----------------------------------------------------------------------
      test('9-4. falls back to IndexedDB when platform Storage errors', async () => {
        // 느린 CI 러너에서 failPage 부트만 ~70초 소요 실측됨 — 90초는 여유가 부족하다
        test.setTimeout(120000);
        expect(failPage, '9-3 should have created failPage').not.toBeNull();

        // persistCount 베이스라인: PlayerPrefs.Save()는 JS queuePersist(비동기 커밋 시작)만
        // 걸고 리턴하므로, "성공" 보고 직후 바로 reload하면 IndexedDB 커밋이 끝나기 전에
        // reload되어 값이 유실될 수 있다(flaky). persist 완료(성공/실패 무관)를 관측해야 한다.
        const persistCountBefore = await failPage.evaluate(() => window['__AIT_PP'].persistCount);

        const setResult = await triggerPlayerPrefsAndWait(
          failPage,
          () => failPage.evaluate((json) => window['TriggerPlayerPrefsSet'](json),
            JSON.stringify({ key: 'ait_e2e_pp2', value: 'v2' })),
          'set'
        );
        expect(setResult.success, 'PlayerPrefs.SetString + Save should succeed even when platform Storage is disabled').toBe(true);

        // reload 전에 persist(populate=false) 방향이 최종 cb까지 완료됐는지 대기
        await failPage.waitForFunction(
          (baseline) => window['__AIT_PP'].persistCount > baseline,
          persistCountBefore,
          { timeout: 30000 }
        );

        // IndexedDB는 건드리지 않고 reload (CDP wipe 없음)
        const response = await failPage.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
        expect(response?.status()).toBe(200);
        await waitForUnityInstance(failPage);

        const getResult = await triggerPlayerPrefsAndWait(
          failPage,
          () => failPage.evaluate((key) => window['TriggerPlayerPrefsGet'](key), 'ait_e2e_pp2'),
          'get'
        );
        console.log(`[9-4] TriggerPlayerPrefsGet result: ${JSON.stringify(getResult)}`);
        expect(getResult.success, 'PlayerPrefs.GetString should succeed').toBe(true);
        expect(getResult.value, 'IndexedDB(IDBFS) round-trip must keep working when platform Storage is disabled').toBe('v2');
      });

      // -----------------------------------------------------------------------
      // 9-5: mock 없음 — 순정 프로덕션 페이지에서 회귀(에러/거부)가 없어야 하며,
      //      mount 트랩은 storage 가용성과 무관하게 발화해야 한다
      // -----------------------------------------------------------------------
      test('9-5. no mock: no rejections, no boot regression', async ({ browser }) => {
        test.setTimeout(120000);

        noMockPage = await browser.newPage();

        const consoleErrors = [];
        noMockPage.on('console', (msg) => {
          if (msg.type() === 'error') consoleErrors.push(msg.text());
        });
        const pageErrors = [];
        noMockPage.on('pageerror', (err) => pageErrors.push(err.message));

        await noMockPage.addInitScript(() => {
          window['__unhandledRejections'] = [];
          window.addEventListener('unhandledrejection', function (e) {
            var reason = e && e.reason;
            window['__unhandledRejections'].push(reason && reason.message ? reason.message : String(reason));
          });
        });

        const response = await noMockPage.goto(`http://localhost:${sharedPort}?e2e=true`, {
          waitUntil: 'domcontentloaded',
          timeout: 60000
        });
        expect(response?.status()).toBe(200);
        await waitForUnityInstance(noMockPage);

        const ppCaptured = await noMockPage.evaluate(() => window['__AIT_PP'].captured);
        expect(ppCaptured, '__AIT_PP.captured should be true regardless of storage backend availability').toBe(true);

        const unhandled = await noMockPage.evaluate(() => window['__unhandledRejections'] || []);
        expect(unhandled.length, `no unhandled rejections: ${JSON.stringify(unhandled)}`).toBe(0);
        expect(pageErrors.length, `no page errors: ${JSON.stringify(pageErrors)}`).toBe(0);

        const aitConsoleErrors = consoleErrors.filter((t) => /\[AIT-PP\]|AITPlayerPrefs/.test(t));
        expect(aitConsoleErrors.length,
          `no AITPlayerPrefs-related console.error: ${JSON.stringify(aitConsoleErrors)}`).toBe(0);
      });

    }); // end of test.describe.serial('9. ...')

  }); // end of test.describe.serial

});
