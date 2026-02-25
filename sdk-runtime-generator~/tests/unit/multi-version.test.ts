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
import * as path from 'path';
import * as fs from 'fs';
import { TypeScriptParser } from '../../src/parser/TypeScriptParser.js';
import { FRAMEWORK_APIS, API_CATEGORIES, CATEGORY_ORDER } from '../../src/categories.js';
import type { ParsedAPI } from '../../src/types.js';
import { generateChangelogHTML } from './report/changelog-html.js';

// =================================================================
// 버전 자동 감지
// =================================================================

/**
 * 설치된 pnpm alias에서 테스트 대상 버전을 자동 감지
 */
function discoverInstalledVersions(): string[] {
  const nmDir = path.join(process.cwd(), 'node_modules');
  if (!fs.existsSync(nmDir)) return [];

  return fs.readdirSync(nmDir)
    .filter(d => d.startsWith('web-framework-') && d !== 'web-framework')
    .map(d => d.replace('web-framework-', ''))
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
}

/**
 * FRAMEWORK_APIS가 도입된 최소 버전 (1.6.0 이상)
 */
function hasFrameworkApis(version: string): boolean {
  const [major, minor] = version.split('.').map(Number);
  return major > 1 || (major === 1 && minor >= 6);
}

// =================================================================
// 버전별 경로 해결
// =================================================================

interface VersionPaths {
  dtsDir: string | null;
  frameworkDts: string | null;
  webFrameworkPath: string;
}

function resolveVersionPaths(version: string): VersionPaths {
  const aliasPath = path.join(process.cwd(), 'node_modules', `web-framework-${version}`);
  const realPath = fs.realpathSync(aliasPath);
  const siblingDir = path.dirname(realPath); // .pnpm/.../node_modules/@apps-in-toss/

  // web-bridge .d.ts 디렉토리 (dist/ 또는 built/)
  const webBridgeBase = path.join(siblingDir, 'web-bridge');
  let dtsDir: string | null = null;

  for (const subdir of ['dist', 'built']) {
    const candidate = path.join(webBridgeBase, subdir);
    if (fs.existsSync(candidate)) {
      const files = fs.readdirSync(candidate);
      const hasValidDts = files.some(f =>
        f.endsWith('.d.ts') &&
        f !== 'index.d.ts' &&
        f !== 'index.d.cts'
      );
      if (hasValidDts) {
        dtsDir = candidate;
        break;
      }
    }
  }

  // framework index.d.cts (FRAMEWORK_APIS용)
  const frameworkDtsPath = path.join(siblingDir, 'framework', 'dist', 'index.d.cts');
  const frameworkDts = fs.existsSync(frameworkDtsPath) ? frameworkDtsPath : null;

  return { dtsDir, frameworkDts, webFrameworkPath: realPath };
}

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
      paths = resolveVersionPaths(version);
      if (!paths.dtsDir) return;

      const frameworkApiNames = hasFrameworkApis(version) ? FRAMEWORK_APIS : [];
      const parser = new TypeScriptParser(paths.dtsDir, paths.webFrameworkPath);
      apis = await parser.parseAPIs(frameworkApiNames);
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

    const firstPaths = resolveVersionPaths(first);
    const lastPaths = resolveVersionPaths(last);
    if (!firstPaths.dtsDir || !lastPaths.dtsDir) return;

    const firstParser = new TypeScriptParser(firstPaths.dtsDir, firstPaths.webFrameworkPath);
    const firstApis = await firstParser.parseAPIs(hasFrameworkApis(first) ? FRAMEWORK_APIS : []);

    const lastParser = new TypeScriptParser(lastPaths.dtsDir, lastPaths.webFrameworkPath);
    const lastApis = await lastParser.parseAPIs(hasFrameworkApis(last) ? FRAMEWORK_APIS : []);

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

  // 버전별 API 변화 HTML 리포트 생성
  test('API 변화 HTML 리포트 생성', async () => {
    const versionApis = new Map<string, ParsedAPI[]>();
    for (const version of installedVersions) {
      const paths = resolveVersionPaths(version);
      if (!paths.dtsDir) continue;
      const parser = new TypeScriptParser(paths.dtsDir, paths.webFrameworkPath);
      const apis = await parser.parseAPIs(hasFrameworkApis(version) ? FRAMEWORK_APIS : []);
      versionApis.set(version, apis);
    }

    const html = generateChangelogHTML(versionApis, CATEGORY_ORDER);
    const reportsDir = path.join(process.cwd(), 'reports');
    fs.mkdirSync(reportsDir, { recursive: true });
    const reportPath = path.join(reportsDir, 'api-changelog.html');
    fs.writeFileSync(reportPath, html, 'utf-8');

    console.log(`\n📊 HTML 리포트 생성: ${reportPath}\n`);
    expect(fs.existsSync(reportPath)).toBe(true);
  });
});
