#!/bin/bash
# 생성물 드리프트 검사: `pnpm generate` 결과가 저장소에 커밋된 생성물과 같은지 확인한다.
# 사용: scripts/check-generated-drift.sh [--no-generate]
#   --no-generate  이미 생성한 뒤 검사만 한다.
# exit 0: 드리프트 없음, exit 1: 생성물이 달라졌거나 생성 실패.
set -euo pipefail

GEN_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(git -C "$GEN_DIR" rev-parse --show-toplevel)"

# generate(src/index.ts)가 쓰는 출력 경로 (저장소 루트 기준).
#  - Runtime/SDK/                       C# 코드, .meta, Plugins/*.jslib
#  - WebGLTemplates/.../unity-bridge.ts unity-bridge.ts
GENERATED_PATHS=(
  "Runtime/SDK"
  "WebGLTemplates/AITTemplate/BuildConfig~/unity-bridge.ts"
)

RUN_GENERATE=1
for arg in "$@"; do
  case "$arg" in
    --no-generate) RUN_GENERATE=0 ;;
    -h|--help) sed -n '2,5p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "알 수 없는 옵션: $arg" >&2; exit 2 ;;
  esac
done

if [ "$RUN_GENERATE" = "1" ]; then
  echo "pnpm generate 실행 중..."
  (cd "$GEN_DIR" && pnpm generate >/dev/null) || {
    echo "pnpm generate 실패" >&2
    exit 1
  }
fi

cd "$REPO_ROOT"
STATUS="$(git status --porcelain --untracked-files=all -- "${GENERATED_PATHS[@]}")"

if [ -n "$STATUS" ]; then
  echo "generated output: DRIFT detected" >&2
  echo "$STATUS" >&2
  echo "" >&2
  git diff --stat -- "${GENERATED_PATHS[@]}" >&2
  exit 1
fi

echo "generated output: no drift"
