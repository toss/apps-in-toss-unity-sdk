import Handlebars from 'handlebars';
import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { ParsedAPI, ParsedParameter, GeneratedCode, ParsedTypeDefinition, ParsedType } from '../types.js';
import { mapToCSharpType } from '../validators/types.js';
import { getCategory, CATEGORY_ORDER } from '../categories.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * C# 예약어 목록
 */
const CSHARP_KEYWORDS = new Set([
  'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char',
  'checked', 'class', 'const', 'continue', 'decimal', 'default', 'delegate',
  'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false',
  'finally', 'fixed', 'float', 'for', 'foreach', 'goto', 'if', 'implicit',
  'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace',
  'new', 'null', 'object', 'operator', 'out', 'override', 'params', 'private',
  'protected', 'public', 'readonly', 'ref', 'return', 'sbyte', 'sealed',
  'short', 'sizeof', 'stackalloc', 'static', 'string', 'struct', 'switch',
  'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong', 'unchecked',
  'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while'
]);

/**
 * C# 예약어를 안전한 변수명으로 변환
 */
function escapeCSharpKeyword(name: string): string {
  if (CSHARP_KEYWORDS.has(name)) {
    return `${name}Param`; // params -> paramsParam
  }
  return name;
}

/**
 * C# 생성기
 */
export class CSharpGenerator {
  private apiTemplate?: HandlebarsTemplateDelegate;
  private classTemplate?: HandlebarsTemplateDelegate;
  private coreTemplate?: HandlebarsTemplateDelegate;
  private partialApiTemplate?: HandlebarsTemplateDelegate;
  private mainTemplate?: HandlebarsTemplateDelegate;
  private categoryPartialTemplate?: HandlebarsTemplateDelegate;

  constructor() {
    this.registerHelpers();
  }

  /**
   * Handlebars 헬퍼 등록
   */
  private registerHelpers(): void {
    // 동등성 비교 헬퍼
    Handlebars.registerHelper('eq', function(a: any, b: any) {
      return a === b;
    });

    // 논리 OR 헬퍼
    Handlebars.registerHelper('or', function(...args: any[]) {
      // 마지막 인자는 Handlebars options 객체이므로 제외
      const values = args.slice(0, -1);
      return values.some(v => v);
    });

    // XML 주석용 텍스트 변환 (마크다운 제거)
    Handlebars.registerHelper('xmlSafe', function(text: string) {
      if (!text) return '';
      // 마크다운 리스트 제거 (- item -> item)
      let cleaned = text.replace(/^[\s-]*-\s+/gm, '');
      // 백틱 코드 제거 (`code` -> code)
      cleaned = cleaned.replace(/`([^`]+)`/g, '$1');
      // 줄바꿈을 공백으로 변환
      cleaned = cleaned.replace(/\n/g, ' ');
      // 연속된 공백을 하나로
      cleaned = cleaned.replace(/\s+/g, ' ');
      // 앞뒤 공백 제거
      cleaned = cleaned.trim();
      // XML 특수 문자 이스케이프 (C# XML 주석에서는 ', "는 이스케이프 불필요)
      cleaned = cleaned
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
      return new Handlebars.SafeString(cleaned);
    });

    // 배열 타입 체크 헬퍼
    Handlebars.registerHelper('isArray', function(typeName: string) {
      return typeName && typeName.endsWith('[]');
    });

    // 배열의 요소 타입 추출
    Handlebars.registerHelper('arrayElementType', function(typeName: string) {
      if (typeName && typeName.endsWith('[]')) {
        return typeName.slice(0, -2);
      }
      return typeName;
    });
  }

  /**
   * 템플릿 로드
   */
  private async loadTemplates(): Promise<void> {
    if (this.apiTemplate && this.classTemplate && this.coreTemplate && this.partialApiTemplate && this.mainTemplate && this.categoryPartialTemplate) return;

    const templatesDir = path.join(__dirname, '../templates');
    const apiTemplatePath = path.join(templatesDir, 'csharp-api.hbs');
    const classTemplatePath = path.join(templatesDir, 'csharp-class.hbs');
    const coreTemplatePath = path.join(templatesDir, 'csharp-core.hbs');
    const partialApiTemplatePath = path.join(templatesDir, 'csharp-partial-api.hbs');
    const mainTemplatePath = path.join(templatesDir, 'csharp-main.hbs');
    const categoryPartialTemplatePath = path.join(templatesDir, 'csharp-category-partial.hbs');

    const apiTemplateSource = await fs.readFile(apiTemplatePath, 'utf-8');
    const classTemplateSource = await fs.readFile(classTemplatePath, 'utf-8');
    const coreTemplateSource = await fs.readFile(coreTemplatePath, 'utf-8');
    const partialApiTemplateSource = await fs.readFile(partialApiTemplatePath, 'utf-8');
    const mainTemplateSource = await fs.readFile(mainTemplatePath, 'utf-8');
    const categoryPartialTemplateSource = await fs.readFile(categoryPartialTemplatePath, 'utf-8');

    this.apiTemplate = Handlebars.compile(apiTemplateSource);
    this.classTemplate = Handlebars.compile(classTemplateSource);
    this.coreTemplate = Handlebars.compile(coreTemplateSource);
    this.partialApiTemplate = Handlebars.compile(partialApiTemplateSource);
    this.mainTemplate = Handlebars.compile(mainTemplateSource);
    this.categoryPartialTemplate = Handlebars.compile(categoryPartialTemplateSource);
  }

  /**
   * 단일 API 생성
   */
  private generateAPI(api: ParsedAPI): string {
    if (!this.apiTemplate) {
      throw new Error('Templates not loaded');
    }

    // 파라미터 변환
    const parameters = api.parameters.map(param => {
      let paramType = mapToCSharpType(param.type);

      // 파라미터가 익명 객체(__type, object)인 경우 의미있는 이름 생성
      if ((paramType === '__type' || paramType === 'object') && param.type.kind === 'object' && param.type.properties && param.type.properties.length > 0) {
        // API 이름 + 파라미터 이름 기반 타입 이름 생성
        // 예: setSecureScreen(options) -> SetSecureScreenOptions
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
        // → Result 타입으로 처리
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
    };

    const generated = this.apiTemplate(data);
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

    if (!this.classTemplate) {
      throw new Error('Class template not loaded');
    }

    // 각 API를 문자열로 생성
    const generatedAPIs = apis.map(api => this.generateAPI(api));

    // 전체 클래스 생성
    const classCode = this.classTemplate({
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

    if (!this.classTemplate) {
      throw new Error('Class template not loaded');
    }

    const generatedAPIs = apis.map(api => this.generateAPI(api));

    const classCode = this.classTemplate({
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

    if (!this.coreTemplate) {
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
      const primitiveTypes = ['void', 'object', 'string', 'bool', 'double', 'int', 'float', 'long', '__type', 'System.Action'];
      if (!primitiveTypes.includes(callbackType) && !callbackType.startsWith('System.Action')) {
        // enum 타입과 일반 타입 분리
        if (enumTypeNames && enumTypeNames.has(callbackType)) {
          enumCallbackTypes.add(callbackType);
        } else {
          callbackTypes.add(callbackType);
        }
      }
    }

    return this.coreTemplate({
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

    if (!this.mainTemplate) {
      throw new Error('Main template not loaded');
    }

    return this.mainTemplate({
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

    if (!this.partialApiTemplate) {
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
      const isPrimitive = ['string', 'int', 'float', 'double', 'bool', 'long', 'short', 'byte'].includes(paramType);

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
        // → Result 타입으로 처리
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

    const generated = this.partialApiTemplate(data);
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

    if (!this.categoryPartialTemplate) {
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
      const apisData = categoryApis.map(api => this.prepareApiData(api));

      const content = this.categoryPartialTemplate({
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
      const apisData = categoryApis.map(api => this.prepareApiData(api));

      const content = this.categoryPartialTemplate({
        categoryName: category,
        apis: apisData,
      });

      const fileName = `AIT.${category}.cs`;
      files.set(fileName, content);
    }

    return files;
  }

  /**
   * API 데이터를 템플릿에서 사용할 형식으로 변환
   */
  private prepareApiData(api: ParsedAPI): any {
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
      const isPrimitive = ['string', 'int', 'float', 'double', 'bool', 'long', 'short', 'byte'].includes(paramType);

      return {
        paramName: this.escapeCSharpKeyword(param.name),
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

    // 콜백 타입 결정
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
        // → Result 타입으로 처리
        callbackType = `${api.pascalName}Result`;
      }
    }

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

    return {
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
      // 네임스페이스 API 지원
      namespace: api.namespace,
      isDeprecated: api.isDeprecated,
      deprecatedMessage,
      // 이벤트 구독 API 지원
      isEventSubscription: api.isEventSubscription,
      eventName: api.eventName,
      hasEventData,
      eventDataType,
    };
  }

  /**
   * C# 예약어를 안전한 변수명으로 변환
   */
  private escapeCSharpKeyword(name: string): string {
    const CSHARP_KEYWORDS = new Set([
      'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char',
      'checked', 'class', 'const', 'continue', 'decimal', 'default', 'delegate',
      'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false',
      'finally', 'fixed', 'float', 'for', 'foreach', 'goto', 'if', 'implicit',
      'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace',
      'new', 'null', 'object', 'operator', 'out', 'override', 'params', 'private',
      'protected', 'public', 'readonly', 'ref', 'return', 'sbyte', 'sealed',
      'short', 'sizeof', 'stackalloc', 'static', 'string', 'struct', 'switch',
      'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong', 'unchecked',
      'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while'
    ]);

    if (CSHARP_KEYWORDS.has(name)) {
      return `${name}Param`;
    }
    return name;
  }
}

/**
 * C# 타입 정의 생성기
 */
export class CSharpTypeGenerator {
  private unionResultTemplate?: HandlebarsTemplateDelegate;

  // Inline string literal union을 enum으로 변환할 때 수집
  // key: enum 이름 (예: SetDeviceOrientationOptionsType)
  // value: enum 값들 (예: ["portrait", "landscape"])
  private inlineEnums: Map<string, string[]> = new Map();
  // 익명 배열 요소 타입을 추적 (propertyPath -> generatedClassName)
  private inlineArrayElementTypes: Map<string, string> = new Map();
  // 프로퍼티가 없지만 참조된 외부 타입을 추적 (나중에 type definitions에서 해결)
  private pendingExternalTypes: Set<string> = new Set();

  constructor() {
    // Constructor for future template loading if needed
  }

  /**
   * inline string literal union인지 확인
   * Case 1: kind가 'union'이고 모든 unionTypes가 string literal
   * Case 2: kind가 'primitive'이지만 raw가 "value1" | "value2" 형태
   */
  private isInlineStringLiteralUnion(type: ParsedType): boolean {
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
  private extractEnumValues(type: ParsedType): string[] {
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
   * Union Result 템플릿 로드
   */
  private async loadUnionResultTemplate(): Promise<void> {
    if (this.unionResultTemplate) return;

    const templatesDir = path.join(__dirname, '../templates');
    const templatePath = path.join(templatesDir, 'csharp-union-result.hbs');
    const templateSource = await fs.readFile(templatePath, 'utf-8');
    this.unionResultTemplate = Handlebars.compile(templateSource);
  }

  /**
   * Discriminated Union Result 클래스 생성
   */
  private async generateUnionResult(
    apiName: string,
    successType: ParsedType,
    errorCodes: string[]
  ): Promise<string> {
    await this.loadUnionResultTemplate();

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
    });
  }

  /**
   * XML 주석용 텍스트 변환 (마크다운 제거)
   */
  private xmlSafe(text: string): string {
    if (!text) return '';
    // 마크다운 리스트 제거 (- item -> item)
    let cleaned = text.replace(/^[\s-]*-\s+/gm, '');
    // 백틱 코드 제거 (`code` -> code)
    cleaned = cleaned.replace(/`([^`]+)`/g, '$1');
    // 줄바꿈을 공백으로 변환
    cleaned = cleaned.replace(/\n/g, ' ');
    // 연속된 공백을 하나로
    cleaned = cleaned.replace(/\s+/g, ' ');
    // 앞뒤 공백 제거
    cleaned = cleaned.trim();
    // XML 특수 문자 이스케이프 (C# XML 주석에서는 ', "는 이스케이프 불필요)
    cleaned = cleaned
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
    return cleaned;
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

    // 중첩 타입 수집
    const nestedTypes: string[] = [];

    const fields = typeDef.properties
      .map(prop => {
        let type = mapToCSharpType(prop.type);

        // 중첩 익명 객체 처리
        if (prop.type.kind === 'object' &&
            prop.type.properties &&
            prop.type.properties.length > 0 &&
            (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
          // 중첩 클래스 이름 생성
          const nestedTypeName = `${typeDef.name}${this.capitalize(prop.name)}`;
          type = nestedTypeName;

          // 중첩 클래스 생성
          const nestedFields = prop.type.properties
            .map((nestedProp: any) => {
              const nestedType = mapToCSharpType(nestedProp.type);
              return this.generateFieldDeclaration(nestedProp.name, nestedType, nestedProp.optional);
            })
            .join('\n');

          nestedTypes.push(`    [Serializable]\n    [Preserve]\n    public class ${nestedTypeName}\n    {\n${nestedFields}\n    }`);
        }

        const description = prop.description
          ? `        /// <summary>${this.xmlSafe(prop.description)}</summary>\n`
          : '';
        return `${description}${this.generateFieldDeclaration(prop.name, type, prop.optional)}`;
      })
      .join('\n');

    const description = typeDef.description
      ? `    /// <summary>\n    /// ${typeDef.description}\n    /// </summary>\n`
      : '';

    const mainClass = `${description}    [Serializable]\n    [Preserve]\n    public class ${typeDef.name}\n    {\n${fields}\n    }`;

    // 중첩 타입들을 먼저 출력
    if (nestedTypes.length > 0) {
      return nestedTypes.join('\n\n') + '\n\n' + mainClass;
    }

    return mainClass;
  }

  /**
   * 타입 정의들을 C# 코드로 생성 (헤더 없이, 본문만)
   */
  async generateTypeDefinitions(typeDefinitions: ParsedTypeDefinition[]): Promise<string> {
    const generatedTypes: string[] = [];

    for (const typeDef of typeDefinitions) {
      if (typeDef.kind === 'enum') {
        const enumCode = this.generateEnum(typeDef);
        if (enumCode) {
          generatedTypes.push(enumCode);
        }
      } else if (typeDef.kind === 'interface') {
        const classCode = this.generateInterfaceAsClass(typeDef);
        if (classCode) {
          generatedTypes.push(classCode);
        }
      }
    }

    if (generatedTypes.length === 0) {
      return '';
    }

    // 헤더/푸터 없이 본문만 반환 (호출자가 합침)
    return generatedTypes.join('\n\n');
  }

  /**
   * API에서 사용되는 모든 타입 정의 생성
   */
  async generateTypes(
    apis: ParsedAPI[],
    excludeTypeNames?: Set<string>,
    typeDefinitions?: ParsedTypeDefinition[],
    parser?: { parseNativeModulesType: (typeName: string) => ParsedTypeDefinition | null }
  ): Promise<string> {
    const typeMap = new Map<string, string>(); // typeName -> classDefinition
    const unionResultMap = new Map<string, string>(); // API name -> Union Result class
    const exclude = excludeTypeNames || new Set<string>();

    // Inline enum Map 초기화 (generateClassType에서 채워짐)
    this.inlineEnums.clear();
    // Inline array element types Map 초기화
    this.inlineArrayElementTypes.clear();
    // Pending external types 초기화
    this.pendingExternalTypes.clear();

    // API에서 사용되는 모든 타입 수집
    for (const api of apis) {
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
                const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
                typeName = `${capitalize(api.name)}${capitalize(param.name)}`;
              }
            } else {
              const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
              typeName = `${capitalize(api.name)}${capitalize(param.name)}`;
            }
          }

          // cleanName을 키로 사용 (중복 방지)
          const cleanName = this.extractCleanName(typeName);
          if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
            typeMap.set(cleanName, this.generateClassType(typeName, param.type.properties));
          }

          // 프로퍼티에서 참조되는 named type도 수집 (재귀)
          this.collectReferencedTypes(param.type.properties, typeMap, exclude, cleanName);

          // 객체의 프로퍼티에 함수 타입이 있으면 함수 파라미터 타입도 수집
          for (const prop of param.type.properties) {
            if (prop.type.kind === 'function' && prop.type.functionParams) {
              for (const funcParam of prop.type.functionParams) {
                this.collectFunctionParamTypes(funcParam, typeMap, exclude);
              }
            }
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
            const unionTypeName = this.extractCleanName(innerType.name);

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
              this.collectReferencedTypes(Array.from(allProperties.values()), typeMap, exclude, unionTypeName);
            }
          } else {
            // 익명 union: 각 멤버를 별도 클래스로
            for (const unionMember of innerType.unionTypes) {
              if (unionMember.kind === 'object' && unionMember.properties && unionMember.properties.length > 0) {
                let typeName = unionMember.name;
                if (typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName) {
                  const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
                  typeName = `${capitalize(api.name)}Result`;
                }

                const cleanName = this.extractCleanName(typeName);
                if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
                  // 반환 타입이므로 isResultType: true - error 필드 추가
                  typeMap.set(cleanName, this.generateClassType(typeName, unionMember.properties, true));
                  // 프로퍼티에서 참조되는 named type도 수집 (재귀)
                  this.collectReferencedTypes(unionMember.properties, typeMap, exclude, cleanName);
                }
              }
            }
          }
        }
        // 일반 객체
        else if (innerType.kind === 'object' && innerType.properties && innerType.properties.length > 0) {
          let typeName = innerType.name;
          if (typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName) {
            const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
            typeName = `${capitalize(api.name)}Result`;
          }

          const cleanName = this.extractCleanName(typeName);
          if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
            // 반환 타입이므로 isResultType: true - error 필드 추가
            typeMap.set(cleanName, this.generateClassType(typeName, innerType.properties, true));
            // 프로퍼티에서 참조되는 named type도 수집 (재귀)
            this.collectReferencedTypes(innerType.properties, typeMap, exclude, cleanName);
          }
        }
      }
      // 동기 함수의 반환 타입
      else if (api.returnType.kind === 'object' && api.returnType.properties && api.returnType.properties.length > 0) {
        let typeName = api.returnType.name;
        if (typeName === '__type' || typeName === 'object' || typeName.startsWith('{') || !typeName) {
          const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
          typeName = `${capitalize(api.name)}Result`;
        }

        const cleanName = this.extractCleanName(typeName);
        if (!typeMap.has(cleanName) && !exclude.has(cleanName)) {
          // 반환 타입이므로 isResultType: true - error 필드 추가
          typeMap.set(cleanName, this.generateClassType(typeName, api.returnType.properties, true));
          // 프로퍼티에서 참조되는 named type도 수집 (재귀)
          this.collectReferencedTypes(api.returnType.properties, typeMap, exclude, cleanName);
        }
      }
    }

    // Pending external types 해결: typeDefinitions와 native-modules에서 찾아서 클래스 생성
    if (this.pendingExternalTypes.size > 0) {
      // 재귀적으로 추가된 pending types 해결 (최대 10번 반복하여 무한 루프 방지)
      let iteration = 0;
      while (this.pendingExternalTypes.size > 0 && iteration < 10) {
        const remainingTypes = new Set(this.pendingExternalTypes);
        this.pendingExternalTypes.clear();

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
            this.collectReferencedTypes(typeDef.properties, typeMap, exclude, typeName);
          }
        }
        iteration++;
      }
    }

    // Inline string literal union에서 생성된 enum 코드
    const inlineEnumTypes: string[] = [];
    for (const [enumName, enumValues] of this.inlineEnums) {
      const enumCode = this.generateEnum({
        name: enumName,
        kind: 'enum',
        file: '',
        enumValues: enumValues,
      });
      if (enumCode) {
        inlineEnumTypes.push(enumCode);
      }
    }

    // Union Result 클래스와 일반 타입 클래스 합치기
    const allTypes = [
      ...inlineEnumTypes,  // inline enum이 먼저 (타입 참조 순서)
      ...Array.from(unionResultMap.values()),
      ...Array.from(typeMap.values())
    ];

    // 헤더/푸터 없이 본문만 반환 (호출자가 합침)
    return allTypes.join('\n\n');
  }

  /**
   * 타입 이름에서 cleanName 추출
   */
  private extractCleanName(name: string): string {
    let cleanName = name;

    // import 경로에서 타입 이름 추출
    if (name.includes('.')) {
      cleanName = name.split('.').pop() || name;
    }

    // Union 타입 (A | B | C)에서 첫 번째 타입만 사용
    if (cleanName.includes('|')) {
      cleanName = cleanName.split('|')[0].trim();
    }

    // 특수 문자 제거 (큰따옴표, 작은따옴표, 중괄호, 괄호, 쉼표, 파이프, C# 식별자로 유효하지 않은 문자)
    cleanName = cleanName.replace(/["'{}(),|$<>]/g, '').trim();

    // C# 식별자로 유효하지 않은 문자 제거 (공백, 하이픈 등)
    cleanName = cleanName.replace(/[\s\-]+/g, '');

    return cleanName;
  }

  /**
   * 중첩 타입을 수집하여 반환
   */
  private collectNestedTypes(
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
        const nestedTypeName = `${parentName}${this.capitalize(prop.name)}`;
        if (!nestedTypes.has(nestedTypeName)) {
          // 재귀적으로 중첩 타입 수집
          this.collectNestedTypes(nestedTypeName, prop.type.properties, nestedTypes);
          // 중첩 클래스 생성
          nestedTypes.set(nestedTypeName, this.generateNestedClassType(nestedTypeName, prop.type.properties, nestedTypes));
        }
      }
    }
  }

  /**
   * 중첩 클래스 타입 생성 (내부 헬퍼)
   */
  private generateNestedClassType(name: string, properties: any[], nestedTypes: Map<string, string>): string {
    const fields = properties
      .map(prop => {
        let type = mapToCSharpType(prop.type);
        // 중첩 익명 객체는 생성된 클래스 이름 사용
        if (prop.type.kind === 'object' &&
            prop.type.properties &&
            prop.type.properties.length > 0 &&
            (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
          type = `${name}${this.capitalize(prop.name)}`;
        }
        const description = prop.description
          ? `        /// <summary>${this.xmlSafe(prop.description)}</summary>\n`
          : '';
        return `${description}${this.generateFieldDeclaration(prop.name, type, prop.optional)}`;
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
   * 클래스 타입 생성
   */
  private generateClassType(name: string, properties: any[], isResultType: boolean = false): string {
    // extractCleanName을 사용하여 정리된 이름 얻기
    const cleanName = this.extractCleanName(name);

    // 중첩 타입 수집
    const nestedTypes = new Map<string, string>();
    this.collectNestedTypes(cleanName, properties, nestedTypes);

    const fields = properties
      .map(prop => {
        let type = mapToCSharpType(prop.type);

        // Inline string literal union → enum 변환
        if (this.isInlineStringLiteralUnion(prop.type)) {
          const enumName = `${cleanName}${this.capitalize(prop.name)}`;
          const enumValues = this.extractEnumValues(prop.type);
          // enum 수집 (중복 방지)
          if (!this.inlineEnums.has(enumName)) {
            this.inlineEnums.set(enumName, enumValues);
          }
          type = enumName;
        }
        // 중첩 익명 객체는 생성된 클래스 이름 사용
        else if (prop.type.kind === 'object' &&
            prop.type.properties &&
            prop.type.properties.length > 0 &&
            (prop.type.name === '__type' || prop.type.name === 'object' || prop.type.name.startsWith('{'))) {
          type = `${cleanName}${this.capitalize(prop.name)}`;
        }
        // 익명 객체 배열은 생성된 클래스 이름 + [] 사용
        else if (prop.type.kind === 'array' &&
            prop.type.elementType &&
            prop.type.elementType.kind === 'object' &&
            prop.type.elementType.properties &&
            prop.type.elementType.properties.length > 0 &&
            (prop.type.elementType.name === '__type' || prop.type.elementType.name === 'object' || prop.type.elementType.name.startsWith('{'))) {
          const propNameSingular = prop.name.endsWith('s') ? prop.name.slice(0, -1) : prop.name;
          type = `${cleanName}${this.capitalize(propNameSingular)}[]`;
        }
        const description = prop.description
          ? `        /// <summary>${this.xmlSafe(prop.description)}</summary>\n`
          : '';
        return `${description}${this.generateFieldDeclaration(prop.name, type, prop.optional)}`;
      })
      .join('\n');

    // Result 타입이면 error 필드 추가 (jslib 에러 응답 수신용)
    const errorField = isResultType
      ? '\n        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>\n        public string error;'
      : '';

    // 중첩 타입들을 먼저 출력하고, 메인 클래스 출력
    const nestedTypesCode = Array.from(nestedTypes.values()).join('\n\n');
    const mainClass = `    [Serializable]
    [Preserve]
    public class ${cleanName}
    {
${fields}${errorField}
    }`;

    return nestedTypesCode ? `${nestedTypesCode}\n\n${mainClass}` : mainClass;
  }

  private capitalize(str: string): string {
    return str.charAt(0).toUpperCase() + str.slice(1);
  }

  /**
   * 필드 선언을 생성 (JsonProperty 어트리뷰트 포함)
   * C# 필드명은 PascalCase, JSON 직렬화는 원본 camelCase 사용
   * System.Action 타입은 직렬화할 수 없으므로 JsonIgnore 추가
   */
  private generateFieldDeclaration(originalName: string, type: string, optional: boolean = false): string {
    const pascalName = this.capitalize(originalName);
    const optionalComment = optional ? ' // optional' : '';

    // System.Action 타입은 직렬화 불가능하므로 JsonIgnore 사용
    if (type.startsWith('System.Action')) {
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
   * 프로퍼티에서 참조되는 named type을 재귀적으로 수집
   */
  private collectReferencedTypes(
    properties: any[],
    typeMap: Map<string, string>,
    exclude: Set<string>,
    parentTypeName?: string
  ): void {
    for (const prop of properties) {
      // named object 타입 (익명이 아닌 경우)
      if (prop.type.kind === 'object' &&
          prop.type.name !== '__type' &&
          prop.type.name !== 'object' &&
          !prop.type.name.startsWith('{')) {
        const typeName = this.extractCleanName(prop.type.name);
        if (!typeMap.has(typeName) && !exclude.has(typeName)) {
          // 프로퍼티가 있으면 바로 클래스 생성
          if (prop.type.properties && prop.type.properties.length > 0) {
            typeMap.set(typeName, this.generateClassType(typeName, prop.type.properties));
            // 재귀적으로 해당 타입의 프로퍼티도 수집
            this.collectReferencedTypes(prop.type.properties, typeMap, exclude, typeName);
          } else {
            // 프로퍼티가 없는 named type은 pendingExternalTypes에 추가
            // (나중에 type definitions에서 해결)
            this.pendingExternalTypes.add(typeName);
          }
        }
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
            typeName = this.extractCleanName(elementType.name);
          } else {
            // 익명 객체 배열 요소 타입: 부모 컨텍스트에서 이름 생성
            // 예: IAPGetPendingOrdersResult의 orders 필드 -> IAPGetPendingOrdersResultOrder
            const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
            const propNameSingular = prop.name.endsWith('s') ? prop.name.slice(0, -1) : prop.name;
            typeName = parentTypeName
              ? `${parentTypeName}${capitalize(propNameSingular)}`
              : `${capitalize(propNameSingular)}Item`;

            // 익명 타입 매핑 저장 (나중에 타입 변환 시 사용)
            const propPath = parentTypeName ? `${parentTypeName}.${prop.name}` : prop.name;
            this.inlineArrayElementTypes.set(propPath, typeName);
          }

          if (!typeMap.has(typeName) && !exclude.has(typeName)) {
            typeMap.set(typeName, this.generateClassType(typeName, elementType.properties));
            // 재귀적으로 해당 타입의 프로퍼티도 수집
            this.collectReferencedTypes(elementType.properties, typeMap, exclude, typeName);
          }
        }
      }
      // 함수 타입의 파라미터
      else if (prop.type.kind === 'function' && prop.type.functionParams) {
        for (const funcParam of prop.type.functionParams) {
          this.collectFunctionParamTypes(funcParam, typeMap, exclude);
        }
      }
    }
  }

  /**
   * 함수 파라미터 타입을 재귀적으로 수집
   */
  private collectFunctionParamTypes(
    paramType: ParsedType,
    typeMap: Map<string, string>,
    exclude: Set<string>
  ): void {
    // Union 타입 처리 (ContactsViralEvent = RewardFromContactsViralEvent | ContactsViralSuccessEvent)
    if (paramType.kind === 'union') {
      // Union 타입 자체가 named type이면 클래스로 생성
      if (paramType.name && paramType.name.includes('.')) {
        const unionTypeName = this.extractCleanName(paramType.name);
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
            typeMap.set(unionTypeName, this.generateClassType(unionTypeName, Array.from(allProperties.values())));
          }
        }
      }
      // Union 멤버들도 재귀 처리
      if (paramType.unionTypes) {
        for (const member of paramType.unionTypes) {
          this.collectFunctionParamTypes(member, typeMap, exclude);
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
        const typeName = extractedTypeName || this.extractCleanName(paramType.name);

        if (!typeMap.has(typeName) && !exclude.has(typeName)) {
          if (paramType.properties && paramType.properties.length > 0) {
            // 인라인 프로퍼티가 있으면 클래스 생성
            typeMap.set(typeName, this.generateClassType(typeName, paramType.properties));
            this.collectReferencedTypes(paramType.properties, typeMap, exclude, typeName);
          } else {
            // 프로퍼티가 없는 named type은 pendingExternalTypes에 추가
            this.pendingExternalTypes.add(typeName);
          }
        }
      }
      // Anonymous type - still collect referenced types if it has properties
      else if (paramType.properties && paramType.properties.length > 0) {
        this.collectReferencedTypes(paramType.properties, typeMap, exclude, undefined);
      }
    }
  }
}
