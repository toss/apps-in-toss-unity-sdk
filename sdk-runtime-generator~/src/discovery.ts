/**
 * web-framework 버전 발견(discovery) 모듈
 *
 * changelog 파이프라인(package.json devDependencies의 web-framework-X.Y.Z alias로
 * 설치된 여러 버전을 전부 찾음, tests/unit/multi-version.test.ts 및
 * tests/unit/scripts/generate-changelog.ts)이 쓴다. 입력은 항상 각 버전의
 * dist/index.d.ts 하나다.
 */

import * as path from 'path';
import * as fs from 'fs/promises';
import * as fsSync from 'fs';
import { TypeScriptParser } from './parser/index.js';

/**
 * 특정 web-framework 버전에 대해 해석된 경로 정보.
 */
export interface VersionPaths {
  version: string;
  webFrameworkPath: string;
  /** dist/index.d.ts 절대 경로 */
  bundleFile: string;
}

// =====================================================
// 다중 버전(changelog) discovery
// =====================================================

/**
 * 설치된 pnpm alias에서 테스트/changelog 대상 버전을 자동 감지
 * (node_modules/web-framework-X.Y.Z 형태의 alias 디렉토리를 스캔)
 */
export function discoverInstalledVersions(): string[] {
  const nmDir = path.join(process.cwd(), 'node_modules');
  if (!fsSync.existsSync(nmDir)) return [];

  return fsSync.readdirSync(nmDir)
    .filter(d => d.startsWith('web-framework-') && d !== 'web-framework')
    .map(d => d.replace('web-framework-', ''))
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
}

/**
 * 특정 web-framework alias 버전의 번들 경로를 해석한다.
 * dist/index.d.ts가 없으면 다른 입력으로 대체하지 않고 이유를 알 수 있는 메시지로 즉시 실패한다.
 */
export async function resolveVersionPaths(version: string): Promise<VersionPaths> {
  const aliasPath = path.join(process.cwd(), 'node_modules', `web-framework-${version}`);
  const realPath = await fs.realpath(aliasPath);
  const bundleFile = path.join(realPath, 'dist', 'index.d.ts');

  try {
    await fs.access(bundleFile);
  } catch {
    throw new Error(
      `web-framework v${version}의 번들 index.d.ts를 찾을 수 없습니다: ${bundleFile}\n` +
      `  이 SDK는 web-framework 3.x의 dist/index.d.ts만 입력으로 씁니다.`
    );
  }

  return { version, webFrameworkPath: realPath, bundleFile };
}

/** 버전 경로에서 번들 파서를 생성한다 */
export function createParserForVersion(paths: VersionPaths): TypeScriptParser {
  return new TypeScriptParser(paths.bundleFile);
}

// =====================================================
// alias ↔ dependency 버전 일관성 invariant
// =====================================================

function compareVersionStrings(a: string, b: string): number {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const diff = (pa[i] || 0) - (pb[i] || 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

/**
 * package.json devDependencies에 등록된 web-framework-X.Y.Z alias 중 최고 버전이
 * 실제 dependencies의 @apps-in-toss/web-framework 버전과 일치하는지, 그리고 선언된
 * alias가 전부 node_modules에 실제 설치되어 있는지 검증한다.
 *
 * 배경: 이 alias 목록이 changelog 파이프라인(discoverInstalledVersions)이 스캔하는
 * 버전 전체 집합이다.
 * - 메인 의존성 버전을 올리면서 이 alias 추가를 깜빡하면(3.0.1 누락 버그) 최신 버전이
 *   changelog에서 조용히 빠진다 → 최고 버전 일치 검사.
 * - alias는 선언되어 있지만 부분 설치(예: 네트워크 오류로 일부만 설치됨) 상태라면
 *   discoverInstalledVersions()가 "실제 존재하는 것만" 스캔하므로, 67개 중 1개만
 *   설치돼도 조용히 1버전짜리 리포트가 생성되어 기존 changelog를 덮어쓸 수 있다 →
 *   전체 alias의 node_modules 실존 여부 검사.
 * fs만 읽는 가벼운 검사이므로 PR CI에서 항상 돌려 즉시 잡아낸다.
 */
export function assertWebFrameworkAliasInvariant(
  pkgJsonPath: string = path.join(process.cwd(), 'package.json'),
): void {
  const pkg = JSON.parse(fsSync.readFileSync(pkgJsonPath, 'utf-8'));
  const devDeps: Record<string, string> = pkg.devDependencies ?? {};
  const aliasVersions = Object.keys(devDeps)
    .filter(k => /^web-framework-\d+\.\d+\.\d+$/.test(k))
    .map(k => k.replace('web-framework-', ''));

  if (aliasVersions.length === 0) {
    throw new Error(
      `package.json(${pkgJsonPath}) devDependencies에 web-framework-X.Y.Z alias가 하나도 없습니다.`
    );
  }

  aliasVersions.sort(compareVersionStrings);
  const maxAliasVersion = aliasVersions[aliasVersions.length - 1];
  const dependencyVersion: string | undefined = pkg.dependencies?.['@apps-in-toss/web-framework'];

  if (!dependencyVersion) {
    throw new Error(
      `package.json(${pkgJsonPath}) dependencies에 @apps-in-toss/web-framework가 없습니다.`
    );
  }

  if (maxAliasVersion !== dependencyVersion) {
    throw new Error(
      `web-framework alias 최고 버전(${maxAliasVersion})이 dependencies 버전(${dependencyVersion})과 다릅니다.\n` +
      `  package.json devDependencies에 "web-framework-${dependencyVersion}": ` +
      `"npm:@apps-in-toss/web-framework@${dependencyVersion}" alias 추가가 필요할 수 있습니다.\n` +
      `  (changelog 파이프라인은 이 alias 목록으로 스캔 대상 버전을 결정하므로, 빠지면 ` +
      `최신 버전이 changelog에서 조용히 누락됩니다.)`
    );
  }

  const nodeModulesDir = path.join(path.dirname(pkgJsonPath), 'node_modules');
  const missingVersions = aliasVersions.filter(
    v => !fsSync.existsSync(path.join(nodeModulesDir, `web-framework-${v}`)),
  );
  if (missingVersions.length > 0) {
    throw new Error(
      `package.json devDependencies에 선언된 web-framework alias 중 ${missingVersions.length}개가 ` +
      `node_modules에 설치되어 있지 않습니다: ${missingVersions.map(v => `v${v}`).join(', ')}\n` +
      `  부분 설치 상태로 changelog를 생성하면 일부 버전이 조용히 빠진 축소된 리포트가 기존 ` +
      `changelog를 덮어쓸 수 있습니다.\n` +
      `  "pnpm install --no-frozen-lockfile"을 다시 실행해 전체 alias를 설치하세요.`
    );
  }
}
