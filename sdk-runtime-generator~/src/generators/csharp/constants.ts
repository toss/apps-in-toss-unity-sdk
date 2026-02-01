/**
 * C# 예약어 목록
 * C# 언어의 키워드로, 변수명이나 파라미터명으로 사용할 수 없음
 */
export const CSHARP_KEYWORDS = new Set([
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
 * WebGL DllImport에서 직접 전달 가능한 primitive 타입 목록
 */
export const PRIMITIVE_TYPES = ['string', 'int', 'float', 'double', 'bool', 'long', 'short', 'byte'];

/**
 * 콜백 타입 수집에서 제외할 primitive/특수 타입 목록
 */
export const EXCLUDED_CALLBACK_TYPES = ['void', 'object', 'string', 'bool', 'double', 'int', 'float', 'long', '__type', 'System.Action'];
