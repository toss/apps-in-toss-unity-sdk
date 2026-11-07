#!/bin/bash
# scripts~/hash-ait-build.sh — Unity 프로젝트의 ait-build/ 트리 해시 매니페스트
#
# 사용:
#   hash-ait-build.sh <unity-project-dir>                    매니페스트를 stdout에 출력
#   hash-ait-build.sh <unity-project-dir> --compare <file>   저장해 둔 매니페스트와 비교
#
# 매니페스트 한 줄: "<sha256>  <ait-build 기준 상대경로>" (상대경로 정렬).
# node_modules, dist, .npm-cache, public, *.ait 는 제외한다.
# --compare: 추가/삭제/변경을 출력하고 차이가 있으면 exit 1.
set -euo pipefail

usage() {
  sed -n '2,10p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

PROJECT_DIR=""
COMPARE_FILE=""
while [ $# -gt 0 ]; do
  case "$1" in
    --compare)
      [ $# -ge 2 ] || usage
      COMPARE_FILE="$2"; shift 2 ;;
    -h|--help) usage ;;
    -*) echo "알 수 없는 옵션: $1" >&2; usage ;;
    *)
      [ -z "$PROJECT_DIR" ] || usage
      PROJECT_DIR="$1"; shift ;;
  esac
done
[ -n "$PROJECT_DIR" ] || usage

BUILD_DIR="$PROJECT_DIR/ait-build"
if [ ! -d "$BUILD_DIR" ]; then
  echo "ait-build 디렉토리가 없습니다: $BUILD_DIR" >&2
  exit 2
fi

if command -v sha256sum >/dev/null 2>&1; then
  hash_cmd() { sha256sum "$1" | cut -d' ' -f1; }
elif command -v shasum >/dev/null 2>&1; then
  hash_cmd() { shasum -a 256 "$1" | cut -d' ' -f1; }
else
  echo "sha256sum 또는 shasum 이 필요합니다" >&2
  exit 2
fi

build_manifest() {
  local rel
  (
    cd "$BUILD_DIR"
    find . \
      \( -name node_modules -o -name dist -o -name .npm-cache -o -name public \) -prune -o \
      -type f ! -name '*.ait' -print
  ) | sed 's|^\./||' | LC_ALL=C sort | while IFS= read -r rel; do
    printf '%s  %s\n' "$(hash_cmd "$BUILD_DIR/$rel")" "$rel"
  done
}

if [ -z "$COMPARE_FILE" ]; then
  build_manifest
  exit 0
fi

if [ ! -f "$COMPARE_FILE" ]; then
  echo "매니페스트 파일이 없습니다: $COMPARE_FILE" >&2
  exit 2
fi

CUR="$(mktemp)"
trap 'rm -f "$CUR"' EXIT
build_manifest > "$CUR"

# 경로 -> 해시 비교 (첫 64자가 해시, 67번째 글자부터 경로: 공백 든 경로도 안전)
DIFF="$(awk '
  function split_line(line) { h = substr(line, 1, 64); p = substr(line, 67) }
  NR == FNR { split_line($0); old[p] = h; next }
  { split_line($0); cur[p] = h }
  END {
    for (p in cur) if (!(p in old)) print "추가: " p
    for (p in old) if (!(p in cur)) print "삭제: " p
    for (p in cur) if ((p in old) && old[p] != cur[p]) print "변경: " p
  }
' "$COMPARE_FILE" "$CUR" | LC_ALL=C sort)"

if [ -z "$DIFF" ]; then
  echo "ait-build tree: no difference"
  exit 0
fi
echo "$DIFF"
echo "총 $(echo "$DIFF" | wc -l | tr -d ' ')건 차이"
exit 1
