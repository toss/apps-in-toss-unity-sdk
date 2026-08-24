/**
 * API Changelog 모델
 *
 * 버전별 ParsedAPI[] 데이터를 받아 HTML·마크다운 렌더러가 공유하는 중간 표현
 * (ChangelogModel)으로 변환한다. diff 계산(추가/제거/변경 API)과 EXCLUDED_APIS 필터링을
 * 이 진입점 한 곳에 모아, HTML과 마크다운 양쪽이 항상 동일한 데이터를 보도록 한다.
 */

import type { ParsedAPI } from '../../../src/types.js';
import { getCategory, EXCLUDED_APIS, CHANGELOG_ONLY_CATEGORIES } from '../../../src/categories.js';
import { mapToCSharpType } from '../../../src/validators/types.js';

export interface SerializedParam {
  name: string;
  type: string;
  optional: boolean;
  description?: string;
}

export interface SerializedAPI {
  name: string;
  pascalName: string;
  displayName: string;
  category: string;
  file: string;
  description?: string;
  returnDescription?: string;
  examples?: string[];
  parameters: SerializedParam[];
  returnType: string;
  isAsync: boolean;
  isCallbackBased?: boolean;
  isEventSubscription?: boolean;
  isDeprecated?: boolean;
  deprecatedMessage?: string;
  hasPermission: boolean;
  versions: string[];
}

function serializeAPI(api: ParsedAPI, versions: string[]): SerializedAPI {
  const category = getCategory(api.name, true, CHANGELOG_ONLY_CATEGORIES);
  // file은 리포트 표시용 category가 아니라 C# 생성기와 동일한 분류(getCategory 기본 맵)를
  // 따라야 한다 — HTML 렌더러가 이 값으로 Runtime/SDK/<file> 딥링크를 만들기 때문에,
  // CHANGELOG_ONLY_CATEGORIES로 재분류된 이름을 쓰면 실존하지 않는 .cs 파일(404)을 가리킨다.
  const generatorCategory = getCategory(api.name, false);
  return {
    name: api.name,
    pascalName: api.pascalName,
    displayName: 'AIT.' + api.pascalName,
    category,
    file: 'AIT.' + generatorCategory + '.cs',
    description: api.description,
    returnDescription: api.returnDescription,
    examples: api.examples,
    parameters: api.parameters.map(p => ({
      name: p.name,
      type: mapToCSharpType(p.type),
      optional: p.optional,
      description: p.description,
    })),
    returnType: mapToCSharpType(api.returnType),
    isAsync: api.isAsync,
    isCallbackBased: api.isCallbackBased,
    isEventSubscription: api.isEventSubscription,
    isDeprecated: api.isDeprecated,
    deprecatedMessage: api.deprecatedMessage,
    hasPermission: api.hasPermission,
    versions,
  };
}

export interface APIChange {
  kind: 'param-added' | 'param-removed' | 'param-type-changed' | 'return-type-changed' | 'flag-changed';
  description: string;
}

export interface ModifiedAPI {
  name: string;
  changes: APIChange[];
}

export interface VersionDiff {
  from: string;
  to: string;
  added: string[];
  removed: string[];
  modified: ModifiedAPI[];
  totalApis: number;
}

function diffAPIs(prev: SerializedAPI, curr: SerializedAPI): APIChange[] {
  const changes: APIChange[] = [];

  // Compare parameters by name
  const prevParams = new Map(prev.parameters.map(p => [p.name, p]));
  const currParams = new Map(curr.parameters.map(p => [p.name, p]));

  for (const [name, cp] of currParams) {
    const pp = prevParams.get(name);
    if (!pp) {
      changes.push({ kind: 'param-added', description: `parameter added: ${name}: ${cp.type}${cp.optional ? '?' : ''}` });
    } else {
      if (pp.type !== cp.type) {
        changes.push({ kind: 'param-type-changed', description: `${name}: ${pp.type} → ${cp.type}` });
      }
      if (pp.optional !== cp.optional) {
        changes.push({ kind: 'param-type-changed', description: `${name}: ${pp.optional ? 'optional' : 'required'} → ${cp.optional ? 'optional' : 'required'}` });
      }
    }
  }
  for (const [name] of prevParams) {
    if (!currParams.has(name)) {
      changes.push({ kind: 'param-removed', description: `parameter removed: ${name}` });
    }
  }

  // Compare return type
  if (prev.returnType !== curr.returnType) {
    changes.push({ kind: 'return-type-changed', description: `return: ${prev.returnType} → ${curr.returnType}` });
  }

  // Compare flags
  const flags = ['isAsync', 'isDeprecated', 'isCallbackBased', 'isEventSubscription', 'hasPermission'] as const;
  for (const flag of flags) {
    if ((prev[flag] ?? false) !== (curr[flag] ?? false)) {
      changes.push({ kind: 'flag-changed', description: `${flag}: ${prev[flag] ?? false} → ${curr[flag] ?? false}` });
    }
  }

  return changes;
}

/**
 * HTML·마크다운 렌더러가 공유하는 changelog 중간 표현.
 * - versions: 오름차순(과거 → 최신) 버전 목록
 * - apis: API 이름 → 직렬화된 최신 데이터 (제거된 API도 마지막으로 관측된 데이터를 보존)
 * - diffs: 인접 버전 간 diff (오름차순)
 * - catalog: 버전 → 카테고리 → API 이름 목록 (categoryOrder 순서)
 */
export interface ChangelogModel {
  versions: string[];
  apis: Record<string, SerializedAPI>;
  diffs: VersionDiff[];
  catalog: Map<string, Map<string, string[]>>;
}

/**
 * 버전별 ParsedAPI[] 데이터로부터 ChangelogModel을 만든다.
 *
 * EXCLUDED_APIS(SDK 생성에서 제외되는 API, src/categories.ts)에 속한 API는 여기서
 * 걸러낸다 — C# 생성기가 애초에 만들지 않는 API가 changelog에 나타나면 혼란을
 * 주므로, HTML·마크다운 양쪽에 일관되게 반영하기 위해 이 모델 진입점 한 곳에서
 * 필터링한다(목록 복제 대신 categories.ts에서 직접 import).
 */
export function buildChangelogModel(
  versionApis: Map<string, ParsedAPI[]>,
  categoryOrder: string[],
): ChangelogModel {
  const excludedSet = new Set(EXCLUDED_APIS);
  const filteredVersionApis = new Map<string, ParsedAPI[]>();
  for (const [version, apis] of versionApis) {
    filteredVersionApis.set(version, apis.filter(api => !excludedSet.has(api.name)));
  }

  const versions = [...filteredVersionApis.keys()];

  // API 인덱스 구축: 최신 버전 데이터 우선, 제거된 API도 보존
  const apiIndex = new Map<string, { api: ParsedAPI; versions: string[] }>();
  for (const [version, apis] of filteredVersionApis) {
    for (const api of apis) {
      const existing = apiIndex.get(api.name);
      if (existing) {
        existing.versions.push(version);
        existing.api = api; // 최신 버전 데이터로 갱신
      } else {
        apiIndex.set(api.name, { api, versions: [version] });
      }
    }
  }

  // Serialized API 데이터
  const serializedApis: Record<string, SerializedAPI> = {};
  for (const [name, { api, versions: apiVersions }] of apiIndex) {
    serializedApis[name] = serializeAPI(api, apiVersions);
  }

  // Diff 계산
  const diffs: VersionDiff[] = [];
  for (let i = 1; i < versions.length; i++) {
    const prevApis = filteredVersionApis.get(versions[i - 1])!;
    const currApis = filteredVersionApis.get(versions[i])!;
    const prevNames = new Set(prevApis.map(a => a.name));
    const currNames = new Set(currApis.map(a => a.name));
    const added = [...currNames].filter(n => !prevNames.has(n)).sort();
    const removed = [...prevNames].filter(n => !currNames.has(n)).sort();

    // Modified 감지: 양쪽에 모두 존재하는 API의 시그니처 비교
    const modified: ModifiedAPI[] = [];
    const commonNames = [...currNames].filter(n => prevNames.has(n));
    const prevApiMap = new Map(prevApis.map(a => [a.name, a]));
    const currApiMap = new Map(currApis.map(a => [a.name, a]));
    for (const name of commonNames) {
      const prevSerialized = serializeAPI(prevApiMap.get(name)!, [versions[i - 1]]);
      const currSerialized = serializeAPI(currApiMap.get(name)!, [versions[i]]);
      const changes = diffAPIs(prevSerialized, currSerialized);
      if (changes.length > 0) {
        modified.push({ name, changes });
      }
    }
    modified.sort((a, b) => a.name.localeCompare(b.name));

    // 변화가 없는 전이(add/remove/modify 전부 0)도 push한다 — 예: pnpm store 폴백으로
    // 직전 버전과 동일한 dtsDir을 읽게 되는 경우(3.0.1처럼 sibling web-bridge가 rename되어
    // 못 찾을 때)에도 해당 버전이 diff 목록에서 통째로 빠지지 않도록 하기 위함. 렌더러가
    // 각자의 형식에 맞게 빈 diff를 다룬다(HTML은 스킵해 기존 형태 보존, 마크다운은
    // "변경 없음."으로 표시).
    diffs.push({
      from: versions[i - 1],
      to: versions[i],
      added,
      removed,
      modified,
      totalApis: currNames.size,
    });
  }

  // 카테고리별 API 그룹핑 (버전별) — getCategory()로 정확한 분류
  const catalog = new Map<string, Map<string, string[]>>();
  for (const [version, apis] of filteredVersionApis) {
    const catMap = new Map<string, string[]>();
    for (const api of apis) {
      const cat = getCategory(api.name, true, CHANGELOG_ONLY_CATEGORIES);
      if (!catMap.has(cat)) catMap.set(cat, []);
      catMap.get(cat)!.push(api.name);
    }
    // 카테고리 내 API 정렬
    for (const [, apiList] of catMap) apiList.sort();
    // categoryOrder 순서로 정렬된 Map 생성
    const orderedMap = new Map<string, string[]>();
    for (const cat of categoryOrder) {
      if (catMap.has(cat)) orderedMap.set(cat, catMap.get(cat)!);
    }
    // categoryOrder에 없는 카테고리 추가
    for (const [cat, apiList] of catMap) {
      if (!orderedMap.has(cat)) orderedMap.set(cat, apiList);
    }
    catalog.set(version, orderedMap);
  }

  return { versions, apis: serializedApis, diffs, catalog };
}
