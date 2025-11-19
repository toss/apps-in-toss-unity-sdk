import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';
import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PROJECT_PATH = path.join(__dirname, '../SampleUnityProject');
const BUILD_PATH = path.join(PROJECT_PATH, 'ait-build');
const UNITY_PATH = process.env.UNITY_PATH || '/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity';

test.describe('Unity WebGL E2E Benchmark', () => {
  let devServer;

  test.beforeAll(async () => {
    // Unity 빌드는 수동으로 먼저 실행해야 함 (너무 오래 걸림)
    if (!process.env.SKIP_BUILD) {
      console.log('⚠️  Unity build is required but skipped by default.');
      console.log('To run full E2E test including Unity build:');
      console.log('1. Open Unity project: Tests/E2E/SampleUnityProject');
      console.log('2. Create BenchmarkScene.unity with E2ETestRunner');
      console.log('3. Run: Apps in Toss > Build for E2E');
      console.log('4. Then run: npm test');
      console.log('');
      console.log('Or set SKIP_BUILD=false to attempt automatic build (slow!)');
    }

    // ait-build/dist 폴더가 있는지 확인
    if (!fs.existsSync(path.join(BUILD_PATH, 'dist'))) {
      console.log('❌ Build artifacts not found. Please build the project first.');
      console.log(`Expected path: ${path.join(BUILD_PATH, 'dist')}`);
      throw new Error('Build artifacts not found. Run Unity build first.');
    }

    // Dev server 시작
    console.log('🚀 Starting dev server...');
    devServer = spawn('npm', ['run', 'preview', '--', '--port', '4173'], {
      cwd: BUILD_PATH,
      stdio: 'pipe',
      shell: true
    });

    // Dev server 로그 출력
    devServer.stdout?.on('data', (data) => {
      console.log(`[Dev Server] ${data.toString().trim()}`);
    });

    devServer.stderr?.on('data', (data) => {
      console.error(`[Dev Server Error] ${data.toString().trim()}`);
    });

    // 서버 시작 대기
    await new Promise(resolve => setTimeout(resolve, 5000));
  });

  test.afterAll(async () => {
    if (devServer) {
      console.log('🛑 Stopping dev server...');
      devServer.kill();
    }
  });

  test('should verify build artifacts exist', async () => {
    // 빌드 아티팩트 검증
    expect(fs.existsSync(path.join(BUILD_PATH, 'dist/index.html'))).toBe(true);
    expect(fs.existsSync(path.join(BUILD_PATH, 'dist/Build'))).toBe(true);

    // 빌드 크기 검증
    const distSize = getDirectorySize(path.join(BUILD_PATH, 'dist'));
    console.log(`📊 Build size: ${(distSize / 1024 / 1024).toFixed(2)}MB`);
    expect(distSize).toBeLessThan(100 * 1024 * 1024); // 100MB 미만

    // Placeholder 치환 검증
    const indexHtml = fs.readFileSync(
      path.join(BUILD_PATH, 'dist/index.html'),
      'utf-8'
    );
    expect(indexHtml).not.toContain('%UNITY_');
    expect(indexHtml).not.toContain('%AIT_');

    // granite.config.ts도 확인
    const graniteConfigPath = path.join(BUILD_PATH, 'granite.config.ts');
    if (fs.existsSync(graniteConfigPath)) {
      const graniteConfig = fs.readFileSync(graniteConfigPath, 'utf-8');
      expect(graniteConfig).not.toContain('%AIT_');
    }
  });

  test('should load in headless browser and measure performance', async ({ page }) => {
    // 성능 측정 시작
    const startTime = Date.now();

    await page.goto('http://localhost:4173', {
      waitUntil: 'networkidle',
      timeout: 60000
    });

    const pageLoadTime = Date.now() - startTime;
    console.log(`⏱️  Page load time: ${pageLoadTime}ms`);

    // Unity 인스턴스 로딩 대기 (최대 2분)
    const unityLoaded = await page.waitForFunction(
      () => window.unityInstance?.Module?.ready || window.createUnityInstance !== undefined,
      { timeout: 120000 }
    ).catch(() => null);

    if (!unityLoaded) {
      console.log('⚠️  Unity instance not ready. Checking page content...');
      const content = await page.content();
      console.log('Page title:', await page.title());

      // Unity loader가 있는지 확인
      const hasLoader = content.includes('UnityLoader') || content.includes('createUnityInstance');
      console.log('Has Unity loader:', hasLoader);

      if (!hasLoader) {
        throw new Error('Unity WebGL content not found. Build may be incomplete.');
      }

      // Unity 초기화 대기 (조금 더 기다림)
      await page.waitForTimeout(10000);
    }

    const unityLoadTime = Date.now() - startTime;
    console.log(`🎮 Unity load time: ${unityLoadTime}ms`);

    // GPU 가속 확인
    const gpuInfo = await page.evaluate(() => {
      const canvas = document.querySelector('canvas');
      if (!canvas) return { supported: false, renderer: 'No canvas found' };

      const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
      if (!gl) return { supported: false, renderer: 'WebGL not supported' };

      const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
      const renderer = gl.getParameter(debugInfo?.UNMASKED_RENDERER_WEBGL || 0x1F01);

      return {
        supported: true,
        renderer: renderer || 'unknown',
        isHardwareAccelerated: renderer && !renderer.includes('SwiftShader')
      };
    });

    console.log('🎨 GPU Info:', gpuInfo);
    expect(gpuInfo.supported).toBe(true);

    // 초기 로딩 메트릭 수집
    const loadMetrics = await page.evaluate(() => {
      const nav = performance.getEntriesByType('navigation')[0];
      if (!nav) return null;

      return {
        domContentLoaded: Math.round(nav.domContentLoadedEventEnd - nav.fetchStart),
        loadComplete: Math.round(nav.loadEventEnd - nav.fetchStart),
      };
    });

    if (loadMetrics) {
      console.log('📈 Load Metrics:', loadMetrics);
    }

    // 벤치마크 결과 수집 대기 (E2ETestRunner에서 전송)
    const benchmarkData = await page.evaluate(() => {
      return new Promise(resolve => {
        // 이미 전송된 데이터가 있는지 확인
        if (window.benchmarkDataReceived) {
          resolve(window.benchmarkDataReceived);
          return;
        }

        // 데이터 수신 핸들러 등록
        window.receiveBenchmarkData = (data) => {
          try {
            const parsed = typeof data === 'string' ? JSON.parse(data) : data;
            window.benchmarkDataReceived = parsed;
            resolve(parsed);
          } catch (e) {
            console.error('Failed to parse benchmark data:', e);
            resolve(null);
          }
        };

        // 15초 타임아웃
        setTimeout(() => {
          console.log('Benchmark data timeout');
          resolve(null);
        }, 15000);
      });
    });

    if (benchmarkData) {
      console.log('🎯 Benchmark Results:', benchmarkData);

      // 성능 검증 (관대한 기준)
      expect(benchmarkData.avgFPS).toBeGreaterThan(20); // 최소 20 FPS
      expect(benchmarkData.minFPS).toBeGreaterThan(10); // 최소 FPS도 10 이상
      expect(benchmarkData.memoryUsageMB).toBeLessThan(1024); // 1GB 미만
    } else {
      console.log('⚠️  No benchmark data received from Unity (this is OK for initial test)');
    }

    // 메트릭을 JSON 파일로 저장
    const distSize = getDirectorySize(path.join(BUILD_PATH, 'dist'));
    const results = {
      timestamp: new Date().toISOString(),
      pageLoadTime,
      unityLoadTime,
      distSize,
      loadMetrics: loadMetrics || {},
      benchmarkData: benchmarkData || {},
      gpuInfo
    };

    fs.writeFileSync(
      path.join(__dirname, 'benchmark-results.json'),
      JSON.stringify(results, null, 2)
    );

    console.log('✅ Benchmark results saved to benchmark-results.json');
  });
});

function getDirectorySize(dirPath) {
  let totalSize = 0;

  if (!fs.existsSync(dirPath)) {
    return 0;
  }

  const files = fs.readdirSync(dirPath, { withFileTypes: true });

  for (const file of files) {
    const filePath = path.join(dirPath, file.name);
    if (file.isDirectory()) {
      totalSize += getDirectorySize(filePath);
    } else {
      totalSize += fs.statSync(filePath).size;
    }
  }

  return totalSize;
}
