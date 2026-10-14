#!/usr/bin/env bash
set -euo pipefail
#
# close-superseded-prs.sh - 새 SDK 업데이트 PR 보다 낮은 버전의 열린 update/ PR 을 닫는다.
#
# update.yml 은 누락 버전이 여러 개여도 가장 높은 버전 하나만 PR 로 만든다. 예전 방식으로 만들어
# 둔 낮은 버전 PR(또는 그 사이 새로 나온 상위 버전에 밀린 PR)은 서로 충돌만 일으키므로,
# 새 PR 을 만든 뒤 코멘트를 남기고 닫고 브랜치를 지운다.
#
# 대상: head 브랜치가 update/<X.Y.Z>-* 이고, 작성자가 github-actions[bot] 이며,
#       head 저장소가 이 저장소인 열린 PR 중 버전이 NEW_VERSION 보다 낮은 것.
# 건드리지 않는 것: 사람이 만든 update/ 브랜치 PR, fork PR, NEW_PR_NUMBER 자신, 같거나 높은 버전.
#
# 환경 변수 (필수): GH_TOKEN, REPO (owner/name), NEW_VERSION, NEW_PR_NUMBER
# 선택: BOT_LOGIN (기본 github-actions[bot])
#
# 개별 PR 의 코멘트/닫기/브랜치 삭제가 실패해도 경고만 남기고 나머지를 계속 처리한다.

: "${REPO:?REPO 가 필요합니다}"
: "${NEW_VERSION:?NEW_VERSION 이 필요합니다}"
: "${NEW_PR_NUMBER:?NEW_PR_NUMBER 가 필요합니다}"
BOT_LOGIN="${BOT_LOGIN:-github-actions[bot]}"

if ! [[ "$NEW_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "::warning::NEW_VERSION 이 X.Y.Z 형식이 아니어서 정리를 건너뜁니다: ${NEW_VERSION}"
  exit 0
fi

# 첫 인자가 둘째 인자보다 엄격히 낮은 버전이면 0
is_lower() {
  local a="$1" b="$2"
  [ "$a" != "$b" ] && [ "$(printf '%s\n%s\n' "$a" "$b" | sort -V | head -n1)" = "$a" ]
}

# 목록 조회가 일시 실패(5xx, rate limit)해도 새 PR 은 이미 만들어진 뒤라 잡을 실패시키지 않는다.
# 낮은 버전 PR 은 다음 실행에서 다시 정리된다.
ROWS=$(gh api "repos/${REPO}/pulls?state=open&per_page=100" --paginate \
  --jq '.[] | select(.head.ref | startswith("update/")) | [.number, .head.ref, .user.login, (.head.repo.full_name // "")] | @tsv') || {
  echo "::warning::열린 PR 목록 조회에 실패해 정리를 건너뜁니다"
  exit 0
}

CLOSED=0
while IFS=$'\t' read -r NUMBER BRANCH AUTHOR HEAD_REPO; do
  [ -z "${NUMBER:-}" ] && continue

  if [ "$NUMBER" = "$NEW_PR_NUMBER" ]; then
    continue
  fi
  if [ "$AUTHOR" != "$BOT_LOGIN" ]; then
    echo "  PR #${NUMBER} (${BRANCH}): 작성자 ${AUTHOR} 는 bot 이 아니어서 건드리지 않습니다"
    continue
  fi
  if [ "$HEAD_REPO" != "$REPO" ]; then
    echo "  PR #${NUMBER} (${BRANCH}): head 저장소 '${HEAD_REPO}' 가 ${REPO} 가 아니어서 건드리지 않습니다"
    continue
  fi

  VERSION=$(printf '%s' "$BRANCH" | sed -n 's|^update/\([0-9]\{1,\}\.[0-9]\{1,\}\.[0-9]\{1,\}\)-.*|\1|p')
  if [ -z "$VERSION" ]; then
    echo "  PR #${NUMBER} (${BRANCH}): 브랜치에서 버전을 읽지 못해 건드리지 않습니다"
    continue
  fi
  if ! is_lower "$VERSION" "$NEW_VERSION"; then
    echo "  PR #${NUMBER} (v${VERSION}): v${NEW_VERSION} 보다 낮지 않아 유지합니다"
    continue
  fi

  echo "  PR #${NUMBER} (v${VERSION}): v${NEW_VERSION} 업데이트 PR #${NEW_PR_NUMBER} 으로 대체 → 닫습니다"
  gh api "repos/${REPO}/issues/${NUMBER}/comments" -X POST \
    -f body="v${NEW_VERSION} 업데이트 PR #${NEW_PR_NUMBER} 으로 대체되어 닫습니다." > /dev/null \
    || echo "::warning::PR #${NUMBER} 코멘트 실패"
  if gh api "repos/${REPO}/pulls/${NUMBER}" -X PATCH -f state=closed > /dev/null; then
    CLOSED=$((CLOSED + 1))
  else
    echo "::warning::PR #${NUMBER} 닫기 실패 — 브랜치는 지우지 않습니다"
    continue
  fi
  gh api "repos/${REPO}/git/refs/heads/${BRANCH}" -X DELETE > /dev/null \
    || echo "::warning::브랜치 삭제 실패: ${BRANCH}"
done <<< "$ROWS"

echo "대체된 PR ${CLOSED}건을 닫았습니다."
