/**
 * 다중 버전 호환성 테스트
 *
 * pnpm alias로 설치된 모든 web-framework 3.x 버전에 대해
 * TypeScriptParser(번들 dist/index.d.ts 입력)가 정상적으로 API를 파싱하는지 검증합니다.
 *
 * 새 버전 추가 시: package.json에 alias만 추가하면 자동으로 테스트 대상에 포함됩니다.
 * 예: "web-framework-3.0.1": "npm:@apps-in-toss/web-framework@3.0.1"
 */

import { describe, test, expect, beforeAll } from 'vitest';
import type { ParsedAPI } from '../../src/types.js';
import { prepareParameters } from '../../src/generators/csharp/api-data-preparer.js';
import {
  discoverInstalledVersions,
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
      apis = await createParserForVersion(paths).parseAPIs();
    });

    test('번들 index.d.ts를 찾을 수 있어야 함', () => {
      expect(paths.bundleFile).toBeTruthy();
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

      // 플랫 함수 appLogin과 네임스페이스 멤버가 모두 진입점으로 노출된다
      expect(apiNames.has('appLogin')).toBe(true);
      expect(apiNames.has('StorageGetItem')).toBe(true);
      expect(apiNames.has('TossPayAuthorize')).toBe(true);

      // 3.x 번들은 getDeviceId/checkoutPayment를 deprecated 최상위 export로 유지한다.
      // 대체 API가 안내된 경우 isDeprecated=true로 보존한다.
      expect(apiNames.has('getDeviceId')).toBe(true);
      expect(apiNames.has('checkoutPayment')).toBe(true);
      expect(apis.find(a => a.name === 'checkoutPayment')?.isDeprecated).toBe(true);

      // TossPay.checkoutPayment 재노출 지점에도 별도 @deprecated가 붙어 있다
      expect(apiNames.has('TossPayCheckoutPayment')).toBe(true);
      expect(apis.find(a => a.name === 'TossPayCheckoutPayment')?.isDeprecated).toBe(true);
    });

    test('FullScreenAd API는 콜백 기반 최상위 export여야 함', () => {
      for (const name of ['loadFullScreenAd', 'showFullScreenAd']) {
        const api = apis.find(a => a.name === name);
        expect(api, `${name}이(가) v${version}에서 파싱되어야 함`).toBeDefined();
        expect(api!.isCallbackBased).toBe(true);
        expect(api!.isTopLevelExport).toBe(true);
        expect(api!.category).toBe('Advertising');
      }
    });

    test('API 이름이 중복되지 않아야 함', () => {
      const names = apis.map(a => a.name);
      expect(new Set(names).size).toBe(names.length);
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

    const firstApis = await createParserForVersion(firstPaths).parseAPIs();
    const lastApis = await createParserForVersion(lastPaths).parseAPIs();

    expect(
      lastApis.length,
      `최신 v${last} (${lastApis.length}개 API)가 최초 v${first} (${firstApis.length}개 API)보다 적음`
    ).toBeGreaterThanOrEqual(firstApis.length);
  });

  // Record<string, object/unknown> 파라미터가 object로 매핑되는지 검증
  // (열린 사전 파라미터는 object여야 한다: Dictionary<string, object>로 매핑하면 호출부가 깨진다)
  describe.each(installedVersions.map(v => ({ version: v })))('v$version Record→object 파라미터 매핑', ({ version }) => {
    let apis: ParsedAPI[] = [];

    beforeAll(async () => {
      const paths = await resolveVersionPaths(version);
      apis = await createParserForVersion(paths).parseAPIs();
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

    test('Analytics screen/impression/click 파라미터가 열린 사전(object)이어야 함', () => {
      const analyticsApis = apis.filter(a =>
        a.name === 'AnalyticsScreen' || a.name === 'AnalyticsImpression' || a.name === 'AnalyticsClick'
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
