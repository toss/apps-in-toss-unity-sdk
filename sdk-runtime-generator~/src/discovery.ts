/**
 * web-framework 버전 발견(discovery) 공유 모듈
 *
 * - index.ts의 generate() 파이프라인(node_modules의 "현재" web-framework 1개를 찾음)과
 * - changelog 파이프라인(package.json devDependencies의 web-framework-X.Y.Z alias로
 *   설치된 여러 버전을 전부 찾음, tests/unit/multi-version.test.ts 및
 *   tests/unit/scripts/generate-changelog.ts)
 *
 * 양쪽이 공유하는 "web-bridge/.d.ts 경로 찾기" 로직을 여기 한 곳에 모은다.
 * index.ts는 이 모듈을 import만 하며 동작은 이전과 동일해야 한다
 * (검증: `pnpm generate` 후 `git status`에서 Runtime/SDK/ 무변경).
 */

import * as path from 'path';
import * as fs from 'fs/promises';
import * as fsSync from 'fs';
import picocolors from 'picocolors';
import { TypeScriptParser } from './parser/index.js';

/**
 * .d.ts 디렉토리를 찾은 전략.
 * - sibling: web-framework의 pnpm 의존성 그래프 sibling에서 발견 (가장 정확)
 * - pnpm-store: node_modules/.pnpm 전체를 스캔해 발견 (sibling 실패 시 폴백)
 * - package-dir: node_modules 직하 또는 web-framework 패키지 내부 경로에서 발견
 */
export type DtsSource = 'sibling' | 'pnpm-store' | 'package-dir';

/**
 * 특정 web-framework 버전에 대해 해석된 경로 정보.
 */
export interface VersionPaths {
  version: string;
  webFrameworkPath: string;
  dtsDir: string;
  dtsSource: DtsSource;
  webAnalyticsDtsDir: string | null;
}

/**
 * pnpm virtual store에서 web-bridge 패키지 동적 검색
 * v1.8.0+: dist/ 디렉토리, v1.5.0~v1.7.x: built/ 디렉토리
 *
 * 스토어에 여러 web-bridge 버전이 공존할 수 있다(여러 web-framework 버전을 testfixture로
 * 설치하면 각자의 transitive web-bridge가 함께 깔린다). 이 폴백은 web-framework의
 * 의존성 그래프에서 sibling web-bridge를 못 찾았을 때만 쓰이므로(findTypeDefinitions의
 * strategy 1 실패 — 예: 3.0.0은 web-bridge가 webview-bridge로 rename됨), 가능한 한
 * **가장 최신** web-bridge 버전을 선택해 그 시점에 알려진 최대 API 표면을 발견하게 한다.
 * (과거에는 readdir 순서상 lexicographically-first 항목 — 예: 1.10.0 — 이 선택되어
 * 오래된 표면으로 폴백하는 버그가 있었다.)
 */
export async function findWebBridgeInPnpmStore(): Promise<string | null> {
  const pnpmDir = path.join(process.cwd(), 'node_modules/.pnpm');
  const PREFIX = '@apps-in-toss+web-bridge@';

  try {
    const entries = await fs.readdir(pnpmDir);
    // @apps-in-toss+web-bridge@{version}[_<peer>] 패턴 찾기 (web-analytics 제외)
    const webBridgeEntries = entries.filter(e =>
      e.startsWith(PREFIX) && !e.includes('+web-analytics')
    );

    // 버전 내림차순 정렬 — 가장 최신 web-bridge를 우선 선택.
    // 엔트리명: "@apps-in-toss+web-bridge@2.6.1" 또는
    //           "@apps-in-toss+web-bridge@1.5.0_@apps-in-toss+bridge-core@1.5.0"
    const parseVersion = (entry: string): number[] => {
      const raw = entry.slice(PREFIX.length);
      // peer-dep 접미사(_… 또는 (…) 와 prerelease(-…) 제거 후 숫자 파트만 비교
      const version = raw.split(/[_(]/)[0].split('-')[0];
      return version.split('.').map(n => Number(n) || 0);
    };
    webBridgeEntries.sort((a, b) => {
      const va = parseVersion(a);
      const vb = parseVersion(b);
      for (let i = 0; i < Math.max(va.length, vb.length); i++) {
        const diff = (vb[i] || 0) - (va[i] || 0);
        if (diff !== 0) return diff;
      }
      return 0;
    });

    // 최신 버전부터 순회하며 dist/built가 실재하는 첫 엔트리를 사용
    for (const webBridgeEntry of webBridgeEntries) {
      const basePath = path.join(
        pnpmDir,
        webBridgeEntry,
        'node_modules/@apps-in-toss/web-bridge'
      );

      // v1.8.0+: dist/, v1.5.0~v1.7.x: built/
      for (const subdir of ['dist', 'built']) {
        const candidatePath = path.join(basePath, subdir);
        try {
          await fs.access(candidatePath);
          console.log(picocolors.gray(`  pnpm store에서 web-bridge 선택(최신): ${webBridgeEntry}`));
          return candidatePath;
        } catch {
          continue;
        }
      }
    }
  } catch {
    // pnpm store가 없으면 null 반환
  }

  return null;
}

/**
 * 후보 디렉토리에 유효한(.d.ts, index.d.ts/index.d.cts 제외) 타입 정의 파일이 있는지 확인
 */
async function hasValidDtsFiles(dir: string): Promise<boolean> {
  try {
    const stat = await fs.stat(dir);
    if (!stat.isDirectory()) return false;
    const files = await fs.readdir(dir);
    return files.some(f =>
      f.endsWith('.d.ts') &&
      f !== 'index.d.ts' &&
      f !== 'index.d.cts'
    );
  } catch {
    return false;
  }
}

/**
 * TypeScript 정의 파일 경로 찾기 (발견 전략과 소스를 함께 반환)
 * 찾지 못하면 null을 반환한다 (throw는 호출부 책임).
 */
export async function findTypeDefinitionsWithSource(
  webFrameworkPath: string,
): Promise<{ path: string; source: DtsSource } | null> {
  // web-framework의 실제 경로를 resolve하여 pnpm 의존성 그래프에 따른 web-bridge 찾기
  // pnpm 구조: .pnpm/...web-framework@X.Y.Z/node_modules/@apps-in-toss/web-framework
  //          → .pnpm/...web-framework@X.Y.Z/node_modules/@apps-in-toss/web-bridge (symlink → 올바른 버전)
  let webBridgeFromDeps: string | null = null;
  try {
    const realWebFrameworkPath = await fs.realpath(webFrameworkPath);
    const siblingWebBridge = path.join(path.dirname(realWebFrameworkPath), 'web-bridge');
    try {
      const realWebBridge = await fs.realpath(siblingWebBridge);
      for (const subdir of ['dist', 'built']) {
        const candidatePath = path.join(realWebBridge, subdir);
        try {
          const stat = await fs.stat(candidatePath);
          if (stat.isDirectory()) {
            webBridgeFromDeps = candidatePath;
            console.log(picocolors.gray(`  web-framework 의존성 그래프에서 web-bridge 발견: ${candidatePath}`));
            break;
          }
        } catch {
          continue;
        }
      }
    } catch {
      // sibling web-bridge가 없으면 스킵
    }
  } catch {
    // realpath 실패 시 스킵
  }

  // pnpm virtual store에서 동적으로 검색 (폴백)
  const pnpmStorePath = await findWebBridgeInPnpmStore();

  // 가능한 경로들 확인 (의존성 그래프 기반 > pnpm store > node_modules > 기타)
  const candidates: { path: string; source: DtsSource }[] = [
    // 의존성 그래프에서 찾은 경로 (가장 정확 - 올바른 버전 보장)
    ...(webBridgeFromDeps ? [{ path: webBridgeFromDeps, source: 'sibling' as const }] : []),
    // pnpm virtual store 경로 (동적 검색 결과 - 여러 버전이 있을 수 있음)
    ...(pnpmStorePath ? [{ path: pnpmStorePath, source: 'pnpm-store' as const }] : []),
    // 일반 node_modules 경로 (dist 우선, built 폴백)
    { path: path.join(process.cwd(), 'node_modules/@apps-in-toss/web-bridge/dist'), source: 'package-dir' },
    { path: path.join(process.cwd(), 'node_modules/@apps-in-toss/web-bridge/built'), source: 'package-dir' },
    // web-framework 내부 경로 (dist 우선, built 폴백)
    { path: path.join(webFrameworkPath, 'node_modules/@apps-in-toss/web-bridge/dist'), source: 'package-dir' },
    { path: path.join(webFrameworkPath, 'node_modules/@apps-in-toss/web-bridge/built'), source: 'package-dir' },
    { path: path.join(webFrameworkPath, 'dist-web'), source: 'package-dir' },
    { path: path.join(webFrameworkPath, 'built'), source: 'package-dir' },
    { path: path.join(webFrameworkPath, 'dist'), source: 'package-dir' },
    { path: path.join(webFrameworkPath, 'lib'), source: 'package-dir' },
  ];

  for (const candidate of candidates) {
    if (await hasValidDtsFiles(candidate.path)) {
      console.log(picocolors.green(`✅ TypeScript 정의 파일 발견: ${candidate.path}`));
      return candidate;
    }
  }

  return null;
}

/**
 * TypeScript 정의 파일 경로 찾기
 * (index.ts generate() 파이프라인 전용 — 못 찾으면 throw)
 */
export async function findTypeDefinitions(webFrameworkPath: string): Promise<string> {
  const result = await findTypeDefinitionsWithSource(webFrameworkPath);
  if (!result) {
    throw new Error('TypeScript 정의 파일을 찾을 수 없습니다.');
  }
  return result.path;
}

/**
 * web-analytics 패키지의 .d.ts 디렉토리 경로 찾기
 * web-framework의 sibling 패턴을 재사용하여 탐색
 */
export async function findWebAnalyticsPath(webFrameworkPath: string): Promise<string | null> {
  // 전략 1: web-framework의 sibling에서 찾기 (가장 정확)
  try {
    const realWebFrameworkPath = await fs.realpath(webFrameworkPath);
    const siblingAnalytics = path.join(path.dirname(realWebFrameworkPath), 'web-analytics');
    try {
      const realAnalytics = await fs.realpath(siblingAnalytics);
      for (const subdir of ['dist', 'built']) {
        const candidate = path.join(realAnalytics, subdir);
        try {
          const stat = await fs.stat(candidate);
          if (stat.isDirectory()) {
            return candidate;
          }
        } catch {
          continue;
        }
      }
    } catch {
      // sibling web-analytics가 없으면 스킵
    }
  } catch {
    // realpath 실패 시 스킵
  }

  // 전략 2: 일반 node_modules 경로
  const directPaths = [
    path.join(process.cwd(), 'node_modules/@apps-in-toss/web-analytics/dist'),
    path.join(process.cwd(), 'node_modules/@apps-in-toss/web-analytics/built'),
  ];
  for (const p of directPaths) {
    try {
      const stat = await fs.stat(p);
      if (stat.isDirectory()) {
        return p;
      }
    } catch {
      continue;
    }
  }

  // 전략 3: pnpm virtual store에서 동적 검색
  try {
    const pnpmDir = path.join(process.cwd(), 'node_modules/.pnpm');
    const entries = await fs.readdir(pnpmDir);
    const webAnalyticsEntry = entries.find(e =>
      e.startsWith('@apps-in-toss+web-analytics@')
    );
    if (webAnalyticsEntry) {
      const basePath = path.join(
        pnpmDir,
        webAnalyticsEntry,
        'node_modules/@apps-in-toss/web-analytics'
      );
      for (const subdir of ['dist', 'built']) {
        const candidate = path.join(basePath, subdir);
        try {
          const stat = await fs.stat(candidate);
          if (stat.isDirectory()) {
            return candidate;
          }
        } catch {
          continue;
        }
      }
    }
  } catch {
    // pnpm store가 없으면 무시
  }

  return null;
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
 * FRAMEWORK_APIS(@apps-in-toss/framework 직접 파싱 API)가 도입된 최소 버전 (1.6.0 이상)
 */
export function hasFrameworkApis(version: string): boolean {
  const [major, minor] = version.split('.').map(Number);
  return major > 1 || (major === 1 && minor >= 6);
}

/**
 * 특정 web-framework alias 버전의 경로 전체를 해석한다.
 *
 * sibling에서 dtsDir을 찾지 못하면(예: 3.0.0+의 web-bridge → webview-bridge 리네임)
 * pnpm store 폴백을 시도하고, 그래도 해석에 실패하면 조용히 스킵하는 대신 이 버전이
 * 왜 빠지는지 알 수 있도록 명확한 한국어 메시지로 throw한다 — changelog 최신 버전
 * 누락(예: 3.0.1) 버그의 재발을 방지하기 위함.
 */
export async function resolveVersionPaths(version: string): Promise<VersionPaths> {
  const aliasPath = path.join(process.cwd(), 'node_modules', `web-framework-${version}`);
  const realPath = await fs.realpath(aliasPath);

  const result = await findTypeDefinitionsWithSource(realPath);
  if (!result) {
    throw new Error(
      `web-framework v${version}의 TypeScript 정의 파일을 찾을 수 없습니다.\n` +
      `  확인한 경로: sibling(web-bridge), pnpm store 폴백, ${realPath} 하위 dist/built/lib 등.\n` +
      `  web-framework 재구성(예: web-bridge → webview-bridge 리네임)으로 발견 전략이 깨졌을 수 있습니다.\n` +
      `  discovery.ts의 findTypeDefinitionsWithSource를 확인하세요.`
    );
  }

  const webAnalyticsDtsDir = await findWebAnalyticsPath(realPath);

  return {
    version,
    webFrameworkPath: realPath,
    dtsDir: result.path,
    dtsSource: result.source,
    webAnalyticsDtsDir,
  };
}

/**
 * 버전 경로에서 파서를 생성하고 web-analytics 소스가 있으면 추가
 */
export function createParserForVersion(paths: VersionPaths): TypeScriptParser {
  const parser = new TypeScriptParser(paths.dtsDir, paths.webFrameworkPath);
  if (paths.webAnalyticsDtsDir) {
    parser.addSourceDirectory(paths.webAnalyticsDtsDir);
  }
  return parser;
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
