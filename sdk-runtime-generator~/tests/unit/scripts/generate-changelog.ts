/**
 * API Changelog 생성 스크립트
 *
 * package.json devDependencies에 등록된 모든 web-framework-X.Y.Z alias 버전을
 * discovery.ts로 해석하고(하나라도 실패하면 즉시 throw — 조용한 스킵 금지),
 * 기존 TypeScriptParser로 파싱한 뒤 changelog-model을 거쳐 HTML·마크다운 리포트를
 * reports/에 생성한다.
 *
 * 사용법: pnpm changelog:generate
 */

import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { FRAMEWORK_APIS, CATEGORY_ORDER } from '../../../src/categories.js';
import type { ParsedAPI } from '../../../src/types.js';
import {
  discoverInstalledVersions,
  hasFrameworkApis,
  resolveVersionPaths,
  createParserForVersion,
  assertWebFrameworkAliasInvariant,
} from '../../../src/discovery.js';
import { buildChangelogModel } from '../report/changelog-model.js';
import { generateChangelogHTML } from '../report/changelog-html.js';
import { generateChangelogMarkdown } from '../report/changelog-markdown.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// reports/는 sdk-runtime-generator~/.gitignore 대상 (커밋되지 않음). CI가 이 결과물을
// Documentation~/changelog/로 복사해 커밋한다 (.github/workflows/update-changelog.yml 참조).
const REPORTS_DIR = path.resolve(__dirname, '../../..', 'reports');

async function main() {
  console.log('\n📊 API Changelog 생성 시작\n');

  // invariant: devDependencies alias 최고 버전 == dependencies 버전.
  // (web-framework 3.0.1 누락 버그 — 메인 의존성만 올리고 alias 추가를 깜빡하면
  //  changelog 파이프라인이 최신 버전을 조용히 못 찾는다. 여기서 즉시 실패시킨다.)
  assertWebFrameworkAliasInvariant();

  const installedVersions = discoverInstalledVersions();
  if (installedVersions.length === 0) {
    throw new Error(
      '설치된 web-framework-X.Y.Z alias가 없습니다 (node_modules 확인). pnpm install을 먼저 실행하세요.',
    );
  }
  console.log(`  대상 버전: ${installedVersions.map(v => `v${v}`).join(', ')}\n`);

  const versionApis = new Map<string, ParsedAPI[]>();
  // dtsSource가 'sibling'이 아닌(=pnpm store 폴백 등으로 API 표면을 근사한) 버전 목록.
  // 마크다운 리포트 인트로에 명시해, 해당 버전의 diff가 비어 있어도 이유를 알 수 있게 한다.
  const approximatedVersions: string[] = [];
  for (const version of installedVersions) {
    console.log(`  파싱 중: v${version}`);
    // resolveVersionPaths는 dtsDir을 못 찾으면 명확한 한국어 메시지로 throw한다 —
    // 과거처럼 "if (!paths.dtsDir) continue"로 조용히 스킵하지 않는다.
    const paths = await resolveVersionPaths(version);
    // 'sibling'과 'self-bundle' 모두 그 버전 자신의 실제 .d.ts를 정확히 반영한다
    // (self-bundle: web-framework 3.x+가 별도 web-bridge sibling 없이 자체
    // dist/index.d.ts에 전체 API 표면을 번들링 — discovery.ts detectSelfContainedDts
    // 참고). "근사(폴백)"로 표시해야 하는 건 stale 버전을 근사로 사용하는
    // pnpm-store/package-dir뿐이다.
    if (paths.dtsSource !== 'sibling' && paths.dtsSource !== 'self-bundle') {
      approximatedVersions.push(version);
    }
    const frameworkApiNames = hasFrameworkApis(version) ? FRAMEWORK_APIS : [];
    // self-bundle(3.x+) index.d.ts는 deprecated 최상위 함수(checkoutPayment 등)를 여전히
    // export하고, getServerTime/fetchAlbumPhotos 류는 intersection/제네릭 wrapper
    // 화살표 타입(예: `(() => X) & { isSupported }`, `PermissionFunctionWithDialog<...>`)
    // 으로 선언되어 있다 — includeDeprecatedGlobals/includeWrappedCallables로 둘 다
    // 감지해 diff가 "제거"가 아니라 "변경(deprecated 전환)"으로 잡히게 한다.
    // sibling(2.x)은 opt-in하지 않는다 — 같은 이름이 file-per-API 경로에서 이미
    // 파싱되므로 여기서 켜면 중복이 생긴다(detection.ts isWrappedCallableType 참고).
    const isSelfBundle = paths.dtsSource === 'self-bundle';
    const apis = await createParserForVersion(paths).parseAPIs(frameworkApiNames, {
      includeDeprecatedGlobals: isSelfBundle,
      includeWrappedCallables: isSelfBundle,
    });
    versionApis.set(version, apis);
  }
  if (approximatedVersions.length > 0) {
    console.log(`  근사(폴백) 버전: ${approximatedVersions.map(v => `v${v}`).join(', ')}`);
  }

  const model = buildChangelogModel(versionApis, CATEGORY_ORDER);

  await fs.mkdir(REPORTS_DIR, { recursive: true });

  const html = generateChangelogHTML(model);
  const htmlPath = path.join(REPORTS_DIR, 'api-changelog.html');
  await fs.writeFile(htmlPath, html, 'utf-8');
  console.log(`\n  ✅ ${htmlPath}`);

  const markdown = generateChangelogMarkdown(model, {
    dialect: 'commonmark',
    maxBytes: 200_000,
    approximatedVersions,
  });
  const mdPath = path.join(REPORTS_DIR, 'api-changelog.md');
  await fs.writeFile(mdPath, markdown, 'utf-8');
  console.log(`  ✅ ${mdPath}`);

  const latestVersion = model.versions[model.versions.length - 1];
  const latestDiff = model.diffs.find(d => d.to === latestVersion);
  if (latestDiff) {
    console.log(
      `\n  최신 전이 v${latestDiff.from} → v${latestDiff.to}: ` +
      `추가 ${latestDiff.added.length} · 변경 ${latestDiff.modified.length} · 제거 ${latestDiff.removed.length}`,
    );
  } else {
    console.log(`\n  v${latestVersion}에 대한 버전 전이 diff가 없습니다 (버전 1개뿐이거나 직전 버전과 변화 없음).`);
  }

  console.log('\n✅ API Changelog 생성 완료\n');
}

main().catch((error) => {
  console.error(`\n❌ API Changelog 생성 실패: ${error instanceof Error ? error.message : String(error)}\n`);
  process.exit(1);
});
