/**
 * discovery.ts의 fs-only invariant 테스트
 *
 * node_modules 설치나 TypeScript 파싱 없이 package.json만 읽어 검증하는 가벼운
 * 테스트다. web-framework 3.0.1 changelog 누락 버그(alias 추가를 깜빡한 채 메인
 * 의존성 버전만 올림)의 재발을 즉시 잡기 위해 test:invariants에 포함해 PR CI에서
 * 항상 돈다.
 */

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

describe('web-framework 3.x self-bundle invariant', () => {
  // web-framework 3.x+는 별도 web-bridge sibling 패키지 없이 자체 dist/index.d.ts에
  // 전체 API 표면을 번들링한다(discovery.ts detectSelfContainedDts). 이 invariant는
  // 설치된 3.x+ alias 전부가 실제로 'self-bundle'로 판정되는지 검증한다 — 판정이
  // 깨지면(예: 4.x에서 번들 방식이 또 바뀜) resolveVersionPaths의 fail-loud 게이트가
  // pnpm-store 폴백에 대해 throw하겠지만, 이 테스트는 그 회귀를 changelog 생성을
  // 직접 돌리지 않고도 PR CI에서 더 빠르고 명시적으로 잡아낸다.
  const major3PlusVersions = discoverInstalledVersions().filter(v => Number(v.split('.')[0]) >= 3);

  test.each(major3PlusVersions.map(v => ({ version: v })))(
    'v$version은 self-bundle로 판정되어야 함',
    async ({ version }) => {
      const paths = await resolveVersionPaths(version);
      expect(paths.dtsSource).toBe('self-bundle');
    },
  );

  // 음성(negative) invariant: 2.x 이하는 self-bundle로 판정되면 안 된다.
  // 현재는 2.x web-bridge 패키지에 dist/index.d.ts 자체 번들 형태가 없어 우연히
  // 안전하지만(detectSelfContainedDts가 애초에 감지하지 못함), 그 우연에 기대지 않고
  // 명시적으로 고정한다 — 향후 2.x 배포 방식이 바뀌어도 self-bundle 오판정이면 여기서
  // 즉시 잡힌다.
  const majorBelow3Versions = discoverInstalledVersions().filter(v => Number(v.split('.')[0]) < 3);

  test.each(majorBelow3Versions.map(v => ({ version: v })))(
    'v$version은 self-bundle로 판정되면 안 됨',
    async ({ version }) => {
      const paths = await resolveVersionPaths(version);
      expect(paths.dtsSource).not.toBe('self-bundle');
    },
  );
});
