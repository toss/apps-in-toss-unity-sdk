import { CSHARP_KEYWORDS } from './constants.js';

/**
 * C# 예약어를 안전한 변수명으로 변환
 * @param name 변수명
 * @returns 안전한 변수명 (예약어인 경우 Param 접미사 추가)
 */
export function escapeCSharpKeyword(name: string): string {
  if (CSHARP_KEYWORDS.has(name)) {
    return `${name}Param`; // params -> paramsParam
  }
  return name;
}

/**
 * camelCase를 PascalCase로 변환
 * @param str 입력 문자열
 * @returns PascalCase 문자열
 */
export function toPascalCase(str: string): string {
  if (!str) return str;
  return str.charAt(0).toUpperCase() + str.slice(1);
}

/**
 * 첫 글자를 대문자로 변환 (toPascalCase의 별칭)
 */
export function capitalize(str: string): string {
  return toPascalCase(str);
}

/**
 * `[Obsolete("...")]` 문자열 리터럴에 넣을 수 있게 바꾼다.
 * 줄바꿈은 공백으로 합치고, 역슬래시를 먼저 이스케이프한 뒤 큰따옴표를 이스케이프한다.
 */
export function escapeObsoleteMessage(text: string): string {
  return text
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"');
}

/**
 * XML 주석용 텍스트 변환 (마크다운 제거)
 * @param text 입력 텍스트
 * @returns XML 안전한 텍스트
 */
export function xmlSafe(text: string): string {
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
 * 여러 줄 설명을 XML 주석 한 줄씩(`///` 접두 없이)으로 나눈다. 줄 구조는 유지하고
 * XML 특수 문자만 이스케이프한다. 번들 JSDoc은 여러 줄 설명이 많아서, 첫 줄만 `///`가
 * 붙고 나머지가 주석 밖으로 새면 컴파일이 깨진다.
 */
export function xmlSafeLines(text: string): string[] {
  if (!text) return [];
  const lines = text
    .split(/\r?\n/)
    .map(line => line.trimEnd().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'));
  while (lines.length > 0 && lines[0].trim() === '') lines.shift();
  while (lines.length > 0 && lines[lines.length - 1].trim() === '') lines.pop();
  return lines;
}

/** 타입(클래스/enum) 선언 위에 붙일 `<summary>` 블록. 설명이 없으면 빈 문자열 */
export function typeSummaryBlock(description: string | undefined, indent = '    '): string {
  const lines = xmlSafeLines(description ?? '');
  if (lines.length === 0) return '';
  const body = lines.map(line => `${indent}/// ${line}`.trimEnd()).join('\n');
  return `${indent}/// <summary>\n${body}\n${indent}/// </summary>\n`;
}

/**
 * 타입 이름에서 cleanName 추출
 * import 경로, union 타입, 특수 문자 등을 정리
 * @param name 타입 이름
 * @returns 정리된 타입 이름
 */
export function extractCleanName(name: string): string {
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
 * 콜백(함수 타입) 파라미터가 인라인 익명 객체 리터럴일 때 생성할 C# 클래스 이름.
 * 부모 타입 이름 + 콜백 프로퍼티 이름(PascalCase) + "Param" 규칙.
 * 세 소비처(type-collector, CSharpTypeGenerator, api-data-preparer)가 반드시 이 함수를
 * 공유해야 한다 (공식을 인라인 재현하면 drift로 CS0246 발생).
 * @param parentName 콜백을 소유한 부모 타입 이름
 * @param callbackName 콜백 프로퍼티 이름 (예: processProductGrant, onEvent, onNoFill)
 * @returns 생성할 파라미터 클래스 이름 (예: IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam)
 */
export function functionParamTypeName(parentName: string, callbackName: string): string {
  return `${extractCleanName(parentName)}${capitalize(callbackName)}Param`;
}
