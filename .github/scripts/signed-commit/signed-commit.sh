#!/usr/bin/env bash
set -euo pipefail
#
# signed-commit.sh - 스테이징된 변경을 GitHub REST git data API로 커밋해 Verified 서명을 받는다
#
# 왜 이 스크립트가 있나:
#   main 브랜치 Ruleset은 required_signatures(서명 커밋 필수, bypass 없음)를 강제한다.
#   러너에서 `git commit` + `git push` 로 만든 github-actions[bot] 커밋은 서명이 없어
#   PR이 mergeable_state=blocked 로 남고 머지되지 않는다(regen-lockfiles·SDK 업데이트 PR 적체).
#   반면 GITHUB_TOKEN으로 REST git data API(blobs -> trees -> commits -> refs)를 호출해
#   만든 커밋은 GitHub이 웹 플랫폼 키로 자동 서명해 Verified가 된다.
#   ⚠️ commits 호출에 author/committer를 지정하면 서명되지 않는다 — 절대 넘기지 말 것.
#
# 동작:
#   1. `git diff-index --cached --raw -z --no-renames <parent>` 로 부모 대비 스테이징 변경을 수집
#      (추가·수정·삭제·모드 변경·이름변경은 삭제+추가로 취급, 실행 비트·심볼릭 링크 보존).
#      호출자가 먼저 `git add` 로 스테이징해 둬야 한다.
#   2. 작은 UTF-8 텍스트 파일은 tree 항목에 content를 인라인(blob 호출 생략),
#      바이너리·큰 파일은 POST git/blobs (base64). 수백 파일(Runtime/SDK, .meta)도 호출 수가 적다.
#   3. POST git/trees (base_tree=부모 tree, 삭제는 sha:null) -> POST git/commits
#      -> 응답의 verification.verified 확인(false면 ref를 건드리기 전에 실패)
#      -> 브랜치 ref 생성(POST git/refs) 또는 갱신(PATCH git/refs, FORCE 반영).
#   4. SYNC_LOCAL=true 면 새 커밋을 fetch해 로컬 체크아웃(브랜치+작업트리)을 그 커밋에 맞춘다.
#
# 환경변수:
#   GH_TOKEN      (필수) contents: write 권한 토큰 — 보통 ${{ github.token }}
#   REPO          (필수) owner/name
#   BRANCH        (필수) 대상 브랜치명 (heads/ 접두사 없이)
#   MESSAGE       (필수, 변경이 없으면 생략 가능) 커밋 메시지 전체
#   PARENT_SHA    (선택) 부모 커밋. 기본 HEAD
#   FORCE         (선택) true 면 기존 브랜치를 non-fast-forward로도 덮어쓴다. 기본 false
#   SYNC_LOCAL    (선택) 기본 true
#   REQUIRE_VERIFIED (선택) 기본 true. false 면 서명 여부를 검사하지 않는다
#
# 출력: GITHUB_OUTPUT 이 있으면 committed(true|false)·commit_sha 를 기록하고, stdout 마지막 줄에 sha를 찍는다.
# 변경이 없으면 아무것도 만들지 않고 committed=false 로 종료한다.

: "${GH_TOKEN:?GH_TOKEN 필요}"
: "${REPO:?REPO 필요}"
: "${BRANCH:?BRANCH 필요}"
MESSAGE="${MESSAGE:-}"
PARENT_SHA="${PARENT_SHA:-}"
FORCE="${FORCE:-false}"
SYNC_LOCAL="${SYNC_LOCAL:-true}"
REQUIRE_VERIFIED="${REQUIRE_VERIFIED:-true}"
export GH_TOKEN

# 인라인 한도: 파일당 / 전체 (tree 요청 본문이 과하게 커지지 않게)
INLINE_MAX_FILE=524288
INLINE_MAX_TOTAL=4194304

if [ -z "$PARENT_SHA" ]; then
  PARENT_SHA=$(git rev-parse HEAD)
fi
PARENT_SHA=$(git rev-parse --verify "${PARENT_SHA}^{commit}")
BASE_TREE=$(git rev-parse --verify "${PARENT_SHA}^{tree}")

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
ERR="$WORK/err"

set_output() {
  if [ -n "${GITHUB_OUTPUT:-}" ]; then
    echo "$1=$2" >> "$GITHUB_OUTPUT"
  fi
}

# gh api 래퍼: rate limit(403/429)·5xx·네트워크 일시 오류는 백오프 재시도, 그 외는 즉시 실패.
# 큰 payload는 호출자가 --input 파일로 넘긴다.
api() {
  local n=0 out
  while :; do
    if out=$(gh api "$@" 2> "$ERR"); then
      printf '%s' "$out"
      return 0
    fi
    n=$((n + 1))
    if [ "$n" -ge 4 ] || ! grep -qiE 'HTTP (403|429|5[0-9][0-9])|rate limit|timeout|connection|EOF' "$ERR"; then
      cat "$ERR" >&2
      return 1
    fi
    echo "::warning::gh api 일시 오류 — ${n}/3 재시도: $(head -c 200 "$ERR")" >&2
    sleep $((n * n * 3))
  done
}

# 1. 변경 수집
git diff-index --cached --raw -z --no-renames --no-abbrev "$PARENT_SHA" > "$WORK/raw"
if [ ! -s "$WORK/raw" ]; then
  echo "스테이징된 변경이 없습니다 — 커밋을 만들지 않습니다."
  set_output committed false
  set_output commit_sha ""
  exit 0
fi
# 변경이 없을 때는 메시지가 비어 있어도 된다(호출자가 변경 없음 케이스에서 메시지를 안 만들 수 있음)
if [ -z "$MESSAGE" ]; then
  echo "::error::MESSAGE 필요" >&2
  exit 1
fi

: > "$WORK/entries.jsonl"
inline_total=0
n_inline=0
n_blob=0
n_delete=0
n_ref=0

while IFS= read -r -d '' meta; do
  IFS= read -r -d '' path
  # meta: ":<oldmode> <newmode> <oldsha> <newsha> <status>"
  meta=${meta#:}
  read -r _ new_mode old_sha new_sha status <<< "$meta"

  case "$status" in
    D)
      jq -nc --arg p "$path" '{path:$p, mode:"100644", type:"blob", sha:null}' >> "$WORK/entries.jsonl"
      n_delete=$((n_delete + 1))
      continue
      ;;
    A | M | T) ;;
    *)
      echo "::error::지원하지 않는 변경 상태 '$status' ($path)" >&2
      exit 1
      ;;
  esac

  case "$new_mode" in
    100644 | 100755 | 120000) ;;
    160000)
      echo "::error::서브모듈(160000)은 지원하지 않습니다: $path" >&2
      exit 1
      ;;
    *)
      echo "::error::지원하지 않는 파일 모드 '$new_mode' ($path)" >&2
      exit 1
      ;;
  esac

  # 내용 불변(모드만 변경): 원격에 이미 있는 blob을 sha로 재사용
  if [ "$new_sha" = "$old_sha" ]; then
    jq -nc --arg p "$path" --arg m "$new_mode" --arg s "$new_sha" \
      '{path:$p, mode:$m, type:"blob", sha:$s}' >> "$WORK/entries.jsonl"
    n_ref=$((n_ref + 1))
    continue
  fi

  git cat-file blob "$new_sha" > "$WORK/content"
  size=$(wc -c < "$WORK/content" | tr -d ' ')

  # 인라인 가능 조건: 일반 파일, 비어있지 않음, 한도 이내, NUL 없음, 유효한 UTF-8
  # (tree의 content는 JSON 문자열이라 UTF-8 텍스트만 바이트 그대로 보존된다)
  can_inline=false
  if [ "$new_mode" != "120000" ] && [ "$size" -gt 0 ] && [ "$size" -le "$INLINE_MAX_FILE" ] \
    && [ $((inline_total + size)) -le "$INLINE_MAX_TOTAL" ]; then
    nonul=$(LC_ALL=C tr -d '\000' < "$WORK/content" | wc -c | tr -d ' ')
    if [ "$nonul" = "$size" ] && iconv -f UTF-8 -t UTF-8 "$WORK/content" > /dev/null 2>&1; then
      can_inline=true
    fi
  fi

  if [ "$can_inline" = "true" ]; then
    jq -nc --arg p "$path" --arg m "$new_mode" --rawfile c "$WORK/content" \
      '{path:$p, mode:$m, type:"blob", content:$c}' >> "$WORK/entries.jsonl"
    inline_total=$((inline_total + size))
    n_inline=$((n_inline + 1))
  else
    # base64 payload를 파일로 만들어 --input 으로 전송 (수 MB lockfile·바이너리 대응)
    {
      printf '{"encoding":"base64","content":"'
      base64 < "$WORK/content" | tr -d '\n'
      printf '"}'
    } > "$WORK/blob.json"
    blob_sha=$(api -X POST "repos/${REPO}/git/blobs" --input "$WORK/blob.json" --jq '.sha')
    if [ "$blob_sha" != "$new_sha" ]; then
      echo "::error::blob sha 불일치($path): 로컬 $new_sha != 원격 $blob_sha" >&2
      exit 1
    fi
    jq -nc --arg p "$path" --arg m "$new_mode" --arg s "$blob_sha" \
      '{path:$p, mode:$m, type:"blob", sha:$s}' >> "$WORK/entries.jsonl"
    n_blob=$((n_blob + 1))
  fi
done < "$WORK/raw"

echo "변경 수집: 인라인 ${n_inline}, blob 업로드 ${n_blob}, sha 재사용 ${n_ref}, 삭제 ${n_delete}"

# 2. tree -> commit
jq -s --arg base "$BASE_TREE" '{base_tree:$base, tree:.}' "$WORK/entries.jsonl" > "$WORK/tree.json"
TREE_SHA=$(api -X POST "repos/${REPO}/git/trees" --input "$WORK/tree.json" --jq '.sha')

# author/committer 를 넣지 않는다 — 지정하면 GitHub이 서명하지 않는다.
jq -n --arg msg "$MESSAGE" --arg tree "$TREE_SHA" --arg parent "$PARENT_SHA" \
  '{message:$msg, tree:$tree, parents:[$parent]}' > "$WORK/commit.json"
api -X POST "repos/${REPO}/git/commits" --input "$WORK/commit.json" > "$WORK/commit-resp.json"
COMMIT_SHA=$(jq -r '.sha' "$WORK/commit-resp.json")

# 3. 서명 가드: ref를 움직이기 전에 검사해 서명 안 된 커밋이 브랜치에 올라가지 않게 한다.
if [ "$REQUIRE_VERIFIED" = "true" ]; then
  VERIFIED=$(jq -r '.verification.verified // false' "$WORK/commit-resp.json")
  if [ "$VERIFIED" != "true" ]; then
    REASON=$(jq -r '.verification.reason // "unknown"' "$WORK/commit-resp.json")
    echo "::error::커밋 ${COMMIT_SHA}이 Verified가 아닙니다 (reason=${REASON}). 서명 필수 Ruleset 때문에 머지할 수 없어 중단합니다." >&2
    exit 1
  fi
  echo "✓ 커밋 ${COMMIT_SHA} Verified"
fi

# 4. ref 생성/갱신
REF_STATE=exists
if ! gh api "repos/${REPO}/git/ref/heads/${BRANCH}" > /dev/null 2> "$ERR"; then
  if grep -q 'HTTP 404' "$ERR"; then
    REF_STATE=missing
  else
    cat "$ERR" >&2
    exit 1
  fi
fi

if [ "$REF_STATE" = "missing" ]; then
  jq -n --arg ref "refs/heads/${BRANCH}" --arg sha "$COMMIT_SHA" '{ref:$ref, sha:$sha}' > "$WORK/ref.json"
  api -X POST "repos/${REPO}/git/refs" --input "$WORK/ref.json" > /dev/null
  echo "브랜치 생성: ${BRANCH} -> ${COMMIT_SHA}"
else
  jq -n --arg sha "$COMMIT_SHA" --argjson force "$([ "$FORCE" = "true" ] && echo true || echo false)" \
    '{sha:$sha, force:$force}' > "$WORK/ref.json"
  api -X PATCH "repos/${REPO}/git/refs/heads/${BRANCH}" --input "$WORK/ref.json" > /dev/null
  echo "브랜치 갱신(force=${FORCE}): ${BRANCH} -> ${COMMIT_SHA}"
fi

# 5. 로컬 체크아웃을 새 커밋에 맞춘다 (후속 스텝의 git rev-parse HEAD·gh pr merge 등이 일관되게 동작)
if [ "$SYNC_LOCAL" = "true" ]; then
  if git fetch -q --no-tags origin "+refs/heads/${BRANCH}:refs/remotes/origin/${BRANCH}" \
    && git checkout -q -f -B "$BRANCH" "$COMMIT_SHA"; then
    echo "로컬 체크아웃 동기화: ${BRANCH} @ ${COMMIT_SHA}"
  else
    echo "::warning::로컬 체크아웃 동기화 실패 — 후속 스텝은 commit_sha 출력을 사용하세요"
  fi
fi

set_output committed true
set_output commit_sha "$COMMIT_SHA"
echo "$COMMIT_SHA"
