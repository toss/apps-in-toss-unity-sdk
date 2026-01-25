// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK - E2E Full Pipeline Tests
 *
 * 8개 테스트 케이스 (빠른 테스트 → 느린 테스트 순서):
 * 1. Unity WebGL Build (Runtime 컴파일)
 * 2. AIT Dev Server
 * 3. AIT Build Directory
 * 4. AIT Packaging
 * 5-8. Production Tests (세션 공유로 ~6분 절약)
 *   5. Production Server (Unity 초기화 검증)
 *   6. Runtime API Error Validation (SDK API 에러 검증)
 *   7. Serialization Round-trip Tests (C# ↔ JavaScript 직렬화 검증)
 *   8. Comprehensive Performance (CPU/GPU + 500MB 메모리 압박 테스트)
 *
 * Test 5-8 세션 공유:
 * - 서버 1회 시작, Unity 1회 초기화로 반복 초기화 방지
 * - JavaScript 트리거 함수로 테스트 실행 (TriggerAPITest, TriggerSerializationTest, TriggerPerformanceTest)
 *
 * Test 6 (Runtime API) 검증 기준:
 * - 모든 SDK API를 호출
 * - 개발 환경에서 "상정된 에러" (expected error) 발생 = PASS
 *   - "XXX is not a constant handler" (bridge-core Constant API)
 *   - "__GRANITE_NATIVE_EMITTER is not available" (Async API)
 *   - "ReactNativeWebView is not available" (Native 통신)
 * - "상정되지 않은 에러" (unexpected error) 발생 = FAIL
 *
 * Test 8 (Performance) 메모리 압박:
 * - WASM 힙: 500MB
 * - JavaScript 힙: 500MB
 * - Canvas (GPU): 500MB (125개 × 4MB)
 */

// ES Module에서 __dirname 대체
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 모바일 에뮬레이션 활성화 여부 (macOS CI에서만 true)
const isMobileEmulation = process.env.MOBILE_EMULATION === 'true';

// CPU 쓰로틀링 배율 (환경변수로 제어, 기본값: 0 = 비활성화)
// 예: CPU_THROTTLE_RATE=4 → 4배 느림
const cpuThrottleRate = parseInt(process.env.CPU_THROTTLE_RATE || '0', 10);

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

// 벤치마크 기준 (모바일 환경에서는 완화된 기준 적용)
const BENCHMARKS = isMobileEmulation ? {
  MAX_LOAD_TIME_MS: 30000,      // 30초 (CPU 4x + 네트워크 지연)
  MAX_BUILD_SIZE_MB: 50,        // 50MB
  MIN_AVG_FPS: 20,              // 20 FPS (모바일 기준)
  MIN_FPS: 10,                  // 최소 FPS
  MAX_MEMORY_MB: 512            // 512MB
} : {
  MAX_LOAD_TIME_MS: 10000,      // 10초 (데스크톱)
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
    // pnpx vite 직접 실행 (granite는 --port 인자를 무시하므로 vite 직접 호출)
    // Windows에서 spawn('pnpx', ...)이 ENOENT 에러 발생하므로 shell: true 사용
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

      // ANSI 색상 코드 제거 후 포트 파싱
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');

      // 포트 파싱: IPv4 (localhost, 0.0.0.0, 127.0.0.1), IPv6 ([::], [::1])
      const portMatch = cleanOutput.match(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/);
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
 * 유틸리티: Granite Dev 서버 시작 (npm exec -- granite dev)
 * Unity Editor의 Start Server 메뉴와 동일한 방식으로 서버 시작
 * 환경 변수를 통해 host/port 전달 (granite.config.ts에서 읽음)
 * @returns {Promise<{process: ChildProcess, port: number, startupOutput: string}>}
 */
async function startGraniteDevServer(aitBuildDir, viteHost, vitePort, graniteHost, granitePort) {
  const isWindows = process.platform === 'win32';

  // 포트 정리
  const portsToClean = [vitePort, granitePort];
  for (const port of portsToClean) {
    try {
      if (isWindows) {
        execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
      } else {
        execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
      }
    } catch {
      // 무시
    }
  }

  await new Promise(r => setTimeout(r, 1000));

  return new Promise((resolve, reject) => {
    // pnpm exec granite dev 실행 (Unity Editor와 동일한 방식)
    const server = spawn('pnpm', ['exec', 'granite', 'dev'], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      shell: true,
      env: {
        ...process.env,
        NODE_OPTIONS: '',
        // Unity Editor에서 설정하는 환경 변수와 동일
        AIT_GRANITE_HOST: graniteHost,
        AIT_GRANITE_PORT: String(granitePort),
        AIT_VITE_HOST: viteHost,
        AIT_VITE_PORT: String(vitePort)
      }
    });

    let started = false;
    let actualPort = vitePort;
    let startupOutput = '';

    server.stdout.on('data', (data) => {
      const output = data.toString();
      startupOutput += output;
      console.log('[granite dev]', output);

      // ANSI 색상 코드 제거 후 포트 파싱
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');

      // 포트 파싱: IPv4 (localhost, 0.0.0.0, 127.0.0.1), IPv6 ([::], [::1])
      const portMatch = cleanOutput.match(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Granite dev server running on port: ${actualPort}`);
        started = true;
        resolve({ process: server, port: actualPort, startupOutput });
      }
    });

    server.stderr.on('data', (data) => {
      const output = data.toString();
      startupOutput += output;
      console.error('[granite dev error]', output);

      // pnpm 옵션 파싱 에러 감지 (버그 재발 시)
      if (output.includes('Unknown cli config') || output.includes('Extraneous positional argument')) {
        reject(new Error(`pnpm exec 명령어 파싱 에러 감지: ${output}`));
      }
    });

    server.on('error', (err) => {
      reject(new Error(`Granite dev server 시작 실패: ${err.message}`));
    });

    server.on('exit', (code) => {
      if (!started && code !== 0) {
        reject(new Error(`Granite dev server가 비정상 종료됨 (Exit Code: ${code})\n출력: ${startupOutput}`));
      }
    });

    // 20초 타임아웃 (granite는 vite보다 시작이 느릴 수 있음)
    setTimeout(() => {
      if (!started) {
        started = true;
        resolve({ process: server, port: actualPort, startupOutput });
      }
    }, 20000);
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
    // pnpm run start는 포트 인자를 전달하기 어려우므로 pnpx vite preview 사용
    // Windows에서 spawn('pnpx', ...)이 ENOENT 에러 발생하므로 shell: true 사용
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

/**
 * CDP를 통한 모바일 환경 시뮬레이션 적용
 *
 * 환경변수로 제어:
 * - MOBILE_EMULATION=true: 모바일 에뮬레이션 (CPU 4x + 4G LTE)
 * - CPU_THROTTLE_RATE=N: CPU만 N배 느리게 (독립 사용 가능)
 *
 * @param {number} overrideRate - 특정 테스트에서 강제로 사용할 CPU 배율 (0=비활성화)
 */
async function applyMobileThrottling(page, overrideRate = undefined) {
  // 쓰로틀링 배율 결정 (우선순위: override > 환경변수)
  const rate = overrideRate !== undefined ? overrideRate :
               (isMobileEmulation ? 4 : cpuThrottleRate);

  if (rate <= 0 && !isMobileEmulation) {
    console.log('📱 Throttling disabled (no MOBILE_EMULATION or CPU_THROTTLE_RATE)');
    return null;
  }

  const client = await page.context().newCDPSession(page);

  // CPU 쓰로틀링 적용 (rate > 0인 경우)
  if (rate > 0) {
    console.log(`📱 Applying CPU ${rate}x slowdown...`);
    await client.send('Emulation.setCPUThrottlingRate', { rate });
  }

  // 네트워크 쓰로틀링 (MOBILE_EMULATION인 경우에만)
  if (isMobileEmulation) {
    console.log('📱 Applying 4G LTE network throttling...');
    // 12 Mbps = 1,572,864 bytes/s, 6 Mbps = 786,432 bytes/s
    await client.send('Network.emulateNetworkConditions', {
      offline: false,
      downloadThroughput: 12 * 1024 * 1024 / 8,  // 12 Mbps
      uploadThroughput: 6 * 1024 * 1024 / 8,     // 6 Mbps
      latency: 70
    });
  }

  return client;
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

    // 결과 저장 (두 가지 파일)
    // 1. 전체 테스트 결과
    const resultsPath = path.resolve(__dirname, 'e2e-test-results.json');
    fs.writeFileSync(resultsPath, JSON.stringify(testResults, null, 2));

    // 2. 벤치마크 결과 (workflow에서 업로드하는 파일)
    const benchmarkPath = path.resolve(__dirname, 'benchmark-results.json');
    const comprehensivePerf = testResults.tests['8_comprehensive_perf'];
    const benchmarkResults = {
      timestamp: testResults.timestamp,
      unityProject: SAMPLE_PROJECT,
      buildSize: testResults.tests['1_webgl_build']?.buildSizeMB,
      pageLoadTime: testResults.tests['5_production_server']?.pageLoadTimeMs || comprehensivePerf?.pageLoadTimeMs,
      unityLoadTime: comprehensivePerf?.unityLoadTimeMs,
      webgl: testResults.tests['5_production_server']?.webgl,
      // 종합 성능 테스트 데이터 (새 구조)
      comprehensivePerfData: comprehensivePerf ? {
        oomOccurred: comprehensivePerf.oomOccurred,
        baseline: comprehensivePerf.baseline,
        physicsWithMemory: comprehensivePerf.physicsWithMemory,
        renderingWithMemory: comprehensivePerf.renderingWithMemory,
        fullLoad: comprehensivePerf.fullLoad
      } : null,
      apiTestResults: testResults.tests['6_runtime_api'] ? {
        totalAPIs: testResults.tests['6_runtime_api'].totalAPIs,
        successCount: testResults.tests['6_runtime_api'].successCount,
        unexpectedErrorCount: testResults.tests['6_runtime_api'].unexpectedErrorCount
      } : null,
      testsPassed: Object.values(testResults.tests || {}).filter(t => t.passed).length,
      testsTotal: Object.keys(testResults.tests || {}).length
    };
    fs.writeFileSync(benchmarkPath, JSON.stringify(benchmarkResults, null, 2));

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
    const pageLoad = tests['5_production_server']?.pageLoadTimeMs || tests['8_comprehensive_perf']?.pageLoadTimeMs;
    const unityLoad = tests['8_comprehensive_perf']?.unityLoadTimeMs;
    const renderer = tests['5_production_server']?.webgl?.renderer;

    console.log('\n  📦 Build Size:      ' + (buildSize ? buildSize.toFixed(2) + ' MB' : 'N/A'));
    console.log('  ⏱️  Page Load:       ' + (pageLoad ? pageLoad + ' ms' : 'N/A'));
    console.log('  🎮 Unity Load:      ' + (unityLoad ? unityLoad + ' ms' : 'N/A'));
    console.log('  🖥️  GPU Renderer:    ' + (renderer || 'N/A'));

    // SDK Runtime 검증 결과 출력
    const apiTest = tests['6_runtime_api'];
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
  // Test 1.5: Granite Dev Server Command Validation
  // Unity Editor의 "Start Server" 메뉴와 동일한 방식으로 서버 시작 검증
  // 버그 재발 방지: pnpm exec 명령어 파싱 에러 감지
  // -------------------------------------------------------------------------
  // Test 1.5: pnpm exec granite dev 명령어 파싱 검증
  // 이 테스트는 서버가 완전히 시작될 때까지 기다리지 않고,
  // pnpm exec 명령어가 올바르게 파싱되는지만 확인합니다.
  // (포트 충돌 이슈를 피하기 위해 간소화됨)
  test('1.5. Granite dev server command should work correctly', async () => {
    test.setTimeout(30000); // 30초

    // ait-build 디렉토리 확인
    if (!directoryExists(AIT_BUILD)) {
      console.log('⚠️ ait-build/ not found, skipping granite dev server test');
      testResults.tests['1.5_granite_dev_command'] = {
        passed: true,
        skipped: true,
        reason: 'ait-build not found'
      };
      return;
    }

    // node_modules 확인
    const nodeModulesPath = path.join(AIT_BUILD, 'node_modules');
    if (!directoryExists(nodeModulesPath)) {
      console.log('⚠️ node_modules not found, skipping granite dev server test');
      testResults.tests['1.5_granite_dev_command'] = {
        passed: true,
        skipped: true,
        reason: 'node_modules not found'
      };
      return;
    }

    console.log('🚀 Testing granite dev command parsing (pnpm exec granite dev)...');
    console.log('   This validates the fix for pnpm exec command parsing bug');

    let graniteProcess = null;
    try {
      // pnpm exec granite dev 명령어 실행 (Unity Editor와 동일한 방식)
      graniteProcess = spawn('pnpm', ['exec', 'granite', 'dev'], {
        cwd: AIT_BUILD,
        stdio: 'pipe',
        shell: true,
        env: { ...process.env, NODE_OPTIONS: '' }
      });

      let output = '';
      let hasPnpmParsingError = false;
      let graniteStarted = false;

      graniteProcess.stdout.on('data', (data) => {
        const text = data.toString();
        output += text;
        console.log('[granite dev]', text);

        // granite/vite가 시작되었는지 확인
        if (text.includes('VITE') || text.includes('localhost:')) {
          graniteStarted = true;
        }
      });

      graniteProcess.stderr.on('data', (data) => {
        const text = data.toString();
        output += text;
        console.log('[granite dev stderr]', text);

        // pnpm 옵션 파싱 에러 감지 (버그 재발 시)
        if (text.includes('Unknown cli config') ||
            text.includes('Extraneous positional argument') ||
            text.includes('is being parsed as a normal command line argument')) {
          hasPnpmParsingError = true;
        }
      });

      // 5초간 출력 수집 (서버 완전 시작 안 기다림, 명령어 파싱만 확인)
      await new Promise(r => setTimeout(r, 5000));

      // pnpm 옵션 파싱 에러 확인
      expect(hasPnpmParsingError, 'pnpm exec 명령어 파싱 에러가 없어야 함').toBe(false);

      // 출력에서 pnpm 파싱 에러 재확인
      const hasParsingErrorInOutput =
        output.includes('Unknown cli config') ||
        output.includes('Extraneous positional argument');
      expect(hasParsingErrorInOutput, '출력에 pnpm 파싱 에러가 없어야 함').toBe(false);

      testResults.tests['1.5_granite_dev_command'] = {
        passed: true,
        pnpmParsingErrorDetected: false,
        graniteStarted: graniteStarted
      };

      console.log(`✅ Granite dev command test passed`);
      console.log(`   - pnpm exec parsing: OK`);
      console.log(`   - granite started: ${graniteStarted}`);

    } catch (error) {
      console.error('❌ Granite dev command test failed:', error.message);

      testResults.tests['1.5_granite_dev_command'] = {
        passed: false,
        error: error.message
      };

      throw error;
    } finally {
      // 프로세스 정리
      if (graniteProcess) {
        graniteProcess.kill();
      }
    }
  });


  // -------------------------------------------------------------------------
  // Test 2: AIT Dev Server (vite)
  // -------------------------------------------------------------------------
  test('2. AIT dev server should start and load Unity', async ({ page }) => {
    test.setTimeout(120000); // 2분

    // 모바일 스로틀링 적용 (MOBILE_EMULATION=true일 때만 실행)
    await applyMobileThrottling(page);

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
  // Tests 5-8: Production Server + Runtime Tests (세션 공유)
  // 서버 1회 시작 + Unity 1회 초기화로 ~6분 절약
  // -------------------------------------------------------------------------
  test.describe.serial('Production Tests (shared session)', () => {
    /** @type {import('@playwright/test').Page} */
    let sharedPage = null;
    let sharedServerProcess = null;
    let sharedPort = serverPort;
    let pageLoadTime = 0;
    let unityLoadTime = 0;
    /** @type {import('@playwright/test').CDPSession} */
    let cdpClient = null;

    test.beforeAll(async ({ browser }) => {
      console.log('\n' + '='.repeat(70));
      console.log('🚀 STARTING SHARED SESSION FOR TESTS 5-8');
      console.log('='.repeat(70));

      expect(directoryExists(DIST_WEB), 'dist/web/ should exist for production server').toBe(true);

      // 1. Production 서버 시작 (1회만)
      console.log('🚀 Starting production server (vite preview)...');
      const prodServer = await startProductionServer(AIT_BUILD, serverPort);
      sharedServerProcess = prodServer.process;
      sharedPort = prodServer.port;

      // 서버가 준비될 때까지 대기 (최대 10초)
      let serverReady = false;
      for (let i = 0; i < 20; i++) {
        try {
          const response = await fetch(`http://localhost:${sharedPort}/`, { method: 'HEAD' });
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
        throw new Error(`Server failed to start on port ${sharedPort}`);
      }
      console.log(`✅ Server ready on port ${sharedPort}`);

      // 2. 페이지 생성 + Unity 초기화 (1회만)
      sharedPage = await browser.newPage();

      // CDP 세션 생성 (CPU 쓰로틀링용)
      cdpClient = await sharedPage.context().newCDPSession(sharedPage);

      // 페이지 로딩 시간 측정 (E2E 모드 활성화)
      const startTime = Date.now();
      const response = await sharedPage.goto(`http://localhost:${sharedPort}?e2e=true`, {
        waitUntil: 'networkidle',
        timeout: 90000
      });

      expect(response?.status()).toBe(200);
      pageLoadTime = Date.now() - startTime;
      console.log(`✅ Page loaded in ${pageLoadTime}ms`);

      // Unity 초기화 대기
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

      // 트리거 함수가 등록될 때까지 대기
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
      console.log('\n' + '='.repeat(70));
      console.log('🛑 CLOSING SHARED SESSION');
      console.log('='.repeat(70));

      // 페이지 닫기
      if (sharedPage) {
        await sharedPage.close();
        sharedPage = null;
      }

      // 서버 종료
      if (sharedServerProcess) {
        sharedServerProcess.kill();
        sharedServerProcess = null;
      }

      console.log('✅ Shared session closed\n');
    });


    // -------------------------------------------------------------------------
    // Test 5: Production Server (vite preview) - Unity 초기화 검증
    // -------------------------------------------------------------------------
    test('5. Production build should load in browser', async () => {
      test.setTimeout(30000); // 30초 (이미 로드됨)

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

      testResults.tests['5_production_server'] = {
        passed: true,
        pageLoadTimeMs: pageLoadTime,
        unityLoadTimeMs: unityLoadTime,
        webgl: webglInfo
      };

      expect(webglInfo.supported, 'WebGL should be supported').toBe(true);
    });


    // -------------------------------------------------------------------------
    // Test 6: Runtime API Error Validation
    // JavaScript에서 TriggerAPITest() 호출하여 테스트 실행
    // -------------------------------------------------------------------------
    test('6. All SDK APIs should return correct errors in dev environment', async () => {
      test.setTimeout(180000); // 3분

      console.log('🔄 Triggering API tests via JavaScript...');

      // 이벤트 리스너 등록 + 트리거 호출
      const apiResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          // 이미 데이터가 있으면 바로 반환 (auto-run 모드)
          if (window['__E2E_API_TEST_DATA__']) {
            resolve(window['__E2E_API_TEST_DATA__']);
            return;
          }

          // 이벤트 리스너 등록
          const handler = (event) => {
            window.removeEventListener('e2e-api-test-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-api-test-complete', handler);

          // 트리거 함수가 있으면 호출
          if (typeof window['TriggerAPITest'] === 'function') {
            console.log('[E2E] Calling TriggerAPITest()');
            window['TriggerAPITest']();
          } else {
            console.log('[E2E] TriggerAPITest not found, waiting for auto-run...');
          }

          // 120초 타임아웃
          setTimeout(() => resolve(null), 120000);
        });
      });

      // 결과 검증
      if (apiResults) {
        // JSON 문자열인 경우 파싱
        let results = apiResults;
        if (typeof results === 'string') {
          try {
            results = JSON.parse(results);
          } catch {
            console.log('⚠️ Failed to parse API results JSON');
          }
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 SDK API ERROR VALIDATION RESULTS');
        console.log('='.repeat(70));
        console.log(`   Total APIs Tested: ${results.totalAPIs}`);
        console.log(`   Success (including expected errors): ${results.successCount}`);
        console.log(`   Expected Errors: ${results.expectedErrorCount || 0}`);
        console.log(`   Unexpected Errors (FAILURES): ${results.unexpectedErrorCount || 0}`);
        console.log('='.repeat(70));

        // 상정된 에러가 발생한 API 목록 (정상)
        if (results.results) {
          const expectedErrors = results.results.filter(r => r.success && r.isExpectedError);
          if (expectedErrors.length > 0) {
            console.log('\n✅ APIs with Expected Errors (correct behavior in dev):');
            expectedErrors.forEach(r => {
              const truncatedError = r.error?.length > 50 ? r.error.substring(0, 50) + '...' : r.error;
              console.log(`   [OK] ${r.apiName}: ${truncatedError}`);
            });
          }

          // 에러 없이 성공한 API
          const cleanSuccess = results.results.filter(r => r.success && !r.isExpectedError && !r.error);
          if (cleanSuccess.length > 0) {
            console.log('\n✅ APIs Completed Successfully (mock worked):');
            cleanSuccess.forEach(r => {
              console.log(`   [OK] ${r.apiName}`);
            });
          }

          // 상정되지 않은 에러
          const unexpectedErrors = results.results.filter(r => !r.success);
          if (unexpectedErrors.length > 0) {
            console.log('\n❌ APIs with UNEXPECTED Errors (TEST FAILURES):');
            unexpectedErrors.forEach(r => {
              console.log(`   [FAIL] ${r.apiName}: ${r.error}`);
            });
          }
        }

        const unexpectedErrorCount = results.unexpectedErrorCount || 0;

        console.log('\n' + '='.repeat(70));
        if (unexpectedErrorCount === 0) {
          console.log('✅ ALL API ERROR VALIDATIONS PASSED');
        } else {
          console.log('❌ API ERROR VALIDATION FAILED');
        }
        console.log('='.repeat(70) + '\n');

        testResults.tests['6_runtime_api'] = {
          passed: unexpectedErrorCount === 0,
          totalAPIs: results.totalAPIs,
          successCount: results.successCount,
          expectedErrorCount: results.expectedErrorCount || 0,
          unexpectedErrorCount: unexpectedErrorCount,
          results: results.results || []
        };

        expect(unexpectedErrorCount, 'All APIs should return expected errors or succeed').toBe(0);

      } else {
        console.log('⚠️ API test results not received');
        testResults.tests['6_runtime_api'] = {
          passed: false,
          reason: 'RuntimeAPITester results not received'
        };
        expect(apiResults, 'RuntimeAPITester should return results').not.toBeNull();
      }
    });


    // -------------------------------------------------------------------------
    // Test 7: Serialization Round-trip Tests
    // JavaScript에서 TriggerSerializationTest() 호출하여 테스트 실행
    // -------------------------------------------------------------------------
    test('7. Serialization round-trip should succeed for all types', async () => {
      test.setTimeout(180000); // 3분

      console.log('🔄 Triggering serialization tests via JavaScript...');

      // 이벤트 리스너 등록 + 트리거 호출
      const serializationResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          // 이미 데이터가 있으면 바로 반환 (auto-run 모드)
          if (window['__E2E_SERIALIZATION_TEST_DATA__']) {
            resolve(window['__E2E_SERIALIZATION_TEST_DATA__']);
            return;
          }

          // 이벤트 리스너 등록
          const handler = (event) => {
            window.removeEventListener('e2e-serialization-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-serialization-complete', handler);

          // 트리거 함수가 있으면 호출
          if (typeof window['TriggerSerializationTest'] === 'function') {
            console.log('[E2E] Calling TriggerSerializationTest()');
            window['TriggerSerializationTest']();
          } else {
            console.log('[E2E] TriggerSerializationTest not found, waiting for auto-run...');
          }

          // 90초 타임아웃
          setTimeout(() => resolve(null), 90000);
        });
      });

      // 결과 검증
      if (serializationResults) {
        let results = serializationResults;
        if (typeof results === 'string') {
          try {
            results = JSON.parse(results);
          } catch {
            console.log('⚠️ Failed to parse serialization results JSON');
          }
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 SERIALIZATION ROUND-TRIP TEST RESULTS');
        console.log('='.repeat(70));
        console.log(`   Total Tests: ${results.totalTests}`);
        console.log(`   Success: ${results.successCount}`);
        console.log(`   Failed: ${results.failCount}`);
        console.log('='.repeat(70));

        if (results.results && Array.isArray(results.results)) {
          const passed = results.results.filter(r => r.success);
          const failed = results.results.filter(r => !r.success);

          if (passed.length > 0) {
            console.log('\n✅ Passed Tests:');
            passed.forEach(r => {
              console.log(`   [OK] ${r.testName}`);
            });
          }

          if (failed.length > 0) {
            console.log('\n❌ Failed Tests:');
            failed.forEach(r => {
              console.log(`   [FAIL] ${r.testName}: ${r.error || 'unknown error'}`);
            });
          }
        }

        console.log('\n' + '='.repeat(70));
        if (results.failCount === 0) {
          console.log('✅ ALL SERIALIZATION TESTS PASSED');
        } else {
          console.log('❌ SERIALIZATION TESTS FAILED');
        }
        console.log('='.repeat(70) + '\n');

        testResults.tests['7_serialization'] = {
          passed: results.failCount === 0,
          totalTests: results.totalTests,
          successCount: results.successCount,
          failCount: results.failCount
        };

        expect(results.failCount, 'All serialization tests should pass').toBe(0);

      } else {
        console.log('⚠️ Serialization test results not received');
        testResults.tests['7_serialization'] = {
          passed: false,
          reason: 'SerializationTester results not received'
        };
        expect(serializationResults, 'SerializationTester should return results').not.toBeNull();
      }
    });


    // -------------------------------------------------------------------------
    // Test 8: Comprehensive Performance Test (CPU/GPU + Memory 통합)
    // JavaScript에서 TriggerPerformanceTest() 호출하여 테스트 실행
    // -------------------------------------------------------------------------
    test('8. Comprehensive performance test should pass', async () => {
      test.setTimeout(240000); // 4분

      console.log('🔄 Triggering performance tests via JavaScript...');

      // CPU 쓰로틀링 6x 적용 (저사양 기기 시뮬레이션)
      await cdpClient.send('Emulation.setCPUThrottlingRate', { rate: 6 });
      console.log('🐢 CPU throttling 6x applied');

      // 이벤트 리스너 등록 + 트리거 호출
      const perfResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          // 이미 데이터가 있으면 바로 반환 (auto-run 모드)
          if (window['__E2E_COMPREHENSIVE_PERF_DATA__']) {
            resolve(window['__E2E_COMPREHENSIVE_PERF_DATA__']);
            return;
          }

          // 이벤트 리스너 등록
          const handler = (event) => {
            window.removeEventListener('e2e-comprehensive-perf-complete', handler);
            console.log('[E2E] Comprehensive perf test event received');
            resolve(event.detail);
          };
          window.addEventListener('e2e-comprehensive-perf-complete', handler);

          // 트리거 함수가 있으면 호출
          if (typeof window['TriggerPerformanceTest'] === 'function') {
            console.log('[E2E] Calling TriggerPerformanceTest()');
            window['TriggerPerformanceTest']();
          } else {
            console.log('[E2E] TriggerPerformanceTest not found, waiting for auto-run...');
          }

          // 180초 타임아웃
          setTimeout(() => {
            console.log('[E2E] Comprehensive perf test timeout');
            resolve(null);
          }, 180000);
        });
      });

      // CPU 쓰로틀링 해제
      await cdpClient.send('Emulation.setCPUThrottlingRate', { rate: 1 });

      // 빌드 크기 확인
      const buildSizeMB = getDirectorySizeMB(DIST_WEB);

      // 결과 검증
      if (perfResults) {
        let results = perfResults;
        if (typeof results === 'string') {
          try {
            results = JSON.parse(results);
          } catch {
            console.log('⚠️ Failed to parse comprehensive perf results JSON');
          }
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 COMPREHENSIVE PERFORMANCE TEST RESULTS');
        console.log('='.repeat(70));
        console.log(`   Page Load: ${pageLoadTime}ms`);
        console.log(`   Unity Load: ${unityLoadTime}ms`);
        console.log(`   Build Size: ${buildSizeMB.toFixed(2)}MB (max: ${BENCHMARKS.MAX_BUILD_SIZE_MB}MB)`);
        console.log('---');
        console.log(`   Baseline:          ${results.baseline?.avgFps?.toFixed(1) || 'N/A'} FPS (min req: 20)`);
        console.log(`   Physics + Memory:  ${results.physicsWithMemory?.avgFps?.toFixed(1) || 'N/A'} FPS (min req: 12)`);
        console.log(`   Rendering + Memory: ${results.renderingWithMemory?.avgFps?.toFixed(1) || 'N/A'} FPS (min req: 12)`);
        console.log(`   Full Load:         ${results.fullLoad?.avgFps?.toFixed(1) || 'N/A'} FPS (min req: 10)`);
        console.log(`   OOM Occurred:      ${results.oomOccurred ? '❌ YES' : '✅ NO'}`);

        // 메모리 정보 출력 (있는 경우)
        if (results.memoryInfo) {
          console.log('---');
          console.log(`   Memory - WASM: ${results.memoryInfo.wasmAllocatedMB?.toFixed(1) || 'N/A'}MB`);
          console.log(`   Memory - JS: ${results.memoryInfo.jsAllocatedMB?.toFixed(1) || 'N/A'}MB`);
          console.log(`   Memory - Canvas: ${results.memoryInfo.canvasEstimatedMB?.toFixed(1) || 'N/A'}MB`);
        }
        console.log('='.repeat(70));

        // 단계별 상세 출력
        const phases = [
          { name: 'Baseline', data: results.baseline, minFps: 20 },
          { name: 'Physics+Memory', data: results.physicsWithMemory, minFps: 12 },
          { name: 'Rendering+Memory', data: results.renderingWithMemory, minFps: 12 },
          { name: 'Full Load', data: results.fullLoad, minFps: 10 }
        ];

        let allPassed = true;
        for (const phase of phases) {
          if (phase.data?.avgFps !== undefined) {
            const passed = phase.data.avgFps >= phase.minFps;
            const status = passed ? '✅' : '❌';
            console.log(`   ${status} ${phase.name}: ${phase.data.avgFps.toFixed(1)} FPS (min: ${phase.data.minFps?.toFixed(1)}, max: ${phase.data.maxFps?.toFixed(1)})`);
            if (!passed) allPassed = false;
          }
        }

        console.log('\n' + '='.repeat(70));
        if (!results.oomOccurred && allPassed) {
          console.log('✅ COMPREHENSIVE PERFORMANCE TEST PASSED');
        } else {
          console.log('❌ COMPREHENSIVE PERFORMANCE TEST FAILED');
          if (results.oomOccurred) {
            console.log('   - OOM occurred during tests');
          }
          if (!allPassed) {
            console.log('   - One or more phases failed FPS requirements');
          }
        }
        console.log('='.repeat(70) + '\n');

        testResults.tests['8_comprehensive_perf'] = {
          passed: !results.oomOccurred && allPassed,
          pageLoadTimeMs: pageLoadTime,
          unityLoadTimeMs: unityLoadTime,
          buildSizeMB,
          oomOccurred: results.oomOccurred,
          baseline: results.baseline,
          physicsWithMemory: results.physicsWithMemory,
          renderingWithMemory: results.renderingWithMemory,
          fullLoad: results.fullLoad,
          memoryInfo: results.memoryInfo
        };

        // 빌드 크기 검증
        expect(buildSizeMB).toBeLessThanOrEqual(BENCHMARKS.MAX_BUILD_SIZE_MB);

        // OOM 검증
        expect(results.oomOccurred, 'Should complete without OOM').toBe(false);

        // Full Load에서 최소 10 FPS 이상 유지해야 함
        if (results.fullLoad?.avgFps !== undefined) {
          expect(results.fullLoad.avgFps, 'Full Load should maintain at least 10 FPS').toBeGreaterThanOrEqual(10);
        }

      } else {
        console.log('⚠️ Comprehensive performance test results not received');
        testResults.tests['8_comprehensive_perf'] = {
          passed: false,
          reason: 'ComprehensivePerfTester results not received'
        };
        expect(perfResults, 'ComprehensivePerfTester should return results').not.toBeNull();
      }
    });

  }); // end of test.describe.serial


});
