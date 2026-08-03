#!/usr/bin/env node

import { Command } from 'commander';
import picocolors from 'picocolors';
import * as path from 'path';
import * as fs from 'fs/promises';
import * as crypto from 'crypto';
import { TypeScriptParser, drainDomViolations, type DomViolation } from './parser/index.js';
import { validateAllTypes } from './validators/types.js';
import { validateCompleteness, printSummary } from './validators/completeness.js';
import { CSharpGenerator, CSharpTypeGenerator } from './generators/csharp/index.js';
import { JSLibGenerator } from './generators/jslib.js';
import { typeCheckBridgeCode, printTypeCheckResult, cleanupCache } from './generators/jslib-compiler.js';
import { generateUnityBridge } from './generators/unity-bridge.js';
import { generateScreenManualCs, generateScreenManualJslib, generateCoreManualJslib } from './generators/webgl-manual.js';
import { formatCommand } from './commands/format.js';
import { FRAMEWORK_APIS, EXCLUDED_APIS, DEPRECATED_API_OVERRIDES, CATEGORY_ORDER, DEFAULT_CATEGORY, resolveApiCategory } from './categories.js';
import { getApiImportSource, type ParsedAPI, type GeneratedTypeUnit } from './types.js';

const program = new Command();

// =====================================================
// Unity .meta 파일 관리
// =====================================================

/**
 * Unity GUID 생성 (32자리 소문자 hex)
 */
function generateUnityGUID(): string {
  return crypto.randomBytes(16).toString('hex');
}

/**
 * 타깃 web-framework가 더 이상 공개 export하지 않는 API를 발견 목록에서 제거한다.
 *
 * 배경: 발견(discovery)은 web-bridge .d.ts 스캔에 의존하는데, web-framework 메이저
 * 업데이트로 패키지가 재구성되면(예: web-bridge → webview-bridge 리네임) 발견 경로가
 * pnpm 스토어의 stale 구버전 .d.ts로 폴백할 수 있다. 그러면 신버전에서 제거된 API가
 * 계속 발견되고, 그 API의 브릿지가 존재하지 않는 심볼을 import 하여 jslib 타입 검사
 * (TS2305)에서 실패한다.
 *
 * 이 필터는 **@apps-in-toss/web-framework에서 import 될** API에 한해, 그 심볼이 실제
 * 타깃 web-framework의 public export 집합에 있는지 확인하고 없으면 제거한다.
 * - @apps-in-toss/framework(isTopLevelExport) / @apps-in-toss/web-analytics 소속 API는
 *   web-framework export와 무관하므로 게이트 대상이 아니다.
 * - export 집합 계산 실패 시(빈 집합) 필터를 적용하지 않는다(fail-open) — 정상 동작하는
 *   기존 버전 생성이 빈 집합 때문에 전부 제거되는 사고를 방지.
 *
 * 2.6.1 등 기존 stable 버전에서는 발견 API가 모두 여전히 export되므로 무회귀.
 */
function filterToWebFrameworkExports(apis: ParsedAPI[], parser: TypeScriptParser): ParsedAPI[] {
  const wfExports = parser.getWebFrameworkExportedNames();
  if (wfExports.size === 0) {
    // 판별 불가 → 필터 스킵 (경고는 getWebFrameworkExportedNames에서 이미 출력)
    return apis;
  }

  const skipped: string[] = [];
  const kept = apis.filter(api => {
    // @apps-in-toss/framework 소속(top-level export)은 framework에서 import → 게이트 제외
    if (api.isTopLevelExport) return true;
    // web-framework가 아닌 패키지(web-analytics)에서 import → 게이트 제외
    if (getApiImportSource(api) !== '@apps-in-toss/web-framework') return true;

    // 브릿지가 web-framework에서 import할 심볼: 네임스페이스 API는 네임스페이스 이름,
    // 그 외는 원본 함수 이름.
    const requiredSymbol = api.namespace ?? api.originalName;
    if (wfExports.has(requiredSymbol)) return true;

    skipped.push(`${api.name} (심볼: ${requiredSymbol})`);
    return false;
  });

  if (skipped.length > 0) {
    console.log(
      picocolors.yellow(
        `⚠️  web-framework public export 제외: ${skipped.join(', ')} (이 web-framework 버전에서 제거됨 — 브릿지 생성 건너뜀)`,
      ),
    );
  }

  return kept;
}

/**
 * C# 파일용 .meta 파일 내용 생성
 */
function generateCSharpMeta(guid: string): string {
  return `fileFormatVersion: 2
guid: ${guid}
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

/**
 * jslib 파일용 .meta 파일 내용 생성 (WebGL 플랫폼만 활성화)
 */
function generateJslibMeta(guid: string): string {
  return `fileFormatVersion: 2
guid: ${guid}
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      WebGL: WebGL
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

/**
 * 디렉토리 내 모든 .meta 파일 수집 (재귀적)
 * @returns Map<파일 절대경로(확장자 제외), .meta 파일 내용>
 */
async function collectMetaFiles(dir: string): Promise<Map<string, string>> {
  const metaFiles = new Map<string, string>();
  const absoluteDir = path.resolve(dir);

  try {
    const entries = await fs.readdir(absoluteDir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(absoluteDir, entry.name);
      if (entry.isDirectory()) {
        // 재귀적으로 하위 디렉토리 탐색
        const subMetas = await collectMetaFiles(fullPath);
        for (const [key, value] of subMetas) {
          metaFiles.set(key, value);
        }
      } else if (entry.name.endsWith('.meta')) {
        // .meta 파일 발견: 원본 파일의 절대경로를 키로 저장
        const content = await fs.readFile(fullPath, 'utf-8');
        metaFiles.set(fullPath.slice(0, -5), content); // .meta 제거한 절대경로
      }
    }
  } catch {
    // 디렉토리가 없으면 빈 맵 반환
  }

  return metaFiles;
}

/**
 * 파일에 대응하는 .meta 파일 처리
 * - 기존 .meta 파일이 있으면 복원
 * - 없으면 새로 생성
 */
async function ensureMetaFile(
  filePath: string,
  existingMetas: Map<string, string>,
  fileType: 'cs' | 'jslib'
): Promise<void> {
  const metaPath = filePath + '.meta';

  if (existingMetas.has(filePath)) {
    // 기존 .meta 파일 복원
    await fs.writeFile(metaPath, existingMetas.get(filePath)!);
  } else {
    // 새 .meta 파일 생성
    const guid = generateUnityGUID();
    const content = fileType === 'cs' ? generateCSharpMeta(guid) : generateJslibMeta(guid);
    await fs.writeFile(metaPath, content);
    console.log(picocolors.blue(`  📄 새 .meta 파일 생성: ${path.basename(metaPath)}`));
  }
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
async function findWebBridgeInPnpmStore(): Promise<string | null> {
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
 * TypeScript 정의 파일 경로 찾기
 */
async function findTypeDefinitions(webFrameworkPath: string): Promise<string> {
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
  const possiblePaths = [
    // 의존성 그래프에서 찾은 경로 (가장 정확 - 올바른 버전 보장)
    ...(webBridgeFromDeps ? [webBridgeFromDeps] : []),
    // pnpm virtual store 경로 (동적 검색 결과 - 여러 버전이 있을 수 있음)
    ...(pnpmStorePath ? [pnpmStorePath] : []),
    // 일반 node_modules 경로 (dist 우선, built 폴백)
    path.join(process.cwd(), 'node_modules/@apps-in-toss/web-bridge/dist'),
    path.join(process.cwd(), 'node_modules/@apps-in-toss/web-bridge/built'),
    // web-framework 내부 경로 (dist 우선, built 폴백)
    path.join(webFrameworkPath, 'node_modules/@apps-in-toss/web-bridge/dist'),
    path.join(webFrameworkPath, 'node_modules/@apps-in-toss/web-bridge/built'),
    path.join(webFrameworkPath, 'dist-web'),
    path.join(webFrameworkPath, 'built'),
    path.join(webFrameworkPath, 'dist'),
    path.join(webFrameworkPath, 'lib'),
  ];

  for (const p of possiblePaths) {
    try {
      const stat = await fs.stat(p);
      if (stat.isDirectory()) {
        // .d.ts 파일이 있는지 확인 (index.d.ts 제외)
        const files = await fs.readdir(p);
        const hasValidDts = files.some(f =>
          f.endsWith('.d.ts') &&
          f !== 'index.d.ts' &&
          f !== 'index.d.cts'
        );
        if (hasValidDts) {
          console.log(picocolors.green(`✅ TypeScript 정의 파일 발견: ${p}`));
          return p;
        }
      }
    } catch {
      // 경로가 없으면 다음 경로 시도
      continue;
    }
  }

  throw new Error('TypeScript 정의 파일을 찾을 수 없습니다.');
}

/**
 * web-analytics 패키지의 .d.ts 디렉토리 경로 찾기
 * web-framework의 sibling 패턴을 재사용하여 탐색
 */
async function findWebAnalyticsPath(webFrameworkPath: string): Promise<string | null> {
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

/**
 * node_modules에서 web-framework 찾기 (버전 일치 검증 포함)
 */
async function findWebFrameworkInNodeModules(): Promise<string> {
  const webFrameworkPath = path.join(process.cwd(), 'node_modules/@apps-in-toss/web-framework');

  try {
    await fs.access(webFrameworkPath);
  } catch {
    throw new Error(
      'web-framework를 찾을 수 없습니다.\n' +
      '다음 명령을 실행하세요: pnpm install'
    );
  }

  // package.json에 명시된 버전과 실제 설치된 버전 비교
  const expectedVersion = await getExpectedWebFrameworkVersion();
  const installedVersion = await getInstalledWebFrameworkVersion(webFrameworkPath);

  if (expectedVersion && installedVersion && expectedVersion !== installedVersion) {
    throw new Error(
      `web-framework 버전 불일치!\n` +
      `  package.json: ${expectedVersion}\n` +
      `  node_modules: ${installedVersion}\n\n` +
      `pnpm-lock.yaml가 오래되었을 수 있습니다. 다음 명령을 실행하세요:\n` +
      `  rm pnpm-lock.yaml && pnpm install`
    );
  }

  console.log(picocolors.green(`✅ web-framework 발견: ${webFrameworkPath} (v${installedVersion})`));
  return webFrameworkPath;
}

async function getExpectedWebFrameworkVersion(): Promise<string | null> {
  try {
    const pkgJson = JSON.parse(await fs.readFile(path.join(process.cwd(), 'package.json'), 'utf-8'));
    return pkgJson.dependencies?.['@apps-in-toss/web-framework'] ?? null;
  } catch {
    return null;
  }
}

async function getInstalledWebFrameworkVersion(webFrameworkPath: string): Promise<string | null> {
  try {
    const pkgJson = JSON.parse(await fs.readFile(path.join(webFrameworkPath, 'package.json'), 'utf-8'));
    return pkgJson.version ?? null;
  } catch {
    return null;
  }
}

function formatDomViolations(violations: DomViolation[]): string {
  const lines = violations.map((v) => {
    const where =
      v.location === 'parameter'
        ? `parameter "${v.paramName}"`
        : 'return type';
    const fnLabel = v.category ? `${v.category}.${v.functionName}` : v.functionName;
    return `  - ${fnLabel}: ${where} has DOM-only type "${v.rawType}" (${v.file})`;
  });
  return [
    `SDK generation failed: ${violations.length} API(s) use unsupported DOM types.`,
    ...lines,
    'Refactor these APIs (e.g. accept a CSS selector string only) or remove them from the SDK.',
  ].join('\n');
}

/**
 * 메인 생성 로직
 */
async function generate(options: {
  tag: string;
  output: string;
  skipClone?: boolean;
  sourcePath?: string;
}) {
  const startTime = Date.now();

  try {
    console.log(picocolors.cyan(picocolors.bold('\n🚀 Unity SDK 자동 생성 시작\n')));
    console.log(picocolors.cyan(`📁 출력 경로: ${options.output}\n`));

    // 1. web-framework 경로 결정
    let webFrameworkPath: string;
    if (options.skipClone && options.sourcePath) {
      console.log(picocolors.yellow(`⚠️  로컬 경로 사용: ${options.sourcePath}`));
      webFrameworkPath = options.sourcePath;
    } else {
      // node_modules에서 web-framework 찾기
      webFrameworkPath = await findWebFrameworkInNodeModules();
    }

    // 2. TypeScript 정의 파일 찾기
    const typeDefinitionsPath = await findTypeDefinitions(webFrameworkPath);

    // 3. web-analytics 패키지 탐색 (선택적)
    const webAnalyticsPath = await findWebAnalyticsPath(webFrameworkPath);
    if (webAnalyticsPath) {
      console.log(picocolors.green(`✅ web-analytics 발견: ${webAnalyticsPath}`));
    } else {
      console.log(picocolors.yellow(`⚠️  web-analytics를 찾을 수 없습니다. Analytics API 생성을 건너뜁니다.`));
    }

    // 4. API 파싱
    console.log(picocolors.cyan('\n📊 web-framework 분석 중...'));
    const parser = new TypeScriptParser(typeDefinitionsPath, webFrameworkPath);
    if (webAnalyticsPath) {
      parser.addSourceDirectory(webAnalyticsPath);
    }
    const allParsedApis = await parser.parseAPIs(FRAMEWORK_APIS);

    // DOM-only 타입 위반 검증: 파싱 중 누적된 위반을 한 번에 보고하고 실패시킨다.
    const domViolations = drainDomViolations();
    if (domViolations.length > 0) {
      throw new Error(formatDomViolations(domViolations));
    }

    // 제외 목록에 있는 API 필터링
    const excludedSet = new Set(EXCLUDED_APIS);
    const notExcludedApis = allParsedApis.filter(api => !excludedSet.has(api.name));

    // 타깃 web-framework가 더 이상 public export하지 않는 API 제거
    // (예: 3.0.0에서 제거된 onVisibilityChangedByTransparentServiceWeb).
    // 발견 경로가 stale .d.ts로 폴백해 제거된 API를 계속 발견하더라도, 실제 타깃
    // web-framework export에 없으면 브릿지를 생성하지 않아 TS2305 실패를 예방한다.
    const apis = filterToWebFrameworkExports(notExcludedApis, parser);

    // 생성기 측 deprecate override 적용 (upstream d.ts에 @deprecated가 없는 API)
    for (const api of apis) {
      const overrideMessage = DEPRECATED_API_OVERRIDES[api.name];
      if (overrideMessage) {
        api.isDeprecated = true;
        api.deprecatedMessage = overrideMessage;
      }
    }

    if (apis.length === 0) {
      console.error(picocolors.red('\n❌ web-framework에서 API를 발견하지 못했습니다.\n'));
      console.error(picocolors.yellow('다음을 확인하세요:'));
      console.error(picocolors.yellow(`  1. TypeScript 정의 경로: ${typeDefinitionsPath}`));
      console.error(picocolors.yellow(`  2. web-framework 버전: ${webFrameworkPath}`));
      console.error(picocolors.yellow(`  3. .d.ts 파일에 export된 함수가 있는지 확인`));
      process.exit(1);
    }

    // FRAMEWORK_APIS 파싱 완전성 검증
    const parsedApiNames = new Set(allParsedApis.map(a => a.name));
    const parsedFrameworkApiCount = FRAMEWORK_APIS.filter(name => parsedApiNames.has(name)).length;
    const missingFrameworkApis = FRAMEWORK_APIS.filter(name => !parsedApiNames.has(name));

    if (missingFrameworkApis.length > 0) {
      if (parsedFrameworkApiCount > 0) {
        // 일부만 파싱됨 → 비정상 (부분 파싱은 버그)
        console.error(picocolors.red(`\n❌ FRAMEWORK_APIS 일부 파싱 실패: ${missingFrameworkApis.join(', ')}`));
        console.error(picocolors.yellow(`   findFrameworkPath()가 올바른 버전을 찾고 있는지 확인하세요.`));
        console.error(picocolors.yellow(`   현재 framework 경로: ${parser.frameworkDtsPath ?? '찾을 수 없음'}`));
        process.exit(1);
      } else {
        // 모두 missing → 이 web-framework 버전에 해당 API 없음 (정상)
        console.log(picocolors.yellow(`⚠️  FRAMEWORK_APIS 스킵: ${missingFrameworkApis.join(', ')} (이 web-framework 버전에 해당 API 없음)`));
      }
    }

    console.log(picocolors.green(`✓ ${apis.length}개 API 발견`));

    // 5. 타입 검증
    console.log(picocolors.cyan('\n🔍 타입 검증 중...'));
    const typeValidation = validateAllTypes(apis);
    if (!typeValidation.success) {
      console.error(picocolors.red('\n❌ 타입 검증 실패\n'));
      for (const error of typeValidation.errors) {
        console.error(error.message);
      }
      process.exit(1);
    }
    console.log(picocolors.green('✓ 타입 매핑 완료'));

    // 6. 타입 정의 파싱 (enum, interface)
    console.log(picocolors.cyan('\n📦 타입 정의 파싱 중...'));
    const typeDefinitions = await parser.parseTypeDefinitions();

    // @apps-in-toss/framework 타입 정의 추가 (loadFullScreenAd, showFullScreenAd 관련)
    // framework 패키지가 없으면 스킵 (v1.5.x에는 해당 API 없음)
    const frameworkTypeDefinitions = parser.frameworkDtsPath
      ? parser.parseFrameworkTypeDefinitions(FRAMEWORK_APIS, parser.frameworkDtsPath)
      : [];
    typeDefinitions.push(...frameworkTypeDefinitions);

    console.log(picocolors.green(`✓ ${typeDefinitions.length}개 타입 정의 발견`));
    if (frameworkTypeDefinitions.length > 0) {
      console.log(picocolors.gray(`   - Framework 타입: ${frameworkTypeDefinitions.length}개 (${frameworkTypeDefinitions.map(t => t.name).join(', ')})`));
    }

    // enum과 interface 분류
    const enums = typeDefinitions.filter(t => t.kind === 'enum');
    const interfaces = typeDefinitions.filter(t => t.kind === 'interface');
    if (enums.length > 0) {
      console.log(picocolors.gray(`   - Enum: ${enums.length}개 (${enums.map(e => e.name).join(', ')})`));
    }
    if (interfaces.length > 0) {
      console.log(picocolors.gray(`   - Interface: ${interfaces.length}개 (${interfaces.map(i => i.name).join(', ')})`));
    }

    // 7. 코드 생성
    console.log(picocolors.cyan('\n🔨 코드 생성 중...'));
    const csharpGenerator = new CSharpGenerator();
    const jslibGenerator = new JSLibGenerator();
    const typeGenerator = new CSharpTypeGenerator();

    // C# API 생성 (기존 방식 - 검증용)
    const generatedCodes = await csharpGenerator.generate(apis, options.tag);

    // 메인 AIT.cs 생성 (partial class 선언만)
    const mainFile = await csharpGenerator.generateMainFile(options.tag, apis.length);
    console.log(picocolors.green(`✓ AIT.cs (메인 partial class)`));

    // 카테고리별 API partial class 파일들 생성
    const categoryFiles = await csharpGenerator.generateCategoryFiles(apis);
    console.log(picocolors.green(`✓ ${categoryFiles.size}개 카테고리 파일 (AIT.{Category}.cs)`));

    // AITCore 생성 (인프라 코드) - enum 타입 목록 전달
    const enumTypeNames = new Set(enums.map(e => e.name));
    const coreFile = await csharpGenerator.generateCoreFile(apis, enumTypeNames);
    console.log(picocolors.green(`✓ AITCore.cs (Infrastructure)`));

    // C# 타입 정의 생성 (파싱된 enum/interface) - 타입별 유닛
    // 생성된 타입 이름도 함께 반환하여 API 타입 생성 시 중복 방지
    const parsedTypesResult = await typeGenerator.generateTypeDefinitions(typeDefinitions);

    // 파싱된 타입 이름 목록 (중첩 타입 포함) - 중복 방지용
    const parsedTypeNames = parsedTypesResult.generatedTypeNames;

    // C# 타입 정의 생성 (API에서 추출된 타입) - 타입별 유닛 (중복 제외)
    // typeDefinitions와 parser도 전달하여 pending external types 해결에 사용
    // parsedTypesResult.stringEnumValues는 inline enum dedup용 (named enum과 값 셋 일치 시 alias)
    const apiTypeUnits = await typeGenerator.generateTypes(
      apis,
      parsedTypeNames,
      typeDefinitions,
      parser,
      parsedTypesResult.stringEnumValues,
    );

    // 타입 유닛을 카테고리별로 버킷팅 (API 추출 타입 + 파싱된 도메인 타입)
    const allTypeUnits: GeneratedTypeUnit[] = [...apiTypeUnits, ...parsedTypesResult.units];

    // 'Other'로 남은 유닛(파싱된 도메인 타입 + 루프 밖 pending 타입)을 API 이름 프리픽스로 재귀속.
    // 이들 대부분은 `{ApiPascalName}{Options|Result|Params|Response|...}` 규약을 따르므로
    // 가장 긴 API 이름이 word-boundary(다음 글자 대문자/끝)로 prefix 매칭되면 그 API의 카테고리로 옮긴다.
    // 매칭 실패한 진짜 공용 타입(LocationCoords, Money 등)은 'Other'에 남는다(오분류 위험 없음).
    // 참조 그래프 기반 delta-snapshot 분류(generateTypes)는 더 정확하므로 건드리지 않는다.
    const apiCatByPascal = new Map<string, string>();
    for (const api of apis) {
      apiCatByPascal.set(api.pascalName, resolveApiCategory(api, false));
    }
    const apiNamesLongestFirst = Array.from(apiCatByPascal.keys()).sort((a, b) => b.length - a.length);
    for (const unit of allTypeUnits) {
      if (unit.category !== DEFAULT_CATEGORY) continue;
      for (const apiName of apiNamesLongestFirst) {
        if (unit.name === apiName) {
          unit.category = apiCatByPascal.get(apiName)!;
          break;
        }
        if (unit.name.length > apiName.length && unit.name.startsWith(apiName)) {
          const next = unit.name.charAt(apiName.length);
          if (next >= 'A' && next <= 'Z') {
            unit.category = apiCatByPascal.get(apiName)!;
            break;
          }
        }
      }
    }

    const typeUnitsByCategory = new Map<string, GeneratedTypeUnit[]>();
    for (const unit of allTypeUnits) {
      if (!typeUnitsByCategory.has(unit.category)) {
        typeUnitsByCategory.set(unit.category, []);
      }
      typeUnitsByCategory.get(unit.category)!.push(unit);
    }

    // 카테고리별 타입 파일 헤더 (copyright file= 속성은 실제 파일명으로)
    const buildTypeFileHeader = (fileName: string) => `// -----------------------------------------------------------------------
// <copyright file="${fileName}" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
`;
    const typeFileFooter = `}
`;

    // 카테고리 순서대로 파일 생성 (CATEGORY_ORDER 우선, 그 외 알파벳 순 — generateCategoryFiles와 동일 규약)
    const typeFiles = new Map<string, string>();
    const orderedTypeCategories: string[] = [];
    const processedTypeCategories = new Set<string>();
    for (const category of CATEGORY_ORDER) {
      if (typeUnitsByCategory.has(category)) {
        orderedTypeCategories.push(category);
        processedTypeCategories.add(category);
      }
    }
    for (const category of Array.from(typeUnitsByCategory.keys())
      .filter(c => !processedTypeCategories.has(c))
      .sort()) {
      orderedTypeCategories.push(category);
    }
    for (const category of orderedTypeCategories) {
      const units = typeUnitsByCategory.get(category)!;
      const fileName = `AIT.Types.${category}.cs`;
      const body = units.map(u => u.code).join('\n\n');
      typeFiles.set(fileName, buildTypeFileHeader(fileName) + body + '\n' + typeFileFooter);
    }
    console.log(picocolors.green(`✓ ${typeFiles.size}개 타입 파일 (AIT.Types.{Category}.cs, ${typeDefinitions.length}개 타입 정의)`));

    // jslib 파일들 생성 (TypeScript 포함)
    const jslibResult = await jslibGenerator.generateWithTypescript(apis, options.tag);
    const jslibFiles = jslibResult.jslibFiles;
    console.log(picocolors.green(`✓ ${jslibFiles.size}개 jslib 파일`));

    // 8. jslib TypeScript 타입 검사
    console.log(picocolors.cyan('\n🔍 jslib TypeScript 타입 검사 중...'));
    const cacheDir = path.join(process.cwd(), '.cache', 'jslib-typecheck');
    const typeCheckResult = await typeCheckBridgeCode(jslibResult.typescriptFiles, cacheDir);

    if (!typeCheckResult.success) {
      printTypeCheckResult(typeCheckResult);
      console.error(picocolors.red('\n❌ jslib TypeScript 타입 검사 실패\n'));
      console.error(picocolors.yellow('타입 오류를 수정하세요. web-framework API와의 타입 불일치가 있을 수 있습니다.'));
      console.error(picocolors.gray(`디버깅용 TypeScript 파일: ${cacheDir}`));
      // 에러 시 캐시를 보존하여 디버깅 가능하도록 함
      process.exit(1);
    }
    console.log(picocolors.green(`✓ TypeScript 타입 검사 통과 (${typeCheckResult.checkedFiles.length}개 파일)`));
    console.log(picocolors.gray(`  (검사 파일 보기: ${cacheDir})`));
    // 성공 시에도 캐시 보존 (디버깅/검토용)

    // 9. 완전성 검증
    console.log(picocolors.cyan('\n🔍 API 완전성 검증 중...'));
    const completenessValidation = validateCompleteness(apis, generatedCodes);
    if (!completenessValidation.success) {
      console.error(picocolors.red('\n❌ API 완전성 검증 실패\n'));
      for (const error of completenessValidation.errors) {
        console.error(error.message);
      }
      process.exit(1);
    }
    console.log(picocolors.green('✓ API 완전성 확인'));

    // 9. 파일 출력
    console.log(picocolors.cyan('\n📝 파일 쓰기 중...'));
    const outputDir = path.resolve(process.cwd(), options.output);
    const pluginsDir = path.join(outputDir, 'Plugins');

    // 새로 생성될 파일 목록 수집
    const newCsFiles = new Set<string>([
      path.join(outputDir, 'AIT.cs'),
      path.join(outputDir, 'AITCore.cs'),
      path.join(outputDir, 'AIT.Screen.cs'), // Screen 수동 API
      ...Array.from(categoryFiles.keys()).map(f => path.join(outputDir, f)),
      ...Array.from(typeFiles.keys()).map(f => path.join(outputDir, f)), // AIT.Types.{Category}.cs
    ]);
    const newJslibFiles = new Set<string>([
      ...Array.from(jslibFiles.keys()).map(f => path.join(pluginsDir, f)),
      path.join(pluginsDir, 'AppsInToss-Screen.jslib'), // Screen 수동 API
      path.join(pluginsDir, 'AppsInToss-Core.jslib'), // Core 수동 API (verbose 로깅 스위치)
    ]);

    // 1. 기존 .meta 파일 수집 (삭제 전에)
    console.log(picocolors.yellow('  📋 기존 .meta 파일 수집 중...'));
    const existingMetas = await collectMetaFiles(outputDir);
    console.log(picocolors.gray(`     ${existingMetas.size}개 .meta 파일 발견`));

    // 2. 기존 생성 파일 삭제 (재현성 보장)
    // 삭제될 파일 중 새로 생성되지 않는 파일의 .meta도 삭제
    console.log(picocolors.yellow('  🗑️  기존 생성 파일 삭제 중...'));
    try {
      const files = await fs.readdir(outputDir).catch(() => []);
      for (const file of files) {
        const filePath = path.join(outputDir, file);
        // .cs 파일 처리
        if (file.endsWith('.cs')) {
          await fs.rm(filePath, { force: true });
          // 새로 생성될 파일이 아니면 .meta도 삭제
          if (!newCsFiles.has(filePath)) {
            await fs.rm(filePath + '.meta', { force: true });
            existingMetas.delete(filePath);
          }
        }
      }

      // Plugins 디렉토리 내 jslib 파일들 처리
      const pluginFiles = await fs.readdir(pluginsDir).catch(() => []);
      for (const file of pluginFiles) {
        const filePath = path.join(pluginsDir, file);
        if (file.endsWith('.jslib')) {
          await fs.rm(filePath, { force: true });
          // 새로 생성될 파일이 아니면 .meta도 삭제
          if (!newJslibFiles.has(filePath)) {
            await fs.rm(filePath + '.meta', { force: true });
            existingMetas.delete(filePath);
          }
        }
      }

      console.log(picocolors.green('  ✓ 기존 파일 삭제 완료'));
    } catch {
      // 파일이 없으면 무시
    }

    await fs.mkdir(outputDir, { recursive: true });
    await fs.mkdir(pluginsDir, { recursive: true });

    // 3. 메인 AIT.cs 쓰기 (partial class 선언만)
    const aitCsPath = path.join(outputDir, 'AIT.cs');
    await fs.writeFile(aitCsPath, mainFile);
    await ensureMetaFile(aitCsPath, existingMetas, 'cs');
    console.log(picocolors.green(`  ✓ AIT.cs`));

    // 4. 카테고리별 API partial class 파일들 쓰기
    for (const [fileName, content] of categoryFiles.entries()) {
      const filePath = path.join(outputDir, fileName);
      await fs.writeFile(filePath, content);
      await ensureMetaFile(filePath, existingMetas, 'cs');
      console.log(picocolors.green(`  ✓ ${fileName}`));
    }

    // 5. AITCore.cs 쓰기 (내부 인프라)
    const corePath = path.join(outputDir, 'AITCore.cs');
    await fs.writeFile(corePath, coreFile);
    await ensureMetaFile(corePath, existingMetas, 'cs');
    console.log(picocolors.green(`  ✓ AITCore.cs`));

    // 6. AIT.Types.{Category}.cs 쓰기 (카테고리별 타입 정의)
    for (const [fileName, content] of typeFiles.entries()) {
      const filePath = path.join(outputDir, fileName);
      await fs.writeFile(filePath, content);
      await ensureMetaFile(filePath, existingMetas, 'cs');
      console.log(picocolors.green(`  ✓ ${fileName}`));
    }

    // 7. jslib 파일들 쓰기
    for (const [fileName, content] of jslibFiles.entries()) {
      const filePath = path.join(pluginsDir, fileName);
      await fs.writeFile(filePath, content);
      await ensureMetaFile(filePath, existingMetas, 'jslib');
      console.log(picocolors.green(`  ✓ Plugins/${fileName}`));
    }

    // 8. Screen 수동 API 파일 쓰기 (브라우저 API - web-framework 외부)
    console.log(picocolors.cyan('\n🖥️  Screen 수동 API 생성 중...'));
    const screenCsPath = path.join(outputDir, 'AIT.Screen.cs');
    await fs.writeFile(screenCsPath, generateScreenManualCs());
    await ensureMetaFile(screenCsPath, existingMetas, 'cs');
    console.log(picocolors.green(`  ✓ AIT.Screen.cs`));

    const screenJslibPath = path.join(pluginsDir, 'AppsInToss-Screen.jslib');
    await fs.writeFile(screenJslibPath, generateScreenManualJslib());
    await ensureMetaFile(screenJslibPath, existingMetas, 'jslib');
    console.log(picocolors.green(`  ✓ Plugins/AppsInToss-Screen.jslib`));

    // Core 수동 API 파일 쓰기 (verbose 로깅 스위치 브릿지 - web-framework 외부)
    const coreJslibPath = path.join(pluginsDir, 'AppsInToss-Core.jslib');
    await fs.writeFile(coreJslibPath, generateCoreManualJslib());
    await ensureMetaFile(coreJslibPath, existingMetas, 'jslib');
    console.log(picocolors.green(`  ✓ Plugins/AppsInToss-Core.jslib`));

    // 9. unity-bridge.ts 생성 (WebGLTemplates/AITTemplate/BuildConfig~/)
    console.log(picocolors.cyan('\n🌉 Unity Bridge 생성 중...'));
    const unityBridgeContent = generateUnityBridge(apis);
    const unityBridgePath = path.resolve(outputDir, '../../WebGLTemplates/AITTemplate/BuildConfig~/unity-bridge.ts');
    await fs.writeFile(unityBridgePath, unityBridgeContent);
    console.log(picocolors.green(`  ✓ unity-bridge.ts`));

    // 10. 요약 출력
    printSummary(apis, generatedCodes);

    const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);
    console.log(picocolors.green(picocolors.bold(`\n✅ 생성 완료! (${elapsed}s)\n`)));
  } catch (error) {
    console.error(
      picocolors.red(`\n❌ 생성 실패: ${error instanceof Error ? error.message : String(error)}\n`)
    );
    process.exit(1);
  }
}

// CLI 설정
program
  .name('generate-unity-sdk')
  .description('Unity SDK 자동 생성 도구')
  .version('1.0.0');

program
  .command('generate')
  .description('node_modules의 @apps-in-toss/web-framework에서 Unity SDK 생성')
  .option('-o, --output <path>', '출력 디렉토리', '../Runtime/SDK')
  .option('--source-path <path>', '(옵션) 로컬 web-framework 경로 (개발/테스트용)')
  .action((options) => {
    generate({
      tag: 'next', // 더 이상 사용하지 않음 (node_modules에서 가져옴)
      output: options.output,
      skipClone: !!options.sourcePath,
      sourcePath: options.sourcePath,
    });
  });

program
  .command('format')
  .description('생성된 C# 파일들을 CSharpier로 포맷팅')
  .action(formatCommand);

program.parse();
