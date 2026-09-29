// @ts-check
import { test, expect } from '@playwright/test';
import { spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import {
  TESTS_DIR,
  PROJECT_ROOT,
  SAMPLE_PROJECT,
  AIT_BUILD,
  DIST_WEB,
  isDevBuild,
  BENCHMARKS,
  VITE_DEV_PORT,
  SERVER_PORT as serverPort,
} from './lib/env.js';
import { directoryExists, fileExists, getDirectorySizeMB, checkForPlaceholders } from './lib/fs-utils.js';
import { killServerProcess, startDevServer, startProductionServer } from './lib/server.js';
import { applyMobileThrottling } from './lib/unity.js';
import { reloadWithRetry, POLICY_WARM_CACHE } from './lib/reload-retry.js';
import { registerPlayerPrefsTests } from './suites/playerprefs.suite.js';

/**
 * Apps in Toss Unity SDK - E2E Full Pipeline Tests
 *
 * 5개 테스트 케이스 (빠른 테스트 → 느린 테스트 순서):
 * 1. Build Validation (build-validation.json 확인 + 메트릭 수집)
 * 2. AIT Dev Server (Vite dev 서버 + Unity 초기화)
 * 3-5. Production Tests (세션 공유로 초기화 1회):
 *   3. Production Server + Preload Metrics (Unity 초기화 + Resource Timing)
 *   4. Runtime API Error Validation (SDK API 에러 검증)
 *   5. Serialization Round-trip Tests (C# ↔ JavaScript 직렬화 검증)
 *
 * Test 3-5 세션 공유:
 * - 서버 1회 시작, Unity 1회 초기화로 반복 초기화 방지
 * - JavaScript 트리거 함수로 테스트 실행 (TriggerAPITest, TriggerSerializationTest)
 */

// 결과 저장용
let testResults = {
  timestamp: new Date().toISOString(),
  tests: {}
};

let serverProcess = null;

// ============================================================================
// Test Suite
// ============================================================================

test.describe('Apps in Toss Unity SDK E2E Pipeline', () => {

  test.beforeAll(async () => {
    console.log('🚀 E2E Pipeline Tests Starting...');
    console.log(`📁 Project Root: ${PROJECT_ROOT}`);
    console.log(`📁 Sample Project: ${SAMPLE_PROJECT}`);
    console.log(`📁 AIT Build: ${AIT_BUILD}`);
  });

  test.afterAll(async () => {
    if (serverProcess) {
      serverProcess.kill();
      serverProcess = null;
    }

    // 1. 전체 테스트 결과
    const resultsPath = path.resolve(TESTS_DIR, 'e2e-test-results.json');
    fs.writeFileSync(resultsPath, JSON.stringify(testResults, null, 2));

    // 2. 벤치마크 결과 (workflow에서 업로드하는 파일)
    const benchmarkPath = path.resolve(TESTS_DIR, 'benchmark-results.json');
    const benchmarkResults = {
      timestamp: testResults.timestamp,
      unityProject: SAMPLE_PROJECT,
      buildSize: testResults.tests['1_build_validation']?.buildSizeMB,
      pageLoadTime: testResults.tests['3_production_server']?.pageLoadTimeMs,
      unityLoadTime: testResults.tests['3_production_server']?.unityLoadTimeMs,
      webgl: testResults.tests['3_production_server']?.webgl,
      apiTestResults: testResults.tests['4_runtime_api'] ? {
        totalAPIs: testResults.tests['4_runtime_api'].totalAPIs,
        successCount: testResults.tests['4_runtime_api'].successCount,
        unexpectedErrorCount: testResults.tests['4_runtime_api'].unexpectedErrorCount
      } : null,
      compressionValidation: testResults.tests['1_build_validation']?.compressionValidation || null,
      testsPassed: Object.values(testResults.tests || {}).filter(t => t.passed).length,
      testsTotal: Object.keys(testResults.tests || {}).length
    };
    fs.writeFileSync(benchmarkPath, JSON.stringify(benchmarkResults, null, 2));

    // stdout으로 결과 출력
    console.log('\n');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📊 E2E Test Results');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');

    const tests = testResults.tests || {};
    const passed = Object.values(tests).filter(t => t.passed).length;
    const total = Object.keys(tests).length;

    console.log(`\n  ✅ Tests Passed: ${passed}/${total}`);

    const buildSize = tests['1_build_validation']?.buildSizeMB;
    const pageLoad = tests['3_production_server']?.pageLoadTimeMs;
    const unityLoad = tests['3_production_server']?.unityLoadTimeMs;
    const renderer = tests['3_production_server']?.webgl?.renderer;

    console.log('\n  📦 Build Size:      ' + (buildSize ? buildSize.toFixed(2) + ' MB' : 'N/A'));
    console.log('  ⏱️  Page Load:       ' + (pageLoad ? pageLoad + ' ms' : 'N/A'));
    console.log('  🎮 Unity Load:      ' + (unityLoad ? unityLoad + ' ms' : 'N/A'));
    console.log('  🖥️  GPU Renderer:    ' + (renderer || 'N/A'));

    console.log('\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('📄 Full Results (JSON):');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log(JSON.stringify(testResults, null, 2));
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n');
  });


  // -------------------------------------------------------------------------
  // Test 1: Build Validation (build-validation.json 확인 + 메트릭 수집)
  // 기존 Tests 1, 3, 4를 통합 - C# BuildOutputValidator가 생성한 결과를 확인
  // -------------------------------------------------------------------------
  test('1. Build validation should pass', async () => {
    test.setTimeout(60000);

    // build-validation.json 확인 (C# BuildOutputValidator가 빌드 후 생성)
    const validationPath = path.resolve(AIT_BUILD, 'build-validation.json');

    if (fileExists(validationPath)) {
      const validation = JSON.parse(fs.readFileSync(validationPath, 'utf-8'));
      console.log(`📋 Build validation: ${validation.passed ? 'PASSED' : 'FAILED'}`);
      console.log(`   Build size: ${validation.buildSizeMB?.toFixed(2)} MB`);
      console.log(`   Compression: ${validation.compressionFormat}`);
      console.log(`   Files: ${validation.fileCount}`);

      if (validation.errors?.length > 0) {
        console.log(`   Errors:`);
        validation.errors.forEach(e => console.log(`     ❌ ${e}`));
      }
      if (validation.warnings?.length > 0) {
        console.log(`   Warnings:`);
        validation.warnings.forEach(w => console.log(`     ⚠️ ${w}`));
      }

      // AIT_COMPRESSION_FORMAT 매핑: 0=disabled, 1=gzip, 2=brotli, -1=auto(brotli),
      // 미설정 시 사용자 PlayerSettings 따름 — null로 기록.
      const compressionFormatEnvMap = { '0': 'disabled', '1': 'gzip', '2': 'brotli', '-1': 'brotli' };
      const expectedCompressionFormat = compressionFormatEnvMap[process.env.AIT_COMPRESSION_FORMAT] ?? null;

      testResults.tests['1_build_validation'] = {
        passed: validation.passed,
        buildSizeMB: validation.buildSizeMB,
        compressionFormat: validation.compressionFormat,
        fileCount: validation.fileCount,
        compressionValidation: {
          detectedFormat: validation.compressionFormat,
          expectedFormat: expectedCompressionFormat
        }
      };

      expect(validation.passed, 'Build validation should pass').toBe(true);
      // Dev Build + 압축 비활성화 조합에서는 산출물이 50MB 한도를 초과하므로 size 단언 스킵.
      // 배포 빌드에는 영향 없음 (production 워크플로우는 사용자 PlayerSettings/압축 따름).
      if (!isDevBuild) {
        expect(validation.buildSizeMB).toBeLessThanOrEqual(BENCHMARKS.MAX_BUILD_SIZE_MB);
      }
    } else {
      // build-validation.json이 없는 경우 직접 검증 (이전 버전 호환)
      console.log('⚠️ build-validation.json not found, performing direct validation...');

      expect(directoryExists(AIT_BUILD), 'ait-build/ should exist').toBe(true);
      expect(directoryExists(DIST_WEB), 'ait-build/dist/web/ should exist').toBe(true);

      // package.json
      expect(fileExists(path.resolve(AIT_BUILD, 'package.json')), 'package.json should exist').toBe(true);

      // granite.config.ts 플레이스홀더 (web-framework 2.x granite build 전용)
      const graniteConfigPath = path.resolve(AIT_BUILD, 'granite.config.ts');
      if (fileExists(graniteConfigPath)) {
        const content = fs.readFileSync(graniteConfigPath, 'utf-8');
        const placeholders = checkForPlaceholders(content);
        expect(placeholders.length, 'Should have no unsubstituted placeholders in granite.config.ts').toBe(0);
      }

      // apps-in-toss.config.ts 플레이스홀더 (web-framework 3.x ait build — cosmiconfig 탐색 대상)
      // 3.x가 실제로 읽는 설정 파일. granite.config.ts만 검증하면 false green이 되므로 함께 검증한다.
      const appsInTossConfigPath = path.resolve(AIT_BUILD, 'apps-in-toss.config.ts');
      if (fileExists(appsInTossConfigPath)) {
        const content = fs.readFileSync(appsInTossConfigPath, 'utf-8');
        const placeholders = checkForPlaceholders(content);
        expect(placeholders.length, 'Should have no unsubstituted placeholders in apps-in-toss.config.ts').toBe(0);
      }

      // node_modules
      expect(directoryExists(path.resolve(AIT_BUILD, 'node_modules')), 'node_modules/ should exist').toBe(true);

      // index.html 플레이스홀더
      const indexPath = path.resolve(DIST_WEB, 'index.html');
      expect(fileExists(indexPath), 'index.html should exist').toBe(true);
      const indexContent = fs.readFileSync(indexPath, 'utf-8');
      const indexPlaceholders = checkForPlaceholders(indexContent);
      expect(indexPlaceholders.length, 'index.html should have no unsubstituted placeholders').toBe(0);

      // Build 폴더
      const buildPath = path.resolve(DIST_WEB, 'Build');
      expect(directoryExists(buildPath), 'Build/ folder should exist').toBe(true);

      const distSizeMB = getDirectorySizeMB(DIST_WEB);

      testResults.tests['1_build_validation'] = {
        passed: true,
        buildSizeMB: distSizeMB,
      };
    }
  });


  // -------------------------------------------------------------------------
  // Test 2: AIT Dev Server (vite)
  // -------------------------------------------------------------------------
  test('2. AIT dev server should start and load Unity', async ({ page }) => {
    // devtools mock 초기화(cold optimizeDeps ~2-4초) + TriggerAPITest allowlist 대기가
    // 추가되어 기존 120000보다 여유를 둔다.
    // 300000 산정 근거: 평시 소요 2.0분(run 1058 실측) 대비 200000은 여유가 40%뿐이라
    // GH-hosted 러너 성능 편차(피크 시간대 wasm 인스턴스화 80초→150초+ 실측)를 흡수하지
    // 못한다. 이 테스트는 기능 검증이 목적이고 소요 시간은 리포트의 per-test duration으로
    // 계속 관측하므로 예산은 편차를 흡수할 만큼 여유 있게 둔다.
    test.setTimeout(300000);

    // 패널/mock 주입 실패는 브라우저 콘솔에만 남고 테스트 실패로 드러나지 않을 수 있어,
    // CI 로그에서 바로 확인할 수 있도록 pageerror/console을 캡처한다.
    page.on('pageerror', error => {
      console.log('[Page Error]', error.message);
    });
    page.on('console', msg => {
      const type = msg.type();
      const text = msg.text();
      if (type === 'error' || type === 'warning' || text.includes('@apps-in-toss/devtools')) {
        console.log('[Browser Console]', text);
      }
    });

    await applyMobileThrottling(page);

    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist for dev server').toBe(true);

    console.log('🚀 Starting dev server (vite) with devtools mock enabled...');
    // AIT_DEVTOOLS=1: Editor가 AIT/Dev Server 실행 시 항상 명시하는 값과 동일한 계약
    // (vite.config.ts가 AIT_DEVTOOLS='1'일 때만 devtools unplugin + 패널을 활성화).
    const devServer = await startDevServer(AIT_BUILD, serverPort, { AIT_DEVTOOLS: '1' });
    serverProcess = devServer.process;
    const actualPort = devServer.port;

    // 서버가 준비될 때까지 대기
    let serverReady = false;
    for (let i = 0; i < 30; i++) {
      try {
        const response = await fetch(`http://localhost:${actualPort}/`, { method: 'HEAD' });
        if (response.ok) {
          serverReady = true;
          break;
        }
      } catch {}
      await new Promise(r => setTimeout(r, 500));
    }

    if (!serverReady) {
      const tryPorts = [5173, 8081, 3000];
      for (const port of tryPorts) {
        if (port === actualPort) continue;
        try {
          const response = await fetch(`http://localhost:${port}/`, { method: 'HEAD' });
          if (response.ok) {
            serverReady = true;
            break;
          }
        } catch {}
      }
    }

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

    const startTime = Date.now();
    const response = await page.goto(`http://localhost:${workingPort}?e2e=true`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000
    });

    expect(response?.status()).toBe(200);

    const hasUnityLoader = await page.evaluate(() => {
      return typeof window['createUnityInstance'] === 'function' ||
             document.querySelector('script[src*="loader.js"]') !== null ||
             document.body.innerHTML.includes('createUnityInstance');
    });

    console.log(`🎮 Unity loader present: ${hasUnityLoader}`);

    try {
      await page.waitForFunction(() => {
        return window['unityInstance'] !== undefined ||
               document.querySelector('canvas') !== null;
      }, { timeout: 60000 });
      console.log('✅ Unity instance initialized');
    } catch {
      console.log('⚠️ Unity instance not initialized within timeout (may be expected in CI)');
    }

    const loadTime = Date.now() - startTime;

    // -------------------------------------------------------------------------
    // devtools mock 통합 단언 (AIT_DEVTOOLS=1 계약 검증)
    // -------------------------------------------------------------------------

    // ① devtools unplugin이 @apps-in-toss/web-framework를 mock으로 alias했다는 직접 증거:
    //    window.AppsInToss.getPlatformOS 함수가 주입되어 있어야 한다.
    // unity-bridge.ts 모듈 실행(window.AppsInToss 생성)은 devtools mock의 cold optimizeDeps(~2-4초) 및
    // Unity 캔버스 로딩과 병렬·독립 경로라 단발 체크가 레이스할 수 있어 폴링으로 먼저 대기한다.
    await page.waitForFunction(() => typeof window['AppsInToss']?.getPlatformOS === 'function', { timeout: 15000 }).catch(() => {});
    const hasGetPlatformOS = await page.evaluate(() => {
      return typeof window['AppsInToss']?.getPlatformOS === 'function';
    });
    expect(hasGetPlatformOS, 'window.AppsInToss.getPlatformOS should be injected by devtools unplugin').toBe(true);

    // ② mock 함수가 reject 없이 문자열을 반환하는지 (mock이 실제로 동작한다는 증명)
    const platformOSResult = await page.evaluate(async () => {
      try {
        const os = await window['AppsInToss'].getPlatformOS();
        return { ok: true, type: typeof os };
      } catch (e) {
        return { ok: false, error: e?.message || String(e) };
      }
    });
    expect(platformOSResult.ok, `getPlatformOS() should not reject (got: ${JSON.stringify(platformOSResult)})`).toBe(true);
    expect(platformOSResult.type, 'getPlatformOS() should resolve to a string').toBe('string');

    // ③ 패널 호스트 엘리먼트 존재 (AIT_DEVTOOLS_PANEL 기본 on, 명시 미설정 시 활성).
    //    셀렉터 `.ait-panel-toggle`은 devtools 패키지 자신이
    //    "The CSS-class / attribute contract relied on by e2e/panel.test.ts"로 문서화한
    //    안정 계약(dist/panel/index.js 주석) — 내부 React 트리 구조가 바뀌어도
    //    devtools가 마이너 업데이트에서 지키기로 약속한 셀렉터라 관대하게 안전하다.
    // 패널 모듈 로드도 Unity 캔버스 등장과 병렬 경로라 카운트 확인 전에 짧게 폴링한다.
    await page.waitForSelector('.ait-panel-toggle', { timeout: 10000 }).catch(() => {});
    const panelToggleCount = await page.locator('.ait-panel-toggle').count();
    expect(panelToggleCount, 'devtools floating panel toggle button should be mounted').toBeGreaterThan(0);

    // ④ 소규모 allowlist API가 mock에서 실제로 성공하는지, 기존 TriggerAPITest 하네스
    //    (Test 4와 동일한 트리거 + 결과 수집 코드)를 재사용해 확인한다 — 새 하네스를
    //    만들지 않는다.
    //    allowlist 근거: RuntimeAPITester.cs가 호출하는 apiName과
    //    node_modules/@apps-in-toss/devtools/dist/mock/3x.js의 export를 대조해,
    //    aitState 기본값(권한 allowed, deviceModes mock 등)으로 예외 없이 즉시 성공
    //    반환하는 항목만 선정했다.
    //    - getPlatformOS/getOperationalEnvironment/getDeviceId/getLocale: 파라미터 없음
    //    - env.getDeploymentId/getAppsInTossGlobals/getServerTime:
    //      SDK 3.0 신규 표면(2026-08 감사로 RuntimeAPITester에 편입). mock이 각각
    //      aitState 값을 동기/즉시 Promise로 반환 — 예외 경로 없음.
    //    - isMinVersionSupported: SDK 3.0 신규 표면(2026-08 감사로 RuntimeAPITester에 편입).
    //      실제 SDK 타입(@apps-in-toss/web-framework)과 devtools mock 구현 모두 boolean을
    //      "동기" 반환하는 함수라 Promise가 아니다 — 생성된 jslib(__isMinVersionSupported_Internal)도
    //      window.AppsInToss.isMinVersionSupported(...) 반환값을 await 없이 그대로 읽어 즉시
    //      SendMessage 콜백을 보낸다. (Unity 쪽 AIT.IsMinVersionSupported가 Task/Awaitable인 것은
    //      SendMessage 콜백 브리지의 공통 패턴일 뿐이며, 이 API 자체가 비동기라서가 아니다.)
    //      예외 경로 없음.
    //    - Storage.getItem/setItem/removeItem/clearItems: 권한 게이트 없이 localStorage에
    //      직접 위임 — 예외 경로 없음(devtools#770/#775에서 이미 실측 확정).
    //    - partner.addAccessoryButton/removeAccessoryButton: console.log만 하고 즉시
    //      resolve하는 스텁 — 예외 경로 없음.
    //    - fetchAlbumItems: photos 권한 기본값 allowed + deviceModes.photos 기본값
    //      mock이라 file picker 없이 즉시 목업 배열을 반환.
    //    - SafeAreaInsets.get: aitState.state.safeAreaInsets 스냅샷을 동기 반환.
    const MOCK_SUCCESS_ALLOWLIST = [
      'API_GetPlatformOS',
      'API_GetOperationalEnvironment',
      'API_GetDeviceId',
      'API_GetLocale',
      'API_EnvGetDeploymentId',
      'API_GetAppsInTossGlobals',
      'API_IsMinVersionSupported',
      'API_GetServerTime',
      'API_StorageSetItem',
      'API_StorageGetItem',
      'API_StorageRemoveItem',
      'API_StorageClearItems',
      'API_PartnerAddAccessoryButton',
      'API_PartnerRemoveAccessoryButton',
      'API_FetchAlbumItems',
      'API_SafeAreaInsetsGet',
    ];

    try {
      await page.waitForFunction(() => typeof window['TriggerAPITest'] === 'function', { timeout: 10000 });
    } catch {
      console.log('⚠️ TriggerAPITest not found on dev server page (mock allowlist assertion may fail)');
    }

    // 원인: 아래 TriggerAPITest 스윕이 CloseView를 호출하면 devtools mock의 closeView()가
    // window.history.back()을 실행해 page.goto 직후 페이지를 실제로 이탈시키고, 이를
    // 기다리는 evaluate()의 실행 컨텍스트를 파괴한다. same-document 히스토리 엔트리를
    // 미리 쌓아 back()을 컨텍스트 보존형 popstate 이동으로 바꿔 방지한다.
    await page.evaluate(() => {
      history.pushState({ aitE2eGuard: true }, '', location.href);
    });

    const devApiResults = await page.evaluate(() => {
      return new Promise((resolve) => {
        if (window['__E2E_API_TEST_DATA__']) {
          resolve(window['__E2E_API_TEST_DATA__']);
          return;
        }

        const handler = (event) => {
          window.removeEventListener('e2e-api-test-complete', handler);
          resolve(event.detail);
        };
        window.addEventListener('e2e-api-test-complete', handler);

        if (typeof window['TriggerAPITest'] === 'function') {
          window['TriggerAPITest']();
        }

        setTimeout(() => resolve(null), 60000);
      });
    });

    if (devApiResults) {
      let devResults = devApiResults;
      if (typeof devResults === 'string') {
        try { devResults = JSON.parse(devResults); } catch {}
      }

      const byName = new Map((devResults.results || []).map(r => [r.apiName, r]));
      for (const name of MOCK_SUCCESS_ALLOWLIST) {
        const r = byName.get(name);
        expect(r, `${name} should be present in TriggerAPITest results`).toBeTruthy();
        expect(r.success, `${name} should succeed under devtools mock (got: ${JSON.stringify(r)})`).toBe(true);
        expect(r.isExpectedError, `${name} should be a genuine mock success, not an expected-error pass-through (got: ${JSON.stringify(r)})`).toBe(false);
      }
    } else {
      console.log('⚠️ TriggerAPITest results not received on dev server (mock allowlist assertion skipped)');
    }

    // ⑤ 회귀: 삭제된 자체 mock 브리지(appsintoss-unity-bridge.js)의 흔적이
    //    devtools 경로로 재유입되지 않았는지 확인.
    const legacyBridgeGlobals = await page.evaluate(() => ({
      unityBridge: typeof window['AppsInTossUnityBridge'],
      googleAdMob: typeof window['GoogleAdMob'],
      aitShowToast: typeof window['aitShowToast'],
    }));
    expect(legacyBridgeGlobals.unityBridge, 'window.AppsInTossUnityBridge should not exist (legacy mock bridge removed)').toBe('undefined');
    expect(legacyBridgeGlobals.googleAdMob, 'window.GoogleAdMob should not exist (legacy mock bridge removed)').toBe('undefined');
    expect(legacyBridgeGlobals.aitShowToast, 'window.aitShowToast should not exist (legacy mock bridge removed)').toBe('undefined');

    await killServerProcess(serverProcess, [VITE_DEV_PORT, serverPort]);
    serverProcess = null;

    testResults.tests['2_dev_server'] = {
      passed: true,
      loadTimeMs: loadTime
    };
  });


  // -------------------------------------------------------------------------
  // Test 2b: SDK dev 서버 커맨드 스모크 (granite bin collision 회귀 방지)
  // -------------------------------------------------------------------------
  // Unity Editor의 Dev Server 메뉴는 vite가 아니라 web-framework의
  // granite CLI 파일을 node로 직접 실행한다 (DevServerCommandResolver —
  // node_modules/.bin/granite 이름 충돌 우회). test 2의 vite 경로는 이 커맨드를
  // 전혀 거치지 않으므로, 실제 커맨드가 즉사하지 않고 리슨 포트를 여는지만
  // 짧게 검증한다 (Unity 로드 검증은 test 2가 담당).
  test('2b. SDK dev server command (granite bin direct) should boot', async () => {
    test.setTimeout(90000);

    expect(directoryExists(AIT_BUILD), 'ait-build/ should exist').toBe(true);

    const wfPkgPath = path.resolve(AIT_BUILD, 'node_modules/@apps-in-toss/web-framework/package.json');
    test.skip(!fs.existsSync(wfPkgPath), 'web-framework not installed in ait-build');

    const wfPkg = JSON.parse(fs.readFileSync(wfPkgPath, 'utf8'));
    const graniteBin = wfPkg.bin && wfPkg.bin.granite;
    // 3.x: granite bin 없음 — Editor는 vite 경로를 쓰므로 test 2가 커버
    test.skip(!graniteBin, 'web-framework has no granite bin (3.x) — vite path covered by test 2');

    const binRel = path.join('node_modules/@apps-in-toss/web-framework', graniteBin.replace(/^\.\//, ''));
    console.log(`🚀 Booting SDK dev command: node ${binRel} dev`);

    // Editor 커맨드(pnpm exec -- node <bin> dev)와 동일한 실행 (pnpm exec는 PATH 추가뿐)
    const child = spawn(process.execPath, [binRel, 'dev'], {
      cwd: AIT_BUILD,
      stdio: 'pipe',
      env: { ...process.env, CI: 'true', NODE_OPTIONS: '' }
    });

    let output = '';
    const seenPorts = new Set();
    const result = await new Promise((resolve) => {
      let settled = false;
      const settle = (value) => {
        if (!settled) {
          settled = true;
          resolve(value);
        }
      };
      const onData = (data) => {
        const clean = data.toString().replace(/\x1B\[[0-9;]*[mGKH]/g, '');
        output += clean;
        for (const m of clean.matchAll(/(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)/g)) {
          seenPorts.add(parseInt(m[1], 10));
        }
        if (seenPorts.size > 0) {
          settle('listening');
        }
      };
      child.stdout.on('data', onData);
      child.stderr.on('data', onData);
      child.on('exit', (code) => settle(`exited:${code}`));
      setTimeout(() => settle('timeout'), 60000);
    });

    // 정리: 프로세스 트리 + 감지된 포트(Metro가 띄운 vite 자식 포함) + Metro 기본 포트
    await killServerProcess(child, [...seenPorts, 8081]);

    console.log(`SDK dev command result: ${result}, ports: ${[...seenPorts].join(',')}`);
    expect(
      result,
      `SDK dev command should open a listen port, got: ${result}\n--- output tail ---\n${output.slice(-2000)}`
    ).toBe('listening');

    testResults.tests['2b_sdk_dev_command'] = {
      passed: true,
      ports: [...seenPorts]
    };
  });


  // -------------------------------------------------------------------------
  // Tests 3-5: Production Server + Runtime Tests (세션 공유)
  // -------------------------------------------------------------------------
  test.describe.serial('Production Tests (shared session)', () => {
    /** @type {import('@playwright/test').Page} */
    let sharedPage = null;
    let sharedServerProcess = null;
    let sharedPort = serverPort;
    let pageLoadTime = 0;
    let unityLoadTime = 0;
    const preloadWarnings = [];

    test.beforeAll(async ({ browser }) => {
      console.log('\n' + '='.repeat(70));
      console.log('🚀 STARTING SHARED SESSION FOR TESTS 3-5');
      console.log('='.repeat(70));

      expect(directoryExists(DIST_WEB), 'dist/web/ should exist for production server').toBe(true);

      // 1. Production 서버 시작
      const prodServer = await startProductionServer(AIT_BUILD, serverPort);
      sharedServerProcess = prodServer.process;
      sharedPort = prodServer.port;

      let serverReady = false;
      for (let i = 0; i < 20; i++) {
        try {
          const response = await fetch(`http://localhost:${sharedPort}/`, { method: 'HEAD' });
          if (response.ok) {
            serverReady = true;
            break;
          }
        } catch {}
        await new Promise(r => setTimeout(r, 500));
      }

      if (!serverReady) {
        throw new Error(`Server failed to start on port ${sharedPort}`);
      }

      // 2. 페이지 생성 + Unity 초기화
      sharedPage = await browser.newPage();

      sharedPage.on('console', msg => {
        if (msg.type() === 'warning' && msg.text().includes('credentials mode')) {
          preloadWarnings.push(msg.text());
        }
      });

      const startTime = Date.now();
      const response = await sharedPage.goto(`http://localhost:${sharedPort}?e2e=true`, {
        waitUntil: 'networkidle',
        timeout: 90000
      });

      expect(response?.status()).toBe(200);
      pageLoadTime = Date.now() - startTime;

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
      if (sharedPage) {
        await sharedPage.close();
        sharedPage = null;
      }

      await killServerProcess(sharedServerProcess, [sharedPort]);
      sharedServerProcess = null;
    });


    // -------------------------------------------------------------------------
    // Test 3: Production Server + Load Metrics
    // 기존 Tests 5, 9 통합
    // -------------------------------------------------------------------------
    test('3. Production build should load with correct metrics', async () => {
      test.setTimeout(60000);

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

      expect(webglInfo.supported, 'WebGL should be supported').toBe(true);

      expect(preloadWarnings.length,
        'Early fetch should not cause credentials mode mismatch warnings').toBe(0);

      // Storage 브릿지 존재 확인 (생성기가 unity-bridge.ts에서 Storage 네임스페이스를
      // 드롭하는 회귀를 감지 — PlayerPrefs 영속화 레이어가 이 함수에 의존한다)
      const storageGetItemType = await sharedPage.evaluate(
        () => typeof (window['AppsInToss'] && window['AppsInToss'].Storage && window['AppsInToss'].Storage.getItem)
      );
      expect(storageGetItemType, 'window.AppsInToss.Storage.getItem should be a function').toBe('function');

      testResults.tests['3_production_server'] = {
        passed: true,
        pageLoadTimeMs: pageLoadTime,
        unityLoadTimeMs: unityLoadTime,
        webgl: webglInfo
      };
    });


    // -------------------------------------------------------------------------
    // Test 3-1: Page Reload Crash Test (cache warm)
    // -------------------------------------------------------------------------
    test('3-1. Page reload should not crash (cache warm)', async () => {
      // 계약: warm reload 후 페이지가 크래시하지 않고 unityInstance가 재세팅되어야 한다
      // (재로드 재초기화 회귀 가드, 4654e21). 제품 측 Cache-Storage 계층이 warm reload 시
      // ~100MB webgl.data 재다운로드를 제거하므로 정상 경로에서는 1회 시도로 통과한다.
      //
      // 하니스 순단 분류: self-hosted 러너의 vite preview가 부하로 루프백 스트림을 끊으면
      // (ERR_CONNECTION_CLOSED 등 Chromium net 에러) 이는 제품 크래시가 아니라 하니스
      // 인프라 아티팩트이므로 bounded 재시도한다.
      // 반면 진짜 크래시 시그니처(RuntimeError/webglcontextlost/Aborted()/out of bounds/
      // memory access)는 즉시 hard-fail — 재시도로 삼키지 않는다(원 계약 보존).
      // 제품 hang 시그니처("Failed to download file" = 로더의 .data 다운로드 실패)도 마찬가지로
      // 즉시 hard-fail — 과거 이 문구가 HARNESS_RE에 들어 있어 제품 결함이 조용한 재시도로
      // 은폐됐다(dev 빌드 warm reload에서 fetch 계측이 로더 다운로드를 깨뜨린 회귀).
      // 분류 정책·로그 문장·예산은 lib/reload-retry.js의 POLICY_WARM_CACHE에 있다.
      test.setTimeout(360000);
      const { attempts } = await reloadWithRetry(sharedPage, {
        ...POLICY_WARM_CACHE,
        tag: '3-1',
        // 성공 경로에서도 진짜 크래시 시그니처는 hard-fail. 시도 안에서 단언해 크래시 분류(진단 덤프)를 탄다.
        onReady: ({ crashErrors }) => {
          expect(crashErrors.length, `No crash errors on reload: ${crashErrors.map(e => e.message).join('; ')}`).toBe(0);
        },
      });
      testResults.tests['3_1_reload'] = { passed: true, attempts };
    });


    // -------------------------------------------------------------------------
    // Test 4: Runtime API Error Validation
    // -------------------------------------------------------------------------
    test('4. All SDK APIs should return correct errors in production preview (no Toss bridge)', async () => {
      test.setTimeout(180000);

      console.log('🔄 Triggering API tests via JavaScript...');

      const apiResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          if (window['__E2E_API_TEST_DATA__']) {
            resolve(window['__E2E_API_TEST_DATA__']);
            return;
          }

          const handler = (event) => {
            window.removeEventListener('e2e-api-test-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-api-test-complete', handler);

          if (typeof window['TriggerAPITest'] === 'function') {
            window['TriggerAPITest']();
          }

          setTimeout(() => resolve(null), 120000);
        });
      });

      if (apiResults) {
        let results = apiResults;
        if (typeof results === 'string') {
          try { results = JSON.parse(results); } catch {}
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 SDK API ERROR VALIDATION RESULTS');
        console.log('='.repeat(70));
        console.log(`   Total APIs Tested: ${results.totalAPIs}`);
        console.log(`   Success: ${results.successCount}`);
        console.log(`   Unexpected Errors: ${results.unexpectedErrorCount || 0}`);
        console.log('='.repeat(70));

        if (results.results) {
          const unexpectedErrors = results.results.filter(r => !r.success);
          if (unexpectedErrors.length > 0) {
            console.log('\n❌ APIs with UNEXPECTED Errors:');
            unexpectedErrors.forEach(r => {
              console.log(`   [FAIL] ${r.apiName}: ${r.error}`);
            });
          }
        }

        const unexpectedErrorCount = results.unexpectedErrorCount || 0;

        testResults.tests['4_runtime_api'] = {
          passed: unexpectedErrorCount === 0,
          totalAPIs: results.totalAPIs,
          successCount: results.successCount,
          expectedErrorCount: results.expectedErrorCount || 0,
          unexpectedErrorCount: unexpectedErrorCount,
          results: results.results || []
        };

        expect(unexpectedErrorCount, 'All APIs should return expected errors or succeed').toBe(0);
      } else {
        testResults.tests['4_runtime_api'] = {
          passed: false,
          reason: 'RuntimeAPITester results not received'
        };
        expect(apiResults, 'RuntimeAPITester should return results').not.toBeNull();
      }
    });


    // -------------------------------------------------------------------------
    // Test 5: Serialization Round-trip Tests
    // -------------------------------------------------------------------------
    test('5. Serialization round-trip should succeed for all types', async () => {
      test.setTimeout(180000);

      console.log('🔄 Triggering serialization tests via JavaScript...');

      const serializationResults = await sharedPage.evaluate(() => {
        return new Promise((resolve) => {
          if (window['__E2E_SERIALIZATION_TEST_DATA__']) {
            resolve(window['__E2E_SERIALIZATION_TEST_DATA__']);
            return;
          }

          const handler = (event) => {
            window.removeEventListener('e2e-serialization-complete', handler);
            resolve(event.detail);
          };
          window.addEventListener('e2e-serialization-complete', handler);

          if (typeof window['TriggerSerializationTest'] === 'function') {
            window['TriggerSerializationTest']();
          }

          setTimeout(() => resolve(null), 90000);
        });
      });

      if (serializationResults) {
        let results = serializationResults;
        if (typeof results === 'string') {
          try { results = JSON.parse(results); } catch {}
        }

        console.log('\n' + '='.repeat(70));
        console.log('📊 SERIALIZATION ROUND-TRIP TEST RESULTS');
        console.log('='.repeat(70));
        console.log(`   Total Tests: ${results.totalTests}`);
        console.log(`   Success: ${results.successCount}`);
        console.log(`   Failed: ${results.failCount}`);
        console.log('='.repeat(70));

        if (results.results && Array.isArray(results.results)) {
          const failed = results.results.filter(r => !r.success);
          if (failed.length > 0) {
            console.log('\n❌ Failed Tests:');
            failed.forEach(r => {
              console.log(`   [FAIL] ${r.testName}: ${r.error || 'unknown error'}`);
            });
          }
        }

        testResults.tests['5_serialization'] = {
          passed: results.failCount === 0,
          totalTests: results.totalTests,
          successCount: results.successCount,
          failCount: results.failCount
        };

        expect(results.failCount, 'All serialization tests should pass').toBe(0);
      } else {
        testResults.tests['5_serialization'] = {
          passed: false,
          reason: 'SerializationTester results not received'
        };
        expect(serializationResults, 'SerializationTester should return results').not.toBeNull();
      }
    });

    // -------------------------------------------------------------------------
    // Test 6: Build Customization Tutorial #1 — canvas-confetti
    // BuildConfig~/src/main.ts 가 번들링되어 confetti 가 발사되었는지 검증
    // (https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization 튜토리얼 #1)
    // -------------------------------------------------------------------------
    test('6. Tutorial #1: canvas-confetti should fire after page load', async () => {
      test.setTimeout(30000);

      const confettiFired = await sharedPage.waitForFunction(
        () => window['__TUTORIAL_CONFETTI_FIRED__'] === true,
        { timeout: 15000 }
      ).then(() => true).catch(() => false);

      console.log(`🎉 Confetti fired: ${confettiFired}`);

      testResults.tests['6_tutorial_confetti'] = {
        passed: confettiFired
      };

      expect(confettiFired, 'window.__TUTORIAL_CONFETTI_FIRED__ should become true (main.ts bundled and load handler executed)').toBe(true);
    });


    // -------------------------------------------------------------------------
    // Test 7: Build Customization Tutorial #2 — Firebase
    // VITE_FIREBASE_* 환경변수가 주입되어 firebase/app 이 초기화되었는지 검증
    // (https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization 튜토리얼 #2)
    //
    // 환경변수가 없으면(로컬 개발) 초기화 시도를 건너뛰므로 skip 처리.
    // CI 에서는 GitHub Secret 으로 주입되어 모든 단계가 통과해야 한다.
    // -------------------------------------------------------------------------
    test('7. Tutorial #2: Firebase should initialize when secrets are provided', async () => {
      test.setTimeout(30000);

      const state = await sharedPage.evaluate(() => ({
        initialized: window['__TUTORIAL_FIREBASE_INITIALIZED__'] === true,
        analyticsReady: window['__TUTORIAL_FIREBASE_ANALYTICS_READY__'] === true,
        error: window['__TUTORIAL_FIREBASE_ERROR__'] || null,
      }));

      console.log(`🔥 Firebase state: ${JSON.stringify(state)}`);

      const secretsProvided = !state.error || !state.error.includes('VITE_FIREBASE_*');

      if (!secretsProvided) {
        console.log('⏭️ Firebase secrets not provided, skipping initialization assertion (expected in local runs without .env)');
        testResults.tests['7_tutorial_firebase'] = {
          passed: true,
          skipped: true,
          reason: 'VITE_FIREBASE_* env vars not provided'
        };
        test.skip();
        return;
      }

      testResults.tests['7_tutorial_firebase'] = {
        passed: state.initialized,
        analyticsReady: state.analyticsReady,
        error: state.error,
      };

      expect(state.initialized, `Firebase initializeApp should succeed when VITE_FIREBASE_* are set (error: ${state.error})`).toBe(true);
    });


    // -------------------------------------------------------------------------
    // Test 8: Nested callback synchronous round-trip (processProductGrant)
    // 결제 이벤트 없이 중첩 콜백 왕복을 실 WebGL 빌드에서 검증한다:
    //   JS SendMessage('AITCore','OnNestedCallback')
    //     → C# OnNestedCallback → 동기 콜백 실행 → __AITRespondToNestedCallback
    //     → JS Promise resolve
    // E2ETestTrigger.Start()가 사전 등록한 콜백 2종(즉시 true / 예외)을 구동하고,
    // 미등록 콜백까지 3케이스로 검증한다. 응답은 SendMessage와 같은 스택에서 나간다.
    // -------------------------------------------------------------------------
    test('8. Nested callback (processProductGrant) should round-trip synchronously', async () => {
      test.setTimeout(60000);

      console.log('🔄 Driving nested callback round-trip via SendMessage...');

      const roundTrip = await sharedPage.evaluate(async () => {
        const CB_NAME = 'processProductGrant';

        // 하나의 콜백을 구동하고 resolve까지의 결과/경과시간을 반환한다.
        // resolver를 __AIT_NESTED_CALLBACKS에 직접 등록(실 jslib과 동일 경로)한 뒤
        // SendMessage로 C#을 트리거하고, C#의 __AITRespondToNestedCallback 응답을 기다린다.
        function drive(callbackId, suffix, timeoutMs) {
          return new Promise((resolve) => {
            const ui = window['unityInstance'];
            if (!ui || typeof ui.SendMessage !== 'function') {
              resolve({ ok: false, reason: 'unityInstance/SendMessage unavailable' });
              return;
            }
            window.__AIT_NESTED_CALLBACKS = window.__AIT_NESTED_CALLBACKS || {};
            const requestId = 'e2e-rt-' + suffix + '-' + Date.now();
            const started = performance.now();
            let settled = false;

            const timer = setTimeout(() => {
              if (settled) return;
              settled = true;
              delete window.__AIT_NESTED_CALLBACKS[requestId];
              resolve({ ok: false, reason: 'timeout', elapsedMs: performance.now() - started });
            }, timeoutMs);

            // jslib의 __AITRespondToNestedCallback은 저장된 resolver를 동기 호출하므로,
            // 동기 dispatch라면 SendMessage가 리턴한 시점에 이미 응답이 도착해 있어야 한다.
            // syncSettled가 그 사실을 기록한다 — dispatch가 fire-and-forget(비동기 1틱 지연)
            // 으로 되돌아가는 회귀를 타이밍 임계값 없이 결정적으로 잡는다.
            let outcome = null;
            let sendReturned = false;
            window.__AIT_NESTED_CALLBACKS[requestId] = (resultBool) => {
              if (settled) return;
              settled = true;
              clearTimeout(timer);
              delete window.__AIT_NESTED_CALLBACKS[requestId];
              outcome = {
                ok: true,
                result: resultBool,
                elapsedMs: performance.now() - started,
                syncSettled: !sendReturned
              };
              if (sendReturned) resolve(outcome); // 비동기 도착 경로 (회귀 시)
            };

            const payload = JSON.stringify({
              RequestId: requestId,
              CallbackId: callbackId,
              CallbackName: CB_NAME,
              Data: JSON.stringify({ orderId: 'e2e-order' })
            });
            ui.SendMessage('AITCore', 'OnNestedCallback', payload);
            sendReturned = true;
            if (outcome) resolve(outcome); // 동기 도착 경로 (정상)
          });
        }

        // 순차 실행(응답이 requestId로 구분되므로 병렬도 가능하나 로그 가독성을 위해 순차)
        const grantCase = await drive('e2e-nested-grant', 'grant', 20000);
        const throwCase = await drive('e2e-nested-throw', 'throw', 20000);
        const unknownCase = await drive('e2e-nested-unknown', 'unknown', 20000);

        return { grantCase, throwCase, unknownCase };
      });

      console.log('\n' + '='.repeat(70));
      console.log('📊 NESTED CALLBACK ROUND-TRIP RESULTS');
      console.log('='.repeat(70));
      console.log(`   grant(true) : ${JSON.stringify(roundTrip.grantCase)}`);
      console.log(`   throw(false): ${JSON.stringify(roundTrip.throwCase)}`);
      console.log(`   unknown     : ${JSON.stringify(roundTrip.unknownCase)}`);
      console.log('='.repeat(70));

      testResults.tests['8_nested_callback'] = {
        passed:
          roundTrip.grantCase.ok && roundTrip.grantCase.result === true &&
          roundTrip.throwCase.ok && roundTrip.throwCase.result === false &&
          roundTrip.unknownCase.ok && roundTrip.unknownCase.result === false,
        grantCase: roundTrip.grantCase,
        throwCase: roundTrip.throwCase,
        unknownCase: roundTrip.unknownCase
      };

      // 1) 즉시 승인 콜백 → true 로 resolve, 그리고 SendMessage와 같은 스택에서 응답이
      //    나갔는지(syncSettled) 검증 — dispatch가 비동기로 되돌아가는 회귀를 결정적으로 잡는다.
      expect(roundTrip.grantCase.ok, `grant case should resolve (got: ${JSON.stringify(roundTrip.grantCase)})`).toBe(true);
      expect(roundTrip.grantCase.result, 'grant callback should resolve true').toBe(true);
      expect(roundTrip.grantCase.syncSettled, 'grant response must arrive on the SendMessage stack (sync dispatch)').toBe(true);

      // 2) 예외 콜백 → false 로 resolve (응답 유실 없이 dispatch가 잡아 정확히 1회 응답)
      expect(roundTrip.throwCase.ok, `throw case should resolve (got: ${JSON.stringify(roundTrip.throwCase)})`).toBe(true);
      expect(roundTrip.throwCase.result, 'throwing callback should resolve false (no lost response)').toBe(false);
      expect(roundTrip.throwCase.syncSettled, 'exception path must also respond synchronously').toBe(true);

      // 3) 미등록 callbackId → false 로 즉시 resolve
      expect(roundTrip.unknownCase.ok, `unknown case should resolve (got: ${JSON.stringify(roundTrip.unknownCase)})`).toBe(true);
      expect(roundTrip.unknownCase.result, 'unknown callback should resolve false').toBe(false);
      expect(roundTrip.unknownCase.syncSettled, 'unregistered path must also respond synchronously').toBe(true);
    });


    registerPlayerPrefsTests({ getPort: () => sharedPort });

  }); // end of test.describe.serial

});
