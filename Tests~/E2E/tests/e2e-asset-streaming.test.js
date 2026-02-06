// @ts-check
import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Apps in Toss Unity SDK - Asset Streaming E2E Tests
 *
 * 4개 테스트 케이스:
 * 1. 마이그레이션 빌드 검증 (build-validation.json에서 StreamingAssets 확인)
 * 2. StreamingAssets HTTP 접근성 (Production 서버에서 번들 파일 접근)
 * 3. 런타임 Addressable 로드 성공 (Unity에서 에셋 로드)
 * 4. .data 파일 크기 변화 확인
 */

// ES Module에서 __dirname 대체
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 경로 상수
const PROJECT_ROOT = path.resolve(__dirname, '../../..');

function findSampleProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) {
    return envPath;
  }

  const versionPatterns = ['6000.3', '6000.2', '6000.0', '2022.3', '2021.3'];
  for (const version of versionPatterns) {
    const projectPath = path.resolve(__dirname, `../SampleUnityProject-${version}`);
    const distPath = path.resolve(projectPath, 'ait-build/dist/web');
    if (fs.existsSync(distPath)) {
      console.log(`📁 Auto-detected project: SampleUnityProject-${version}`);
      return projectPath;
    }
  }

  return path.resolve(__dirname, '../SampleUnityProject-6000.2');
}

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

const SAMPLE_PROJECT = findSampleProject();
const AIT_BUILD = path.resolve(SAMPLE_PROJECT, 'ait-build');
const DIST_WEB = path.resolve(AIT_BUILD, 'dist/web');
const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
const SERVER_PORT = 4173 + PORT_OFFSET;

console.log(`📦 Unity project: ${SAMPLE_PROJECT}`);
console.log(`🔌 Server port: ${SERVER_PORT} (offset: ${PORT_OFFSET})`);

// 유틸리티 함수

function fileExists(filePath) {
  try {
    return fs.existsSync(filePath) && fs.statSync(filePath).isFile();
  } catch {
    return false;
  }
}

function directoryExists(dirPath) {
  try {
    return fs.existsSync(dirPath) && fs.statSync(dirPath).isDirectory();
  } catch {
    return false;
  }
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
  for (const port of ports) {
    try {
      execSync(`lsof -ti:${port} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
    } catch {}
  }
  for (const port of ports) {
    await waitForPortRelease(port, 5000);
  }
}

async function startProductionServer(aitBuildDir, defaultPort) {
  try {
    execSync(`lsof -ti:${defaultPort} | xargs kill -9 2>/dev/null || true`, { stdio: 'ignore' });
  } catch {}
  await waitForPortRelease(defaultPort, 5000);

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
      const cleanOutput = output.replace(/\x1B\[[0-9;]*[mGKH]/g, '');
      const portMatch = cleanOutput.match(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/);
      if (portMatch && !started) {
        actualPort = parseInt(portMatch[1], 10);
        started = true;
        resolve({ process: server, port: actualPort });
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


test.describe('Asset Streaming E2E Tests', () => {
  let buildValidation = null;

  test.beforeAll(async () => {
    // build-validation.json 로드
    const validationPath = path.resolve(AIT_BUILD, 'build-validation.json');
    if (fileExists(validationPath)) {
      const raw = fs.readFileSync(validationPath, 'utf-8');
      buildValidation = JSON.parse(raw);
    }
  });

  test('1. Migration build validation - StreamingAssets should exist', async () => {
    test.skip(!buildValidation, 'build-validation.json not found');

    expect(buildValidation.passed).toBe(true);
    expect(buildValidation.hasStreamingAssets).toBe(true);
    expect(buildValidation.streamingAssetBundleCount).toBeGreaterThan(0);
    expect(buildValidation.streamingAssetsSizeMB).toBeGreaterThan(0);

    console.log(`✅ StreamingAssets: ${buildValidation.streamingAssetBundleCount} bundles, ${buildValidation.streamingAssetsSizeMB.toFixed(2)} MB`);
    console.log(`✅ .data file size: ${buildValidation.dataSizeMB.toFixed(2)} MB`);
  });

  test.describe.serial('Production Server Tests (shared session)', () => {
    let sharedPage;
    let sharedServerProcess;
    let sharedServerPort;

    test.beforeAll(async ({ browser }) => {
      test.skip(!directoryExists(DIST_WEB), 'dist/web not found');

      // Production 서버 시작
      const { process: proc, port } = await startProductionServer(AIT_BUILD, SERVER_PORT);
      sharedServerProcess = proc;
      sharedServerPort = port;
      console.log(`🚀 Production server started on port ${port}`);

      // 페이지 생성 및 Unity 초기화 대기
      const context = await browser.newContext();
      sharedPage = await context.newPage();

      const e2eUrl = `http://localhost:${port}?e2e=true`;
      console.log(`🌐 Navigating to ${e2eUrl}`);

      await sharedPage.goto(e2eUrl, { waitUntil: 'domcontentloaded', timeout: 30000 });

      // Unity 초기화 대기
      try {
        await sharedPage.waitForFunction(
          () => window.unityInstance !== undefined && window.unityInstance !== null,
          { timeout: 120000 }
        );
        console.log('✅ Unity instance ready');
      } catch (e) {
        console.error('⚠️ Unity instance not ready, some tests may fail');
      }

      // 트리거 함수 등록 대기
      await sharedPage.waitForFunction(
        () => typeof window.TriggerStreamingTest === 'function',
        { timeout: 30000 }
      ).catch(() => console.log('⚠️ TriggerStreamingTest not registered'));
    });

    test.afterAll(async () => {
      if (sharedPage) {
        await sharedPage.close().catch(() => {});
      }
      if (sharedServerProcess) {
        await killServerProcess(sharedServerProcess, [sharedServerPort]);
      }
    });

    test('2. StreamingAssets should be accessible via HTTP', async () => {
      test.skip(!sharedPage, 'Server not started');

      const streamingAssetsPath = path.resolve(DIST_WEB, 'StreamingAssets');
      test.skip(!directoryExists(streamingAssetsPath), 'StreamingAssets directory not found in dist');

      // StreamingAssets 디렉토리에서 번들 파일 찾기
      const files = fs.readdirSync(streamingAssetsPath, { recursive: true });
      const bundleFiles = [];
      for (const file of files) {
        const filePath = path.join(streamingAssetsPath, String(file));
        if (fs.statSync(filePath).isFile()) {
          const ext = path.extname(String(file)).toLowerCase();
          if (ext !== '.meta' && ext !== '.json') {
            bundleFiles.push(String(file));
          }
        }
      }

      expect(bundleFiles.length).toBeGreaterThan(0);
      console.log(`📦 Found ${bundleFiles.length} bundle files`);

      // 각 번들 파일에 HTTP GET 요청
      for (const bundleFile of bundleFiles) {
        const url = `http://localhost:${sharedServerPort}/StreamingAssets/${bundleFile}`;
        const response = await sharedPage.request.get(url);
        expect(response.status()).toBe(200);

        const body = await response.body();
        expect(body.length).toBeGreaterThan(0);
        console.log(`  ✅ ${bundleFile}: ${response.status()} (${(body.length / 1024).toFixed(1)} KB)`);
      }

      // 잘못된 경로는 404 반환
      const badResponse = await sharedPage.request.get(
        `http://localhost:${sharedServerPort}/StreamingAssets/nonexistent_bundle_12345`
      );
      expect(badResponse.status()).toBe(404);
      console.log('  ✅ Invalid path returns 404');
    });

    test('3. Runtime Addressable load should succeed', async () => {
      test.skip(!sharedPage, 'Server not started');

      // 스트리밍 테스트 데이터 초기화
      await sharedPage.evaluate(() => {
        window.__E2E_STREAMING_TEST_DATA__ = null;
      });

      // TriggerStreamingTest 호출
      const triggerResult = await sharedPage.evaluate(() => {
        if (typeof window.TriggerStreamingTest === 'function') {
          return window.TriggerStreamingTest();
        }
        return false;
      });

      if (!triggerResult) {
        console.log('⚠️ TriggerStreamingTest not available, skipping runtime test');
        test.skip(true, 'TriggerStreamingTest not available');
        return;
      }

      // 결과 대기 (CustomEvent 또는 window 객체)
      const streamingResult = await sharedPage.waitForFunction(
        () => window.__E2E_STREAMING_TEST_DATA__ !== null && window.__E2E_STREAMING_TEST_DATA__ !== undefined,
        { timeout: 60000 }
      ).then(() => sharedPage.evaluate(() => {
        return JSON.parse(window.__E2E_STREAMING_TEST_DATA__);
      }));

      console.log('📊 Streaming test result:', streamingResult);

      expect(streamingResult.success).toBe(true);
      expect(streamingResult.textureWidth).toBe(2048);
      expect(streamingResult.textureHeight).toBe(2048);
      expect(streamingResult.loadTimeMs).toBeGreaterThanOrEqual(0);

      console.log(`✅ Texture loaded: ${streamingResult.textureWidth}x${streamingResult.textureHeight} in ${streamingResult.loadTimeMs}ms`);
    });
  });

  test('4. Data file size should reflect streaming asset separation', async () => {
    test.skip(!buildValidation, 'build-validation.json not found');
    test.skip(!buildValidation.hasStreamingAssets, 'No StreamingAssets in build');

    // StreamingAssets가 있을 때 .data 파일 크기가 합리적인지 확인
    // (완전한 크기 비교는 불가능하지만, 기본 검증)
    expect(buildValidation.dataSizeMB).toBeGreaterThan(0);
    expect(buildValidation.streamingAssetsSizeMB).toBeGreaterThan(0);

    console.log(`📊 Build size breakdown:`);
    console.log(`  .data file: ${buildValidation.dataSizeMB.toFixed(2)} MB`);
    console.log(`  StreamingAssets: ${buildValidation.streamingAssetsSizeMB.toFixed(2)} MB`);
    console.log(`  Total build: ${buildValidation.buildSizeMB.toFixed(2)} MB`);
  });
});
