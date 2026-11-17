// @ts-check
// e2e-full-pipeline.test.js에서 추출한 dev/production 서버 기동·종료 유틸리티.
// 함수 본문은 원본과 동일하다.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import { execSync, spawn } from 'child_process';
import * as net from 'net';
import { SERVER_PORT as serverPort, VITE_DEV_PORT } from './env.js';

export function isPortAvailable(port) {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once('error', () => resolve(false));
    server.once('listening', () => {
      server.close(() => resolve(true));
    });
    server.listen(port, '127.0.0.1');
  });
}

export async function waitForPortRelease(port, timeoutMs = 10000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPortAvailable(port)) {
      return true;
    }
    await new Promise(r => setTimeout(r, 200));
  }
  return false;
}

export async function killServerProcess(proc, ports = []) {
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
        // LISTEN 소켓만 kill: ESTABLISHED 소켓을 가진 Chrome 프로세스까지 kill되는 것을 막는다
        execSync(`lsof -ti tcp:${port} -sTCP:LISTEN | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
      }
    } catch {}
  }

  for (const port of ports) {
    await waitForPortRelease(port, 5000);
  }
}

export async function startDevServer(aitBuildDir, defaultPort, extraEnv = {}) {
  const vitePort = VITE_DEV_PORT;

  const myPorts = [serverPort, vitePort];
  const isWindows = process.platform === 'win32';
  for (const port of myPorts) {
    try {
      if (isWindows) {
        execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${port} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
      } else {
        execSync(`lsof -ti tcp:${port} -sTCP:LISTEN | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
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

export async function startProductionServer(aitBuildDir, defaultPort) {
  const isWindows = process.platform === 'win32';
  const myPort = serverPort;
  try {
    if (isWindows) {
      execSync(`for /f "tokens=5" %a in ('netstat -ano ^| findstr :${myPort} ^| findstr LISTENING') do taskkill /F /PID %a 2>nul`, { stdio: 'ignore', shell: true });
    } else {
      execSync(`lsof -ti tcp:${myPort} -sTCP:LISTEN | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
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
