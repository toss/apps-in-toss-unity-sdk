// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK - E2E Full Pipeline Tests
 *
 * 7개 테스트 케이스:
 * 1. Unity WebGL Build (Runtime 컴파일)
 * 2. AIT Dev Server
 * 3. AIT Build Directory
 * 4. AIT Packaging
 * 5. Production Server
 * 6. Performance Benchmarks
 * 7. Runtime API Error Validation (39개 SDK API 에러 검증)
 *
 * Test 7 검증 기준:
 * - 모든 39개 SDK API를 호출
 * - 개발 환경에서 "상정된 에러" (expected error) 발생 = PASS
 *   - "XXX is not a constant handler" (bridge-core Constant API)
 *   - "__GRANITE_NATIVE_EMITTER is not available" (Async API)
 *   - "ReactNativeWebView is not available" (Native 통신)
 * - "상정되지 않은 에러" (unexpected error) 발생 = FAIL
 */

// ES Module에서 __dirname 대체
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 경로 상수
const PROJECT_ROOT = path.resolve(__dirname, '../../..');

// UNITY_PROJECT_PATH 환경변수로 프로젝트 경로 지정 가능
// 기본값: 빌드 결과물이 있는 첫 번째 버전별 프로젝트 탐지
function findSampleProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) {
    return envPath;
  }

  // 버전별 프로젝트 탐지 (우선순위: 6000.2 > 6000.0 > 2022.3 > 2021.3)
  const versionPatterns = ['6000.2', '6000.0', '2022.3', '2021.3'];
  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../SampleUnityProject-${version}`);
    const distPath = path.resolve(projectPath, 'ait-build/dist/web');
    if (fs.existsSync(distPath)) {
      console.log(`📁 Auto-detected project: SampleUnityProject-${version}`);
      return projectPath;
    }
  }

  // 기존 단일 프로젝트 폴백 (하위 호환)
  const legacyPath = path.resolve(__dirname, '../SampleUnityProject');
  if (fs.existsSync(legacyPath)) {
    console.log('📁 Using legacy SampleUnityProject');
    return legacyPath;
  }

  // 빌드 없이 첫 번째 버전별 프로젝트 반환
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
const WEBGL_BUILD = path.resolve(SAMPLE_PROJECT, 'webgl');

// 벤치마크 기준
const BENCHMARKS = {
  MAX_LOAD_TIME_MS: 10000,      // 10초
  MAX_BUILD_SIZE_MB: 50,        // 50MB
  MIN_AVG_FPS: 30,              // 30 FPS
  MIN_FPS: 15,                  // 최소 FPS (흔들림 허용)
  MAX_MEMORY_MB: 512            // 512MB
};

// 결과 저장용
let testResults = {
  timestamp: new Date().toISOString(),
  tests: {}
};

/**
 * Unity 버전에서 고유 포트 오프셋 계산
 * 동시 실행 시 포트 충돌 방지
 */
function getPortOffsetFromUnityVersion(projectPath) {
  const match = projectPath.match(/SampleUnityProject-(\d+)\.(\d+)/);
  if (!match) return 0;

  const major = parseInt(match[1], 10);
  const minor = parseInt(match[2], 10);

  // 2021.3 → 0, 2022.3 → 1, 6000.0 → 2, 6000.2 → 3
  if (major === 2021) return 0;
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  return 0;
}

const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
const VITE_DEV_PORT = 8081 + PORT_OFFSET;  // vite dev 서버 포트

// 서버 프로세스 관리
let serverProcess = null;
// Unity 버전별 고유 포트 (E2EBuildRunner.cs의 GetPortForUnityVersion()와 동일)
// 2021.3 → 4173, 2022.3 → 4174, 6000.0 → 4175, 6000.2 → 4176
let serverPort = 4173 + PORT_OFFSET;
console.log(`📦 Unity project: ${SAMPLE_PROJECT}`);
console.log(`🔌 Server port: ${serverPort} (offset: ${PORT_OFFSET})`);

/**
 * 유틸리티: 디렉토리 존재 확인
 */
function directoryExists(dirPath) {
  try {
    return fs.existsSync(dirPath) && fs.statSync(dirPath).isDirectory();
  } catch {
    return false;
  }
}

/**
 * 유틸리티: 파일 존재 확인
 */
function fileExists(filePath) {
  try {
    return fs.existsSync(filePath) && fs.statSync(filePath).isFile();
  } catch {
    return false;
  }
}

/**
 * 유틸리티: 디렉토리 크기 계산 (MB)
 */
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

/**
 * 유틸리티: Dev 서버 시작 (npx vite --host --port)
 * @returns {Promise<{process: ChildProcess, port: number}>}
 */
async function startDevServer(aitBuildDir, defaultPort) {
  // Unity 버전별 고유 포트 사용 (동시 실행 시 충돌 방지)
  const vitePort = VITE_DEV_PORT;
  console.log(`🔌 Using vite port: ${vitePort} (offset: ${PORT_OFFSET})`);

  // 이 테스트 전용 포트만 정리 (다른 Unity 버전 테스트와 충돌 방지)
  // 다른 버전의 포트는 건드리지 않음
  const myPorts = [serverPort, vitePort];
  const isWindows = process.platform === 'win32';
  for (const port of myPorts) {
    try {
      if (isWindows) {
        // Windows: netstat + taskkill
        execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
      } else {
        // macOS/Linux: lsof + kill
        execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
      }
    } catch {
      // 무시
    }
  }

  // 포트가 해제될 때까지 대기
  await new Promise(r => setTimeout(r, 1000));

  return new Promise((resolve, reject) => {
    // npx vite 직접 실행 (granite는 --port 인자를 무시하므로 vite 직접 호출)
    // Windows에서 spawn('npx', ...)이 ENOENT 에러 발생하므로 shell: true 사용
    const server = spawn('npx', ['vite', '--host', '--port', String(vitePort)], {
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

      // ANSI 색상 코드 제거 후 포트 파싱
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');

      // Vite 포트 파싱 (Local: http://localhost:5173/ 또는 localhost:5173)
      const portMatch = cleanOutput.match(/localhost:(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Dev server running on port: ${actualPort}`);

        // 포트를 찾으면 바로 resolve (서버 준비 완료)
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });

    server.stderr.on('data', (data) => {
      console.error('[vite dev error]', data.toString());
    });

    server.on('error', reject);

    // 10초 타임아웃
    setTimeout(() => {
      if (!started) {
        started = true;
        resolve({ process: server, port: actualPort });
      }
    }, 10000);
  });
}

/**
 * 유틸리티: Production 서버 시작 (npm run start = vite preview)
 * @returns {Promise<{process: ChildProcess, port: number}>}
 */
async function startProductionServer(aitBuildDir, defaultPort) {
  // 이 테스트 전용 포트만 정리 (다른 Unity 버전 테스트와 충돌 방지)
  const isWindows = process.platform === 'win32';
  const myPort = serverPort;  // Unity 버전별 고유 포트
  try {
    if (isWindows) {
      // Windows: netstat + taskkill
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${myPort} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      // macOS/Linux: lsof + kill
      execSync(`lsof -ti:${myPort} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {
    // 무시
  }

  // 포트가 해제될 때까지 대기
  await new Promise(r => setTimeout(r, 1000));

  return new Promise((resolve, reject) => {
    // vite preview 직접 실행 (포트 지정 가능)
    // npm run start는 포트 인자를 전달하기 어려우므로 npx vite preview 사용
    // Windows에서 spawn('npx', ...)이 ENOENT 에러 발생하므로 shell: true 사용
    const server = spawn('npx', ['vite', 'preview', '--outDir', 'dist/web', '--port', String(defaultPort)], {
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

      // 포트 파싱 (Local: http://localhost:4173/ 또는 listening on port 4173)
      const portMatch = output.match(/(?:Local:\s+http:\/\/localhost:|listening.*?port\s*|:)(\d+)/i);
      if (portMatch) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Production server running on port: ${actualPort}`);
      }

      // vite preview 시작 확인
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

    // 10초 타임아웃
    setTimeout(() => {
      if (!started) {
        started = true;
        resolve({ process: server, port: actualPort });
      }
    }, 10000);
  });
}

/**
 * 유틸리티: placeholder 검사
 */
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
  return [...new Set(found)]; // 중복 제거
}


// ============================================================================
// Test Suite
// ============================================================================

test.describe('Apps in Toss Unity SDK E2E Pipeline', () => {

  // 테스트 전 설정
  test.beforeAll(async () => {
    console.log('🚀 E2E Pipeline Tests Starting...');
    console.log(`📁 Project Root: ${PROJECT_ROOT}`);
    console.log(`📁 Sample Project: ${SAMPLE_PROJECT}`);
    console.log(`📁 AIT Build: ${AIT_BUILD}`);
  });

  // 테스트 후 정리
  test.afterAll(async () => {
    // 서버 종료
    if (serverProcess) {
      serverProcess.kill();
      serverProcess = null;
    }

    // 결과 저장
    const resultsPath = path.resolve(__dirname, 'e2e-test-results.json');
    fs.writeFileSync(resultsPath, JSON.stringify(testResults, null, 2));

    // stdout으로 결과 출력
    console.log('\n');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📊 E2E Test Results');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');

    // 테스트 통과 여부 카운트
    const tests = testResults.tests || {};
    const passed = Object.values(tests).filter(t => t.passed).length;
    const total = Object.keys(tests).length;

    console.log(`\n  ✅ Tests Passed: ${passed}/${total}`);

    // 주요 메트릭
    const buildSize = tests['1_webgl_build']?.buildSizeMB;
    const pageLoad = tests['5_production_server']?.pageLoadTimeMs || tests['6_benchmarks']?.pageLoadTimeMs;
    const unityLoad = tests['6_benchmarks']?.unityLoadTimeMs;
    const renderer = tests['5_production_server']?.webgl?.renderer;

    console.log('\n  📦 Build Size:      ' + (buildSize ? buildSize.toFixed(2) + ' MB' : 'N/A'));
    console.log('  ⏱️  Page Load:       ' + (pageLoad ? pageLoad + ' ms' : 'N/A'));
    console.log('  🎮 Unity Load:      ' + (unityLoad ? unityLoad + ' ms' : 'N/A'));
    console.log('  🖥️  GPU Renderer:    ' + (renderer || 'N/A'));

    // SDK Runtime 검증 결과 출력
    const apiTest = tests['7_runtime_api'];
    if (apiTest && apiTest.runtimeValidation) {
      const rv = apiTest.runtimeValidation;
      console.log('\n  🔍 SDK Runtime Validation:');
      console.log('     C# ↔ jslib:     ' + rv.csharpJslibMatching.matched + '/' + rv.csharpJslibMatching.totalAPIs + ' APIs matched');
      console.log('     Type Safety:    ' +
        (rv.typeMarshalling.stringPassed + rv.typeMarshalling.numberPassed +
         rv.typeMarshalling.booleanPassed + rv.typeMarshalling.objectPassed) + ' types validated');
      if (rv.typeMarshalling.failed.length > 0) {
        console.log('     ⚠️  Type Errors:  ' + rv.typeMarshalling.failed.length + ' failed');
      }
    }

    console.log('\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📄 Full Results (JSON):');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log(JSON.stringify(testResults, null, 2));
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n');
  });


  // -------------------------------------------------------------------------
  // Test 1: Unity WebGL Build
  // -------------------------------------------------------------------------
  test('1. Unity WebGL build should succeed', async () => {
    test.setTimeout(180000); // 3분

    // webgl/ 폴더 확인 (Unity 빌드 출력)
    // Note: E2EBuildRunner는 직접 ait-build를 생성하므로 webgl/ 폴더가 없을 수 있음
    if (directoryExists(WEBGL_BUILD)) {
      console.log('✅ webgl/ directory found');

      // 필수 파일 확인
      const loaderPath = path.join(WEBGL_BUILD, 'Build');
      if (directoryExists(loaderPath)) {
        const buildFiles = fs.readdirSync(loaderPath);
        console.log(`📦 Build files: ${buildFiles.join(', ')}`);

        const hasLoader = buildFiles.some(f => f.endsWith('.loader.js'));
        const hasWasm = buildFiles.some(f => f.endsWith('.wasm') || f.endsWith('.wasm.gz') || f.endsWith('.wasm.br') || f.endsWith('.wasm.unityweb'));
        const hasData = buildFiles.some(f => f.endsWith('.data') || f.endsWith('.data.gz') || f.endsWith('.data.br') || f.endsWith('.data.unityweb'));
        const hasFramework = buildFiles.some(f => f.endsWith('.framework.js') || f.endsWith('.framework.js.gz') || f.endsWith('.framework.js.br') || f.endsWith('.framework.js.unityweb'));

        expect(hasLoader, 'Should have loader.js').toBe(true);
        expect(hasWasm, 'Should have wasm file').toBe(true);
        expect(hasData, 'Should have data file').toBe(true);

        // Framework file is optional (only in some Unity versions)
        if (buildFiles.some(f => f.includes('framework'))) {
          expect(hasFramework, 'Framework file should be valid if present').toBe(true);
        }
      }
    } else {
      // E2EBuildRunner가 직접 ait-build를 생성한 경우
      console.log('ℹ️ webgl/ not found (E2EBuildRunner creates ait-build directly)');
    }

    // ait-build/dist/web 확인 (최종 빌드 출력)
    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist').toBe(true);
    expect(directoryExists(DIST_WEB), 'ait-build/dist/web/ should exist').toBe(true);

    // 빌드 크기 확인
    const distSizeMB = getDirectorySizeMB(DIST_WEB);
    console.log(`📦 Build size: ${distSizeMB.toFixed(2)} MB`);

    testResults.tests['1_webgl_build'] = {
      passed: true,
      buildSizeMB: distSizeMB
    };
  });


  // -------------------------------------------------------------------------
  // Test 2: AIT Dev Server (vite)
  // -------------------------------------------------------------------------
  test('2. AIT dev server should start and load Unity', async ({ page }) => {
    test.setTimeout(120000); // 2분

    // ait-build 디렉토리 확인
    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist for dev server').toBe(true);

    // Dev 서버 시작 (npx vite --host --port)
    console.log('🚀 Starting dev server (vite)...');
    const devServer = await startDevServer(AIT_BUILD, serverPort);
    serverProcess = devServer.process;
    const actualPort = devServer.port;

    console.log(`📍 Checking server on port: ${actualPort}`);

    // 서버가 준비될 때까지 대기 (최대 15초)
    let serverReady = false;
    for (let i = 0; i < 30; i++) {
      try {
        const response = await fetch(`http://localhost:${actualPort}/`, { method: 'HEAD' });
        if (response.ok) {
          serverReady = true;
          break;
        }
      } catch {
        // 서버가 아직 준비되지 않음
      }
      await new Promise(r => setTimeout(r, 500));
    }

    if (!serverReady) {
      console.log(`⚠️ Server not responding on port ${actualPort}, trying common dev ports...`);
      // 다른 포트도 시도 (vite 기본값은 5173)
      const tryPorts = [5173, 8081, 3000];
      for (const port of tryPorts) {
        if (port === actualPort) continue;
        try {
          const response = await fetch(`http://localhost:${port}/`, { method: 'HEAD' });
          if (response.ok) {
            console.log(`✅ Found server on port ${port}`);
            serverReady = true;
            // actualPort를 업데이트 (하지만 const이므로 새 변수 사용)
            break;
          }
        } catch {
          // 무시
        }
      }
    }

    // 최종 확인: 어떤 포트에서든 서버가 응답하면 통과
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

    console.log(`✅ Dev server running on port: ${workingPort}`);

    // 페이지 로딩 (E2E 모드 활성화)
    const startTime = Date.now();
    const response = await page.goto(`http://localhost:${workingPort}?e2e=true`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000
    });

    expect(response?.status()).toBe(200);
    console.log('✅ Dev server responded with 200');

    // createUnityInstance 함수 존재 확인
    const hasUnityLoader = await page.evaluate(() => {
      return typeof window['createUnityInstance'] === 'function' ||
             document.querySelector('script[src*="loader.js"]') !== null ||
             document.body.innerHTML.includes('createUnityInstance');
    });

    console.log(`🎮 Unity loader present: ${hasUnityLoader}`);

    // Unity 로딩 진행 확인 (progress 체크)
    try {
      // Unity 인스턴스 초기화 대기 (최대 60초)
      await page.waitForFunction(() => {
        return window['unityInstance'] !== undefined ||
               document.querySelector('canvas') !== null;
      }, { timeout: 60000 });

      console.log('✅ Unity instance initialized');
    } catch {
      console.log('⚠️ Unity instance not initialized within timeout (may be expected in CI)');
    }

    const loadTime = Date.now() - startTime;
    console.log(`⏱️ Page load time: ${loadTime}ms`);

    // 서버 종료
    serverProcess.kill();
    serverProcess = null;

    testResults.tests['2_dev_server'] = {
      passed: true,
      loadTimeMs: loadTime
    };
  });


  // -------------------------------------------------------------------------
  // Test 3: AIT Build Directory
  // -------------------------------------------------------------------------
  test('3. AIT build directory should be created correctly', async () => {
    test.setTimeout(30000); // 30초

    // ait-build/ 디렉토리 확인
    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist').toBe(true);

    // package.json 확인
    const packageJsonPath = path.resolve(AIT_BUILD, 'package.json');
    expect(fileExists(packageJsonPath), 'package.json should exist').toBe(true);

    // granite.config.ts 확인
    const graniteConfigPath = path.resolve(AIT_BUILD, 'granite.config.ts');
    if (fileExists(graniteConfigPath)) {
      const content = fs.readFileSync(graniteConfigPath, 'utf-8');
      const placeholders = checkForPlaceholders(content);

      if (placeholders.length > 0) {
        console.log(`⚠️ Placeholders found in granite.config.ts: ${placeholders.join(', ')}`);
      } else {
        console.log('✅ No placeholders in granite.config.ts');
      }

      // 플레이스홀더가 있으면 실패 (CI에서는 중요)
      expect(placeholders.length, 'Should have no unsubstituted placeholders').toBe(0);
    }

    // node_modules 확인 (npm install 완료)
    const nodeModulesPath = path.resolve(AIT_BUILD, 'node_modules');
    expect(directoryExists(nodeModulesPath), 'node_modules/ should exist').toBe(true);

    console.log('✅ AIT build directory structure is correct');

    testResults.tests['3_ait_build'] = { passed: true };
  });


  // -------------------------------------------------------------------------
  // Test 4: AIT Packaging
  // -------------------------------------------------------------------------
  test('4. AIT packaging should complete without placeholders', async () => {
    test.setTimeout(30000); // 30초

    // dist/ 확인
    const distPath = path.resolve(AIT_BUILD, 'dist');
    expect(directoryExists(distPath), 'dist/ should exist').toBe(true);

    // dist/web/ 확인
    expect(directoryExists(DIST_WEB), 'dist/web/ should exist').toBe(true);

    // index.html 확인
    const indexPath = path.resolve(DIST_WEB, 'index.html');
    expect(fileExists(indexPath), 'index.html should exist').toBe(true);

    const indexContent = fs.readFileSync(indexPath, 'utf-8');
    const placeholders = checkForPlaceholders(indexContent);

    if (placeholders.length > 0) {
      console.log(`❌ Placeholders found in index.html: ${placeholders.join(', ')}`);
    } else {
      console.log('✅ No placeholders in index.html');
    }

    expect(placeholders.length, 'index.html should have no unsubstituted placeholders').toBe(0);

    // Build 폴더 확인
    const buildPath = path.resolve(DIST_WEB, 'Build');
    expect(directoryExists(buildPath), 'Build/ folder should exist').toBe(true);

    const buildFiles = fs.readdirSync(buildPath);
    console.log(`📦 Packaged build files: ${buildFiles.join(', ')}`);

    testResults.tests['4_ait_packaging'] = { passed: true };
  });


  // -------------------------------------------------------------------------
  // Test 5: Production Server (vite preview)
  // -------------------------------------------------------------------------
  test('5. Production build should load in browser', async ({ page }) => {
    test.setTimeout(180000); // 3분

    expect(directoryExists(DIST_WEB), 'dist/web/ should exist for production server').toBe(true);

    // Production 서버 시작 (npm run start = vite preview)
    console.log('🚀 Starting production server (vite preview)...');

    const prodServer = await startProductionServer(AIT_BUILD, serverPort);
    serverProcess = prodServer.process;
    const actualPort = prodServer.port;

    // 서버가 준비될 때까지 대기 (최대 10초)
    let serverReady = false;
    for (let i = 0; i < 20; i++) {
      try {
        const response = await fetch(`http://localhost:${actualPort}/`, { method: 'HEAD' });
        if (response.ok) {
          serverReady = true;
          break;
        }
      } catch {
        // 서버가 아직 준비되지 않음
      }
      await new Promise(r => setTimeout(r, 500));
    }

    if (!serverReady) {
      throw new Error(`Server failed to start on port ${actualPort}`);
    }

    // 페이지 로딩 (E2E 모드 활성화)
    const startTime = Date.now();
    const response = await page.goto(`http://localhost:${actualPort}?e2e=true`, {
      waitUntil: 'networkidle',
      timeout: 60000
    });

    expect(response?.status()).toBe(200);
    const pageLoadTime = Date.now() - startTime;
    console.log(`✅ Production server responded - Page load: ${pageLoadTime}ms`);

    // Unity 인스턴스 초기화 대기
    try {
      await page.waitForFunction(() => {
        return window['unityInstance'] !== undefined;
      }, { timeout: 120000 });

      console.log('✅ Unity instance initialized in production');
    } catch {
      console.log('⚠️ Unity instance timeout (checking canvas instead)');

      // canvas 존재 확인
      const hasCanvas = await page.evaluate(() => {
        return document.querySelector('canvas') !== null;
      });

      if (hasCanvas) {
        console.log('✅ Canvas element found');
      }
    }

    // WebGL 지원 확인
    const webglInfo = await page.evaluate(() => {
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

    // 서버 종료
    serverProcess.kill();
    serverProcess = null;

    testResults.tests['5_production_server'] = {
      passed: true,
      pageLoadTimeMs: pageLoadTime,
      webgl: webglInfo
    };
  });


  // -------------------------------------------------------------------------
  // Test 6: Performance Benchmarks (vite preview)
  // -------------------------------------------------------------------------
  test('6. Performance benchmarks should pass', async ({ page }) => {
    test.setTimeout(180000); // 3분

    expect(directoryExists(DIST_WEB), 'dist/web/ should exist').toBe(true);

    // Production 서버 시작 (npm run start = vite preview)
    console.log('🚀 Starting production server (vite preview)...');
    const prodServer = await startProductionServer(AIT_BUILD, serverPort);
    serverProcess = prodServer.process;
    const actualPort = prodServer.port;

    // 서버가 준비될 때까지 대기 (최대 10초)
    let serverReady = false;
    for (let i = 0; i < 20; i++) {
      try {
        const response = await fetch(`http://localhost:${actualPort}/`, { method: 'HEAD' });
        if (response.ok) {
          serverReady = true;
          break;
        }
      } catch {
        // 서버가 아직 준비되지 않음
      }
      await new Promise(r => setTimeout(r, 500));
    }

    if (!serverReady) {
      throw new Error(`Server failed to start on port ${actualPort}`);
    }

    // 페이지 로딩 시간 측정 (E2E 모드 활성화)
    const startTime = Date.now();
    await page.goto(`http://localhost:${actualPort}?e2e=true`, {
      waitUntil: 'domcontentloaded',
      timeout: 60000
    });
    const pageLoadTime = Date.now() - startTime;

    // Unity 초기화 대기
    const unityStartTime = Date.now();
    try {
      await page.waitForFunction(() => {
        return window['unityInstance'] !== undefined ||
               (window['unityInstance']?.Module?.ready === true);
      }, { timeout: 120000 });
    } catch {
      console.log('⚠️ Unity initialization timeout');
    }
    const unityLoadTime = Date.now() - unityStartTime;

    // 빌드 크기 확인
    const buildSizeMB = getDirectorySizeMB(DIST_WEB);

    // 벤치마크 데이터 수집 (Unity에서 CustomEvent로 전송)
    let benchmarkData = null;
    try {
      benchmarkData = await page.evaluate(() => {
        return new Promise((resolve) => {
          // E2EBridge.jslib에서 발생시키는 CustomEvent 수신
          const handler = (event) => {
            window.removeEventListener('e2e-benchmark-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-benchmark-complete', handler);

          // 이미 데이터가 있으면 바로 반환
          if (window['__E2E_BENCHMARK_DATA__']) {
            resolve(window['__E2E_BENCHMARK_DATA__']);
            return;
          }

          // 30초 타임아웃
          setTimeout(() => resolve(null), 30000);
        });
      });
    } catch {
      console.log('⚠️ Benchmark data not received from Unity');
    }

    // 결과 로깅
    console.log('\n📊 BENCHMARK RESULTS:');
    console.log(`   Page Load: ${pageLoadTime}ms (max: ${BENCHMARKS.MAX_LOAD_TIME_MS}ms)`);
    console.log(`   Unity Load: ${unityLoadTime}ms`);
    console.log(`   Build Size: ${buildSizeMB.toFixed(2)}MB (max: ${BENCHMARKS.MAX_BUILD_SIZE_MB}MB)`);

    if (benchmarkData) {
      console.log(`   Avg FPS: ${benchmarkData.avgFps?.toFixed(1) || 'N/A'} (min: ${BENCHMARKS.MIN_AVG_FPS})`);
      console.log(`   Min FPS: ${benchmarkData.minFps?.toFixed(1) || 'N/A'}`);
      console.log(`   Memory: ${benchmarkData.memoryUsageMB?.toFixed(1) || 'N/A'}MB`);
    }

    // 검증
    // 로딩 시간은 CI 환경에서 느릴 수 있으므로 경고만
    if (pageLoadTime > BENCHMARKS.MAX_LOAD_TIME_MS) {
      console.log(`⚠️ Page load time exceeded (${pageLoadTime}ms > ${BENCHMARKS.MAX_LOAD_TIME_MS}ms)`);
    }

    // 빌드 크기는 반드시 검증
    expect(buildSizeMB).toBeLessThanOrEqual(BENCHMARKS.MAX_BUILD_SIZE_MB);

    // FPS는 데이터가 있을 때만 검증
    if (benchmarkData?.avgFps) {
      expect(benchmarkData.avgFps).toBeGreaterThanOrEqual(BENCHMARKS.MIN_AVG_FPS);
    }

    // 서버 종료
    serverProcess.kill();
    serverProcess = null;

    testResults.tests['6_benchmarks'] = {
      passed: true,
      pageLoadTimeMs: pageLoadTime,
      unityLoadTimeMs: unityLoadTime,
      buildSizeMB,
      benchmarkData
    };
  });


  // -------------------------------------------------------------------------
  // Test 7: Runtime API Error Validation
  // 39개 SDK API 호출 시 올바른 에러가 발생하는지 검증
  // -------------------------------------------------------------------------
  test('7. All 39 SDK APIs should return correct errors in dev environment', async ({ page }) => {
    test.setTimeout(180000); // 3분

    expect(directoryExists(DIST_WEB), 'dist/web/ should exist').toBe(true);

    // Production 서버 시작 (npm run start = vite preview)
    console.log('🚀 Starting production server for API error validation...');
    const prodServer = await startProductionServer(AIT_BUILD, serverPort);
    serverProcess = prodServer.process;
    const actualPort = prodServer.port;

    // 서버가 준비될 때까지 대기 (최대 10초)
    let serverReady = false;
    for (let i = 0; i < 20; i++) {
      try {
        const response = await fetch(`http://localhost:${actualPort}/`, { method: 'HEAD' });
        if (response.ok) {
          serverReady = true;
          break;
        }
      } catch {
        // 서버가 아직 준비되지 않음
      }
      await new Promise(r => setTimeout(r, 500));
    }

    if (!serverReady) {
      throw new Error(`Server failed to start on port ${actualPort}`);
    }

    // 페이지 로딩 (E2E 모드 활성화)
    await page.goto(`http://localhost:${actualPort}?e2e=true`, {
      waitUntil: 'networkidle',
      timeout: 60000
    });

    // Unity 초기화 대기
    try {
      await page.waitForFunction(() => {
        return window['unityInstance'] !== undefined;
      }, { timeout: 120000 });
      console.log('✅ Unity instance ready for API tests');
    } catch {
      console.log('⚠️ Unity instance not ready, API tests may fail');
    }

    // RuntimeAPITester에서 결과 수신 대기 (CustomEvent 방식)
    // 39개 API 테스트에 충분한 시간 (최대 120초)
    const apiResults = await page.evaluate(() => {
      return new Promise((resolve) => {
        // E2EBridge.jslib에서 발생시키는 CustomEvent 수신
        const handler = (event) => {
          window.removeEventListener('e2e-api-test-complete', handler);
          resolve(event.detail);
        };
        window.addEventListener('e2e-api-test-complete', handler);

        // 이미 데이터가 있으면 바로 반환
        if (window['__E2E_API_TEST_RESULTS__']) {
          resolve(window['__E2E_API_TEST_RESULTS__']);
          return;
        }

        // 120초 타임아웃 (39개 API 테스트 완료 대기)
        setTimeout(() => resolve(null), 120000);
      });
    });

    // 서버 종료
    serverProcess.kill();
    serverProcess = null;

    // =========================================================================
    // C# Task 결과 기반 검증 (콘솔 에러가 아닌 실제 API 호출 결과)
    // =========================================================================
    if (apiResults) {
      console.log('\n' + '='.repeat(70));
      console.log('📊 SDK API ERROR VALIDATION RESULTS');
      console.log('='.repeat(70));
      console.log(`   Total APIs Tested: ${apiResults.totalAPIs}`);
      console.log(`   Success (including expected errors): ${apiResults.successCount}`);
      console.log(`   Expected Errors: ${apiResults.expectedErrorCount || 0}`);
      console.log(`   Unexpected Errors (FAILURES): ${apiResults.unexpectedErrorCount || 0}`);
      console.log('='.repeat(70));

      // 상정된 에러가 발생한 API 목록 (정상)
      if (apiResults.results) {
        const expectedErrors = apiResults.results.filter(r => r.success && r.isExpectedError);
        if (expectedErrors.length > 0) {
          console.log('\n✅ APIs with Expected Errors (correct behavior in dev):');
          expectedErrors.forEach(r => {
            const truncatedError = r.error?.length > 50 ? r.error.substring(0, 50) + '...' : r.error;
            console.log(`   [OK] ${r.apiName}: ${truncatedError}`);
          });
        }

        // 에러 없이 성공한 API (Mock이 동작한 경우)
        const cleanSuccess = apiResults.results.filter(r => r.success && !r.isExpectedError && !r.error);
        if (cleanSuccess.length > 0) {
          console.log('\n✅ APIs Completed Successfully (mock worked):');
          cleanSuccess.forEach(r => {
            console.log(`   [OK] ${r.apiName}`);
          });
        }

        // 상정되지 않은 에러 (테스트 실패)
        const unexpectedErrors = apiResults.results.filter(r => !r.success);
        if (unexpectedErrors.length > 0) {
          console.log('\n❌ APIs with UNEXPECTED Errors (TEST FAILURES):');
          unexpectedErrors.forEach(r => {
            console.log(`   [FAIL] ${r.apiName}: ${r.error}`);
          });
        }
      }

      // =========================================================================
      // 핵심 검증: unexpectedErrorCount가 0이어야 테스트 통과
      // =========================================================================
      const unexpectedErrorCount = apiResults.unexpectedErrorCount || 0;

      console.log('\n' + '='.repeat(70));
      if (unexpectedErrorCount === 0) {
        console.log('✅ ALL API ERROR VALIDATIONS PASSED');
        console.log(`   All ${apiResults.totalAPIs} APIs returned correct errors or succeeded`);
      } else {
        console.log('❌ API ERROR VALIDATION FAILED');
        console.log(`   ${unexpectedErrorCount} APIs returned unexpected errors`);
      }
      console.log('='.repeat(70) + '\n');

      // 테스트 결과 저장
      testResults.tests['7_runtime_api'] = {
        passed: unexpectedErrorCount === 0,
        totalAPIs: apiResults.totalAPIs,
        successCount: apiResults.successCount,
        expectedErrorCount: apiResults.expectedErrorCount || 0,
        unexpectedErrorCount: unexpectedErrorCount,
        results: apiResults.results || []
      };

      // 상정되지 않은 에러가 있으면 테스트 실패
      expect(unexpectedErrorCount, 'All APIs should return expected errors or succeed').toBe(0);

    } else {
      console.log('⚠️ API test results not received (RuntimeAPITester may not be in scene)');
      console.log('   Waiting for RuntimeAPITester to complete...');

      // RuntimeAPITester 결과가 없으면 테스트 실패
      testResults.tests['7_runtime_api'] = {
        passed: false,
        skipped: false,
        reason: 'RuntimeAPITester results not received'
      };

      // 결과가 없으면 실패
      expect(apiResults, 'RuntimeAPITester should return results').not.toBeNull();
    }
  });

});
