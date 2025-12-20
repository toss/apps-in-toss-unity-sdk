/**
 * 파싱된 API 정보
 */
export interface ParsedAPI {
  name: string;
  pascalName: string; // C# PascalCase 이름 (예: AppLogin)
  originalName: string; // 원본 TypeScript 이름 (예: appLogin)
  category: string;
  file: string;
  description?: string;
  returnDescription?: string; // @returns 태그 설명
  examples?: string[]; // @example 태그 코드 예제들
  parameters: ParsedParameter[];
  returnType: ParsedType;
  isAsync: boolean;
  hasPermission: boolean;
  // 네임스페이스 객체 지원 (IAP, Storage 등)
  namespace?: string; // 네임스페이스 이름 (예: 'IAP', 'Storage')
  isDeprecated?: boolean; // deprecated 여부
  deprecatedMessage?: string; // 대체 API 안내 메시지
  // 이벤트 API 지원 (tdsEvent, graniteEvent, appsInTossEvent)
  isEventSubscription?: boolean; // addEventListener 패턴인지
  eventName?: string; // 이벤트 이름 (예: 'navigationAccessoryEvent')
  eventDataType?: ParsedType; // onEvent 콜백 데이터 타입 (void면 undefined)
  // 콜백 기반 API 지원 (loadFullScreenAd, showFullScreenAd 등)
  isCallbackBased?: boolean; // 콜백 기반 API인지 (onEvent/onError 콜백 사용)
  isTopLevelExport?: boolean; // 최상위 export인지 (AppsInToss 네임스페이스 없이 호출)
}

/**
 * 파라미터 정보
 */
export interface ParsedParameter {
  name: string;
  type: ParsedType;
  optional: boolean;
  description?: string;
}

/**
 * 타입 정보
 */
export interface ParsedType {
  name: string;
  kind: 'primitive' | 'object' | 'array' | 'promise' | 'function' | 'union' | 'record' | 'unknown';
  properties?: ParsedProperty[];
  elementType?: ParsedType;
  promiseType?: ParsedType;
  unionTypes?: ParsedType[];
  raw: string;
  // Discriminated Union 정보
  isDiscriminatedUnion?: boolean;
  successType?: ParsedType; // Union에서 객체 타입
  errorCodes?: string[]; // Union에서 문자열 리터럴들
  // Record 타입 정보
  keyType?: ParsedType; // Record<K, V>의 K
  valueType?: ParsedType; // Record<K, V>의 V
  // Function 타입 정보
  functionParams?: ParsedType[]; // 함수 파라미터 타입들
  functionReturnType?: ParsedType; // 함수 반환 타입
  // Intersection 타입 정보
  isIntersection?: boolean;
  // Nullable 타입 정보 (T | null 또는 T | undefined 패턴)
  isNullable?: boolean;
}

/**
 * 객체 프로퍼티 정보
 */
export interface ParsedProperty {
  name: string;
  type: ParsedType;
  optional: boolean;
  description?: string;
  // Inline string literal union을 enum으로 변환할 때 사용
  inlineEnumName?: string;    // 생성될 enum 이름 (예: SetDeviceOrientationType)
  inlineEnumValues?: string[]; // enum 값들 (예: ["portrait", "landscape"])
}

/**
 * Enum 멤버 값 (문자열 또는 숫자 값을 가진 객체)
 */
export type EnumValue = string | { name: string; value: number };

/**
 * 파싱된 타입 정의 (enum, interface)
 */
export interface ParsedTypeDefinition {
  name: string;
  kind: 'enum' | 'interface';
  file: string;
  description?: string;
  // enum인 경우 (문자열 enum 또는 숫자 enum)
  enumValues?: EnumValue[];
  // interface인 경우
  properties?: ParsedProperty[];
}

/**
 * 생성된 코드 정보
 */
export interface GeneratedCode {
  api: ParsedAPI;
  csharp: string;
  jslib: string;
}

/**
 * 검증 결과
 */
export interface ValidationResult {
  success: boolean;
  apiCount?: number;
  errors?: ValidationError[];
}

/**
 * 검증 에러
 */
export interface ValidationError {
  api?: string;
  type: 'missing' | 'type-unsupported' | 'syntax-error';
  message: string;
  suggestion?: string;
}
