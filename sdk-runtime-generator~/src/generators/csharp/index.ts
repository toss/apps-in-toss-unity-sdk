/**
 * C# 코드 생성기 모듈
 *
 * 이 모듈은 TypeScript 정의에서 Unity C# SDK 코드를 생성합니다.
 *
 * @module generators/csharp
 */

// 메인 생성기 클래스
export { CSharpGenerator } from './CSharpGenerator.js';
export { CSharpTypeGenerator } from './CSharpTypeGenerator.js';

// 유틸리티 함수
export {
  escapeCSharpKeyword,
  toPascalCase,
  capitalize,
  xmlSafe,
  extractCleanName,
} from './utils.js';

// 상수
export {
  CSHARP_KEYWORDS,
  PRIMITIVE_TYPES,
  EXCLUDED_CALLBACK_TYPES,
} from './constants.js';

// 템플릿 관련
export {
  registerHelpers,
  loadAllTemplates,
  loadUnionResultTemplate,
  getTemplateCache,
} from './templates.js';

// API 데이터 준비
export {
  prepareApiData,
  prepareParameters,
  determineCallbackType,
  extractCallbackApiParameters,
  extractCallbackTypes,
  extractCallbackEventType,
  getNestedCallbackParamType,
  getNestedCallbackOptionsType,
  getNestedCallbackEventType,
} from './api-data-preparer.js';
export type { PreparedParameter, PreparedApiData } from './api-data-preparer.js';

// 타입 수집
export {
  InlineTypeTracker,
  isInlineStringLiteralUnion,
  extractEnumValues,
  generateFieldDeclaration,
  generateNestedClassType,
  collectNestedTypes,
  collectReferencedTypes,
  collectFunctionParamTypes,
  collectNestedTypesForTypeDefinition,
} from './type-collector.js';
