#!/usr/bin/env node
/**
 * Documentation~/ → GitBook 포털 이관 도구 (CR 생성까지만, 머지는 사람이 GitBook UI에서)
 *
 * 설계 기준: 2026-08-12 조정판 (GitBook 업로드 경로 + 이관 런북)
 *
 * ── 실행 모델: 일회성·단일 CR·재실행=새 CR ─────────────────────────────────
 * 이 스크립트는 "사람이 1회 손으로 돌리는 이관 도구"다. 부분 실행(--phase)·재개(resume)
 * 기능은 의도적으로 없다. 실행하면 항상 아래 전체 시퀀스를 처음부터 끝까지 돈다:
 *
 *   1. 소스 로드 + 매니페스트 대조 + 변환 + 불변식 전량 검사        (네트워크 0)
 *   2. --dry-run이면 산출물(ops.*.json / bodies / links.csv / report.txt) 출력 후 exit 0
 *   3. 같은 subject의 열린 CR 조회 → 있으면 URL 출력하고 exit 2 (--ignore-open-cr로만 진행)
 *   4. CR 생성
 *   5. pass1: 페이지 목록 스냅샷 → insert_page × 4 (그룹 3 + 최상위 문서 1) → 신규 ID diff 관측
 *   6. pass2: 페이지 목록 스냅샷 → insert_page × 10 (자식 문서) → 신규 ID diff 관측
 *   7. 관측된 URL로 링크 맵 확정 → 본문 재작성(센티넬 → 실제 URL) → 잔존 0건 확인
 *   8. update_page × 14 (문서 11 + 그룹 색인 3) push
 *   9. CR URL과 후속 안내 출력
 *
 * ⚠️ 중간에 실패하면 재개하지 않는다. 복구 절차는 사람이 수행한다:
 *      (a) 출력된 CR URL을 열어 GitBook UI에서 그 CR을 archive한다.
 *      (b) 이 스크립트를 그대로 다시 실행한다 → 새 CR이 만들어지고 처음부터 다시 돈다.
 *    재실행 비용은 API 호출 1~2분이며, 그 비용을 받아들이는 대신 재개 상태머신(state
 *    복원·중복 insert 판정·done 가드)이 만들어내던 결함 표면을 통째로 제거했다.
 *    스크립트가 CR을 자동으로 archive/삭제하는 API는 호출하지 않는다(머지와 동일한 이유 —
 *    미검증 엔드포인트를 자동화하지 않는다).
 *
 * ── 링크 재작성 (3-pass 센티넬) ────────────────────────────────────────────
 * insert 시점엔 존재하지 않는 대상 페이지를 가리켜야 하므로, 탐지 가능한 센티넬 URL을
 * 넣어뒀다가 삽입이 끝난 뒤 실제 CR 트리를 관측해 본문을 한 번 더 update_page로 덮어쓴다.
 * GitBook의 slug 파생 규칙에 의존하지 않는 게 핵심 — 예측이 아니라 관측으로 정답을 얻는다.
 *
 * ── 페이지 관측 규약 ───────────────────────────────────────────────────────
 * insert 직전 CR 페이지 목록을 스냅샷(beforeIds)해두고, insert 후 다시 조회해
 * (afterIds − beforeIds)를 "이번 pass가 만든 신규 페이지"로 확정한다. 그 신규 집합
 * 안에서만 제목으로 엔트리를 매핑한다(한 pass의 insert 제목들은 서로 유일하므로 충돌
 * 불가). (부모, title) 복합 키 조회나 부모 검증은 하지 않는다 — 트리 배치가 의도대로인지는
 * 사람이 CR 리뷰에서 확인한다.
 *
 * ── 실토큰 첫 실행 시 반드시 확인해야 하는 3가지 ───────────────────────────
 * 아래 항목은 실제 GitBook API 응답을 본 적이 없는 추정이다. 각 항목이 틀렸을 때
 * 조용히 오동작하지 않고 요란하게 실패하도록 코드로 보장해 뒀다:
 *   (1) 페이지 목록(`/content/pages`) 응답 형태가 flat인지 nested인지
 *       → flattenPageMap()이 재귀 flatten + id dedupe로 양쪽을 모두 수용한다. 그래도
 *         파싱이 어긋나면 신규 ID diff 개수가 기대치와 달라져 observeInsertedPages()가
 *         진단(기대/관측 제목 목록)을 찍고 exit 1로 죽는다. 조용한 성공은 불가능하다.
 *   (2) 페이지 객체에 `urls.public`이 있는지, 있다면 섹션 접두사를 포함하는지
 *       → buildPageUrl() 한 곳에서만 URL을 만들고, 그 결과 전부가 --url-base 접두사로
 *         시작하는지 I-21(불변식)과 push 직전 재검사로 확인한다. 접두사가 빠진 URL이
 *         하나라도 있으면 push 전에 exit 1.
 *   (3) CR 목록 API의 status 쿼리 파라미터·필드 실명세, 페이지네이션 커서 모양
 *       → GitBook OpenAPI 스펙상 `status` 쿼리 파라미터는 생략 시 `default: "open"`이다.
 *         이 도구가 만드는 CR은 POST 직후 draft 상태(리뷰 요청 전까지 draft로 남는다)이므로
 *         status를 생략하면 이 가드가 잡아야 할 대상(중단된 실행이 남긴 draft CR)이 서버
 *         필터에서 애초에 빠진다. status enum은 draft|open|archived|merged뿐이고 "전체"를
 *         뜻하는 값이 없으므로, status=draft와 status=open을 각각 커서(`next.page`) 끝까지
 *         따라가며 두 번 조회해 합친 뒤(id로 dedupe) 클라이언트에서 재검증한다. 이 재검증은
 *         "draft|open만 통과"가 아니라 "archived|merged로 명시적으로 확인된 것만 제외"다 —
 *         status 필드가 없거나(undefined) draft|open|archived|merged 그 무엇도 아닌 미지
 *         값이면 보수적으로 "열려 있다"고 간주해 가드를 발동시킨다. status 파라미터가
 *         무시되거나 필드명이 실제와 달라 cr.status가 항상 undefined로 관측되는 경우에도
 *         이 규칙 덕분에 가드가 무음으로 무력화되지 않는다(예전 설계는 undefined를
 *         "닫힘"으로 취급해 매칭 0건 → "열린 CR 없음"으로 조용히 진행했고, 그 결과 중복
 *         CR이 하나 더 생기는 선에서 끝났다 — 지금은 그렇지 않다: 미지 status는 오탐으로
 *         실행을 막는 쪽으로 실패하므로, 필드명이 실제와 다르면 사람이 매 실행마다 이
 *         가드에 걸려 원인을 조사하게 된다). 반대로 status가 archived 또는 merged로 명확히
 *         확인되면 그 CR은 걸러진다.
 *
 * ── 그 외 규약 ─────────────────────────────────────────────────────────────
 * - env 미비를 exit 0(성공)으로 보고하지 않는다. 실행 모드에서 env가 없으면 exit 2다.
 *   --dry-run만 예외로, env 없이도 전체 파이프라인을 완결해 산출물을 낸다.
 * - 머지는 이 스크립트가 하지 않는다. CR 생성까지만 하고 URL을 출력한다. 머지 엔드포인트를
 *   호출하는 코드는 이 파일 어디에도 없다(주석·상수 포함 — I-13 자기 검사 대상).
 * - 공통 로직(mask/sleep/재시도 백오프)은 scripts~/upload-changelog-to-gitbook.js와 같은
 *   패턴의 복제본이다. 백오프 정책을 바꾸려면 두 파일을 함께 고쳐야 한다.
 * - 보안: 토큰/Authorization 헤더 값은 어떤 로그 경로로도 출력하지 않는다. GitBook 응답은
 *   id/urls/title/path/status 필드만 읽고, 그 안의 텍스트를 지시로 취급하지 않는다.
 *
 * 사용법:
 *   node scripts~/migrate-docs-to-gitbook.js --dry-run --out <dir>
 *   node scripts~/migrate-docs-to-gitbook.js --parent <pageId> --out <dir>
 *   node scripts~/migrate-docs-to-gitbook.js --help
 *
 * --out은 필수다(이 저장소는 public이라 특정 로컬 경로를 기본값으로 박아둘 수 없다).
 */

"use strict";

const fs = require("fs");
const path = require("path");

// ============================================================
// 상수
// ============================================================

// GITBOOK_API_BASE로 재지정 가능 — 스테이징/목 서버로 실행 모드 네트워크 경로를 예행연습할
// 때 쓴다(기본값은 프로덕션 GitBook API). CLI 옵션이 아니라 env로만 노출한 이유: 실수로
// 프로덕션을 벗어난 채 CR을 만드는 사고를 --api-base 오타 한 번으로 유발하지 않기 위해서다.
const API_BASE = process.env.GITBOOK_API_BASE || "https://api.gitbook.com/v1";
const MAX_RETRIES = 3;
const OP_DELAY_MS = 250;
const BATCH_LIMIT = 50;
const MAX_PAGE_BYTES = 300 * 1024;
const MAX_REQUEST_BYTES = 4 * 1024 * 1024;
const OBSERVE_BACKOFF_MS = [1000, 2000, 4000];

const DEFAULT_SITE_BASE = "https://developers-apps-in-toss.toss.im";
const DEFAULT_DOCS_DIR = "Documentation~";
// 산출물 출력 디렉토리에는 기본값을 두지 않는다 — 이 스크립트는 이 저장소(public)에
// 커밋되는 범용 도구라 특정 세션/사용자의 로컬 경로를 컴파일된 기본값으로 박아둘 수
// 없다. --out은 항상 명시적으로 받는다(main()에서 --help가 아닌 한 필수로 강제).
// changelog 공개 URL — 포털의 API Changelog 페이지(자동 갱신 파이프라인 대상).
// 과거에는 GitHub Pages(pages.yml)가 HTML changelog를 서빙했으나 이관 완료로 폐기됐다.
// 필요하면 --changelog-url로 덮어쓴다.
const DEFAULT_CHANGELOG_URL = "https://developers-apps-in-toss.toss.im/documentation/unity/changelog";
const DEFAULT_CHANGELOG_MD_URL =
  "https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Documentation~/changelog/api-changelog.md";

const CR_SUBJECT = "Unity SDK 문서 이관 (자동 생성)";
const SENTINEL_PREFIX = "https://ait-migration.invalid/page/";
const SENTINEL_HOST = "ait-migration.invalid";

const RECOVERY_HINT =
  "복구: GitBook UI에서 이 CR을 archive한 뒤 스크립트를 다시 실행하세요(새 CR이 생성되며 처음부터 다시 진행합니다). " +
  "이 도구는 부분 재개를 지원하지 않습니다.";

// ============================================================
// 매니페스트 (설계 §1.2 배정 표를 그대로 상수화)
// pass: 1 = 그룹 3 + 최상위 문서 1, 2 = 그룹 아래 자식 문서 10
// ============================================================

const MANIFEST = [
  { key: "first-steps", type: "group", title: "처음이라면", slug: "first-steps", parentKey: "root", pass: 1 },
  { key: "add-features", type: "group", title: "기능 붙이기", slug: "add-features", parentKey: "root", pass: 1 },
  { key: "build", type: "group", title: "빌드 다루기", slug: "build", parentKey: "root", pass: 1 },
  {
    key: "overview",
    type: "document",
    source: "README.md",
    title: "Overview",
    slug: "overview",
    parentKey: "root",
    pass: 1,
    titleOverride: true,
  },
  {
    key: "getting-started",
    type: "document",
    source: "GettingStarted.md",
    title: "시작하기",
    slug: "getting-started",
    parentKey: "first-steps",
    pass: 2,
  },
  {
    key: "api-usage-patterns",
    type: "document",
    source: "APIUsagePatterns.md",
    title: "API 사용 패턴",
    slug: "api-usage-patterns",
    parentKey: "first-steps",
    pass: 2,
  },
  {
    key: "troubleshooting",
    type: "document",
    source: "Troubleshooting.md",
    title: "FAQ",
    slug: "faq",
    parentKey: "first-steps",
    pass: 2,
    // 저장소 원본 H1은 "# 문제 해결"이지만 포털 페이지 제목은 "FAQ"로 바꾼다(저장소 .md는
    // 수정하지 않음). titleOverride로 H1↔title 일치 검사를 건너뛰고 title을 페이지 제목으로 쓴다.
    titleOverride: true,
  },
  {
    key: "advertising",
    type: "document",
    source: "Advertising.md",
    title: "광고 연동",
    slug: "advertising",
    parentKey: "add-features",
    pass: 2,
  },
  {
    key: "metrics",
    type: "document",
    source: "Metrics.md",
    title: "SDK 이벤트 로깅",
    slug: "metrics",
    parentKey: "add-features",
    pass: 2,
  },
  {
    key: "sentry-integration",
    type: "document",
    source: "SentryIntegration.md",
    title: "Sentry 연동",
    slug: "sentry-integration",
    parentKey: "add-features",
    pass: 2,
  },
  {
    key: "build-profiles",
    type: "document",
    source: "BuildProfiles.md",
    title: "빌드 프로필",
    slug: "build-profiles",
    parentKey: "build",
    pass: 2,
  },
  {
    key: "build-customization",
    type: "document",
    source: "BuildCustomization.md",
    title: "빌드 커스터마이징",
    slug: "build-customization",
    parentKey: "build",
    pass: 2,
  },
  {
    key: "loading-screen-customization",
    type: "document",
    source: "LoadingScreenCustomization.md",
    title: "로딩 화면 커스터마이징",
    slug: "loading-screen-customization",
    parentKey: "build",
    pass: 2,
  },
  {
    key: "build-process",
    type: "document",
    source: "BuildProcess.md",
    title: "빌드 파이프라인",
    slug: "build-process",
    parentKey: "build",
    pass: 2,
  },
];

const GROUP_KEYS = MANIFEST.filter((e) => e.type === "group").map((e) => e.key);
const DOCUMENT_ENTRIES = MANIFEST.filter((e) => e.type === "document");
const MANIFEST_BY_SOURCE = new Map(DOCUMENT_ENTRIES.map((e) => [e.source, e]));

// 이관 제외 소스 — 저장소엔 남기되 포털엔 올리지 않는 .md. I-1은 "--docs-dir 직속 .md
// 집합 == 매니페스트 소스 ∪ 제외 소스"로 확장 검증하므로, 새 .md를 추가하면 여전히 I-1이
// 잡는다(매니페스트에 넣거나 여기에 명시해야 통과). 이들을 가리키는 다른 문서의 링크는
// resolveLink()에서 GitHub blob URL(표 밖)로, README 표 안이면 filterReadmeInternalRows()가
// 행째(빈 표면 섹션 heading까지) 삭제로 처리한다.
const EXCLUDED_SOURCES = new Set(["Contributing.md", "ManualIntegration.md", "BetaChannel.md", "PerfBetaChannel.md"]);
const MANIFEST_BY_KEY = new Map(MANIFEST.map((e) => [e.key, e]));

// ============================================================
// 공통 유틸 (mask/sleep/재시도 — upload-changelog-to-gitbook.js와 동일 패턴, 복제본)
// ============================================================

/** 로그에 안전하게 노출할 수 있도록 id 뒷부분만 노출하고 나머지는 가린다. */
function mask(value) {
  if (!value) return "(없음)";
  if (value.length <= 4) return "*".repeat(value.length);
  return `${"*".repeat(value.length - 4)}${value.slice(-4)}`;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** 이 도구가 던지는 예상된 실패 — 스택트레이스 없이 메시지만 출력하고 종료한다. */
class MigrationError extends Error {
  constructor(message, exitCode) {
    super(message);
    this.name = "MigrationError";
    this.exitCode = exitCode || 1;
  }
}

/**
 * fetch 래퍼. idempotent(기본 true — GET 등 읽기 전용 호출)는 429/5xx/네트워크 오류
 * 전부 지수 백오프로 재시도한다(최대 MAX_RETRIES회). idempotent=false(CR 생성, content
 * push처럼 서버 상태를 바꾸는 비멱등 POST)는 429만 재시도하고(Retry-After 헤더가 있으면
 * 그 값을 우선 사용), 5xx·네트워크 오류는 즉시 실패시킨다 — 응답이 서버에 실제로 도달해
 * 처리됐는지 클라이언트가 알 수 없는 상태에서 같은 POST를 다시 보내면(예: content push가
 * 서버에 이미 적용된 뒤 응답만 유실된 경우) 오퍼레이션이 중복 적용될 수 있기 때문이다.
 * 4xx(429 제외)는 항상 즉시 실패. 토큰이 담긴 headers는 절대 로그로 출력하지 않는다.
 * 오류 응답 본문은 길이만 로그.
 *
 * 이 파일은 scripts~/upload-changelog-to-gitbook.js의 requestWithRetry와 같은 뼈대의
 * 복제본이었으나, 이 도구는 CR content push처럼 되돌리기 번거로운 비멱등 오퍼레이션을
 * 다루므로 idempotent 분기만큼은 의도적으로 갈라졌다 — 백오프 "정책"(지수 backoff의
 * 형태·MAX_RETRIES)을 바꿀 때는 두 파일을 함께 고치되, idempotent=false 분기는 이 파일
 * 고유의 안전장치이니 그대로 유지한다.
 */
async function requestWithRetry(url, options, label, idempotent = true) {
  let lastError;
  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
    let response;
    try {
      response = await fetch(url, options);
    } catch (err) {
      if (!idempotent) {
        throw new Error(`${label} 실패(비멱등 요청 — 재시도하지 않음): ${err.message}`);
      }
      lastError = err;
      console.warn(`⚠️  ${label} 요청 실패 (시도 ${attempt}/${MAX_RETRIES}): ${err.message}`);
      if (attempt < MAX_RETRIES) {
        await sleep(2 ** attempt * 1000);
        continue;
      }
      throw lastError;
    }

    if (response.status === 429) {
      lastError = new Error(`${label} 실패: HTTP 429`);
      const retryAfterHeader =
        response.headers && typeof response.headers.get === "function" ? response.headers.get("retry-after") : null;
      const retryAfterSeconds = retryAfterHeader !== null ? Number(retryAfterHeader) : NaN;
      // Retry-After는 서버 통제 값이라 상한 없이 따르면 도구가 시간 단위로 멈춰 보일 수 있고,
      // 빈 문자열은 Number("")===0이라 백오프 없는 즉시 재시도가 된다 — 양수만 채택하고 60초로 클램프.
      const waitMs =
        Number.isFinite(retryAfterSeconds) && retryAfterSeconds > 0
          ? Math.min(retryAfterSeconds * 1000, 60_000)
          : 2 ** attempt * 1000;
      console.warn(
        `⚠️  ${label} 재시도 가능한 오류 (시도 ${attempt}/${MAX_RETRIES}): HTTP 429` +
          (retryAfterHeader ? ` (Retry-After: ${retryAfterHeader})` : ""),
      );
      if (attempt < MAX_RETRIES) {
        await sleep(waitMs);
        continue;
      }
      throw lastError;
    }

    if (response.status >= 500) {
      if (!idempotent) {
        const bodyText = await response.text().catch(() => "");
        throw new Error(
          `${label} 실패(비멱등 요청 — 재시도하지 않음): HTTP ${response.status}` +
            (bodyText ? ` (본문 길이 ${bodyText.length}자)` : ""),
        );
      }
      lastError = new Error(`${label} 실패: HTTP ${response.status}`);
      console.warn(`⚠️  ${label} 재시도 가능한 오류 (시도 ${attempt}/${MAX_RETRIES}): HTTP ${response.status}`);
      if (attempt < MAX_RETRIES) {
        await sleep(2 ** attempt * 1000);
        continue;
      }
      throw lastError;
    }

    if (!response.ok) {
      const bodyText = await response.text().catch(() => "");
      throw new Error(
        `${label} 실패: HTTP ${response.status}${bodyText ? ` (본문 길이 ${bodyText.length}자)` : ""}`,
      );
    }

    return response;
  }
  throw lastError;
}

// ============================================================
// CLI 인자 파싱
// ============================================================

const USAGE = `Documentation~/ → GitBook 포털 이관 도구 (일회성, CR 생성까지만)

사용법:
  node scripts~/migrate-docs-to-gitbook.js --dry-run [옵션]
  node scripts~/migrate-docs-to-gitbook.js [옵션]

실행은 항상 전체 시퀀스(CR 생성 → insert 4건 → insert 10건 → update 14건)를 돈다.
부분 실행·재개 옵션은 없다. 중간 실패 시 GitBook UI에서 그 CR을 archive하고 다시 실행하면
새 CR로 처음부터 진행한다.

옵션:
  --dry-run             네트워크 호출 없이 산출물만 --out에 작성하고 종료 (env 불필요)
  --parent <pageId>     그룹/최상위 문서를 삽입할 부모 페이지 id (없으면 space 최상위)
  --existing-map <path> {"Foo.md": "pageId"} JSON — 포털에 이미 있는 페이지는 insert 대신
                        해당 pageId로 update_page만 수행
  --ignore-open-cr      같은 subject의 열린 CR이 있어도 중단하지 않고 새 CR을 만든다
  --site-base <url>     사이트 base URL (기본: ${DEFAULT_SITE_BASE})
  --url-base <url>      섹션 접두사까지 포함한 문서 base URL (기본: <site-base>/documentation/unity)
  --changelog-url <url> changelog HTML URL
  --changelog-md-url <url>  api-changelog.md URL
  --docs-dir <path>     소스 디렉토리 (기본: ${DEFAULT_DOCS_DIR})
  --out <path>          산출물 출력 디렉토리 (필수 — --help 제외 모든 실행에서 요구)
  --help                이 도움말

환경 변수:
  GITBOOK_TOKEN, GITBOOK_SPACE_ID   실행 모드 필수 (없으면 exit 2)
  GITBOOK_PARENT_PAGE_ID            --parent 기본값
  GITBOOK_API_BASE                  API base 재지정 (목 서버 예행연습용)

종료 코드: 0 정상 / 1 실행 중 실패(관측·URL 구성 등) / 2 사전 검증·설정·열린 CR 충돌
`;

function parseArgs(argv) {
  const opts = {
    dryRun: false,
    help: false,
    parent: null,
    existingMap: null,
    ignoreOpenCr: false,
    siteBase: DEFAULT_SITE_BASE,
    urlBase: null,
    changelogUrl: DEFAULT_CHANGELOG_URL,
    changelogMdUrl: DEFAULT_CHANGELOG_MD_URL,
    docsDir: DEFAULT_DOCS_DIR,
    out: null,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    // 값이 없거나(끝에 도달) 다음 토큰이 "--"로 시작하는 다른 옵션이면 값을 삼키지
    // 않고 즉시 실패한다. 과거 결함: 무조건 다음 토큰을 소비해서 `--parent --dry-run`
    // 같은 값 누락 입력에서 "--dry-run" 문자열이 parent 값으로 삼켜지고 dryRun은
    // false로 남아 안전 게이트(dry-run)가 조용히 사라졌다.
    const next = () => {
      const value = argv[i + 1];
      if (value === undefined || value.startsWith("--")) {
        throw new MigrationError(`${arg} 옵션에는 값이 필요합니다.\n\n${USAGE}`, 2);
      }
      i++;
      return value;
    };
    switch (arg) {
      case "--dry-run":
        opts.dryRun = true;
        break;
      case "--help":
      case "-h":
        opts.help = true;
        break;
      case "--parent":
        opts.parent = next();
        break;
      case "--existing-map":
        opts.existingMap = next();
        break;
      case "--ignore-open-cr":
        opts.ignoreOpenCr = true;
        break;
      case "--site-base":
        opts.siteBase = next();
        break;
      case "--url-base":
        opts.urlBase = next();
        break;
      case "--changelog-url":
        opts.changelogUrl = next();
        break;
      case "--changelog-md-url":
        opts.changelogMdUrl = next();
        break;
      case "--docs-dir":
        opts.docsDir = next();
        break;
      case "--out":
        opts.out = next();
        break;
      default:
        throw new MigrationError(`알 수 없는 옵션: ${arg}\n\n${USAGE}`, 2);
    }
  }

  opts.siteBase = String(opts.siteBase).replace(/\/+$/, "");
  if (!opts.urlBase) {
    opts.urlBase = `${opts.siteBase}/documentation/unity`;
  }
  opts.urlBase = String(opts.urlBase).replace(/\/+$/, "");
  if (!opts.parent) {
    opts.parent = process.env.GITBOOK_PARENT_PAGE_ID || null;
  }

  return opts;
}

// ============================================================
// 텍스트 스캔 유틸 (코드펜스 인식 / 인라인 코드 마스킹)
// ============================================================

/**
 * 라인 배열을 순회하며 각 라인이 펜스(``` 또는 ~~~) 안인지 여부를 함께 계산한다.
 * 반환값: [{ line, inFence }]
 */
function annotateFences(lines) {
  const result = [];
  let inFence = false;
  let marker = null;
  for (const line of lines) {
    const trimmed = line.trimStart();
    const fenceMatch = trimmed.match(/^(`{3,}|~{3,})/);
    if (!inFence && fenceMatch) {
      inFence = true;
      marker = fenceMatch[1][0].repeat(fenceMatch[1].length);
      result.push({ line, inFence: true }); // 여는 펜스 라인 자체는 "펜스 안"으로 취급
      continue;
    }
    if (inFence && fenceMatch && fenceMatch[1][0] === marker[0] && fenceMatch[1].length >= marker.length) {
      result.push({ line, inFence: true }); // 닫는 펜스 라인도 "펜스 안"으로 취급(내용 취급 안 함)
      inFence = false;
      marker = null;
      continue;
    }
    result.push({ line, inFence });
  }
  return result;
}

// 인라인 코드 마스킹용 경계 문자 - 유니코드 사용자 영역(Private Use Area)이라 실제
// 마크다운 본문에 등장할 일이 없고, 제어문자(NUL 등)가 아니라 텍스트 편집기 git diff
// 터미널에서 안전하게 다뤄진다.
const MASK_BOUNDARY = "\uE000";

/** 인라인 코드(`...`) 구간을 마스킹 토큰으로 치환해 링크 정규식이 건드리지 않게 한다. */
function maskInlineCode(line) {
  const spans = [];
  const masked = line.replace(/`[^`\n]*`/g, (m) => {
    const token = `${MASK_BOUNDARY}${spans.length}${MASK_BOUNDARY}`;
    spans.push(m);
    return token;
  });
  return { masked, spans };
}

function unmaskInlineCode(line, spans) {
  if (spans.length === 0) return line;
  const re = new RegExp(`${MASK_BOUNDARY}(\\d+)${MASK_BOUNDARY}`, "g");
  return line.replace(re, (_, i) => spans[Number(i)]);
}

// 마크다운 링크: [텍스트](대상) — 이미지(![...])는 제외.
const LINK_RE = /(!?)\[([^\]]*)\]\(([^)\s][^)]*)\)/g;

// ============================================================
// 슬러그 체인 / URL 구성 (단일 진입점)
// ============================================================

function slugChain(entry) {
  const chain = [entry.slug];
  let cur = entry;
  while (cur.parentKey !== "root") {
    const parent = MANIFEST_BY_KEY.get(cur.parentKey);
    if (!parent) throw new MigrationError(`매니페스트 오류: ${cur.key}의 부모 ${cur.parentKey}를 찾을 수 없습니다.`, 2);
    chain.unshift(parent.slug);
    cur = parent;
  }
  return chain;
}

/**
 * --url-base에서 --site-base를 뗀 "섹션 경로"("documentation/unity"). --url-base가
 * --site-base로 시작하지 않으면(완전히 다른 base를 준 경우) 빈 문자열이다.
 */
function sectionPath(opts) {
  if (!opts.urlBase.startsWith(`${opts.siteBase}/`)) return "";
  return opts.urlBase.slice(opts.siteBase.length + 1).replace(/^\/+|\/+$/g, "");
}

/**
 * 문서 하나의 최종 공개 URL을 만드는 **유일한** 함수다(예측·관측 공통).
 *
 * page=null이면 매니페스트 slug 체인으로 예측 URL을, page가 주어지면 관측값으로 만든다:
 *   1) page.urls.public이 있으면 그대로 쓴다(GitBook이 알려준 정답).
 *   2) 없으면 page.path(관측) 또는 slug 체인(예측)을 --url-base 뒤에 붙인다.
 *      단 관측된 path가 이미 섹션 경로("documentation/unity/...")로 시작하면 site-base에
 *      붙여 접두사가 두 번 들어가지 않게 한다.
 *
 * 과거 결함: 관측 경로만 `siteBase + page.path`로 조립해 섹션 접두사가 통째로 빠진
 * URL이 본문에 새어나갔다(예측 경로는 urlBase를 썼으므로 dry-run에서는 드러나지 않음).
 * 그래서 두 경로를 이 함수 하나로 합치고, 결과 전부가 --url-base 접두사로 시작하는지
 * I-21과 push 직전 재검사로 강제한다.
 */
function buildPageUrl(entry, page, opts) {
  const publicUrl = page && page.urls && typeof page.urls.public === "string" && page.urls.public.trim()
    ? page.urls.public.trim()
    : null;
  if (publicUrl) return publicUrl;

  const observedPath =
    page && typeof page.path === "string" && page.path.trim() ? page.path.trim().replace(/^\/+/, "") : null;
  if (page && !observedPath) {
    // page가 주어지는 호출은 전부 recordDocument()를 거치는 CR 생성 이후 경로뿐이다
    // (예측 전용 호출은 항상 page=null) — RECOVERY_HINT는 여기서 붙이지 않는다.
    // recordDocument()가 이 예외를 CR 참조와 함께 한 번만 감싸 다시 던진다.
    throw new MigrationError(
      `문서 "${entry.title}"의 공개 URL을 구성할 수 없습니다(page.urls.public / page.path 둘 다 없음). ` +
        `GitBook 페이지 응답 형태가 예상과 다릅니다.`,
      1,
    );
  }
  const rel = observedPath || slugChain(entry).join("/");
  const section = sectionPath(opts);
  if (section && (rel === section || rel.startsWith(`${section}/`))) {
    return `${opts.siteBase}/${rel}`;
  }
  return `${opts.urlBase}/${rel}`;
}

/** 그룹까지 포함한 미리보기용 예측 URL (manifest.json 출력 전용). */
function predictedUrl(entry, opts) {
  return buildPageUrl(entry, null, opts);
}

/**
 * 실행 모드에서만 의미가 있다: 문서별 예측 URL(slug 체인 기반)과 관측 URL(서버 응답
 * 기준, pipeline.resolveCtx.pageMap이 pass1/pass2 관측 후 갱신된 값)을 비교해 다른
 * 문서 목록을 "slug drift" 경고로 만든다. I-21이 최종적으로 관측 URL만 검사하므로
 * (위 주석 참고 — 예측 URL 검사는 동어반복) 예측이 빗나가도 파이프라인은 계속 성공할 수
 * 있다. 이 경고는 그 성공 뒤에 "GitBook이 실제로 어떤 URL을 부여했는지"가 리뷰어의
 * 기대(예: manifest.json의 predictedUrl)와 다르다는 신호이므로, report.txt에 남겨
 * 리뷰어가 그 문서들의 실제 링크를 CR에서 클릭해 확인하도록 유도한다.
 */
function computeSlugDrift(pipeline, opts) {
  const drift = [];
  for (const entry of DOCUMENT_ENTRIES) {
    const predicted = predictedUrl(entry, opts);
    const observed = pipeline.resolveCtx.pageMap.get(entry.source);
    const observedUrl = observed && observed.url;
    if (typeof observedUrl === "string" && observedUrl !== predicted) {
      drift.push(
        `slug drift: ${entry.source} — 예측 ${predicted} ≠ 관측 ${observedUrl} (리뷰 시 실제 링크를 클릭해 확인하세요)`,
      );
    }
  }
  return drift;
}

/** 내부 페이지 링크가 반드시 시작해야 하는 접두사. */
function internalLinkPrefix(opts) {
  return `${opts.urlBase}/`;
}

/**
 * I-21: 재작성된 내부 링크가 전부 --url-base 접두사로 시작하는지 검사한다.
 * pageMap(문서 → 최종 URL)과 링크 대장의 내부 규칙 행 양쪽을 본다.
 */
function findBadInternalLinks(pageMap, linkRecords, opts) {
  const prefix = internalLinkPrefix(opts);
  const bad = [];
  for (const [source, value] of pageMap) {
    const url = value && value.url;
    if (typeof url !== "string" || !url.startsWith(prefix)) bad.push(`${source} → ${url}`);
  }
  for (const r of linkRecords || []) {
    if (r.rule !== "INTERNAL_PAGE" && r.rule !== "ANCHOR_DROPPED") continue;
    if (!String(r.rewritten_target).startsWith(prefix)) {
      bad.push(`${r.source_file}${r.line ? `:${r.line}` : ""} → ${r.rewritten_target}`);
    }
  }
  return bad;
}

// ============================================================
// 링크 분류 (설계 §2.6 규칙 테이블)
// ============================================================

/**
 * 링크 하나를 분류하고 재작성 값을 계산한다.
 * ctx: { mode: "sentinel"|"resolve", pageMap: Map<source,{url}>, changelogUrl, changelogMdUrl }
 * mode "resolve"는 dry-run(예측 URL)·실행 모드(관측 URL) 공통이며 pageMap이 무엇을
 * 담고 있는지만 다르다.
 */
function resolveLink(sourceFile, rawTarget, ctx) {
  if (/^https?:\/\//i.test(rawTarget)) {
    return { rule: "EXTERNAL_KEEP", rewritten: rawTarget, anchor: "" };
  }
  if (rawTarget === "changelog/index.html") {
    return { rule: "CHANGELOG", rewritten: ctx.changelogUrl, anchor: "" };
  }
  if (rawTarget === "changelog/api-changelog.md") {
    return { rule: "CHANGELOG_MD", rewritten: ctx.changelogMdUrl, anchor: "" };
  }
  if (rawTarget.startsWith("internal/")) {
    if (sourceFile === "README.md") {
      // 표 행째 삭제 대상 — 본문 재작성은 filterReadmeInternalRows()가 담당하고,
      // 여기서는 links.csv 기록용으로만 분류한다(§3.3, §2.6).
      return { rule: "REMOVED_WITH_TABLE_ROW", rewritten: "", anchor: "" };
    }
    return {
      rule: "INTERNAL_RUNBOOK",
      rewritten: `https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Documentation~/${rawTarget}`,
      anchor: "",
    };
  }

  const hashIdx = rawTarget.indexOf("#");
  const base = hashIdx === -1 ? rawTarget : rawTarget.slice(0, hashIdx);
  const anchor = hashIdx === -1 ? "" : rawTarget.slice(hashIdx);

  // 이관 제외 소스(저장소엔 있으나 포털엔 없음). README 표 안이면 행째 삭제(internal/과 동일
  // 경로), 그 밖에서는 GitHub blob URL로 재작성한다. anchor는 GitHub .md의 한글 앵커가
  // 불안정하므로 버린다(internal/ 규칙과 동일).
  if (EXCLUDED_SOURCES.has(base)) {
    if (sourceFile === "README.md") {
      return { rule: "REMOVED_WITH_TABLE_ROW", rewritten: "", anchor: "" };
    }
    return {
      rule: "EXCLUDED_TO_GITHUB",
      rewritten: `https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Documentation~/${base}`,
      anchor: "",
    };
  }

  if (/^[A-Za-z][A-Za-z0-9]*\.md$/.test(base)) {
    if (!MANIFEST_BY_SOURCE.has(base)) {
      throw new MigrationError(
        `미분류 링크: ${sourceFile} → ${rawTarget} (매니페스트에 없는 .md 대상 — 신규/개명 문서를 매니페스트에 반영하세요)`,
        2,
      );
    }
    const rule = anchor ? "ANCHOR_DROPPED" : "INTERNAL_PAGE";
    let rewritten;
    if (ctx.mode === "sentinel") {
      rewritten = `${SENTINEL_PREFIX}${base}`;
    } else {
      const resolved = ctx.pageMap.get(base);
      if (!resolved || !resolved.url) {
        throw new MigrationError(`링크 해석 실패: ${sourceFile} → ${rawTarget} (대상 페이지의 공개 URL을 구하지 못했습니다)`, 1);
      }
      rewritten = resolved.url;
    }
    return { rule, rewritten, anchor };
  }

  throw new MigrationError(
    `미분류 상대 링크: ${sourceFile} → ${rawTarget} (§2.6 규칙 테이블에 없는 대상 — 중단)`,
    2,
  );
}

// ============================================================
// README 전용: internal/ 표 행 삭제 (설계 §3.3)
// ============================================================

function isTableRow(line) {
  return /^\s*\|.*\|\s*$/.test(line);
}
function isTableSeparator(line) {
  return isTableRow(line) && /^[\s|:-]+$/.test(line) && line.includes("-");
}
function rowLinksToInternal(row) {
  if (/\]\(internal\//.test(row)) return true;
  // 이관 제외 소스로 링크하는 행도 삭제 대상(포털엔 없는 문서다).
  for (const src of EXCLUDED_SOURCES) {
    if (row.includes(`](${src})`) || row.includes(`](${src}#`)) return true;
  }
  return false;
}

/**
 * internal/*.md로 링크하는 표 행을 통째로 삭제한다. 삭제 후 데이터 행이 0인 표는
 * 헤더·구분선까지, 그리고 바로 앞의 안내 문단까지 함께 삭제한다.
 */
function filterReadmeInternalRows(lines) {
  const out = [];
  let removed = 0;
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const next = lines[i + 1];
    if (isTableRow(line) && next !== undefined && isTableSeparator(next)) {
      const header = line;
      const sep = next;
      let j = i + 2;
      const dataRows = [];
      while (j < lines.length && isTableRow(lines[j])) {
        dataRows.push(lines[j]);
        j++;
      }
      const hasInternal = dataRows.some(rowLinksToInternal);
      if (hasInternal) {
        const kept = dataRows.filter((row) => !rowLinksToInternal(row));
        removed += dataRows.length - kept.length;
        if (kept.length === 0) {
          // 표 전체 삭제 + 바로 앞 안내 문단(1블록) 삭제. 단일 패스로만 걷어내 자체 heading이
          // 없는 섹션(internal/ 안내문+표)에서 위 섹션 내용까지 잠식하지 않게 한다.
          const popBlanks = () => {
            while (out.length && out[out.length - 1].trim() === "") out.pop();
          };
          popBlanks();
          while (out.length && out[out.length - 1].trim() !== "" && !/^#{1,6}\s/.test(out[out.length - 1])) {
            out.pop();
          }
          // 안내 문단과 heading 사이 빈 줄까지 걷어낸 뒤, 바로 앞이 이 표 섹션의 heading이면
          // (= 그 섹션에 이 표 말고는 내용이 없었다는 뜻) heading도 제거해 빈 섹션을 없앤다.
          popBlanks();
          if (out.length && /^#{1,6}\s/.test(out[out.length - 1])) out.pop();
        } else {
          out.push(header, sep, ...kept);
        }
      } else {
        out.push(header, sep, ...dataRows);
      }
      i = j;
      continue;
    }
    out.push(line);
    i++;
  }
  return { lines: out, removed };
}

/**
 * 3줄 이상 연속된 빈 줄을 1줄로 접는다(표 행 삭제 뒤 남는 군더더기 정리).
 * annotateFences()로 코드펜스 안쪽은 건드리지 않는다 — 이 파일의 다른 텍스트 스캔
 * 함수(rewriteLinks/findRemainingRelativeLinks 등)와 동일한 펜스 인식 규약을 따른다.
 * 펜스 안의 빈 줄 서식은 코드 예제의 의도된 표현일 수 있으므로 원문 그대로 보존한다.
 */
function collapseBlankLines(lines) {
  const annotated = annotateFences(lines);
  const out = [];
  let blankRun = 0;
  for (const { line, inFence } of annotated) {
    if (inFence) {
      blankRun = 0;
      out.push(line);
      continue;
    }
    if (line.trim() === "") {
      blankRun++;
      if (blankRun <= 1) out.push(line);
    } else {
      blankRun = 0;
      out.push(line);
    }
  }
  return out;
}

// ============================================================
// 선두 H1 제거 (설계 §3.1)
// ============================================================

function extractLeadingH1(rawText, sourceFile) {
  const lines = rawText.split("\n");
  let idx = 0;
  while (idx < lines.length && lines[idx].trim() === "") idx++;
  if (idx >= lines.length) {
    throw new MigrationError(`${sourceFile}: 선두 H1을 찾을 수 없습니다(빈 파일).`, 2);
  }
  const m = lines[idx].match(/^#(?!#)\s+(.+?)\s*$/);
  if (!m) {
    throw new MigrationError(`${sourceFile}: 첫 비어있지 않은 줄이 "# 제목" 형태가 아닙니다: ${JSON.stringify(lines[idx])}`, 2);
  }
  const title = m[1];
  idx++;
  while (idx < lines.length && lines[idx].trim() === "") idx++;
  return { title, rest: lines.slice(idx) };
}

// ============================================================
// 링크 재작성 (펜스/인라인코드 인식) — 설계 §3.2, §2.6
// ============================================================

/**
 * lines를 순회하며 링크를 재작성한다. records가 주어지면 (text, original, rewritten,
 * rule) 튜플을 그대로 누적한다(링크 대장/사후 검사용).
 */
function rewriteLinks(sourceFile, lines, ctx, records) {
  const annotated = annotateFences(lines);
  const out = [];
  for (const { line, inFence } of annotated) {
    if (inFence) {
      out.push(line);
      continue;
    }
    const { masked, spans } = maskInlineCode(line);
    const replaced = masked.replace(LINK_RE, (full, bang, text, target) => {
      if (bang === "!") return full; // 이미지는 건드리지 않음
      const { rule, rewritten, anchor } = resolveLink(sourceFile, target, ctx);
      if (anchor && ctx.mode !== "sentinel") {
        // sentinel/resolve 두 모드 모두 buildDocumentBody에서 이 경로를 타므로, 콘솔
        // 경고는 resolve 모드(최종 본문 기준) 1회로만 남긴다 — 같은 링크에 대해
        // sentinel 패스에서 또 울리면 리뷰어에게 중복 소음만 된다. links.csv에는
        // auditLinks()가 모드와 무관하게 정확히 1행만 기록하므로 감사 커버리지는
        // 영향받지 않는다.
        console.warn(`⚠️  ANCHOR_DROPPED: ${sourceFile} → ${target} (fragment 제거, 페이지 상단으로)`);
      }
      if (records) {
        records.push({
          source_file: sourceFile,
          link_text: text,
          original_target: target,
          rewritten_target: rewritten,
          rule,
        });
      }
      if (rule === "REMOVED_WITH_TABLE_ROW") {
        // README 표 행은 filterReadmeInternalRows()가 이미 줄째 삭제했어야 한다.
        // 혹시라도 표 밖에서 internal/ 링크가 발견되면 불변식(I-8)에서 잡히도록
        // 원문을 그대로 남겨 잔존을 드러낸다(조용히 사라지지 않게).
        return full;
      }
      return `[${text}](${rewritten})`;
    });
    out.push(unmaskInlineCode(replaced, spans));
  }
  return out;
}

/**
 * 그룹(섹션) 랜딩 페이지의 본문을 만든다 — 자식 문서로 가는 링크 색인. 페이지 제목은
 * title 필드에서 오므로 본문에 선두 H1을 넣지 않는다(문서 본문과 동일 규약). 링크 대상은
 * 자식의 .md source이고, rewriteLinks()가 문서 본문과 똑같은 resolveLink()로 최종
 * URL(sentinel/관측)로 바꾸므로 규칙·결과가 문서 링크와 항상 일치한다. records를 넘기면
 * I-21 관측 URL 재검사(findBadInternalLinks)가 그룹 링크까지 커버한다.
 */
function buildGroupBody(groupKey, mode, ctx, records) {
  if (ctx.mode !== mode) {
    throw new MigrationError(`buildGroupBody 내부 오류: mode="${mode}"인데 ctx.mode="${ctx.mode}"`, 1);
  }
  const children = DOCUMENT_ENTRIES.filter((e) => e.parentKey === groupKey);
  const srcName = `__group:${groupKey}__`;
  const lines = children.map((c) => `- [${c.title}](${c.source})`);
  return `${rewriteLinks(srcName, lines, ctx, records).join("\n")}\n`;
}

// ============================================================
// 문서 본문 빌드
// ============================================================

/**
 * 매니페스트 문서 엔트리 하나의 최종 본문을 만든다.
 * mode: "sentinel" (insert 페이로드용) | "resolve" (links 페이로드/미리보기용)
 * 반환: { title, body, internalRemoved }
 */
function buildDocumentBody(entry, rawText, mode, ctx, records) {
  if (ctx.mode !== mode) {
    // 호출부 실수 방지용 자기 검사 — sentinel 모드인데 resolve용 ctx가 들어오면(또는
    // 그 반대) 센티넬이 새지 않고 즉시 죽게 한다. 과거 실제로 이 자리에서 ctx를
    // 통째로 무시하고 { mode: "sentinel" }만 새로 만드는 버그가 있었다(changelogUrl이
    // 함께 사라져 링크가 "undefined"로 새어나갔다) — 그 재발을 막는 가드다.
    throw new MigrationError(`buildDocumentBody 내부 오류: mode="${mode}"인데 ctx.mode="${ctx.mode}"`, 1);
  }
  const { title, rest } = extractLeadingH1(rawText, entry.source);
  if (!entry.titleOverride && title !== entry.title) {
    throw new MigrationError(
      `${entry.source}: 선두 H1("${title}")이 매니페스트 title("${entry.title}")과 다릅니다.`,
      2,
    );
  }

  let lines = rest;
  let internalRemoved = 0;
  if (entry.source === "README.md") {
    const filtered = filterReadmeInternalRows(lines);
    lines = filtered.lines;
    internalRemoved = filtered.removed;
    if (internalRemoved === 0) {
      throw new MigrationError("README.md: internal/ 표 행이 0건입니다 — README 구조 변경이 의심됩니다(중단).", 2);
    }
  }

  lines = rewriteLinks(entry.source, lines, ctx, records);
  lines = collapseBlankLines(lines);
  const body = lines.join("\n").replace(/\n+$/, "\n");

  return { title, body, internalRemoved };
}

// ============================================================
// 최종 본문 검증 (네트워크 없는 불변식들이 공유하는 스캐너)
// ============================================================

function bodyStartsWithHeading(body) {
  const lines = body.split("\n");
  let idx = 0;
  while (idx < lines.length && lines[idx].trim() === "") idx++;
  return idx < lines.length && /^#(?!#)\s/.test(lines[idx]);
}

/** 펜스 밖에서 상대(스킴 없는) 링크가 남아 있는지 스캔한다. */
function findRemainingRelativeLinks(body) {
  const found = [];
  const annotated = annotateFences(body.split("\n"));
  for (const { line, inFence } of annotated) {
    if (inFence) continue;
    const { masked } = maskInlineCode(line);
    let m;
    const re = new RegExp(LINK_RE.source, "g");
    while ((m = re.exec(masked))) {
      const target = m[3];
      if (!/^https?:\/\//i.test(target)) found.push(target);
    }
  }
  return found;
}

function findFenceAwareMatches(body, re) {
  const found = [];
  const annotated = annotateFences(body.split("\n"));
  for (const { line, inFence } of annotated) {
    if (inFence) continue;
    const { masked } = maskInlineCode(line);
    if (re.test(masked)) found.push(line);
  }
  return found;
}

// ============================================================
// 소스 로드 + 매니페스트 대조 (I-1)
// ============================================================

function loadSources(docsDir) {
  const entries = fs.readdirSync(docsDir, { withFileTypes: true });
  const mdFiles = entries.filter((e) => e.isFile() && e.name.endsWith(".md")).map((e) => e.name);
  const actualSet = new Set(mdFiles);
  const manifestSet = new Set(DOCUMENT_ENTRIES.map((e) => e.source));

  // 제외 소스가 매니페스트에도 있으면 모순(어느 쪽 규칙을 따를지 불명) — 즉시 중단.
  const overlap = [...EXCLUDED_SOURCES].filter((f) => manifestSet.has(f));
  if (overlap.length) {
    throw new MigrationError(`I-1 위반: EXCLUDED_SOURCES와 매니페스트 소스가 겹칩니다: ${JSON.stringify(overlap)}`, 2);
  }

  // I-1: docs 직속 .md 집합 == 매니페스트 소스 ∪ 제외 소스. 새 .md는 둘 중 하나에
  // 명시되기 전까지 여기서 잡힌다. 제외 소스인데 파일이 없으면(dangling) 그것도 중단.
  const allowedSet = new Set([...manifestSet, ...EXCLUDED_SOURCES]);
  const missing = [...manifestSet].filter((f) => !actualSet.has(f));
  const extra = [...actualSet].filter((f) => !allowedSet.has(f));
  const danglingExcluded = [...EXCLUDED_SOURCES].filter((f) => !actualSet.has(f));
  if (missing.length || extra.length || danglingExcluded.length) {
    throw new MigrationError(
      `I-1 위반: --docs-dir 직속 .md 파일 집합이 매니페스트∪제외와 다릅니다. ` +
        `누락=${JSON.stringify(missing)} 추가=${JSON.stringify(extra)} 제외-미존재=${JSON.stringify(danglingExcluded)}`,
      2,
    );
  }

  const sources = new Map();
  for (const entry of DOCUMENT_ENTRIES) {
    const filePath = path.join(docsDir, entry.source);
    sources.set(entry.source, fs.readFileSync(filePath, "utf8"));
  }
  return sources;
}

// ============================================================
// existing-map 검증 (I-19)
// ============================================================

function loadExistingMap(existingMapPath) {
  if (!existingMapPath) return new Map();
  const raw = fs.readFileSync(path.resolve(process.cwd(), existingMapPath), "utf8");
  let json;
  try {
    json = JSON.parse(raw);
  } catch (err) {
    throw new MigrationError(`--existing-map JSON 파싱 실패: ${err.message}`, 2);
  }
  const manifestSources = new Set(DOCUMENT_ENTRIES.map((e) => e.source));
  const seenIds = new Set();
  const map = new Map();
  for (const [key, value] of Object.entries(json)) {
    if (!manifestSources.has(key)) {
      throw new MigrationError(`I-19 위반: --existing-map의 키 "${key}"가 매니페스트 소스에 없습니다.`, 2);
    }
    if (!value || typeof value !== "string") {
      throw new MigrationError(`I-19 위반: --existing-map의 "${key}" 값이 비어있습니다.`, 2);
    }
    if (seenIds.has(value)) {
      throw new MigrationError(`I-19 위반: --existing-map에 중복 pageId가 있습니다: ${value}`, 2);
    }
    seenIds.add(value);
    map.set(key, value);
  }
  return map;
}

// ============================================================
// 매니페스트 자체 불변식 (I-2, I-11, I-12)
// ============================================================

function checkManifestInvariants(existingMap) {
  const rows = [];

  // I-2: 오퍼레이션 수 — insert 억제분(existing-map)을 더하면 매니페스트의 pass별 총수와
  // 같아야 한다(제외/제거로 개수가 바뀌어도 자동 추종). links = 문서 update + 그룹 색인 update.
  const suppressed = (pass) => [...existingMap.keys()].filter((k) => MANIFEST_BY_SOURCE.get(k).pass === pass).length;
  const insertCount = (pass) =>
    MANIFEST.filter((e) => e.pass === pass && !(e.type === "document" && existingMap.has(e.source))).length;
  const expPass1 = MANIFEST.filter((e) => e.pass === 1).length;
  const expPass2 = MANIFEST.filter((e) => e.pass === 2).length;
  const pass1 = insertCount(1);
  const pass2 = insertCount(2);
  const links = DOCUMENT_ENTRIES.length + GROUP_KEYS.length;
  rows.push({
    id: "I-2",
    desc: "오퍼레이션 수 (pass1 insert / pass2 insert / links update)",
    pass: pass1 + suppressed(1) === expPass1 && pass2 + suppressed(2) === expPass2,
    detail: `pass1=${pass1}(+억제 ${suppressed(1)})/${expPass1} pass2=${pass2}(+억제 ${suppressed(2)})/${expPass2} links=${links}(문서 ${DOCUMENT_ENTRIES.length}+그룹 ${GROUP_KEYS.length})`,
  });

  // I-11: 제목·slug 유일성
  const titles = MANIFEST.map((e) => e.title);
  const slugs = MANIFEST.map((e) => e.slug);
  const titleDupes = titles.filter((t, i) => titles.indexOf(t) !== i);
  const slugDupes = slugs.filter((s, i) => slugs.indexOf(s) !== i);
  const badSlugs = slugs.filter((s) => !/^[a-z0-9-]+$/.test(s));
  rows.push({
    id: "I-11",
    desc: "제목·slug 유일성, slug 형식",
    pass: titleDupes.length === 0 && slugDupes.length === 0 && badSlugs.length === 0 && MANIFEST.length === 14,
    detail: `총 ${MANIFEST.length}개, 제목중복=${titleDupes.length} slug중복=${slugDupes.length} 형식위반=${JSON.stringify(badSlugs)}`,
  });

  // I-12: pass2의 부모 참조가 pass1 그룹 중 하나인지
  const badParents = MANIFEST.filter((e) => e.pass === 2 && !GROUP_KEYS.includes(e.parentKey));
  rows.push({
    id: "I-12",
    desc: `pass2 부모 참조 무결성 (pass1 그룹 ${GROUP_KEYS.length}개 중 하나)`,
    pass: badParents.length === 0,
    detail: badParents.length ? `위반: ${badParents.map((e) => e.key).join(", ")}` : "전부 유효",
  });

  return rows;
}

// ============================================================
// 자기 검사 (I-13) — 스크립트 소스에 머지 엔드포인트 문자열이 없는지
// ============================================================

function checkSelfNoMerge() {
  const text = fs.readFileSync(__filename, "utf8");
  // "/" + "merge"를 런타임에 조립해서 검사 코드 자신이 그 부분 문자열을 소스에
  // 담지 않도록 한다 — 그래야 이 검사 자체가 자기모순 없이 통과할 수 있다.
  const forbidden = "/" + "merge";
  const hasForbidden = text.includes(forbidden);
  return {
    id: "I-13",
    desc: "스크립트 자기 검사 (머지 엔드포인트 문자열 0건)",
    pass: !hasForbidden,
    detail: hasForbidden ? "발견됨" : "0건",
  };
}

// ============================================================
// 메인 변환 파이프라인 (네트워크 없는 부분 — 항상 먼저 돈다)
// ============================================================

function runTransformPipeline(opts) {
  const docsDirAbs = path.resolve(process.cwd(), opts.docsDir);
  const sources = loadSources(docsDirAbs);
  const existingMap = loadExistingMap(opts.existingMap);

  // 예측 pageMap — dry-run 산출물과 실행 모드의 사전 감사 단계 공통. 실행 모드는 관측
  // 직후 이 맵을 관측값으로 덮어쓴다.
  const predictedPageMap = new Map();
  for (const entry of DOCUMENT_ENTRIES) {
    predictedPageMap.set(entry.source, { url: buildPageUrl(entry, null, opts) });
  }
  const resolveCtx = {
    mode: "resolve",
    pageMap: predictedPageMap,
    changelogUrl: opts.changelogUrl,
    changelogMdUrl: opts.changelogMdUrl,
  };
  // sentinel 모드도 CHANGELOG/CHANGELOG_MD/INTERNAL_RUNBOOK 규칙은 즉시 최종값으로
  // 해석해야 한다(센티넬은 오직 INTERNAL_PAGE/ANCHOR_DROPPED에만 적용) — changelogUrl/
  // changelogMdUrl을 여기서도 채워야 resolveLink()가 "undefined"를 뱉지 않는다.
  const sentinelCtx = { mode: "sentinel", changelogUrl: opts.changelogUrl, changelogMdUrl: opts.changelogMdUrl };

  const linksCsvRows = [];
  const bodies = {}; // source -> { title, sentinelBody, resolvedBody }
  let totalInternalRemoved = 0;

  for (const entry of DOCUMENT_ENTRIES) {
    const raw = sources.get(entry.source);

    // 링크 대장(원본 줄 번호 보존)은 펜스/인라인코드 인식 스캐너로 원문을 그대로
    // 스캔해서 만든다 — 표 행 삭제·H1 제거로 줄이 바뀌기 전 원본 기준. buildDocumentBody가
    // 쓰는 rewriteLinks()와 같은 resolveLink()를 호출하므로 규칙·결과가 항상 일치한다.
    linksCsvRows.push(...auditLinks(entry.source, raw, resolveCtx));

    const sentinelResult = buildDocumentBody(entry, raw, "sentinel", sentinelCtx, null);
    const resolvedResult = buildDocumentBody(entry, raw, "resolve", resolveCtx, null);

    if (entry.source === "README.md") totalInternalRemoved = sentinelResult.internalRemoved;

    bodies[entry.source] = {
      title: entry.titleOverride ? entry.title : sentinelResult.title,
      sentinelBody: sentinelResult.body,
      resolvedBody: resolvedResult.body,
    };
  }

  // 그룹 랜딩 페이지 본문(자식 색인). 문서 링크와 같은 sentinel→resolve 흐름을 타므로
  // pass1은 sentinelBody로 insert하고, links 패스에서 관측 URL로 resolvedBody를 다시
  // 만들어 update한다. 이 시점의 resolvedBody는 예측 URL 기준(dry-run 산출물용).
  const groupBodies = {};
  for (const groupKey of GROUP_KEYS) {
    groupBodies[groupKey] = {
      sentinelBody: buildGroupBody(groupKey, "sentinel", sentinelCtx, null),
      resolvedBody: buildGroupBody(groupKey, "resolve", resolveCtx, null),
    };
  }

  return { sources, existingMap, resolveCtx, sentinelCtx, linksCsvRows, bodies, groupBodies, totalInternalRemoved, docsDirAbs };
}

// auditLinks: 원본 텍스트(H1 포함)를 그대로 줄 단위로 스캔하며 원본 줄 번호로 링크를 기록한다.
// buildDocumentBody의 rewriteLinks와 동일한 resolveLink()를 쓰므로 규칙·결과가 항상 일치한다.
function auditLinks(sourceFile, rawText, resolveCtx) {
  const rows = [];
  const annotated = annotateFences(rawText.split("\n"));
  annotated.forEach(({ line, inFence }, idx) => {
    if (inFence) return;
    const { masked } = maskInlineCode(line);
    const re = new RegExp(LINK_RE.source, "g");
    let m;
    while ((m = re.exec(masked))) {
      const [, bang, text, target] = m;
      if (bang === "!") continue;
      const { rule, rewritten } = resolveLink(sourceFile, target, resolveCtx);
      rows.push({
        source_file: sourceFile,
        line: idx + 1,
        link_text: text,
        original_target: target,
        rewritten_target: rewritten,
        rule,
      });
    }
  });
  return rows;
}

// ============================================================
// 불변식 러너 (네트워크 없는 전량 검사)
// ============================================================

function runInvariants(pipeline, opts) {
  const rows = [];
  const push = (id, desc, pass, detail) => rows.push({ id, desc, pass, detail });

  // I-1은 loadSources에서 이미 강제됨(FAIL이면 여기까지 오지 못함) — 통과 기록만 남김.
  push(
    "I-1",
    "--docs-dir 직속 .md 집합 == 매니페스트 소스 ∪ 제외 소스",
    true,
    `문서 ${DOCUMENT_ENTRIES.length} + 제외 ${EXCLUDED_SOURCES.size}`,
  );

  for (const row of checkManifestInvariants(pipeline.existingMap)) rows.push(row);

  // I-3, I-4: 선두 H1 제거 + 잔존 0건
  let h1RemovedCount = 0;
  const residualHeading = [];
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    h1RemovedCount++;
    if (bodyStartsWithHeading(b.resolvedBody)) residualHeading.push(source);
    if (bodyStartsWithHeading(b.sentinelBody)) residualHeading.push(`${source}(sentinel)`);
  }
  push(
    "I-3",
    `선두 H1 제거 (${DOCUMENT_ENTRIES.length}건, 제목 일치)`,
    h1RemovedCount === DOCUMENT_ENTRIES.length,
    `${h1RemovedCount}/${DOCUMENT_ENTRIES.length}`,
  );
  push("I-4", "최종 본문 시작이 '# '인 파일 0건", residualHeading.length === 0, JSON.stringify(residualHeading));

  // I-5: 최종 본문의 상대 경로 링크 0건
  const relLeftover = [];
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    const found = findRemainingRelativeLinks(b.resolvedBody);
    if (found.length) relLeftover.push(`${source}:${JSON.stringify(found)}`);
  }
  push("I-5", "최종 본문의 상대 경로 링크 0건", relLeftover.length === 0, relLeftover.join("; "));

  // I-6: 센티넬 잔존 0건 (resolvedBody 기준 — sentinelBody는 정의상 센티넬을 담고 있어 검사 대상 아님)
  const sentinelLeftover = [];
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    if (b.resolvedBody.includes(SENTINEL_HOST)) sentinelLeftover.push(source);
  }
  for (const groupKey of GROUP_KEYS) {
    if (pipeline.groupBodies[groupKey].resolvedBody.includes(SENTINEL_HOST)) sentinelLeftover.push(`group:${groupKey}`);
  }
  push("I-6", `${SENTINEL_HOST} 잔존 0건 (resolved 본문)`, sentinelLeftover.length === 0, JSON.stringify(sentinelLeftover));

  // I-7: 재작성된 "내부" 링크의 #fragment 0건. 본문 전체를 정규식으로 훑으면 §2.5가
  // 명시적으로 그대로 두라는 EXTERNAL_KEEP 링크(예: 외부 매뉴얼 딥링크)의 fragment까지
  // 내부 링크로 오탐한다 — 그래서 링크 대장(rule 기준)으로 INTERNAL_PAGE/ANCHOR_DROPPED
  // 행만 검사한다. 외부 URL의 fragment는 검사 대상이 아니다.
  const fragmentLeftover = pipeline.linksCsvRows
    .filter((r) => (r.rule === "INTERNAL_PAGE" || r.rule === "ANCHOR_DROPPED") && r.rewritten_target.includes("#"))
    .map((r) => `${r.source_file}:${r.line}`);
  push("I-7", "재작성된 내부 링크 #fragment 0건", fragmentLeftover.length === 0, JSON.stringify(fragmentLeftover));

  // I-8: 최종 본문의 문자열 "internal/" — GitHub blob URL 화이트리스트 제외 0건
  const internalLeftover = [];
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    const withoutWhitelisted = b.resolvedBody.replace(
      /https:\/\/github\.com\/toss\/apps-in-toss-unity-sdk\/blob\/main\/Documentation~\/internal\/[A-Za-z0-9._-]+\.md/g,
      "",
    );
    if (withoutWhitelisted.includes("internal/")) internalLeftover.push(source);
    if (source === "README.md" && b.resolvedBody.includes("internal/")) internalLeftover.push("README.md(비화이트리스트)");
  }
  push("I-8", "최종 본문의 'internal/' 잔존 (화이트리스트 제외) 0건", internalLeftover.length === 0, JSON.stringify(internalLeftover));

  // I-9: '{%' 토큰 0건
  const blockTokenLeftover = [];
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    if (findFenceAwareMatches(b.resolvedBody, /\{%/).length) blockTokenLeftover.push(source);
  }
  push("I-9", "'{%' GitBook 블록 토큰 0건", blockTokenLeftover.length === 0, JSON.stringify(blockTokenLeftover));

  // I-10: 링크 대장 커버리지 — rewritten_target 비어있지 않음(REMOVED_* 제외)
  const badCsvRows = pipeline.linksCsvRows.filter(
    (r) => !r.rule.startsWith("REMOVED_") && (!r.rewritten_target || r.rewritten_target.length === 0),
  );
  const ruleCounts = {};
  for (const r of pipeline.linksCsvRows) ruleCounts[r.rule] = (ruleCounts[r.rule] || 0) + 1;
  push(
    "I-10",
    "링크 대장 커버리지 (rewritten_target 비어있지 않음 또는 REMOVED_*)",
    badCsvRows.length === 0,
    `총 ${pipeline.linksCsvRows.length}행, 규칙별=${JSON.stringify(ruleCounts)}`,
  );

  // I-14: 배치 크기 (한 요청당 오퍼레이션 ≤ BATCH_LIMIT). links = 문서 update + 그룹 색인 update.
  const i14p1 = MANIFEST.filter((e) => e.pass === 1).length;
  const i14p2 = MANIFEST.filter((e) => e.pass === 2).length;
  const i14lk = DOCUMENT_ENTRIES.length + GROUP_KEYS.length;
  push(
    "I-14",
    `배치 크기 ≤ ${BATCH_LIMIT}`,
    i14p1 <= BATCH_LIMIT && i14p2 <= BATCH_LIMIT && i14lk <= BATCH_LIMIT,
    `pass1=${i14p1} pass2=${i14p2} links=${i14lk} (전부 ≤ ${BATCH_LIMIT})`,
  );

  // I-15: 본문 크기
  const oversizedPages = [];
  let totalBytes = 0;
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    const bytes = Buffer.byteLength(b.resolvedBody, "utf8");
    totalBytes += bytes;
    if (bytes > MAX_PAGE_BYTES) oversizedPages.push(`${source}(${bytes}B)`);
  }
  push(
    "I-15",
    "본문 크기 (페이지 ≤ 300KB, 합계 ≤ 4MB)",
    oversizedPages.length === 0 && totalBytes <= MAX_REQUEST_BYTES,
    `합계 ${totalBytes}B, 초과=${JSON.stringify(oversizedPages)}`,
  );

  const selfCheck = checkSelfNoMerge();
  push(selfCheck.id, selfCheck.desc, selfCheck.pass, selfCheck.detail);

  // I-18: 인코딩 (UTF-8 유효 + LF + BOM 없음)
  const encodingIssues = [];
  for (const [source, b] of Object.entries(pipeline.bodies)) {
    if (b.resolvedBody.includes("\r")) encodingIssues.push(`${source}: CRLF`);
    if (b.resolvedBody.charCodeAt(0) === 0xfeff) encodingIssues.push(`${source}: BOM`);
  }
  push("I-18", "본문 인코딩 (UTF-8 / LF / BOM 없음)", encodingIssues.length === 0, JSON.stringify(encodingIssues));

  // I-19는 loadExistingMap에서 이미 강제됨
  push("I-19", "--existing-map 정합", true, pipeline.existingMap.size ? `${pipeline.existingMap.size}건` : "미사용");

  // I-20: changelog URL 유효성
  const urlsOk = opts.changelogUrl.startsWith("https://") && opts.changelogMdUrl.startsWith("https://");
  push(
    "I-20",
    "changelog URL이 https://로 시작",
    urlsOk,
    `changelog-url=${opts.changelogUrl} changelog-md-url=${opts.changelogMdUrl}`,
  );

  // I-21: 재작성된 내부 링크 전부가 --url-base(= site-base + 섹션 접두사)로 시작.
  // dry-run에서는 예측 URL을, 실행 모드에서는 push 직전 관측 URL을 같은 함수로 재검사한다.
  //
  // ⚠️ dry-run(및 실행 모드의 이 시점 — pageMap이 아직 predictedPageMap인 최초 호출)에서는
  // 이 검사가 사실상 동어반복이다: predictedUrl()이 애초에 `${opts.urlBase}/${slugChain}`을
  // 조립해서 만든 값이므로, "그 값이 opts.urlBase로 시작하는가"는 조립 방식 자체가 보장한다
  // (section 이중 접두사 분기만 실질적 검사 대상). 이 패스가 실제로 의미를 갖는 지점은
  // buildPageUrl()이 page.urls.public/page.path 같은 **서버 관측값**을 쓸 때뿐이다 —
  // 실행 모드에서 pass1/pass2 관측 후 pageMap을 갱신하고, push 직전에 이 함수를 다시 불러
  // 재검사하는 지점(runExecutionMode의 "I-21 재검사(관측 URL 기준)")이 진짜 방어선이다.
  const badPrefix = findBadInternalLinks(pipeline.resolveCtx.pageMap, pipeline.linksCsvRows, opts);
  push(
    "I-21",
    `재작성된 내부 링크가 전부 "${internalLinkPrefix(opts)}" 접두사로 시작`,
    badPrefix.length === 0,
    badPrefix.length ? JSON.stringify(badPrefix) : `${pipeline.resolveCtx.pageMap.size}개 문서 URL 전부 일치`,
  );

  // I-22: 그룹 색인 본문 무결성 — 센티넬 0 / 상대링크 0 / 선두 H1 없음 / 자식 수 == 링크 수.
  const groupIssues = [];
  for (const groupKey of GROUP_KEYS) {
    const b = pipeline.groupBodies[groupKey];
    if (!b) {
      groupIssues.push(`${groupKey}: 본문 없음`);
      continue;
    }
    if (b.resolvedBody.includes(SENTINEL_HOST)) groupIssues.push(`${groupKey}: 센티넬 잔존`);
    if (findRemainingRelativeLinks(b.resolvedBody).length) groupIssues.push(`${groupKey}: 상대링크 잔존`);
    if (bodyStartsWithHeading(b.resolvedBody)) groupIssues.push(`${groupKey}: 선두 H1`);
    const childCount = DOCUMENT_ENTRIES.filter((e) => e.parentKey === groupKey).length;
    const linkCount = (b.resolvedBody.match(/^- \[/gm) || []).length;
    if (childCount !== linkCount) groupIssues.push(`${groupKey}: 자식 ${childCount} vs 링크 ${linkCount}`);
  }
  push("I-22", `그룹 색인 본문 무결성 (${GROUP_KEYS.length}개 그룹)`, groupIssues.length === 0, JSON.stringify(groupIssues));

  return rows;
}

// ============================================================
// 오퍼레이션 페이로드 빌드 (설계 §4.6)
// ============================================================

function buildInsertOp(entry, body, intoValue) {
  const op = {
    operation: "insert_page",
    title: entry.title,
    document: { markdown: body },
  };
  if (intoValue) op.into = intoValue;
  if (entry.slug) op.slug = entry.slug;
  return op;
}

function buildUpdateOp(pageId, body) {
  return { operation: "update_page", page: pageId, document: { markdown: body } };
}

function chunkChanges(changes) {
  const chunks = [];
  for (let i = 0; i < changes.length; i += BATCH_LIMIT) {
    chunks.push({ changes: changes.slice(i, i + BATCH_LIMIT) });
  }
  if (chunks.length === 0) chunks.push({ changes: [] });
  return chunks;
}

/** pass1에서 실제로 insert할 엔트리(그룹 3 + 최상위 문서 1 − existing-map 억제분). */
function pass1Entries(existingMap) {
  return MANIFEST.filter((e) => e.pass === 1 && !(e.type === "document" && existingMap.has(e.source)));
}

/** pass2에서 실제로 insert할 엔트리(자식 문서 10 − existing-map 억제분). */
function pass2Entries(existingMap) {
  return MANIFEST.filter((e) => e.pass === 2 && !existingMap.has(e.source));
}

function buildPass1Ops(pipeline, opts) {
  const changes = [];
  for (const entry of pass1Entries(pipeline.existingMap)) {
    const into = entry.parentKey === "root" ? opts.parent || undefined : undefined;
    if (entry.type === "group") {
      // insert_page 오퍼레이션 실스펙에는 group/document를 가르는 type 필드가 없다 —
      // document가 필수다. 그룹 역할은 "자식 색인 본문 + 자식을 이 페이지의 into로 삽입"으로
      // 구현한다(빈 본문이면 사이드바 랜딩 페이지가 비어 보인다). 본문 링크는 pass1 시점엔
      // 아직 자식이 없으므로 sentinel이고, links 패스에서 관측 URL로 update된다. CR diff에서
      // 사이드바에 그룹처럼 보이는지 사람이 1회 스팟체크한다(report.txt에도 경고로 남긴다).
      changes.push(buildInsertOp(entry, pipeline.groupBodies[entry.key].sentinelBody, into));
    } else {
      changes.push(buildInsertOp(entry, pipeline.bodies[entry.source].sentinelBody, into));
    }
  }
  return chunkChanges(changes);
}

function buildPass2Ops(pipeline, groupPageIds) {
  const changes = [];
  for (const entry of pass2Entries(pipeline.existingMap)) {
    const into = groupPageIds.get(entry.parentKey) || `<pass1:${entry.parentKey}>`;
    changes.push(buildInsertOp(entry, pipeline.bodies[entry.source].sentinelBody, into));
  }
  return chunkChanges(changes);
}

function buildLinksOps(pipeline, pageIdBySource, groupPageIds) {
  const changes = [];
  for (const entry of DOCUMENT_ENTRIES) {
    const pageId = pageIdBySource.get(entry.source) || `<observed:${entry.key}>`;
    changes.push(buildUpdateOp(pageId, pipeline.bodies[entry.source].resolvedBody));
  }
  // 그룹 랜딩 페이지도 관측 URL로 자식 색인 본문을 채운다(pass1엔 sentinel로 넣었다).
  for (const groupKey of GROUP_KEYS) {
    const pageId = (groupPageIds && groupPageIds.get(groupKey)) || `<observed-group:${groupKey}>`;
    changes.push(buildUpdateOp(pageId, pipeline.groupBodies[groupKey].resolvedBody));
  }
  return chunkChanges(changes);
}

// ============================================================
// 산출물 쓰기 (결정적: 시각·난수·절대경로·해시 미포함, 키 정렬, LF, UTF-8 무 BOM)
// ============================================================

function writeFileLF(filePath, content) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content.replace(/\r\n/g, "\n"), { encoding: "utf8" });
}

function writeJsonLF(filePath, obj) {
  writeFileLF(filePath, `${JSON.stringify(obj, null, 2)}\n`);
}

function toCsvField(value) {
  const s = String(value == null ? "" : value);
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

function writeLinksCsv(filePath, rows) {
  const header = ["source_file", "line", "link_text", "original_target", "rewritten_target", "rule"];
  const sorted = [...rows].sort((a, b) => {
    if (a.source_file !== b.source_file) return a.source_file.localeCompare(b.source_file);
    return (a.line || 0) - (b.line || 0);
  });
  const lines = [header.join(",")];
  for (const r of sorted) {
    lines.push(
      [
        toCsvField(r.source_file),
        toCsvField(r.line || ""),
        toCsvField(r.link_text),
        toCsvField(r.original_target),
        toCsvField(r.rewritten_target),
        toCsvField(r.rule),
      ].join(","),
    );
  }
  writeFileLF(filePath, `${lines.join("\n")}\n`);
}

function writeReport(filePath, invariantRows, opts, warnings) {
  const lines = [];
  lines.push("GitBook 이관 도구 — 검증 리포트");
  lines.push("=".repeat(60));
  lines.push("");
  lines.push("실행 모델: 일회성 · 단일 CR · 부분 실행/재개 없음.");
  lines.push("  전체 시퀀스 = CR 생성 → insert 4건(그룹 3 + 최상위 문서 1) → insert 10건(자식 문서)");
  lines.push("               → update 14건(문서 11 + 그룹 색인 3, 센티넬 링크를 관측된 실제 URL로 재작성).");
  lines.push("  중간 실패 시 GitBook UI에서 그 CR을 archive하고 다시 실행하면 새 CR로 처음부터 진행한다.");
  lines.push("  머지는 이 도구가 하지 않는다 — 사람이 GitBook UI에서 검토 후 수동으로 처리한다.");
  lines.push("");
  lines.push(`site-base:        ${opts.siteBase}`);
  lines.push(`url-base:         ${opts.urlBase}  (내부 링크 접두사 — I-21의 기준)`);
  lines.push(`changelog-url:    ${opts.changelogUrl}`);
  lines.push(`changelog-md-url: ${opts.changelogMdUrl}`);
  lines.push("manifest.json의 predictedUrl은 slug 기반 예측값이며 실행 모드에서도 갱신되지 않는다(항상 예측값).");
  lines.push("반면 ops.pass2.json / ops.links.json / bodies/*.md / links.csv / 이 report.txt 자체는 실행 모드에서");
  lines.push("네트워크 첫 호출 전에 예측값으로 한 번 쓰이고, 각 pass 관측 직후 실제로 push한 관측값으로 다시");
  lines.push("쓰인다 — 이 파일을 읽고 있는 시점이 실행 완료 후라면 아래 경고 절(slug drift 포함)과 링크 대장은");
  lines.push("전부 실제로 push한 값 기준이다. dry-run에서는 네트워크 호출이 없으므로 전부 예측값 그대로다.");
  lines.push("관측값과 예측값이 달라도 최종 링크는 항상 url-base 접두사로 시작해야 한다(push 직전 재검사).");
  lines.push("");
  lines.push("불변식 검사 결과");
  lines.push("-".repeat(60));
  let anyFail = false;
  for (const row of invariantRows) {
    if (!row.pass) anyFail = true;
    lines.push(`[${row.pass ? "PASS" : "FAIL"}] ${row.id} — ${row.desc}`);
    lines.push(`       ${row.detail}`);
  }
  lines.push("");
  lines.push(`총계: ${invariantRows.length}건 중 ${invariantRows.filter((r) => r.pass).length}건 PASS`);
  lines.push("");
  lines.push("사람이 CR 리뷰에서 직접 확인할 것");
  lines.push("-".repeat(60));
  lines.push("- 그룹 3개가 사이드바에서 실제로 '그룹'처럼 보이는지 (자식 문서 색인 본문을 담은 페이지로 구현했다)");
  lines.push("- 문서 13개가 의도한 그룹 아래에 배치됐는지 (스크립트는 부모 배치를 검증하지 않는다)");
  lines.push("- 재작성된 내부 링크가 실제로 열리는지 (샘플 2~3건)");
  lines.push("");
  if (warnings.length) {
    lines.push("경고");
    lines.push("-".repeat(60));
    for (const w of warnings) lines.push(`- ${w}`);
    lines.push("");
  }
  lines.push("전체 판정: " + (anyFail ? "FAIL" : "PASS"));
  writeFileLF(filePath, `${lines.join("\n")}\n`);
  return anyFail;
}

function manifestToJson(opts) {
  return MANIFEST.map((entry) => ({
    source: entry.source || null,
    title: entry.title,
    type: entry.type,
    parent: entry.parentKey,
    slug: entry.slug,
    pass: entry.pass,
    predictedUrl: predictedUrl(entry, opts),
  }));
}

/**
 * 산출물 전량을 --out에 쓴다. dry-run과 실행 모드가 같은 함수를 쓰고, 실행 모드에서는
 * 반드시 첫 네트워크 호출(findOpenChangeRequests) **이전**에 이 함수가 먼저 실행된다
 * (runExecutionMode 최상단 — "네트워크 push 전에 무엇을 보낼지 남긴다"). 이 시점에는
 * 아직 아무 페이지도 insert되지 않았으므로 ops.pass2.json/ops.links.json/bodies/*.md/
 * links.csv/report.txt는 전부 예측값(플레이스홀더 `<pass1:key>` / `<observed:key>` 포함)
 * 으로 쓴다 — 실제 관측값은 runExecutionMode가 각 pass 관측 직후 writeJsonLF()/
 * writeFileLF()/writeLinksCsv()/writeReport()로 같은 경로를 덮어써 갱신한다(ops.pass2.json
 * 은 pass1 관측 직후, ops.links.json/bodies/*.md/links.csv/report.txt는 최종(links) 관측
 * 직후 — 이 산출물들은 실행이 끝나면 항상 실제로 push한 값을 담는다). manifest.json만
 * 예외로 실행 모드에서도 갱신되지 않는다(항상 slug 기반 예측 미리보기 — slug drift 경고가
 * report.txt에 남는 이유이기도 하다).
 */
function writeAllArtifacts(pipeline, invariantRows, opts, warnings) {
  const out = opts.out;
  const groupPageIds = new Map();
  const pageIdBySource = new Map();

  writeJsonLF(path.join(out, "manifest.json"), manifestToJson(opts));
  writeJsonLF(path.join(out, "ops.pass1.json"), buildPass1Ops(pipeline, opts));
  writeJsonLF(path.join(out, "ops.pass2.json"), buildPass2Ops(pipeline, groupPageIds));
  writeJsonLF(path.join(out, "ops.links.json"), buildLinksOps(pipeline, pageIdBySource, groupPageIds));

  for (const entry of DOCUMENT_ENTRIES) {
    writeFileLF(path.join(out, "bodies", `${entry.slug}.md`), pipeline.bodies[entry.source].resolvedBody);
  }
  for (const g of MANIFEST.filter((e) => e.type === "group")) {
    writeFileLF(path.join(out, "bodies", `${g.slug}.md`), pipeline.groupBodies[g.key].resolvedBody);
  }

  writeLinksCsv(path.join(out, "links.csv"), pipeline.linksCsvRows);
  return writeReport(path.join(out, "report.txt"), invariantRows, opts, warnings);
}

// ============================================================
// GitBook API 계층 (실행 모드 전용)
// ============================================================

function authHeaders(token) {
  return { Authorization: `Bearer ${token}`, "Content-Type": "application/json" };
}

// 서버 쿼리(`status=`)로 후보를 넉넉히 끌어오기 위한 상태 집합 — 이 두 값으로 조회하면
// 이 가드가 잡아야 하는 케이스(중단된 실행이 남긴 draft CR, 리뷰 요청된 open CR)를 놓치지
// 않는다. 이 Set은 "무엇을 열린 것으로 볼지"의 최종 판정 기준이 **아니다** — 최종 판정은
// isClosedCrStatus()가 한다(아래).
const OPEN_CR_QUERY_STATUSES = ["draft", "open"];

// 서버가 명시적으로 "종결"이라고 선언한 상태만 안전하게 걸러낸다(닫힌 것으로 인정).
// status enum은 draft|open|archived|merged뿐이라고 문서화되어 있지만, 그 문서를 실토큰
// 없이 검증한 적이 없다(헤더 주석 "실토큰 첫 실행 시 확인" (3)) — 그래서 이 목록에 없는
// 모든 경우(값이 없음/undefined, draft, open, 그리고 향후 enum이 늘어나 생길 미지 값)는
// 전부 "열려 있다"고 보수적으로 간주한다. status 필드명이 실제와 달라 cr.status가 항상
// undefined로 관측되더라도 이 규칙 덕분에 가드는 무음으로 무력화되지 않고 오탐(=실행을
// 막고 사람이 조사)으로 실패한다 — "중복 CR이 하나 생기고 마는" 것보다 안전한 실패 방향이다.
const CLOSED_CR_STATUSES = new Set(["archived", "merged"]);
function isClosedCrStatus(status) {
  return CLOSED_CR_STATUSES.has(status);
}

/**
 * 같은 subject의 미종결(=isClosedCrStatus()가 아닌) CR을 전부 찾는다.
 *
 * GitBook OpenAPI 스펙상 `status` 쿼리 파라미터는 생략하면 `default: "open"`으로 서버가
 * 필터링한다 — 그런데 이 도구가 POST로 만드는 CR은 리뷰 요청 전까지 draft 상태이므로,
 * status를 생략하면 이 가드가 잡아야 할 바로 그 대상(중단된 실행이 남긴 draft CR)이 서버
 * 필터에서 빠진다. status enum에는 "전체"를 뜻하는 값이 없으므로(draft|open|archived|
 * merged뿐) status=draft와 status=open을 각각 커서(`next.page`)를 끝까지 따라가며 두 번
 * 조회해 id 기준으로 합친다(서버 필터는 후보를 넉넉히 끌어오기 위한 최적화일 뿐이다).
 * 그 위에 클라이언트에서 isClosedCrStatus()로 다시 검증한다 — status 쿼리 파라미터가
 * 무시되거나 필드명이 실제와 다른 경우에도 조용히 통과시키지 않기 위한 이중 방어다
 * (헤더 주석 "실토큰 첫 실행 시 확인" (3)). archived 또는 merged로 명확히 확인된 CR만 제외되고,
 * 그 외(값이 없거나 미지)는 전부 열린 것으로 간주해 가드를 발동시킨다 — 필드명이 실제와
 * 다르면 매 실행마다 이 가드에 걸려 사람이 원인을 조사하게 된다(무음 무력화는 불가능하다).
 */
async function findOpenChangeRequests(spaceId, token) {
  const items = [];
  const seenIds = new Set();
  for (const status of OPEN_CR_QUERY_STATUSES) {
    let cursor = null;
    do {
      const qs = new URLSearchParams({ limit: "50", status });
      if (cursor) qs.set("page", cursor);
      const response = await requestWithRetry(
        `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests?${qs.toString()}`,
        { method: "GET", headers: authHeaders(token) },
        `열린 change-request 조회(status=${status})`,
      );
      const data = await response.json();
      if (Array.isArray(data.items)) {
        for (const cr of data.items) {
          if (!cr || !cr.id || seenIds.has(cr.id)) continue;
          seenIds.add(cr.id);
          items.push(cr);
        }
      }
      cursor = data.next && data.next.page ? data.next.page : null;
    } while (cursor);
  }
  return items.filter((cr) => cr && cr.subject === CR_SUBJECT && !isClosedCrStatus(cr.status));
}

// createChangeRequest/pushContent는 서버 상태를 바꾸는 비멱등 POST다(idempotent=false) —
// 응답 유실 시 재시도하면 CR이 중복 생성되거나 content 오퍼레이션이 중복 적용될 수 있어서,
// 429(Retry-After 존중)만 재시도하고 5xx·네트워크 오류는 requestWithRetry가 즉시 던진다.

async function createChangeRequest(spaceId, token) {
  const response = await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests`,
    { method: "POST", headers: authHeaders(token), body: JSON.stringify({ subject: CR_SUBJECT }) },
    "change-request 생성",
    false,
  );
  const data = await response.json();
  if (!data || !data.id) throw new MigrationError("change-request 생성 응답에 id 필드가 없습니다.", 1);
  return data;
}

async function pushContent(spaceId, token, crId, changes) {
  const response = await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests/${encodeURIComponent(crId)}/content`,
    { method: "POST", headers: authHeaders(token), body: JSON.stringify({ changes }) },
    "content 갱신",
    false,
  );
  return response.json();
}

async function getContentPages(spaceId, token, crId) {
  const response = await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests/${encodeURIComponent(crId)}/content/pages`,
    { method: "GET", headers: authHeaders(token) },
    "CR 페이지 목록 조회",
  );
  const data = await response.json();
  return Array.isArray(data.pages) ? data.pages : Array.isArray(data.items) ? data.items : [];
}

/**
 * pageId를 이미 아는 페이지(--existing-map 대상)를 직접 조회한다 — 제목 매칭에 기대지
 * 않으므로 기존 페이지의 실제 제목이 매니페스트와 달라도 정확한 페이지를 가져온다.
 */
async function getPageById(spaceId, token, crId, pageId) {
  const response = await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests/${encodeURIComponent(crId)}/content/page/${encodeURIComponent(pageId)}`,
    { method: "GET", headers: authHeaders(token) },
    "CR 페이지 단건 조회",
  );
  return response.json();
}

/**
 * 페이지 목록 응답을 id → page 맵으로 평탄화한다.
 * flat(`pages: [...]`)과 nested(`pages[].pages[]`) 양쪽을 모두 수용하고 id 기준으로
 * dedupe한다 — 실제 응답 형태를 확인하기 전이므로 어느 쪽이든 신규 ID diff 관측이
 * 동작해야 한다(헤더 주석 "실토큰 첫 실행 시 확인" (1)).
 */
function flattenPageMap(pages, acc) {
  const map = acc || new Map();
  if (!Array.isArray(pages)) return map;
  for (const page of pages) {
    if (!page || typeof page !== "object") continue;
    if (page.id && !map.has(page.id)) map.set(page.id, page);
    if (Array.isArray(page.pages)) flattenPageMap(page.pages, map);
  }
  return map;
}

async function snapshotPageIds(spaceId, token, crId) {
  return new Set(flattenPageMap(await getContentPages(spaceId, token, crId)).keys());
}

/**
 * insert 직후 "이번 pass가 만든 신규 페이지"를 신규 ID diff로만 관측한다.
 *
 * beforeIds: insert 직전 스냅샷. (afterIds − beforeIds)가 신규 집합이며, 그 안에서만
 * 제목으로 엔트리를 매핑한다 — 한 pass의 insert 제목들은 서로 유일하므로(I-11) 충돌이
 * 불가능하고, 포털에 같은 제목의 기존 페이지가 있어도 beforeIds에 들어 있어 후보에서
 * 자동으로 빠진다. 부모 관계는 검증하지 않는다(사람이 CR 리뷰에서 확인).
 *
 * 관측 수가 기대와 다르면 조용히 넘어가지 않고 진단을 찍고 exit 1로 죽는다 — 페이지
 * 목록 응답 형태가 예상과 다를 때 가장 먼저 드러나는 지점이다.
 */
async function observeInsertedPages(spaceId, token, crId, beforeIds, entries, label) {
  const expected = entries.length;
  let newPages = [];
  // OBSERVE_BACKOFF_MS.length + 1회 조회(최초 1회 + 백오프마다 재조회)로, 선언된
  // 백오프 값을 전부 실제로 소진한다(과거 결함: 마지막 백오프 값이 죽은 값이었다 —
  // 조회 루프가 마지막 조회 뒤 재조회 없이 바로 종료해 마지막 대기가 쓰일 자리가 없었다).
  for (let attempt = 0; ; attempt++) {
    const after = flattenPageMap(await getContentPages(spaceId, token, crId));
    newPages = [...after.values()].filter((p) => !beforeIds.has(p.id));
    if (newPages.length >= expected || attempt >= OBSERVE_BACKOFF_MS.length) break;
    await sleep(OBSERVE_BACKOFF_MS[attempt]);
  }

  // 여기서부터의 MigrationError는 RECOVERY_HINT를 자체적으로 붙이지 않는다 — 호출부
  // (runExecutionMode)가 withCrRef()로 감싸 CR 참조와 함께 한 번만 붙인다. 이 함수는
  // CR 생성 이후에만 호출되므로 CR 참조 없이 복구 안내만 나가는 경우가 없다.
  if (newPages.length !== expected) {
    throw new MigrationError(
      `${label}: 신규 페이지 관측 수가 기대와 다릅니다 (기대 ${expected}건, 관측 ${newPages.length}건).\n` +
        `  기대한 제목: ${JSON.stringify(entries.map((e) => e.title))}\n` +
        `  관측된 신규 제목: ${JSON.stringify(newPages.map((p) => p.title))}\n` +
        `  가능한 원인: (a) 페이지 목록 응답 형태가 예상과 달라 평탄화가 어긋남, ` +
        `(b) insert가 일부만 반영됨, (c) 다른 사용자가 같은 CR을 동시에 수정 중, ` +
        `(d) 이전 실행/재시도로 이 CR에 이미 같은 insert가 한 번 더 적용되어 있음(비멱등 POST 중복 적용).`,
      1,
    );
  }

  const byTitle = new Map();
  const dupes = new Set();
  for (const page of newPages) {
    if (byTitle.has(page.title)) dupes.add(page.title);
    byTitle.set(page.title, page);
  }
  if (dupes.size) {
    throw new MigrationError(
      `${label}: 신규 페이지 제목이 중복입니다: ${JSON.stringify([...dupes])} — 어느 페이지가 어느 문서인지 판단할 수 없습니다.`,
      1,
    );
  }

  const result = new Map();
  for (const entry of entries) {
    const page = byTitle.get(entry.title);
    if (!page || !page.id) {
      throw new MigrationError(
        `${label}: 제목 "${entry.title}"에 해당하는 신규 페이지를 찾지 못했습니다.\n` +
          `  관측된 신규 제목: ${JSON.stringify(newPages.map((p) => p.title))}`,
        1,
      );
    }
    result.set(entry.key, page);
  }
  return result;
}

// ============================================================
// state.json — 감사 기록 전용 (쓰기만 한다. 이 스크립트는 절대 읽지 않는다)
// ============================================================

function saveState(out, state) {
  writeJsonLF(path.join(out, "state.json"), state);
}

// ============================================================
// 메인
// ============================================================

async function main() {
  let opts;
  try {
    opts = parseArgs(process.argv.slice(2));
  } catch (err) {
    if (err instanceof MigrationError) {
      console.error(`❌ ${err.message}`);
      process.exit(err.exitCode);
    }
    throw err;
  }

  if (opts.help) {
    console.log(USAGE);
    process.exit(0);
  }

  if (!opts.out) {
    console.error(`❌ --out은 필수입니다(산출물 출력 디렉토리를 명시적으로 지정하세요).\n\n${USAGE}`);
    process.exit(2);
  }

  const warnings = [];
  const warn = (message) => {
    warnings.push(message);
    console.warn(`⚠️  ${message}`);
  };
  if (!opts.parent) {
    warn("--parent가 지정되지 않아 최상위(space root)에 삽입합니다. 리뷰 시 GitBook UI에서 올바른 위치로 옮기세요.");
  }
  if (!opts.existingMap) {
    warn(
      `--existing-map이 지정되지 않아 ${DOCUMENT_ENTRIES.length}개 문서를 전부 새로 insert합니다. 포털에 동일 제목의 ` +
        "기존 페이지가 남아 있다면 중복이 생기니 리뷰 시 GitBook UI에서 정리하세요.",
    );
  }

  let pipeline;
  let invariantRows;
  try {
    pipeline = runTransformPipeline(opts);
    invariantRows = runInvariants(pipeline, opts);
  } catch (err) {
    if (err instanceof MigrationError) {
      console.error(`❌ ${err.message}`);
      process.exit(err.exitCode);
    }
    throw err;
  }

  if (invariantRows.some((r) => !r.pass)) {
    // 산출물은 감사를 위해 그대로 쓴다(무엇이 실패했는지 report.txt로 확인 가능).
    writeAllArtifacts(pipeline, invariantRows, opts, warnings);
    console.error(`❌ 불변식 위반이 있습니다. ${path.join(opts.out, "report.txt")}를 확인하세요.`);
    process.exit(2);
  }

  if (opts.dryRun) {
    writeAllArtifacts(pipeline, invariantRows, opts, warnings);
    console.log("🧪 --dry-run: 네트워크 호출 없이 산출물을 작성했습니다.");
    console.log(`   출력: ${opts.out}`);
    console.log(`   불변식: ${invariantRows.length}건 전부 PASS`);
    process.exit(0);
  }

  // 실행 모드 — env 게이트 (self-gate). "사람이 1회 손으로 돌리는 이관 도구"라 env 미비를
  // 성공(exit 0)으로 보고하지 않는다. upload-changelog-to-gitbook.js와 의도적으로 다른 종료 코드다.
  const token = process.env.GITBOOK_TOKEN;
  const spaceId = process.env.GITBOOK_SPACE_ID;
  if (!token || !spaceId) {
    // 사전 검사 산출물은 감사용으로 항상 남긴다.
    writeAllArtifacts(pipeline, invariantRows, opts, warnings);
    console.error(
      "❌ GITBOOK_TOKEN / GITBOOK_SPACE_ID가 설정되지 않았습니다. " +
        "네트워크 호출 없이 종료합니다. 먼저 --dry-run으로 산출물을 검토하세요.",
    );
    process.exit(2);
  }

  try {
    await runExecutionMode(opts, pipeline, invariantRows, warnings, token, spaceId);
  } catch (err) {
    if (err instanceof MigrationError) {
      console.error(`❌ ${err.message}`);
      process.exit(err.exitCode);
    }
    console.error(`❌ 예상치 못한 오류: ${err.message}\n   ${RECOVERY_HINT}`);
    process.exit(1);
  }
}

async function runExecutionMode(opts, pipeline, invariantRows, warnings, token, spaceId) {
  console.log(`📤 GitBook 이관 시작 (space: ${mask(spaceId)})`);

  // --- 0. 사전 감사 산출물, 1. 열린 CR 충돌 처리, 2. CR 생성 -----------------
  // CR이 실제로 생성되기 전(아래 세 단계)의 실패는 "archive 후 재실행" 안내가 틀린
  // 안내다 — archive할 CR이 아직 없다. 그래서 이 구간 전체를 감싸 MigrationError가
  // 아닌 예외(--out 쓰기 실패, 재시도 소진으로 인한 평범한 Error 등)를 "CR 미생성"
  // 메시지로 바꾼다. MigrationError(가드가 의도적으로 던진 것, 예: 아래 "같은 제목의
  // 열린 CR" 충돌)는 이미 정확한 안내를 담고 있으므로 그대로 다시 던진다.
  let created;
  try {
    // 사전 감사 산출물(예측값 기준)을 네트워크 첫 호출보다 먼저 쓴다 — writeAllArtifacts()의
    // docstring 계약("실행 모드에서도 같은 산출물을 먼저 쓰고 네트워크를 탄다")과 실제 호출
    // 순서를 일치시킨다. 같은 try 안이므로 이 호출이 실패해도(예: --out에 쓰기 권한 없음)
    // RECOVERY_HINT 없이 "아직 CR이 생성되지 않았습니다" 쪽 실패로 떨어진다.
    writeAllArtifacts(pipeline, invariantRows, opts, warnings);

    // CR id는 토큰/space id와 달리 비밀이 아니다 — urls.app이 없을 때 mask()로 가리면
    // 이 도구가 사람에게 넘기는 유일한 산출물(CR 위치)을 사람이 찾을 수 없게 된다.
    const openCrs = await findOpenChangeRequests(spaceId, token);
    if (openCrs.length) {
      // status는 CR id와 같은 논리로 비밀이 아니다 — 원값 그대로 보여줘야 사람이
      // "정말 열려 있는 게 맞는지, 혹시 필드명이 달라 오탐인지"를 판단할 수 있다.
      // undefined도 그대로 문자열 "undefined"로 보여준다(값이 없다는 사실 자체가 진단
      // 정보다 — isClosedCrStatus()가 이런 값을 보수적으로 "열림"으로 취급하는 이유이기도 하다).
      const describe = (cr) => {
        const loc = (cr.urls && cr.urls.app) || `id:${cr.id}`;
        return `${loc} (status: ${cr.status === undefined ? "undefined" : JSON.stringify(cr.status)})`;
      };
      if (!opts.ignoreOpenCr) {
        throw new MigrationError(
          `같은 제목("${CR_SUBJECT}")의 열린(archived 또는 merged로 확인되지 않은) CR이 ${openCrs.length}건 있습니다:\n` +
            openCrs.map((cr) => `    - ${describe(cr)}`).join("\n") +
            `\n  아직 이 실행으로 생성한 CR은 없습니다. GitBook UI에서 위 CR을 검토 후 처리(머지 또는 archive)하고 ` +
            `다시 실행하세요. 그래도 새 CR을 만들려면 --ignore-open-cr를 주세요` +
            `(이 도구는 CR을 자동으로 정리하지 않습니다).`,
          2,
        );
      }
      // --ignore-open-cr로 강행한 사실은 report.txt 경고 절에도 남긴다 — 무시한 CR을
      // 나중에 리뷰어가 report.txt만 보고도 추적할 수 있어야 한다(콘솔 로그는 휘발성).
      // 이 시점에 즉시 report.txt를 다시 쓴다 — 이후 단계(CR 생성, pass1/2 관측 등)가
      // 실패해도 "강행했다"는 사실 자체는 이미 디스크에 남아 있어야 하기 때문이다
      // (report.txt는 실행이 끝까지 성공해야만 최신화되는 게 아니다. links 단계에서
      // 한 번 더 최종값으로 덮어쓴다 — 아래 "report.txt도 ... 다시 쓴다" 지점).
      const ignoreMsg = `--ignore-open-cr: 열린 CR ${openCrs.length}건을 무시하고 새 CR을 만듭니다: ${openCrs
        .map(describe)
        .join(", ")}`;
      warnings.push(ignoreMsg);
      console.warn(`⚠️  ${ignoreMsg}`);
      writeReport(path.join(opts.out, "report.txt"), invariantRows, opts, warnings);
    }

    created = await createChangeRequest(spaceId, token);
  } catch (err) {
    if (err instanceof MigrationError) throw err;
    throw new MigrationError(
      `CR 생성 준비 중 실패했습니다: ${err.message}\n` +
        `  아직 CR이 생성되지 않았습니다 — 원인을 해결한 뒤 스크립트를 다시 실행하세요.`,
      1,
    );
  }
  const crId = created.id;
  const crUrl = (created.urls && created.urls.app) || null;
  console.log(`   CR 생성됨: ${crUrl || `id:${crId}`}`);

  // state.json은 감사 기록 전용이다 — 다음 실행이 읽지 않는다(재개 없음).
  const state = { crId, crUrl, subject: CR_SUBJECT, completed: [] };
  saveState(opts.out, state);

  // 이 지점(=CR 생성 성공) 이후의 실패는 전부 "사람이 CR을 archive하고 재실행"으로
  // 복구한다 — RECOVERY_HINT는 여기서부터만 붙는다.
  const fail = (message, code) =>
    new MigrationError(`${message}\n  CR: ${crUrl || `id:${crId}`}\n  ${RECOVERY_HINT}`, code || 1);

  // CR 생성 이후 네트워크/관측 단계에서 던져지는 예외는 예외 종류(재시도 소진으로 인한
  // 평범한 Error든, observeInsertedPages()가 던지는 MigrationError든)와 무관하게 전부
  // 이 래퍼를 거쳐야 한다 — 과거 결함: pushAll()(재시도 소진)과 observeInsertedPages()의
  // 예외는 fail()을 거치지 않아 최종 에러 줄에 CR 참조가 없었다. 이제 CR 생성 이후의 모든
  // await 호출을 이 래퍼로 감싸 CR 참조 누락 경로 자체를 없앤다.
  const withCrRef = async (promise) => {
    try {
      return await promise;
    } catch (err) {
      throw fail(err.message, err instanceof MigrationError ? err.exitCode : 1);
    }
  };

  const existingMap = pipeline.existingMap;
  const groupPageIds = new Map(); // groupKey -> pageId
  const pageIdBySource = new Map(); // source -> pageId

  /** 관측된 페이지(또는 existing-map 페이지)를 pageId/URL 맵에 반영한다. */
  const recordDocument = (entry, page) => {
    pageIdBySource.set(entry.source, page.id);
    try {
      pipeline.resolveCtx.pageMap.set(entry.source, { url: buildPageUrl(entry, page, opts) });
    } catch (err) {
      throw fail(err.message, err instanceof MigrationError ? err.exitCode : 1);
    }
  };

  /**
   * --existing-map 대상 문서를 pageId로 직접 조회해 반영한다. pageId는 CR id와 마찬가지로
   * 비밀이 아니다(토큰/space id만 비밀) — 실패 시 어느 pageId를 조회하다 실패했는지, 서버가
   * 어떤 HTTP 상태를 돌려줬는지 mask 없이 원문 그대로 출력해야 사람이 --existing-map JSON을
   * 열어 그 값을 바로 대조할 수 있다. .catch(() => null)로 오류를 삼키면 이 정보가 전부
   * 사라지므로 대신 try/catch로 err.message(HTTP 상태 포함)를 그대로 실어 던진다.
   */
  const resolveExistingDocs = async (entries) => {
    for (const entry of entries) {
      const pageId = existingMap.get(entry.source);
      let page;
      try {
        page = await getPageById(spaceId, token, crId, pageId);
      } catch (err) {
        throw fail(`--existing-map의 "${entry.source}"(pageId: ${pageId}) 조회에 실패했습니다: ${err.message}`);
      }
      if (!page || !page.id) {
        throw fail(`--existing-map의 "${entry.source}"(pageId: ${pageId}) 조회 응답에 id 필드가 없습니다.`);
      }
      recordDocument(entry, page);
    }
  };

  /** ops 배치를 순서대로 push한다. */
  const pushAll = async (ops) => {
    for (const chunk of ops) {
      if (!chunk.changes.length) continue;
      await pushContent(spaceId, token, crId, chunk.changes);
      await sleep(OP_DELAY_MS);
    }
  };

  // --- 3. pass1: 그룹 3 + 최상위 문서 1 --------------------------------------
  const p1Entries = pass1Entries(existingMap);
  const beforeP1 = await withCrRef(snapshotPageIds(spaceId, token, crId));
  await withCrRef(pushAll(buildPass1Ops(pipeline, opts)));
  console.log(`   pass1 삽입 완료 (${p1Entries.length}건: 그룹 + 최상위 문서)`);

  const p1Observed = await withCrRef(observeInsertedPages(spaceId, token, crId, beforeP1, p1Entries, "pass1 관측"));
  for (const entry of p1Entries) {
    const page = p1Observed.get(entry.key);
    if (entry.type === "group") groupPageIds.set(entry.key, page.id);
    else recordDocument(entry, page);
  }
  await resolveExistingDocs(DOCUMENT_ENTRIES.filter((e) => e.pass === 1 && existingMap.has(e.source)));
  if (groupPageIds.size !== GROUP_KEYS.length) {
    throw fail(`그룹 pageId를 ${GROUP_KEYS.length}개 확보해야 하는데 ${groupPageIds.size}개만 확보했습니다.`);
  }
  state.completed.push({ pass: 1, inserted: p1Entries.length, groups: Object.fromEntries(groupPageIds) });
  saveState(opts.out, state);

  // 관측된 실제 그룹 pageId로 ops.pass2.json을 다시 쓴다 — 아니면 감사 산출물이
  // 영원히 `<pass1:key>` 플레이스홀더로 남아 실제로 push한 into 값과 어긋난다.
  writeJsonLF(path.join(opts.out, "ops.pass2.json"), buildPass2Ops(pipeline, groupPageIds));

  // --- 4. pass2: 자식 문서 10 ------------------------------------------------
  const p2Entries = pass2Entries(existingMap);
  if (p2Entries.length) {
    const beforeP2 = await withCrRef(snapshotPageIds(spaceId, token, crId));
    await withCrRef(pushAll(buildPass2Ops(pipeline, groupPageIds)));
    console.log(`   pass2 삽입 완료 (${p2Entries.length}건: 자식 문서)`);

    const p2Observed = await withCrRef(observeInsertedPages(spaceId, token, crId, beforeP2, p2Entries, "pass2 관측"));
    for (const entry of p2Entries) recordDocument(entry, p2Observed.get(entry.key));
  } else {
    console.log("   pass2 건너뜀 (--existing-map으로 자식 문서 10건이 전부 억제됨)");
  }
  await resolveExistingDocs(DOCUMENT_ENTRIES.filter((e) => e.pass === 2 && existingMap.has(e.source)));
  state.completed.push({ pass: 2, inserted: p2Entries.length });
  saveState(opts.out, state);

  // --- 5. links: 센티넬 → 관측 URL 재작성 후 update_page × 14 (문서 11 + 그룹 색인 3) ----
  if (pageIdBySource.size !== DOCUMENT_ENTRIES.length) {
    throw fail(
      `문서 pageId를 ${DOCUMENT_ENTRIES.length}건 확보해야 하는데 ${pageIdBySource.size}건만 확보했습니다: ` +
        JSON.stringify(DOCUMENT_ENTRIES.filter((e) => !pageIdBySource.has(e.source)).map((e) => e.source)),
    );
  }

  const observedRecords = [];
  for (const entry of DOCUMENT_ENTRIES) {
    const rebuilt = buildDocumentBody(entry, pipeline.sources.get(entry.source), "resolve", pipeline.resolveCtx, observedRecords);
    pipeline.bodies[entry.source].resolvedBody = rebuilt.body;
  }
  // 그룹 색인도 관측 URL로 다시 만든다. observedRecords에 함께 실어 아래 I-21 관측 재검사가
  // 그룹 링크까지 커버하게 한다.
  for (const groupKey of GROUP_KEYS) {
    pipeline.groupBodies[groupKey].resolvedBody = buildGroupBody(groupKey, "resolve", pipeline.resolveCtx, observedRecords);
  }

  // I-21 재검사(관측 URL 기준) — 예측 URL로 통과했더라도 관측 URL이 섹션 접두사를
  // 빠뜨렸다면 여기서 push 전에 죽는다.
  const badPrefix = findBadInternalLinks(pipeline.resolveCtx.pageMap, observedRecords, opts);
  if (badPrefix.length) {
    throw fail(
      `I-21 위반(관측 URL): 다음 내부 링크가 "${internalLinkPrefix(opts)}" 접두사로 시작하지 않습니다:\n` +
        badPrefix.map((b) => `    - ${b}`).join("\n"),
    );
  }

  // I-6 재확인: 센티넬 잔존 0건이어야 실제로 push한다(문서 + 그룹 색인 본문 모두).
  const sentinelLeft = DOCUMENT_ENTRIES.filter((e) => pipeline.bodies[e.source].resolvedBody.includes(SENTINEL_HOST));
  const groupSentinelLeft = GROUP_KEYS.filter((k) => pipeline.groupBodies[k].resolvedBody.includes(SENTINEL_HOST));
  if (sentinelLeft.length || groupSentinelLeft.length) {
    throw fail(
      `센티넬 잔존(링크 해석 실패 가능성): 문서=${JSON.stringify(sentinelLeft.map((e) => e.source))} ` +
        `그룹=${JSON.stringify(groupSentinelLeft)}`,
    );
  }

  // links.csv도 관측 URL 기준으로 다시 만든다 — auditLinks()를 관측이 반영된
  // pipeline.resolveCtx.pageMap으로 재실행해서, ops.links.json/bodies/*.md와 마찬가지로
  // 실제로 push하는 값과 감사 대장이 어긋나지 않게 한다. auditLinks()는 원본 텍스트를
  // 그대로 스캔하므로 원본 줄 번호와 REMOVED_WITH_TABLE_ROW 행(README 표 삭제)도 그대로
  // 보존된다 — dry-run이 쓰는 예측 기준 links.csv와 같은 형식.
  const observedLinksCsvRows = [];
  for (const entry of DOCUMENT_ENTRIES) {
    observedLinksCsvRows.push(...auditLinks(entry.source, pipeline.sources.get(entry.source), pipeline.resolveCtx));
  }
  pipeline.linksCsvRows = observedLinksCsvRows;

  // 실제로 push하는 페이로드를 관측값 그대로 산출물에 남긴다(사후 감사용).
  const linksOps = buildLinksOps(pipeline, pageIdBySource, groupPageIds);
  writeJsonLF(path.join(opts.out, "ops.links.json"), linksOps);
  for (const entry of DOCUMENT_ENTRIES) {
    writeFileLF(path.join(opts.out, "bodies", `${entry.slug}.md`), pipeline.bodies[entry.source].resolvedBody);
  }
  for (const g of MANIFEST.filter((e) => e.type === "group")) {
    writeFileLF(path.join(opts.out, "bodies", `${g.slug}.md`), pipeline.groupBodies[g.key].resolvedBody);
  }
  writeLinksCsv(path.join(opts.out, "links.csv"), pipeline.linksCsvRows);

  // slug drift 경고: 예측 URL(manifest.json)과 방금 확정된 관측 URL이 다른 문서를
  // report.txt 경고 절에 남긴다. I-21 재검사는 이미 위에서 통과했으므로(그렇지 않으면
  // 이 지점에 도달하지 못한다) 이 경고는 "틀렸다"가 아니라 "예측이 빗나갔으니 실제
  // 링크를 눈으로 한 번 확인하라"는 신호다.
  for (const w of computeSlugDrift(pipeline, opts)) warnings.push(w);
  // report.txt도 다른 산출물들과 마찬가지로 관측 직후(=push 직전) 최종값으로 다시 쓴다 —
  // --ignore-open-cr 경고·slug drift 경고가 이 시점에야 전부 확정되기 때문이다.
  writeReport(path.join(opts.out, "report.txt"), invariantRows, opts, warnings);

  await withCrRef(pushAll(linksOps));
  console.log(
    `   links 갱신 완료 (문서 ${DOCUMENT_ENTRIES.length}개 + 그룹 색인 ${GROUP_KEYS.length}개 본문을 관측된 실제 URL로 재작성)`,
  );
  state.completed.push({ pass: "links", updated: DOCUMENT_ENTRIES.length });
  saveState(opts.out, state);

  console.log("");
  console.log(`✅ CR 준비 완료: ${crUrl || `id:${crId}`}`);
  console.log("   리뷰어가 GitBook UI에서 검토·수정 후 수동으로 반영하세요. 이 도구는 CR 생성까지만 합니다.");
  console.log(`   산출물(감사용): ${opts.out}`);
}

main();
