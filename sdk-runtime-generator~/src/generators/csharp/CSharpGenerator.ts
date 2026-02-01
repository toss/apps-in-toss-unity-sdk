import { ParsedAPI, GeneratedCode } from '../../types.js';
import { mapToCSharpType } from '../../validators/types.js';
import { getCategory, CATEGORY_ORDER } from '../../categories.js';
import { loadAllTemplates, getTemplateCache } from './templates.js';
import { escapeCSharpKeyword, toPascalCase } from './utils.js';
import { PRIMITIVE_TYPES, EXCLUDED_CALLBACK_TYPES } from './constants.js';
import {
  prepareApiData,
  prepareParameters,
  determineCallbackType,
  extractCallbackEventType,
} from './api-data-preparer.js';

/**
 * C# 생성기
 */
export class CSharpGenerator {
  constructor() {
    // 헬퍼 등록은 loadTemplates에서 수행
  }

  /**
   * 템플릿 로드
   */
  private async loadTemplates(): Promise<void> {
    await loadAllTemplates();
  }

  /**
   * 단일 API 생성
   */
  private generateAPI(api: ParsedAPI): string {
    const templates = getTemplateCache();
    if (!templates.apiTemplate) {
      throw new Error('Templates not loaded');
    }

    // 파라미터 변환
    const parameters = api.parameters.map(param => {
      let paramType = mapToCSharpType(param.type);

      // 파라미터가 익명 객체(__type, object)인 경우 의미있는 이름 생성
      if ((paramType === '__type' || paramType === 'object') && param.type.kind === 'object' && param.type.properties && param.type.properties.length > 0) {
        const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
        const apiName = capitalize(api.name);
        const paramName = capitalize(param.name);
        paramType = `${apiName}${paramName}`;
      }

      return {
        paramName: escapeCSharpKeyword(param.name), // C# 예약어 처리
        paramType,
        optional: param.optional,
        description: param.description, // JSDoc 파라미터 설명 추가
      };
    });

    // C# 규칙: optional 파라미터는 필수 파라미터 뒤에 와야 함
    parameters.sort((a, b) => {
      if (a.optional === b.optional) return 0;
      return a.optional ? 1 : -1;
    });

    // 반환 타입 변환
    const returnType = mapToCSharpType(api.returnType);

    // 콜백 타입 결정: 비동기는 Promise 내부 타입, 동기는 반환 타입 직접 사용
    let callbackType: string;
    if (api.returnType.kind === 'promise' && api.returnType.promiseType) {
      callbackType = mapToCSharpType(api.returnType.promiseType);
    } else {
      // 동기 함수: 반환 타입을 직접 사용
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

    // Discriminated Union 여부 및 정보
    const isDiscriminatedUnion = api.returnType.kind === 'promise' &&
                                  api.returnType.promiseType?.isDiscriminatedUnion === true;
    const unionInfo = isDiscriminatedUnion ? {
      successType: mapToCSharpType(api.returnType.promiseType!.successType!),
      errorCodes: api.returnType.promiseType!.errorCodes || []
    } : undefined;

    // nullable 참조 타입 여부 확인 (C#에서 ?를 붙일 수 없지만 문서에 명시 필요)
    const innerType = api.returnType.kind === 'promise' ? api.returnType.promiseType : api.returnType;
    const isNullableReturn = innerType?.isNullable === true && callbackType !== 'void';

    const data = {
      name: api.name,
      pascalName: api.pascalName, // PascalCase 이름 추가
      originalName: api.originalName, // 원본 이름 추가 (jslib 매핑용)
      description: api.description,
      returnDescription: api.returnDescription, // @returns 태그 설명 추가
      examples: api.examples, // @example 태그들 추가
      parameters,
      returnType,
      isAsync: api.isAsync,
      callbackType,
      isDiscriminatedUnion,
      unionInfo,
      isNullableReturn, // nullable 참조 타입 여부 (문서용)
    };

    const generated = templates.apiTemplate(data);
    // 개별 API는 포맷팅하지 않음 (완전한 파일이 아니므로 CSharpier 컴파일 실패)
    // 전체 클래스 파일에서 한 번에 포맷팅
    return generated;
  }

  /**
   * 전체 C# 클래스 생성
   */
  async generate(
    apis: ParsedAPI[],
    webFrameworkTag: string
  ): Promise<GeneratedCode[]> {
    await this.loadTemplates();

    const templates = getTemplateCache();
    if (!templates.classTemplate) {
      throw new Error('Class template not loaded');
    }

    // 각 API를 문자열로 생성
    const generatedAPIs = apis.map(api => this.generateAPI(api));

    // 전체 클래스 생성
    const classCode = templates.classTemplate({
      webFrameworkTag,
      timestamp: new Date().toISOString(),
      apis: generatedAPIs,
    });

    // GeneratedCode 배열 반환
    return apis.map((api, index) => ({
      api,
      csharp: generatedAPIs[index],
      jslib: '', // jslib는 별도 생성기에서
    }));
  }

  /**
   * 전체 클래스 파일 생성
   */
  async generateClassFile(
    apis: ParsedAPI[],
    webFrameworkTag: string
  ): Promise<string> {
    await this.loadTemplates();

    const templates = getTemplateCache();
    if (!templates.classTemplate) {
      throw new Error('Class template not loaded');
    }

    const generatedAPIs = apis.map(api => this.generateAPI(api));

    const classCode = templates.classTemplate({
      webFrameworkTag,
      timestamp: new Date().toISOString(),
      apis: generatedAPIs,
    });

    // 템플릿으로 생성된 코드를 그대로 반환
    return classCode;
  }

  /**
   * AITCore.cs 파일 생성 (인프라 코드)
   * @param apis API 목록
   * @param enumTypeNames enum 타입 이름 Set (enum은 별도 처리 필요)
   */
  async generateCoreFile(apis: ParsedAPI[], enumTypeNames?: Set<string>): Promise<string> {
    await this.loadTemplates();

    const templates = getTemplateCache();
    if (!templates.coreTemplate) {
      throw new Error('Core template not loaded');
    }

    // 모든 API의 콜백 타입 수집 (동기/비동기 모두 Unity SDK에서는 콜백 패턴 사용)
    const callbackTypes = new Set<string>();
    const enumCallbackTypes = new Set<string>();

    // 이벤트 데이터 타입 수집 (void가 아닌 이벤트)
    const eventDataTypes = new Set<string>();

    for (const api of apis) {
      // 이벤트 구독 API에서 이벤트 데이터 타입 수집
      if (api.isEventSubscription && api.eventDataType) {
        const dataType = mapToCSharpType(api.eventDataType);
        if (dataType !== 'void' && dataType !== 'undefined') {
          eventDataTypes.add(dataType);
        }
        continue;
      }

      // Callback-based API (loadFullScreenAd, GoogleAdMob.loadAppsInTossAdMob 패턴) 이벤트 타입 수집
      if (api.isCallbackBased) {
        const callbackEventType = extractCallbackEventType(api);
        if (callbackEventType && callbackEventType !== 'void') {
          eventDataTypes.add(callbackEventType);
        }
        continue;
      }

      // 이벤트 구독 API는 콜백 타입 수집에서 제외
      if (api.isEventSubscription) continue;

      let callbackType: string;

      // 비동기 API (Promise 반환)
      if (api.returnType.kind === 'promise' && api.returnType.promiseType) {
        callbackType = mapToCSharpType(api.returnType.promiseType);

        // Discriminated Union인 경우 Result 클래스 사용
        if (api.returnType.promiseType.isDiscriminatedUnion) {
          callbackType = `${api.pascalName}Result`;
        }
      }
      // 동기 API (직접 값 반환)
      else {
        callbackType = mapToCSharpType(api.returnType);
      }

      // 익명 객체 타입(__type, object)이면 Result 클래스 사용
      if (callbackType === '__type' || callbackType === 'object') {
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
          callbackType = `${api.pascalName}Result`;
        }
      }

      // void, object, __type, primitive 타입은 제외 (템플릿에서 수동으로 처리)
      if (!EXCLUDED_CALLBACK_TYPES.includes(callbackType) && !callbackType.startsWith('System.Action')) {
        // enum 타입과 일반 타입 분리
        if (enumTypeNames && enumTypeNames.has(callbackType)) {
          enumCallbackTypes.add(callbackType);
        } else {
          callbackTypes.add(callbackType);
        }
      }
    }

    return templates.coreTemplate({
      callbackTypes: Array.from(callbackTypes).sort(),
      enumCallbackTypes: Array.from(enumCallbackTypes).sort(),
      eventDataTypes: Array.from(eventDataTypes).sort(),
    });
  }

  /**
   * 메인 AIT.cs 파일 생성 (partial class 선언만)
   */
  async generateMainFile(webFrameworkTag: string, apiCount: number): Promise<string> {
    await this.loadTemplates();

    const templates = getTemplateCache();
    if (!templates.mainTemplate) {
      throw new Error('Main template not loaded');
    }

    return templates.mainTemplate({
      webFrameworkTag,
      timestamp: new Date().toISOString(),
      apiCount,
    });
  }

  /**
   * 개별 API의 partial class 파일 생성
   */
  async generatePartialApiFile(api: ParsedAPI): Promise<string> {
    await this.loadTemplates();

    const templates = getTemplateCache();
    if (!templates.partialApiTemplate) {
      throw new Error('Partial API template not loaded');
    }

    // 파라미터 변환
    const parameters = api.parameters.map(param => {
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

    // 반환 타입 변환
    const returnType = mapToCSharpType(api.returnType);

    // 콜백 타입 결정: 비동기는 Promise 내부 타입, 동기는 반환 타입 직접 사용
    let callbackType: string;
    if (api.returnType.kind === 'promise' && api.returnType.promiseType) {
      callbackType = mapToCSharpType(api.returnType.promiseType);
    } else {
      // 동기 함수: 반환 타입을 직접 사용
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

    const data = {
      name: api.name,
      pascalName: api.pascalName,
      originalName: api.originalName,
      description: api.description,
      returnDescription: api.returnDescription,
      examples: api.examples,
      parameters,
      returnType,
      isAsync: api.isAsync,
      callbackType,
    };

    const generated = templates.partialApiTemplate(data);
    // 템플릿으로 생성된 코드를 그대로 반환
    return generated;
  }

  /**
   * 모든 API의 partial class 파일들을 Map으로 반환
   * @returns Map<파일명, 내용>
   * @deprecated Use generateCategoryFiles() instead for category-based grouping
   */
  async generatePartialApiFiles(apis: ParsedAPI[]): Promise<Map<string, string>> {
    const files = new Map<string, string>();

    for (const api of apis) {
      const fileName = `AIT.${api.pascalName}.cs`;
      const content = await this.generatePartialApiFile(api);
      files.set(fileName, content);
    }

    return files;
  }

  /**
   * API를 카테고리별로 그룹핑하여 partial class 파일들을 생성
   * @returns Map<파일명, 내용>
   */
  async generateCategoryFiles(apis: ParsedAPI[]): Promise<Map<string, string>> {
    await this.loadTemplates();

    const templates = getTemplateCache();
    if (!templates.categoryPartialTemplate) {
      throw new Error('Category partial template not loaded');
    }

    const files = new Map<string, string>();

    // API를 카테고리별로 그룹핑
    // 이벤트 API는 파서에서 설정한 카테고리 사용, 나머지는 categories.ts에서 룩업
    const categoryMap = new Map<string, ParsedAPI[]>();
    for (const api of apis) {
      // 이벤트 구독 API는 파서에서 설정한 카테고리 사용
      // 네임스페이스 API는 전체 이름(pascalName)으로 룩업
      // 일반 API는 원본 이름(originalName)으로 룩업
      let category: string;
      if (api.isEventSubscription) {
        category = api.category;
      } else if (api.namespace) {
        // 네임스페이스 API: 전체 이름으로 룩업 (예: IAPGetProductItemList)
        category = getCategory(api.pascalName);
      } else {
        // 일반 API: 원본 이름으로 룩업 (예: appLogin)
        category = getCategory(api.originalName);
      }
      if (!categoryMap.has(category)) {
        categoryMap.set(category, []);
      }
      categoryMap.get(category)!.push(api);
    }

    // 카테고리 순서에 따라 파일 생성 (CATEGORY_ORDER 우선, 그 외 알파벳 순)
    const processedCategories = new Set<string>();

    // 1. CATEGORY_ORDER에 있는 카테고리 먼저 처리
    for (const category of CATEGORY_ORDER) {
      const categoryApis = categoryMap.get(category);
      if (!categoryApis || categoryApis.length === 0) continue;

      // 각 API에 대해 템플릿 데이터 준비
      const apisData = categoryApis.map(api => prepareApiData(api));

      const content = templates.categoryPartialTemplate({
        categoryName: category,
        apis: apisData,
      });

      const fileName = `AIT.${category}.cs`;
      files.set(fileName, content);
      processedCategories.add(category);
    }

    // 2. CATEGORY_ORDER에 없는 새로운 카테고리 처리 (동적 감지된 카테고리)
    const remainingCategories = Array.from(categoryMap.keys())
      .filter(cat => !processedCategories.has(cat))
      .sort(); // 알파벳 순

    for (const category of remainingCategories) {
      const categoryApis = categoryMap.get(category)!;
      const apisData = categoryApis.map(api => prepareApiData(api));

      const content = templates.categoryPartialTemplate({
        categoryName: category,
        apis: apisData,
      });

      const fileName = `AIT.${category}.cs`;
      files.set(fileName, content);
    }

    return files;
  }
}
