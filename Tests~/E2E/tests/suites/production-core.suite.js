// @ts-check
// 이 디렉터리 파일은 *.test.js 이름 금지(playwright 기본 testMatch에 잡힘)
import { test, expect } from '@playwright/test';
import { reloadWithRetry, POLICY_WARM_CACHE } from '../lib/reload-retry.js';
import { testResults } from '../lib/results.js';

/**
 * 테스트 3, 3-1, 4, 5를 등록한다. 공유 세션(서버 기동, Unity 초기화)은
 * 진입 파일의 test.describe.serial('Production Tests (shared session)') 훅이
 * 관리하고, 여기서는 ctx.session을 통해서만 읽는다.
 * @param {{ session: {
 *   page: import('@playwright/test').Page,
 *   pageLoadTime: number,
 *   unityLoadTime: number,
 *   preloadWarnings: string[],
 * } }} ctx
 */
export function registerProductionCoreTests(ctx) {

  // -------------------------------------------------------------------------
  // Test 3: Production Server + Load Metrics
  // 기존 Tests 5, 9 통합
  // -------------------------------------------------------------------------
  test('3. Production build should load with correct metrics', async () => {
    test.setTimeout(60000);

    // WebGL 지원 확인
    const webglInfo = await ctx.session.page.evaluate(() => {
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
    console.log(`⏱️ Page load: ${ctx.session.pageLoadTime}ms, Unity load: ${ctx.session.unityLoadTime}ms`);

    expect(webglInfo.supported, 'WebGL should be supported').toBe(true);

    expect(ctx.session.preloadWarnings.length,
      'Early fetch should not cause credentials mode mismatch warnings').toBe(0);

    // Storage 브릿지 존재 확인 (생성기가 unity-bridge.ts에서 Storage 네임스페이스를
    // 드롭하는 회귀를 감지 — PlayerPrefs 영속화 레이어가 이 함수에 의존한다)
    const storageGetItemType = await ctx.session.page.evaluate(
      () => typeof (window['AppsInToss'] && window['AppsInToss'].Storage && window['AppsInToss'].Storage.getItem)
    );
    expect(storageGetItemType, 'window.AppsInToss.Storage.getItem should be a function').toBe('function');

    testResults.tests['3_production_server'] = {
      passed: true,
      pageLoadTimeMs: ctx.session.pageLoadTime,
      unityLoadTimeMs: ctx.session.unityLoadTime,
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
    const { attempts } = await reloadWithRetry(ctx.session.page, {
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

    const apiResults = await ctx.session.page.evaluate(() => {
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

    const serializationResults = await ctx.session.page.evaluate(() => {
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

}
