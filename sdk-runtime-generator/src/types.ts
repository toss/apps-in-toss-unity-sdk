/**
 * 파싱된 API 정보
 */
export interface ParsedAPI {
  name: string;
  pascalName: string; // C# PascalCase 이름 (예: AppLogin)
  originalName: string; // 원본 TypeScript 이름 (예: appLogin)
  category: string;
  file: string;
  description?: string;
  returnDescription?: string; // @returns 태그 설명
  examples?: string[]; // @example 태그 코드 예제들
  parameters: ParsedParameter[];
  returnType: ParsedType;
  isAsync: boolean;
  hasPermission: boolean;
}

/**
 * 파라미터 정보
 */
export interface ParsedParameter {
  name: string;
  type: ParsedType;
  optional: boolean;
  description?: string;
}

/**
 * 타입 정보
 */
export interface ParsedType {
  name: string;
  kind: 'primitive' | 'object' | 'array' | 'promise' | 'function' | 'union' | 'unknown';
  properties?: ParsedProperty[];
  elementType?: ParsedType;
  promiseType?: ParsedType;
  unionTypes?: ParsedType[];
  raw: string;
  // Discriminated Union 정보
  isDiscriminatedUnion?: boolean;
  successType?: ParsedType; // Union에서 객체 타입
  errorCodes?: string[]; // Union에서 문자열 리터럴들
}

/**
 * 객체 프로퍼티 정보
 */
export interface ParsedProperty {
  name: string;
  type: ParsedType;
  optional: boolean;
  description?: string;
}

/**
 * 파싱된 타입 정의 (enum, interface)
 */
export interface ParsedTypeDefinition {
  name: string;
  kind: 'enum' | 'interface';
  file: string;
  description?: string;
  // enum인 경우
  enumValues?: string[];
  // interface인 경우
  properties?: ParsedProperty[];
}

/**
 * 생성된 코드 정보
 */
export interface GeneratedCode {
  api: ParsedAPI;
  csharp: string;
  jslib: string;
}

/**
 * 검증 결과
 */
export interface ValidationResult {
  success: boolean;
  apiCount?: number;
  errors?: ValidationError[];
}

/**
 * 검증 에러
 */
export interface ValidationError {
  api?: string;
  type: 'missing' | 'type-unsupported' | 'syntax-error';
  message: string;
  suggestion?: string;
}
