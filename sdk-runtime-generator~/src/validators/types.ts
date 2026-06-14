import { ParsedAPI, ParsedType, ValidationError } from '../types.js';
import picocolors from 'picocolors';

/**
 * 지원되는 타입 목록
 */
const SUPPORTED_PRIMITIVES = new Set(['string', 'number', 'boolean', 'void', 'any', 'unknown', 'object', 'null', 'undefined', 'never', 'symbol']);

/**
 * C# 타입 매핑 테이블
 */
export const TYPE_MAPPING: Record<string, string> = {
  // Primitives
  string: 'string',
  number: 'double',
  boolean: 'bool',
  void: 'void',
  any: 'void', // any는 void로 처리 (Promise<any> → Task)
  unknown: 'object',
  symbol: 'object', // C#에 대응 타입 없음 — object로 매핑 (예: Record<string, Primitive> 값)

  // Unity types
  Date: 'DateTime',
  ArrayBuffer: 'byte[]',
  Uint8Array: 'byte[]',

  // Common types
  Error: 'Exception',
};

/**
 * 타입 지원 여부 확인
 */
export function isTypeSupported(type: ParsedType): boolean {
  // nullable 타입은 base 타입이 지원되면 허용
  // isNullable이 설정된 경우, 이미 base 타입으로 변환되어 있음
  // 따라서 별도 처리 없이 kind에 따른 검증 진행

  switch (type.kind) {
    case 'primitive':
      return SUPPORTED_PRIMITIVES.has(type.name);

    case 'promise':
      // Promise의 내부 타입 검증
      return type.promiseType ? isTypeSupported(type.promiseType) : false;

    case 'array':
      // Array의 요소 타입 검증
      return type.elementType ? isTypeSupported(type.elementType) : false;

    case 'object':
      // Object는 허용 (프로퍼티가 있으면 재귀 검증)
      if (type.properties && type.properties.length > 0) {
        return type.properties.every(prop => isTypeSupported(prop.type));
      }
      // 프로퍼티 없는 object도 허용 (Named type이거나 any)
      return true;

    case 'union':
      // Discriminated Union은 항상 지원 (객체 + 문자열 리터럴)
      if (type.isDiscriminatedUnion) {
        return true;
      }
      // Union의 모든 타입 검증
      return type.unionTypes ? type.unionTypes.every(t => isTypeSupported(t)) : false;

    case 'function':
      // 함수 타입은 System.Action으로 매핑 가능
      return true;

    case 'record':
      // Record<K, V>는 Dictionary<K, V>로 매핑 가능
      if (type.keyType && type.valueType) {
        return isTypeSupported(type.keyType) && isTypeSupported(type.valueType);
      }
      return true;

    case 'unknown':
      // 알 수 없는 타입은 허용하지 않음
      // 단, nullable 타입인 경우 기본 허용 (타입 이름이 있으면 object로 처리됨)
      if (type.isNullable && type.name && !type.name.includes('|')) {
        return true;
      }
      // Named type이면서 valid한 식별자인 경우 허용 (외부 타입 참조)
      // 예: IsAdMobLoadedOptions, LoadAdMobOptions 등
      if (type.name && /^[A-Z][a-zA-Z0-9_]*$/.test(type.name)) {
        return true;
      }
      return false;

    default:
      return false;
  }
}

/**
 * API 타입 매핑 검증
 */
export function validateTypeMapping(api: ParsedAPI): ValidationError[] {
  const errors: ValidationError[] = [];

  // 파라미터 타입 검증
  for (const param of api.parameters) {
    if (!isTypeSupported(param.type)) {
      errors.push({
        api: api.name,
        type: 'type-unsupported',
        message: picocolors.red(`
❌ 지원되지 않는 타입: ${param.type.raw}

API: ${api.name}
Parameter: ${param.name}
Type: ${param.type.raw}
Kind: ${param.type.kind}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/validators/types.ts에 타입 매핑 추가
2. 또는 src/templates/에 수동 템플릿 작성

지원 가능한 타입:
- Primitives: string, number, boolean, void
- Objects: interface { ... }
- Arrays: T[]
- Promises: Promise<T>
- Unions: T | U

생성 중단됨.
        `),
        suggestion: `${param.type.kind} 타입에 대한 매핑 추가 필요`,
      });
    }
  }

  // 반환 타입 검증
  if (!isTypeSupported(api.returnType)) {
    errors.push({
      api: api.name,
      type: 'type-unsupported',
      message: picocolors.red(`
❌ 지원되지 않는 반환 타입: ${api.returnType.raw}

API: ${api.name}
Return Type: ${api.returnType.raw}
Kind: ${api.returnType.kind}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/validators/types.ts에 타입 매핑 추가
2. 또는 src/templates/에 수동 템플릿 작성

생성 중단됨.
      `),
      suggestion: `${api.returnType.kind} 타입에 대한 매핑 추가 필요`,
    });
  }

  return errors;
}

/**
 * 전체 API 목록에 대한 타입 검증
 */
export function validateAllTypes(apis: ParsedAPI[]): { success: boolean; errors: ValidationError[] } {
  const allErrors: ValidationError[] = [];

  for (const api of apis) {
    const errors = validateTypeMapping(api);
    allErrors.push(...errors);
  }

  return {
    success: allErrors.length === 0,
    errors: allErrors,
  };
}

/**
 * 알려진 외부 타입 (런타임에만 존재하는 타입)
 * 이 타입들은 web-bridge에서 참조되지만 TypeScript 정의가 없어서 object로 매핑됨
 *
 * 참고: InterstitialAd, RewardedAd, ResponseInfo 등의 AdMob 타입은
 * @apps-in-toss/native-modules에 정의되어 있으므로 제외됨 (파서가 자동으로 파싱)
 */
const EXTERNAL_TYPES = new Set<string>([
  // 현재 런타임 전용 타입 없음 - 필요시 추가
]);

/**
 * 타입 이름에서 외부 타입 체크
 */
function isExternalType(typeName: string): boolean {
  if (!typeName) return false;
  // import("path").TypeName 형식에서 TypeName만 추출
  const simpleName = typeName.includes('.')
    ? typeName.split('.').pop() || typeName
    : typeName;
  const cleanName = simpleName.replace(/["'{}(),;\s<>|]/g, '').replace(/\$\d+$/, '').trim();

  return EXTERNAL_TYPES.has(cleanName);
}

/**
 * C# 값 타입 (Value Types) 목록
 * 이 타입들만 Nullable<T> 또는 T?를 사용할 수 있음
 * 참조 타입(string, object, 클래스 등)은 이미 null이 될 수 있으므로 ? 접미사 불필요
 */
const CSHARP_VALUE_TYPES = new Set(['int', 'double', 'float', 'bool', 'long', 'short', 'byte', 'char', 'decimal', 'DateTime']);

/**
 * TypeScript 타입을 C# 타입으로 변환
 */
export function mapToCSharpType(type: ParsedType): string {
  // 기본 타입 변환
  const baseType = mapToCSharpTypeCore(type);

  // nullable 타입에 ? 접미사 추가 (값 타입만)
  // C#에서 Nullable<T>는 값 타입만 지원함
  // 참조 타입(string, object, class)은 이미 null 할당 가능하므로 ? 불필요
  // Unity의 기본 설정은 Nullable Reference Types가 비활성화되어 있음
  if (type.isNullable && !baseType.endsWith('?') && !baseType.endsWith('[]')) {
    // 값 타입인지 확인
    if (CSHARP_VALUE_TYPES.has(baseType)) {
      return baseType + '?';
    }
    // 참조 타입은 그대로 반환
  }

  return baseType;
}

/**
 * TypeScript 타입을 C# 타입으로 변환 (내부 구현)
 */
function mapToCSharpTypeCore(type: ParsedType): string {
  // 외부 타입 체크 (모든 kind에 대해 먼저 체크)
  if (isExternalType(type.name)) {
    return 'object';
  }

  switch (type.kind) {
    case 'primitive':
      return TYPE_MAPPING[type.name] || type.name;

    case 'promise':
      // Promise<T> -> Task<T> 또는 void (callback 기반)
      if (type.promiseType) {
        const innerType = mapToCSharpType(type.promiseType);
        return innerType === 'void' ? 'void' : innerType;
      }
      return 'void';

    case 'array':
      if (type.elementType) {
        const elementType = mapToCSharpType(type.elementType);
        return `${elementType}[]`;
      }
      return 'object[]';

    case 'object':
      // 객체는 클래스로 생성해야 함
      // import("path").TypeName 형식에서 TypeName만 추출
      let objectName = type.name.includes('.')
        ? type.name.split('.').pop() || type.name
        : type.name;

      // 인라인 객체 리터럴 감지: { prop: type; ... } 형식
      // 이런 타입은 클래스로 매핑할 수 없으므로 object로 반환
      if (objectName.trim().startsWith('{') || objectName.includes(':')) {
        return 'object';
      }

      // 특수 문자 제거 (중괄호, 콤마, 공백, 세미콜론, C# 식별자로 유효하지 않은 문자 등)
      // TypeScript 빌드 시 생성되는 $1, $2 등의 접미사도 제거
      let cleanName = objectName.replace(/["'{}(),;\s<>|]/g, '').replace(/\$\d+$/, '').trim();

      // __type 또는 빈 이름은 익명 타입
      if (cleanName === '__type' || !cleanName || cleanName.startsWith('{')) {
        // raw 필드에서 실제 타입 이름 추출 시도 (import("...").TypeName 형식)
        // 단, raw가 '{'로 시작하면 인라인 객체이므로 스킵
        if (type.raw && type.raw.includes('.') && !type.raw.trim().startsWith('{')) {
          const rawTypeName = type.raw.split('.').pop()?.replace(/["'{}(),;\s<>|]/g, '').replace(/\$\d+$/, '').trim();
          if (rawTypeName && rawTypeName !== '__type' && !rawTypeName.startsWith('{')) {
            // 외부 타입 체크: raw에서 추출한 타입 이름도 외부 타입인지 확인
            if (isExternalType(rawTypeName)) {
              return 'object';
            }
            return rawTypeName;
          }
        }
        return 'object'; // C#의 object 타입으로 매핑
      }

      // 외부 타입 체크: cleanName도 확인
      if (isExternalType(cleanName)) {
        return 'object';
      }

      return cleanName;

    case 'union':
      // Named union type이면 이름 그대로 반환 (enum으로 생성됨)
      // 예: PermissionName, PermissionAccess, HapticFeedbackType 등
      if (type.name && !type.name.includes('|') && !type.name.includes('"') && !type.name.includes("'")) {
        // 특수 문자 제거 후 $1, $2 등의 접미사도 제거
        const cleanName = type.name.replace(/["'{}()|,;\s<>]/g, '').replace(/\$\d+$/, '').trim();
        if (cleanName && cleanName !== '__type' && !cleanName.startsWith('{')) {
          return cleanName;
        }
      }

      // Union 타입이 import 경로를 포함하면 타입 이름 추출
      // 예: import("...").GameCenterGameProfileResponse -> GameCenterGameProfileResponse
      if (type.name && (type.name.includes('.') || type.name.includes('import('))) {
        const typeName = type.name.split('.').pop() || type.name;
        const cleanName = typeName.replace(/["'{}()|,;\s<>]/g, '').replace(/\$\d+$/, '').trim();

        if (cleanName && cleanName !== '__type') {
          return cleanName;
        }
      }

      // Union 타입은 첫 번째 비-undefined/비-익명 타입 사용
      if (type.unionTypes && type.unionTypes.length > 0) {
        // undefined와 익명 타입(__type)을 제외한 첫 번째 타입
        const namedType = type.unionTypes.find(
          t => t.name !== 'undefined' && t.name !== '__type' && !t.name.startsWith('{') && !t.name.includes('|')
        );
        if (namedType) {
          return mapToCSharpType(namedType);
        }

        // 익명 타입이지만 properties가 있는 타입
        const nonUndefined = type.unionTypes.find(t => t.name !== 'undefined');
        if (nonUndefined) {
          return mapToCSharpType(nonUndefined);
        }

        return mapToCSharpType(type.unionTypes[0]);
      }

      return 'object';

    case 'function':
      // 함수 타입 매핑
      // 먼저 반환 타입이 void인지 확인
      const isVoidReturn = !type.functionReturnType ||
        (type.functionReturnType.kind === 'primitive' && type.functionReturnType.name === 'void') ||
        (type.functionReturnType.kind === 'primitive' && type.functionReturnType.name === 'any');

      if (!isVoidReturn && type.functionReturnType) {
        // 반환 타입이 있는 함수: Func<T1, T2, ..., TResult>
        let returnType = mapToCSharpType(type.functionReturnType);

        // void가 매핑되면 Action 사용
        if (returnType === 'void') {
          // fall through to Action handling below
        } else {
          // Promise<T> 반환은 T로 단순화 (JS 측에서 await 처리)
          if (type.functionReturnType.kind === 'promise' && type.functionReturnType.promiseType) {
            returnType = mapToCSharpType(type.functionReturnType.promiseType);
          }
          // union 타입 (boolean | Promise<boolean>)은 첫 번째 타입 사용
          if (type.functionReturnType.kind === 'union' && type.functionReturnType.unionTypes?.[0]) {
            let firstType = type.functionReturnType.unionTypes[0];
            if (firstType.kind === 'promise' && firstType.promiseType) {
              firstType = firstType.promiseType;
            }
            returnType = mapToCSharpType(firstType);
          }

          // void가 아닌 경우만 Func 반환
          if (returnType !== 'void') {
            if (type.functionParams && type.functionParams.length > 0) {
              const paramTypes = type.functionParams.map(p => mapToCSharpType(p));
              return `System.Func<${paramTypes.join(', ')}, ${returnType}>`;
            }
            return `System.Func<${returnType}>`;
          }
        }
      }

      // 반환 타입이 void인 함수: Action
      if (type.functionParams && type.functionParams.length > 0) {
        // 파라미터가 있는 함수: Action<T1, T2, ...>
        const paramTypes = type.functionParams.map(p => mapToCSharpType(p));
        return `System.Action<${paramTypes.join(', ')}>`;
      }
      // 파라미터 없는 함수: Action
      return 'System.Action';

    case 'record':
      // Record<K, V> -> Dictionary<K, V>
      if (type.keyType && type.valueType) {
        const keyType = mapToCSharpType(type.keyType);
        let valueType = mapToCSharpType(type.valueType);
        // Primitive union (string | number | boolean)은 object로
        if (type.valueType.kind === 'union') {
          valueType = 'object';
        }
        // 'never' 타입은 C#에서 유효하지 않으므로 object로 변환
        if (valueType === 'never') {
          valueType = 'object';
        }
        return `Dictionary<${keyType}, ${valueType}>`;
      }
      return 'Dictionary<string, object>';

    case 'unknown':
      // 제네릭 텍스트(Record<K, V>, Partial<Record<...>> 등)가 unknown으로 떨어진 경우,
      // 아래 split/strip은 "ConsentedUserDataKeystring" 같은 깨진 식별자를 만들므로
      // (CS0246 — 존재하지 않는 타입) 안전한 Dictionary 매핑으로 우회한다.
      // 정상 경로는 parser가 kind:'record'로 분류하는 것이고, 이 분기는 방어선이다.
      if (type.name && type.name.includes('Record<')) {
        return 'Dictionary<string, object>';
      }
      // unknown 타입이지만 name에 import 경로가 있으면 타입 이름 추출
      // 예: import("...").GameCenterGameProfileResponse -> GameCenterGameProfileResponse
      if (type.name && type.name.includes('.')) {
        const typeName = type.name.split('.').pop() || type.name;
        const cleanName = typeName.replace(/["'{}()|,;\s<>]/g, '').replace(/\$\d+$/, '').trim();
        if (cleanName && cleanName !== '__type' && cleanName !== 'undefined') {
          return cleanName;
        }
      }
      return 'object';

    default:
      return 'object';
  }
}
