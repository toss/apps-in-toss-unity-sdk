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
      apis = await createParserForVersion(paths).parseAPIs(frameworkApiNames);
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
      // 모든 버전에 존재하는 기본 API
      expect(apiNames.has('appLogin')).toBe(true);
      expect(apiNames.has('getDeviceId')).toBe(true);
      expect(apiNames.has('checkoutPayment')).toBe(true);
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
