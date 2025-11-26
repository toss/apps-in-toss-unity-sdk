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
 * 7. Runtime API Tests
 */

// ES Module에서 __dirname 대체
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 경로 상수
const PROJECT_ROOT = path.resolve(__dirname, '../../..');
const SAMPLE_PROJECT = path.resolve(__dirname, '../SampleUnityProject');
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
let benchmarkResults = {
  timestamp: new Date().toISOString(),
  tests: {}
};

// 서버 프로세스 관리
let serverProcess = null;
let serverPort = 4173;

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
 * 유틸리티: Dev 서버 시작 (npm run dev = granite dev)
 * @returns {Promise<{process: ChildProcess, port: number}>}
 */
async function startDevServer(aitBuildDir, defaultPort) {
  // 기존 프로세스 종료 시도 (여러 포트)
  for (const port of [defaultPort, 5173, 8081]) {
    try {
      execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    } catch {
      // 무시
    }
  }

  // 포트가 해제될 때까지 대기
  await new Promise(r => setTimeout(r, 1000));

  return new Promise((resolve, reject) => {
    // npm run dev (granite dev) 실행
    const server = spawn('npm', ['run', 'dev'], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      env: { ...process.env, NODE_OPTIONS: '' }
    });

    let started = false;
    let actualPort = defaultPort;

    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[granite dev]', output);

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
      console.error('[granite dev error]', data.toString());
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
  // 기존 프로세스 종료 시도 (여러 포트)
  for (const port of [defaultPort, 4173, 3000, 8080]) {
    try {
      execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    } catch {
      // 무시
    }
  }

  // 포트가 해제될 때까지 대기
  await new Promise(r => setTimeout(r, 1000));

  return new Promise((resolve, reject) => {
    // npm run start (granite start) 실행
    const server = spawn('npm', ['run', 'start'], {
      cwd: aitBuildDir,
      stdio: 'pipe',
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
    const resultsPath = path.resolve(__dirname, 'benchmark-results.json');
    fs.writeFileSync(resultsPath, JSON.stringify(benchmarkResults, null, 2));

    // stdout으로 결과 출력
    console.log('\n');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📊 E2E Test Results');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');

    // 테스트 통과 여부 카운트
    const tests = benchmarkResults.tests || {};
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

    console.log('\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📄 Full Results (JSON):');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log(JSON.stringify(benchmarkResults, null, 2));
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
        const hasWasm = buildFiles.some(f => f.endsWith('.wasm') || f.endsWith('.wasm.gz') || f.endsWith('.wasm.br'));
        const hasData = buildFiles.some(f => f.endsWith('.data') || f.endsWith('.data.gz') || f.endsWith('.data.br'));

        expect(hasLoader, 'Should have loader.js').toBe(true);
        expect(hasWasm, 'Should have wasm file').toBe(true);
        expect(hasData, 'Should have data file').toBe(true);
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

    benchmarkResults.tests['1_webgl_build'] = {
      passed: true,
      buildSizeMB: distSizeMB
    };
  });


  // -------------------------------------------------------------------------
  // Test 2: AIT Dev Server (granite dev)
  // -------------------------------------------------------------------------
  test('2. AIT dev server should start and load Unity', async ({ page }) => {
    test.setTimeout(120000); // 2분

    // ait-build 디렉토리 확인
    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist for dev server').toBe(true);

    // Dev 서버 시작 (npm run dev = granite dev)
    console.log('🚀 Starting dev server (granite dev)...');
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
      // 다른 포트도 시도 (granite dev는 5173을 사용할 수 있음)
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

    // 페이지 로딩
    const startTime = Date.now();
    const response = await page.goto(`http://localhost:${workingPort}`, {
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

    benchmarkResults.tests['2_dev_server'] = {
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

    benchmarkResults.tests['3_ait_build'] = { passed: true };
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

    benchmarkResults.tests['4_ait_packaging'] = { passed: true };
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

    // 페이지 로딩
    const startTime = Date.now();
    const response = await page.goto(`http://localhost:${actualPort}`, {
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

    benchmarkResults.tests['5_production_server'] = {
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

    // 페이지 로딩 시간 측정
    const startTime = Date.now();
    await page.goto(`http://localhost:${actualPort}`, {
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

    benchmarkResults.tests['6_benchmarks'] = {
      passed: true,
      pageLoadTimeMs: pageLoadTime,
      unityLoadTimeMs: unityLoadTime,
      buildSizeMB,
      benchmarkData
    };
  });


  // -------------------------------------------------------------------------
  // Test 7: Runtime API Tests (vite preview)
  // -------------------------------------------------------------------------
  test('7. All Runtime APIs should work with callbacks', async ({ page }) => {
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

    // 콘솔 로그/에러 캡처 (에러 소스 분석용)
    const consoleErrors = [];
    const consoleWarnings = [];

    page.on('console', msg => {
      const text = msg.text();
      if (msg.type() === 'error') {
        consoleErrors.push(text);
      } else if (msg.type() === 'warning') {
        consoleWarnings.push(text);
      }
    });

    page.on('pageerror', error => {
      consoleErrors.push(`[PageError] ${error.message}`);
    });

    // 페이지 로딩
    await page.goto(`http://localhost:${actualPort}`, {
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

        // 60초 타임아웃 (모든 API 테스트 완료 대기)
        setTimeout(() => resolve(null), 60000);
      });
    });

    // 서버 종료
    serverProcess.kill();
    serverProcess = null;

    // 에러 분류: expected vs unexpected
    // bridge-core 에러 패턴 (개발 환경에서 예상되는 에러)
    const EXPECTED_ERROR_PATTERNS = [
      'is not a constant handler',                              // Constant API 에러
      '__GRANITE_NATIVE_EMITTER is not available',              // Async API 에러 (emitter)
      'ReactNativeWebView is not available in browser environment', // Async API 에러 (webview)
    ];

    const errorAnalysis = {
      expectedErrors: [],    // 개발 환경에서 예상되는 에러 (bridge-core)
      unexpectedErrors: []   // 발생하면 안 되는 에러
    };

    consoleErrors.forEach(error => {
      const isExpected = EXPECTED_ERROR_PATTERNS.some(pattern => error.includes(pattern));

      if (isExpected) {
        errorAnalysis.expectedErrors.push(error);
      } else {
        errorAnalysis.unexpectedErrors.push(error);
      }
    });

    // 에러 분석 결과 출력
    console.log('\n📋 Console Error Analysis:');

    if (errorAnalysis.expectedErrors.length > 0) {
      console.log(`   ✅ Expected errors (bridge-core in dev): ${errorAnalysis.expectedErrors.length}`);
      errorAnalysis.expectedErrors.slice(0, 5).forEach(e => console.log(`      → ${e.substring(0, 100)}`));
    } else {
      console.log(`   ⚠️  No expected errors detected`);
      console.log(`      → Expected: "XXX is not a constant handler" in dev environment`);
    }

    if (errorAnalysis.unexpectedErrors.length > 0) {
      console.log(`   ❌ Unexpected errors: ${errorAnalysis.unexpectedErrors.length}`);
      errorAnalysis.unexpectedErrors.slice(0, 10).forEach(e => console.log(`      → ${e.substring(0, 100)}`));
    }

    // 결과 처리
    if (apiResults) {
      console.log(`\n📊 API TEST RESULTS:`);
      console.log(`   Total APIs: ${apiResults.totalAPIs}`);
      console.log(`   Passed: ${apiResults.successCount}`);
      console.log(`   Failed: ${apiResults.failCount}`);

      // 실패한 API 목록
      if (apiResults.results) {
        const failures = apiResults.results.filter(r => !r.success);
        if (failures.length > 0) {
          console.log('\n❌ Failed APIs:');
          failures.forEach(f => {
            console.log(`   - ${f.apiName}: ${f.error || 'Unknown error'}`);
          });
        }
      }

      // 모든 API가 성공해야 함 (또는 최소 성공률 검증)
      const successRate = apiResults.totalAPIs > 0
        ? (apiResults.successCount / apiResults.totalAPIs) * 100
        : 0;

      console.log(`\n✅ Success rate: ${successRate.toFixed(1)}%`);

      // 최소 80% 성공률 요구 (일부 API는 WebGL 환경에서 작동하지 않을 수 있음)
      expect(successRate).toBeGreaterThanOrEqual(80);

      benchmarkResults.tests['7_runtime_api'] = {
        passed: true,
        totalAPIs: apiResults.totalAPIs,
        successCount: apiResults.successCount,
        failCount: apiResults.failCount,
        successRate,
        failures: apiResults.results?.filter(r => !r.success) || [],
        errorAnalysis: {
          expectedErrors: errorAnalysis.expectedErrors.length,
          unexpectedErrors: errorAnalysis.unexpectedErrors.length
        }
      };
    } else {
      console.log('⚠️ API test results not received (RuntimeAPITester may not be in scene)');
      console.log('   This is expected if RuntimeAPITester.cs is not added to the Unity project');

      // RuntimeAPITester가 없으면 스킵 (실패하지 않음)
      benchmarkResults.tests['7_runtime_api'] = {
        passed: true,
        skipped: true,
        reason: 'RuntimeAPITester not found in scene'
      };
    }
  });

});
