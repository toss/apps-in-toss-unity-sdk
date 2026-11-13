/**
 * changelog-model 타입 이름 직렬화 테스트
 *
 * 교차(`&`)·`Omit` 별칭 파라미터가 object로 찍히지 않고 별칭 이름으로 나오는지,
 * 인라인 객체 리터럴은 object로 남는지 검증한다.
 */

import { describe, test, expect } from 'vitest';
import type { ParsedAPI, ParsedType } from '../../../src/types.js';
import { buildChangelogModel } from './changelog-model.js';

const IMPORT_RAW = 'import("/x/node_modules/@apps-in-toss/web-framework/dist/index").AddAccessoryButtonParams';

const importedObject: ParsedType = { name: '__type', kind: 'object', raw: IMPORT_RAW, properties: [] };
const aliasedIntersection: ParsedType = {
  name: '__type',
  kind: 'object',
  raw: 'LegacyAddAccessoryButtonParams',
  isIntersection: true,
  properties: [],
};
const inlineLiteral: ParsedType = { name: '__type', kind: 'object', raw: '{ id: string; title: string; }', properties: [] };
const voidType: ParsedType = { name: 'void', kind: 'primitive', raw: 'void' };

function api(paramType: ParsedType, isDeprecated = false): ParsedAPI {
  return {
    name: 'partnerAddAccessoryButton',
    pascalName: 'PartnerAddAccessoryButton',
    originalName: 'partnerAddAccessoryButton',
    category: 'Other',
    file: 'sample.d.ts',
    parameters: [{ name: 'params', type: paramType, optional: false }],
    returnType: voidType,
    isAsync: true,
    hasPermission: false,
    isDeprecated,
  };
}

function build(...types: Array<[ParsedType, boolean?]>) {
  const versions = new Map<string, ParsedAPI[]>();
  types.forEach(([t, dep], i) => versions.set(`1.${i}.0`, [api(t, dep)]));
  return buildChangelogModel(versions, ['Other']);
}

describe('changelog 타입 이름 직렬화', () => {
  test('교차·Omit 별칭 파라미터는 별칭 이름으로 나와야 함', () => {
    const model = build([aliasedIntersection]);
    expect(model.apis['partnerAddAccessoryButton'].parameters[0].type).toBe('LegacyAddAccessoryButtonParams');
  });

  test('import 경로가 raw에 있는 객체 타입은 기존대로 끝 이름이 나와야 함', () => {
    const model = build([importedObject]);
    expect(model.apis['partnerAddAccessoryButton'].parameters[0].type).toBe('AddAccessoryButtonParams');
  });

  test('인라인 객체 리터럴 파라미터는 object로 남아야 함', () => {
    const model = build([inlineLiteral]);
    expect(model.apis['partnerAddAccessoryButton'].parameters[0].type).toBe('object');
  });

  test('별칭만 바뀐 전이의 diff 줄은 별칭 이름을 그대로 보여야 함', () => {
    const model = build([importedObject, false], [aliasedIntersection, true]);
    const changes = model.diffs[0].modified[0].changes.map(c => c.description);
    expect(changes).toEqual([
      'params: AddAccessoryButtonParams → LegacyAddAccessoryButtonParams',
      'isDeprecated: false → true',
    ]);
    expect(changes.join('\n')).not.toContain('object');
  });
});
