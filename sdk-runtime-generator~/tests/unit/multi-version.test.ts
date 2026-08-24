/**
 * 다중 버전 호환성 테스트
 *
 * pnpm alias로 설치된 모든 web-framework 버전에 대해
 * TypeScriptParser가 정상적으로 API를 파싱하는지 검증합니다.
 *
 * 새 버전 추가 시: package.json에 alias만 추가하면 자동으로 테스트 대상에 포함됩니다.
 * 예: "web-framework-1.14.0": "npm:@apps-in-toss/web-framework@1.14.0"
 */

import { describe, test, expect, beforeAll } from 'vitest';
import { FRAMEWORK_APIS, API_CATEGORIES } from '../../src/categories.js';
import type { ParsedAPI } from '../../src/types.js';
import { prepareParameters } from '../../src/generators/csharp/api-data-preparer.js';
import {
  discoverInstalledVersions,
  hasFrameworkApis,
  resolveVersionPaths,
  createParserForVersion,
  type VersionPaths,
} from '../../src/discovery.js';

// 버전 자동 감지·경로 해석은 src/discovery.ts로 통합됨 (changelog 파이프라인과 공유).

// =================================================================
// 테스트
// =================================================================

const installedVersions = discoverInstalledVersions();

describe('다중 버전 호환성 테스트', () => {
  test('테스트 대상 버전이 최소 1개 이상 설치되어 있어야 함', () => {
    expect(installedVersions.length).toBeGreaterThanOrEqual(1);
  });

  describe.each(installedVersions.map(v => ({ version: v })))('web-framework v$version', ({ version }) => {
    let paths: VersionPaths;
    let apis: ParsedAPI[] = [];

    beforeAll(async () => {
      paths = await resolveVersionPaths(version);
      const frameworkApiNames = hasFrameworkApis(version) ? FRAMEWORK_APIS : [];
      // changelog 파이프라인(generate-changelog.ts)과 동일한 옵션 — self-bundle(3.x+)
      // index.d.ts의 deprecated 최상위 함수(checkoutPayment 등)를 isDeprecated=true로
      // 보존하고, intersection/제네릭 wrapper 화살표 타입(getServerTime 등)도 감지해서
      // 파싱한다 (detection.ts detectGlobalFunctions 참고).
      const isSelfBundle = paths.dtsSource === 'self-bundle';
      apis = await createParserForVersion(paths).parseAPIs(frameworkApiNames, {
        includeDeprecatedGlobals: isSelfBundle,
        includeWrappedCallables: isSelfBundle,
      });
    });

    test('TypeScript 정의 파일 디렉토리를 찾을 수 있어야 함', () => {
      expect(paths.dtsDir).toBeTruthy();
    });

    test('최소 1개 이상 API가 파싱되어야 함', () => {
      expect(apis.length).toBeGreaterThanOrEqual(1);
    });

    test('모든 API에 name과 category가 있어야 함', () => {
      for (const api of apis) {
        expect(api.name).toBeTruthy();
        expect(api.category).toBeTruthy();
      }
    });

    test('핵심 API가 포함되어야 함', () => {
      const apiNames = new Set(apis.map(a => a.name));
      const major = Number(version.split('.')[0]);

      // appLogin은 3.x에서도 플랫 함수로 그대로 남아있음 (실측)
      expect(apiNames.has('appLogin')).toBe(true);

      if (major < 3) {
        // 2.x 이하: getDeviceId/checkoutPayment가 플랫 함수로 존재
        expect(apiNames.has('getDeviceId')).toBe(true);
        expect(apiNames.has('checkoutPayment')).toBe(true);
      } else {
        // 3.x+: web-framework 자체 dist/index.d.ts(discovery.ts detectSelfContainedDts가
        // 'self-bundle'로 판정)는 getDeviceId/checkoutPayment를 여전히 최상위 export로
        // 유지하되 @deprecated JSDoc만 붙인다(실측: 3.0.1 index.d.ts). self-bundle 경로에서
        // 파서는 이런 deprecated 플랫 함수를 제거하지 않고 isDeprecated=true로 보존한다
        // (detectGlobalFunctions의 includeDeprecatedGlobals 옵션) — 그래서 getDeviceId/
        // checkoutPayment 둘 다 여전히 파싱 결과에 존재한다.
        expect(apiNames.has('getDeviceId')).toBe(true);
        expect(apiNames.has('checkoutPayment')).toBe(true);
        const flatCheckoutPayment = apis.find(a => a.name === 'checkoutPayment');
        expect(flatCheckoutPayment?.isDeprecated).toBe(true);

        // TossPay 네임스페이스 객체는 checkoutPayment를 `typeof checkoutPayment`로
        // 그대로 재노출하며, 이 재노출 지점(TossPay.checkoutPayment)에도 별도로
        // `@deprecated TossPay.authorize를 사용해주세요` JSDoc이 붙어 있다(실측).
        // 즉 TossPayCheckoutPayment는 대체 API가 아니라 checkoutPayment와 동일하게
        // deprecated된 별칭이며, 실제 대체 API는 TossPayAuthorize다.
        expect(apiNames.has('TossPayCheckoutPayment')).toBe(true);
        const namespacedCheckoutPayment = apis.find(a => a.name === 'TossPayCheckoutPayment');
        expect(namespacedCheckoutPayment?.isDeprecated).toBe(true);
        expect(apiNames.has('TossPayAuthorize')).toBe(true);
      }
    });

    test('FRAMEWORK_APIS 호환성', () => {
      if (!hasFrameworkApis(version)) {
        // 1.5.x: framework 패키지에 해당 API 없음 (정상)
        return;
      }

      const apiNames = new Set(apis.map(a => a.name));
      for (const name of FRAMEWORK_APIS) {
        expect(apiNames.has(name), `FRAMEWORK_API '${name}'이(가) v${version}에서 파싱되어야 함`).toBe(true);
      }
    });

    test('FRAMEWORK_APIS 속성 검증', () => {
      if (!hasFrameworkApis(version)) return;

      for (const name of FRAMEWORK_APIS) {
        const api = apis.find(a => a.name === name);
        if (!api) return; // 이전 테스트에서 실패 처리됨

        expect(api.isCallbackBased).toBe(true);
        expect(api.isTopLevelExport).toBe(true);
        expect(api.category).toBe('Advertising');
      }
    });
  });

  // 메타 테스트: 최신 버전이 최소 버전보다 API가 같거나 많아야 함
  // (중간 버전에서 deprecated API 제거가 가능하므로 patch별 단조 비교는 하지 않음)
  test('최신 버전의 API 수가 최소 버전 이상이어야 함', async () => {
    if (installedVersions.length < 2) return;

    const first = installedVersions[0];
    const last = installedVersions[installedVersions.length - 1];

    const firstPaths = await resolveVersionPaths(first);
    const lastPaths = await resolveVersionPaths(last);

    const firstApis = await createParserForVersion(firstPaths).parseAPIs(hasFrameworkApis(first) ? FRAMEWORK_APIS : []);
    const lastApis = await createParserForVersion(lastPaths).parseAPIs(hasFrameworkApis(last) ? FRAMEWORK_APIS : []);

    expect(
      lastApis.length,
      `최신 v${last} (${lastApis.length}개 API)가 최초 v${first} (${firstApis.length}개 API)보다 적음`
    ).toBeGreaterThanOrEqual(firstApis.length);
  });

  // FRAMEWORK_APIS ↔ API_CATEGORIES 일관성 검증
  test('FRAMEWORK_APIS의 모든 항목이 API_CATEGORIES에 매핑되어야 함', () => {
    const allMappedApis = new Set<string>();
    for (const categoryApis of Object.values(API_CATEGORIES)) {
      categoryApis.forEach(api => allMappedApis.add(api));
    }

    for (const name of FRAMEWORK_APIS) {
      expect(allMappedApis.has(name), `FRAMEWORK_API '${name}'이(가) API_CATEGORIES에 없음`).toBe(true);
    }
  });

  // Record<string, object/unknown> 파라미터가 object로 매핑되는지 검증
  // (하위 호환성: 수동 작성 파일이 object 타입으로 호출하므로 Dictionary<string, object>가 되면 안 됨)
  describe.each(installedVersions.map(v => ({ version: v })))('v$version Record→object 파라미터 호환성', ({ version }) => {
    let apis: ParsedAPI[] = [];

    beforeAll(async () => {
      const paths = await resolveVersionPaths(version);
      const frameworkApiNames = hasFrameworkApis(version) ? FRAMEWORK_APIS : [];
      apis = await createParserForVersion(paths).parseAPIs(frameworkApiNames);
    });

    test('모든 API 파라미터에서 Dictionary<string, object>가 생성되지 않아야 함', () => {
      for (const api of apis) {
        const prepared = prepareParameters(api);
        for (const param of prepared) {
          expect(
            param.paramType,
            `API '${api.name}'의 파라미터 '${param.paramName}'이 Dictionary<string, object>로 매핑됨 (object여야 함)`
          ).not.toBe('Dictionary<string, object>');
        }
      }
    });

    test('Analytics API 파라미터가 object 타입이어야 함 (수동 파일 호환성)', () => {
      const analyticsApis = apis.filter(a =>
        a.name === 'analyticsScreen' || a.name === 'analyticsImpression' || a.name === 'analyticsClick'
      );

      for (const api of analyticsApis) {
        const prepared = prepareParameters(api);
        for (const param of prepared) {
          expect(
            param.paramType,
            `Analytics API '${api.name}'의 파라미터 '${param.paramName}'이 '${param.paramType}'이지만 'object'여야 함`
          ).toBe('object');
        }
      }
    });
  });
});
