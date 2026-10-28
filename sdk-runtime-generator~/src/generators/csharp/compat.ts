/**
 * 하위 호환 멤버 생성.
 *
 * 3.x d.ts가 타입을 좁히거나 모양을 바꾼 곳에서도 origin/main이 공개하던 C# API는 한 줄도 지우지 않는다.
 * 옛 멤버는 같은 이름·같은 타입으로 남기고 `[Obsolete]`를 붙이며, 더 나은 타입의 멤버는 `Typed` 접미사로 곁에 둔다.
 *
 * 변환은 모두 선언 표(아래 *_COMPAT 상수)로 걸고, 코드를 고치는 함수는 표를 읽기만 한다.
 * 표에 적은 대상이 생성 결과에 없거나 모양이 다르면 조용히 넘어가지 않고 생성을 중단한다.
 *
 * - 필드 변환은 생성된 타입 유닛의 C# 텍스트를 고친다 (`applyTypeCompat`).
 * - API 메서드 변환은 템플릿 데이터(`getApiCompat`)로 전달해 `csharp-category-partial.hbs`가 그린다.
 * - 변환 도우미(`AITCompat`)는 `csharp-core.hbs`에 있다.
 */
import type { GeneratedTypeUnit } from '../../types.js';

// ---------------------------------------------------------------------------
// 필드: string -> enum
// ---------------------------------------------------------------------------

/**
 * 3.x에서 string 필드가 enum으로 바뀐 곳. 원래 이름은 string 그대로 두어 와이어에 쓰이는 값을 담고(정의에 없는
 * 값도 잃지 않는다), `<이름>Typed`는 그 문자열에서 계산하는 enum 프로퍼티다. 직렬화되는 것은 string 필드 하나뿐이다.
 */
export interface StringEnumCompat {
  type: string;
  member: string;
}

export const STRING_ENUM_COMPAT: readonly StringEnumCompat[] = [
  { type: 'EventLogParams', member: 'Log_type' },
  { type: 'DeclaredAgeRange', member: 'Status' },
  { type: 'Location', member: 'AccessLocation' },
  { type: 'SubmitGameCenterLeaderBoardScoreResponse', member: 'StatusCode' },
  { type: 'TossAdsAttachBannerOptions', member: 'Theme' },
  { type: 'TossAdsAttachBannerOptions', member: 'Tone' },
  { type: 'TossAdsAttachBannerOptions', member: 'Variant' },
  { type: 'TossAdsAttachOptions', member: 'Theme' },
];

// ---------------------------------------------------------------------------
// 필드: double -> double?
// ---------------------------------------------------------------------------

/**
 * 3.x에서 필수 number가 선택(`number | undefined`)이 된 곳. 원래 `double` 필드는 와이어 값을 그대로 담고,
 * `<이름>Typed`가 값이 없음을 null로 돌려준다. 값이 실제로 왔는지는 Newtonsoft의 `<이름>Specified` 규약으로 안다.
 */
export interface OptionalNumberCompat {
  type: string;
  member: string;
}

export const OPTIONAL_NUMBER_COMPAT: readonly OptionalNumberCompat[] = [
  { type: 'ContactsViralEventData', member: 'RewardAmount' },
];

// ---------------------------------------------------------------------------
// 필드: 최상위 -> Params 안으로 이동
// ---------------------------------------------------------------------------

/**
 * 3.x에서 최상위 필드가 `params` 객체 안으로 옮겨진 곳. 옛 이름은 `[JsonIgnore]` 프로퍼티로 남겨
 * `Params`(없으면 새로 만든다)의 같은 이름 필드로 전달한다. 그래서 옛 코드도 올바른 payload를 보낸다.
 */
export interface ForwardingPropertyCompat {
  type: string;
  member: string;
  /** 옛 이름이 가리키던 값의 타입 */
  memberType: string;
  /** 새 위치: `Params` 필드가 가진 타입 안의 필드 이름 */
  target: string;
  doc: string;
}

export const FORWARDING_PROPERTY_COMPAT: readonly ForwardingPropertyCompat[] = [
  { type: 'CheckoutPaymentOptions', member: 'PayToken', memberType: 'string', target: 'PayToken', doc: '결제 토큰이에요.' },
  { type: 'RequestTossPayPaysBillingOptions', member: 'WrappedToken', memberType: 'string', target: 'WrappedToken', doc: '정기결제 래핑 토큰이에요.' },
];

// ---------------------------------------------------------------------------
// 필드: 결과 클래스의 error / code
// ---------------------------------------------------------------------------

const ERROR_FIELD_MESSAGE = '오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.';

/** 이전 SDK가 결과 클래스에 붙이던 `error` 필드 (와이어 키 없이 C# 이름 그대로) */
export const LEGACY_ERROR_FIELD_TYPES: readonly string[] = [
  'AppLoginResult',
  'AppsInTossGlobals',
  'AttachBannerResult',
  'CompletedOrRefundedOrdersResult',
  'IAPGetPendingOrdersResult',
  'IAPGetProductItemListResult',
  'IapSubscriptionInfoResponse',
  'Location',
  'SafeAreaInsets',
  'SetScreenAwakeModeResult',
  'SetSecureScreenResult',
];

export interface LegacyFieldCompat {
  type: string;
  /** 필드 선언 (들여쓰기 8칸, 문서 주석 포함). 끝에 개행은 붙이지 않는다 */
  declaration: string;
  member: string;
}

export const LEGACY_FIELDS: readonly LegacyFieldCompat[] = [
  ...LEGACY_ERROR_FIELD_TYPES.map(type => ({
    type,
    member: 'error',
    declaration:
      `        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>\n` +
      `        [Obsolete("${ERROR_FIELD_MESSAGE}")]\n` +
      `        public string error;`,
  })),
  {
    type: 'GrantPromotionRewardResult',
    member: 'Code',
    declaration:
      `        [Preserve]\n` +
      `        [JsonProperty("code")]\n` +
      `        [Obsolete("오류 코드는 ErrorCode로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]\n` +
      `        public string Code;`,
  },
];

// ---------------------------------------------------------------------------
// 타입: 되살리는 옛 공개 타입
// ---------------------------------------------------------------------------

export interface LegacyTypeCompat {
  name: string;
  /** 헤더 없는 C# 선언 전체 (들여쓰기 4칸). 카테고리는 index.ts 가 이름 접두사로 다시 정한다 */
  code: string;
}

const EXTENSION_DATA_FIELD =
  '        [Preserve]\n' +
  '        [Newtonsoft.Json.JsonExtensionData]\n' +
  '        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;';

export const LEGACY_TYPES: readonly LegacyTypeCompat[] = [
  {
    name: 'SafeAreaInsetsSubscribe__0',
    code: `    /// <summary>
    /// 이전 SDK의 SafeAreaInsetsSubscribe 매개변수 묶음이다. 새 코드는
    /// <c>AIT.SafeAreaInsetsSubscribe(onEvent, onError)</c>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("AIT.SafeAreaInsetsSubscribe(Action<SafeAreaInsets> onEvent, Action<AITException> onError)를 사용하세요.")]
    public class SafeAreaInsetsSubscribe__0
    {
        [JsonIgnore]
        public System.Action<SafeAreaInsets> OnEvent;

${EXTENSION_DATA_FIELD}
    }`,
  },
  {
    name: 'number',
    code: `    /// <summary>
    /// 이전 생성기가 잘못 만들어 공개한 빈 클래스다. 쓸 곳이 없다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("쓸 곳이 없는 빈 클래스예요.")]
    public class number
    {
    }`,
  },
];

// ---------------------------------------------------------------------------
// enum 멤버 순서
// ---------------------------------------------------------------------------

/**
 * 3.x가 멤버 순서를 바꾼 enum. 순서가 바뀌면 `(int)` 캐스트와 저장된 정수 값이 어긋나므로
 * 이전 SDK의 순서를 앞에 두고, 새로 생긴 멤버는 그 뒤에 원래 순서대로 붙인다.
 */
export const ENUM_MEMBER_ORDER: Readonly<Record<string, readonly string[]>> = {
  PermissionName: ['Clipboard', 'Contacts', 'Photos', 'Geolocation', 'Camera', 'Microphone'],
  ConsentedUserDataKey: [
    'USER_NAME',
    'USER_GENDER',
    'USER_NATIONALITY',
    'USER_BIRTHDAY',
    'USER_PHONE',
    'USER_ADDRESS',
    'USER_EMAIL',
    'USER_CONSUMPTION_HISTORY',
  ],
};

// ---------------------------------------------------------------------------
// 코드 변환
// ---------------------------------------------------------------------------

function fail(message: string): never {
  throw new Error(`하위 호환 변환 실패: ${message}`);
}

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

interface FieldMatch {
  start: number;
  end: number;
  docs: string;
  jsonProperty?: string;
  type: string;
  trailing: string;
}

/** 클래스 유닛에서 `[Preserve]` 필드 선언 블록(문서 주석 포함)을 찾는다 */
function findField(code: string, member: string): FieldMatch | undefined {
  const re = new RegExp(
    '((?: {8}///[^\\n]*\\n)*)' +
    ' {8}\\[Preserve\\]\\n' +
    '(?: {8}\\[JsonProperty\\("([^"]+)"\\)\\]\\n)?' +
    ` {8}public ([^\\n]+?) ${escapeRegExp(member)};([^\\n]*)\\n`,
  );
  const m = re.exec(code);
  if (!m) return undefined;
  return {
    start: m.index,
    end: m.index + m[0].length,
    docs: m[1],
    jsonProperty: m[2],
    type: m[3],
    trailing: m[4],
  };
}

function jsonPropertyLine(jsonProperty: string | undefined, extra?: string): string {
  if (!jsonProperty) return '';
  return `        [JsonProperty("${jsonProperty}"${extra ? `, ${extra}` : ''})]\n`;
}

function unitFor(units: GeneratedTypeUnit[], type: string, what: string): GeneratedTypeUnit {
  const unit = units.find(u => u.name === type);
  if (!unit) fail(`${what}의 대상 타입 '${type}'이(가) 생성되지 않았습니다 (compat.ts 표 확인)`);
  return unit;
}

/** 클래스 끝의 `_extensionData` 블록 앞에 멤버를 끼워 넣는다 */
function insertBeforeExtensionData(code: string, members: string, what: string): string {
  const marker = '\n\n        [Preserve]\n        [Newtonsoft.Json.JsonExtensionData]';
  const idx = code.indexOf(marker);
  if (idx < 0) fail(`${what}: _extensionData 블록을 찾지 못했습니다`);
  return `${code.slice(0, idx)}\n${members}${code.slice(idx)}`;
}

function applyStringEnum(units: GeneratedTypeUnit[], entry: StringEnumCompat): void {
  const what = `${entry.type}.${entry.member}`;
  const unit = unitFor(units, entry.type, what);
  const f = findField(unit.code, entry.member);
  if (!f) fail(`${what}: 필드 선언을 찾지 못했습니다`);
  const enumType = f.type.replace(/\?$/, '');
  if (enumType === 'string') fail(`${what}: 이미 string 필드입니다`);
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(enumType)) fail(`${what}: enum 타입이 아닙니다 (${f.type})`);

  const typed = `${entry.member}Typed`;
  const original =
    `${f.docs}        [Preserve]\n${jsonPropertyLine(f.jsonProperty)}` +
    `        [Obsolete("Use ${typed}")]\n` +
    `        public string ${entry.member};${f.trailing}\n`;
  const added =
    `\n        /// <summary>\n` +
    `        /// <see cref="${entry.member}"/>를 <see cref="${enumType}"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,\n` +
    `        /// 그 문자열은 <see cref="${entry.member}"/>에 그대로 남는다. 값을 쓰면 <see cref="${entry.member}"/>도 함께 바뀐다.\n` +
    `        /// </summary>\n` +
    `        [JsonIgnore]\n` +
    `        public ${enumType}? ${typed}\n` +
    `        {\n` +
    `            get => AITCompat.ParseEnum<${enumType}>(${entry.member});\n` +
    `            set => ${entry.member} = AITCompat.ToWire(value);\n` +
    `        }\n`;
  unit.code = unit.code.slice(0, f.start) + original + added + unit.code.slice(f.end);
}

function applyOptionalNumber(units: GeneratedTypeUnit[], entry: OptionalNumberCompat): void {
  const what = `${entry.type}.${entry.member}`;
  const unit = unitFor(units, entry.type, what);
  const f = findField(unit.code, entry.member);
  if (!f) fail(`${what}: 필드 선언을 찾지 못했습니다`);
  if (f.type !== 'double?') fail(`${what}: double? 필드가 아닙니다 (${f.type})`);
  if (!f.jsonProperty) fail(`${what}: JsonProperty가 없습니다`);

  const typed = `${entry.member}Typed`;
  const specified = `${entry.member}Specified`;
  // NullValueHandling.Ignore: 와이어가 null을 보내도 double 필드가 역직렬화 오류를 내지 않는다
  const original =
    `${f.docs}        [Preserve]\n${jsonPropertyLine(f.jsonProperty, 'NullValueHandling = NullValueHandling.Ignore')}` +
    `        [Obsolete("Use ${typed}")]\n` +
    `        public double ${entry.member};\n`;
  const added =
    `\n        /// <summary>와이어에 <see cref="${entry.member}"/> 값이 왔는지 여부. Newtonsoft가 역직렬화할 때 채운다.</summary>\n` +
    `        [JsonIgnore]\n` +
    `        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]\n` +
    `        public bool ${specified} { get; set; }\n` +
    `\n        /// <summary>\n` +
    `        /// <see cref="${entry.member}"/>를 값이 없으면 null로 돌려준다. 값을 쓰면 <see cref="${entry.member}"/>도 함께 바뀐다.\n` +
    `        /// </summary>\n` +
    `        [JsonIgnore]\n` +
    `        public double? ${typed}\n` +
    `        {\n` +
    `            get => ${specified} || ${entry.member} != 0 ? ${entry.member} : (double?)null;\n` +
    `            set\n` +
    `            {\n` +
    `                ${entry.member} = value ?? 0;\n` +
    `                ${specified} = value.HasValue;\n` +
    `            }\n` +
    `        }\n`;
  unit.code = unit.code.slice(0, f.start) + original + added + unit.code.slice(f.end);
}

function applyForwardingProperty(units: GeneratedTypeUnit[], entry: ForwardingPropertyCompat): void {
  const what = `${entry.type}.${entry.member}`;
  const unit = unitFor(units, entry.type, what);
  if (findField(unit.code, entry.member)) fail(`${what}: 이미 같은 이름의 필드가 있습니다`);
  const params = findField(unit.code, 'Params');
  if (!params) fail(`${what}: Params 필드를 찾지 못했습니다`);
  const paramsType = params.type;
  const target = units.find(u => u.name === paramsType);
  if (!target || !findField(target.code, entry.target)) {
    fail(`${what}: ${paramsType}.${entry.target} 필드를 찾지 못했습니다`);
  }

  const property =
    `        /// <summary>${entry.doc}</summary>\n` +
    `        [JsonIgnore]\n` +
    `        [Obsolete("Use Params.${entry.target}")]\n` +
    `        public ${entry.memberType} ${entry.member}\n` +
    `        {\n` +
    `            get => Params?.${entry.target};\n` +
    `            set\n` +
    `            {\n` +
    `                if (Params == null) Params = new ${paramsType}();\n` +
    `                Params.${entry.target} = value;\n` +
    `            }\n` +
    `        }\n`;
  unit.code = insertBeforeExtensionData(unit.code, property, what);
}

function applyLegacyField(units: GeneratedTypeUnit[], entry: LegacyFieldCompat): void {
  const what = `${entry.type}.${entry.member}`;
  const unit = unitFor(units, entry.type, what);
  if (findField(unit.code, entry.member) || new RegExp(`public [^\\n]+ ${escapeRegExp(entry.member)};`).test(unit.code)) {
    fail(`${what}: 이미 같은 이름의 필드가 있습니다`);
  }
  unit.code = insertBeforeExtensionData(unit.code, `${entry.declaration}\n`, what);
}

/** enum 유닛의 멤버(속성 줄 포함) 블록을 이전 SDK의 순서로 다시 놓는다 */
export function reorderEnumMembers(code: string, order: readonly string[], enumName: string): string {
  const open = code.indexOf('{');
  const close = code.lastIndexOf('}');
  if (open < 0 || close < open) fail(`enum ${enumName}: 본문을 찾지 못했습니다`);
  const body = code.slice(open + 1, close);
  const members = body
    .split(/,\n/)
    .map(chunk => chunk.replace(/^\n+/, '').replace(/\s+$/, ''))
    .filter(chunk => chunk.trim().length > 0);

  const nameOf = (chunk: string): string => {
    const lines = chunk.split('\n');
    return lines[lines.length - 1].trim().replace(/\s*=.*$/, '');
  };
  const byName = new Map(members.map(chunk => [nameOf(chunk), chunk] as const));
  for (const name of order) {
    if (!byName.has(name)) fail(`enum ${enumName}: 이전 SDK 멤버 '${name}'이(가) 생성되지 않았습니다`);
  }
  const ordered = [...order.map(name => byName.get(name)!), ...members.filter(chunk => !order.includes(nameOf(chunk)))];
  return `${code.slice(0, open + 1)}\n${ordered.join(',\n')}\n    ${code.slice(close)}`;
}

/** 타입 유닛에 적용할 변환 표 묶음. 테스트가 작은 표로 바꿔 끼울 수 있다 */
export interface TypeCompatTables {
  stringEnums: readonly StringEnumCompat[];
  optionalNumbers: readonly OptionalNumberCompat[];
  forwardingProperties: readonly ForwardingPropertyCompat[];
  legacyFields: readonly LegacyFieldCompat[];
  legacyTypes: readonly LegacyTypeCompat[];
  enumOrder: Readonly<Record<string, readonly string[]>>;
}

export const DEFAULT_TYPE_COMPAT_TABLES: TypeCompatTables = {
  stringEnums: STRING_ENUM_COMPAT,
  optionalNumbers: OPTIONAL_NUMBER_COMPAT,
  forwardingProperties: FORWARDING_PROPERTY_COMPAT,
  legacyFields: LEGACY_FIELDS,
  legacyTypes: LEGACY_TYPES,
  enumOrder: ENUM_MEMBER_ORDER,
};

/**
 * 타입 유닛에 하위 호환 멤버와 타입을 적용한다. 입력 유닛은 건드리지 않고 새 배열을 돌려준다.
 */
export function applyTypeCompat(
  units: readonly GeneratedTypeUnit[],
  tables: TypeCompatTables = DEFAULT_TYPE_COMPAT_TABLES,
): GeneratedTypeUnit[] {
  const result = units.map(unit => ({ ...unit }));

  for (const entry of tables.stringEnums) applyStringEnum(result, entry);
  for (const entry of tables.optionalNumbers) applyOptionalNumber(result, entry);
  for (const entry of tables.forwardingProperties) applyForwardingProperty(result, entry);
  for (const entry of tables.legacyFields) applyLegacyField(result, entry);

  for (const [enumName, order] of Object.entries(tables.enumOrder)) {
    const unit = unitFor(result, enumName, `enum ${enumName}`);
    unit.code = reorderEnumMembers(unit.code, order, enumName);
  }

  for (const legacy of tables.legacyTypes) {
    if (result.some(u => u.name === legacy.name)) {
      fail(`옛 타입 '${legacy.name}'이(가) 이미 생성돼 있습니다 (compat.ts LEGACY_TYPES 확인)`);
    }
    result.push({ name: legacy.name, code: legacy.code, category: 'Other' });
  }
  return result;
}

// ---------------------------------------------------------------------------
// API 메서드
// ---------------------------------------------------------------------------

/**
 * 3.x에서 반환 타입이 바뀐 API. 새 반환 타입은 `<이름>Typed`로 두고, 원래 이름은 옛 반환 타입으로 남긴다.
 * (C#은 반환 타입만 다른 오버로드를 허용하지 않는다.)
 *
 * 옛 멤버는 Typed 결과를 다시 해석하지 않고, main과 같은 경로(같은 extern을 typeName "string"으로 호출)로
 * 값을 받는다. 그래서 `AITCore`의 string 처리가 만드는 문자열이 main과 바이트까지 같다.
 * jslib는 typeName "string"을 받으면 main의 인코딩(`data: JSON.stringify(result)`)으로 보낸다
 * (`hasLegacyStringReturn`).
 */
export interface ReturnTypeCompat {
  api: string;
  legacyReturnType: 'string';
}

export const RETURN_TYPE_COMPAT: readonly ReturnTypeCompat[] = [
  { api: 'OpenPermissionDialog', legacyReturnType: 'string' },
  { api: 'GetUserKeyForGame', legacyReturnType: 'string' },
];

/** jslib 생성기용: 이 API(camelCase 또는 PascalCase 이름)가 옛 string 반환 경로를 함께 지원해야 하는지 */
export function hasLegacyStringReturn(apiName: string): boolean {
  const lower = apiName.charAt(0).toLowerCase() + apiName.slice(1);
  return RETURN_TYPE_COMPAT.some(c => c.api.charAt(0).toLowerCase() + c.api.slice(1) === lower);
}

/**
 * 3.x에서 매개변수가 묶음 객체에서 콜백으로 바뀐 구독 API. 옛 시그니처를 `[Obsolete]` 오버로드로 남기고
 * 새 구독을 시작한 뒤 해제 Action을 돌려준다.
 */
export interface LegacySubscribeCompat {
  api: string;
  /** 옛 매개변수 묶음 타입 (`OnEvent` 필드를 가진다) */
  paramType: string;
  paramName: string;
  eventType: string;
}

export const LEGACY_SUBSCRIBE_COMPAT: readonly LegacySubscribeCompat[] = [
  { api: 'SafeAreaInsetsSubscribe', paramType: 'SafeAreaInsetsSubscribe__0', paramName: '__0', eventType: 'SafeAreaInsets' },
];

/**
 * 3.x에서 매개변수 타입이 새 타입으로 대체돼 옛 이름이 하위 클래스(`DEPRECATED_TYPE_SHIMS`)로 남은 API.
 * 옛 타입을 받는 `[Obsolete]` 오버로드를 두고 새 타입으로 바꿔 넘긴다.
 */
export interface LegacyParamOverloadCompat {
  api: string;
  /** 옛 매개변수 타입 */
  legacyParamType: string;
  /** 새 매개변수 타입 */
  paramType: string;
  paramName: string;
  /** `paramName`(옛 타입, null일 수 있음)을 새 타입으로 바꾸는 C# 식 */
  convert: string;
}

export const LEGACY_PARAM_OVERLOAD_COMPAT: readonly LegacyParamOverloadCompat[] = [
  {
    api: 'GrantPromotionRewardForGame',
    legacyParamType: 'GrantPromotionRewardForGameParams',
    paramType: 'GrantPromotionRewardParams',
    paramName: 'paramsParam',
    convert:
      'paramsParam == null ? null : new GrantPromotionRewardParams { ' +
      'Params = paramsParam.Params ?? ((GrantPromotionRewardParams)paramsParam).Params, ' +
      '_extensionData = paramsParam._extensionData }',
  },
];

export interface ApiCompatData {
  /** 새 반환 타입 메서드의 C# 이름 (`OpenPermissionDialogTyped`). 바꾸지 않으면 undefined */
  methodName?: string;
  returnTypeCompat?: ReturnTypeCompat & { typedName: string };
  legacySubscribe?: LegacySubscribeCompat;
  legacyParamOverload?: LegacyParamOverloadCompat;
}

/** API 이름(PascalCase)에 걸린 하위 호환 템플릿 데이터를 돌려준다 */
export function getApiCompat(pascalName: string): ApiCompatData {
  const data: ApiCompatData = {};
  const ret = RETURN_TYPE_COMPAT.find(c => c.api === pascalName);
  if (ret) {
    data.methodName = `${pascalName}Typed`;
    data.returnTypeCompat = { ...ret, typedName: `${pascalName}Typed` };
  }
  data.legacySubscribe = LEGACY_SUBSCRIBE_COMPAT.find(c => c.api === pascalName);
  data.legacyParamOverload = LEGACY_PARAM_OVERLOAD_COMPAT.find(c => c.api === pascalName);
  return data;
}

/** 표에 적힌 API가 모두 실제 API 목록에 있는지 확인한다 (없으면 생성을 중단) */
export function assertApiCompatTargets(apiPascalNames: ReadonlySet<string>): void {
  const entries: Array<{ api: string }> = [...RETURN_TYPE_COMPAT, ...LEGACY_SUBSCRIBE_COMPAT, ...LEGACY_PARAM_OVERLOAD_COMPAT];
  for (const entry of entries) {
    if (!apiPascalNames.has(entry.api)) {
      fail(`API '${entry.api}'을(를) 찾지 못했습니다 (compat.ts API 표 확인)`);
    }
  }
}
