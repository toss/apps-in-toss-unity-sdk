/**
 * Tier 2: C# ↔ jslib 일관성 검증
 *
 * 생성된 C# DllImport 메서드와 jslib 함수 간의 일관성을 검증합니다.
 * - 함수 존재 여부
 * - 파라미터 개수 일치
 * - 콜백 파라미터 위치 규칙
 * - SendMessage 호출 패턴
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';
import { glob } from 'glob';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// =================================================================
// Types
// =================================================================

interface DllImportMethod {
  name: string;
  parameters: string[];
  file: string;
}

interface JslibFunction {
  name: string;
  parameters: string[];
  file: string;
  hasSendMessage: boolean;
  sendMessageTarget: string | null;
}

// =================================================================
// Helper Functions
// =================================================================

function extractDllImportMethods(csharpContent: string, fileName: string): DllImportMethod[] {
  const methods: DllImportMethod[] = [];
  const dllImportRegex = /\[System\.Runtime\.InteropServices\.DllImport\s*\(\s*"__Internal"\s*\)\s*\]\s*(?:private\s+)?static\s+extern\s+void\s+(__\w+_Internal)\s*\(([^)]*)\)\s*;/g;

  let match;
  while ((match = dllImportRegex.exec(csharpContent)) !== null) {
    const methodName = match[1];
    const paramsStr = match[2].trim();
    const parameters: string[] = [];

    if (paramsStr) {
      const paramParts = paramsStr.split(',');
      for (const part of paramParts) {
        const trimmed = part.trim();
        const parts = trimmed.split(/\s+/);
        if (parts.length >= 2) {
          parameters.push(parts[parts.length - 1]);
        }
      }
    }

    methods.push({ name: methodName, parameters, file: fileName });
  }

  return methods;
}

function extractJslibFunctions(jslibContent: string, fileName: string): JslibFunction[] {
  const functions: JslibFunction[] = [];
  const funcRegex = /(__\w+_Internal)\s*:\s*function\s*\(([^)]*)\)\s*\{/g;

  let match;
  while ((match = funcRegex.exec(jslibContent)) !== null) {
    const funcName = match[1];
    const paramsStr = match[2].trim();
    const startIndex = match.index;

    const parameters: string[] = [];
    if (paramsStr) {
      parameters.push(...paramsStr.split(',').map(p => p.trim()).filter(p => p));
    }

    let braceCount = 1;
    let bodyStart = jslibContent.indexOf('{', startIndex) + 1;
    let bodyEnd = bodyStart;

    for (let i = bodyStart; i < jslibContent.length && braceCount > 0; i++) {
      if (jslibContent[i] === '{') braceCount++;
      if (jslibContent[i] === '}') braceCount--;
      bodyEnd = i;
    }

    const funcBody = jslibContent.substring(bodyStart, bodyEnd);
    const hasSendMessage = funcBody.includes("SendMessage('AITCore'");
    let sendMessageTarget: string | null = null;

    if (hasSendMessage) {
      if (funcBody.includes("'OnAITEventCallback'")) {
        sendMessageTarget = 'OnAITEventCallback';
      } else if (funcBody.includes("'OnAITCallback'")) {
        sendMessageTarget = 'OnAITCallback';
      }
    }

    functions.push({ name: funcName, parameters, file: fileName, hasSendMessage, sendMessageTarget });
  }

  return functions;
}

// =================================================================
// Data Loading (Top-level await)
// =================================================================

const sdkGeneratorRoot = path.resolve(__dirname, '../..');
const runtimeSDKPath = path.resolve(sdkGeneratorRoot, '../Runtime/SDK');
const pluginsPath = path.join(runtimeSDKPath, 'Plugins');

// Load data synchronously at module level
const dllImportMethods: DllImportMethod[] = [];
const jslibFunctions: Map<string, JslibFunction> = new Map();

async function loadData() {
  if (dllImportMethods.length > 0) return;

  try {
    await fs.access(runtimeSDKPath);
  } catch {
    throw new Error(
      '❌ 생성된 SDK 파일을 찾을 수 없습니다!\n' +
      '   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.'
    );
  }

  const csFiles = await glob('*.cs', { cwd: runtimeSDKPath, absolute: false });
  for (const fileName of csFiles) {
    const content = await fs.readFile(path.join(runtimeSDKPath, fileName), 'utf-8');
    dllImportMethods.push(...extractDllImportMethods(content, fileName));
  }

  const jslibFilesList = await glob('*.jslib', { cwd: pluginsPath, absolute: false });
  for (const fileName of jslibFilesList) {
    const content = await fs.readFile(path.join(pluginsPath, fileName), 'utf-8');
    for (const func of extractJslibFunctions(content, fileName)) {
      jslibFunctions.set(func.name, func);
    }
  }
}

// =================================================================
// Constants
// =================================================================

// 이벤트 구독 여부를 jslib의 sendMessageTarget으로 자동 감지
// 하드코딩 목록 대신 생성된 코드에서 직접 판별
function isEventSubscription(name: string): boolean {
  const func = jslibFunctions.get(name);
  return func?.sendMessageTarget === 'OnAITEventCallback';
}

// =================================================================
// Tests
// =================================================================

describe('Tier 2: C# ↔ jslib 일관성 검증', () => {
  beforeAll(async () => {
    console.log('\n📂 C# 및 jslib 파일 분석 중...\n');
    await loadData();
    console.log(`✅ ${dllImportMethods.length}개 DllImport 메서드 발견`);
    console.log(`✅ ${jslibFunctions.size}개 jslib 함수 발견\n`);
  }, 10000);

  // =========================================
  // 1. 함수 존재 검증 (1 test)
  // =========================================
  describe('1. 함수 존재 검증', () => {
    test('모든 DllImport 메서드에 대응하는 jslib 함수 존재', async () => {
      await loadData();
      const missing = dllImportMethods.filter(m => !jslibFunctions.has(m.name));

      if (missing.length > 0) {
        console.error('❌ 누락된 jslib 함수:');
        missing.forEach(m => console.error(`   - ${m.name} (${m.file})`));
      }

      expect(missing).toHaveLength(0);
    });
  });

  // =========================================
  // 2. 파라미터 개수 검증 (65 tests)
  // =========================================
  describe('2. 파라미터 개수 검증', () => {
    test('각 메서드별 파라미터 개수 일치 검증', async () => {
      await loadData();

      const mismatches: string[] = [];
      for (const method of dllImportMethods) {
        const jslibFunc = jslibFunctions.get(method.name);
        if (!jslibFunc) continue;

        if (method.parameters.length !== jslibFunc.parameters.length) {
          mismatches.push(
            `${method.name}: C#=${method.parameters.length}, jslib=${jslibFunc.parameters.length}`
          );
        }
      }

      if (mismatches.length > 0) {
        console.error('❌ 파라미터 개수 불일치:');
        mismatches.forEach(m => console.error(`   - ${m}`));
      }

      expect(mismatches).toHaveLength(0);
    });
  });

  // =========================================
  // 3. 콜백 파라미터 규칙 검증 (65 tests)
  // =========================================
  describe('3. 콜백 파라미터 규칙 검증', () => {
    test('각 메서드별 콜백 파라미터 규칙 준수', async () => {
      await loadData();

      const violations: string[] = [];
      for (const method of dllImportMethods) {
        const params = method.parameters;

        if (method.name === '__AITUnsubscribe_Internal') {
          if (params.length !== 1 || params[0] !== 'subscriptionId') {
            violations.push(`${method.name}: expected (subscriptionId)`);
          }
          continue;
        }

        if (params.length < 2) {
          violations.push(`${method.name}: 파라미터 2개 미만`);
          continue;
        }

        if (params[params.length - 1] !== 'typeName') {
          violations.push(`${method.name}: 마지막 파라미터가 typeName이 아님`);
        }

        const secondLast = params[params.length - 2];
        if (!['callbackId', 'subscriptionId'].includes(secondLast)) {
          violations.push(`${method.name}: 콜백 ID 파라미터 누락`);
        }
      }

      if (violations.length > 0) {
        console.error('❌ 콜백 파라미터 규칙 위반:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });
  });

  // =========================================
  // 4. SendMessage 호출 검증 (64 tests - Unsubscribe, 동기 함수 제외)
  // =========================================
  describe('4. SendMessage 호출 검증', () => {
    // 동기 함수 목록 (SendMessage 사용하지 않거나 특수 패턴 사용)
    const SYNC_FUNCTIONS = [
      '__AITUnsubscribe_Internal',
      '__GetDevicePixelRatio_Internal', // WebGL 수동 API - 동기 함수
      '__AITVisibilityHelper_GetIsVisible_Internal', // VisibilityHelper - 특수 SendMessage 패턴 (OnVisibilityStateChanged)
    ];

    test('각 jslib 함수별 SendMessage 호출 확인', async () => {
      await loadData();

      const violations: string[] = [];
      for (const [name, func] of jslibFunctions) {
        if (SYNC_FUNCTIONS.includes(name)) continue;

        if (!func.hasSendMessage) {
          violations.push(`${name}: SendMessage 호출 없음`);
        } else if (!func.sendMessageTarget) {
          violations.push(`${name}: SendMessage 타겟 확인 불가`);
        }
      }

      if (violations.length > 0) {
        console.error('❌ SendMessage 호출 누락:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });
  });

  // =========================================
  // 5. jslib ↔ C# 콜백 파라미터 교차 일관성 검증
  // =========================================
  describe('5. jslib ↔ C# 콜백 파라미터 교차 일관성 검증', () => {
    test('OnAITEventCallback 함수는 C#에 subscriptionId, OnAITCallback 함수는 callbackId 사용', async () => {
      await loadData();

      const SYNC_FUNCTIONS = [
        '__AITUnsubscribe_Internal',
        '__GetDevicePixelRatio_Internal',
        '__AITVisibilityHelper_GetIsVisible_Internal',
      ];

      const violations: string[] = [];
      for (const [name, func] of jslibFunctions) {
        if (SYNC_FUNCTIONS.includes(name)) continue;

        const method = dllImportMethods.find(m => m.name === name);
        if (!method || method.parameters.length < 2) continue;

        const secondLast = method.parameters[method.parameters.length - 2];

        if (func.sendMessageTarget === 'OnAITEventCallback' && secondLast !== 'subscriptionId') {
          violations.push(`${name}: OnAITEventCallback이지만 C#에 subscriptionId 없음 (got ${secondLast})`);
        } else if (func.sendMessageTarget === 'OnAITCallback' && secondLast !== 'callbackId') {
          violations.push(`${name}: OnAITCallback이지만 C#에 callbackId 없음 (got ${secondLast})`);
        }
      }

      if (violations.length > 0) {
        console.error('❌ jslib ↔ C# 콜백 파라미터 불일치:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });
  });

  // =========================================
  // 6. 비동기 함수 콜백 타겟 유효성 검증
  // =========================================
  describe('6. 비동기 함수 콜백 타겟 유효성 검증', () => {
    const SYNC_FUNCTIONS = [
      '__AITUnsubscribe_Internal',
      '__GetDevicePixelRatio_Internal',
      '__AITVisibilityHelper_GetIsVisible_Internal',
    ];

    test('모든 비동기 함수는 OnAITCallback 또는 OnAITEventCallback 사용', async () => {
      await loadData();

      const VALID_TARGETS = ['OnAITCallback', 'OnAITEventCallback'];
      const violations: string[] = [];
      for (const [name, func] of jslibFunctions) {
        if (SYNC_FUNCTIONS.includes(name)) continue;

        if (!func.sendMessageTarget || !VALID_TARGETS.includes(func.sendMessageTarget)) {
          violations.push(`${name}: 유효하지 않은 콜백 타겟 (got ${func.sendMessageTarget})`);
        }
      }

      if (violations.length > 0) {
        console.error('❌ 유효하지 않은 콜백 타겟:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });
  });

  // =========================================
  // 7. 개별 메서드 상세 검증
  // =========================================
  describe('7. 개별 메서드 상세 검증', () => {
    test.concurrent.each([
      // 인증
      ['__appLogin_Internal', 2, 'OnAITCallback'],
      ['__getIsTossLoginIntegratedService_Internal', 2, 'OnAITCallback'],
      // 결제
      ['__checkoutPayment_Internal', 3, 'OnAITCallback'],
      // 이벤트 구독
      ['__GraniteEventSubscribeBackEvent_Internal', 2, 'OnAITEventCallback'],
      ['__TdsEventSubscribeNavigationAccessoryEvent_Internal', 2, 'OnAITEventCallback'],
      // Unsubscribe
      ['__AITUnsubscribe_Internal', 1, null],
      // Storage
      ['__StorageGetItem_Internal', 3, 'OnAITCallback'],
      ['__StorageSetItem_Internal', 4, 'OnAITCallback'],
      ['__StorageRemoveItem_Internal', 3, 'OnAITCallback'],
      ['__StorageClearItems_Internal', 2, 'OnAITCallback'],
    ])('%s: 파라미터 %d개, 콜백 타겟 %s', async (name, expectedParams, expectedTarget) => {
      await loadData();

      const method = dllImportMethods.find(m => m.name === name);
      const jslibFunc = jslibFunctions.get(name);

      expect(method).toBeDefined();
      expect(jslibFunc).toBeDefined();

      if (method) {
        expect(method.parameters.length).toBe(expectedParams);
      }

      if (jslibFunc) {
        expect(jslibFunc.parameters.length).toBe(expectedParams);
        expect(jslibFunc.sendMessageTarget).toBe(expectedTarget);
      }
    });
  });

  // =========================================
  // 8. 요약 출력
  // =========================================
  test('요약: C# ↔ jslib 매핑 현황', async () => {
    await loadData();

    // jslib에만 있고 DllImport가 없는 함수 목록
    // __AITUnsubscribe_Internal: C# 측에서 수동 구현, DllImport 없음
    // 참고: __AITVisibilityHelper_GetIsVisible_Internal은 Runtime/Helpers/에 별도 위치함
    const JSLIB_ONLY_FUNCTIONS = [
      '__AITUnsubscribe_Internal',
    ];

    const eventFunctions = Array.from(jslibFunctions.keys()).filter(isEventSubscription);
    const apiFunctions = Array.from(jslibFunctions.keys()).filter(name =>
      !isEventSubscription(name) && !JSLIB_ONLY_FUNCTIONS.includes(name)
    );

    console.log('\n📊 C# ↔ jslib 매핑 요약:');
    console.log(`   - DllImport 메서드: ${dllImportMethods.length}개`);
    console.log(`   - jslib 함수: ${jslibFunctions.size}개`);
    console.log(`   - jslib only (Unsubscribe 등): ${JSLIB_ONLY_FUNCTIONS.length}개`);
    console.log(`   - 이벤트 구독 (OnAITEventCallback): ${eventFunctions.length}개`);
    console.log(`   - 일반 API (OnAITCallback): ${apiFunctions.length}개`);

    // 검증 통과 확인: jslib only 함수를 제외하면 수가 맞아야 함
    expect(dllImportMethods.length).toBe(jslibFunctions.size - JSLIB_ONLY_FUNCTIONS.length);
  });
});
