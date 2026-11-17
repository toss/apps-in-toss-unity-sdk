/**
 * API Changelog 생성 스크립트
 *
 * package.json devDependencies에 등록된 모든 web-framework-X.Y.Z alias 버전을
 * discovery.ts로 해석하고(하나라도 실패하면 즉시 throw — 조용한 스킵 금지),
 * TypeScriptParser(번들 dist/index.d.ts 입력)로 파싱한 뒤 changelog-model을 거쳐 HTML·마크다운 리포트를
 * reports/에 생성한다.
 *
 * 사용법: pnpm changelog:generate
 */

import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { CATEGORY_ORDER } from '../../../src/categories.js';
import type { ParsedAPI } from '../../../src/types.js';
import {
  discoverInstalledVersions,
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
  for (const version of installedVersions) {
    console.log(`  파싱 중: v${version}`);
    // resolveVersionPaths는 번들 dist/index.d.ts를 못 찾으면 명확한 한국어 메시지로 throw한다 (조용한 스킵 금지)
    const paths = await resolveVersionPaths(version);
    versionApis.set(version, await createParserForVersion(paths).parseAPIs());
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
