import { ParsedAPI, ParsedType, ParsedTypeDefinition, GeneratedTypeUnit } from '../../types.js';
import { resolveApiCategory, DEFAULT_CATEGORY } from '../../categories.js';
import { mapToCSharpType } from '../../validators/types.js';
import { loadUnionResultTemplate } from './templates.js';
import { extractCleanName, capitalize, xmlSafe, functionParamTypeName } from './utils.js';
import {
  InlineTypeTracker,
  JSON_EXTENSION_DATA_FIELD,
  isInlineStringLiteralUnion,
  extractEnumValues,
  generateFieldDeclaration,
  generateNestedClassType,
  collectNestedTypes,
  collectReferencedTypes,
  collectFunctionParamTypes,
  collectNestedTypesForTypeDefinition,
  isInlineAnonymousObjectParam,
} from './type-collector.js';
import { computeEnumAliases, applyEnumAliases } from './enum-dedup.js';

/**
 * C# 타입 정의 생성기
 */
export class CSharpTypeGenerator {
  private unionResultTemplate?: HandlebarsTemplateDelegate;

  // 인라인 타입 추적기
  private tracker: InlineTypeTracker = new InlineTypeTracker();

  constructor() {
    // Constructor for future template loading if needed
  }

  /**
   * Union Result 템플릿 로드
   */
  private async loadUnionResultTemplateInternal(): Promise<void> {
    if (this.unionResultTemplate) return;
    this.unionResultTemplate = await loadUnionResultTemplate();
  }

  /**
   * Discriminated Union Result 클래스 생성
   */
  private async generateUnionResult(
    apiName: string,
    successType: ParsedType,
    errorCodes: string[]
  ): Promise<string> {
    await this.loadUnionResultTemplateInternal();

    if (!this.unionResultTemplate) {
      throw new Error('Union Result template not loaded');
    }

    const successTypeName = mapToCSharpType(successType);
    const resultClassName = `${apiName}Result`;

    return this.unionResultTemplate({
      apiName,
      resultClassName,
      successTypeName,
      errorCodes,
      extensionDataField: JSON_EXTENSION_DATA_FIELD,
    });
  }

  /**
   * enum 정의 생성
   * - 숫자 enum: 명시적 값 할당 (Accuracy = 1, 2, 3...)
   * - 문자열 enum: EnumMember 어트리뷰트로 원본 값(camelCase) 지정
   */
  private generateEnum(typeDef: ParsedTypeDefinition): string {
    if (!typeDef.enumValues || typeDef.enumValues.length === 0) {
      return '';
    }

    // 숫자 enum인지 확인 (하나라도 { name, value } 형태면 숫자 enum)
    const isNumericEnum = typeDef.enumValues.some(
      v => typeof v === 'object' && v !== null && 'value' in v
    );

    const enumMembers = typeDef.enumValues
      .map(value => {
        if (isNumericEnum) {
          // 숫자 enum: EnumMember 없이 명시적 값 할당
          // StringEnumConverter가 적용되지 않아 숫자로 직렬화됨
          const item = typeof value === 'object' && value !== null && 'name' in value
            ? value as { name: string; value: number }
            : { name: value as string, value: 0 };

          let memberName = item.name;
          // 숫자로 시작하는 경우 언더스코어 추가
          if (/^\d/.test(memberName)) {
            memberName = `_${memberName}`;
          }
          const pascalValue = memberName.charAt(0).toUpperCase() + memberName.slice(1);
          return `        ${pascalValue} = ${item.value}`;
        } else {
          // 문자열 enum: EnumMember 어트리뷰트 사용
          const originalValue = value as string;
          let memberName = originalValue;

          // 숫자로 시작하는 경우 언더스코어 추가 (C# 식별자 규칙)
          if (/^\d/.test(memberName)) {
            memberName = `_${memberName}`;
          }

          // camelCase를 PascalCase로 변환
          const pascalValue = memberName.charAt(0).toUpperCase() + memberName.slice(1);

          // EnumMember 어트리뷰트로 원본 값 지정 (JSON 직렬화 시 사용)
          return `        [EnumMember(Value = "${originalValue}")]\n        ${pascalValue}`;
        }
      })
      .join(',\n');

    const description = typeDef.description
      ? `    /// <summary>\n    /// ${typeDef.description}\n    /// </summary>\n`
      : '';

    // 참고: [JsonConverter(typeof(StringEnumConverter))]는 IL2CPP에서 타입 해석 무한 루프를 일으킴
    // EnumMember 어트리뷰트만 사용하고, 직렬화 시 JsonSerializerSettings에서 StringEnumConverter 설정
    return `${description}    public enum ${typeDef.name}\n    {\n${enumMembers}\n    }`;
  }

  /**
   * interface를 class로 생성
   */
  private generateInterfaceAsClass(typeDef: ParsedTypeDefinition): string {
    if (!typeDef.properties || typeDef.properties.length === 0) {
      return '';
    }

    // Note: 중첩 타입은 generateTypes에서 collectReferencedTypes로 생성됨 (중복 방지)
    const fields = typeDef.properties
      .map(prop => {
        let type = mapToCSharpType(prop.type);

        // 중첩 익명 객체 처리 - 타입 이름만 생성 (클래스는 별도 생성)
        if (prop.type.kind === 'object' &&
            prop.type.properties &&
            prop.type.properties.length > 0 &&
            (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
          // 중첩 클래스 이름 생성
          type = `${typeDef.name}${capitalize(prop.name)}`;
        }

        const description = prop.description
          ? `        /// <summary>${xmlSafe(prop.description)}</summary>\n`
          : '';
        return `${description}${generateFieldDeclaration(prop.name, type, prop.optional)}`;
      })
      .join('\n');

    const description = typeDef.description
      ? `    /// <summary>\n    /// ${typeDef.description}\n    /// </summary>\n`
      : '';

    return `${description}    [Serializable]\n    [Preserve]\n    public class ${typeDef.name}\n    {\n${fields}\n\n${JSON_EXTENSION_DATA_FIELD}\n    }`;
  }

  /**
   * 타입 정의들을 C# 코드로 생성 (헤더 없이, 타입 1개당 1 유닛)
   * @param typeDefinitions 파싱된 타입 정의 목록
   * @param excludeTypeNames 제외할 타입 이름 Set (API에서 이미 생성된 타입)
   * @returns 객체 { units: 타입별 유닛 배열, generatedTypeNames: 생성된 타입 이름 Set,
   *                stringEnumValues: 문자열 enum 이름→값 셋(원본 camelCase) Map (inline enum dedup용) }
   *
   * 파싱된 도메인 타입은 특정 API에 직접 귀속되지 않으므로 모두 'Other' 카테고리로 분류한다
   * (AIT.Types.Other.cs). getCategory는 API 이름용이라 도메인 타입명에 적용하면 경고만 양산되므로
   * 호출하지 않는다.
   */
  async generateTypeDefinitions(
    typeDefinitions: ParsedTypeDefinition[],
    excludeTypeNames?: Set<string>
  ): Promise<{ units: GeneratedTypeUnit[]; generatedTypeNames: Set<string>; stringEnumValues: Map<string, string[]> }> {
    const units: GeneratedTypeUnit[] = [];
    const generatedTypeNames = new Set<string>();
    const stringEnumValues = new Map<string, string[]>();
    const exclude = excludeTypeNames || new Set<string>();

    for (const typeDef of typeDefinitions) {
      if (typeDef.kind === 'enum') {
        const enumCode = this.generateEnum(typeDef);
        if (enumCode) {
          units.push({ name: typeDef.name, code: enumCode, category: DEFAULT_CATEGORY });
          generatedTypeNames.add(typeDef.name);
          // inline enum dedup용 — 문자열 enum의 원본 값만 수집 (숫자 enum은 dedup 대상 아님)
          if (typeDef.enumValues && typeDef.enumValues.length > 0) {
            const isNumeric = typeDef.enumValues.some(
              v => typeof v === 'object' && v !== null && 'value' in v
            );
            if (!isNumeric) {
              stringEnumValues.set(typeDef.name, typeDef.enumValues as string[]);
            }
          }
        }
      } else if (typeDef.kind === 'interface') {
        const classCode = this.generateInterfaceAsClass(typeDef);
        if (classCode) {
          units.push({ name: typeDef.name, code: classCode, category: DEFAULT_CATEGORY });
          generatedTypeNames.add(typeDef.name);
        }

        // 중첩 타입 수집 및 생성 (type definitions에서 참조되는 nested types)
        if (typeDef.properties) {
          const nestedTypes = new Map<string, string>();
          collectNestedTypesForTypeDefinition(typeDef.name, typeDef.properties, nestedTypes, exclude, generatedTypeNames);
          for (const [nestedName, nestedCode] of nestedTypes) {
            if (!generatedTypeNames.has(nestedName) && !exclude.has(nestedName)) {
              units.push({ name: nestedName, code: nestedCode, category: DEFAULT_CATEGORY });
              generatedTypeNames.add(nestedName);
            }
          }
        }
      }
    }

    // 헤더/푸터 없이 유닛만 반환 (호출자가 카테고리별로 합침)
    return { units, generatedTypeNames, stringEnumValues };
  }

  /**
   * 클래스 타입 생성
   * Note: 중첩 타입은 collectReferencedTypes에서 별도로 생성됨 (중복 방지)
   */
  private generateClassType(name: string, properties: any[], isResultType: boolean = false): string {
    // extractCleanName을 사용하여 정리된 이름 얻기
    const cleanName = extractCleanName(name);

    const fields = properties
      .map(prop => {
        let type = mapToCSharpType(prop.type);

        // Inline string literal union -> enum 변환
        if (isInlineStringLiteralUnion(prop.type)) {
          const enumName = `${cleanName}${capitalize(prop.name)}`;
          const enumValues = extractEnumValues(prop.type);
          // enum 수집 (중복 방지)
          if (!this.tracker.inlineEnums.has(enumName)) {
            this.tracker.inlineEnums.set(enumName, enumValues);
          }
          type = enumName;
        }
        // 콜백(함수 타입)의 첫 번째 파라미터가 인라인 익명 객체 리터럴인 경우
        // object로 뭉개지지 않도록 명명 클래스(...Param)를 참조하도록 치환한다.
        // named param(예: BannerSlotEventPayload)·onError(kind!=='object')는 가드로 배제 → 불변.
        // 주의: name이 '__type'이라도 raw가 named type을 가리키면(예: LoadAdMobEvent)
        //   mapToCSharpType이 그 이름을 복구하므로 'object'가 아니게 되어 배제된다
        //   (collectFunctionParamTypes의 익명 판정과 일치 → 등록되지 않은 클래스 참조 방지).
        else if (
          prop.type.kind === 'function' &&
          isInlineAnonymousObjectParam(prop.type.functionParams?.[0])
        ) {
          const className = functionParamTypeName(cleanName, prop.name);
          // functionParams[0]만 명명 클래스로 교체한 합성 함수 타입을 매핑
          const synthFn = {
            ...prop.type,
            functionParams: [
              { ...prop.type.functionParams[0], name: className },
              ...prop.type.functionParams.slice(1),
            ],
          };
          type = mapToCSharpType(synthFn);
        }
        // 중첩 익명 객체는 생성된 클래스 이름 사용
        else if (prop.type.kind === 'object' &&
            prop.type.properties &&
            prop.type.properties.length > 0 &&
            (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
          type = `${cleanName}${capitalize(prop.name)}`;
        }
        // Union 타입의 멤버가 intersection/익명 객체인 경우 (예: (Sku & Opts) | (Sku2 & Opts))
        // 모든 멤버의 프로퍼티를 병합하여 단일 클래스 생성
        else if (prop.type.kind === 'union' && prop.type.unionTypes && prop.type.unionTypes.length > 0) {
          // Union 멤버 중 프로퍼티가 있는 객체/intersection 타입만 수집
          const membersWithProps = prop.type.unionTypes.filter(
            (t: any) => (t.kind === 'object' || t.isIntersection) && t.properties && t.properties.length > 0
          );
          if (membersWithProps.length > 0) {
            type = `${cleanName}${capitalize(prop.name)}`;
          }
        }
        // 익명 객체 배열은 생성된 클래스 이름 + [] 사용
        else if (prop.type.kind === 'array' &&
            prop.type.elementType &&
            prop.type.elementType.kind === 'object' &&
            prop.type.elementType.properties &&
            prop.type.elementType.properties.length > 0 &&
            (prop.type.elementType.name === '__type' || prop.type.elementType.name === 'object' || prop.type.elementType.name.startsWith('{'))) {
          const propNameSingular = prop.name.endsWith('s') ? prop.name.slice(0, -1) : prop.name;
          type = `${cleanName}${capitalize(propNameSingular)}[]`;
        }
        const description = prop.description
          ? `        /// <summary>${xmlSafe(prop.description)}</summary>\n`
          : '';
        return `${description}${generateFieldDeclaration(prop.name, type, prop.optional)}`;
      })
      .join('\n');

    // Result 타입이면 error 필드 추가 (jslib 에러 응답 수신용)
    const errorField = isResultType
      ? '\n        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>\n        public string error;'
      : '';

    return `    [Serializable]
    [Preserve]
    public class ${cleanName}
    {
${fields}${errorField}

${JSON_EXTENSION_DATA_FIELD}
    }`;
  }

  /**
   * 찾을 수 없는 타입에 대한 빈 stub 클래스 생성
   * SDK 버전 호환성: 이전 버전에 없는 타입이 참조될 때 컴파일 오류 방지
   */
  private generateStubClass(typeName: string): string {
    const cleanName = extractCleanName(typeName);
    const lines: string[] = [];

    lines.push('    /// <summary>');
    lines.push(`    /// Stub class for ${cleanName} (type definition not found in current SDK version)`);
    lines.push('    /// This class is auto-generated for SDK version compatibility');
    lines.push('    /// </summary>');
    lines.push('    [Serializable]');
    lines.push('    [Preserve]');
    lines.push(`    public class ${cleanName}`);
    lines.push('    {');
    lines.push('        // Stub class - no properties defined');
    lines.push('        // This type may not be available in the current SDK version');
    lines.push('    }');

    return lines.join('\n');
  }

  /**
   * API에서 사용되는 모든 타입 정의 생성
   *
   * @param parsedStringEnumValues 문자열 named enum 이름→값 셋. inline enum과 값 셋이
   *   완전 일치하면 inline enum 생성을 생략하고 named enum 이름으로 alias한다.
   */
  async generateTypes(
    apis: ParsedAPI[],
    excludeTypeNames?: Set<string>,
    typeDefinitions?: ParsedTypeDefinition[],
    parser?: { parseNativeModulesType: (typeName: string) => ParsedTypeDefinition | null },
    parsedStringEnumValues?: Map<string, string[]>
  ): Promise<GeneratedTypeUnit[]> {
    const typeMap = new Map<string, string>(); // typeName -> classDefinition
    const unionResultMap = new Map<string, string>(); // API name -> Union Result class
    const exclude = excludeTypeNames || new Set<string>();

    // 타입/enum 이름 → 카테고리 귀속 맵. API 루프에서 각 api 처리 전후의 키 델타로 채운다.
    // (카테고리는 순전히 미용 목적 — 모든 타입이 같은 namespace AppsInToss라 오분류해도 컴파일됨)
    const typeCategory = new Map<string, string>();
    const inlineEnumCategory = new Map<string, string>();

    // Inline enum Map 초기화 (generateClassType에서 채워짐)
    this.tracker.clear();

    // generateClassType을 바인딩하여 collectReferencedTypes에 전달
    const boundGenerateClassType = this.generateClassType.bind(this);

    // API에서 사용되는 모든 타입 수집
    for (const api of apis) {
      // 이 api가 새로 추가하는 타입/enum 키를 잡기 위한 처리 전 스냅샷
      // (경고는 generateCategoryFiles가 이미 1회 출력하므로 여기서는 silent)
      const apiCategory = resolveApiCategory(api, false);
      const typeKeysBefore = new Set(typeMap.keys());
      const urKeysBefore = new Set(unionResultMap.keys());
      const enumKeysBefore = new Set(this.tracker.inlineEnums.keys());

      // Discriminated Union 타입 처리
      if (api.returnType.kind === 'promise' && api.returnType.promiseType) {
        const innerType = api.returnType.promiseType;
        if (innerType.isDiscriminatedUnion && innerType.successType && innerType.errorCodes) {
          const resultClassName = `${api.pascalName}Result`;
          if (!unionResultMap.has(resultClassName)) {
            const unionResultCode = await this.generateUnionResult(
              api.pascalName,
              innerType.successType,
              innerType.errorCodes
            );
            unionResultMap.set(resultClassName, unionResultCode);
          }
        }
      }

      // 파라미터 타입
      for (const param of api.parameters) {
        if (param.type.kind === 'object' && param.type.properties && param.type.properties.length > 0) {
          // 익명 타입이면 의미있는 이름 생성, named type이면 그대로 사용
          let typeName = param.type.name;
          const isAnonymous = typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName;
          if (isAnonymous) {
            // raw 필드에서 named type 추출 시도 (import("...").TypeName 형식)
            if (param.type.raw && param.type.raw.includes('.') && !param.type.raw.trim().startsWith('{')) {
              const rawTypeName = param.type.raw.split('.').pop()?.replace(/["'{}(),;\s$<>|]/g, '').trim();
              if (rawTypeName && rawTypeName !== '__type' && !rawTypeName.startsWith('{')) {
                typeName = rawTypeName;
              } else {
                typeName = `${capitalize(api.name)}${capitalize(param.name)}`;
              }
            } else {
              typeName = `${capitalize(api.name)}${capitalize(param.name)}`;
            }
          } else if (param.optional) {
            // optional named 객체 파라미터는 API-스코프 합성명을 쓴다(예: AttachOptions →
            // TossAdsAttachOptions). prepareParameters의 시그니처 합성과 반드시 일치시켜
            // 클래스명과 참조가 어긋나 CS0246이 나지 않도록 한다. (자세한 근거는
            // api-data-preparer.ts prepareParameters 주석 참조.)
            typeName = `${capitalize(api.name)}${capitalize(param.name)}`;
          }

          // cleanName을 키로 사용 (중복 방지)
          const cleanName = extractCleanName(typeName);
          if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
            typeMap.set(cleanName, this.generateClassType(typeName, param.type.properties));
          }

          // 프로퍼티에서 참조되는 named type도 수집 (재귀)
          collectReferencedTypes(param.type.properties, typeMap, exclude, this.tracker, boundGenerateClassType, cleanName);

          // 객체의 프로퍼티에 함수 타입이 있으면 함수 파라미터 타입도 수집
          for (const prop of param.type.properties) {
            if (prop.type.kind === 'function' && prop.type.functionParams) {
              for (const funcParam of prop.type.functionParams) {
                collectFunctionParamTypes(funcParam, typeMap, exclude, this.tracker, boundGenerateClassType);
              }
            }
          }
        }
        // unknown 타입 파라미터: import("...").TypeName 형식의 외부 타입
        else if (param.type.kind === 'unknown' && param.type.name && param.type.name.includes('.')) {
          const typeName = extractCleanName(param.type.name);
          if (typeName && typeName !== '__type' && typeName !== 'undefined' &&
              !typeMap.has(typeName) && !exclude.has(typeName)) {
            this.tracker.pendingExternalTypes.add(typeName);
          }
        }
      }

      // 반환 타입 (Promise 내부 포함)
      if (api.returnType.kind === 'promise' && api.returnType.promiseType) {
        const innerType = api.returnType.promiseType;

        // Union 타입: named type이면 그대로 사용, 익명이면 각 멤버를 개별 클래스로
        if (innerType.kind === 'union' && innerType.unionTypes) {
          // Union 자체가 named type인지 확인
          const isNamedUnion = innerType.name && innerType.name.includes('.');

          if (isNamedUnion) {
            // named union: 멤버들을 하나의 클래스로 병합하거나 개별 생성
            // 여기서는 named type 이름을 사용
            const unionTypeName = extractCleanName(innerType.name);

            // Union의 모든 멤버 속성을 수집
            const allProperties = new Map<string, any>();
            for (const member of innerType.unionTypes) {
              if (member.kind === 'object' && member.properties) {
                for (const prop of member.properties) {
                  if (!allProperties.has(prop.name)) {
                    allProperties.set(prop.name, prop);
                  }
                }
              }
            }

            if (allProperties.size > 0 && !typeMap.has(unionTypeName) && !exclude.has(unionTypeName)) {
              typeMap.set(unionTypeName, this.generateClassType(
                innerType.name,
                Array.from(allProperties.values())
              ));
              // 프로퍼티에서 참조되는 named type도 수집 (재귀)
              collectReferencedTypes(Array.from(allProperties.values()), typeMap, exclude, this.tracker, boundGenerateClassType, unionTypeName);
            }
          } else {
            // 익명 union: 각 멤버를 별도 클래스로
            for (const unionMember of innerType.unionTypes) {
              if (unionMember.kind === 'object' && unionMember.properties && unionMember.properties.length > 0) {
                let typeName = unionMember.name;
                if (typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName) {
                  typeName = `${capitalize(api.name)}Result`;
                }

                const cleanName = extractCleanName(typeName);
                if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
                  // 반환 타입이므로 isResultType: true - error 필드 추가
                  typeMap.set(cleanName, this.generateClassType(typeName, unionMember.properties, true));
                  // 프로퍼티에서 참조되는 named type도 수집 (재귀)
                  collectReferencedTypes(unionMember.properties, typeMap, exclude, this.tracker, boundGenerateClassType, cleanName);
                }
              }
            }
          }
        }
        // 일반 객체
        else if (innerType.kind === 'object' && innerType.properties && innerType.properties.length > 0) {
          let typeName = innerType.name;
          if (typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName) {
            typeName = `${capitalize(api.name)}Result`;
          }

          const cleanName = extractCleanName(typeName);
          if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
            // 반환 타입이므로 isResultType: true - error 필드 추가
            typeMap.set(cleanName, this.generateClassType(typeName, innerType.properties, true));
            // 프로퍼티에서 참조되는 named type도 수집 (재귀)
            collectReferencedTypes(innerType.properties, typeMap, exclude, this.tracker, boundGenerateClassType, cleanName);
          }
        }
      }
      // 동기 함수의 반환 타입
      else if (api.returnType.kind === 'object' && api.returnType.properties && api.returnType.properties.length > 0) {
        let typeName = api.returnType.name;
        if (typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName) {
          typeName = `${capitalize(api.name)}Result`;
        }

        const cleanName = extractCleanName(typeName);
        if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
          // 반환 타입이므로 isResultType: true - error 필드 추가
          typeMap.set(cleanName, this.generateClassType(typeName, api.returnType.properties, true));
          // 프로퍼티에서 참조되는 named type도 수집 (재귀)
          collectReferencedTypes(api.returnType.properties, typeMap, exclude, this.tracker, boundGenerateClassType, cleanName);
        }
      }

      // 이 api가 새로 추가한 타입/enum 키에 카테고리 귀속 (최초 등장 api 기준, 덮어쓰지 않음)
      for (const k of typeMap.keys()) {
        if (!typeKeysBefore.has(k) && !typeCategory.has(k)) typeCategory.set(k, apiCategory);
      }
      for (const k of unionResultMap.keys()) {
        if (!urKeysBefore.has(k) && !typeCategory.has(k)) typeCategory.set(k, apiCategory);
      }
      for (const k of this.tracker.inlineEnums.keys()) {
        if (!enumKeysBefore.has(k) && !inlineEnumCategory.has(k)) inlineEnumCategory.set(k, apiCategory);
      }
    }

    // Pending external types 해결: typeDefinitions와 native-modules에서 찾아서 클래스 생성
    // 타입을 찾을 수 없는 경우 빈 stub 클래스 생성 (SDK 버전 호환성)
    if (this.tracker.pendingExternalTypes.size > 0) {
      // 재귀적으로 추가된 pending types 해결 (최대 10번 반복하여 무한 루프 방지)
      let iteration = 0;
      const unresolvedTypes = new Set<string>(); // 찾을 수 없는 타입 추적

      while (this.tracker.pendingExternalTypes.size > 0 && iteration < 10) {
        const remainingTypes = new Set(this.tracker.pendingExternalTypes);
        this.tracker.pendingExternalTypes.clear();

        for (const typeName of remainingTypes) {
          if (typeMap.has(typeName) || exclude.has(typeName)) {
            continue;
          }

          // 1. typeDefinitions에서 먼저 찾기
          let typeDef = typeDefinitions?.find(t => t.name === typeName);

          // 2. 없으면 native-modules에서 찾기
          if (!typeDef && parser) {
            typeDef = parser.parseNativeModulesType(typeName) || undefined;
          }

          if (typeDef && typeDef.kind === 'interface' && typeDef.properties && typeDef.properties.length > 0) {
            typeMap.set(typeName, this.generateClassType(typeName, typeDef.properties));
            collectReferencedTypes(typeDef.properties, typeMap, exclude, this.tracker, boundGenerateClassType, typeName);
          } else {
            // 타입을 찾을 수 없는 경우 추적
            unresolvedTypes.add(typeName);
          }
        }
        iteration++;
      }

      // 찾을 수 없는 타입에 대해 빈 stub 클래스 생성 (SDK 버전 호환성)
      // 이전 SDK 버전에 없는 타입이 참조되는 경우 컴파일 오류 방지
      for (const typeName of unresolvedTypes) {
        if (!typeMap.has(typeName) && !exclude.has(typeName)) {
          const stubClass = this.generateStubClass(typeName);
          typeMap.set(typeName, stubClass);
          console.log(`  [Stub] Generated stub class for unresolved type: ${typeName}`);
        }
      }
    }

    // Inline string-literal-union enum을 named enum/이전 inline enum과 dedup (값 셋이 같으면 alias)
    const { emit: inlineEmit, aliases: enumAliases } = computeEnumAliases(
      this.tracker.inlineEnums,
      parsedStringEnumValues,
    );

    // 타입 1개당 1 유닛으로 반환. alias는 본문 식별자 word-boundary 치환이라 유닛별로 적용해도
    // 전체를 합쳐 적용한 것과 결과가 동일하다 (블록 간 의존 없음). 순서는 기존 단일 파일과 동일:
    // inline enum → union result → 일반 타입.
    const units: GeneratedTypeUnit[] = [];

    for (const { name, values } of inlineEmit) {
      const enumCode = this.generateEnum({ name, kind: 'enum', file: '', enumValues: values });
      if (enumCode) {
        units.push({
          name,
          code: applyEnumAliases(enumCode, enumAliases),
          category: inlineEnumCategory.get(name) ?? DEFAULT_CATEGORY,
        });
      }
    }

    for (const [name, code] of unionResultMap) {
      units.push({
        name,
        code: applyEnumAliases(code, enumAliases),
        category: typeCategory.get(name) ?? DEFAULT_CATEGORY,
      });
    }

    // API 루프 밖(pending/stub 해결 단계)에서 추가된 타입은 typeCategory에 없으므로 'Other'로 흡수
    for (const [name, code] of typeMap) {
      units.push({
        name,
        code: applyEnumAliases(code, enumAliases),
        category: typeCategory.get(name) ?? DEFAULT_CATEGORY,
      });
    }

    return units;
  }
}
