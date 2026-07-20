/**
 * IAP grant 콜백 교착(deadlock) 방지 하드닝 회귀 테스트
 *
 * `processProductGrant` 같은 nested grant 콜백에서 결제 native 오버레이가 Unity WebGL
 * player loop를 정지시켜 발생하는 순환 교착을 opt-in 타임아웃으로 방어한다. 단일 노브는
 * `AITCore.NestedCallbackTimeoutMs`(기본 0 = 비활성 = 기존 동작 불변) 하나뿐이며, >0이면
 * 두 타임아웃을 동시에 무장한다:
 *  (A) JS-side setTimeout — 오버레이로 loop가 얼어도 이벤트 루프에서 발화 → 교착을 실제로 깬다.
 *  (B) C# 코루틴(WaitForSecondsRealtime) — loop 의존적 심층 방어(loop 재개 후 정리).
 *
 * exactly-once: 정상 C# 응답과 JS-side setTimeout 두 경로가 정확히 1회만 resolve하고
 * 서로의 pending 상태를 정리한다. C#쪽은 _pendingNestedRequests, JS쪽은
 * window.__AIT_NESTED_CALLBACKS 엔트리 삭제로 이중 정리한다.
 *
 * 정합성 트레이드오프: JS-side 타임아웃이 grant를 false로 resolve해도 서버 결제는 성공했을 수
 * 있어 "결제 완료·미지급" 상태가 되며 IAPGetCompletedOrRefundedOrders로 사후 reconcile 필요.
 * 기본 0(비활성)인 이유가 이 트레이드오프임 — XML doc/주석에 표면화되어야 한다.
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const runtimeSDKPath = path.resolve(__dirname, '../..', '../Runtime/SDK');

let coreContent = '';
let iapJslibContent = '';

/** `functionName: function(...) { ... }` 본문을 중괄호 매칭으로 추출 (jslib) */
function extractJslibFunctionBody(content: string, functionName: string): string | null {
  const marker = `${functionName}: function`;
  const idx = content.indexOf(marker);
  if (idx === -1) return null;
  const braceStart = content.indexOf('{', idx);
  if (braceStart === -1) return null;
  let depth = 0;
  for (let i = braceStart; i < content.length; i++) {
    const ch = content[i];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return content.slice(braceStart, i + 1);
    }
  }
  return null;
}

/** C# 메서드 본문을 중괄호 매칭으로 추출 (시그니처 원문으로 시작점 지정) */
function extractCsMethodBody(content: string, signatureNeedle: string): string | null {
  const idx = content.indexOf(signatureNeedle);
  if (idx === -1) return null;
  const braceStart = content.indexOf('{', idx + signatureNeedle.length);
  if (braceStart === -1) return null;
  let depth = 0;
  for (let i = braceStart; i < content.length; i++) {
    const ch = content[i];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return content.slice(braceStart, i + 1);
    }
  }
  return null;
}

beforeAll(async () => {
  try {
    await fs.access(runtimeSDKPath);
  } catch {
    throw new Error(
      '❌ 생성된 SDK 파일을 찾을 수 없습니다!\n' +
      '   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.'
    );
  }
  coreContent = await fs.readFile(path.join(runtimeSDKPath, 'AITCore.cs'), 'utf-8');
  iapJslibContent = await fs.readFile(
    path.join(runtimeSDKPath, 'Plugins', 'AppsInToss-IAP.jslib'),
    'utf-8'
  );
});

describe('(a) NestedCallbackTimeoutMs 노브 표면 (csharp-core.hbs)', () => {
  test('public static int 프로퍼티 + 기본값 0 백킹 필드가 존재한다 (비파괴 opt-in)', () => {
    expect(coreContent).toMatch(/public static int NestedCallbackTimeoutMs/);
    expect(coreContent).toMatch(/private static int _nestedCallbackTimeoutMs = 0;/);
  });

  test('세터가 WebGL 가드 안에서 __AITSetNestedCallbackTimeoutMs 브릿지를 호출한다', () => {
    const body = extractCsMethodBody(coreContent, 'public static int NestedCallbackTimeoutMs');
    expect(body).not.toBeNull();
    // 세터에서 백킹 필드 갱신 + 브릿지 호출이 #if UNITY_WEBGL 가드 안에 있어야 한다
    expect(body).toContain('_nestedCallbackTimeoutMs = value;');
    expect(body).toContain('#if UNITY_WEBGL && !UNITY_EDITOR');
    expect(body).toContain('__AITSetNestedCallbackTimeoutMs(value);');
  });

  test('브릿지 DllImport(__AITSetNestedCallbackTimeoutMs)가 WebGL 가드 안에 선언된다', () => {
    expect(coreContent).toMatch(
      /private static extern void __AITSetNestedCallbackTimeoutMs\(int timeoutMs\);/
    );
    // _Internal 접미사가 아니므로 invariants의 DllImport 규약 검사에서 제외됨(__AITRespondToNestedCallback과 동일)
    expect(coreContent).not.toContain('__AITSetNestedCallbackTimeoutMs_Internal');
  });

  test('정합성 트레이드오프가 XML doc에 표면화된다 (paid-but-not-granted + IAPGetCompletedOrRefundedOrders + 기본 0 사유)', () => {
    // XML doc 주석에 사후 reconcile 플로우와 기본 비활성 사유가 명시되어야 한다
    expect(coreContent).toContain('IAPGetCompletedOrRefundedOrders');
    expect(coreContent).toMatch(/paid but not granted/i);
    expect(coreContent).toMatch(/default is 0/i);
  });
});

describe('(c) C# nested 코루틴 타임아웃 (심층 방어, csharp-core.hbs)', () => {
  test('DispatchNestedCallbackAsync가 timeoutMs>0일 때만 코루틴을 무장한다', () => {
    const body = extractCsMethodBody(
      coreContent,
      'private async Task DispatchNestedCallbackAsync(string requestId, string data, Func<string, Task<bool>> callback)'
    );
    expect(body).not.toBeNull();
    // 노브 스냅샷 → 게이트 → 무장 (pending 등록 + 코루틴 시작)
    expect(body).toContain('int timeoutMs = NestedCallbackTimeoutMs;');
    expect(body).toMatch(/if \(timeoutMs > 0\)/);
    expect(body).toContain('_pendingNestedRequests.Add(requestId);');
    expect(body).toContain('StartCoroutine(NestedTimeoutCoroutine(requestId, timeoutMs))');
    // 정상 완료 시 코루틴 취소 + exactly-once 가드
    expect(body).toContain('CancelNestedTimeout(requestId);');
    expect(body).toMatch(/if \(timeoutMs > 0 && !_pendingNestedRequests\.Remove\(requestId\)\)/);
  });

  test('NestedTimeoutCoroutine이 #978 WaitForSecondsRealtime 패턴으로 정확히 1회 false 응답한다', () => {
    const body = extractCsMethodBody(
      coreContent,
      'private System.Collections.IEnumerator NestedTimeoutCoroutine(string requestId, int timeoutMs)'
    );
    expect(body).not.toBeNull();
    expect(body).toContain('WaitForSecondsRealtime(timeoutMs / 1000f)');
    // pending에서 제거에 성공한 경우에만 응답 (double-respond 방지)
    expect(body).toMatch(/if \(_pendingNestedRequests\.Remove\(requestId\)\)/);
    expect(body).toContain('RespondToNestedCallback(requestId, false);');
  });

  test('CancelNestedTimeout이 StopCoroutine으로 대기 중 코루틴을 취소한다', () => {
    const body = extractCsMethodBody(coreContent, 'private void CancelNestedTimeout(string requestId)');
    expect(body).not.toBeNull();
    expect(body).toContain('StopCoroutine(coroutine)');
    expect(body).toContain('_nestedTimeoutCoroutines.Remove(requestId);');
  });

  test('코루틴이 player-loop 의존이라 오버레이 교착을 못 깬다는 한계가 주석에 명시된다 (역할 분담)', () => {
    const body = extractCsMethodBody(
      coreContent,
      'private async Task DispatchNestedCallbackAsync(string requestId, string data, Func<string, Task<bool>> callback)'
    );
    expect(body).not.toBeNull();
    // (B)가 loop 재개 후에만 발화하고, 오버레이 교착은 (A) JS-side가 깬다는 역할 분담 주석
    expect(body).toMatch(/WaitForSecondsRealtime/);
    expect(body).toMatch(/lever A|JavaScript-side setTimeout/);
  });
});

describe('(A) grant Promise JS-side 타임아웃 (jslib.ts → AppsInToss-IAP.jslib)', () => {
  test('processProductGrant Promise가 __AIT_NESTED_TIMEOUT_MS 가드 setTimeout을 무장한다', () => {
    // 최소 1개 이상의 grant 콜백(one-time/subscription)에 lever A가 삽입되어야 한다
    const timeoutReads = (iapJslibContent.match(/var timeoutMs = window\.__AIT_NESTED_TIMEOUT_MS;/g) || []).length;
    expect(timeoutReads).toBeGreaterThanOrEqual(1);
    // 가드: window.__AIT_NESTED_TIMEOUT_MS(undefined 기본) 뒤에서만 setTimeout
    expect(iapJslibContent).toMatch(/if \(timeoutMs && timeoutMs > 0\) \{\s*timeoutId = setTimeout\(/);
    // setTimeout 발화 시 콜백 제거 + false resolve (늦은 C# 응답은 no-op이 되도록)
    expect(iapJslibContent).toContain('delete window.__AIT_NESTED_CALLBACKS[requestId];');
    expect(iapJslibContent).toMatch(/resolve\(false\);/);
    // 엔트리는 { resolve, timeoutId } 형태로 저장(응답 경로에서 clearTimeout 가능)
    expect(iapJslibContent).toContain('{ resolve: resolve, timeoutId: timeoutId };');
  });

  test('__AITRespondToNestedCallback: 정상 응답 시 clearTimeout + resolve, 이미 정리된 경우 조용히 no-op', () => {
    const body = extractJslibFunctionBody(iapJslibContent, '__AITRespondToNestedCallback');
    expect(body).not.toBeNull();
    // 정상 경로: 저장된 setTimeout을 clearTimeout(중복 resolve 금지) 후 resolve
    expect(body).toContain('clearTimeout(entry.timeoutId)');
    expect(body).toContain('delete window.__AIT_NESTED_CALLBACKS[reqId];');
    expect(body).toContain('entry.resolve(resultBool)');
    // 이미 setTimeout이 정리한 경우(엔트리 없음): 에러 로그 없이 no-op이어야 한다
    expect(body).not.toContain('console.warn');
    expect(body).not.toContain('console.error');
  });

  test('__AITSetNestedCallbackTimeoutMs 브릿지가 window.__AIT_NESTED_TIMEOUT_MS 전역을 설정한다', () => {
    const body = extractJslibFunctionBody(iapJslibContent, '__AITSetNestedCallbackTimeoutMs');
    expect(body).not.toBeNull();
    expect(body).toContain('window.__AIT_NESTED_TIMEOUT_MS = timeoutMs;');
  });

  test('정합성 트레이드오프가 jslib 주석에 표면화된다 (paid-but-not-granted reconcile)', () => {
    expect(iapJslibContent).toContain('IAPGetCompletedOrRefundedOrders');
    expect(iapJslibContent).toMatch(/paid but not granted/i);
  });
});

describe('(d) 기본 0(비활성) = 기존 동작 불변 (게이팅 무결성)', () => {
  test('C#: 코루틴 무장/스냅샷이 timeoutMs>0 게이트 뒤에만 존재한다 (기본 0이면 무장 안 됨)', () => {
    // 게이트 밖에서 무조건 무장하는 코드가 없어야 한다 — StartCoroutine(NestedTimeoutCoroutine ...)은
    // 반드시 `if (timeoutMs > 0)` 블록 안에서만 나타난다.
    const dispatchBody = extractCsMethodBody(
      coreContent,
      'private async Task DispatchNestedCallbackAsync(string requestId, string data, Func<string, Task<bool>> callback)'
    )!;
    const gateIdx = dispatchBody.indexOf('if (timeoutMs > 0)');
    const armIdx = dispatchBody.indexOf('StartCoroutine(NestedTimeoutCoroutine');
    expect(gateIdx).toBeGreaterThanOrEqual(0);
    expect(armIdx).toBeGreaterThan(gateIdx);
    // 무장은 이 dispatch 메서드에서 정확히 1회만 (게이트 뒤)
    expect((coreContent.match(/StartCoroutine\(NestedTimeoutCoroutine/g) || []).length).toBe(1);
  });

  test('JS: setTimeout이 __AIT_NESTED_TIMEOUT_MS 진리성 가드 뒤에만 존재한다 (기본 undefined이면 무장 안 됨)', () => {
    // grant 콜백 블록 내 setTimeout( 호출은 모두 `if (timeoutMs && timeoutMs > 0)` 뒤에 온다.
    // 무장 setTimeout 수 == 가드 수 (짝이 맞아야 게이팅 누락 없음)
    const armCount = (iapJslibContent.match(/timeoutId = setTimeout\(/g) || []).length;
    const guardCount = (iapJslibContent.match(/if \(timeoutMs && timeoutMs > 0\) \{/g) || []).length;
    expect(armCount).toBeGreaterThanOrEqual(1);
    expect(armCount).toBe(guardCount);
  });
});
