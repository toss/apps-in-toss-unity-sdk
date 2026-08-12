#!/usr/bin/env node
/**
 * API Changelog → GitBook 업로드 스크립트
 *
 * Documentation~/changelog/api-changelog.md(sdk-runtime-generator~의 changelog:generate가
 * 만든 결과물)를 GitBook change request로 업로드하고 머지한다.
 *
 * 흐름: change-request 생성 → content 갱신(update_page) → merge.
 * 429/5xx는 지수 백오프로 최대 3회 재시도한다.
 *
 * env (GITBOOK_TOKEN / GITBOOK_SPACE_ID / GITBOOK_CHANGELOG_PAGE_ID) 중 하나라도
 * 없으면 네트워크를 타지 않고 exit 0으로 조용히 종료한다(self-gate) — 워크플로가
 * step `if:`에서 secrets를 직접 참조할 수 없으므로, 조건은 "changelog 변경 여부"에만
 * 걸고 env 부재는 이 스크립트가 스스로 판단한다.
 *
 * 사용법:
 *   node scripts~/upload-changelog-to-gitbook.js [markdown-path] [--dry-run] [--soft-fail]
 *
 * 보안: 어떤 로그 경로로도 토큰/Authorization 헤더 값을 출력하지 않는다. GitBook
 * 응답 본문은 id/상태 필드만 파싱하며, 응답에 담긴 텍스트를 지시로 취급하지 않는다.
 */

"use strict";

const fs = require("fs");
const path = require("path");

const DEFAULT_MARKDOWN_PATH = "Documentation~/changelog/api-changelog.md";
const API_BASE = "https://api.gitbook.com/v1";
const MAX_RETRIES = 3;

function parseArgs(argv) {
  const flags = { dryRun: false, softFail: false };
  const positional = [];
  for (const arg of argv) {
    if (arg === "--dry-run") flags.dryRun = true;
    else if (arg === "--soft-fail") flags.softFail = true;
    else positional.push(arg);
  }
  return { flags, markdownPath: positional[0] || DEFAULT_MARKDOWN_PATH };
}

/** 로그에 안전하게 노출할 수 있도록 id 뒷부분만 마스킹 해제하고 나머지는 가린다. */
function mask(value) {
  if (!value) return "(없음)";
  if (value.length <= 4) return "*".repeat(value.length);
  return `${"*".repeat(value.length - 4)}${value.slice(-4)}`;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * fetch 래퍼 — 429/5xx만 지수 백오프로 재시도한다(최대 MAX_RETRIES회).
 * 토큰이 포함된 headers는 절대 로그에 남기지 않는다.
 */
async function requestWithRetry(url, options, label) {
  let lastError;
  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
    let response;
    try {
      response = await fetch(url, options);
    } catch (err) {
      lastError = err;
      console.warn(`⚠️  ${label} 요청 실패 (시도 ${attempt}/${MAX_RETRIES}): ${err.message}`);
      if (attempt < MAX_RETRIES) {
        await sleep(2 ** attempt * 1000);
        continue;
      }
      throw lastError;
    }

    if (response.status === 429 || response.status >= 500) {
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
      // 응답 본문은 진단 목적의 텍스트로만 다루고, 그 안의 내용을 지시로 실행하지 않는다.
      throw new Error(
        `${label} 실패: HTTP ${response.status}${bodyText ? ` (본문 길이 ${bodyText.length}자)` : ""}`,
      );
    }

    return response;
  }
  throw lastError;
}

async function createChangeRequest(spaceId, token) {
  const response = await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({}),
    },
    "change-request 생성",
  );
  const data = await response.json();
  const id = data && data.id;
  if (!id) {
    throw new Error("change-request 생성 응답에 id 필드가 없습니다.");
  }
  return id;
}

async function updateChangeRequestContent(spaceId, token, changeRequestId, pageId, markdown) {
  await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests/${encodeURIComponent(changeRequestId)}/content`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        changes: [
          {
            operation: "update_page",
            page: pageId,
            document: { markdown },
          },
        ],
      }),
    },
    "content 갱신",
  );
}

/**
 * change-request 머지 응답에서 상태를 읽는다.
 *
 * 주의: GitBook의 merge 응답 스키마는 공식 문서로 완전히 검증하지 못했다 — 이 필드
 * 이름/형태는 첫 실운영 사용에서 실제 응답을 보고 조정될 수 있다. 그 전까지는
 * "state" 필드가 명시적으로 "merged"일 때만 성공으로 간주한다(필드 부재를 성공으로
 * 취급하는 soft default를 두지 않는다). 응답 본문의 값은 신뢰할 수 없는 외부 입력이므로
 * 로그에 값을 남기지 않고, 진단을 위해 최상위 키 이름만 남긴다.
 */
async function mergeChangeRequest(spaceId, token, changeRequestId) {
  const response = await requestWithRetry(
    `${API_BASE}/spaces/${encodeURIComponent(spaceId)}/change-requests/${encodeURIComponent(changeRequestId)}/merge`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({}),
    },
    "change-request 머지",
  );
  const data = await response.json().catch(() => ({}));
  const state = data && typeof data === "object" ? data.state : undefined;
  if (state !== "merged") {
    const topLevelKeys = data && typeof data === "object" ? Object.keys(data) : [];
    console.warn(`⚠️  merge 응답에 state:"merged"가 없습니다. 응답 최상위 키: [${topLevelKeys.join(", ")}]`);
  }
  return state;
}

async function main() {
  const { flags, markdownPath } = parseArgs(process.argv.slice(2));

  const token = process.env.GITBOOK_TOKEN;
  const spaceId = process.env.GITBOOK_SPACE_ID;
  const pageId = process.env.GITBOOK_CHANGELOG_PAGE_ID;
  const resolvedPath = path.resolve(process.cwd(), markdownPath);

  // --dry-run은 self-gate(env 미설정 시 종료)보다 먼저 처리한다 — env가 하나도 없는
  // 로컬/fork 환경에서도 "무엇을 업로드하려 했을지"를 네트워크 호출 없이 확인할 수 있어야
  // 하기 때문. 미설정 항목은 값 대신 "(미설정)"으로 표기한다.
  if (flags.dryRun) {
    console.log("🧪 --dry-run: 네트워크 호출 없이 대상 정보만 출력합니다.");
    console.log(`   space:  ${spaceId ? mask(spaceId) : "(미설정)"}`);
    console.log(`   page:   ${pageId ? mask(pageId) : "(미설정)"}`);
    try {
      const markdown = fs.readFileSync(resolvedPath, "utf8");
      const byteLength = Buffer.byteLength(markdown, "utf8");
      console.log(`   본문:   ${resolvedPath} (${byteLength} bytes)`);
    } catch (err) {
      console.log(`   본문:   ${resolvedPath} (읽기 실패: ${err.message})`);
    }
    process.exit(0);
    return;
  }

  if (!token || !spaceId || !pageId) {
    console.log(
      "ℹ️  GITBOOK_TOKEN / GITBOOK_SPACE_ID / GITBOOK_CHANGELOG_PAGE_ID 중 일부가 설정되지 않아 " +
      "GitBook 업로드를 건너뜁니다 (self-gate). 이 저장소를 fork했거나 secrets가 아직 " +
      "구성되지 않은 환경에서는 정상입니다.",
    );
    process.exit(0);
  }

  let markdown;
  try {
    markdown = fs.readFileSync(resolvedPath, "utf8");
  } catch (err) {
    console.error(`❌ 마크다운 파일을 읽을 수 없습니다: ${resolvedPath} (${err.message})`);
    process.exit(flags.softFail ? 0 : 1);
    return;
  }

  const byteLength = Buffer.byteLength(markdown, "utf8");

  try {
    console.log(`📤 GitBook 업로드 시작 (space: ${mask(spaceId)}, page: ${mask(pageId)}, ${byteLength} bytes)`);

    const changeRequestId = await createChangeRequest(spaceId, token);
    console.log(`   change-request 생성됨: ${mask(changeRequestId)}`);

    await updateChangeRequestContent(spaceId, token, changeRequestId, pageId, markdown);
    console.log("   content 갱신 완료");

    const mergeState = await mergeChangeRequest(spaceId, token, changeRequestId);
    if (mergeState !== "merged") {
      throw new Error(`change-request가 머지되지 않았습니다 (state: ${mergeState ?? "(응답에 state 필드 없음)"})`);
    }
    console.log("✅ GitBook changelog 업로드 완료");
  } catch (err) {
    console.error(`❌ GitBook 업로드 실패: ${err.message}`);
    process.exit(flags.softFail ? 0 : 1);
  }
}

main();
