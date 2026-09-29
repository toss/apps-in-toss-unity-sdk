// @ts-check
// e2e-full-pipeline.test.js에서 추출한 Unity 로드/재로드 유틸리티.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import { isMobileEmulation, cpuThrottleRate } from './env.js';
import { reloadWithRetry, POLICY_ISOLATED_PAGE } from './reload-retry.js';

export async function applyMobileThrottling(page, overrideRate = undefined) {
  const rate = overrideRate !== undefined ? overrideRate :
               (isMobileEmulation ? 4 : cpuThrottleRate);

  if (rate <= 0 && !isMobileEmulation) {
    return null;
  }

  const client = await page.context().newCDPSession(page);

  if (rate > 0) {
    await client.send('Emulation.setCPUThrottlingRate', { rate });
  }

  if (isMobileEmulation) {
    await client.send('Network.emulateNetworkConditions', {
      offline: false,
      downloadThroughput: 12 * 1024 * 1024 / 8,
      uploadThroughput: 6 * 1024 * 1024 / 8,
      latency: 70
    });
  }

  return client;
}

/**
 * window.unityInstance가 세팅될 때까지 대기 (신규 격리 page용 헬퍼).
 * 기존 shared-session beforeAll의 인라인 폴링과 동일한 조건.
 */
export async function waitForUnityInstance(page, timeoutMs = 60000) {
  await page.waitForFunction(() => window['unityInstance'] !== undefined, { timeout: timeoutMs });
}

/**
 * 9-2/9-4/9-7(격리 page)의 reload → unityInstance 재설정 재시도. 루프와 분류는
 * reload-retry.js의 reloadWithRetry + POLICY_ISOLATED_PAGE이고, 이 함수는 기존 시그니처를
 * 유지하는 얇은 래퍼다.
 */
export async function reloadAndWaitForUnity(page, tag, {
  maxAttempts = POLICY_ISOLATED_PAGE.maxAttempts,
  bootBudgetMs = POLICY_ISOLATED_PAGE.budgetMs,
} = {}) {
  await reloadWithRetry(page, { ...POLICY_ISOLATED_PAGE, tag, maxAttempts, budgetMs: bootBudgetMs });
}
