/**
 * Changelog golden 테스트용 합성(fake) 버전 데이터
 *
 * 실제 web-framework를 설치하지 않고도 changelog-model/changelog-markdown 렌더링을
 * 결정적으로 검증하기 위한 가짜 API 4버전 세트. 다음 케이스를 커버한다:
 * - v1.0.0 → v1.1.0: API 추가(sampleBazNew) + 파라미터 타입 변경(sampleFooBar)
 * - v1.1.0 → v2.0.0: API 추가(sampleQuxLatest) + API 제거(sampleFooBar)
 * - v2.0.0 → v3.0.0: API 표면 무변화(빈 diff) — sibling web-bridge를 못 찾아 pnpm store
 *   폴백으로 직전 버전과 동일한 dtsDir을 읽게 되는 실제 3.0.1 케이스를 합성 fixture로
 *   흉내낸 것. major 버전이 바뀌어 그룹 전체(v3.x)가 빈 전이뿐인 경우까지 커버한다
 *   (changelog-markdown.ts의 "그룹 내 빈 전이 압축 줄" 렌더링 검증용).
 * - 전 버전에 걸쳐 EXCLUDED_APIS 소속 API 1개(실제 목록에서 가져옴)가 존재 —
 *   changelog-model의 EXCLUDED_APIS 필터가 diff/카탈로그 어디에도 노출시키지
 *   않아야 함을 검증한다.
 */

import type { ParsedAPI, ParsedType } from '../../../src/types.js';
import { EXCLUDED_APIS } from '../../../src/categories.js';

function type(name: string, kind: ParsedType['kind'] = 'primitive'): ParsedType {
  return { name, kind, raw: name };
}

function api(overrides: Partial<ParsedAPI> & Pick<ParsedAPI, 'name'>): ParsedAPI {
  return {
    pascalName: overrides.name.charAt(0).toUpperCase() + overrides.name.slice(1),
    originalName: overrides.name,
    category: 'Other',
    file: 'sample.d.ts',
    parameters: [],
    returnType: type('void'),
    isAsync: true,
    hasPermission: false,
    ...overrides,
  };
}

// 실제 EXCLUDED_APIS 목록에서 하나 사용 (목록을 복제하지 않고 그대로 재사용)
const EXCLUDED_SAMPLE_NAME = EXCLUDED_APIS[0];

function excludedSampleApi(): ParsedAPI {
  return api({ name: EXCLUDED_SAMPLE_NAME, category: 'Advertising' });
}

const fooBarV1 = api({
  name: 'sampleFooBar',
  parameters: [{ name: 'value', type: type('string'), optional: false }],
  returnType: type('void'),
  isAsync: true,
});

const fooBarV1_1 = api({
  name: 'sampleFooBar',
  // v1.1.0에서 파라미터 타입 변경: string → number (modified diff 검증용)
  parameters: [{ name: 'value', type: type('number'), optional: false }],
  returnType: type('void'),
  isAsync: true,
});

const bazNew = api({
  name: 'sampleBazNew',
  parameters: [],
  returnType: type('boolean'),
  isAsync: false,
});

const quxLatest = api({
  name: 'sampleQuxLatest',
  parameters: [{ name: 'flag', type: type('boolean'), optional: true }],
  returnType: type('string'),
  isAsync: true,
});

/**
 * 버전 → ParsedAPI[] 맵을 만든다. buildChangelogModel/generateChangelogHTML/
 * generateChangelogMarkdown이 실제로 소비하는 입력과 동일한 형태.
 */
export function buildSampleVersionApis(): Map<string, ParsedAPI[]> {
  const versionApis = new Map<string, ParsedAPI[]>();

  versionApis.set('1.0.0', [fooBarV1, excludedSampleApi()]);
  versionApis.set('1.1.0', [fooBarV1_1, bazNew, excludedSampleApi()]);
  versionApis.set('2.0.0', [bazNew, quxLatest, excludedSampleApi()]);
  // v2.0.0과 API 표면이 완전히 동일 → 빈 diff (pnpm store 폴백으로 근사된 3.0.1 흉내).
  versionApis.set('3.0.0', [bazNew, quxLatest, excludedSampleApi()]);

  return versionApis;
}

export const SAMPLE_CATEGORY_ORDER = ['Advertising', 'Other'];
