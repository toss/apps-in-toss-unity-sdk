/**
 * Tier 2: Binding 일관성 검증
 *
 * C# extern 선언과 jslib 함수 간의 바인딩 일관성을 검증합니다.
 *
 * 검증 항목:
 * 1. 모든 C# extern 선언에 대응하는 jslib 함수가 존재하는지
 * 2. 모든 jslib 함수에 대응하는 C# extern 선언이 존재하는지
 * 3. 함수 시그니처(파라미터 수)가 일치하는지
 * 4. UTF8ToString 사용 패턴이 올바른지
 * 5. SendMessage 콜백 패턴이 올바른지
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';
import { glob } from 'glob';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

interface CSharpExtern {
  functionName: string;
  parameters: string[];
  returnType: string;
  sourceFile: string;
}

interface JslibFunction {
  functionName: string;
  parameterCount: number;
  sourceFile: string;
  usesUTF8ToString: boolean;
  usesSendMessage: boolean;
}

describe('Tier 2: C# ↔ jslib 바인딩 일관성 검증', () => {
  let csharpExterns: CSharpExtern[] = [];
  let jslibFunctions: JslibFunction[] = [];

  beforeAll(async () => {
    console.log('\n📂 SDK 파일 로딩 중...\n');

    const sdkGeneratorRoot = path.resolve(__dirname, '../..');
    const runtimeSDKPath = path.resolve(sdkGeneratorRoot, '../Runtime/SDK');
    const pluginsPath = path.resolve(runtimeSDKPath, 'Plugins');

    try {
      await fs.access(runtimeSDKPath);
    } catch {
      throw new Error(
        '❌ 생성된 SDK 파일을 찾을 수 없습니다!\n' +
          '   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.\n' +
          `   Expected path: ${runtimeSDKPath}`
      );
    }

    // C# extern 선언 파싱
    const csFiles = await glob('AIT*.cs', { cwd: runtimeSDKPath, absolute: false });
    for (const fileName of csFiles) {
      const filePath = path.join(runtimeSDKPath, fileName);
      const content = await fs.readFile(filePath, 'utf-8');
      const externs = parseCSharpExterns(content, fileName);
      csharpExterns.push(...externs);
    }

    // AITCore.cs에서도 extern 찾기
    const coreFilePath = path.join(runtimeSDKPath, 'AITCore.cs');
    try {
      const coreContent = await fs.readFile(coreFilePath, 'utf-8');
      const coreExterns = parseCSharpExterns(coreContent, 'AITCore.cs');
      csharpExterns.push(...coreExterns);
    } catch {
      console.log('⚠️ AITCore.cs 읽기 실패');
    }

    // jslib 함수 파싱
    try {
      await fs.access(pluginsPath);
      const jslibFiles = await glob('AppsInToss*.jslib', { cwd: pluginsPath, absolute: false });
      for (const fileName of jslibFiles) {
        const filePath = path.join(pluginsPath, fileName);
        const content = await fs.readFile(filePath, 'utf-8');
        const functions = parseJslibFunctions(content, fileName);
        jslibFunctions.push(...functions);
      }
    } catch {
      console.log('⚠️ Plugins 폴더 접근 실패');
    }

    console.log(`✅ C# extern 선언: ${csharpExterns.length}개`);
    console.log(`✅ jslib 함수: ${jslibFunctions.length}개\n`);
  }, 15000);

  /**
   * C# DllImport extern 선언 파싱
   */
  function parseCSharpExterns(content: string, fileName: string): CSharpExtern[] {
    const externs: CSharpExtern[] = [];

    // [DllImport("__Internal")] 패턴 찾기
    // private static extern void __functionName_Internal(params);
    const externRegex =
      /\[(?:System\.Runtime\.InteropServices\.)?DllImport\s*\(\s*"__Internal"\s*\)\]\s*(?:private\s+)?static\s+extern\s+(\w+)\s+(\w+)\s*\(([^)]*)\)/g;

    let match;
    while ((match = externRegex.exec(content)) !== null) {
      const returnType = match[1];
      const functionName = match[2];
      const paramsStr = match[3];

      // 파라미터 파싱
      const parameters: string[] = [];
      if (paramsStr.trim()) {
        const paramParts = paramsStr.split(',');
        for (const part of paramParts) {
          const trimmed = part.trim();
          if (trimmed) {
            // "string callbackId" -> "string"
            const typeMatch = trimmed.match(/^(\w+)\s+\w+$/);
            if (typeMatch) {
              parameters.push(typeMatch[1]);
            } else {
              parameters.push(trimmed);
            }
          }
        }
      }

      externs.push({
        functionName,
        parameters,
        returnType,
        sourceFile: fileName,
      });
    }

    return externs;
  }

  /**
   * jslib 함수 파싱
   */
  function parseJslibFunctions(content: string, fileName: string): JslibFunction[] {
    const functions: JslibFunction[] = [];

    // mergeInto(LibraryManager.library, { ... }) 내부의 함수 찾기
    // functionName: function(params) { ... }
    const functionRegex = /(\w+)\s*:\s*function\s*\(([^)]*)\)\s*\{/g;

    // 중첩된 콜백 함수는 제외 (onEvent, onError 등)
    const nestedCallbackNames = ['onEvent', 'onError', 'onSuccess', 'onFailure'];

    let match;
    while ((match = functionRegex.exec(content)) !== null) {
      const functionName = match[1];
      const paramsStr = match[2];

      // 중첩 콜백 함수는 건너뛰기
      if (nestedCallbackNames.includes(functionName)) {
        continue;
      }

      // 파라미터 개수 계산
      let parameterCount = 0;
      if (paramsStr.trim()) {
        parameterCount = paramsStr.split(',').filter(p => p.trim()).length;
      }

      // 함수 본문 분석 (대략적으로 다음 함수까지)
      const startIdx = match.index;
      let braceCount = 1;
      let endIdx = content.indexOf('{', startIdx) + 1;

      while (braceCount > 0 && endIdx < content.length) {
        if (content[endIdx] === '{') braceCount++;
        if (content[endIdx] === '}') braceCount--;
        endIdx++;
      }

      const functionBody = content.substring(startIdx, endIdx);

      functions.push({
        functionName,
        parameterCount,
        sourceFile: fileName,
        usesUTF8ToString: functionBody.includes('UTF8ToString'),
        usesSendMessage: functionBody.includes('SendMessage'),
      });
    }

    return functions;
  }

  describe('C# extern ↔ jslib 함수 매칭', () => {
    test('모든 C# extern에 대응하는 jslib 함수가 있어야 함', () => {
      const missingInJslib: string[] = [];

      for (const extern of csharpExterns) {
        const found = jslibFunctions.find(f => f.functionName === extern.functionName);
        if (!found) {
          missingInJslib.push(`${extern.functionName} (from ${extern.sourceFile})`);
        }
      }

      if (missingInJslib.length > 0) {
        console.error(`\n❌ jslib에 없는 C# extern:\n${missingInJslib.join('\n')}`);
      }

      expect(missingInJslib).toHaveLength(0);
    });

    test('모든 jslib 함수에 대응하는 C# extern이 있어야 함', () => {
      const missingInCSharp: string[] = [];

      for (const func of jslibFunctions) {
        const found = csharpExterns.find(e => e.functionName === func.functionName);
        if (!found) {
          missingInCSharp.push(`${func.functionName} (from ${func.sourceFile})`);
        }
      }

      if (missingInCSharp.length > 0) {
        console.warn(`\n⚠️ C# extern이 없는 jslib 함수:\n${missingInCSharp.join('\n')}`);
        // 이것은 경고만 (jslib에 추가 유틸리티 함수가 있을 수 있음)
      }

      // 대부분은 매칭되어야 함 (80% 이상)
      const matchRate = (jslibFunctions.length - missingInCSharp.length) / jslibFunctions.length;
      expect(matchRate).toBeGreaterThanOrEqual(0.8);
    });
  });

  describe('파라미터 수 일관성', () => {
    test('C# extern과 jslib 함수의 파라미터 수가 일치해야 함', () => {
      const parameterMismatches: string[] = [];

      for (const extern of csharpExterns) {
        const jslibFunc = jslibFunctions.find(f => f.functionName === extern.functionName);
        if (jslibFunc) {
          if (extern.parameters.length !== jslibFunc.parameterCount) {
            parameterMismatches.push(
              `${extern.functionName}: C# has ${extern.parameters.length} params, jslib has ${jslibFunc.parameterCount}`
            );
          }
        }
      }

      if (parameterMismatches.length > 0) {
        console.error(`\n❌ 파라미터 수 불일치:\n${parameterMismatches.join('\n')}`);
      }

      expect(parameterMismatches).toHaveLength(0);
    });
  });

  describe('함수 명명 패턴', () => {
    test('C# extern 함수명이 __name_Internal 패턴을 따라야 함', () => {
      const invalidNames: string[] = [];

      for (const extern of csharpExterns) {
        // __functionName_Internal 또는 _functionName 패턴
        const isValid =
          extern.functionName.startsWith('__') && extern.functionName.endsWith('_Internal');

        if (!isValid) {
          invalidNames.push(`${extern.functionName} (from ${extern.sourceFile})`);
        }
      }

      if (invalidNames.length > 0) {
        console.warn(`\n⚠️ 명명 패턴 불일치:\n${invalidNames.join('\n')}`);
      }

      // 대부분은 패턴을 따라야 함
      const validRate = (csharpExterns.length - invalidNames.length) / csharpExterns.length;
      expect(validRate).toBeGreaterThanOrEqual(0.9);
    });
  });

  describe('UTF8ToString 사용 패턴', () => {
    test('string 파라미터가 있는 jslib 함수는 UTF8ToString을 사용해야 함', () => {
      const missingUTF8ToString: string[] = [];

      for (const extern of csharpExterns) {
        // string 파라미터가 있는 경우
        const hasStringParam = extern.parameters.includes('string');

        if (hasStringParam) {
          const jslibFunc = jslibFunctions.find(f => f.functionName === extern.functionName);
          if (jslibFunc && !jslibFunc.usesUTF8ToString) {
            missingUTF8ToString.push(`${extern.functionName} (from ${jslibFunc.sourceFile})`);
          }
        }
      }

      if (missingUTF8ToString.length > 0) {
        console.error(`\n❌ UTF8ToString 누락:\n${missingUTF8ToString.join('\n')}`);
      }

      expect(missingUTF8ToString).toHaveLength(0);
    });
  });

  describe('SendMessage 콜백 패턴', () => {
    test('void 반환 함수는 SendMessage 콜백을 사용해야 함', () => {
      const missingSendMessage: string[] = [];

      // 콜백이 필요 없는 동기 함수 목록 (예외)
      const syncFunctions = [
        '__AITUnsubscribe_Internal', // 구독 해제는 동기적으로 처리됨
        '__AITRespondToNestedCallback', // 중첩 콜백 응답 (동기적으로 결과 전달)
      ];

      for (const extern of csharpExterns) {
        // void 반환 타입인 경우 (비동기 API)
        if (extern.returnType === 'void') {
          // 동기 함수는 제외
          if (syncFunctions.includes(extern.functionName)) {
            continue;
          }

          const jslibFunc = jslibFunctions.find(f => f.functionName === extern.functionName);
          if (jslibFunc && !jslibFunc.usesSendMessage) {
            missingSendMessage.push(`${extern.functionName} (from ${jslibFunc.sourceFile})`);
          }
        }
      }

      if (missingSendMessage.length > 0) {
        console.error(`\n❌ SendMessage 누락:\n${missingSendMessage.join('\n')}`);
      }

      expect(missingSendMessage).toHaveLength(0);
    });
  });

  describe('바인딩 통계', () => {
    test('바인딩 커버리지 리포트', () => {
      const totalExterns = csharpExterns.length;
      const totalJslib = jslibFunctions.length;

      let matchedCount = 0;
      let paramMismatchCount = 0;

      for (const extern of csharpExterns) {
        const jslibFunc = jslibFunctions.find(f => f.functionName === extern.functionName);
        if (jslibFunc) {
          matchedCount++;
          if (extern.parameters.length !== jslibFunc.parameterCount) {
            paramMismatchCount++;
          }
        }
      }

      console.log('\n📊 바인딩 통계:');
      console.log(`   C# extern 총 개수: ${totalExterns}`);
      console.log(`   jslib 함수 총 개수: ${totalJslib}`);
      console.log(`   매칭된 바인딩: ${matchedCount}`);
      console.log(`   파라미터 불일치: ${paramMismatchCount}`);
      console.log(`   바인딩 커버리지: ${((matchedCount / totalExterns) * 100).toFixed(1)}%\n`);

      // 최소 90% 이상 커버리지
      expect(matchedCount / totalExterns).toBeGreaterThanOrEqual(0.9);
    });
  });
});
