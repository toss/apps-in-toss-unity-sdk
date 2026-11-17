/**
 * 카테고리별로 분할 출력하기 위한 단위 C# 타입.
 * generateTypes / generateTypeDefinitions가 타입 1개당 1개씩 반환하면
 * 호출자(index.ts)가 category로 버킷팅해 AIT.Types.{Category}.cs로 분할 기록한다.
 */
export interface GeneratedTypeUnit {
  /** 타입(클래스/enum) 이름 */
  name: string;
  /** 헤더/네임스페이스 없는 C# 본문 (alias 적용 완료) */
  code: string;
  /** 소속 카테고리 (AIT.Types.{category}.cs 파일로 모임) */
  category: string;
}

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
  // 네임스페이스 콜백 기반 API (GoogleAdMob.loadAppsInTossAdMob 패턴)
  isNamespaceCallbackBased?: boolean; // 네임스페이스 내 콜백 기반 API인지
  callbackEventType?: string; // 콜백 이벤트 타입 (예: LoadAdMobEvent)
  // 중첩 콜백 지원 (options.processProductGrant 등)
  nestedCallbacks?: NestedCallback[];
  // 호출에 넘기는 options.callbacks 묶음 (TossAds.initialize/attach/attachBanner)
  callbackBag?: CallbackBag;
  /** 반환 타입에 ArrayBuffer/Uint8Array가 들어 있어 jslib가 base64로 직렬화해야 하는 API */
  returnsBinary?: boolean;
  /**
   * window.AppsInToss 기준 호출 경로 (예: 'Device.getContacts.getPermission').
   * 없으면 namespace/originalName으로 유도한다 (getApiCallPath 참고).
   * 함수의 하위 멤버(isSupported/getPermission/openPermissionDialog)처럼 이름 규약만으로
   * 경로를 만들 수 없는 API가 지정한다.
   */
  callPath?: string;
  /** 호출이 아니라 프로퍼티 읽기(Device.locale 등)로 값을 얻는 API */
  isProperty?: boolean;
  /**
   * addEventListener가 아니라 `X({ onEvent, onError })`를 직접 호출하는 구독 API
   * (SafeArea.subscribe, Game.subscribeInfoOverlayHidden). isEventSubscription과 함께 쓴다.
   */
  isDirectSubscribe?: boolean;
  /** 직접 구독 API의 인자 객체가 onError를 받는지 (SafeArea.subscribe는 받지 않는다) */
  directSubscribeHasOnError?: boolean;
  /** 다른 API의 하위 멤버(isSupported 등)일 때 기반 API의 C# 이름 */
  baseApiName?: string;
}

/**
 * window.AppsInToss 기준 API 호출 경로를 반환한다.
 * - callPath가 있으면 그대로
 * - 네임스페이스 API: `<Namespace>.<member>`
 * - 최상위 API: `<name>`
 */
export function getApiCallPath(api: Pick<ParsedAPI, 'callPath' | 'namespace' | 'originalName'>): string {
  if (api.callPath) return api.callPath;
  return api.namespace ? `${api.namespace}.${api.originalName}` : api.originalName;
}

/**
 * options 안의 `callbacks: { onX?: (payload) => void, ... }` 묶음.
 * 응답을 기다리는 콜백(NestedCallback)과 달리 JS가 C#으로 알리기만 하는 이벤트이고,
 * 호출이 끝난 뒤에도 계속 올 수 있다. C# 쪽 `Callbacks` 클래스의 Action 필드가 받는다.
 */
export interface CallbackBag {
  /** 묶음을 가진 파라미터 이름 (예: options) */
  paramName: string;
  /** 파라미터 안의 프로퍼티 이름 (예: callbacks) */
  property: string;
  members: CallbackBagMember[];
  /** 호출 결과가 `{ destroy }` 핸들이면 그 프로퍼티 이름. 없으면 undefined */
  handleProperty?: string;
}

export interface CallbackBagMember {
  /** JS 콜백 이름 (예: onAdRendered) */
  name: string;
  /** C# 필드 이름 (예: OnAdRendered) */
  pascalName: string;
  /** 인자를 받는지 (Action<T> vs Action) */
  hasPayload: boolean;
}

/**
 * 중첩 콜백 정보 (options 객체 내부의 콜백 함수)
 * 예: createOneTimePurchaseOrder의 options.processProductGrant
 */
export interface NestedCallback {
  name: string;           // 콜백 이름 (예: "processProductGrant")
  path: string[];         // 경로 (예: ["options", "processProductGrant"])
  parameterType?: ParsedType; // 콜백 파라미터 타입 (예: { orderId: string })
  returnType?: ParsedType;    // 콜백 반환 타입 (예: boolean | Promise<boolean>)
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
  kind: 'primitive' | 'object' | 'array' | 'promise' | 'function' | 'union' | 'record' | 'unknown' | 'dom-only';
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
