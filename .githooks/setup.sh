#!/bin/bash

# Git hooks 설정 스크립트
# 저장소를 클론한 후 한 번 실행하면 됩니다.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Git hooks 설정 중..."
git -C "$REPO_ROOT" config core.hooksPath .githooks

echo "✓ Git hooks가 .githooks 디렉토리로 설정되었습니다."
echo ""
echo "활성화된 hooks:"
ls -1 "$SCRIPT_DIR" 2>/dev/null | grep -vE '^(setup\.sh|patterns\.local.*)$' | sed 's/^/  - /' || echo "  (없음)"

# pre-push 훅(내부 호스트명 유출 차단)용 로컬 denylist 안내
echo ""
if [ -f "$SCRIPT_DIR/patterns.local" ]; then
  echo "✓ pre-push denylist: .githooks/patterns.local 감지됨 (내부 호스트 검사 활성)."
else
  echo "ℹ pre-push 훅은 내부 호스트명 유출을 차단합니다. 완전한 보호를 위해 로컬 denylist를 만드세요:"
  echo "    cp .githooks/patterns.local.example .githooks/patterns.local"
  echo "  그런 다음 patterns.local 에 조직 내부 호스트 규칙(CI STRING_CHECK_PATTERN과 동일)을 채우세요."
  echo "  (patterns.local 은 .gitignore되어 커밋되지 않습니다.)"
fi
