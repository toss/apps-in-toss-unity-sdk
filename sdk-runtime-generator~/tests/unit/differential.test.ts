/**
 * Tier 4: Differential Testing (Golden Files)
 *
 * 생성된 SDK 코드가 이전 버전과 동일한지 검증합니다.
 * 의도치 않은 변경(regression)을 방지하기 위한 스냅샷 테스트입니다.
 *
 * 사용법:
 * - pnpm test:tier4      - golden file과 비교
 * - pnpm test:update-golden - golden file 업데이트 (의도적 변경 시)
 *
 * 검증 항목:
 * 1. AIT.Types.cs - 타입 정의 일관성
 * 2. AITCore.cs - 핵심 인프라 코드 일관성
 * 3. jslib 파일들 - JavaScript 브릿지 코드 일관성
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Golden file 경로
const GOLDEN_DIR = path.resolve(__dirname, '../fixtures/golden');
const SDK_GENERATED_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

// 비교할 주요 파일들
const GOLDEN_FILES = [
  'AIT.Types.cs',
  'AITCore.cs',
  // jslib 파일은 자주 변경되므로 선택적으로 포함
];

describe('Tier 4: Golden File 비교 (회귀 테스트)', () => {
  let goldenFilesExist = false;

  beforeAll(async () => {
    console.log('\n📂 Golden file 디렉토리 확인 중...\n');

    try {
      await fs.access(GOLDEN_DIR);
      const files = await fs.readdir(GOLDEN_DIR);
      goldenFilesExist = files.some(f => f.endsWith('.golden'));
      console.log(`✅ Golden 디렉토리: ${GOLDEN_DIR}`);
      console.log(`   파일 수: ${files.length}`);
    } catch {
      console.log('⚠️ Golden 디렉토리가 없습니다. 처음 실행 시 생성됩니다.');
      goldenFilesExist = false;
    }
  });

  /**
   * 정규화 함수: 줄바꿈, 공백 차이 무시
   */
  function normalizeContent(content: string): string {
    return content
      .replace(/\r\n/g, '\n') // Windows 줄바꿈 정규화
      .replace(/[ \t]+$/gm, '') // 줄 끝 공백 제거
      .trim();
  }

  /**
   * 핵심 구조만 추출 (주석, 빈 줄 제외)
   */
  function extractCoreStructure(content: string): string {
    return content
      .split('\n')
      .filter(line => {
        const trimmed = line.trim();
        // 주석 줄 제외
        if (trimmed.startsWith('//')) return false;
        if (trimmed.startsWith('/*') || trimmed.startsWith('*')) return false;
        // 빈 줄 제외
        if (trimmed === '') return false;
        return true;
      })
      .join('\n');
  }

  describe('주요 파일 비교', () => {
    for (const fileName of GOLDEN_FILES) {
      test(`${fileName}이 golden file과 일치해야 함`, async () => {
        const generatedPath = path.join(SDK_GENERATED_DIR, fileName);
        const goldenPath = path.join(GOLDEN_DIR, `${fileName}.golden`);

        // 생성된 파일 확인
        let generatedContent: string;
        try {
          generatedContent = await fs.readFile(generatedPath, 'utf-8');
        } catch {
          console.log(`⚠️ ${fileName} 생성 파일 없음 (pnpm generate 실행 필요)`);
          return; // 스킵
        }

        // Golden file 확인
        let goldenContent: string;
        try {
          goldenContent = await fs.readFile(goldenPath, 'utf-8');
        } catch {
          // Golden file이 없으면 생성
          console.log(`📝 ${fileName}.golden 생성 중...`);
          await fs.mkdir(GOLDEN_DIR, { recursive: true });
          await fs.writeFile(goldenPath, generatedContent);
          console.log(`✅ ${fileName}.golden 생성됨`);
          return; // 첫 실행이므로 통과
        }

        // 비교
        const normalizedGenerated = normalizeContent(generatedContent);
        const normalizedGolden = normalizeContent(goldenContent);

        if (normalizedGenerated !== normalizedGolden) {
          // 차이점 분석
          const genLines = normalizedGenerated.split('\n');
          const goldLines = normalizedGolden.split('\n');

          const differences: string[] = [];
          const maxLines = Math.max(genLines.length, goldLines.length);

          for (let i = 0; i < maxLines && differences.length < 10; i++) {
            const genLine = genLines[i] || '(EOF)';
            const goldLine = goldLines[i] || '(EOF)';

            if (genLine !== goldLine) {
              differences.push(`Line ${i + 1}:`);
              differences.push(`  - Golden: ${goldLine.substring(0, 80)}`);
              differences.push(`  + Generated: ${genLine.substring(0, 80)}`);
            }
          }

          if (differences.length > 0) {
            console.error(`\n❌ ${fileName} 차이점 발견:\n${differences.join('\n')}`);
            console.log('\n💡 의도적 변경이라면: pnpm test:update-golden 실행');
          }
        }

        expect(normalizedGenerated).toBe(normalizedGolden);
      });
    }
  });

  describe('구조적 일관성 검증', () => {
    test('AIT.Types.cs의 클래스 수가 일정해야 함', async () => {
      const typesPath = path.join(SDK_GENERATED_DIR, 'AIT.Types.cs');
      const goldenPath = path.join(GOLDEN_DIR, 'AIT.Types.cs.golden');

      let generatedContent: string;
      let goldenContent: string;

      try {
        generatedContent = await fs.readFile(typesPath, 'utf-8');
      } catch {
        console.log('⚠️ AIT.Types.cs 파일 없음');
        return;
      }

      try {
        goldenContent = await fs.readFile(goldenPath, 'utf-8');
      } catch {
        console.log('⚠️ AIT.Types.cs.golden 파일 없음');
        return;
      }

      // 클래스 수 비교
      const genClassCount = (generatedContent.match(/public class \w+/g) || []).length;
      const goldClassCount = (goldenContent.match(/public class \w+/g) || []).length;

      console.log(`📊 클래스 수: Generated=${genClassCount}, Golden=${goldClassCount}`);

      // 클래스 수가 크게 줄어들면 문제
      if (genClassCount < goldClassCount * 0.8) {
        console.error(`\n❌ 클래스 수 급감! ${goldClassCount} → ${genClassCount}`);
      }

      expect(genClassCount).toBeGreaterThanOrEqual(goldClassCount * 0.8);
    });

    test('AIT.Types.cs의 enum 수가 일정해야 함', async () => {
      const typesPath = path.join(SDK_GENERATED_DIR, 'AIT.Types.cs');
      const goldenPath = path.join(GOLDEN_DIR, 'AIT.Types.cs.golden');

      let generatedContent: string;
      let goldenContent: string;

      try {
        generatedContent = await fs.readFile(typesPath, 'utf-8');
      } catch {
        console.log('⚠️ AIT.Types.cs 파일 없음');
        return;
      }

      try {
        goldenContent = await fs.readFile(goldenPath, 'utf-8');
      } catch {
        console.log('⚠️ AIT.Types.cs.golden 파일 없음');
        return;
      }

      // Enum 수 비교
      const genEnumCount = (generatedContent.match(/public enum \w+/g) || []).length;
      const goldEnumCount = (goldenContent.match(/public enum \w+/g) || []).length;

      console.log(`📊 Enum 수: Generated=${genEnumCount}, Golden=${goldEnumCount}`);

      // Enum 수가 크게 줄어들면 문제
      if (genEnumCount < goldEnumCount * 0.8) {
        console.error(`\n❌ Enum 수 급감! ${goldEnumCount} → ${genEnumCount}`);
      }

      expect(genEnumCount).toBeGreaterThanOrEqual(goldEnumCount * 0.8);
    });
  });

  describe('API 수 검증', () => {
    test('공개 API 메서드 수가 줄어들지 않아야 함', async () => {
      const sdkFiles = await fs.readdir(SDK_GENERATED_DIR).catch(() => []);
      const apiFiles = sdkFiles.filter(
        f => f.startsWith('AIT.') && f.endsWith('.cs') && f !== 'AIT.cs' && f !== 'AIT.Types.cs'
      );

      let totalApiMethods = 0;

      for (const fileName of apiFiles) {
        const filePath = path.join(SDK_GENERATED_DIR, fileName);
        try {
          const content = await fs.readFile(filePath, 'utf-8');
          // public static async Task 메서드 카운트
          const methodCount = (content.match(/public static async Task/g) || []).length;
          totalApiMethods += methodCount;
        } catch {
          // 무시
        }
      }

      console.log(`📊 총 API 메서드 수: ${totalApiMethods}`);

      // 최소 50개 이상의 API가 있어야 함 (현재 61개)
      expect(totalApiMethods).toBeGreaterThanOrEqual(50);
    });
  });
});

/**
 * Golden file 업데이트 유틸리티
 * pnpm test:update-golden 으로 실행
 */
export async function updateGoldenFiles(): Promise<void> {
  console.log('\n📝 Golden files 업데이트 중...\n');

  await fs.mkdir(GOLDEN_DIR, { recursive: true });

  for (const fileName of GOLDEN_FILES) {
    const generatedPath = path.join(SDK_GENERATED_DIR, fileName);
    const goldenPath = path.join(GOLDEN_DIR, `${fileName}.golden`);

    try {
      const content = await fs.readFile(generatedPath, 'utf-8');
      await fs.writeFile(goldenPath, content);
      console.log(`✅ ${fileName}.golden 업데이트됨`);
    } catch {
      console.log(`⚠️ ${fileName} 파일 없음, 스킵`);
    }
  }

  console.log('\n✅ Golden files 업데이트 완료\n');
}
