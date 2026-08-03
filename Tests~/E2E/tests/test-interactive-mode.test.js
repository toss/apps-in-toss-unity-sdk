// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as net from 'net';
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
  if (projectPath.includes('6000.3')) return 4;
  return 0;
}

const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
const VITE_DEV_PORT = 5173 + PORT_OFFSET;  // vite dev 서버 포트

let serverProcess = null;
let actualServerPort = VITE_DEV_PORT;

/**
 * 유틸리티: 포트가 사용 가능한지 확인
 */
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

/**
 * 유틸리티: 포트가 해제될 때까지 대기
 */
async function waitForPortRelease(port, timeoutMs = 10000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPortAvailable(port)) return true;
    await new Promise(r => setTimeout(r, 200));
  }
  return false;
}

/**
 * 유틸리티: 프로세스 트리를 안전하게 종료 후 포트 해제 대기
 */
async function killServerProcess(proc, ports = []) {
  if (!proc) return;
  try { proc.kill('SIGTERM'); } catch {}
  const exited = await new Promise((resolve) => {
    if (proc.exitCode !== null) { resolve(true); return; }
    const timer = setTimeout(() => resolve(false), 3000);
    proc.once('exit', () => { clearTimeout(timer); resolve(true); });
  });
  if (!exited) {
    try { proc.kill('SIGKILL'); } catch {}
    await new Promise(r => setTimeout(r, 1000));
  }
  const isWindows = process.platform === 'win32';
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

/**
 * Dev 서버 시작 (pnpx vite)
 */
async function startServer(aitBuildDir, vitePort) {
  console.log(`🔌 Using vite port: ${vitePort} (offset: ${PORT_OFFSET})`);

  // 포트 정리 (이전 테스트에서 잔여 프로세스가 있을 수 있음)
  const isWindows = process.platform === 'win32';
  try {
    if (isWindows) {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${vitePort} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      execSync(`lsof -ti:${vitePort} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    }
  } catch {}

  await waitForPortRelease(vitePort, 5000);

  return new Promise((resolve, reject) => {
    // pnpx vite 사용 (pnpm exec)
    const server = spawn('pnpx', ['vite', '--host', '--port', String(vitePort)], {
      cwd: aitBuildDir,
      stdio: 'pipe',
      shell: true,
      env: { ...process.env, NODE_OPTIONS: '' }
    });

    let started = false;
    let actualPort = vitePort;

    server.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('[dev server]', output);

      // ANSI 색상 코드 제거 후 포트 파싱
      // IPv4 (localhost, 0.0.0.0, 127.0.0.1), IPv6 ([::], [::1])
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');
      const portMatch = cleanOutput.match(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Server running on port: ${actualPort}`);
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });

    server.stderr.on('data', (data) => {
      const output = data.toString();
      console.error('[dev server stderr]', output);
      // stderr에서도 포트 파싱 시도 (일부 출력이 stderr로 갈 수 있음)
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');
      const portMatch = cleanOutput.match(/localhost:(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        console.log(`📍 Server running on port: ${actualPort}`);
        started = true;
        resolve({ process: server, port: actualPort });
      }
    });

    server.on('error', reject);

    // 30초 타임아웃 (granite/vite 초기화에 시간이 걸릴 수 있음)
    setTimeout(() => {
      if (!started) {
        started = true;
        console.log(`⚠️ Server start timeout, assuming port: ${actualPort}`);
        resolve({ process: server, port: actualPort });
      }
    }, 30000);
  });
}

test.describe('Interactive API Tester', () => {
  test.beforeAll(async () => {
    console.log('🚀 Starting dev server for interactive mode test...');
    const devServer = await startServer(AIT_BUILD, VITE_DEV_PORT);
    serverProcess = devServer.process;
    actualServerPort = devServer.port;

    // 서버 준비 대기 및 확인 - 더 강력하게
    console.log('⏳ Waiting for server to be ready...');
    let serverReady = false;
    for (let i = 0; i < 30; i++) {  // 최대 60초 대기
      try {
        const response = await fetch(`http://localhost:${actualServerPort}/`);
        if (response.ok) {
          console.log(`✅ Server ready on port ${actualServerPort}`);
          serverReady = true;
          break;
        }
      } catch (e) {
        console.log(`⏳ Waiting... (attempt ${i + 1}/30)`);
        await new Promise(r => setTimeout(r, 2000));
      }
    }
    if (!serverReady) {
      throw new Error(`Server failed to start on port ${actualServerPort}`);
    }
    // 추가 안정화 대기
    await new Promise(r => setTimeout(r, 2000));
  });

  test.afterAll(async () => {
    await killServerProcess(serverProcess, [actualServerPort, VITE_DEV_PORT]);
    serverProcess = null;
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

  test('E2E mode (with ?e2e=true) should initialize E2E components', async ({ page }) => {
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
      log.includes('E2EBootstrapper') ||
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
