/**
 * 하위 호환 멤버 생성(compat.ts) 테스트.
 *
 * - 변환 함수: 작은 합성 유닛으로 생성 코드와 실패 조건을 확인한다.
 * - 생성 결과: 표(compat.ts)의 모든 항목이 Runtime/SDK 에 반영됐는지 확인한다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { GeneratedTypeUnit } from '../../src/types.js';
import {
  ENUM_MEMBER_ORDER,
  FORWARDING_PROPERTY_COMPAT,
  LEGACY_FIELDS,
  LEGACY_PARAM_OVERLOAD_COMPAT,
  LEGACY_SUBSCRIBE_COMPAT,
  LEGACY_TYPES,
  OPTIONAL_NUMBER_COMPAT,
  RETURN_TYPE_COMPAT,
  STRING_ENUM_COMPAT,
  applyTypeCompat,
  assertApiCompatTargets,
  getApiCompat,
  reorderEnumMembers,
  type TypeCompatTables,
} from '../../src/generators/csharp/compat.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

const EXTENSION = `
        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;`;

function classUnit(name: string, fields: string): GeneratedTypeUnit {
  return {
    name,
    category: 'Other',
    code: `    [Serializable]\n    [Preserve]\n    public class ${name}\n    {\n${fields}\n${EXTENSION}\n    }`,
  };
}

const NO_TABLES: TypeCompatTables = {
  stringEnums: [],
  optionalNumbers: [],
  forwardingProperties: [],
  legacyFields: [],
  legacyTypes: [],
  enumOrder: {},
};

describe('applyTypeCompat: string -> enum 필드', () => {
  const tables: TypeCompatTables = { ...NO_TABLES, stringEnums: [{ type: 'Log', member: 'Kind' }] };

  test('원래 이름은 string 으로 와이어 키를 유지하고 Typed 는 계산 프로퍼티다', () => {
    const units = [classUnit('Log', '        /// <summary>종류</summary>\n        [Preserve]\n        [JsonProperty("kind")]\n        public LogKind Kind;')];
    const [unit] = applyTypeCompat(units, tables);
    expect(unit.code).toContain('[JsonProperty("kind")]');
    expect(unit.code).toContain('[Obsolete("Use KindTyped")]');
    expect(unit.code).toContain('public string Kind;');
    expect(unit.code).toContain('public LogKind? KindTyped');
    expect(unit.code).toContain('get => AITCompat.ParseEnum<LogKind>(Kind);');
    expect(unit.code).toContain('set => Kind = AITCompat.ToWire(value);');
    // Typed 는 직렬화되지 않는다: 와이어 키 선언은 하나뿐이다
    expect(unit.code.match(/\[JsonProperty\("kind"\)\]/g)).toHaveLength(1);
    expect(unit.code).toMatch(/\[JsonIgnore\]\n\s+public LogKind\? KindTyped/);
    // 문서 주석은 원래 필드 위에 그대로 남는다
    expect(unit.code.indexOf('/// <summary>종류</summary>')).toBeLessThan(unit.code.indexOf('public string Kind;'));
  });

  test('optional enum(LogKind?)도 같은 enum 타입의 nullable 프로퍼티가 된다', () => {
    const units = [classUnit('Log', '        [Preserve]\n        [JsonProperty("kind")]\n        public LogKind? Kind; // optional')];
    const [unit] = applyTypeCompat(units, tables);
    expect(unit.code).toContain('public string Kind; // optional');
    expect(unit.code).toContain('public LogKind? KindTyped');
    expect(unit.code).not.toContain('LogKind??');
  });

  test('입력 유닛은 바꾸지 않는다', () => {
    const units = [classUnit('Log', '        [Preserve]\n        [JsonProperty("kind")]\n        public LogKind Kind;')];
    const before = units[0].code;
    applyTypeCompat(units, tables);
    expect(units[0].code).toBe(before);
  });

  test('대상 타입이 없거나 이미 string 이면 생성을 중단한다', () => {
    expect(() => applyTypeCompat([], tables)).toThrow(/'Log'.*생성되지 않았/);
    expect(() => applyTypeCompat([classUnit('Log', '        [Preserve]\n        public string Kind;')], tables)).toThrow(/이미 string/);
    expect(() => applyTypeCompat([classUnit('Log', '        [Preserve]\n        public int Other;')], tables)).toThrow(/필드 선언을 찾지 못/);
  });
});

describe('applyTypeCompat: double -> double? 필드', () => {
  const tables: TypeCompatTables = { ...NO_TABLES, optionalNumbers: [{ type: 'Ev', member: 'Amount' }] };

  test('double 필드 + Specified + Typed 프로퍼티', () => {
    const units = [classUnit('Ev', '        [Preserve]\n        [JsonProperty("amount")]\n        public double? Amount; // optional')];
    const [unit] = applyTypeCompat(units, tables);
    expect(unit.code).toContain('[JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]');
    expect(unit.code).toContain('[Obsolete("Use AmountTyped")]');
    expect(unit.code).toContain('public double Amount;');
    expect(unit.code).toContain('public bool AmountSpecified { get; set; }');
    expect(unit.code).toContain('public double? AmountTyped');
    expect(unit.code.match(/\[JsonProperty\("amount"/g)).toHaveLength(1);
  });

  test('double? 가 아니면 중단한다', () => {
    expect(() =>
      applyTypeCompat([classUnit('Ev', '        [Preserve]\n        [JsonProperty("amount")]\n        public double Amount;')], tables),
    ).toThrow(/double\? 필드가 아닙니다/);
  });
});

describe('applyTypeCompat: Params 로 옮겨진 필드', () => {
  const tables: TypeCompatTables = {
    ...NO_TABLES,
    forwardingProperties: [{ type: 'Opts', member: 'Token', memberType: 'string', target: 'Token', doc: '토큰이에요.' }],
  };
  const optsUnit = () =>
    classUnit('Opts', '        [Preserve]\n        [JsonProperty("params")]\n        public OptsParams Params;');
  const paramsUnit = () =>
    classUnit('OptsParams', '        [Preserve]\n        [JsonProperty("token")]\n        public string Token;');

  test('[JsonIgnore] 프로퍼티가 Params(없으면 새로 만듦)로 전달한다', () => {
    const units = applyTypeCompat([optsUnit(), paramsUnit()], tables);
    const code = units.find(u => u.name === 'Opts')!.code;
    expect(code).toMatch(/\[JsonIgnore\]\n\s+\[Obsolete\("Use Params\.Token"\)\]\n\s+public string Token/);
    expect(code).toContain('get => Params?.Token;');
    expect(code).toContain('if (Params == null) Params = new OptsParams();');
    expect(code).toContain('Params.Token = value;');
    // 확장 데이터 필드보다 앞에 놓인다
    expect(code.indexOf('public string Token')).toBeLessThan(code.indexOf('_extensionData'));
  });

  test('Params 안에 대상 필드가 없으면 중단한다', () => {
    expect(() => applyTypeCompat([optsUnit(), classUnit('OptsParams', '        [Preserve]\n        public int Other;')], tables)).toThrow(
      /OptsParams\.Token 필드를 찾지 못/,
    );
  });
});

describe('applyTypeCompat: 옛 필드·타입·enum 순서', () => {
  test('옛 필드는 확장 데이터 앞에 들어가고, 이미 있으면 중단한다', () => {
    const tables: TypeCompatTables = {
      ...NO_TABLES,
      legacyFields: [{ type: 'Res', member: 'error', declaration: '        [Obsolete("x")]\n        public string error;' }],
    };
    const [unit] = applyTypeCompat([classUnit('Res', '        [Preserve]\n        public int Value;')], tables);
    expect(unit.code).toContain('[Obsolete("x")]\n        public string error;');
    expect(unit.code.indexOf('public string error;')).toBeLessThan(unit.code.indexOf('_extensionData'));
    expect(() => applyTypeCompat([classUnit('Res', '        public string error;')], tables)).toThrow(/이미 같은 이름의 필드/);
  });

  test('옛 타입은 유닛으로 추가되고, 같은 이름이 이미 있으면 중단한다', () => {
    const tables: TypeCompatTables = { ...NO_TABLES, legacyTypes: [{ name: 'Old', code: '    public class Old {}' }] };
    expect(applyTypeCompat([], tables).map(u => u.name)).toEqual(['Old']);
    expect(() => applyTypeCompat([classUnit('Old', '')], tables)).toThrow(/이미 생성돼 있습니다/);
  });

  test('reorderEnumMembers: 이전 순서가 앞, 새 멤버는 뒤에 원래 순서대로', () => {
    const code = `    public enum E
    {
        [EnumMember(Value = "a")]
        A,
        [EnumMember(Value = "n1")]
        New1,
        [EnumMember(Value = "c")]
        C,
        [EnumMember(Value = "b")]
        B,
        [EnumMember(Value = "n2")]
        New2
    }`;
    const out = reorderEnumMembers(code, ['A', 'B', 'C'], 'E');
    const names = [...out.matchAll(/^\s{8}(\w+),?$/gm)].map(m => m[1]);
    expect(names).toEqual(['A', 'B', 'C', 'New1', 'New2']);
    // 각 멤버의 EnumMember 값이 이름과 함께 움직였다
    expect(out).toMatch(/\[EnumMember\(Value = "b"\)\]\n\s+B,/);
    expect(out.trimEnd().endsWith('}')).toBe(true);
  });

  test('이전 멤버가 생성되지 않았으면 중단한다', () => {
    expect(() => reorderEnumMembers('public enum E\n{\n    A\n}', ['A', 'Gone'], 'E')).toThrow(/'Gone'/);
  });
});

describe('API 메서드 표', () => {
  test('getApiCompat: Typed 이름과 래퍼 데이터를 돌려준다', () => {
    expect(getApiCompat('OpenPermissionDialog')).toMatchObject({
      methodName: 'OpenPermissionDialogTyped',
      returnTypeCompat: { api: 'OpenPermissionDialog', legacyReturnType: 'string', typedName: 'OpenPermissionDialogTyped' },
    });
    expect(getApiCompat('SafeAreaInsetsSubscribe').legacySubscribe?.paramType).toBe('SafeAreaInsetsSubscribe__0');
    expect(getApiCompat('GrantPromotionRewardForGame').legacyParamOverload?.legacyParamType).toBe('GrantPromotionRewardForGameParams');
    expect(getApiCompat('Anything')).toEqual({ methodName: undefined, returnTypeCompat: undefined, legacySubscribe: undefined, legacyParamOverload: undefined });
  });

  test('assertApiCompatTargets: 표의 API 가 없으면 중단한다', () => {
    const all = new Set([...RETURN_TYPE_COMPAT, ...LEGACY_SUBSCRIBE_COMPAT, ...LEGACY_PARAM_OVERLOAD_COMPAT].map(c => c.api));
    expect(() => assertApiCompatTargets(all)).not.toThrow();
    all.delete('GetUserKeyForGame');
    expect(() => assertApiCompatTargets(all)).toThrow(/GetUserKeyForGame/);
  });
});

describe('생성된 Runtime/SDK 에 반영', () => {
  const read = (file: string) => fs.readFileSync(path.join(SDK_DIR, file), 'utf-8');
  const typeText = fs
    .readdirSync(SDK_DIR)
    .filter(f => /^AIT\.Types\..+\.cs$/.test(f))
    .map(read)
    .join('\n');
  const apiText = fs
    .readdirSync(SDK_DIR)
    .filter(f => /^AIT\.[A-Za-z]+\.cs$/.test(f))
    .map(read)
    .join('\n');

  /** `public class <name> { ... }` 본문 (다음 클래스/enum 선언 직전까지) */
  function classBody(name: string): string {
    const start = typeText.search(new RegExp(`public class ${name}\\b`));
    expect(start, `class ${name}`).toBeGreaterThanOrEqual(0);
    const rest = typeText.slice(start + 1);
    const next = rest.search(/\n    (?:public (?:class|enum)|\[Serializable\])/);
    return rest.slice(0, next < 0 ? undefined : next);
  }

  test.each(STRING_ENUM_COMPAT)('$type.$member: string 필드 [Obsolete] + $memberTyped', ({ type, member }) => {
    const body = classBody(type);
    expect(body).toMatch(new RegExp(`\\[Obsolete\\("Use ${member}Typed"\\)\\]\\n\\s+public string ${member};`));
    expect(body).toMatch(new RegExp(`\\[JsonIgnore\\]\\n\\s+public \\w+\\? ${member}Typed\\n`));
  });

  test.each(OPTIONAL_NUMBER_COMPAT)('$type.$member: double [Obsolete] + double? $memberTyped', ({ type, member }) => {
    const body = classBody(type);
    expect(body).toMatch(new RegExp(`\\[Obsolete\\("Use ${member}Typed"\\)\\]\\n\\s+public double ${member};`));
    expect(body).toContain(`public double? ${member}Typed`);
  });

  test.each(FORWARDING_PROPERTY_COMPAT)('$type.$member: Params 로 전달하는 [Obsolete] 프로퍼티', ({ type, member, target }) => {
    const body = classBody(type);
    expect(body).toContain(`[Obsolete("Use Params.${target}")]`);
    expect(body).toContain(`get => Params?.${target};`);
    expect(body).toMatch(new RegExp(`public string ${member}\\n`));
  });

  test.each(LEGACY_FIELDS)('$type.$member: 옛 필드가 [Obsolete] 로 남아 있다', ({ type, member }) => {
    expect(classBody(type)).toMatch(new RegExp(`\\[Obsolete\\("[^"]+"\\)\\]\\n\\s+public string ${member};`));
  });

  test.each(LEGACY_TYPES)('$name: 옛 타입이 [Obsolete] 로 남아 있다', ({ name }) => {
    expect(typeText).toMatch(new RegExp(`\\[Obsolete\\("[^"]+"\\)\\]\\n\\s+public class ${name}\\n`));
  });

  test.each(Object.entries(ENUM_MEMBER_ORDER))('%s: 이전 SDK 멤버 순서를 앞에 둔다', (name, order) => {
    const start = typeText.indexOf(`public enum ${name}\n`);
    expect(start).toBeGreaterThanOrEqual(0);
    const body = typeText.slice(start, typeText.indexOf('\n    }', start));
    const members = [...body.matchAll(/^\s{8}(\w+),?$/gm)].map(m => m[1]);
    expect(members.slice(0, order.length)).toEqual([...order]);
  });

  test.each(RETURN_TYPE_COMPAT)('$api: 옛 이름은 $legacyReturnType 을 돌려주고 Typed 가 새 반환 타입이다', ({ api, legacyReturnType }) => {
    for (const wrapper of ['Awaitable', 'Task']) {
      expect(apiText).toMatch(new RegExp(`public static async ${wrapper}<${legacyReturnType}> ${api}\\(`));
      expect(apiText).toMatch(new RegExp(`public static async ${wrapper}<(?!${legacyReturnType}>)[\\w.]+> ${api}Typed\\(`));
    }
    expect(apiText).toContain(`[Obsolete("Use ${api}Typed")]`);
    // 옛 멤버는 이전 SDK와 같은 경로(typeName "string")로 값을 받고, Typed 는 새 타입 이름으로 받는다
    const legacy = apiText.slice(apiText.indexOf(`[Obsolete("Use ${api}Typed")]`));
    expect(legacy).toMatch(/RegisterCallback<string>\([\s\S]*?"string"\);/);
    expect(legacy).not.toContain(`await ${api}Typed(`);
  });

  test('SafeAreaInsetsSubscribe: 새 구독 오버로드와 옛 시그니처가 함께 있다', () => {
    expect(apiText).toContain('public static Action SafeAreaInsetsSubscribe(\n            Action<SafeAreaInsets> onEvent,');
    expect(apiText).toContain('public static async Awaitable<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0, int timeoutMs = 0)');
    expect(apiText).toContain('public static async Task<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0, int timeoutMs = 0)');
    // 인자 하나만 넘기는 옛 호출(null 포함)이 새 오버로드와 모호하지 않도록 기본값 없는 오버로드가 있다
    expect(apiText).toContain('public static Awaitable<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)');
    expect(apiText).toContain('public static Task<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)');
  });

  test('IAPGetCompletedOrRefundedOrders: 옛 (int timeoutMs = 0) 시그니처가 기본값까지 그대로고 새 시그니처의 첫 인자는 필수다', () => {
    expect(apiText).toContain('IAPGetCompletedOrRefundedOrders(int timeoutMs = 0)');
    expect(apiText).toMatch(/IAPGetCompletedOrRefundedOrders\(IAPGetCompletedOrRefundedOrdersParams paramsParam, int timeoutMs = 0\)/);
    expect(apiText).not.toMatch(/IAPGetCompletedOrRefundedOrders\(IAPGetCompletedOrRefundedOrdersParams paramsParam = null/);
  });

  test('GrantPromotionRewardForGame: 옛 매개변수 타입 오버로드가 새 타입으로 넘긴다', () => {
    for (const entry of LEGACY_PARAM_OVERLOAD_COMPAT) {
      expect(apiText).toContain(`${entry.api}(${entry.legacyParamType} ${entry.paramName}, int timeoutMs = 0)`);
    }
    // 옛 이름 하위 클래스는 옛 Params 필드 타입을 유지한다
    expect(typeText).toContain('public new GrantPromotionRewardForGameParamsParams Params;');
  });
});
