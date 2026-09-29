// @ts-check
// 이 디렉터리 파일은 *.test.js 이름 금지(playwright 기본 testMatch에 잡힘)
import { test, expect } from '@playwright/test';
import { spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import {
  AIT_BUILD,
  DIST_WEB,
  isDevBuild,
  BENCHMARKS,
  VITE_DEV_PORT,
  SERVER_PORT as serverPort,
} from '../lib/env.js';
import { directoryExists, fileExists, getDirectorySizeMB, checkForPlaceholders } from '../lib/fs-utils.js';
import { killServerProcess, startDevServer } from '../lib/server.js';
import { applyMobileThrottling } from '../lib/unity.js';
import { testResults } from '../lib/results.js';

/**
 * 테스트 1, 2, 2b를 등록한다.
 * @param {{ devServer: { process: import('child_process').ChildProcess | null } }} ctx
 *   테스트 2가 띄우는 dev 서버 프로세스 핸들. 진입 파일의 외곽 afterAll이
 *   비상 정리(테스트가 중간에 실패해 killServerProcess를 못 탄 경우)에 참조한다.
 */
export function registerBuildAndDevServerTests(ctx) {

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
    const devServerHandle = await startDevServer(AIT_BUILD, serverPort, { AIT_DEVTOOLS: '1' });
    ctx.devServer.process = devServerHandle.process;
    const actualPort = devServerHandle.port;

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

    await killServerProcess(ctx.devServer.process, [VITE_DEV_PORT, serverPort]);
    ctx.devServer.process = null;

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

}
