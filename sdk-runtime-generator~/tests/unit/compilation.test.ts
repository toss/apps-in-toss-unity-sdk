/**
 * Tier 1: 컴파일 가능성 검증
 *
 * 실제 컴파일러(Roslyn/Mono mcs, TypeScript Compiler API)를 사용하여
 * 생성된 코드가 실제로 컴파일 가능한지 검증합니다.
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';
import { glob } from 'glob';
import { compileCSharp, compileCSharpFiles, printCompilationResult } from './helpers/roslyn-compiler.js';
import { validateJavaScriptSyntax, validateMergeIntoSyntax, printValidationResult } from './helpers/ts-compiler.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

describe('Tier 1: 컴파일 가능성 검증', () => {
  let csharpFiles: { [filename: string]: string };
  let jslibFiles: Map<string, string>;

  beforeAll(async () => {
    console.log('\n📂 생성된 SDK 파일 로딩 중...\n');

    // sdk-runtime-generator 루트 경로
    const sdkGeneratorRoot = path.resolve(__dirname, '../..');

    // 생성된 SDK 경로
    const runtimeSDKPath = path.resolve(sdkGeneratorRoot, '../Runtime/SDK');
    const pluginsPath = path.join(runtimeSDKPath, 'Plugins');

    // 생성된 파일 존재 확인
    try {
      await fs.access(runtimeSDKPath);
    } catch {
      throw new Error(
        '❌ 생성된 SDK 파일을 찾을 수 없습니다!\n' +
        '   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.\n' +
        `   Expected path: ${runtimeSDKPath}`
      );
    }

    // C# 파일들 로딩
    csharpFiles = {};
    const csFiles = await glob('*.cs', { cwd: runtimeSDKPath, absolute: false });

    console.log(`✅ ${csFiles.length}개 C# 파일 발견`);

    for (const fileName of csFiles) {
      const filePath = path.join(runtimeSDKPath, fileName);
      const content = await fs.readFile(filePath, 'utf-8');
      csharpFiles[fileName] = content;
    }

    // JavaScript 브릿지 파일들 로딩
    jslibFiles = new Map();
    const jslibFilesList = await glob('*.jslib', { cwd: pluginsPath, absolute: false });

    console.log(`✅ ${jslibFilesList.length}개 jslib 파일 발견\n`);

    for (const fileName of jslibFilesList) {
      const filePath = path.join(pluginsPath, fileName);
      const content = await fs.readFile(filePath, 'utf-8');
      jslibFiles.set(fileName, content);
    }

    console.log('✅ 파일 로딩 완료\n');
  }, 10000); // 10초 타임아웃

  describe('C# 컴파일 검증', () => {
    test('AIT.cs (메인 partial class)가 컴파일 가능해야 함', async () => {
      // 메인 파일은 선언만 있고 실제 메서드는 없으므로 단독 컴파일 가능
      const result = await compileCSharp(csharpFiles['AIT.cs'], {
        references: [
          'UnityEngine.dll',
          'Newtonsoft.Json.dll',
          'System.dll',
        ],
        allowUnsafe: false,
      });

      if (!result.success) {
        console.error('\n❌ AIT.cs (메인) 컴파일 실패:');
        printCompilationResult(result);
      }

      expect(result.success).toBe(true);
      expect(result.errors).toHaveLength(0);
    }, 30000);

    test('AITCore.cs가 컴파일 가능해야 함', async () => {
      // AITCore.cs는 AIT.Types.cs에 정의된 타입들을 사용하므로 함께 컴파일
      // AITVisibilityHelper는 Runtime/Helpers/에 별도 위치하며 AITCore의 이벤트를 구독함
      const result = await compileCSharpFiles({
        'AITCore.cs': csharpFiles['AITCore.cs'],
        'AIT.Types.cs': csharpFiles['AIT.Types.cs'],
      }, {
        references: [
          'UnityEngine.dll',
          'Newtonsoft.Json.dll',
          'System.dll',
        ],
      });

      if (!result.success) {
        console.error('\n❌ AITCore.cs 컴파일 실패:');
        printCompilationResult(result);
      }

      expect(result.success).toBe(true);
      expect(result.errors).toHaveLength(0);
    }, 30000);

    test('AIT.Types.cs가 컴파일 가능해야 함', async () => {
      const result = await compileCSharp(csharpFiles['AIT.Types.cs'], {
        references: ['System.dll', 'UnityEngine.dll', 'Newtonsoft.Json.dll'],
      });

      if (!result.success) {
        console.error('\n❌ AIT.Types.cs 컴파일 실패:');
        printCompilationResult(result);
      }

      expect(result.success).toBe(true);
      expect(result.errors).toHaveLength(0);
    }, 30000);

    test('모든 C# 파일이 함께 컴파일 가능해야 함 (partial class 통합)', async () => {
      // ⭐ 핵심: 모든 partial class 파일들을 함께 컴파일
      // Unity에서 실제로 사용되는 방식과 동일
      try {
        const result = await compileCSharpFiles(csharpFiles, {
          references: [
            'UnityEngine.dll',
            'Newtonsoft.Json.dll',
            // System.Runtime.InteropServices는 System.dll에 포함되어 있음 (Mono)
            'System.dll',
          ],
        });

        if (!result.success) {
          console.error('\n❌ 전체 C# 컴파일 실패 (partial class 통합):');
          printCompilationResult(result);

          // 어떤 파일에서 오류가 발생했는지 상세 정보 출력
          const fileCount = Object.keys(csharpFiles).length;
          console.error(`\n📊 컴파일 시도한 파일: ${fileCount}개`);
          console.error('   - AIT.cs (메인)');
          console.error(`   - AIT.*.cs (${fileCount - 3}개 partial API 파일)`);
          console.error('   - AITCore.cs');
          console.error('   - AIT.Types.cs');
        }

        expect(result.success).toBe(true);
        expect(result.errors).toHaveLength(0);
      } catch (error) {
        if (error instanceof Error && error.message.includes('Compiler not found')) {
          console.error('\n❌ C# 컴파일러가 설치되지 않았습니다!');
          console.error('\n📦 설치 방법:');
          console.error('   macOS/Linux: brew install mono');
          console.error('   Windows: .NET SDK 설치 (https://dotnet.microsoft.com)');
          console.error('\n자세한 내용은 sdk-runtime-generator/README.md를 참고하세요.');
        }
        throw error;
      }
    }, 60000); // partial class 파일이 많아서 타임아웃 증가
  });

  describe('JavaScript 문법 검증', () => {
    test('모든 .jslib 파일이 유효한 JavaScript 문법이어야 함', () => {
      let failedFiles: string[] = [];

      for (const [filename, code] of jslibFiles.entries()) {
        const result = validateJavaScriptSyntax(code, filename);

        if (!result.valid) {
          failedFiles.push(filename);
          console.error(`\n❌ ${filename} 문법 오류:`);
          printValidationResult(result);
        }

        expect(result.valid).toBe(true);
      }

      if (failedFiles.length > 0) {
        console.error(`\n실패한 파일: ${failedFiles.join(', ')}`);
      }
    });

    test('모든 .jslib 파일이 올바른 mergeInto 패턴을 사용해야 함', () => {
      let failedFiles: string[] = [];

      for (const [filename, code] of jslibFiles.entries()) {
        const result = validateMergeIntoSyntax(code);

        if (!result.valid) {
          failedFiles.push(filename);
          console.error(`\n❌ ${filename} mergeInto 패턴 오류:`);
          printValidationResult(result);
        }

        expect(result.valid).toBe(true);
      }

      if (failedFiles.length > 0) {
        console.error(`\n실패한 파일: ${failedFiles.join(', ')}`);
      }
    });
  });
});
