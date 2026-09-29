/**
 * Optional 타임아웃 파라미터(timeoutMs) 회귀 테스트
 *
 * 모든 cs-waits-js 비동기 API(C#이 JS 브릿지 응답을 기다리는 `public static async`
 * 메서드)에 opt-in 클라이언트 타임아웃 파라미터 `int timeoutMs = 0`이 일관되게
 * 부여되는지, 그리고 그 값이 AITCore.RegisterCallback으로 정확히 전달되는지 검증한다.
 *
 * 설계 계약 (비파괴적 opt-in):
 *  - `timeoutMs = 0`(기본값) → 무한 대기 = 기존 동작 그대로 (기존 사용자 코드 컴파일/동작 불변)
 *  - `timeoutMs > 0` → 마감 초과 시 AITClientTimeoutException으로 awaiter를 fault
 *  - 타임아웃은 C# 측 대기만 포기 — JS/플랫폼 작업은 계속되며 늦은 결과는 폐기
 *
 * 대상은 정확히 cs-waits-js 71개 API(× Awaitable/Task 두 브랜치 = 142). Action을
 * 반환하는 nested-callback(RegisterNestedCallback) / event-subscription API는
 * await하지 않으므로 타임아웃 대상이 아니며 timeoutMs가 새어들면 안 된다.
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';
import { glob } from 'glob';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const runtimeSDKPath = path.resolve(__dirname, '../..', '../Runtime/SDK');

interface AsyncMethod {
  /** 메서드명 (= PascalName, apiName 문자열과 일치해야 함) */
  name: string;
  /** 시그니처의 파라미터 목록 원문 */
  params: string;
  /** 여는 중괄호부터 매칭되는 닫는 중괄호까지의 본문 */
  body: string;
  file: string;
}

/**
 * `public static async <ret> <Name>(<params>) { ... }` 메서드를 중괄호 매칭으로 추출.
 * 반환 타입에 중첩 제네릭(Awaitable<List<X>>)이 있어도 파라미터 목록의 첫 '('까지만
 * 소비하도록 non-greedy로 매칭한다. Action 반환(비-async) 메서드는 매칭되지 않는다.
 */
function extractAsyncMethods(content: string, file: string): AsyncMethod[] {
  const methods: AsyncMethod[] = [];
  const sigRe = /public static async [^\n(]+?\s+(\w+)\s*\(([^)]*)\)/g;
  let m: RegExpExecArray | null;
  while ((m = sigRe.exec(content)) !== null) {
    const name = m[1];
    const params = m[2];
    const braceStart = content.indexOf('{', sigRe.lastIndex);
    if (braceStart === -1) continue;
    let depth = 0;
    let end = braceStart;
    for (let i = braceStart; i < content.length; i++) {
      const ch = content[i];
      if (ch === '{') depth++;
      else if (ch === '}') {
        depth--;
        if (depth === 0) { end = i; break; }
      }
    }
    methods.push({ name, params, body: content.slice(braceStart, end + 1), file });
  }
  return methods;
}

/** RegisterCallback<...>( ... ) 호출의 인자 목록 원문을 추출 (본문 내 첫 호출) */
function extractRegisterCallbackArgs(body: string): string | null {
  const idx = body.indexOf('RegisterCallback<');
  if (idx === -1) return null;
  const open = body.indexOf('(', idx);
  if (open === -1) return null;
  let depth = 0;
  for (let i = open; i < body.length; i++) {
    const ch = body[i];
    if (ch === '(') depth++;
    else if (ch === ')') {
      depth--;
      if (depth === 0) return body.slice(open + 1, i);
    }
  }
  return null;
}

let coreContent = '';
let apiFiles: { name: string; content: string }[] = [];
let asyncMethods: AsyncMethod[] = [];

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

  const csFiles = await glob('AIT.*.cs', { cwd: runtimeSDKPath });
  for (const fileName of csFiles) {
    if (fileName.startsWith('AIT.Types') || fileName === 'AIT.cs') continue;
    const content = await fs.readFile(path.join(runtimeSDKPath, fileName), 'utf-8');
    apiFiles.push({ name: fileName, content });
    asyncMethods.push(...extractAsyncMethods(content, fileName));
  }
});

describe('AITCore 타임아웃 인프라 (csharp-core.hbs)', () => {
  test('AITClientTimeoutException이 AITException을 상속하고 TimeoutMs를 노출한다', () => {
    expect(coreContent).toMatch(/public partial class AITClientTimeoutException\s*:\s*AITException/);
    expect(coreContent).toContain('public int TimeoutMs { get; }');
    // catch (AITException) 하위호환: 생성자가 base(...)로 위임
    expect(coreContent).toMatch(/public AITClientTimeoutException\(string apiName, int timeoutMs\)/);
  });

  test('RegisterCallback가 opt-in timeoutMs/apiName 파라미터를 받는다 (기본값 0 = 무한 대기)', () => {
    expect(coreContent).toMatch(
      /public string RegisterCallback<T>\([^)]*int timeoutMs = 0, string apiName = null\)/
    );
    // timeoutMs > 0 일 때만 코루틴 무장
    expect(coreContent).toMatch(/if \(timeoutMs > 0\)/);
    expect(coreContent).toMatch(/StartCoroutine\(TimeoutCoroutine\(id, timeoutMs, apiName\)\)/);
  });

  test('TimeoutCoroutine / CancelTimeout 메서드가 존재한다', () => {
    expect(coreContent).toMatch(
      /private System\.Collections\.IEnumerator TimeoutCoroutine\(string callbackId, int timeoutMs, string apiName\)/
    );
    expect(coreContent).toContain('WaitForSecondsRealtime(timeoutMs / 1000f)');
    // 마감 초과 시 AITClientTimeoutException으로 에러 콜백 호출
    expect(coreContent).toMatch(/new AITClientTimeoutException\(apiName \?\? string\.Empty, timeoutMs\)/);
    expect(coreContent).toMatch(/private void CancelTimeout\(string callbackId\)/);
  });

  test('실제 응답 도착/콜백 제거 경로에서 CancelTimeout으로 코루틴을 취소한다 (double-complete 방지)', () => {
    // TryGetCallback / TryGetErrorCallback / RemoveCallback 3곳에서 취소
    const cancelCalls = (coreContent.match(/CancelTimeout\(callbackId\);/g) || []).length;
    expect(cancelCalls).toBeGreaterThanOrEqual(3);
  });
});

describe('cs-waits-js async API의 timeoutMs 파라미터 (csharp-category-partial.hbs)', () => {
  test('추출된 async 메서드가 존재하고 Awaitable/Task 두 브랜치로 짝수 개다', () => {
    expect(asyncMethods.length).toBeGreaterThan(0);
    expect(asyncMethods.length % 2).toBe(0);
  });

  test('모든 async 메서드의 마지막 파라미터가 int timeoutMs = 0 이다 (비파괴적 기본값)', () => {
    const missing = asyncMethods.filter(
      (mth) => !/(?:^|,\s*)int timeoutMs = 0\s*$/.test(mth.params.trim())
    );
    expect(
      missing.map((x) => `${x.file}:${x.name}(${x.params})`)
    ).toEqual([]);
  });

  test('모든 async 메서드가 timeoutMs와 자신의 이름을 RegisterCallback으로 전달한다', () => {
    const violations: string[] = [];
    for (const mth of asyncMethods) {
      const args = extractRegisterCallbackArgs(mth.body);
      if (args === null) {
        violations.push(`${mth.file}:${mth.name} — RegisterCallback 호출 없음`);
        continue;
      }
      if (!/\btimeoutMs\b/.test(args)) {
        violations.push(`${mth.file}:${mth.name} — timeoutMs 미전달`);
      }
      // apiName 문자열이 메서드명과 정확히 일치
      if (!args.includes(`"${mth.name}"`)) {
        violations.push(`${mth.file}:${mth.name} — apiName "${mth.name}" 미전달`);
      }
    }
    expect(violations).toEqual([]);
  });

  test('async 메서드 수 == int timeoutMs = 0 시그니처 수 == RegisterCallback 호출 수 (1:1:1)', () => {
    let sigCount = 0;
    let regCount = 0;
    for (const { content } of apiFiles) {
      sigCount += (content.match(/int timeoutMs = 0\)/g) || []).length;
      regCount += (content.match(/RegisterCallback</g) || []).length;
    }
    expect(sigCount).toBe(asyncMethods.length);
    expect(regCount).toBe(asyncMethods.length);
  });
});

describe('비대상 API 격리 (nested-callback / event-subscription)', () => {
  test('RegisterNestedCallback 호출은 timeoutMs를 전달하지 않는다', () => {
    for (const { name, content } of apiFiles) {
      for (const m of content.matchAll(/RegisterNestedCallback\(([\s\S]*?)\);/g)) {
        expect(
          m[1].includes('timeoutMs'),
          `${name}: RegisterNestedCallback가 timeoutMs를 전달함`
        ).toBe(false);
      }
    }
  });

  test('Action 반환(비-async) 시그니처에 timeoutMs가 새어들지 않는다', () => {
    const leaks: string[] = [];
    for (const { name, content } of apiFiles) {
      for (const m of content.matchAll(/public static (?:System\.)?Action[^\n]*/g)) {
        if (m[0].includes('timeoutMs')) leaks.push(`${name}: ${m[0].trim()}`);
      }
    }
    expect(leaks).toEqual([]);
  });
});
