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
ls -la "$SCRIPT_DIR"/*.* 2>/dev/null | grep -v setup.sh || echo "  (없음)"
