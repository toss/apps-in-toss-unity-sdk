// @ts-check
// 이 디렉터리 파일은 *.test.js 이름 금지(playwright 기본 testMatch에 잡힘)
import { test, expect } from '@playwright/test';
import { testResults } from '../lib/results.js';

/**
 * 테스트 8(중첩 콜백 동기 왕복)을 등록한다. 공유 세션(서버 기동, Unity 초기화)은
 * 진입 파일의 test.describe.serial('Production Tests (shared session)') 훅이
 * 관리하고, 여기서는 ctx.session.page만 읽는다.
 * @param {{ session: { page: import('@playwright/test').Page } }} ctx
 */
export function registerNestedCallbackTests(ctx) {

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

    const roundTrip = await ctx.session.page.evaluate(async () => {
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

}
