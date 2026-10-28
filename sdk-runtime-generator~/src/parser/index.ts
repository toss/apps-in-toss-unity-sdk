/**
 * Parser module re-exports
 */

// Main class
export { TypeScriptParser } from './TypeScriptParser.js';

// Bundle parsing
export { parseBundle, findDeprecationReplacement } from './bundle-parser.js';
export type { BundleExportRecord, BundleParseResult } from './bundle-parser.js';
export { detectCallbackBasedPattern, detectNestedCallbacks } from './callback-patterns.js';

// Type parser utilities
export { parseType, parseTypeMembers } from './type-parser.js';

// Event parsing
export { parseEventNamespace, parseEventTypeDefinition } from './event-parser.js';

// Type definition parsing
export { parseTypeDefinitionsFromFile } from './type-definition-parser.js';

// JSDoc extraction
export { extractParamDescriptions, extractReturnsDescription, extractExamples } from './jsdoc-extractor.js';

// Utilities
export { cleanTypeName, toPascalCase } from './utils.js';

// Constants
export { DOM_TYPES } from './constants.js';

// DOM violations collector
export { recordDomViolation, drainDomViolations, clearDomViolations } from './dom-violations.js';
export type { DomViolation } from './dom-violations.js';
