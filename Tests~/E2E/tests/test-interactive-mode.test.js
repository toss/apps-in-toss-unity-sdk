// @ts-check
import { test, expect } from '@playwright/test';
import { spawn } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// UNITY_PROJECT_PATH 환경변수 사용, 없으면 기본값
function findSampleProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath) {
    return envPath;
  }
  // 기본값: 2021.3
  return path.resolve(__dirname, '../SampleUnityProject-2021.3');
}

const SAMPLE_PROJECT = findSampleProject();
const AIT_BUILD = path.resolve(SAMPLE_PROJECT, 'ait-build');

// Unity 버전별 포트 오프셋 (e2e-full-pipeline.test.js와 동일한 로직)
// 2021.3 → 0, 2022.3 → 1, 6000.0 → 2, 6000.2 → 3
function getPortOffsetFromUnityVersion(projectPath) {
  const match = projectPath.match(/(\d{4})\.(\d+)/);
  if (!match) return 0;

  const major = parseInt(match[1], 10);
  const minor = parseInt(match[2], 10);

  if (major === 2021) return 0;
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  return 0;
}

const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
// e2e-full-pipeline.test.js는 4173+offset, 여기서는 5173+offset 사용
// 두 테스트 파일이 다른 포트 범위를 사용하므로 충돌 없음
const DEFAULT_PORT = 5173 + PORT_OFFSET;
console.log(`📦 Unity project: ${SAMPLE_PROJECT}`);
console.log(`🔌 Interactive test port: ${DEFAULT_PORT} (offset: ${PORT_OFFSET})`);

let serverProcess = null;
let actualServerPort = DEFAULT_PORT;

/**
 * Dev 서버 시작
 * 포트 충돌은 GitHub Actions의 job-level concurrency로 방지됨
 */
async function startServer(aitBuildDir, port) {
  console.log(`🔌 Starting server on port: ${port}`);

  return new Promise((resolve, reject) => {
    // Windows에서 spawn('npm', ...)이 ENOENT 에러 발생하므로 shell: true 사용
    // 포트를 명시적으로 지정하여 granite dev에 전달
    const server = spawn('npm', ['run', 'dev', '--', '--port', String(port)], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      shell: true,
      env: { ...process.env, NODE_OPTIONS: '' }
    });

    let started = false;
    let actualPort = port;

    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[dev server]', output);

      const portMatch = output.match(/localhost:(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Server running on port: ${actualPort}`);
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });

    server.stderr.on('data', (data) => {
      console.error('[dev server error]', data.toString());
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

test.describe('Interactive API Tester', () => {
  test.beforeAll(async () => {
    console.log('🚀 Starting dev server for interactive mode test...');
    console.log(`📁 Sample Project: ${SAMPLE_PROJECT}`);
    console.log(`📁 AIT Build: ${AIT_BUILD}`);
    console.log(`🔌 Default port: ${DEFAULT_PORT} (offset: ${PORT_OFFSET})`);

    const devServer = await startServer(AIT_BUILD, DEFAULT_PORT);
    serverProcess = devServer.process;
    actualServerPort = devServer.port;

    console.log(`✅ Server started on port: ${actualServerPort}`);

    // 서버 준비 대기
    await new Promise(r => setTimeout(r, 3000));
  });

  test.afterAll(async () => {
    if (serverProcess) {
      serverProcess.kill();
      serverProcess = null;
    }
  });

  test('Interactive mode (without ?e2e=true) should load InteractiveAPITester', async ({ page }) => {
    test.setTimeout(180000);  // 3분 (Unity 6000.x는 초기화가 더 오래 걸릴 수 있음)

    // 콘솔 로그 캡처
    const consoleLogs = [];
    page.on('console', msg => {
      const text = msg.text();
      consoleLogs.push(text);
      console.log('[Browser Console]', text);
    });

    // 페이지 로딩 (파라미터 없음 - 대화형 모드)
    console.log(`📍 Loading page: http://localhost:${actualServerPort}`);
    await page.goto(`http://localhost:${actualServerPort}`, {
      waitUntil: 'domcontentloaded',
      timeout: 60000
    });

    // Unity 초기화 대기 (Unity 6000.x는 더 오래 걸릴 수 있음)
    await page.waitForFunction(() => {
      return window['unityInstance'] !== undefined;
    }, { timeout: 120000 });

    console.log('✅ Unity instance initialized');

    // E2EBootstrapper 로그 확인
    await new Promise(r => setTimeout(r, 2000));

    // 콘솔 로그에서 모드 확인
    const modeLog = consoleLogs.find(log => log.includes('[E2EBootstrapper] Mode:'));
    console.log('🔍 Mode log:', modeLog);

    // InteractiveAPITester 초기화 로그 확인
    const interactiveLogs = consoleLogs.filter(log =>
      log.includes('InteractiveAPITester') ||
      log.includes('Interactive Test App')
    );
    console.log('🔍 Interactive logs:', interactiveLogs);

    // 스크린샷 촬영
    await page.screenshot({ path: 'interactive-mode-screenshot.png', fullPage: true });
    console.log('📸 Screenshot saved: interactive-mode-screenshot.png');

    // Unity 로그 출력
    console.log('\n📋 All Console Logs:');
    consoleLogs.forEach(log => console.log('  ', log));
  });

  test('E2E mode (with ?e2e=true) should load AutoBenchmarkRunner', async ({ page }) => {
    test.setTimeout(180000);  // 3분 (Unity 6000.x는 초기화가 더 오래 걸릴 수 있음)

    const consoleLogs = [];
    page.on('console', msg => {
      const text = msg.text();
      consoleLogs.push(text);
      console.log('[Browser Console]', text);
    });

    // 페이지 로딩 (E2E 모드)
    console.log(`📍 Loading page: http://localhost:${actualServerPort}?e2e=true`);
    await page.goto(`http://localhost:${actualServerPort}?e2e=true`, {
      waitUntil: 'domcontentloaded',
      timeout: 60000
    });

    // Unity 초기화 대기 (Unity 6000.x는 더 오래 걸릴 수 있음)
    await page.waitForFunction(() => {
      return window['unityInstance'] !== undefined;
    }, { timeout: 120000 });

    console.log('✅ Unity instance initialized');

    // E2EBootstrapper 로그 확인
    await new Promise(r => setTimeout(r, 2000));

    const modeLog = consoleLogs.find(log => log.includes('[E2EBootstrapper] Mode:'));
    console.log('🔍 Mode log:', modeLog);

    const e2eLogs = consoleLogs.filter(log =>
      log.includes('AutoBenchmarkRunner') ||
      log.includes('E2E Test')
    );
    console.log('🔍 E2E logs:', e2eLogs);

    // 스크린샷 촬영
    await page.screenshot({ path: 'e2e-mode-screenshot.png', fullPage: true });
    console.log('📸 Screenshot saved: e2e-mode-screenshot.png');

    // Unity 로그 출력
    console.log('\n📋 All Console Logs:');
    consoleLogs.forEach(log => console.log('  ', log));
  });
});
