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
