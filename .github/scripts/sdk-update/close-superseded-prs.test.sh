#!/usr/bin/env bash
set -euo pipefail
#
# close-superseded-prs.test.sh - close-superseded-prs.sh 로컬 검증 (실제 GitHub 호출 없음)
#
# 가짜 gh 가 JSON 픽스처를 jq 로 걸러 돌려주고, 쓰기 호출(코멘트/닫기/브랜치 삭제)은 로그에 남긴다.
# 낮은 bot PR 만 닫히고, 사람 PR·fork PR·같거나 높은 버전·새 PR 자신은 그대로인지 확인한다.
#
# 실행: bash .github/scripts/sdk-update/close-superseded-prs.test.sh

SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/close-superseded-prs.sh"
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT

mkdir -p "$T/bin"
cat > "$T/bin/gh" <<'GHEOF'
#!/usr/bin/env bash
# 사용: gh api <path> [-X METHOD] [-f k=v] [--paginate] [--jq FILTER]
shift # api
path="$1"; shift
method=GET; jq_filter=""; extra=""
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    --jq) jq_filter="$2"; shift 2 ;;
    --paginate) shift ;;
    -f) extra="$extra $2"; shift 2 ;;
    *) shift ;;
  esac
done
if [ "$method" = GET ]; then
  jq -r "$jq_filter" "$FIXTURE"
  exit 0
fi
echo "$method $path$extra" >> "$CALLS"
if [ -n "${FAIL_PATCH:-}" ] && [ "$method" = PATCH ] && [[ "$path" == *"/pulls/${FAIL_PATCH}" ]]; then
  exit 1
fi
exit 0
GHEOF
chmod +x "$T/bin/gh"

export PATH="$T/bin:$PATH"
export FIXTURE="$T/prs.json"
export CALLS="$T/calls.log"
export REPO="toss/apps-in-toss-unity-sdk"
export GH_TOKEN="x"

cat > "$FIXTURE" <<'JSONEOF'
[
  {"number": 101, "head": {"ref": "update/3.6.0-20261001-000000", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 102, "head": {"ref": "update/3.7.0-20261002-000000", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 103, "head": {"ref": "update/3.8.0-20261003-000000", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 104, "head": {"ref": "update/3.9.0-20261004-000000", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 105, "head": {"ref": "update/3.5.1-hand-made", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "someone"}},
  {"number": 106, "head": {"ref": "update/3.4.0-20260901-000000", "repo": {"full_name": "someone/fork"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 107, "head": {"ref": "feat/unrelated", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 108, "head": {"ref": "update/not-a-version", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 109, "head": {"ref": "update/3.10.0-20261005-000000", "repo": {"full_name": "toss/apps-in-toss-unity-sdk"}}, "user": {"login": "github-actions[bot]"}},
  {"number": 110, "head": {"ref": "update/3.2.0-20260801-000000", "repo": null}, "user": {"login": "github-actions[bot]"}}
]
JSONEOF

FAILED=0
fail() { echo "FAIL: $*"; FAILED=1; }

# --- 케이스 1: v3.8.0 PR #103 이 새로 생김 ---
: > "$CALLS"
NEW_VERSION="3.8.0" NEW_PR_NUMBER="103" bash "$SCRIPT" > "$T/out1.log" 2>&1 || fail "스크립트가 실패했다: $(cat "$T/out1.log")"

for n in 101 102; do
  grep -q "POST repos/$REPO/issues/$n/comments body=v3.8.0 업데이트 PR #103 으로 대체되어 닫습니다." "$CALLS" || fail "PR #$n 코멘트가 없다"
  grep -q "PATCH repos/$REPO/pulls/$n state=closed" "$CALLS" || fail "PR #$n 이 닫히지 않았다"
done
grep -q "DELETE repos/$REPO/git/refs/heads/update/3.6.0-20261001-000000" "$CALLS" || fail "3.6.0 브랜치가 삭제되지 않았다"
grep -q "DELETE repos/$REPO/git/refs/heads/update/3.7.0-20261002-000000" "$CALLS" || fail "3.7.0 브랜치가 삭제되지 않았다"

# 코멘트 -> 닫기 -> 삭제 순서
awk '/issues\/101\/comments/{c=NR} /pulls\/101 /{p=NR} /3.6.0-20261001/{d=NR} END{exit !(c && p && d && c<p && p<d)}' "$CALLS" || fail "101 처리 순서가 코멘트 → 닫기 → 삭제가 아니다"

# 건드리면 안 되는 PR: 새 PR 자신(103), 높은 버전(104, 109 = 3.10.0 이 3.8.0 보다 높음), 사람(105), fork(106), 무관(107, 108), repo 없음(110)
for n in 103 104 105 106 107 108 109 110; do
  if grep -Eq "/${n}( |/|$)" "$CALLS"; then fail "PR #$n 을 건드렸다"; fi
done
[ "$(grep -c . "$CALLS")" -eq 6 ] || fail "쓰기 호출 수가 6 이 아니다: $(grep -c . "$CALLS")"
grep -q "사람이 아닌\|작성자 someone" "$T/out1.log" || fail "사람 PR 을 건드리지 않은 이유가 로그에 없다"

# --- 케이스 2: 가장 낮은 버전이 새 PR 이면 아무것도 닫지 않는다 ---
: > "$CALLS"
NEW_VERSION="3.6.0" NEW_PR_NUMBER="101" bash "$SCRIPT" > "$T/out2.log" 2>&1 || fail "케이스 2 실패"
[ ! -s "$CALLS" ] || fail "케이스 2: 쓰기 호출이 있었다: $(cat "$CALLS")"

# --- 케이스 3: 3.10.0 은 3.9.0 보다 높다 (사전식이 아니라 버전 비교) ---
: > "$CALLS"
NEW_VERSION="3.10.0" NEW_PR_NUMBER="109" bash "$SCRIPT" > "$T/out3.log" 2>&1 || fail "케이스 3 실패"
for n in 101 102 103 104; do
  grep -q "PATCH repos/$REPO/pulls/$n state=closed" "$CALLS" || fail "케이스 3: PR #$n 이 닫히지 않았다"
done
[ "$(grep -c "PATCH" "$CALLS")" -eq 4 ] || fail "케이스 3: 닫힌 PR 수가 4 가 아니다"

# --- 케이스 4: 닫기 실패하면 그 브랜치는 지우지 않고 나머지는 계속 처리 ---
: > "$CALLS"
FAIL_PATCH=101 NEW_VERSION="3.8.0" NEW_PR_NUMBER="103" bash "$SCRIPT" > "$T/out4.log" 2>&1 || fail "케이스 4: 스크립트가 중단됐다"
grep -q "DELETE .*3.6.0-20261001" "$CALLS" && fail "케이스 4: 닫기에 실패한 PR 의 브랜치를 지웠다"
grep -q "DELETE .*3.7.0-20261002" "$CALLS" || fail "케이스 4: 나머지 PR 처리가 이어지지 않았다"

# --- 케이스 5: 잘못된 NEW_VERSION 은 아무것도 하지 않는다 ---
: > "$CALLS"
NEW_VERSION="latest" NEW_PR_NUMBER="103" bash "$SCRIPT" > "$T/out5.log" 2>&1 || fail "케이스 5 실패"
[ ! -s "$CALLS" ] || fail "케이스 5: 쓰기 호출이 있었다"

if [ "$FAILED" -ne 0 ]; then
  echo "close-superseded-prs.test.sh 실패"
  exit 1
fi
echo "close-superseded-prs.test.sh 통과"
