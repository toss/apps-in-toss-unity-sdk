// @ts-check
// reload → unityInstance 재설정 대기를 하니스 순단 분류로 감싼 재시도 루프.
// 3-1(공유 세션 warm reload)과 9-x(격리 page, reloadAndWaitForUnity)가 같은 루프를 쓰고,
// 두 호출부의 분류 정책과 로그 문장은 프리셋(POLICY_WARM_CACHE / POLICY_ISOLATED_PAGE)에 고정한다.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import { expect } from '@playwright/test';

/**
 * 진짜 크래시 시그니처. 두 정책 모두 재시도로 삼키지 않고 즉시 hard-fail한다(원 계약 보존).
 */
export const CRASH_RE = /webglcontextlost|Aborted\(|RuntimeError|out of bounds|memory access/i;

// 페이지/컨텍스트가 닫혔다는 Playwright 에러 문구. 재시도 불가(fatal).
const CLOSED_RE = /has been closed|Target closed/;

// 3-1 제품 hang 지문의 동반 pageerror("...reading 'subarray'"). 진단용으로만 수집한다(판정 조건 아님 —
// 로더가 실패를 삼켜 subarray 예외 없이 조용히 매다는 변종도 있기 때문).
const HANG_PAGEERROR_RE = /reading ['"]subarray['"]/i;

// 3-1 시도당 unityInstance 대기 예산. 근거(실측): 성공 경로는 warm reload 후 1.1~5.8초에
// unityInstance가 재설정되고, 실패(제품 hang) 경로는 3.5초 안에 "Failed to download file"로
// 확정된다. 25s면 성공 상한의 ~4배 마진이라 느린 러너에서도 오탐하지 않으면서, 예전 75s처럼
// 실패 케이스에서 시도당 1분 이상을 버리지 않는다.
export const UNITY_WAIT_BUDGET_MS = 25000;

/**
 * 벽시계-바운드 unityInstance 폴링. Playwright waitForFunction은 제품 워치독의
 * location.reload() 루프를 만나면 자체 timeout을 무시하고 navigation마다 re-arm되어
 * test.setTimeout 예산 전체를 소진한다(관측: 3-1에서 90s 지정에도 363s 실행, 9-4 rerun2에서 180초 소진).
 * 이 헬퍼는 벽시계 deadline으로 시도별 예산을 실제로 강제하고, navigation 중 evaluate
 * 예외("context destroyed")를 삼켜 재로드 루프에 견딘다. 페이지가 닫히면 { closed: true }와
 * 원래 에러(error)를 돌려준다.
 *
 * abortIf: 매 폴링 사이클 앞에서 평가되는 조기 종료 술어(제품 hang 지문 관측 등).
 * 예산을 끝까지 태우지 않고 즉시 { aborted: true }로 빠져나온다.
 *
 * @param {import('@playwright/test').Page} page
 * @param {number} budgetMs
 * @param {{ abortIf?: () => boolean }} [options]
 * @returns {Promise<{ ready: boolean, aborted?: boolean, closed?: boolean, evalThrows: number, error?: any }>}
 */
export async function waitForUnityBounded(page, budgetMs, { abortIf } = {}) {
  const deadline = Date.now() + budgetMs;
  let evalThrows = 0;
  while (Date.now() < deadline) {
    if (abortIf && abortIf()) return { ready: false, aborted: true, evalThrows };
    try {
      const ready = await page.evaluate(
        () => typeof window !== 'undefined' && window['unityInstance'] !== undefined);
      if (ready) return { ready: true, evalThrows };
    } catch (e) {
      evalThrows++; // 재로드 중 컨텍스트 파괴 등 — 계속 폴링.
      if (CLOSED_RE.test(e.message || '')) {
        return { ready: false, closed: true, evalThrows, error: e };
      }
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return { ready: false, evalThrows };
}

/**
 * 9-2/9-4/9-7(격리 page, reloadAndWaitForUnity) 정책.
 *
 * self-hosted 러너의 vite preview가 부하로 루프백 스트림을 끊으면(ERR_CONNECTION_CLOSED /
 * "Failed to download file" / download-watchdog) 제품 결함이 아니라 인프라 아티팩트로 보고
 * bounded 재시도한다. run 31581794167 rerun2에서 9-4의 reload 부트가 단발 drop으로 죽은
 * 실측에 따른 보강. 3-1과 달리 "Failed to download file"을 아직 하니스 순단으로 분류한다
 * (정렬 여부는 TODO.md 참조).
 */
export const POLICY_ISOLATED_PAGE = Object.freeze({
  maxAttempts: 3,
  budgetMs: 75000,
  harnessRe: /ERR_CONNECTION_CLOSED|Failed to download file|download-watchdog/i,
  productHangRe: null,
  collectBuildResponses: false,
  /** @param {any} err */
  toPageError: (err) => ({ message: String((err && err.message) || err) }),
  /** @param {import('@playwright/test').ConsoleMessage} msg */
  toConsoleLine: (msg) => msg.text(),
  forwardConsoleLine: null,
  /**
   * @param {import('@playwright/test').Page} page
   * @param {import('@playwright/test').Response | null} resp
   */
  async checkReload(page, resp) {
    if (!resp || resp.status() !== 200) {
      throw new Error(`reload status=${resp ? resp.status() : 'null'}`);
    }
  },
  dumpDiag: null,
  format: Object.freeze({
    /** @param {any} c */
    attemptFailed: (c) =>
      `[${c.tag}] reload attempt ${c.attempt}/${c.maxAttempts} FAILED after ${c.elapsedMs}ms: ` +
      `${c.err.message} (crash=${c.crash}, drop=${c.drop}; requestfailed=${c.failedRequests.slice(0, 5).join(' | ')})`,
    /** @param {any} c */
    ready: (c) => (c.attempt > 1 ? `[${c.tag}] reload recovered on attempt ${c.attempt}/${c.maxAttempts}` : null),
    // 페이지가 닫혔으면 폴링에서 받은 원래 에러를 그대로 던진다.
    /** @param {any} c */
    notReadyError: (c) => (c.res.closed ? c.res.error : new Error(`unityInstance not set within ${c.budgetMs}ms budget`)),
    crash: null,
    productHang: null,
    productHangError: null,
    closed: null,
    /** @param {any} c */
    retry: (c) => `[${c.tag}] harness connection-drop classified — retrying reload`,
  }),
});

/**
 * 3-1(공유 세션 warm reload) 정책.
 *
 * HARNESS_RE(harnessRe)는 하니스 전용 패턴만 남긴다: 아래 넷은 모두 Chromium이 requestfailed에
 * 싣는 전송 계층 순단(루프백 스트림 끊김/서버 종료)으로, 제품 코드가 절대 만들어내지 않는 문구다.
 * ERR_INCOMPLETE_CHUNKED_ENCODING: vite preview는 .data를 chunked로 서빙하므로 본문 스트리밍 중
 * 끊기면 Chromium이 CLOSED/RESET 대신 이 코드를 보고한다.
 * 제거된 것: "Failed to download file"(Unity 로더의 제품 결함 지문) 및 "download-watchdog"
 * (그 실패를 받은 제품 워치독의 진단 마커 — 즉 같은 제품 결함).
 *
 * PRODUCT_HANG_RE(productHangRe): 로더가 .data 다운로드 실패 시 남기는 유일한 콘솔 신호.
 * 관측되면 예산 소진을 기다리지 않고 대기를 중단하고, net 에러가 없으면 재시도 없이 hard-fail한다.
 */
export const POLICY_WARM_CACHE = Object.freeze({
  maxAttempts: 3,
  budgetMs: UNITY_WAIT_BUDGET_MS,
  harnessRe: /ERR_CONNECTION_CLOSED|ERR_CONNECTION_RESET|ERR_EMPTY_RESPONSE|ERR_INCOMPLETE_CHUNKED_ENCODING/i,
  productHangRe: /Failed to download file/i,
  // Build/* 응답 상태 관측 — 데이터가 캐시 서빙됐는지/재다운로드 됐는지 확인.
  collectBuildResponses: true,
  /** @param {any} err */
  toPageError: (err) => ({ message: err.message, stack: err.stack }),
  /** @param {import('@playwright/test').ConsoleMessage} msg */
  toConsoleLine: (msg) => `[${msg.type()}] ${msg.text()}`,
  // 제품 캐시 계층 마커를 CI stdout으로 즉시 포워딩(콜드 워밍/재로드 HIT·MISS 진단).
  /** @param {string} line */
  forwardConsoleLine: (line) => {
    if (line.indexOf('[AIT] cache:') !== -1) console.log(`  (page) ${line}`);
  },
  /**
   * @param {import('@playwright/test').Page} page
   * @param {import('@playwright/test').Response | null} resp
   * @param {{ tag: string, attempt: number, maxAttempts: number, t0: number }} c
   */
  async checkReload(page, resp, c) {
    console.log(`[${c.tag}] attempt ${c.attempt}/${c.maxAttempts} reload status=${resp?.status()} after ${Date.now() - c.t0}ms`);
    expect(resp?.status()).toBe(200);

    const navType = await page.evaluate(() => {
      try {
        const e = performance.getEntriesByType('navigation')[0];
        return e ? e.type : (performance.navigation && performance.navigation.type);
      } catch (e) { return 'unknown'; }
    }).catch(() => 'unknown');
    console.log(`[${c.tag}] navigation type=${navType}`);
  },
  /** @param {any} c */
  dumpDiag: (c) => {
    console.log(`[${c.tag}] pageerrors(${c.pageErrors.length}):`);
    c.pageErrors.forEach((/** @type {any} */ e, /** @type {number} */ i) => {
      console.log(`  #${i}: ${e.message}`);
      if (e.stack && e.stack !== e.message) console.log(`     stack: ${e.stack.split('\n').slice(0, 4).join(' | ')}`);
    });
    console.log(`[${c.tag}] requestfailed(${c.failedRequests.length}): ${c.failedRequests.join(' | ')}`);
    console.log(`[${c.tag}] Build/* responses(${c.buildResponses.length}): ${c.buildResponses.join(' | ')}`);
    const spam = /still waiting on run dependencies|dependency: dataUrl|\(end of list\)/;
    const signal = c.consoleLines.filter((/** @type {string} */ l) => !spam.test(l));
    console.log(`[${c.tag}] ${c.reason} console total=${c.consoleLines.length}, signal=${signal.length}`);
    console.log(`[${c.tag}] --- signal head (first 80) ---\n${signal.slice(0, 80).join('\n')}`);
    if (signal.length > 110) console.log(`[${c.tag}] --- signal tail (last 30) ---\n${signal.slice(-30).join('\n')}`);
  },
  format: Object.freeze({
    /** @param {any} c */
    attemptFailed: (c) => `[${c.tag}] attempt ${c.attempt}/${c.maxAttempts} FAILED after ${c.elapsedMs}ms: ${c.err.message}`,
    /** @param {any} c */
    ready: (c) => `[${c.tag}] unityInstance re-set after ${c.waitMs}ms (warm reinit ok, attempt ${c.attempt}, evalThrows=${c.evalThrows})`,
    // evalThrows>0이면 재로드 루프 진행 중 = 워치독 발동.
    /** @param {any} c */
    notReadyError: (c) => new Error(`unityInstance not set within ${c.budgetMs / 1000}s budget (evalThrows=${c.res.evalThrows}${c.res.closed ? ', page closed' : ''})`),
    /** @param {any} c */
    crash: (c) => `[${c.tag}] genuine crash signature detected — hard-fail (no retry)`,
    /** @param {any} c */
    productHang: (c) => `[${c.tag}] product hang signature detected (Failed to download file) — hard-fail (no retry)`,
    /** @param {any} c */
    productHangError: (c) => {
      const sig = c.consoleLines.filter((/** @type {string} */ l) => c.productHangRe.test(l)).slice(0, 3);
      const sub = c.pageErrors.filter((/** @type {any} */ e) => HANG_PAGEERROR_RE.test(e.message)).map((/** @type {any} */ e) => e.message).slice(0, 2);
      const detail = `console=[${sig.join(' | ')}] pageerror(subarray)=[${sub.join(' | ') || '없음'}]`;
      return new Error(
        'Unity 로더 .data 다운로드가 fetch 계측 예외로 깨진 제품 결함 시그니처 ' +
        `(런북: dev 빌드 warm reload hang): ${detail} :: ${c.err.message}`);
    },
    /** @param {any} c */
    closed: (c) => `[${c.tag}] page/context closed — cannot retry`,
    /** @param {any} c */
    retry: (c) => `[${c.tag}] harness connection-drop classified (server dropped webgl.data stream) — retrying reload`,
  }),
});

/**
 * reload → unityInstance 재설정까지를 하니스 순단 분류로 감싼 재시도 루프.
 *
 * 시도마다 수집기를 in-place로 비우고, 2회차부터는 sessionStorage의 제품 워치독 카운터와
 * 캐시 우회 플래그를 리셋한 뒤 reload(domcontentloaded, 45s)한다. unityInstance 대기는
 * waitForUnityBounded(벽시계-바운드)다. 실패 분류 순서는 crash → product hang →
 * page/context closed → 하니스 순단 재시도 → 소진이고, 리스너는 finally에서 해제한다.
 *
 * policy.onReady({ attempt, crashErrors })는 unityInstance가 잡힌 직후 시도 안에서 호출된다.
 * 여기서 던진 에러도 같은 분류를 탄다(3-1의 성공 경로 크래시 단언).
 *
 * @param {import('@playwright/test').Page} page
 * @param {any} policy POLICY_* 프리셋에 tag(로그 접두어)와 필요하면 onReady를 더한 객체
 * @returns {Promise<{ attempts: number, crashErrors: Array<{ message: string, stack?: string }> }>}
 */
export async function reloadWithRetry(page, policy) {
  const { tag, maxAttempts, budgetMs, harnessRe, productHangRe, format } = policy;

  const pageErrors = [];     // { message, stack? }
  const consoleLines = [];
  // 실패한 네트워크 요청(끊긴 소켓 등)을 URL+원인과 함께 포착.
  const failedRequests = [];
  const buildResponses = [];
  const errHandler = (/** @type {any} */ err) => pageErrors.push(policy.toPageError(err));
  const consoleHandler = (/** @type {import('@playwright/test').ConsoleMessage} */ msg) => {
    const line = policy.toConsoleLine(msg);
    consoleLines.push(line);
    if (policy.forwardConsoleLine) policy.forwardConsoleLine(line);
  };
  const reqFailedHandler = (/** @type {import('@playwright/test').Request} */ req) => {
    try {
      failedRequests.push(`${req.url().split('/').slice(-2).join('/')} :: ${req.failure()?.errorText || '?'}`);
    } catch (e) {}
  };
  const respHandler = (/** @type {import('@playwright/test').Response} */ resp) => {
    try {
      const u = resp.url();
      if (/\/Build\//.test(u)) buildResponses.push(`${u.split('/').slice(-1)[0]} -> ${resp.status()}`);
    } catch (e) {}
  };
  page.on('pageerror', errHandler);
  page.on('console', consoleHandler);
  page.on('requestfailed', reqFailedHandler);
  if (policy.collectBuildResponses) page.on('response', respHandler);

  const hadCrash = () => pageErrors.some((e) => CRASH_RE.test(e.message));
  const hadHarnessDrop = () =>
    failedRequests.some((f) => harnessRe.test(f)) ||
    consoleLines.some((l) => harnessRe.test(l));
  const hadProductHang = () => !!productHangRe && consoleLines.some((l) => productHangRe.test(l));
  const dumpDiag = (/** @type {string} */ reason) => {
    if (policy.dumpDiag) policy.dumpDiag({ tag, reason, pageErrors, failedRequests, buildResponses, consoleLines });
  };
  const logIf = (/** @type {string | null} */ line) => {
    if (line !== null) console.log(line);
  };

  try {
    let lastErr = null;
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      // 각 시도마다 수집기 초기화(참조 유지 위해 in-place clear).
      pageErrors.length = 0; consoleLines.length = 0;
      failedRequests.length = 0; buildResponses.length = 0;
      // 재시도 시엔 페이지 재로드 예산을 리셋해 페이지 자체 워치독도 새로 시도하게 하고,
      // 캐시 우회 플래그도 지워 재시도 reload가 워밍된 Cache-Storage를 활용하도록 한다.
      if (attempt > 1) {
        try {
          await page.evaluate(() => {
            try { sessionStorage.removeItem('__ait_reload_count__'); } catch (e) {}
            try { sessionStorage.removeItem('__ait_skip_data_cache__'); } catch (e) {}
          });
        } catch (e) {}
      }
      const t0 = Date.now();
      let closedFatal = false;
      try {
        // domcontentloaded로 커밋(networkidle 금지 — 워치독 재다운로드 루프 하에선 idle이 안 옴).
        // unityInstance 대기는 벽시계-바운드 폴링으로 분리 제어한다.
        const resp = await page.reload({ waitUntil: 'domcontentloaded', timeout: 45000 });
        await policy.checkReload(page, resp, { tag, attempt, maxAttempts, t0 });

        const tWait = Date.now();
        const res = await waitForUnityBounded(page, budgetMs, { abortIf: productHangRe ? hadProductHang : undefined });
        if (res.ready) {
          logIf(format.ready({ tag, attempt, maxAttempts, waitMs: Date.now() - tWait, evalThrows: res.evalThrows }));
          const crashErrors = pageErrors.filter((e) => CRASH_RE.test(e.message));
          if (policy.onReady) policy.onReady({ attempt, crashErrors });
          return { attempts: attempt, crashErrors };
        }
        closedFatal = !!res.closed;
        if (res.aborted) {
          // 제품 hang 지문 관측 — 예산 소진을 기다리지 않고 즉시 실패로 넘긴다(분류는 catch에서).
          throw new Error(`제품 hang 지문 조기 감지로 대기 중단 (${Date.now() - tWait}ms 경과)`);
        }
        throw format.notReadyError({ budgetMs, res });
      } catch (err) {
        lastErr = err;
        const crash = hadCrash();
        const drop = hadHarnessDrop();
        console.log(format.attemptFailed({ tag, attempt, maxAttempts, elapsedMs: Date.now() - t0, err, crash, drop, failedRequests }));
        // 진짜 크래시면 재시도 없이 즉시 실패(원 계약 보존).
        if (crash) {
          logIf(format.crash ? format.crash({ tag }) : null);
          dumpDiag('crash');
          throw err;
        }
        // 제품 hang 시그니처면 재시도 없이 즉시 실패 — 재시도로 삼키면 회귀가 다시 은폐된다.
        // (제품 워치독이 최대 2회 자동 reload 하지만, 첫 관측에서 바로 종료하므로 그 루프와
        //  경합하지 않는다. 리스너는 아래 finally에서 한 번에 해제된다.)
        // 단, 로더는 진짜 전송 계층 순단(fetch reject)에도 같은 "Failed to download file"을
        // 남기므로 net 에러 시그니처가 공존하면 순단이 원인 — 하니스 재시도 경로로 넘긴다.
        // 제품 결함(fetch 계측 예외)은 요청 자체는 성공해 net 에러가 절대 없다는 점이 지문이다.
        if (hadProductHang() && !drop) {
          logIf(format.productHang({ tag }));
          dumpDiag('product-hang');
          throw format.productHangError({ err, productHangRe, consoleLines, pageErrors });
        }
        // 페이지/컨텍스트가 닫혔으면 재시도 불가(fatal).
        if (closedFatal || CLOSED_RE.test(err.message || '')) {
          logIf(format.closed ? format.closed({ tag }) : null);
          dumpDiag('closed');
          throw err;
        }
        // 하니스 순단(로컬 서버 연결 끊김)이고 시도가 남았으면 재시도.
        if (attempt < maxAttempts && drop) {
          console.log(format.retry({ tag }));
          continue;
        }
        // 소진 또는 미분류: 진단 덤프 후 실패.
        dumpDiag('exhausted');
        throw err;
      }
    }
    throw lastErr;
  } finally {
    page.off('pageerror', errHandler);
    page.off('console', consoleHandler);
    page.off('requestfailed', reqFailedHandler);
    if (policy.collectBuildResponses) page.off('response', respHandler);
  }
}
