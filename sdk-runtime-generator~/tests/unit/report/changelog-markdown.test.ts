/**
 * Changelog 마크다운 렌더러 golden 테스트
 *
 * 합성(fake) 버전 데이터(tests/fixtures/changelog/sample-versions.ts)를 모델로 만들어
 * generateChangelogMarkdown의 출력이 golden file과 일치하는지, 그리고 결정성·dialect·
 * maxBytes 계약을 지키는지 검증한다.
 *
 * golden 갱신: pnpm exec tsx tests/unit/scripts/update-golden.ts
 */

import { describe, test, expect } from 'vitest';
import * as path from 'path';
import * as fs from 'fs/promises';
import { fileURLToPath } from 'url';
import { EXCLUDED_APIS } from '../../../src/categories.js';
import { toPascalCase } from '../../../src/parser/utils.js';
import { buildSampleVersionApis, SAMPLE_CATEGORY_ORDER } from '../../fixtures/changelog/sample-versions.js';
import { buildChangelogModel } from './changelog-model.js';
import { generateChangelogMarkdown } from './changelog-markdown.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const GOLDEN_PATH = path.resolve(__dirname, '../../fixtures/golden/api-changelog.md.golden');

// v3.0.0을 "sibling 탐색 실패 → pnpm store 폴백으로 근사됨" 버전으로 가정한다(실제
// 3.0.1 케이스를 합성 fixture로 흉내낸 것 — v2.0.0과 API 표면이 동일해 v2.0.0 → v3.0.0
// 전이가 빈 diff가 되고, major 버전이 바뀌므로 v3.x 그룹 전체가 빈 전이뿐이게 된다).
// golden 파일도 이 옵션으로 재생성해야 한다(tests/unit/scripts/update-golden.ts와
// 반드시 동일한 옵션을 사용).
const APPROXIMATED_VERSIONS = ['3.0.0'];

describe('generateChangelogMarkdown', () => {
  const model = buildChangelogModel(buildSampleVersionApis(), SAMPLE_CATEGORY_ORDER);

  test('golden file과 일치해야 함', async () => {
    const markdown = generateChangelogMarkdown(model, {
      dialect: 'commonmark',
      approximatedVersions: APPROXIMATED_VERSIONS,
    });

    let golden: string;
    try {
      golden = await fs.readFile(GOLDEN_PATH, 'utf-8');
    } catch {
      if (process.env.CI) {
        // CI에서는 golden 부재를 조용히 넘기지 않는다 — golden이 커밋에서 빠지면 이
        // 테스트가 매번 "새로 생성 후 통과"로 영구 초록이 되어 회귀를 못 잡는다.
        throw new Error(
          `golden 파일이 없습니다: ${GOLDEN_PATH}\n` +
          `  로컬에서 "pnpm exec tsx tests/unit/scripts/update-golden.ts"를 실행해 생성한 뒤 커밋하세요.`,
        );
      }
      await fs.mkdir(path.dirname(GOLDEN_PATH), { recursive: true });
      await fs.writeFile(GOLDEN_PATH, markdown, 'utf-8');
      console.log(`📝 golden file 생성됨: ${GOLDEN_PATH}`);
      return;
    }

    expect(markdown).toBe(golden);
  });

  test('approximatedVersions가 인트로에 결정적 문구로 표시되어야 함', () => {
    const markdown = generateChangelogMarkdown(model, {
      dialect: 'commonmark',
      approximatedVersions: APPROXIMATED_VERSIONS,
    });
    expect(markdown).toContain('pnpm store 폴백으로 API 표면을 근사함: v3.0.0');
  });

  test('빈 전이는 그룹 안에서 "API 총 N개" 없이 한 줄로 압축되어야 함', () => {
    const markdown = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    // v2.0.0 → v3.0.0은 API 표면이 동일해 빈 diff다. H3 풀 섹션이 아니라 압축 줄로 남아야 함.
    expect(markdown).not.toContain('### v2.0.0 → v3.0.0');
    expect(markdown).toContain('변경 없음: v2.0.0 → v3.0.0');
  });

  test('그룹 전체가 빈 전이뿐이면 헤더와 압축 줄만 남아야 함 (v3.x)', () => {
    const markdown = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    const v3Section = markdown.split('## v3.x')[1]?.split('\n## ')[0] ?? '';
    expect(v3Section).toContain('변경 없음: v2.0.0 → v3.0.0');
    expect(v3Section).not.toContain('###');
    expect(v3Section).not.toContain('API 총');
  });

  test('결정적 출력이어야 함 (같은 모델 → 같은 문자열)', () => {
    const a = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    const b = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    expect(a).toBe(b);
  });

  test('선두에 H1을 두지 않아야 함 (GitBook 페이지 제목은 별도 필드)', () => {
    const markdown = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    expect(markdown.startsWith('# ')).toBe(false);
  });

  test('EXCLUDED_APIS 소속 API는 출력에 나타나지 않아야 함', () => {
    const markdown = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    // 원본 API 이름(EXCLUDED_APIS[0])이 아니라, 렌더러가 실제로 출력하는 형식
    // (AIT.<PascalName>, changelog-model.ts의 serializeAPI와 동일한 변환 유틸 재사용)으로
    // 검증한다 — 원본 이름 문자열로 검사하면 카탈로그 순서가 바뀌어도 항상 통과하는
    // vacuous 어서션이 될 수 있다.
    const excludedDisplayName = `AIT.${toPascalCase(EXCLUDED_APIS[0])}`;
    expect(markdown).not.toContain(excludedDisplayName);
  });

  test('최신 버전(v3.0.0) 카탈로그와 마지막 비어있지 않은 전이(v1.1.0 → v2.0.0)가 포함되어야 함', () => {
    const markdown = generateChangelogMarkdown(model, { dialect: 'commonmark' });
    expect(markdown).toContain('v1.1.0 → v2.0.0');
    expect(markdown).toContain('v3.0.0 API 카탈로그');
    expect(markdown).toContain('AIT.SampleQuxLatest');
  });

  test('gitbook dialect는 미구현 에러를 throw해야 함', () => {
    expect(() => generateChangelogMarkdown(model, { dialect: 'gitbook' })).toThrow();
  });

  test('maxBytes 초과 시 한국어 메시지로 throw해야 함', () => {
    expect(() => generateChangelogMarkdown(model, { dialect: 'commonmark', maxBytes: 10 })).toThrow(
      /최대 크기/,
    );
  });
});
