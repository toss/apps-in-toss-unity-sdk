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

// Unity 버전별 포트 오프셋 계산
function getPortOffsetFromUnityVersion(projectPath) {
  if (projectPath.includes('2021.3')) return 0;
  if (projectPath.includes('2022.3')) return 1;
  if (projectPath.includes('6000.0')) return 2;
  if (projectPath.includes('6000.2')) return 3;
  return 0;
}

const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
const VITE_DEV_PORT = 5173 + PORT_OFFSET;  // vite dev 서버 포트

let serverProcess = null;
let actualServerPort = VITE_DEV_PORT;

/**
 * Dev 서버 시작 (npx vite --host --port)
 */
async function startServer(aitBuildDir, vitePort) {
  console.log(`🔌 Using vite port: ${vitePort} (offset: ${PORT_OFFSET})`);

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
    let actualPort = vitePort;

    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[vite dev]', output);

      // ANSI 색상 코드 제거 후 포트 파싱
      // localhost:PORT, 0.0.0.0:PORT, 127.0.0.1:PORT 모두 매칭
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');
      const portMatch = cleanOutput.match(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1):(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Server running on port: ${actualPort}`);
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

test.describe('Interactive API Tester', () => {
  test.beforeAll(async () => {
    console.log('🚀 Starting dev server for interactive mode test...');
    const devServer = await startServer(AIT_BUILD, VITE_DEV_PORT);
    serverProcess = devServer.process;
    actualServerPort = devServer.port;

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
