// @ts-check
// 이 디렉터리 파일은 *.test.js 이름 금지(playwright 기본 testMatch에 잡힘)
import { test, expect } from '@playwright/test';
import { testResults } from '../lib/results.js';

/**
 * 테스트 6, 7(Build Customization 튜토리얼)을 등록한다. 공유 세션(서버 기동, Unity
 * 초기화)은 진입 파일의 test.describe.serial('Production Tests (shared session)')
 * 훅이 관리하고, 여기서는 ctx.session.page만 읽는다.
 * @param {{ session: { page: import('@playwright/test').Page } }} ctx
 */
export function registerTutorialTests(ctx) {

  // -------------------------------------------------------------------------
  // Test 6: Build Customization Tutorial #1 — canvas-confetti
  // BuildConfig~/src/main.ts 가 번들링되어 confetti 가 발사되었는지 검증
  // (https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization 튜토리얼 #1)
  // -------------------------------------------------------------------------
  test('6. Tutorial #1: canvas-confetti should fire after page load', async () => {
    test.setTimeout(30000);

    const confettiFired = await ctx.session.page.waitForFunction(
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

    const state = await ctx.session.page.evaluate(() => ({
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

}
