/**
 * Differential Testing (Golden Files)
 *
 * 생성된 SDK 코드가 이전 버전과 동일한지 검증합니다.
 * 의도치 않은 변경(regression)을 방지하기 위한 골든 파일 기반 회귀 테스트입니다.
 *
 * 사용법:
 * - pnpm test:differential  - golden file과 비교
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
import { readdirSync } from 'node:fs';
import * as fs from 'fs/promises';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// public class 또는 public partial class를 매칭하는 공통 패턴
const CLASS_PATTERN = `public (?:partial )?class`;

// Golden file 경로
const GOLDEN_DIR = path.resolve(__dirname, '../fixtures/golden');
const SDK_GENERATED_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

// 카테고리별 타입 파일(AIT.Types.{Category}.cs) 매칭 — 구버전 단일 AIT.Types.cs는 매칭 안 됨
const TYPE_FILE_RE = /^AIT\.Types\..+\.cs$/;

/**
 * 비교할 주요 파일 목록을 동적으로 수집한다.
 * 타입 정의는 카테고리별로 분할(AIT.Types.{Category}.cs)되므로 생성 디렉토리와
 * golden 디렉토리 양쪽에서 발견되는 타입 파일의 합집합을 대상으로 한다
 * (카테고리 추가/삭제 모두 회귀로 검출). AITCore.cs는 항상 포함.
 */
function discoverGoldenFiles(): string[] {
  const typeFiles = new Set<string>();
  try {
    for (const f of readdirSync(SDK_GENERATED_DIR)) {
      if (TYPE_FILE_RE.test(f)) typeFiles.add(f);
    }
  } catch { /* generate 전이면 비어있음 */ }
  try {
    for (const f of readdirSync(GOLDEN_DIR)) {
      if (f.endsWith('.golden') && TYPE_FILE_RE.test(f.slice(0, -'.golden'.length))) {
        typeFiles.add(f.slice(0, -'.golden'.length));
      }
    }
  } catch { /* golden 디렉토리 없음 */ }
  return [...Array.from(typeFiles).sort(), 'AITCore.cs'];
}

// 비교할 주요 파일들 (jslib 파일은 자주 변경되므로 선택적으로 포함)
const GOLDEN_FILES = discoverGoldenFiles();

/**
 * 분할된 타입 파일들을 모아 하나의 문자열로 결합 (집계 카운트용).
 * @param dir 대상 디렉토리
 * @param golden true면 *.golden 접미사 기준으로 수집
 */
async function readAllTypeFiles(dir: string, golden: boolean): Promise<string> {
  const all = await fs.readdir(dir).catch(() => [] as string[]);
  const matched = all
    .filter(f => golden
      ? f.endsWith('.golden') && TYPE_FILE_RE.test(f.slice(0, -'.golden'.length))
      : TYPE_FILE_RE.test(f))
    .sort();
  const parts = await Promise.all(matched.map(f => fs.readFile(path.join(dir, f), 'utf-8')));
  return parts.join('\n');
}

describe('Golden File 비교 (회귀 테스트)', () => {
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
    test('AIT.Types.*.cs의 클래스 수가 일정해야 함', async () => {
      // 타입 정의는 카테고리별 파일로 분할되므로 전체 AIT.Types.*.cs를 집계해 비교
      const generatedContent = await readAllTypeFiles(SDK_GENERATED_DIR, false);
      const goldenContent = await readAllTypeFiles(GOLDEN_DIR, true);

      if (!generatedContent) {
        console.log('⚠️ AIT.Types.*.cs 생성 파일 없음');
        return;
      }
      if (!goldenContent) {
        console.log('⚠️ AIT.Types.*.cs.golden 파일 없음');
        return;
      }

      // 클래스 수 비교
      const classRegex = new RegExp(`${CLASS_PATTERN} \\w+`, 'g');
      const genClassCount = (generatedContent.match(classRegex) || []).length;
      const goldClassCount = (goldenContent.match(classRegex) || []).length;

      console.log(`📊 클래스 수: Generated=${genClassCount}, Golden=${goldClassCount}`);

      // 클래스 수가 크게 줄어들면 문제
      if (genClassCount < goldClassCount * 0.8) {
        console.error(`\n❌ 클래스 수 급감! ${goldClassCount} → ${genClassCount}`);
      }

      expect(genClassCount).toBeGreaterThanOrEqual(goldClassCount * 0.8);
    });

    test('AIT.Types.*.cs의 enum 수가 일정해야 함', async () => {
      const generatedContent = await readAllTypeFiles(SDK_GENERATED_DIR, false);
      const goldenContent = await readAllTypeFiles(GOLDEN_DIR, true);

      if (!generatedContent) {
        console.log('⚠️ AIT.Types.*.cs 생성 파일 없음');
        return;
      }
      if (!goldenContent) {
        console.log('⚠️ AIT.Types.*.cs.golden 파일 없음');
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
        f => f.startsWith('AIT.') && f.endsWith('.cs') && f !== 'AIT.cs' && !f.startsWith('AIT.Types')
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
