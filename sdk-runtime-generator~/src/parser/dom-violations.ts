/**
 * DOM 전용 타입(HTMLElement 등)을 시그니처에 사용한 API에 대한 위반 정보.
 * generator 실행 중 수집되어 generate 종료 직전에 한 번에 보고된다.
 */
export interface DomViolation {
  functionName: string;
  category?: string;
  location: 'parameter' | 'return';
  paramName?: string;
  rawType: string;
  file: string;
}

const violations: DomViolation[] = [];

export function recordDomViolation(violation: DomViolation): void {
  violations.push(violation);
}

/**
 * 수집된 위반을 모두 반환하고 collector를 비운다.
 * generate 진입점에서 한 번 호출한다.
 */
export function drainDomViolations(): DomViolation[] {
  const out = violations.slice();
  violations.length = 0;
  return out;
}

/**
 * 테스트 셋업용. 매 케이스 시작에 호출해 격리한다.
 */
export function clearDomViolations(): void {
  violations.length = 0;
}
