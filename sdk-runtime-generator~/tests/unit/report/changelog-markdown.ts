/**
 * API Changelog 마크다운 렌더러
 *
 * ChangelogModel(changelog-model.ts)을 GitBook에 업로드할 수 있는 마크다운 문서로
 * 렌더링한다. HTML 리포트(changelog-html.ts)와 동일한 모델을 소비하므로 두 출력은
 * 항상 일관된 데이터(EXCLUDED_APIS 필터 포함)를 보여준다.
 *
 * 결정적 출력: 타임스탬프·난수 등 입력 모델 외의 값을 사용하지 않는다 — 같은
 * ChangelogModel이면 항상 같은 문자열을 반환한다(golden 테스트 전제).
 */

import type { ChangelogModel, VersionDiff } from './changelog-model.js';

export type ChangelogMarkdownDialect = 'commonmark' | 'gitbook';

export interface ChangelogMarkdownOptions {
  dialect: ChangelogMarkdownDialect;
  /** 결과 문자열의 최대 UTF-8 바이트 크기 (기본 200,000) */
  maxBytes?: number;
  /**
   * dtsSource가 'sibling'이 아니어서(예: web-bridge → webview-bridge 리네임으로 sibling
   * 탐색 실패 → pnpm store 폴백) API 표면이 근사(approximate)된 버전 목록.
   * 인트로 문단에 결정적 문구로 명시해, 해당 버전의 diff가 비어 있어도 이유를 알 수 있게 한다.
   */
  approximatedVersions?: string[];
}

const DEFAULT_MAX_BYTES = 200_000;

/**
 * ChangelogModel을 마크다운 문자열로 렌더링한다.
 *
 * - commonmark만 구현되어 있다. gitbook dialect는 `{% hint %}` 등 GitBook 전용 문법을
 *   쓰는데, 이 프로젝트의 pages.yml이 Jekyll `{% raw %}` 처리와 함께 마크다운을
 *   서빙하므로 지금은 GitBook 전용 문법을 도입하지 않는다 — 값을 넘기면 미구현으로
 *   throw한다.
 * - 선두에 H1을 두지 않는다 (GitBook 페이지 제목은 별도 필드로 관리된다). 문서는
 *   짧은 소개 문단 다음 H2/H3 구조로 이어진다.
 * - 구성: 최신 버전이 위로 오도록 정렬, major 버전별로 그룹핑된 버전 전이 diff
 *   (추가/제거/변경 API), 마지막으로 최신 버전의 API 카탈로그.
 * - 각 major 그룹 안에서 변화가 있는 전이만 H3 섹션(추가/변경/제거 목록 포함)으로 펼치고,
 *   변화 없는 전이는 "변경 없음: vA → vB · vC → vD" 형태로 그룹 끝에 한 줄로 압축한다
 *   (GitBook에 올라가는 문서라 가독성을 위해 — HTML 리포트가 빈 diff 카드를 아예
 *   숨기는 것과 대응되는 마크다운 쪽 처리). 그룹 전체가 빈 전이뿐이면 헤더와 이 한
 *   줄만 남는다.
 */
export function generateChangelogMarkdown(
  model: ChangelogModel,
  options: ChangelogMarkdownOptions,
): string {
  if (options.dialect !== 'commonmark') {
    throw new Error(
      `지원하지 않는 마크다운 dialect입니다: '${options.dialect}' (현재 commonmark만 구현되어 있습니다)`,
    );
  }
  const maxBytes = options.maxBytes ?? DEFAULT_MAX_BYTES;

  const { versions, apis, diffs, catalog } = model;
  if (versions.length === 0) {
    throw new Error('ChangelogModel에 버전 데이터가 없어 changelog 마크다운을 생성할 수 없습니다.');
  }

  const latestVersion = versions[versions.length - 1];
  const displayName = (name: string): string => apis[name]?.displayName ?? name;

  const lines: string[] = [];

  lines.push(
    `Apps in Toss Unity SDK가 노출하는 C# API 표면이 \`@apps-in-toss/web-framework\` 버전에 따라 ` +
    `어떻게 달라지는지 정리한 자동 생성 리포트입니다. 최신 버전(v${latestVersion})이 최상단에 오도록 정렬되어 있습니다.`,
  );
  lines.push('');

  if (options.approximatedVersions && options.approximatedVersions.length > 0) {
    const sortedApproximated = [...options.approximatedVersions].sort((a, b) =>
      a.localeCompare(b, undefined, { numeric: true }),
    );
    lines.push(
      `다음 버전은 패키지 리네임 등으로 sibling 탐색에 실패해 pnpm store 폴백으로 API 표면을 근사함: ` +
      sortedApproximated.map(v => `v${v}`).join(', '),
    );
    lines.push('');
  }

  // 버전 전이 diff — 최신 → 과거 순, major 버전 그룹으로 묶는다.
  const diffsDesc = [...diffs].reverse();
  const majorGroups = new Map<string, VersionDiff[]>();
  for (const diff of diffsDesc) {
    const major = `v${diff.to.split('.')[0]}.x`;
    if (!majorGroups.has(major)) majorGroups.set(major, []);
    majorGroups.get(major)!.push(diff);
  }

  if (majorGroups.size === 0) {
    lines.push('## 버전 전이');
    lines.push('');
    lines.push('감지된 버전 전이 diff가 없습니다 (설치된 버전이 1개뿐이거나 변화가 없습니다).');
    lines.push('');
  } else {
    const isEmptyDiff = (d: VersionDiff): boolean =>
      d.added.length === 0 && d.removed.length === 0 && d.modified.length === 0;

    for (const [major, groupDiffs] of majorGroups) {
      lines.push(`## ${major}`);
      lines.push('');

      // 변화가 있는 전이만 H3 섹션으로 렌더한다. 변화 없는 전이(예: pnpm store 폴백으로
      // 직전 버전과 동일한 API 표면을 읽게 된 경우)를 전부 풀 섹션으로 펼치면 문서
      // 대부분이 "변경 없음." 노이즈가 된다 — GitBook에 올라가는 문서라 가독성이 중요하므로
      // 이들은 그룹 끝에 한 줄로 압축한다(아래 emptyDiffs 처리).
      const nonEmptyDiffs = groupDiffs.filter(d => !isEmptyDiff(d));
      const emptyDiffs = groupDiffs.filter(isEmptyDiff);

      for (const diff of nonEmptyDiffs) {
        lines.push(`### v${diff.from} → v${diff.to}`);
        lines.push('');
        lines.push(
          `API 총 ${diff.totalApis}개 · 추가 ${diff.added.length} · 변경 ${diff.modified.length} · 제거 ${diff.removed.length}`,
        );
        lines.push('');

        if (diff.added.length > 0) {
          lines.push('**추가된 API**');
          lines.push('');
          for (const name of diff.added) {
            lines.push(`- \`${displayName(name)}\``);
          }
          lines.push('');
        }

        if (diff.modified.length > 0) {
          lines.push('**변경된 API**');
          lines.push('');
          for (const m of diff.modified) {
            // changeText에 System.Action<string> 같은 타입 전이가 포함될 수 있어, 백틱 밖에
            // 두면 <string>이 CommonMark에 의해 raw HTML 태그로 해석되어 삼켜질 수 있다.
            // 코드 스팬으로 감싸 안전하게 만든다.
            const changeText = m.changes.map(c => c.description).join('; ');
            lines.push(`- \`${displayName(m.name)}\`: \`${changeText}\``);
          }
          lines.push('');
        }

        if (diff.removed.length > 0) {
          lines.push('**제거된 API**');
          lines.push('');
          for (const name of diff.removed) {
            lines.push(`- \`${displayName(name)}\``);
          }
          lines.push('');
        }
      }

      // 빈 전이는 "API 총 N개" 같은 정보 가치 없는 표기 없이, groupDiffs와 동일하게
      // 최신 우선 순서로 한 줄에 압축한다. 그룹 전체가 빈 전이뿐이면(예: v3.x — sibling
      // web-bridge가 없어 pnpm store 폴백으로 직전 버전과 동일하게 근사된 경우) 이 한
      // 줄만 남고, 그래도 해당 버전이 문서에 나타난다는 사실 자체가 핵심이다.
      if (emptyDiffs.length > 0) {
        const compact = emptyDiffs.map(d => `v${d.from} → v${d.to}`).join(' · ');
        lines.push(`변경 없음: ${compact}`);
        lines.push('');
      }
    }
  }

  // 최신 버전 API 카탈로그만 수록 (과거 버전 카탈로그는 문서 크기 상 생략)
  lines.push(`## v${latestVersion} API 카탈로그`);
  lines.push('');
  const latestCatalog = catalog.get(latestVersion);
  if (latestCatalog && latestCatalog.size > 0) {
    for (const [category, names] of latestCatalog) {
      lines.push(`### ${category}`);
      lines.push('');
      for (const name of names) {
        lines.push(`- \`${displayName(name)}\``);
      }
      lines.push('');
    }
  } else {
    lines.push('카탈로그 데이터가 없습니다.');
    lines.push('');
  }

  const content = lines.join('\n').replace(/\n{3,}/g, '\n\n').trimEnd() + '\n';

  const byteLength = Buffer.byteLength(content, 'utf-8');
  if (byteLength > maxBytes) {
    throw new Error(
      `API changelog 마크다운 결과가 최대 크기(${maxBytes} bytes)를 초과했습니다: 실제 ${byteLength} bytes.\n` +
      `  구성(예: 최신 버전 카탈로그 범위)을 줄이거나 maxBytes 옵션을 조정하세요.`,
    );
  }

  return content;
}
