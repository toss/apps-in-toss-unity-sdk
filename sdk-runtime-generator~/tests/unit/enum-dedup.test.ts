/**
 * computeEnumAliases / applyEnumAliases 단위 테스트
 *
 * Inline string-literal-union enum 중복 제거 로직 검증.
 * 실제 Permission* 시나리오를 fixture로 사용해 회귀 방지.
 */

import { describe, test, expect } from 'vitest';
import {
  computeEnumAliases,
  applyEnumAliases,
} from '../../src/generators/csharp/enum-dedup.js';

describe('computeEnumAliases', () => {
  test('빈 입력은 빈 결과를 반환한다', () => {
    const result = computeEnumAliases(new Map());
    expect(result.emit).toEqual([]);
    expect(result.aliases.size).toBe(0);
  });

  test('값이 빈 inline enum은 무시한다', () => {
    const inline = new Map<string, string[]>([['Empty', []]]);
    const result = computeEnumAliases(inline);
    expect(result.emit).toEqual([]);
    expect(result.aliases.size).toBe(0);
  });

  test('named enum과 값 셋이 같은 inline enum은 named로 alias된다', () => {
    const inline = new Map<string, string[]>([
      [
        'GetPermissionPermissionName',
        ['clipboard', 'contacts', 'photos', 'geolocation', 'camera', 'microphone'],
      ],
    ]);
    const named = new Map<string, string[]>([
      [
        'PermissionName',
        ['clipboard', 'contacts', 'photos', 'geolocation', 'camera', 'microphone'],
      ],
    ]);

    const result = computeEnumAliases(inline, named);

    expect(result.emit).toEqual([]);
    expect(result.aliases.get('GetPermissionPermissionName')).toBe('PermissionName');
  });

  test('Permission 3중 중복 시나리오: 모든 inline이 named로 합쳐진다', () => {
    const permissionValues = [
      'clipboard',
      'contacts',
      'photos',
      'geolocation',
      'camera',
      'microphone',
    ];
    const inline = new Map<string, string[]>([
      ['GetPermissionPermissionName', permissionValues],
      ['OpenPermissionDialogPermissionName', permissionValues],
      ['RequestPermissionPermissionName', permissionValues],
    ]);
    const named = new Map<string, string[]>([
      ['PermissionName', permissionValues],
    ]);

    const result = computeEnumAliases(inline, named);

    expect(result.emit).toEqual([]);
    expect(result.aliases.size).toBe(3);
    expect(result.aliases.get('GetPermissionPermissionName')).toBe('PermissionName');
    expect(result.aliases.get('OpenPermissionDialogPermissionName')).toBe('PermissionName');
    expect(result.aliases.get('RequestPermissionPermissionName')).toBe('PermissionName');
  });

  test('값 순서가 달라도 같은 set이면 alias된다', () => {
    const inline = new Map<string, string[]>([
      ['InlineA', ['a', 'b', 'c']],
    ]);
    const named = new Map<string, string[]>([
      ['NamedA', ['c', 'a', 'b']],
    ]);

    const result = computeEnumAliases(inline, named);

    expect(result.aliases.get('InlineA')).toBe('NamedA');
    expect(result.emit).toEqual([]);
  });

  test('named가 없으면 첫 inline에 나머지 inline이 alias된다', () => {
    const inline = new Map<string, string[]>([
      ['FirstFoo', ['x', 'y']],
      ['SecondFoo', ['x', 'y']],
      ['ThirdFoo', ['x', 'y']],
    ]);

    const result = computeEnumAliases(inline);

    expect(result.emit).toEqual([{ name: 'FirstFoo', values: ['x', 'y'] }]);
    expect(result.aliases.get('SecondFoo')).toBe('FirstFoo');
    expect(result.aliases.get('ThirdFoo')).toBe('FirstFoo');
  });

  test('값 셋이 다른 inline enum은 alias되지 않는다', () => {
    const inline = new Map<string, string[]>([
      ['EnumA', ['a', 'b']],
      ['EnumB', ['a', 'c']],
    ]);

    const result = computeEnumAliases(inline);

    expect(result.emit).toEqual([
      { name: 'EnumA', values: ['a', 'b'] },
      { name: 'EnumB', values: ['a', 'c'] },
    ]);
    expect(result.aliases.size).toBe(0);
  });

  test('자기 자신은 alias하지 않는다 (named에 같은 이름이 있어도)', () => {
    const inline = new Map<string, string[]>([['PermissionName', ['a', 'b']]]);
    const named = new Map<string, string[]>([['PermissionName', ['a', 'b']]]);

    const result = computeEnumAliases(inline, named);

    expect(result.emit).toEqual([{ name: 'PermissionName', values: ['a', 'b'] }]);
    expect(result.aliases.size).toBe(0);
  });

  test('emit은 등장 순서를 보존한다', () => {
    const inline = new Map<string, string[]>([
      ['Z', ['1']],
      ['A', ['2']],
      ['M', ['3']],
    ]);

    const result = computeEnumAliases(inline);

    expect(result.emit.map((e) => e.name)).toEqual(['Z', 'A', 'M']);
  });

  test('PermissionAccess 시나리오 (read/write/access)', () => {
    const accessValues = ['read', 'write', 'access'];
    const inline = new Map<string, string[]>([
      ['GetPermissionPermissionAccess', accessValues],
      ['OpenPermissionDialogPermissionAccess', accessValues],
      ['RequestPermissionPermissionAccess', accessValues],
    ]);
    const named = new Map<string, string[]>([
      ['PermissionAccess', accessValues],
    ]);

    const result = computeEnumAliases(inline, named);

    expect(result.emit).toEqual([]);
    expect(result.aliases.size).toBe(3);
    for (const from of [
      'GetPermissionPermissionAccess',
      'OpenPermissionDialogPermissionAccess',
      'RequestPermissionPermissionAccess',
    ]) {
      expect(result.aliases.get(from)).toBe('PermissionAccess');
    }
  });
});

describe('applyEnumAliases', () => {
  test('alias가 비어있으면 원문을 그대로 반환한다', () => {
    const body = 'public class Foo { GetPermissionPermissionName Name; }';
    expect(applyEnumAliases(body, new Map())).toBe(body);
  });

  test('단순 치환', () => {
    const aliases = new Map([['GetPermissionPermissionName', 'PermissionName']]);
    const body = 'GetPermissionPermissionName name;';
    expect(applyEnumAliases(body, aliases)).toBe('PermissionName name;');
  });

  test('substring 충돌을 피한다 (긴 이름이 먼저 치환됨)', () => {
    // PermissionName ⊂ GetPermissionPermissionName 충돌 방지 시나리오
    const aliases = new Map([
      ['GetPermissionPermissionName', 'PermissionName'],
      ['ShortName', 'NewShortName'],
    ]);
    const body = 'GetPermissionPermissionName foo; PermissionName bar; ShortName baz;';
    const result = applyEnumAliases(body, aliases);

    // GetPermissionPermissionName가 먼저 치환되어야 PermissionName이 두 번 치환되지 않음
    expect(result).toBe('PermissionName foo; PermissionName bar; NewShortName baz;');
  });

  test('word boundary로 식별자 일부 매칭 방지', () => {
    const aliases = new Map([['Foo', 'Bar']]);
    const body = 'Foo x; FooBar y; xFoo z; Foo.method();';
    const result = applyEnumAliases(body, aliases);

    // FooBar, xFoo는 식별자 일부라서 치환되면 안 됨
    expect(result).toBe('Bar x; FooBar y; xFoo z; Bar.method();');
  });

  test('다중 등장 모두 치환', () => {
    const aliases = new Map([['A', 'X']]);
    const body = 'A first; A second; A third;';
    expect(applyEnumAliases(body, aliases)).toBe('X first; X second; X third;');
  });

  test('Permission 3중 alias가 본문에 모두 반영된다', () => {
    const aliases = new Map([
      ['GetPermissionPermissionName', 'PermissionName'],
      ['OpenPermissionDialogPermissionName', 'PermissionName'],
      ['RequestPermissionPermissionName', 'PermissionName'],
    ]);
    const body = [
      'GetPermissionPermissionName Get(GetPermissionPermissionName name);',
      'OpenPermissionDialogPermissionName Open();',
      'RequestPermissionPermissionName Request();',
      'PermissionName Existing();', // 원본 named enum은 유지
    ].join('\n');

    const result = applyEnumAliases(body, aliases);

    expect(result).toBe(
      [
        'PermissionName Get(PermissionName name);',
        'PermissionName Open();',
        'PermissionName Request();',
        'PermissionName Existing();',
      ].join('\n'),
    );
  });

  test('정규식 특수문자가 포함된 식별자도 안전하게 escape된다', () => {
    // 실제로는 C# 식별자에 특수문자가 없지만, 방어적 escape 검증
    const aliases = new Map([['Foo.Bar', 'Baz']]);
    const body = 'Foo.Bar x; Foo_Bar y;';
    // Foo.Bar는 . 때문에 word boundary 매칭이 안 되지만, regex 안전성은 확보됨
    // (실패하지 않고 정상 동작하는지만 검증)
    expect(() => applyEnumAliases(body, aliases)).not.toThrow();
  });
});

describe('end-to-end: computeEnumAliases + applyEnumAliases', () => {
  test('Permission 시나리오 통합', () => {
    const permissionValues = [
      'clipboard',
      'contacts',
      'photos',
      'geolocation',
      'camera',
      'microphone',
    ];
    const inline = new Map<string, string[]>([
      ['GetPermissionPermissionName', permissionValues],
      ['OpenPermissionDialogPermissionName', permissionValues],
      ['RequestPermissionPermissionName', permissionValues],
    ]);
    const named = new Map<string, string[]>([
      ['PermissionName', permissionValues],
    ]);

    const { emit, aliases } = computeEnumAliases(inline, named);

    // emit해야 할 inline enum 없음 (모두 named로 흡수)
    expect(emit).toEqual([]);

    // 가상의 SDK 본문 코드
    const body = [
      'public partial class AIT {',
      '  public static GetPermissionPermissionName GetPermission(GetPermissionPermissionName name) {}',
      '  public static OpenPermissionDialogPermissionName OpenPermissionDialog() {}',
      '  public static RequestPermissionPermissionName RequestPermission() {}',
      '}',
    ].join('\n');

    const rewritten = applyEnumAliases(body, aliases);

    // 모든 inline 참조가 PermissionName으로 치환되어야 함
    expect(rewritten).not.toContain('GetPermissionPermissionName');
    expect(rewritten).not.toContain('OpenPermissionDialogPermissionName');
    expect(rewritten).not.toContain('RequestPermissionPermissionName');
    expect(rewritten.split('PermissionName').length - 1).toBe(4); // 4번 등장 (1 param type + 3 return type)
  });
});
