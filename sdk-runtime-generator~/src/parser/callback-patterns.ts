import { ParsedParameter, ParsedType, NestedCallback } from '../types.js';

/**
 * 콜백 기반 API 패턴 감지
 *
 * 조건:
 * 1. 파라미터가 1개 (args 객체)
 * 2. args 객체에 onEvent, onError 함수 프로퍼티 존재
 * 3. 반환 타입이 () => void (cleanup/dispose 함수)
 *
 * 예: GoogleAdMob.loadAppsInTossAdMob, loadFullScreenAd 등
 */
export function detectCallbackBasedPattern(parameters: ParsedParameter[], returnType: ParsedType): boolean {
  if (parameters.length !== 1) return false;

  const argsType = parameters[0].type;
  if (argsType.kind !== 'object' || !argsType.properties) return false;

  const hasOnEvent = argsType.properties.some(p => p.name === 'onEvent' && p.type.kind === 'function');
  const hasOnError = argsType.properties.some(p => p.name === 'onError' && p.type.kind === 'function');
  if (!hasOnEvent || !hasOnError) return false;

  return (
    returnType.kind === 'function' &&
    (returnType.raw?.includes('() => void') || returnType.functionReturnType?.name === 'void')
  );
}

/**
 * 파라미터에서 중첩 콜백 감지
 *
 * 중첩 콜백 렌더링(RegisterNestedCallback + `options.<콜백>` 접근 + `<래퍼타입명>Options`
 * 파라미터 타입 유도)이 지원하는 형태는 "단일 파라미터 + 파라미터 타입에 'options' 프로퍼티 +
 * 콜백이 options 직속"뿐이다. (예: IAP.createOneTimePurchaseOrder({ options: { ..., processProductGrant } }))
 * 그 외 형태(다중 파라미터인 TossAds.attach, 'callbacks' 아래 콜백인 TossAds.initialize)는
 * 존재하지 않는 타입을 참조하는 C#이 생성되므로 감지 대상에서 제외하고 일반(async) 경로로 생성한다.
 */
export function detectNestedCallbacks(parameters: ParsedParameter[]): NestedCallback[] {
  if (parameters.length !== 1) return [];
  const wrapperType = parameters[0].type;
  if (wrapperType.kind !== 'object' || !wrapperType.properties?.some(p => p.name === 'options')) {
    return [];
  }
  const nestedCallbacks: NestedCallback[] = [];
  const seenCallbacks = new Set<string>();

  // 템플릿은 `options.<콜백>` 접근만 렌더하므로 options "직속" 함수 프로퍼티만 수집한다.
  const collectDirectCallbacks = (type: ParsedType, path: string[]): void => {
    if (type.kind === 'object' && type.properties) {
      for (const prop of type.properties) {
        if (prop.type.kind === 'function') {
          const callbackKey = [...path, prop.name].join('.');
          if (!seenCallbacks.has(callbackKey)) {
            seenCallbacks.add(callbackKey);
            nestedCallbacks.push({
              name: prop.name,
              path: [...path, prop.name],
              parameterType: prop.type.functionParams?.[0],
              returnType: prop.type.functionReturnType,
            });
          }
        }
      }
    }
    // 인터섹션이 union으로 파싱된 경우(Sku & { processProductGrant })도 같은 레벨로 취급
    if (type.kind === 'union' && type.unionTypes) {
      for (const unionMember of type.unionTypes) {
        collectDirectCallbacks(unionMember, path);
      }
    }
  };

  const optionsProp = wrapperType.properties.find(p => p.name === 'options');
  if (optionsProp && (optionsProp.type.kind === 'object' || optionsProp.type.kind === 'union')) {
    collectDirectCallbacks(optionsProp.type, ['options']);
  }

  return nestedCallbacks;
}
