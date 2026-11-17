#!/usr/bin/env bash
set -euo pipefail
#
# release-refs.test.sh - release-refs.sh 로컬 검증 (실제 GitHub 호출 없음)
#
# 가짜 gh 는 EXISTING_REFS(공백으로 구분한 태그 이름)와 RELEASE_TAGS 로 조회 결과를 흉내 내고,
# 쓰기 호출(POST/PATCH)은 로그에 남긴다. 고정 태그는 옮기지 않고, 별칭은 Release 가 붙어 있지 않을 때만
# 옮기는지 확인한다.
#
# 실행: bash .github/scripts/release/release-refs.test.sh

SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/release-refs.sh"
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT

mkdir -p "$T/bin"
cat > "$T/bin/gh" <<'GHEOF'
#!/usr/bin/env bash
# 사용: gh api <path> [-X METHOD] [-f k=v] [-F k=v] [--silent]
shift # api
path="$1"; shift
method=GET; extra=""
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    -f|-F) extra="$extra $2"; shift 2 ;;
    *) shift ;;
  esac
done
if [ -n "${FAIL_GET:-}" ] && [ "$method" = GET ]; then
  echo "gh: Server Error (HTTP 500)" >&2
  exit 1
fi
if [ "$method" = GET ]; then
  case "$path" in
    */git/matching-refs/tags/release/v)
      for r in ${ALL_TAGS:-}; do echo "refs/tags/release/v$r"; done
      exit 0
      ;;
    */git/ref/tags/*)
      name="${path#*/git/ref/tags/}"
      for r in ${EXISTING_REFS:-}; do [ "$r" = "$name" ] && exit 0; done
      ;;
    */releases/tags/*)
      name="${path#*/releases/tags/}"
      for r in ${RELEASE_TAGS:-}; do [ "$r" = "$name" ] && exit 0; done
      ;;
  esac
  echo "gh: Not Found (HTTP 404)" >&2
  exit 1
fi
echo "$method $path$extra" >> "$CALLS"
exit 0
GHEOF
chmod +x "$T/bin/gh"

export PATH="$T/bin:$PATH"
export CALLS="$T/calls.log"
export REPO="toss/apps-in-toss-unity-sdk"
export GH_TOKEN="x"
SHA=$(printf 'a%.0s' {1..40})

FAILED=0
fail() { echo "FAIL: $*"; FAILED=1; }

# run <기대 종료 코드> <인자...>: 종료 코드가 다르면 실패로 기록하고 출력은 $T/out.log 에 둔다
run() {
  local want="$1"; shift
  local got=0
  : > "$CALLS"
  bash "$SCRIPT" "$@" > "$T/out.log" 2>&1 || got=$?
  [ "$got" -eq "$want" ] || fail "종료 코드 ${got} (기대 ${want}): $*"
}

# --- create-tag: 없으면 POST 한 번 ---
EXISTING_REFS="" run 0 create-tag "release/v3.10.0+1" "$SHA"
grep -q "POST repos/$REPO/git/refs ref=refs/tags/release/v3.10.0+1 sha=$SHA" "$CALLS" || fail "create-tag: POST 가 없다"

# --- create-tag: 이미 있으면 거부하고 쓰지 않는다 ---
EXISTING_REFS="release/v3.10.0+1" run 1 create-tag "release/v3.10.0+1" "$SHA"
[ ! -s "$CALLS" ] || fail "create-tag: 기존 태그가 있는데 쓰기 호출이 있었다"
grep -q "이미 있습니다" "$T/out.log" || fail "create-tag: 안내 메시지가 없다"

# --- create-tag: 리비전 없는 이름, 잘못된 SHA 는 거부 ---
run 2 create-tag "release/v3.10.0" "$SHA"
run 2 create-tag "release/v3.10.0+1" "abc"
[ ! -s "$CALLS" ] || fail "create-tag: 잘못된 입력인데 쓰기 호출이 있었다"

# --- create-tag: 조회가 404 가 아닌 오류면 만들지 않는다 ---
FAIL_GET=1 run 2 create-tag "release/v3.10.0+1" "$SHA"
[ ! -s "$CALLS" ] || fail "create-tag: 조회 오류인데 쓰기 호출이 있었다"

# --- move-alias: 별칭이 없으면 만든다 ---
EXISTING_REFS="" run 0 move-alias "release/v3.10.0" "$SHA"
grep -q "POST repos/$REPO/git/refs ref=refs/tags/release/v3.10.0 sha=$SHA" "$CALLS" || fail "move-alias: 생성 POST 가 없다"

# --- move-alias: 별칭이 있고 Release 가 없으면 force PATCH ---
EXISTING_REFS="release/v3.10.0" RELEASE_TAGS="" run 0 move-alias "release/v3.10.0" "$SHA"
grep -q "PATCH repos/$REPO/git/refs/tags/release/v3.10.0 sha=$SHA force=true" "$CALLS" || fail "move-alias: force PATCH 가 없다"
[ "$(grep -c . "$CALLS")" -eq 1 ] || fail "move-alias: 쓰기 호출이 한 번이 아니다"

# --- move-alias: 별칭에 Release 가 붙어 있으면 거부하고 쓰지 않는다 ---
EXISTING_REFS="release/v3.10.0" RELEASE_TAGS="release/v3.10.0" run 1 move-alias "release/v3.10.0" "$SHA"
[ ! -s "$CALLS" ] || fail "move-alias: Release 가 붙어 있는데 쓰기 호출이 있었다"
grep -q "Release 객체를 먼저 삭제" "$T/out.log" || fail "move-alias: 안내 메시지가 없다"

# --- move-alias: 다른 별칭의 Release 는 영향이 없다 ---
EXISTING_REFS="release/v3.10.0" RELEASE_TAGS="release/v3.9.0" run 0 move-alias "release/v3.10.0" "$SHA"
grep -q "PATCH" "$CALLS" || fail "move-alias: 다른 태그의 Release 때문에 막혔다"

# --- move-alias: +N 이름은 거부, 조회 오류면 옮기지 않는다 ---
run 2 move-alias "release/v3.10.0+1" "$SHA"
FAIL_GET=1 run 2 move-alias "release/v3.10.0" "$SHA"
[ ! -s "$CALLS" ] || fail "move-alias: 거부·오류인데 쓰기 호출이 있었다"

# --- move-alias: 더 높은 리비전이 이미 있으면 옮기지 않는다 (늦게 끝난 실행 보호) ---
ALL_TAGS="3.10.0 3.10.0+1 3.10.0+2" EXISTING_REFS="release/v3.10.0" run 0 move-alias "release/v3.10.0" "$SHA" "release/v3.10.0+1"
[ ! -s "$CALLS" ] || fail "move-alias: 더 높은 리비전이 있는데 옮겼다"
grep -q "옮기지 않습니다" "$T/out.log" || fail "move-alias: 경고가 없다"
ALL_TAGS="3.10.0 3.10.0+1 3.10.0+2" EXISTING_REFS="release/v3.10.0" run 0 move-alias "release/v3.10.0" "$SHA" "release/v3.10.0+2"
grep -q "PATCH" "$CALLS" || fail "move-alias: 최고 리비전인데 옮기지 않았다"
ALL_TAGS="3.10.0+10 3.10.0+9" EXISTING_REFS="release/v3.10.0" run 0 move-alias "release/v3.10.0" "$SHA" "release/v3.10.0+9"
[ ! -s "$CALLS" ] || fail "move-alias: +10 이 +9 보다 높아야 한다 (숫자 비교)"
ALL_TAGS="3.10.0 3.10.0+1 3.1.0+5" EXISTING_REFS="release/v3.10.0" run 0 move-alias "release/v3.10.0" "$SHA" "release/v3.10.0+1"
grep -q "PATCH" "$CALLS" || fail "move-alias: 다른 core 의 리비전 때문에 막혔다"
run 2 move-alias "release/v3.10.0" "$SHA" "release/v3.9.0+1"

# --- check-alias ---
EXISTING_REFS="" run 0 check-alias "release/v3.10.0"
EXISTING_REFS="release/v3.10.0" RELEASE_TAGS="" run 0 check-alias "release/v3.10.0"
EXISTING_REFS="release/v3.10.0" RELEASE_TAGS="release/v3.10.0" run 1 check-alias "release/v3.10.0"
grep -q "Release 객체를 먼저 삭제" "$T/out.log" || fail "check-alias: 안내 메시지가 없다"
run 2 check-alias "release/v3.10.0+1"
[ ! -s "$CALLS" ] || fail "check-alias: 쓰기 호출이 있었다"

# --- is-latest ---
latest() { ALL_TAGS="$1" bash "$SCRIPT" is-latest "$2"; }
[ "$(latest "3.10.0 3.10.0+1" "release/v3.10.0+1")" = true ] || fail "is-latest: 옛 태그(N=0)보다 +1 이 높아야 한다"
[ "$(latest "3.10.0+1 3.10.0+2" "release/v3.10.0+1")" = false ] || fail "is-latest: +1 은 +2 보다 낮다"
[ "$(latest "3.10.0+1 3.11.0+1" "release/v3.10.0+2")" = false ] || fail "is-latest: 높은 core 가 있으면 false"
[ "$(latest "3.9.0 3.10.0 3.10.0+1" "release/v3.10.0+1")" = true ] || fail "is-latest: 가장 높은 (core, N) 이면 true"
[ "$(latest "3.9.0+12 3.10.0" "release/v3.10.0+1")" = true ] || fail "is-latest: core 가 먼저, N 은 그다음이다"
[ "$(latest "3.10.0+9" "release/v3.10.0+10")" = true ] || fail "is-latest: N 은 숫자 비교"
[ "$(latest "3.10.0 3.11.0-beta.1" "release/v3.10.0+1")" = true ] || fail "is-latest: prerelease 태그는 세지 않는다"
[ "$(latest "3.10.0" "release/v3.11.0-beta.1+1")" = false ] || fail "is-latest: prerelease 후보는 Latest 가 아니다"
[ "$(latest "" "release/v3.10.0+1")" = true ] || fail "is-latest: 태그가 없으면(후보 자신만) true"
run 2 is-latest "release/v3.10.0"

# --- 알 수 없는 명령 ---
run 2 bogus

if [ "$FAILED" -ne 0 ]; then
  echo "release-refs.test.sh 실패"
  exit 1
fi
echo "release-refs.test.sh 통과"
