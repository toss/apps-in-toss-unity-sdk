/**
 * discovery.ts의 fs-only invariant 테스트
 *
 * node_modules 설치나 TypeScript 파싱 없이 package.json만 읽어 검증하는 가벼운
 * 테스트다. web-framework 3.0.1 changelog 누락 버그(alias 추가를 깜빡한 채 메인
 * 의존성 버전만 올림)의 재발을 즉시 잡기 위해 test:invariants에 포함해 PR CI에서
 * 항상 돈다.
 */

import * as fs from 'fs';
import { describe, test, expect } from 'vitest';
import {
  assertWebFrameworkAliasInvariant,
  discoverInstalledVersions,
  resolveVersionPaths,
} from '../../src/discovery.js';

describe('web-framework alias invariant', () => {
  test('devDependencies의 web-framework-X.Y.Z 최고 버전이 dependencies 버전과 일치해야 함', () => {
    expect(() => assertWebFrameworkAliasInvariant()).not.toThrow();
  });
});

describe('web-framework 3.x 번들 입력 invariant', () => {
  // 이 SDK는 web-framework 3.x의 dist/index.d.ts를 유일한 입력으로 쓴다.
  // 설치된 3.x alias 전부가 번들 index.d.ts를 갖고 있는지 확인한다 (없으면 resolveVersionPaths가 throw).
  const installed = discoverInstalledVersions();

  test('설치된 버전이 전부 3.x여야 함', () => {
    expect(installed.length).toBeGreaterThan(0);
    for (const v of installed) {
      expect(Number(v.split('.')[0])).toBeGreaterThanOrEqual(3);
    }
  });

  test.each(installed.map(v => ({ version: v })))(
    'v$version은 dist/index.d.ts 번들을 가져야 함',
    async ({ version }) => {
      const paths = await resolveVersionPaths(version);
      expect(fs.existsSync(paths.bundleFile)).toBe(true);
    },
  );
});
