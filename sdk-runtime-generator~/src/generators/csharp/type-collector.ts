import { ParsedType } from '../../types.js';
import { mapToCSharpType } from '../../validators/types.js';
import { extractCleanName, capitalize, xmlSafe } from './utils.js';

/**
 * 인라인 타입 추적을 위한 상태 클래스
 */
export class InlineTypeTracker {
  // Inline string literal union을 enum으로 변환할 때 수집
  // key: enum 이름 (예: SetDeviceOrientationOptionsType)
  // value: enum 값들 (예: ["portrait", "landscape"])
  readonly inlineEnums: Map<string, string[]> = new Map();

  // 익명 배열 요소 타입을 추적 (propertyPath -> generatedClassName)
  readonly inlineArrayElementTypes: Map<string, string> = new Map();

  // 익명/intersection 객체 타입을 추적 (propertyPath -> generatedClassName)
  readonly inlineObjectTypes: Map<string, string> = new Map();

  // 프로퍼티가 없지만 참조된 외부 타입을 추적 (나중에 type definitions에서 해결)
  readonly pendingExternalTypes: Set<string> = new Set();

  /**
   * 모든 추적 상태 초기화
   */
  clear(): void {
    this.inlineEnums.clear();
    this.inlineArrayElementTypes.clear();
    this.inlineObjectTypes.clear();
    this.pendingExternalTypes.clear();
  }
}

/**
 * inline string literal union인지 확인
 * Case 1: kind가 'union'이고 모든 unionTypes가 string literal
 * Case 2: kind가 'primitive'이지만 raw가 "value1" | "value2" 형태
 */
export function isInlineStringLiteralUnion(type: ParsedType): boolean {
  // Case 1: 파서가 union으로 인식한 경우
  if (type.kind === 'union' && type.unionTypes && type.unionTypes.length > 0) {
    return type.unionTypes.every(
      t => t.kind === 'primitive' && t.name === 'string' && t.raw.startsWith('"')
    );
  }

  // Case 2: 파서가 primitive로 인식했지만 raw가 string literal union인 경우
  // 예: { kind: 'primitive', name: 'string', raw: '"portrait" | "landscape"' }
  if (type.kind === 'primitive' && type.name === 'string' && type.raw) {
    // raw가 "..." | "..." 형태인지 확인
    const unionPattern = /^"[^"]*"(\s*\|\s*"[^"]*")+$/;
    return unionPattern.test(type.raw.trim());
  }

  return false;
}

/**
 * inline string literal union에서 enum 값들 추출
 */
export function extractEnumValues(type: ParsedType): string[] {
  // Case 1: unionTypes가 있는 경우
  if (type.unionTypes && type.unionTypes.length > 0) {
    return type.unionTypes
      .filter(t => t.raw.startsWith('"'))
      .map(t => t.raw.replace(/"/g, ''));
  }

  // Case 2: raw에서 직접 추출 (예: '"portrait" | "landscape"')
  if (type.raw) {
    const matches = type.raw.match(/"([^"]*)"/g);
    if (matches) {
      return matches.map(m => m.replace(/"/g, ''));
    }
  }

  return [];
}

/**
 * 필드 선언을 생성 (JsonProperty 어트리뷰트 포함)
 * C# 필드명은 PascalCase, JSON 직렬화는 원본 camelCase 사용
 * System.Action, System.Func 타입은 직렬화할 수 없으므로 JsonIgnore 추가
 */
export function generateFieldDeclaration(originalName: string, type: string, optional: boolean = false): string {
  const pascalName = capitalize(originalName);
  const optionalComment = optional ? ' // optional' : '';

  // System.Action, System.Func 타입은 직렬화 불가능하므로 JsonIgnore 사용
  if (type.startsWith('System.Action') || type.startsWith('System.Func')) {
    return `        [JsonIgnore]\n        public ${type} ${pascalName};${optionalComment}`;
  }

  // 원본 이름과 PascalCase가 다른 경우에만 JsonProperty 추가
  // [Preserve]는 IL2CPP 코드 스트리핑 방지
  if (originalName !== pascalName) {
    return `        [Preserve]\n        [JsonProperty("${originalName}")]\n        public ${type} ${pascalName};${optionalComment}`;
  }
  return `        [Preserve]\n        public ${type} ${pascalName};${optionalComment}`;
}

/**
 * 중첩 클래스 타입 생성 (내부 헬퍼)
 */
export function generateNestedClassType(
  name: string,
  properties: any[],
  nestedTypes: Map<string, string>
): string {
  const fields = properties
    .map(prop => {
      let type = mapToCSharpType(prop.type);
      // 중첩 익명 객체는 생성된 클래스 이름 사용
      if (prop.type.kind === 'object' &&
          prop.type.properties &&
          prop.type.properties.length > 0 &&
          (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
        type = `${name}${capitalize(prop.name)}`;
      }
      // Union 타입의 멤버가 intersection/익명 객체인 경우
      else if (prop.type.kind === 'union' && prop.type.unionTypes && prop.type.unionTypes.length > 0) {
        const membersWithProps = prop.type.unionTypes.filter(
          (t: any) => (t.kind === 'object' || t.isIntersection) && t.properties && t.properties.length > 0
        );
        if (membersWithProps.length > 0) {
          type = `${name}${capitalize(prop.name)}`;
        }
      }
      const description = prop.description
        ? `        /// <summary>${xmlSafe(prop.description)}</summary>\n`
        : '';
      return `${description}${generateFieldDeclaration(prop.name, type, prop.optional)}`;
    })
    .join('\n');

  return `    [Serializable]
    [Preserve]
    public class ${name}
    {
${fields}
    }`;
}

/**
 * 중첩 타입을 수집하여 반환
 */
export function collectNestedTypes(
  parentName: string,
  properties: any[],
  nestedTypes: Map<string, string>
): void {
  for (const prop of properties) {
    // 중첩 익명 객체 타입 처리
    if (prop.type.kind === 'object' &&
        prop.type.properties &&
        prop.type.properties.length > 0 &&
        (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
      const nestedTypeName = `${parentName}${capitalize(prop.name)}`;
      if (!nestedTypes.has(nestedTypeName)) {
        // 재귀적으로 중첩 타입 수집
        collectNestedTypes(nestedTypeName, prop.type.properties, nestedTypes);
        // 중첩 클래스 생성
        nestedTypes.set(nestedTypeName, generateNestedClassType(nestedTypeName, prop.type.properties, nestedTypes));
      }
    }
    // Note: Union 타입의 intersection 멤버는 collectReferencedTypes에서 처리됨
    // 여기서 중복 생성하면 안됨
  }
}

/**
 * 프로퍼티에서 참조되는 named type을 재귀적으로 수집
 */
export function collectReferencedTypes(
  properties: any[],
  typeMap: Map<string, string>,
  exclude: Set<string>,
  tracker: InlineTypeTracker,
  generateClassType: (name: string, properties: any[], isResultType?: boolean) => string,
  parentTypeName?: string
): void {
  for (const prop of properties) {
    // named object 타입 (익명이 아닌 경우)
    if (prop.type.kind === 'object' &&
        prop.type.name !== '__type' &&
        prop.type.name !== 'object' &&
        !prop.type.name.startsWith('{')) {
      const typeName = extractCleanName(prop.type.name);
      if (!typeMap.has(typeName) && !exclude.has(typeName)) {
        // 프로퍼티가 있으면 바로 클래스 생성
        if (prop.type.properties && prop.type.properties.length > 0) {
          typeMap.set(typeName, generateClassType(typeName, prop.type.properties));
          // 재귀적으로 해당 타입의 프로퍼티도 수집
          collectReferencedTypes(prop.type.properties, typeMap, exclude, tracker, generateClassType, typeName);
        } else {
          // 프로퍼티가 없는 named type은 pendingExternalTypes에 추가
          // (나중에 type definitions에서 해결)
          tracker.pendingExternalTypes.add(typeName);
        }
      }
    }
    // unknown 타입: import("...").TypeName 형식의 외부 타입
    // 예: import("...").LoadAdMobInterstitialAdOptions
    else if (prop.type.kind === 'unknown' && prop.type.name && prop.type.name.includes('.')) {
      const typeName = extractCleanName(prop.type.name);
      if (typeName && typeName !== '__type' && typeName !== 'undefined' &&
          !typeMap.has(typeName) && !exclude.has(typeName)) {
        tracker.pendingExternalTypes.add(typeName);
      }
    }
    // Intersection 타입 또는 익명 객체 타입이지만 프로퍼티가 있는 경우
    // 부모 컨텍스트에서 타입 이름 생성 (예: IapCreateOneTimePurchaseOrderOptionsOptions)
    else if (prop.type.kind === 'object' &&
             (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{')) &&
             prop.type.properties && prop.type.properties.length > 0) {
      // 부모 타입 이름 + 프로퍼티 이름으로 타입 이름 생성
      const typeName = parentTypeName
        ? `${parentTypeName}${capitalize(prop.name)}`
        : `${capitalize(prop.name)}Type`;

      if (!typeMap.has(typeName) && !exclude.has(typeName)) {
        typeMap.set(typeName, generateClassType(typeName, prop.type.properties));
        // 재귀적으로 해당 타입의 프로퍼티도 수집
        collectReferencedTypes(prop.type.properties, typeMap, exclude, tracker, generateClassType, typeName);
      }

      // 익명 타입 매핑 저장 (나중에 타입 변환 시 사용)
      const propPath = parentTypeName ? `${parentTypeName}.${prop.name}` : prop.name;
      tracker.inlineObjectTypes.set(propPath, typeName);
    }
    // 배열의 요소 타입
    else if (prop.type.kind === 'array' && prop.type.elementType) {
      const elementType = prop.type.elementType;
      const isAnonymousObject = elementType.kind === 'object' &&
          elementType.properties &&
          elementType.properties.length > 0;

      if (isAnonymousObject) {
        const isNamedType = elementType.name !== '__type' &&
            elementType.name !== 'object' &&
            !elementType.name.startsWith('{');

        let typeName: string;
        if (isNamedType) {
          typeName = extractCleanName(elementType.name);
        } else {
          // 익명 객체 배열 요소 타입: 부모 컨텍스트에서 이름 생성
          // 예: IAPGetPendingOrdersResult의 orders 필드 -> IAPGetPendingOrdersResultOrder
          const propNameSingular = prop.name.endsWith('s') ? prop.name.slice(0, -1) : prop.name;
          typeName = parentTypeName
            ? `${parentTypeName}${capitalize(propNameSingular)}`
            : `${capitalize(propNameSingular)}Item`;

          // 익명 타입 매핑 저장 (나중에 타입 변환 시 사용)
          const propPath = parentTypeName ? `${parentTypeName}.${prop.name}` : prop.name;
          tracker.inlineArrayElementTypes.set(propPath, typeName);
        }

        if (!typeMap.has(typeName) && !exclude.has(typeName)) {
          typeMap.set(typeName, generateClassType(typeName, elementType.properties));
          // 재귀적으로 해당 타입의 프로퍼티도 수집
          collectReferencedTypes(elementType.properties, typeMap, exclude, tracker, generateClassType, typeName);
        }
      }
    }
    // Union 타입의 멤버가 intersection/익명 객체인 경우
    // 모든 멤버의 프로퍼티를 병합하여 단일 클래스 생성
    else if (prop.type.kind === 'union' && prop.type.unionTypes && prop.type.unionTypes.length > 0) {
      const membersWithProps = prop.type.unionTypes.filter(
        (t: any) => (t.kind === 'object' || t.isIntersection) && t.properties && t.properties.length > 0
      );
      if (membersWithProps.length > 0) {
        const typeName = parentTypeName
          ? `${parentTypeName}${capitalize(prop.name)}`
          : `${capitalize(prop.name)}Type`;

        if (!typeMap.has(typeName) && !exclude.has(typeName)) {
          // 모든 멤버의 프로퍼티 병합 (중복 제거)
          const mergedProps = new Map<string, any>();
          for (const member of membersWithProps) {
            for (const memberProp of member.properties) {
              if (!mergedProps.has(memberProp.name)) {
                mergedProps.set(memberProp.name, memberProp);
              }
            }
          }
          const mergedPropsArray = Array.from(mergedProps.values());
          typeMap.set(typeName, generateClassType(typeName, mergedPropsArray));
          // 재귀적으로 해당 타입의 프로퍼티도 수집
          collectReferencedTypes(mergedPropsArray, typeMap, exclude, tracker, generateClassType, typeName);
        }

        // 익명 타입 매핑 저장 (나중에 타입 변환 시 사용)
        const propPath = parentTypeName ? `${parentTypeName}.${prop.name}` : prop.name;
        tracker.inlineObjectTypes.set(propPath, typeName);
      }
    }
    // 함수 타입의 파라미터
    else if (prop.type.kind === 'function' && prop.type.functionParams) {
      for (const funcParam of prop.type.functionParams) {
        collectFunctionParamTypes(funcParam, typeMap, exclude, tracker, generateClassType);
      }
    }
  }
}

/**
 * 함수 파라미터 타입을 재귀적으로 수집
 */
export function collectFunctionParamTypes(
  paramType: ParsedType,
  typeMap: Map<string, string>,
  exclude: Set<string>,
  tracker: InlineTypeTracker,
  generateClassType: (name: string, properties: any[], isResultType?: boolean) => string
): void {
  // Union 타입 처리 (ContactsViralEvent = RewardFromContactsViralEvent | ContactsViralSuccessEvent)
  if (paramType.kind === 'union') {
    // Union 타입 자체가 named type이면 클래스로 생성
    if (paramType.name && paramType.name.includes('.')) {
      const unionTypeName = extractCleanName(paramType.name);
      if (!typeMap.has(unionTypeName) && !exclude.has(unionTypeName)) {
        // Union의 모든 멤버 속성을 수집
        const allProperties = new Map<string, any>();
        if (paramType.unionTypes) {
          for (const member of paramType.unionTypes) {
            if (member.kind === 'object' && member.properties) {
              for (const prop of member.properties) {
                if (!allProperties.has(prop.name)) {
                  allProperties.set(prop.name, prop);
                }
              }
            }
          }
        }
        if (allProperties.size > 0) {
          const mergedPropsArray = Array.from(allProperties.values());
          typeMap.set(unionTypeName, generateClassType(unionTypeName, mergedPropsArray));
          // 중첩 타입도 수집 (예: ContactsViralEventData)
          collectReferencedTypes(mergedPropsArray, typeMap, exclude, tracker, generateClassType, unionTypeName);
        }
      }
    }
    // Union 멤버들도 재귀 처리
    if (paramType.unionTypes) {
      for (const member of paramType.unionTypes) {
        collectFunctionParamTypes(member, typeMap, exclude, tracker, generateClassType);
      }
    }
  }
  // Object 타입 처리
  else if (paramType.kind === 'object') {
    // Named type (not anonymous)
    let isAnonymous = paramType.name === '__type' || paramType.name === 'object' || paramType.name.startsWith('{');

    // __type이지만 raw에서 실제 타입 이름을 추출할 수 있는 경우
    let extractedTypeName: string | undefined;
    if (isAnonymous && paramType.raw && paramType.raw.includes('.') && !paramType.raw.trim().startsWith('{')) {
      const rawTypeName = paramType.raw.split('.').pop()?.replace(/["'{}(),;\s<>|]/g, '').replace(/\$\d+$/, '').trim();
      if (rawTypeName && rawTypeName !== '__type' && !rawTypeName.startsWith('{')) {
        extractedTypeName = rawTypeName;
        isAnonymous = false; // raw에서 타입 이름을 추출했으므로 익명이 아님
      }
    }

    if (!isAnonymous) {
      const typeName = extractedTypeName || extractCleanName(paramType.name);

      if (!typeMap.has(typeName) && !exclude.has(typeName)) {
        if (paramType.properties && paramType.properties.length > 0) {
          // 인라인 프로퍼티가 있으면 클래스 생성
          typeMap.set(typeName, generateClassType(typeName, paramType.properties));
          collectReferencedTypes(paramType.properties, typeMap, exclude, tracker, generateClassType, typeName);
        } else {
          // 프로퍼티가 없는 named type은 pendingExternalTypes에 추가
          tracker.pendingExternalTypes.add(typeName);
        }
      }
    }
    // Anonymous type - still collect referenced types if it has properties
    else if (paramType.properties && paramType.properties.length > 0) {
      collectReferencedTypes(paramType.properties, typeMap, exclude, tracker, generateClassType, undefined);
    }
  }
}

/**
 * 타입 정의에서 중첩 타입을 수집 (generateTypeDefinitions 전용)
 * collectNestedTypes와 유사하지만 exclude/generatedTypeNames 체크 포함
 */
export function collectNestedTypesForTypeDefinition(
  parentName: string,
  properties: any[],
  nestedTypes: Map<string, string>,
  exclude: Set<string>,
  generatedTypeNames: Set<string>
): void {
  for (const prop of properties) {
    // 중첩 익명 객체 타입 처리
    if (prop.type.kind === 'object' &&
        prop.type.properties &&
        prop.type.properties.length > 0 &&
        (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
      const nestedTypeName = `${parentName}${capitalize(prop.name)}`;
      if (!nestedTypes.has(nestedTypeName) && !exclude.has(nestedTypeName) && !generatedTypeNames.has(nestedTypeName)) {
        // 재귀적으로 중첩 타입 수집
        collectNestedTypesForTypeDefinition(nestedTypeName, prop.type.properties, nestedTypes, exclude, generatedTypeNames);
        // 중첩 클래스 생성
        nestedTypes.set(nestedTypeName, generateNestedClassType(nestedTypeName, prop.type.properties, nestedTypes));
      }
    }
  }
}
