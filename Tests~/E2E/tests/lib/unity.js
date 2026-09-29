// @ts-check
// e2e-full-pipeline.test.js에서 추출한 Unity 로드/재로드 유틸리티. 함수 본문은 원본과 동일하다.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import { isMobileEmulation, cpuThrottleRate } from './env.js';

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
 * reload → unityInstance 재설정까지를 3-1과 동일한 하니스 순단 분류로 감싼 재시도 헬퍼.
 *
 * self-hosted 러너의 vite preview가 부하로 루프백 스트림을 끊으면(ERR_CONNECTION_CLOSED /
 * "Failed to download file" / download-watchdog) 제품 결함이 아니라 인프라 아티팩트이므로
 * bounded 재시도한다. 진짜 크래시 시그니처(RuntimeError/webglcontextlost/Aborted()/
 * out of bounds/memory access)는 재시도로 삼키지 않고 즉시 hard-fail (3-1과 동일 계약).
 * run 31581794167 rerun2에서 9-4의 reload 부트가 단발 drop으로 죽은 실측에 따른 보강.
 *
 * unityInstance 대기는 벽시계-바운드 폴링이다 — 제품 워치독의 location.reload() 루프를
 * 만나면 Playwright waitForFunction은 navigation마다 re-arm되어 자체 timeout을 무시하고
 * test.setTimeout 예산 전체를 소진한다(3-1 주석 및 rerun2의 9-4 180초 소진으로 실측).
 */
export async function reloadAndWaitForUnity(page, tag, { maxAttempts = 3, bootBudgetMs = 75000 } = {}) {
  const CRASH_RE = /webglcontextlost|Aborted\(|RuntimeError|out of bounds|memory access/i;
  const HARNESS_RE = /ERR_CONNECTION_CLOSED|Failed to download file|download-watchdog/i;

  const pageErrors = [];
  const failedRequests = [];
  const consoleLines = [];
  const errHandler = (err) => pageErrors.push(String((err && err.message) || err));
  const reqFailedHandler = (req) => {
    try {
      failedRequests.push(`${req.url().split('/').slice(-2).join('/')} :: ${req.failure()?.errorText || '?'}`);
    } catch (e) {}
  };
  const consoleHandler = (msg) => consoleLines.push(msg.text());
  page.on('pageerror', errHandler);
  page.on('requestfailed', reqFailedHandler);
  page.on('console', consoleHandler);

  try {
    let lastErr = null;
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      pageErrors.length = 0; failedRequests.length = 0; consoleLines.length = 0;
      if (attempt > 1) {
        // 제품 측 재로드 워치독 카운터와 캐시 우회 플래그를 리셋해 재시도 reload가
        // 새 예산 + 워밍된 Cache-Storage로 부트하게 한다 (3-1과 동일).
        try {
          await page.evaluate(() => {
            try { sessionStorage.removeItem('__ait_reload_count__'); } catch (e) {}
            try { sessionStorage.removeItem('__ait_skip_data_cache__'); } catch (e) {}
          });
        } catch (e) {}
      }
      const t0 = Date.now();
      try {
        const resp = await page.reload({ waitUntil: 'domcontentloaded', timeout: 45000 });
        if (!resp || resp.status() !== 200) {
          throw new Error(`reload status=${resp ? resp.status() : 'null'}`);
        }
        const deadline = Date.now() + bootBudgetMs;
        let ready = false;
        while (Date.now() < deadline) {
          try {
            ready = await page.evaluate(() => window['unityInstance'] !== undefined);
            if (ready) break;
          } catch (e) {
            // 재로드 루프 중 컨텍스트 파괴는 계속 폴링, 페이지가 닫혔으면 fatal
            if (/has been closed|Target closed/.test(e.message || '')) throw e;
          }
          await new Promise((r) => setTimeout(r, 1000));
        }
        if (!ready) throw new Error(`unityInstance not set within ${bootBudgetMs}ms budget`);
        if (attempt > 1) console.log(`[${tag}] reload recovered on attempt ${attempt}/${maxAttempts}`);
        return;
      } catch (err) {
        lastErr = err;
        const crash = pageErrors.some((m) => CRASH_RE.test(m));
        const drop = failedRequests.some((f) => HARNESS_RE.test(f)) || consoleLines.some((l) => HARNESS_RE.test(l));
        console.log(`[${tag}] reload attempt ${attempt}/${maxAttempts} FAILED after ${Date.now() - t0}ms: ` +
          `${err.message} (crash=${crash}, drop=${drop}; requestfailed=${failedRequests.slice(0, 5).join(' | ')})`);
        if (crash) throw err; // 진짜 크래시 — 재시도로 삼키지 않음
        if (/has been closed|Target closed/.test(err.message || '')) throw err; // 재시도 불가
        if (attempt < maxAttempts && drop) {
          console.log(`[${tag}] harness connection-drop classified — retrying reload`);
          continue;
        }
        throw err; // 소진 또는 미분류
      }
    }
    throw lastErr;
  } finally {
    page.off('pageerror', errHandler);
    page.off('requestfailed', reqFailedHandler);
    page.off('console', consoleHandler);
  }
}
