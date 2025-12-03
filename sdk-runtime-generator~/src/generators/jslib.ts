import Handlebars from 'handlebars';
import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { ParsedAPI, ParsedType } from '../types.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * jslib 생성기
 *
 * 카테고리는 파서에서 .d.ts 파일명 기반으로 추출되므로
 * 항상 영어 파일명을 사용합니다.
 */
export class JSLibGenerator {
  private functionTemplate?: HandlebarsTemplateDelegate;
  private fileTemplate?: HandlebarsTemplateDelegate;

  constructor() {
    this.registerHelpers();
  }

  /**
   * Handlebars 헬퍼 등록
   */
  private registerHelpers(): void {
    // JavaScript 타입 변환 헬퍼
    Handlebars.registerHelper('jsConversion', function (this: any) {
      const paramName = this.name;
      const paramType = this.type;

      // C# → JavaScript 타입 변환
      switch (paramType.kind) {
        case 'primitive':
          if (paramType.name === 'string') {
            return `UTF8ToString(${paramName})`;
          }
          return paramName;

        case 'object':
          return `JSON.parse(UTF8ToString(${paramName}))`;

        case 'array':
          return `JSON.parse(UTF8ToString(${paramName}))`;

        default:
          return paramName;
      }
    });

    // 기본 반환값 헬퍼
    Handlebars.registerHelper('defaultReturnValue', function (returnType: ParsedType) {
      switch (returnType.kind) {
        case 'primitive':
          if (returnType.name === 'boolean') return 'false';
          if (returnType.name === 'number') return '0';
          if (returnType.name === 'string') return '""';
          return 'null';
        default:
          return 'null';
      }
    });
  }

  /**
   * 템플릿 로드
   */
  private async loadTemplates(): Promise<void> {
    if (this.functionTemplate && this.fileTemplate) return;

    const templatesDir = path.join(__dirname, '../templates');
    const functionTemplatePath = path.join(templatesDir, 'jslib-function.hbs');
    const fileTemplatePath = path.join(templatesDir, 'jslib-file.hbs');

    const functionTemplateSource = await fs.readFile(functionTemplatePath, 'utf-8');
    const fileTemplateSource = await fs.readFile(fileTemplatePath, 'utf-8');

    this.functionTemplate = Handlebars.compile(functionTemplateSource);
    this.fileTemplate = Handlebars.compile(fileTemplateSource);
  }

  /**
   * 단일 API 함수 생성
   */
  private generateFunction(api: ParsedAPI): string {
    if (!this.functionTemplate) {
      throw new Error('Templates not loaded');
    }

    // 파라미터 변환
    const parameters = api.parameters.map(param => ({
      name: param.name,
      type: param.type,
      jsConversion: this.getJSConversion(param.name, param.type),
    }));

    // web-framework 네임스페이스 추출
    const webFrameworkNamespace = this.extractNamespace(api.category);

    // Discriminated Union 여부 확인
    const isDiscriminatedUnion = api.returnType.kind === 'promise' &&
                                  api.returnType.promiseType?.isDiscriminatedUnion === true;

    const data = {
      name: api.name,
      originalName: api.originalName, // Use the original TypeScript name from parser
      parameters,
      hasCallback: true, // 동기/비동기 모두 Unity로 콜백 필요
      isAsync: api.isAsync,
      hasWebFrameworkImport: true,
      webFrameworkNamespace,
      defaultReturnValue: this.getDefaultReturnValue(api.returnType),
      isDiscriminatedUnion,
    };

    return this.functionTemplate(data);
  }

  /**
   * JavaScript 타입 변환 코드 생성
   */
  private getJSConversion(paramName: string, paramType: ParsedType): string {
    switch (paramType.kind) {
      case 'primitive':
        if (paramType.name === 'string') {
          return `UTF8ToString(${paramName})`;
        }
        return paramName;

      case 'object':
      case 'array':
        return `JSON.parse(UTF8ToString(${paramName}))`;

      default:
        return paramName;
    }
  }

  /**
   * 기본 반환값 생성
   */
  private getDefaultReturnValue(returnType: ParsedType): string {
    switch (returnType.kind) {
      case 'primitive':
        if (returnType.name === 'boolean') return 'false';
        if (returnType.name === 'number') return '0';
        if (returnType.name === 'string') return '""';
        return 'null';
      default:
        return 'null';
    }
  }

  /**
   * 카테고리에서 네임스페이스 추출
   *
   * 주의: 더 이상 사용되지 않음
   * jslib은 이제 @apps-in-toss/bridge-core 패턴을 직접 사용하여
   * window.__CONSTANT_HANDLER_MAP, window.ReactNativeWebView,
   * window.__GRANITE_NATIVE_EMITTER를 직접 호출합니다.
   *
   * @deprecated window.AppsInToss 네임스페이스는 더 이상 사용되지 않습니다.
   */
  private extractNamespace(category: string): string {
    // 이제 사용되지 않음 - bridge-core 패턴으로 직접 호출
    return 'window';
  }

  /**
   * 카테고리별 jslib 파일 생성
   */
  async generate(
    apis: ParsedAPI[],
    webFrameworkTag: string
  ): Promise<Map<string, string>> {
    await this.loadTemplates();

    if (!this.fileTemplate) {
      throw new Error('File template not loaded');
    }

    // 카테고리별로 API 그룹화
    const apisByCategory = new Map<string, ParsedAPI[]>();
    for (const api of apis) {
      const category = api.category;
      if (!apisByCategory.has(category)) {
        apisByCategory.set(category, []);
      }
      apisByCategory.get(category)!.push(api);
    }

    // 카테고리별 jslib 파일 생성
    const jslibFiles = new Map<string, string>();

    for (const [category, categoryAPIs] of apisByCategory.entries()) {
      const functions = categoryAPIs.map(api => this.generateFunction(api));

      // 카테고리는 파서에서 .d.ts 파일명 기반으로 추출되어 이미 영어
      const fileName = `AppsInToss-${category}.jslib`;
      const fileContent = this.fileTemplate({
        fileName,
        webFrameworkTag,
        timestamp: new Date().toISOString(),
        category,
        functions,
      });

      jslibFiles.set(fileName, fileContent);
    }

    return jslibFiles;
  }
}
