import Handlebars from 'handlebars';
import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { xmlSafe } from './utils.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 템플릿 디렉토리 경로 (csharp/ 디렉토리에서 상위로 올라가서 templates로)
const TEMPLATES_DIR = path.join(__dirname, '../../templates');

/**
 * 템플릿 캐시
 */
interface TemplateCache {
  apiTemplate?: HandlebarsTemplateDelegate;
  classTemplate?: HandlebarsTemplateDelegate;
  coreTemplate?: HandlebarsTemplateDelegate;
  partialApiTemplate?: HandlebarsTemplateDelegate;
  mainTemplate?: HandlebarsTemplateDelegate;
  categoryPartialTemplate?: HandlebarsTemplateDelegate;
  unionResultTemplate?: HandlebarsTemplateDelegate;
}

const templateCache: TemplateCache = {};

/**
 * Handlebars 헬퍼 등록 여부
 */
let helpersRegistered = false;

/**
 * Handlebars 헬퍼 등록
 */
export function registerHelpers(): void {
  if (helpersRegistered) return;

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
    return new Handlebars.SafeString(xmlSafe(text));
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

  // nullable 타입 체크 헬퍼 (string?, double? 등)
  Handlebars.registerHelper('isNullable', function(typeName: string) {
    return typeName && typeName.endsWith('?');
  });

  helpersRegistered = true;
}

/**
 * 템플릿 파일 로드 및 컴파일
 */
async function loadTemplate(templateName: string): Promise<HandlebarsTemplateDelegate> {
  const templatePath = path.join(TEMPLATES_DIR, `${templateName}.hbs`);
  const templateSource = await fs.readFile(templatePath, 'utf-8');
  return Handlebars.compile(templateSource);
}

/**
 * 모든 템플릿 로드
 */
export async function loadAllTemplates(): Promise<TemplateCache> {
  registerHelpers();

  if (!templateCache.apiTemplate) {
    templateCache.apiTemplate = await loadTemplate('csharp-api');
  }
  if (!templateCache.classTemplate) {
    templateCache.classTemplate = await loadTemplate('csharp-class');
  }
  if (!templateCache.coreTemplate) {
    templateCache.coreTemplate = await loadTemplate('csharp-core');
  }
  if (!templateCache.partialApiTemplate) {
    templateCache.partialApiTemplate = await loadTemplate('csharp-partial-api');
  }
  if (!templateCache.mainTemplate) {
    templateCache.mainTemplate = await loadTemplate('csharp-main');
  }
  if (!templateCache.categoryPartialTemplate) {
    templateCache.categoryPartialTemplate = await loadTemplate('csharp-category-partial');
  }

  return templateCache;
}

/**
 * Union Result 템플릿 로드
 */
export async function loadUnionResultTemplate(): Promise<HandlebarsTemplateDelegate> {
  registerHelpers();

  if (!templateCache.unionResultTemplate) {
    templateCache.unionResultTemplate = await loadTemplate('csharp-union-result');
  }
  return templateCache.unionResultTemplate;
}

/**
 * 템플릿 캐시 getter
 */
export function getTemplateCache(): TemplateCache {
  return templateCache;
}
