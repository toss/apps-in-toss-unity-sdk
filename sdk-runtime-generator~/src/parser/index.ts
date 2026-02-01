/**
 * Parser module re-exports
 * 하위호환성을 위해 모든 public export를 유지
 */

// Main class
export { TypeScriptParser } from './TypeScriptParser.js';

// Type parser utilities
export {
  parseType,
  parseSimpleType,
  parseSimpleFunctionType,
  parseFrameworkSimpleType,
  parseTypeMembers,
} from './type-parser.js';

// API parsing
export {
  parseFunctionDeclaration,
  parseVariableFunction,
  parseSourceFile,
} from './api-parser.js';

// Namespace parsing
export {
  parseNamespaceObjects,
  parseNamespaceObject,
  detectCallbackBasedPattern,
  detectNestedCallbacks,
} from './namespace-parser.js';

// Event parsing
export {
  parseEventNamespace,
  parseEventTypeDefinition,
} from './event-parser.js';

// Framework parsing
export {
  parseFrameworkAPIs,
  parseFrameworkTypeDefinitions,
  parseNativeModulesType,
  findFrameworkPath,
} from './framework-parser.js';

// Type definition parsing
export { parseTypeDefinitionsFromFile } from './type-definition-parser.js';

// Detection utilities
export {
  detectEventNamespaces,
  detectGlobalFunctions,
  detectNamespaceObjects,
  getTypeAnnotationText,
  isDefinedInFile,
  isDeprecatedDeclaration,
} from './detection.js';

// JSDoc extraction
export {
  extractParamDescriptions,
  extractReturnsDescription,
  extractExamples,
  extractJsDocForProperty,
} from './jsdoc-extractor.js';

// Utilities
export {
  cleanTypeName,
  toPascalCase,
  getCategoryFromPath,
  getNamespaceCategory,
} from './utils.js';

// Constants
export { DOM_TYPES, NAMESPACE_CATEGORY_OVERRIDES } from './constants.js';
