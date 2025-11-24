import Handlebars from 'handlebars';
import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { ParsedAPI, ParsedParameter, GeneratedCode, ParsedTypeDefinition, ParsedType } from '../types.js';
import { mapToCSharpType } from '../validators/types.js';
import { CSharpFormatter } from '../formatters/csharp.js';

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
  }

  /**
   * 템플릿 로드
   */
  private async loadTemplates(): Promise<void> {
    if (this.apiTemplate && this.classTemplate && this.coreTemplate) return;

    const templatesDir = path.join(__dirname, '../templates');
    const apiTemplatePath = path.join(templatesDir, 'csharp-api.hbs');
    const classTemplatePath = path.join(templatesDir, 'csharp-class.hbs');
    const coreTemplatePath = path.join(templatesDir, 'csharp-core.hbs');

    const apiTemplateSource = await fs.readFile(apiTemplatePath, 'utf-8');
    const classTemplateSource = await fs.readFile(classTemplatePath, 'utf-8');
    const coreTemplateSource = await fs.readFile(coreTemplatePath, 'utf-8');

    this.apiTemplate = Handlebars.compile(apiTemplateSource);
    this.classTemplate = Handlebars.compile(classTemplateSource);
    this.coreTemplate = Handlebars.compile(coreTemplateSource);
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

      // 파라미터가 익명 객체(__type)인 경우 의미있는 이름 생성
      if (paramType === '__type' && param.type.kind === 'object' && param.type.properties && param.type.properties.length > 0) {
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

    // 반환 타입 변환
    const returnType = mapToCSharpType(api.returnType);

    // 콜백 사용 여부
    const hasCallback = api.isAsync;
    let callbackType =
      api.returnType.kind === 'promise' && api.returnType.promiseType
        ? mapToCSharpType(api.returnType.promiseType)
        : 'object';

    // Discriminated Union인 경우 Result 클래스 사용
    if (api.returnType.kind === 'promise' &&
        api.returnType.promiseType?.isDiscriminatedUnion) {
      callbackType = `${api.pascalName}Result`;
    }
    // 익명 객체 타입(__type, object 등)이면 더 구체적인 이름 생성
    else if (callbackType === '__type') {
      callbackType = `${api.pascalName}Result`;
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

    // 최종 클래스 파일에 포맷터 적용
    return CSharpFormatter.format(classCode);
  }

  /**
   * AITCore.cs 파일 생성 (인프라 코드)
   */
  async generateCoreFile(apis: ParsedAPI[]): Promise<string> {
    await this.loadTemplates();

    if (!this.coreTemplate) {
      throw new Error('Core template not loaded');
    }

    // 모든 비동기 API의 콜백 타입 수집
    const callbackTypes = new Set<string>();

    for (const api of apis) {
      if (api.isAsync && api.returnType.kind === 'promise' && api.returnType.promiseType) {
        const callbackType = mapToCSharpType(api.returnType.promiseType);

        // void, object 같은 primitive는 제외하고 실제 타입만 추가
        if (callbackType !== 'void' && callbackType !== 'object' && callbackType !== 'string') {
          callbackTypes.add(callbackType);
        }
      }
    }

    return this.coreTemplate({
      callbackTypes: Array.from(callbackTypes).sort(),
    });
  }
}

/**
 * C# 타입 정의 생성기
 */
export class CSharpTypeGenerator {
  private unionResultTemplate?: HandlebarsTemplateDelegate;

  constructor() {
    // Constructor for future template loading if needed
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
   * enum 정의 생성
   */
  private generateEnum(typeDef: ParsedTypeDefinition): string {
    if (!typeDef.enumValues || typeDef.enumValues.length === 0) {
      return '';
    }

    const enumMembers = typeDef.enumValues
      .map(value => {
        let memberName = value;

        // 숫자로 시작하는 경우 언더스코어 추가 (C# 식별자 규칙)
        if (/^\d/.test(memberName)) {
          memberName = `_${memberName}`;
        }

        // camelCase를 PascalCase로 변환
        const pascalValue = memberName.charAt(0).toUpperCase() + memberName.slice(1);
        return `        ${pascalValue}`;
      })
      .join(',\n');

    const description = typeDef.description
      ? `    /// <summary>\n    /// ${typeDef.description}\n    /// </summary>\n`
      : '';

    return `${description}    public enum ${typeDef.name}\n    {\n${enumMembers}\n    }`;
  }

  /**
   * interface를 class로 생성
   */
  private generateInterfaceAsClass(typeDef: ParsedTypeDefinition): string {
    if (!typeDef.properties || typeDef.properties.length === 0) {
      return '';
    }

    const fields = typeDef.properties
      .map(prop => {
        const type = mapToCSharpType(prop.type);
        const optional = prop.optional ? ' // optional' : '';
        const description = prop.description
          ? `        /// <summary>${prop.description}</summary>\n`
          : '';
        return `${description}        public ${type} ${this.capitalize(prop.name)};${optional}`;
      })
      .join('\n');

    const description = typeDef.description
      ? `    /// <summary>\n    /// ${typeDef.description}\n    /// </summary>\n`
      : '';

    return `${description}    [Serializable]\n    public class ${typeDef.name}\n    {\n${fields}\n    }`;
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
  async generateTypes(apis: ParsedAPI[]): Promise<string> {
    const typeMap = new Map<string, string>(); // typeName -> classDefinition
    const unionResultMap = new Map<string, string>(); // API name -> Union Result class

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
          // 익명 타입이면 의미있는 이름 생성
          let typeName = param.type.name;
          if (typeName === '__type' || typeName.startsWith('{')) {
            const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
            typeName = `${capitalize(api.name)}${capitalize(param.name)}`;
          }

          // cleanName을 키로 사용 (중복 방지)
          const cleanName = this.extractCleanName(typeName);
          if (!typeMap.has(cleanName)) {
            typeMap.set(cleanName, this.generateClassType(typeName, param.type.properties));
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

            if (allProperties.size > 0 && !typeMap.has(unionTypeName)) {
              typeMap.set(unionTypeName, this.generateClassType(
                innerType.name,
                Array.from(allProperties.values())
              ));
            }
          } else {
            // 익명 union: 각 멤버를 별도 클래스로
            for (const unionMember of innerType.unionTypes) {
              if (unionMember.kind === 'object' && unionMember.properties && unionMember.properties.length > 0) {
                let typeName = unionMember.name;
                if (typeName === '__type' || typeName.startsWith('{')) {
                  const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
                  typeName = `${capitalize(api.name)}Result`;
                }

                const cleanName = this.extractCleanName(typeName);
                if (!typeMap.has(cleanName)) {
                  typeMap.set(cleanName, this.generateClassType(typeName, unionMember.properties));
                }
              }
            }
          }
        }
        // 일반 객체
        else if (innerType.kind === 'object' && innerType.properties && innerType.properties.length > 0) {
          let typeName = innerType.name;
          if (typeName === '__type' || typeName.startsWith('{')) {
            const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
            typeName = `${capitalize(api.name)}Result`;
          }

          const cleanName = this.extractCleanName(typeName);
          if (!typeMap.has(cleanName)) {
            typeMap.set(cleanName, this.generateClassType(typeName, innerType.properties));
          }
        }
      }
      // 동기 함수의 반환 타입
      else if (api.returnType.kind === 'object' && api.returnType.properties && api.returnType.properties.length > 0) {
        let typeName = api.returnType.name;
        if (typeName === '__type' || typeName.startsWith('{')) {
          const capitalize = (str: string) => str.charAt(0).toUpperCase() + str.slice(1);
          typeName = `${capitalize(api.name)}Result`;
        }

        const cleanName = this.extractCleanName(typeName);
        if (!typeMap.has(cleanName)) {
          typeMap.set(cleanName, this.generateClassType(typeName, api.returnType.properties));
        }
      }
    }

    // Union Result 클래스와 일반 타입 클래스 합치기
    const allTypes = [
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

    // 특수 문자 제거 (큰따옴표, 작은따옴표, 중괄호, 괄호, 쉼표, 파이프)
    cleanName = cleanName.replace(/["'{}(),|]/g, '').trim();

    // C# 식별자로 유효하지 않은 문자 제거 (공백, 하이픈 등)
    cleanName = cleanName.replace(/[\s\-]+/g, '');

    return cleanName;
  }

  /**
   * 클래스 타입 생성
   */
  private generateClassType(name: string, properties: any[]): string {
    // extractCleanName을 사용하여 정리된 이름 얻기
    const cleanName = this.extractCleanName(name);

    const fields = properties
      .map(prop => {
        const type = mapToCSharpType(prop.type);
        const optional = prop.optional ? ' // optional' : '';
        return `        public ${type} ${this.capitalize(prop.name)};${optional}`;
      })
      .join('\n');

    return `    [Serializable]
    public class ${cleanName}
    {
${fields}
    }`;
  }

  private capitalize(str: string): string {
    return str.charAt(0).toUpperCase() + str.slice(1);
  }
}
