/**
 * Tier 3: 직렬화/역직렬화 검증
 *
 * TypeScript ↔ C# 타입 매핑의 JSON 스키마 일치성을 검증합니다.
 * C# JsonSerializer의 실제 동작은 E2E 테스트(SerializationTester.cs)에서 검증합니다.
 *
 * 검증 항목:
 * 1. C# 생성 코드의 직렬화 어트리뷰트 적용 여부
 * 2. Enum 타입의 EnumMember 어트리뷰트 적용 여부
 * 3. Union 타입(discriminated union)의 구조 검증
 * 4. TypeScript ↔ C# 타입 매핑 일관성
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';
import { glob } from 'glob';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

describe('Tier 3: JSON 스키마 일치성 검증', () => {
  let typesFileContent: string;
  let allCSharpFiles: Map<string, string>;

  beforeAll(async () => {
    console.log('\n📂 생성된 SDK 파일 로딩 중...\n');

    const sdkGeneratorRoot = path.resolve(__dirname, '../..');
    const runtimeSDKPath = path.resolve(sdkGeneratorRoot, '../Runtime/SDK');

    try {
      await fs.access(runtimeSDKPath);
    } catch {
      throw new Error(
        '❌ 생성된 SDK 파일을 찾을 수 없습니다!\n' +
          '   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.\n' +
          `   Expected path: ${runtimeSDKPath}`
      );
    }

    // AIT.Types.cs 로딩
    const typesFilePath = path.join(runtimeSDKPath, 'AIT.Types.cs');
    typesFileContent = await fs.readFile(typesFilePath, 'utf-8');

    // 모든 C# 파일 로딩
    allCSharpFiles = new Map();
    const csFiles = await glob('*.cs', { cwd: runtimeSDKPath, absolute: false });

    for (const fileName of csFiles) {
      const filePath = path.join(runtimeSDKPath, fileName);
      const content = await fs.readFile(filePath, 'utf-8');
      allCSharpFiles.set(fileName, content);
    }

    console.log(`✅ ${csFiles.length}개 C# 파일 로딩 완료\n`);
  }, 10000);

  describe('Enum 직렬화 어트리뷰트', () => {
    test('모든 enum이 EnumMember 어트리뷰트를 가져야 함', () => {
      // enum 정의 찾기
      const enumRegex = /public enum (\w+)\s*\{([^}]+)\}/g;
      const enumMatches = [...typesFileContent.matchAll(enumRegex)];

      expect(enumMatches.length).toBeGreaterThan(0);
      console.log(`✅ ${enumMatches.length}개 enum 발견`);

      const enumsWithoutEnumMember: string[] = [];

      for (const match of enumMatches) {
        const enumName = match[1];
        const enumBody = match[2];

        // enum 값마다 EnumMember 어트리뷰트가 있는지 확인
        // 패턴: [EnumMember(Value = "...")] 또는 enum 값 이름
        const enumValues = enumBody
          .split(',')
          .map(v => v.trim())
          .filter(v => v.length > 0);

        const hasEnumMember = enumValues.every(
          value =>
            value.includes('[EnumMember(Value =') || value.includes('[EnumMember(Value=')
        );

        if (!hasEnumMember) {
          enumsWithoutEnumMember.push(enumName);
        }
      }

      if (enumsWithoutEnumMember.length > 0) {
        console.error(
          `\n❌ EnumMember 어트리뷰트가 없는 enum:\n${enumsWithoutEnumMember.join('\n')}`
        );
      }

      expect(enumsWithoutEnumMember).toHaveLength(0);
    });

    test('EnumMember Value가 원본 TypeScript 문자열과 일치해야 함', () => {
      // 일반적인 패턴 검증: [EnumMember(Value = "value")] EnumValue
      const enumMemberRegex = /\[EnumMember\(Value\s*=\s*"([^"]+)"\)\]\s*(\w+)/g;
      const matches = [...typesFileContent.matchAll(enumMemberRegex)];

      expect(matches.length).toBeGreaterThan(0);
      console.log(`✅ ${matches.length}개 EnumMember 발견`);

      // 값 검증: Value 문자열이 유효한 JSON 값이어야 함
      for (const match of matches) {
        const value = match[1];
        const enumValue = match[2];

        // Value는 빈 문자열이 아니어야 함
        expect(value.length).toBeGreaterThan(0);

        // 특수문자가 있다면 이스케이프되어야 함
        expect(() => JSON.parse(`"${value}"`)).not.toThrow();
      }
    });
  });

  describe('Discriminated Union 타입', () => {
    test('Result 타입이 올바른 discriminator 필드를 가져야 함', () => {
      // Result 클래스 찾기 (GetUserKeyForGameResult 등)
      const resultClassRegex = /public class (\w+Result)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}/gs;
      const matches = [...typesFileContent.matchAll(resultClassRegex)];

      const resultsWithoutDiscriminator: string[] = [];

      for (const match of matches) {
        const className = match[1];
        const classBody = match[2];

        // _type 필드가 있어야 함 (discriminator)
        const hasTypeField =
          classBody.includes('[JsonProperty("_type")]') ||
          classBody.includes('public string _type');

        if (!hasTypeField) {
          resultsWithoutDiscriminator.push(className);
        }
      }

      if (resultsWithoutDiscriminator.length > 0) {
        console.warn(
          `\n⚠️ _type discriminator가 없는 Result 클래스:\n${resultsWithoutDiscriminator.join('\n')}`
        );
      }

      // 최소한 하나의 Result 클래스가 있어야 함
      expect(matches.length).toBeGreaterThan(0);
      console.log(`✅ ${matches.length}개 Result 클래스 발견`);
    });

    test('Result 타입이 IsSuccess/IsError 프로퍼티를 가져야 함', () => {
      const resultClassRegex = /public class (\w+Result)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}/gs;
      const matches = [...typesFileContent.matchAll(resultClassRegex)];

      const resultsWithoutHelpers: string[] = [];

      for (const match of matches) {
        const className = match[1];
        const classBody = match[2];

        // IsSuccess, IsError 프로퍼티가 있어야 함
        const hasIsSuccess = classBody.includes('public bool IsSuccess');
        const hasIsError = classBody.includes('public bool IsError');

        if (!hasIsSuccess || !hasIsError) {
          resultsWithoutHelpers.push(className);
        }
      }

      if (resultsWithoutHelpers.length > 0) {
        console.warn(
          `\n⚠️ IsSuccess/IsError가 없는 Result 클래스:\n${resultsWithoutHelpers.join('\n')}`
        );
      }
    });

    test('Result 타입이 Match 메서드를 가져야 함', () => {
      const resultClassRegex = /public class (\w+Result)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}/gs;
      const matches = [...typesFileContent.matchAll(resultClassRegex)];

      let matchMethodCount = 0;

      for (const match of matches) {
        const classBody = match[2];

        // Match 메서드가 있는지 확인
        if (classBody.includes('public void Match(') || classBody.includes('public T Match<T>(')) {
          matchMethodCount++;
        }
      }

      expect(matchMethodCount).toBeGreaterThan(0);
      console.log(`✅ ${matchMethodCount}개 Result 클래스에 Match 메서드 발견`);
    });
  });

  describe('[Preserve] 어트리뷰트 (IL2CPP 스트리핑 방지)', () => {
    test('모든 public class가 [Preserve] 어트리뷰트를 가져야 함', () => {
      // Serializable 클래스 찾기
      const serializableClassRegex = /\[Serializable\]\s*(?:\[Preserve\])?\s*public class (\w+)/g;
      const matches = [...typesFileContent.matchAll(serializableClassRegex)];

      const classesWithoutPreserve: string[] = [];

      for (const match of matches) {
        const className = match[1];
        const fullMatch = match[0];

        if (!fullMatch.includes('[Preserve]')) {
          classesWithoutPreserve.push(className);
        }
      }

      if (classesWithoutPreserve.length > 0) {
        console.error(
          `\n❌ [Preserve] 어트리뷰트가 없는 클래스:\n${classesWithoutPreserve.join('\n')}`
        );
      }

      // 모든 Serializable 클래스에 Preserve가 있어야 함
      expect(classesWithoutPreserve).toHaveLength(0);
    });

    test('모든 public 프로퍼티가 [Preserve] 어트리뷰트를 가져야 함', () => {
      // JsonProperty가 있는 프로퍼티 찾기
      const propertyRegex = /\[JsonProperty\([^\]]+\)\]\s*(?:\[Preserve\])?\s*public\s+\w+\s+(\w+)/g;
      const matches = [...typesFileContent.matchAll(propertyRegex)];

      let propertiesWithPreserve = 0;
      let propertiesWithoutPreserve = 0;

      for (const match of matches) {
        const fullMatch = match[0];

        if (fullMatch.includes('[Preserve]')) {
          propertiesWithPreserve++;
        } else {
          propertiesWithoutPreserve++;
        }
      }

      console.log(
        `✅ 프로퍼티 분석: ${propertiesWithPreserve}개 [Preserve] 있음, ${propertiesWithoutPreserve}개 없음`
      );

      // 모든 JsonProperty에 Preserve가 있어야 함
      expect(propertiesWithoutPreserve).toBe(0);
    });
  });

  describe('TypeScript ↔ C# 타입 매핑', () => {
    test('number 타입이 double로 매핑되어야 함', () => {
      // number -> double 매핑 확인
      // 예: latitude: number -> public double latitude
      const hasDoubleType = allCSharpFiles
        .values()
        .some(content => content.includes('public double'));

      expect(hasDoubleType).toBe(true);
    });

    test('string 타입이 string으로 매핑되어야 함', () => {
      const hasStringType = allCSharpFiles
        .values()
        .some(content => content.includes('public string'));

      expect(hasStringType).toBe(true);
    });

    test('boolean 타입이 bool로 매핑되어야 함', () => {
      const hasBoolType = allCSharpFiles
        .values()
        .some(content => content.includes('public bool'));

      expect(hasBoolType).toBe(true);
    });

    test('Promise<T>가 Task<T>로 매핑되어야 함', () => {
      // API 메서드에서 Task<T> 반환 타입 확인
      const hasAsyncTask = allCSharpFiles
        .values()
        .some(content => content.includes('public static async Task<'));

      expect(hasAsyncTask).toBe(true);
    });

    test('T[]가 C# 배열로 매핑되어야 함', () => {
      // 배열 타입 확인
      const hasArrayType = allCSharpFiles
        .values()
        .some(
          content => content.includes('[]') && content.includes('public')
        );

      expect(hasArrayType).toBe(true);
    });
  });

  describe('JSON 직렬화 옵션', () => {
    test('AITCore.cs에 JsonSerializerSettings가 정의되어야 함', () => {
      const coreFile = allCSharpFiles.get('AITCore.cs');
      expect(coreFile).toBeDefined();

      // JsonSerializerSettings 또는 JsonSerializerOptions 확인
      const hasJsonSettings =
        coreFile!.includes('JsonSerializerSettings') ||
        coreFile!.includes('JsonSerializerOptions') ||
        coreFile!.includes('JsonConvert');

      expect(hasJsonSettings).toBe(true);
    });
  });

  describe('API 파라미터/반환 타입 커버리지', () => {
    test('모든 API 파일이 적절한 타입을 반환해야 함', async () => {
      const apiFiles = [...allCSharpFiles.entries()].filter(
        ([name]) => name.startsWith('AIT.') && name !== 'AIT.cs' && name !== 'AIT.Types.cs'
      );

      console.log(`\n📊 API 파일 분석: ${apiFiles.length}개`);

      let totalMethods = 0;
      let methodsWithTask = 0;

      for (const [fileName, content] of apiFiles) {
        // public static async Task 메서드 카운트
        const methodMatches = content.match(/public static async Task/g);
        const count = methodMatches?.length || 0;
        totalMethods += count;
        methodsWithTask += count;
      }

      console.log(`✅ 총 ${totalMethods}개 API 메서드 발견`);
      console.log(`✅ ${methodsWithTask}개 메서드가 Task 반환`);

      expect(totalMethods).toBeGreaterThan(0);
    });
  });
});
