import { ParsedAPI } from '../../types.js';
import { mapToCSharpType } from '../../validators/types.js';
import { escapeCSharpKeyword, toPascalCase, xmlSafe } from './utils.js';
import { PRIMITIVE_TYPES } from './constants.js';

/**
 * 준비된 파라미터 데이터
 */
export interface PreparedParameter {
  paramName: string;
  paramType: string;
  optional: boolean;
  description?: string;
  isPrimitive: boolean;
}

/**
 * 준비된 API 데이터 (템플릿용)
 */
export interface PreparedApiData {
  name: string;
  pascalName: string;
  originalName: string;
  description?: string;
  returnDescription?: string;
  examples?: string[];
  parameters: PreparedParameter[];
  returnType: string;
  isAsync: boolean;
  callbackType: string;
  isNullableReturn: boolean;
  namespace?: string;
  isDeprecated?: boolean;
  deprecatedMessage?: string;
  isEventSubscription?: boolean;
  eventName?: string;
  hasEventData: boolean;
  eventDataType?: string;
  isCallbackBased?: boolean;
  isNamespaceCallbackBased?: boolean;
  callbackEventType?: string;
  callbackErrorType?: string;
  hasNestedCallbacks?: boolean;
  nestedCallbacks?: Array<{
    name: string;
    pascalName: string;
    path: string[];
  }>;
  nestedCallbackParamType?: string;
  nestedCallbackOptionsType?: string;
  nestedCallbackEventType?: string;
}

/**
 * 파라미터 변환
 */
export function prepareParameters(api: ParsedAPI): PreparedParameter[] {
  // 파라미터 변환 (void 타입 파라미터는 제외)
  const parameters = api.parameters
    .filter(param => {
      const paramType = mapToCSharpType(param.type);
      // C#에서 void는 파라미터 타입으로 사용할 수 없음
      return paramType !== 'void';
    })
    .map(param => {
      let paramType = mapToCSharpType(param.type);

      // 파라미터가 익명 객체(__type, object)인 경우 의미있는 이름 생성
      if ((paramType === '__type' || paramType === 'object') && param.type.kind === 'object' && param.type.properties && param.type.properties.length > 0) {
        const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
        const apiName = capitalize(api.name);
        const paramName = capitalize(param.name);
        paramType = `${apiName}${paramName}`;
      }

      // WebGL DllImport에서 직접 전달 가능한 primitive 타입인지 확인
      const isPrimitive = PRIMITIVE_TYPES.includes(paramType);

      return {
        paramName: escapeCSharpKeyword(param.name),
        paramType,
        optional: param.optional,
        description: param.description,
        isPrimitive,
      };
    });

  // C# 규칙: optional 파라미터는 필수 파라미터 뒤에 와야 함
  parameters.sort((a, b) => {
    if (a.optional === b.optional) return 0;
    return a.optional ? 1 : -1;
  });

  return parameters;
}

/**
 * 콜백 타입 결정
 */
export function determineCallbackType(api: ParsedAPI): string {
  let callbackType: string;
  if (api.returnType.kind === 'promise' && api.returnType.promiseType) {
    callbackType = mapToCSharpType(api.returnType.promiseType);
  } else {
    callbackType = mapToCSharpType(api.returnType);
  }

  // Discriminated Union인 경우 Result 클래스 사용
  if (api.returnType.kind === 'promise' &&
      api.returnType.promiseType?.isDiscriminatedUnion) {
    callbackType = `${api.pascalName}Result`;
  }
  // 익명 객체 타입(__type, object)이면 더 구체적인 이름 생성
  else if (callbackType === '__type' || callbackType === 'object') {
    // 동기/비동기 함수 모두 처리: promiseType이 있으면 사용, 없으면 returnType 직접 사용
    const targetType = api.returnType.kind === 'promise'
      ? api.returnType.promiseType
      : api.returnType;

    if (targetType && targetType.kind === 'object' &&
        targetType.properties && targetType.properties.length > 0) {
      callbackType = `${api.pascalName}Result`;
    } else if (!targetType || targetType.name === 'void' || targetType.name === 'undefined') {
      callbackType = 'void';
    } else {
      // 이름이 있는 타입이지만 properties가 빈 경우 (예: interface 참조)
      // -> Result 타입으로 처리
      callbackType = `${api.pascalName}Result`;
    }
  }

  return callbackType;
}

/**
 * 콜백 기반 API의 파라미터 추출
 */
export function extractCallbackApiParameters(
  api: ParsedAPI,
  parameters: PreparedParameter[]
): PreparedParameter[] {
  if (!api.isCallbackBased) {
    return parameters;
  }

  // args 객체 패턴 감지: 파라미터가 1개이고, 그 타입이 object이며, options 프로퍼티가 있는 경우
  const hasArgsObjectPattern = api.parameters.length === 1 &&
    api.parameters[0].type.kind === 'object' &&
    api.parameters[0].type.properties?.some(p => p.name === 'options');

  if (hasArgsObjectPattern) {
    // args 객체 패턴: args 객체 내의 options 프로퍼티 추출
    const argsType = api.parameters[0].type;
    if (argsType.kind === 'object' && argsType.properties) {
      const optionsProp = argsType.properties.find(p => p.name === 'options');
      if (optionsProp) {
        let optionsTypeName: string;

        // 익명 객체 타입인지 확인
        const isAnonymousObject = optionsProp.type.kind === 'object' &&
          (optionsProp.type.name === '__type' || optionsProp.type.name === 'object' ||
           optionsProp.type.name.startsWith('{'));

        if (isAnonymousObject && argsType.name && argsType.name !== '__type') {
          // 부모 타입명 + 'Options' 패턴으로 타입명 생성
          // 예: ContactsViralParams + options -> ContactsViralParamsOptions
          // 타입명 정리: import("...").TypeName -> TypeName, 특수문자 제거
          let parentTypeName = argsType.name;
          if (parentTypeName.includes('.')) {
            parentTypeName = parentTypeName.split('.').pop() || parentTypeName;
          }
          parentTypeName = parentTypeName.replace(/["'{}(),|$<>]/g, '').trim();
          optionsTypeName = `${parentTypeName}Options`;
        } else {
          // named type이면 mapToCSharpType 사용
          optionsTypeName = mapToCSharpType(optionsProp.type);
        }

        return [{
          paramName: 'options',
          paramType: optionsTypeName,
          optional: optionsProp.optional ?? true,
          description: undefined,
          isPrimitive: false,
        }];
      } else {
        return [];
      }
    }
  } else {
    // 직접 파라미터 패턴: onEvent, onError 제외
    return parameters.filter(p => p.paramName !== 'onEvent' && p.paramName !== 'onError');
  }

  return parameters;
}

/**
 * 콜백 기반 API의 이벤트/에러 타입 추출
 */
export function extractCallbackTypes(api: ParsedAPI): {
  callbackEventType?: string;
  callbackErrorType?: string;
} {
  if (!api.isCallbackBased || !api.parameters) {
    return {};
  }

  let callbackEventType: string | undefined;
  let callbackErrorType: string | undefined;

  // 1. 먼저 최상위 파라미터에서 onEvent/onError 찾기 (top-level export 패턴)
  let onEventParam = api.parameters.find(p => p.name === 'onEvent');
  let onErrorParam = api.parameters.find(p => p.name === 'onError');

  // 2. 없으면 첫 번째 파라미터(args 객체)의 프로퍼티에서 찾기 (namespace method 패턴)
  if (!onEventParam && api.parameters.length === 1 && api.parameters[0].type.kind === 'object') {
    const argsProperties = api.parameters[0].type.properties || [];
    const onEventProp = argsProperties.find(p => p.name === 'onEvent');
    if (onEventProp && onEventProp.type.kind === 'function') {
      onEventParam = { name: 'onEvent', type: onEventProp.type, optional: false };
    }
    const onErrorProp = argsProperties.find(p => p.name === 'onError');
    if (onErrorProp && onErrorProp.type.kind === 'function') {
      onErrorParam = { name: 'onError', type: onErrorProp.type, optional: false };
    }
  }

  if (onEventParam && onEventParam.type.kind === 'function' && onEventParam.type.functionParams?.[0]) {
    callbackEventType = mapToCSharpType(onEventParam.type.functionParams[0]);
  }
  if (onErrorParam && onErrorParam.type.kind === 'function' && onErrorParam.type.functionParams?.[0]) {
    callbackErrorType = mapToCSharpType(onErrorParam.type.functionParams[0]);
  }

  return { callbackEventType, callbackErrorType };
}

/**
 * 중첩 콜백 API의 파라미터 타입 추출 (래퍼 타입)
 */
export function getNestedCallbackParamType(api: ParsedAPI): string | undefined {
  if (!api.nestedCallbacks || api.nestedCallbacks.length === 0) return undefined;
  // 첫 번째 파라미터 타입 사용
  if (api.parameters.length > 0) {
    return mapToCSharpType(api.parameters[0].type);
  }
  return 'object';
}

/**
 * 중첩 콜백 API의 내부 options 타입 추출
 * 예: IapCreateOneTimePurchaseOrderOptions -> IapCreateOneTimePurchaseOrderOptionsOptions
 */
export function getNestedCallbackOptionsType(api: ParsedAPI): string | undefined {
  if (!api.nestedCallbacks || api.nestedCallbacks.length === 0) return undefined;

  // 첫 번째 파라미터의 options 프로퍼티 타입 사용
  if (api.parameters.length > 0) {
    const paramType = api.parameters[0].type;

    // 1. 래퍼 타입명에서 내부 options 타입명 유도
    // 예: IapCreateOneTimePurchaseOrderOptions -> IapCreateOneTimePurchaseOrderOptionsOptions
    // TypeScript의 복잡한 union/intersection 타입은 mapToCSharpType이 처리하지 못하므로
    // 래퍼 타입명 + "Options" 패턴으로 내부 타입명을 생성
    if (paramType.kind === 'object' && paramType.name) {
      const wrapperTypeName = paramType.name;
      // 래퍼 타입명이 "Options"로 끝나면 "Options"를 추가 (예: XxxOptions -> XxxOptionsOptions)
      // 그렇지 않으면 Options를 추가
      return wrapperTypeName + 'Options';
    }

    // 2. 폴백: mapToCSharpType 사용
    const wrapperType = mapToCSharpType(paramType);
    if (wrapperType && wrapperType !== 'object') {
      return wrapperType + 'Options';
    }
  }
  return 'object';
}

/**
 * 중첩 콜백 API의 이벤트 타입 추출
 */
export function getNestedCallbackEventType(api: ParsedAPI): string | undefined {
  if (!api.nestedCallbacks || api.nestedCallbacks.length === 0) return undefined;
  // 파라미터에서 onEvent 콜백의 타입 추출
  if (api.parameters.length > 0 && api.parameters[0].type.kind === 'object') {
    const onEventProp = api.parameters[0].type.properties?.find(p => p.name === 'onEvent');
    if (onEventProp && onEventProp.type.kind === 'function' && onEventProp.type.functionParams?.[0]) {
      return mapToCSharpType(onEventProp.type.functionParams[0]);
    }
  }
  return 'object';
}

/**
 * 콜백 기반 API에서 이벤트 타입 추출
 * loadFullScreenAd, GoogleAdMob.loadAppsInTossAdMob 등의 패턴에서 onEvent 콜백의 데이터 타입 추출
 */
export function extractCallbackEventType(api: ParsedAPI): string | undefined {
  if (!api.isCallbackBased) return undefined;

  // 1. 직접 파라미터에서 onEvent 찾기 (top-level export 패턴)
  let onEventParam = api.parameters.find(p => p.name === 'onEvent');

  // 2. 없으면 첫 번째 파라미터(args 객체)의 프로퍼티에서 찾기 (namespace method 패턴)
  if (!onEventParam && api.parameters.length === 1 && api.parameters[0].type.kind === 'object') {
    const argsProperties = api.parameters[0].type.properties || [];
    const onEventProp = argsProperties.find(p => p.name === 'onEvent');
    if (onEventProp && onEventProp.type.kind === 'function') {
      onEventParam = { name: 'onEvent', type: onEventProp.type, optional: false };
    }
  }

  if (onEventParam && onEventParam.type.kind === 'function' && onEventParam.type.functionParams?.[0]) {
    return mapToCSharpType(onEventParam.type.functionParams[0]);
  }

  return undefined;
}

/**
 * API 데이터를 템플릿에서 사용할 형식으로 변환
 */
export function prepareApiData(api: ParsedAPI): PreparedApiData {
  const parameters = prepareParameters(api);
  const returnType = mapToCSharpType(api.returnType);
  const callbackType = determineCallbackType(api);

  // deprecated 메시지의 줄바꿈 제거 (C# [Obsolete] 어트리뷰트에서 줄바꿈 불가)
  let deprecatedMessage = api.deprecatedMessage;
  if (deprecatedMessage) {
    deprecatedMessage = deprecatedMessage
      .replace(/\n/g, ' ')  // 줄바꿈을 공백으로
      .replace(/\s+/g, ' ') // 연속 공백을 하나로
      .replace(/"/g, '\\"') // 큰따옴표 이스케이프
      .trim();
  }

  // 이벤트 데이터 타입 결정
  let eventDataType: string | undefined;
  let hasEventData = false;
  if (api.isEventSubscription && api.eventDataType) {
    eventDataType = mapToCSharpType(api.eventDataType);
    hasEventData = eventDataType !== 'void' && eventDataType !== 'undefined';
  }

  // 콜백 기반 API의 이벤트 타입 및 에러 타입 결정
  const { callbackEventType, callbackErrorType } = extractCallbackTypes(api);

  // 콜백 기반 API의 options 파라미터만 추출
  const callbackApiParameters = extractCallbackApiParameters(api, parameters);

  // nullable 참조 타입 여부 확인 (C#에서 ?를 붙일 수 없지만 문서에 명시 필요)
  const innerType = api.returnType.kind === 'promise' ? api.returnType.promiseType : api.returnType;
  const isNullableReturn = innerType?.isNullable === true && callbackType !== 'void';

  // args 객체 패턴 콜백 기반 API 여부 (네임스페이스 메서드 또는 단일 파라미터 top-level export)
  // 파라미터가 1개이고, 그 타입이 object이며, options 프로퍼티가 있는 경우
  const hasArgsObjectPattern = api.parameters.length === 1 &&
    api.parameters[0].type.kind === 'object' &&
    api.parameters[0].type.properties?.some(p => p.name === 'options');
  const isNamespaceCallbackBased = api.isCallbackBased && hasArgsObjectPattern;

  return {
    name: api.name,
    pascalName: api.pascalName,
    originalName: api.originalName,
    description: api.description,
    returnDescription: api.returnDescription,
    examples: api.examples,
    parameters: api.isCallbackBased ? callbackApiParameters : parameters,
    returnType,
    isAsync: api.isAsync,
    callbackType,
    isNullableReturn,
    // 네임스페이스 API 지원
    namespace: api.namespace,
    isDeprecated: api.isDeprecated,
    deprecatedMessage,
    // 이벤트 구독 API 지원
    isEventSubscription: api.isEventSubscription,
    eventName: api.eventName,
    hasEventData,
    eventDataType,
    // 콜백 기반 API 지원
    isCallbackBased: api.isCallbackBased,
    isNamespaceCallbackBased, // 네임스페이스 메서드의 콜백 기반 API (args 객체 패턴)
    callbackEventType,
    callbackErrorType,
    // 중첩 콜백 API 지원
    hasNestedCallbacks: api.nestedCallbacks && api.nestedCallbacks.length > 0,
    nestedCallbacks: api.nestedCallbacks?.map(nc => ({
      name: nc.name,
      pascalName: toPascalCase(nc.name),
      path: nc.path,
    })),
    nestedCallbackParamType: getNestedCallbackParamType(api),
    nestedCallbackOptionsType: getNestedCallbackOptionsType(api),
    nestedCallbackEventType: getNestedCallbackEventType(api),
  };
}
