/**
 * discovery.ts의 fs-only invariant 테스트
 *
 * node_modules 설치나 TypeScript 파싱 없이 package.json만 읽어 검증하는 가벼운
 * 테스트다. web-framework 3.0.1 changelog 누락 버그(alias 추가를 깜빡한 채 메인
 * 의존성 버전만 올림)의 재발을 즉시 잡기 위해 test:invariants에 포함해 PR CI에서
 * 항상 돈다.
 */

import { describe, test, expect } from 'vitest';
import { assertWebFrameworkAliasInvariant } from '../../src/discovery.js';

describe('web-framework alias invariant', () => {
  test('devDependencies의 web-framework-X.Y.Z 최고 버전이 dependencies 버전과 일치해야 함', () => {
    expect(() => assertWebFrameworkAliasInvariant()).not.toThrow();
  });
});
