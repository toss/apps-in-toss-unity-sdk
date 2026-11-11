/**
 * 이름 스냅샷 테스트 (생성기 안전망)
 *
 * web-framework 3.9.0 번들을 파싱한 결과의 이름·shape를 fixture로 고정한다.
 * 생성기 리팩토링이 API 이름(callPath/pascalName/namespace/category)이나
 * shape 플래그를 바꾸면 이 테스트가 추가/삭제/변경된 callPath를 알려 준다.
 *
 * 의도한 변경이면 fixture를 다시 쓴다:
 *   UPDATE_NAMING_SNAPSHOT=1 pnpm exec vitest run tests/unit/naming-snapshot.test.ts
 */

import { describe, test, expect, beforeAll } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import type { ParsedAPI } from '../../src/types.js';
import { getApiCallPath } from '../../src/types.js';
import { DEPRECATED_API_OVERRIDES } from '../../src/categories.js';
import { DOC_FALLBACKS } from '../../src/doc-fallbacks.js';
import { resolveVersionPaths, createParserForVersion } from '../../src/discovery.js';

const SNAPSHOT_VERSION = '3.9.0';
const FIXTURE_DIR = path.join(__dirname, '..', 'fixtures', 'naming-snapshot');
const API_FIXTURE = path.join(FIXTURE_DIR, `web-framework-${SNAPSHOT_VERSION}.json`);
const OVERRIDE_FIXTURE = path.join(FIXTURE_DIR, 'override-keys.json');
const UPDATE = process.env.UPDATE_NAMING_SNAPSHOT === '1';

interface ApiEntry {
  callPath: string;
  /** 이벤트 구독 API는 callPath(addEventListener)를 공유하므로 이벤트 이름으로 구분한다 */
  eventName: string | null;
  originalName: string;
  pascalName: string;
  namespace: string | null;
  category: string;
  isAsync: boolean;
  isProperty: boolean;
  isEventSubscription: boolean;
  isCallbackBased: boolean;
  isDeprecated: boolean;
  returnsBinary: boolean;
}

interface OverrideEntry {
  source: 'DEPRECATED_API_OVERRIDES' | 'DOC_FALLBACKS';
  /** 키가 대응하는 ParsedAPI 필드 (생성기가 실제로 조회하는 필드) */
  field: 'name' | 'pascalName';
  key: string;
  matches: number;
  callPath: string | null;
}

/** API 식별 키: callPath (이벤트 구독은 callPath[eventName]) */
function apiKey(e: Pick<ApiEntry, 'callPath' | 'eventName'>): string {
  return e.eventName ? `${e.callPath}[${e.eventName}]` : e.callPath;
}

function toEntry(api: ParsedAPI): ApiEntry {
  return {
    callPath: getApiCallPath(api),
    eventName: api.eventName ?? null,
    originalName: api.originalName,
    pascalName: api.pascalName,
    namespace: api.namespace ?? null,
    category: api.category,
    isAsync: api.isAsync,
    isProperty: api.isProperty === true,
    isEventSubscription: api.isEventSubscription === true,
    isCallbackBased: api.isCallbackBased === true,
    isDeprecated: api.isDeprecated === true,
    returnsBinary: api.returnsBinary === true,
  };
}

function byKey<T>(items: T[], key: (t: T) => string): T[] {
  return [...items].sort((a, b) => {
    const ka = key(a);
    const kb = key(b);
    return ka < kb ? -1 : ka > kb ? 1 : 0;
  });
}

function writeJson(file: string, data: unknown): void {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(data, null, 2) + '\n');
}

function readJson<T>(file: string): T {
  if (!fs.existsSync(file)) {
    throw new Error(
      `fixture가 없습니다: ${file}\n  UPDATE_NAMING_SNAPSHOT=1 로 생성하세요.`,
    );
  }
  return JSON.parse(fs.readFileSync(file, 'utf8')) as T;
}

function describeDiff(expected: ApiEntry[], actual: ApiEntry[]): string {
  const exp = new Map(expected.map(e => [apiKey(e), e]));
  const act = new Map(actual.map(e => [apiKey(e), e]));
  const added = [...act.keys()].filter(k => !exp.has(k));
  const removed = [...exp.keys()].filter(k => !act.has(k));
  const changed = [...act.keys()].filter(
    k => exp.has(k) && JSON.stringify(exp.get(k)) !== JSON.stringify(act.get(k)),
  );
  const lines = [
    `이름 스냅샷 불일치 (web-framework ${SNAPSHOT_VERSION})`,
    `  추가된 callPath (${added.length}): ${added.join(', ') || '-'}`,
    `  삭제된 callPath (${removed.length}): ${removed.join(', ') || '-'}`,
    `  변경된 callPath (${changed.length}):`,
  ];
  for (const k of changed) {
    const e = exp.get(k) as unknown as Record<string, unknown>;
    const a = act.get(k) as unknown as Record<string, unknown>;
    const fields = Object.keys(a).filter(f => JSON.stringify(e[f]) !== JSON.stringify(a[f]));
    lines.push(`    ${k}: ${fields.map(f => `${f} ${JSON.stringify(e[f])} -> ${JSON.stringify(a[f])}`).join('; ')}`);
  }
  lines.push('의도한 변경이면 UPDATE_NAMING_SNAPSHOT=1 로 fixture를 다시 쓰세요.');
  return lines.join('\n');
}

describe(`이름 스냅샷 (web-framework ${SNAPSHOT_VERSION})`, () => {
  let apis: ParsedAPI[] = [];

  beforeAll(async () => {
    const paths = await resolveVersionPaths(SNAPSHOT_VERSION);
    apis = await createParserForVersion(paths).parseAPIs();
  });

  test('API 이름·shape가 fixture와 일치해야 함', () => {
    const actual = byKey(apis.map(toEntry), apiKey);
    if (UPDATE) {
      writeJson(API_FIXTURE, actual);
      return;
    }
    const expected = readJson<ApiEntry[]>(API_FIXTURE);
    if (JSON.stringify(expected) !== JSON.stringify(actual)) {
      throw new Error(describeDiff(expected, actual));
    }
    expect(actual.length).toBe(expected.length);
  });

  test('API 식별 키(callPath[eventName])는 중복이 없어야 함', () => {
    const paths = apis.map(a => apiKey(toEntry(a)));
    const dup = paths.filter((p, i) => paths.indexOf(p) !== i);
    expect(dup).toEqual([]);
  });

  test('override 키가 각각 정확히 1개 API로 해석되는지 (해석 실패는 기록만)', () => {
    const rows: OverrideEntry[] = [];
    const resolve = (
      source: OverrideEntry['source'],
      field: OverrideEntry['field'],
      keys: string[],
    ) => {
      for (const key of keys) {
        const hits = apis.filter(a => a[field] === key);
        rows.push({
          source,
          field,
          key,
          matches: hits.length,
          callPath: hits.length === 1 ? getApiCallPath(hits[0]) : null,
        });
      }
    };
    // 생성기가 실제로 조회하는 필드: 모든 override는 api.name, 문서 폴백은 api.pascalName
    resolve('DEPRECATED_API_OVERRIDES', 'name', Object.keys(DEPRECATED_API_OVERRIDES));
    resolve('DOC_FALLBACKS', 'pascalName', Object.keys(DOC_FALLBACKS));
    const actual = byKey(rows, r => `${r.source}\0${r.key}`);

    const ambiguous = actual.filter(r => r.matches > 1);
    const unresolved = actual.filter(r => r.matches === 0);
    // 현재 상태 기록이 목적이라 미해석은 실패시키지 않고 개수만 보고한다.
    console.log(
      `[naming-snapshot] override 키 ${actual.length}개 중 해석 실패 ${unresolved.length}개` +
        (unresolved.length ? `: ${unresolved.map(r => `${r.source}:${r.key}`).join(', ')}` : ''),
    );

    if (UPDATE) {
      writeJson(OVERRIDE_FIXTURE, actual);
    } else {
      const expected = readJson<OverrideEntry[]>(OVERRIDE_FIXTURE);
      expect(actual, 'override 키 해석 결과가 fixture와 다릅니다 (UPDATE_NAMING_SNAPSHOT=1 로 갱신)').toEqual(expected);
    }
    expect(
      ambiguous.map(r => `${r.source}:${r.key} (${r.matches}개)`),
      '2개 이상 API로 해석되는 override 키',
    ).toEqual([]);
  });
});
