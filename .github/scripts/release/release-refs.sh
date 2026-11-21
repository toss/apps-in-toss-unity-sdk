#!/usr/bin/env bash
set -euo pipefail
#
# release-refs.sh - 릴리스 태그 ref 를 REST 로 만들고 옮긴다.
#
#   release-refs.sh create-tag <tag> <sha>
#       고정 태그(release/v<wf>+<N>)를 lightweight 로 만든다. 이미 있으면 실패한다 (옮기지 않는다).
#   release-refs.sh move-alias <alias> <sha> [<fixed-tag>]
#       별칭 태그(release/v<wf>)를 <sha> 로 옮긴다. 없으면 만든다.
#       <fixed-tag> 를 주면, 같은 core 에 그보다 높은 리비전의 고정 태그가 이미 있을 때 옮기지 않고 넘어간다
#       (늦게 끝난 실행이 별칭을 옛 리비전으로 되돌리지 않게 한다).
#       별칭에 GitHub Release 가 붙어 있으면 옮기지 않고 실패한다
#       (Immutable Releases 가 켜지면 Release 가 붙은 태그는 옮길 수 없으므로, 옛 Release 를 먼저 지워야 한다).
#   release-refs.sh check-alias <alias>
#       별칭에 GitHub Release 가 붙어 있으면 실패한다 (없거나 Release 가 없으면 통과). 아무것도 쓰지 않는다.
#       공개되는 단계를 시작하기 전에 미리 부른다.
#   release-refs.sh is-latest <fixed-tag>
#       <fixed-tag> 가 안정 릴리스 중 (core, N) 최고인지 true/false 로 출력한다.
#       리비전이 없는 옛 태그(release/v<wf>)는 N=0 으로 센다. prerelease(이름에 `-`)는 Latest 가 될 수 없다.
#
# 환경 변수 (필수): REPO (owner/name). gh 가 GH_TOKEN 을 읽는다.
#
# 종료 코드: 0 성공, 1 거부(이미 있음, 별칭에 Release 가 붙어 있음), 2 사용법·API 오류

: "${REPO:?REPO 가 필요합니다}"

die() {
  echo "::error::$*" >&2
  exit "${2:-1}"
}

# ref 가 있으면 0, 없으면(404) 1, 그 밖의 오류는 종료
ref_exists() {
  local out
  if out=$(gh api "repos/${REPO}/git/ref/tags/$1" --silent 2>&1); then
    return 0
  fi
  if [[ "$out" == *"HTTP 404"* ]]; then
    return 1
  fi
  die "ref 조회에 실패했습니다 (refs/tags/$1): ${out}" 2
}

# 태그에 GitHub Release 가 붙어 있으면 0, 없으면(404) 1, 그 밖의 오류는 종료
release_exists() {
  local out
  if out=$(gh api "repos/${REPO}/releases/tags/$1" --silent 2>&1); then
    return 0
  fi
  if [[ "$out" == *"HTTP 404"* ]]; then
    return 1
  fi
  die "Release 조회에 실패했습니다 ($1): ${out}" 2
}

# release/v 아래 모든 태그 이름에서 `release/v` 접두사를 뗀 목록 (예: 3.10.0, 3.10.0+1)
list_versions() {
  local out
  out=$(gh api "repos/${REPO}/git/matching-refs/tags/release/v" --paginate --jq '.[].ref' 2>&1) \
    || die "태그 목록 조회에 실패했습니다: ${out}" 2
  printf '%s\n' "$out" | sed -n 's#^refs/tags/release/v##p'
}

# 같은 core 의 고정 태그 중 가장 높은 리비전 (없으면 0)
max_revision() {
  local core="$1"
  list_versions | sed -n "s#^${core//./\\.}+\([1-9][0-9]*\)\$#\1#p" | sort -n | tail -1 | { read -r n || true; echo "${n:-0}"; }
}

check_args() {
  local tag="$1" sha="$2"
  [[ "$tag" =~ ^release/v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[1-9][0-9]*)?$ ]] \
    || die "릴리스 태그 이름 형식이 아닙니다: ${tag}" 2
  [[ "$sha" =~ ^[0-9a-f]{40}$ ]] || die "커밋 SHA 형식이 아닙니다: ${sha}" 2
}

ALIAS_RELEASE_MSG() {
  echo "별칭 $1 에 GitHub Release 가 붙어 있어 옮길 수 없습니다. 옛 Release 객체를 먼저 삭제하세요 (태그는 그대로 둡니다). 삭제한 뒤 Release 워크플로를 다시 실행하세요."
}

cmd="${1:-}"
case "$cmd" in
  create-tag)
    TAG="${2:?태그 이름이 필요합니다}"; SHA="${3:?커밋 SHA 가 필요합니다}"
    check_args "$TAG" "$SHA"
    [[ "$TAG" == *"+"* ]] || die "고정 태그는 +<N> 리비전이 있어야 합니다: ${TAG}" 2
    if ref_exists "$TAG"; then
      die "고정 태그가 이미 있습니다: ${TAG}. 고정 태그는 옮기지 않습니다. 새 리비전이 필요하면 sdkVersion 의 +N 을 올리세요."
    fi
    gh api "repos/${REPO}/git/refs" -X POST -f "ref=refs/tags/${TAG}" -f "sha=${SHA}" --silent \
      || die "태그를 만들지 못했습니다: ${TAG}" 2
    echo "태그 생성: ${TAG} -> ${SHA}"
    ;;
  move-alias)
    ALIAS="${2:?별칭 태그 이름이 필요합니다}"; SHA="${3:?커밋 SHA 가 필요합니다}"; FIXED="${4:-}"
    check_args "$ALIAS" "$SHA"
    [[ "$ALIAS" != *"+"* ]] || die "별칭은 +<N> 리비전이 없는 이름이어야 합니다: ${ALIAS}" 2
    if [ -n "$FIXED" ]; then
      [[ "$FIXED" == "${ALIAS}+"* ]] || die "고정 태그(${FIXED})가 별칭(${ALIAS})의 리비전이 아닙니다" 2
      MINE="${FIXED##*+}"
      HIGHEST=$(max_revision "${ALIAS#release/v}")
      if [ "$HIGHEST" -gt "$MINE" ]; then
        echo "::warning::더 높은 리비전(+${HIGHEST})이 이미 있어 별칭 ${ALIAS} 를 옮기지 않습니다 (이 실행: +${MINE})."
        exit 0
      fi
    fi
    if ref_exists "$ALIAS"; then
      if release_exists "$ALIAS"; then
        die "$(ALIAS_RELEASE_MSG "$ALIAS")"
      fi
      gh api "repos/${REPO}/git/refs/tags/${ALIAS}" -X PATCH -f "sha=${SHA}" -F force=true --silent \
        || die "별칭을 옮기지 못했습니다: ${ALIAS}" 2
      echo "별칭 이동: ${ALIAS} -> ${SHA}"
    else
      gh api "repos/${REPO}/git/refs" -X POST -f "ref=refs/tags/${ALIAS}" -f "sha=${SHA}" --silent \
        || die "별칭을 만들지 못했습니다: ${ALIAS}" 2
      echo "별칭 생성: ${ALIAS} -> ${SHA}"
    fi
    ;;
  check-alias)
    ALIAS="${2:?별칭 태그 이름이 필요합니다}"
    [[ "$ALIAS" =~ ^release/v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || die "별칭 이름 형식이 아닙니다: ${ALIAS}" 2
    if ref_exists "$ALIAS" && release_exists "$ALIAS"; then
      die "$(ALIAS_RELEASE_MSG "$ALIAS")"
    fi
    echo "별칭 ${ALIAS}: 옮길 수 있습니다"
    ;;
  is-latest)
    TAG="${2:?고정 태그 이름이 필요합니다}"
    [[ "$TAG" =~ ^release/v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?\+[1-9][0-9]*$ ]] \
      || die "고정 태그 이름 형식이 아닙니다: ${TAG}" 2
    VER="${TAG#release/v}"
    if [[ "$VER" == *-* ]]; then
      echo false
      exit 0
    fi
    MINE="${VER%%+*}.${VER##*+}"
    # 안정 태그만 센다. 리비전 없는 옛 태그는 N=0, 후보 자신도 목록에 넣는다.
    HIGHEST=$({ list_versions; echo "$VER"; } \
      | sed -n -e 's#^\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)$#\1.0#p' \
               -e 's#^\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)+\([1-9][0-9]*\)$#\1.\2#p' \
      | sort -V | tail -1)
    if [ "$HIGHEST" = "$MINE" ]; then echo true; else echo false; fi
    ;;
  *)
    echo "사용법: $0 create-tag <tag> <sha> | check-alias <alias> | move-alias <alias> <sha> [<fixed-tag>] | is-latest <fixed-tag>" >&2
    exit 2
    ;;
esac
