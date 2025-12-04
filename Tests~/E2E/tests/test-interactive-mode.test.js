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

let serverProcess = null;
const serverPort = 5173;

/**
 * Production 서버 시작
 */
async function startServer(aitBuildDir, defaultPort) {
  return new Promise((resolve, reject) => {
    const server = spawn('npm', ['run', 'dev'], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      shell: true,
      env: { ...process.env, NODE_OPTIONS: '' }
    });

    let started = false;
    let actualPort = defaultPort;

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
    const devServer = await startServer(AIT_BUILD, serverPort);
    serverProcess = devServer.process;

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
    test.setTimeout(60000);

    // 콘솔 로그 캡처
    const consoleLogs = [];
    page.on('console', msg => {
      const text = msg.text();
      consoleLogs.push(text);
      console.log('[Browser Console]', text);
    });

    // 페이지 로딩 (파라미터 없음 - 대화형 모드)
    console.log(`📍 Loading page: http://localhost:${serverPort}`);
    await page.goto(`http://localhost:${serverPort}`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000
    });

    // Unity 초기화 대기
    await page.waitForFunction(() => {
      return window['unityInstance'] !== undefined;
    }, { timeout: 30000 });

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
    test.setTimeout(60000);

    const consoleLogs = [];
    page.on('console', msg => {
      const text = msg.text();
      consoleLogs.push(text);
      console.log('[Browser Console]', text);
    });

    // 페이지 로딩 (E2E 모드)
    console.log(`📍 Loading page: http://localhost:${serverPort}?e2e=true`);
    await page.goto(`http://localhost:${serverPort}?e2e=true`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000
    });

    // Unity 초기화 대기
    await page.waitForFunction(() => {
      return window['unityInstance'] !== undefined;
    }, { timeout: 30000 });

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
