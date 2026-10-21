#!/bin/bash

# Git hooks 설정 스크립트
# 저장소를 클론한 후(그리고 새 worktree 를 만든 후) 한 번 실행하면 됩니다.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Git hooks 설정 중..."
# core.hooksPath 를 '상대경로' .githooks 로 지정한다. 상대경로는 각 worktree 의 top-level 기준으로
# 해석되므로, worktree 마다 자기 자신의 .githooks 를 사용한다(절대경로로 넣으면 특정 worktree 에
# 고정되어 다른 worktree 에서 잘못된 훅이 돌 수 있음).
git -C "$REPO_ROOT" config core.hooksPath .githooks

# (c) 검증 가능한 설치 마커 — 실제로 어떤 경로/worktree 에 적용됐는지 눈으로 확인.
RESOLVED="$(git -C "$REPO_ROOT" config --get core.hooksPath 2>/dev/null || echo '(미설정)')"
TOP="$(git -C "$REPO_ROOT" rev-parse --show-toplevel 2>/dev/null || echo '?')"
echo "✓ Git hooks 설정 완료: core.hooksPath=${RESOLVED}  (worktree top-level=${TOP})"
echo "  확인: git config --get core.hooksPath  →  .githooks (이 worktree 의 .githooks 해석)"
echo ""
echo "활성화된 hooks:"
ls -1 "$SCRIPT_DIR" 2>/dev/null | grep -vE '^(setup\.sh|patterns\.local.*)$' | sed 's/^/  - /' || echo "  (없음)"

# (b) 원격 추적 ref 최신화 — pre-push 의 range 스캔(--not --remotes)이 오래된 remote ref 때문에
#     불필요하게 커지거나 부정확해지는 것을 예방. 오프라인/권한없음이어도 설치는 실패시키지 않는다.
echo ""
echo "원격 ref 최신화(fetch --prune) 시도 중... (실패해도 설치에는 영향 없음)"
git -C "$REPO_ROOT" fetch --quiet --prune 2>/dev/null && echo "  ✓ fetch 완료" || echo "  ℹ fetch 건너뜀(오프라인/권한). 나중에 'git fetch --prune' 권장."

# pre-push 훅용 로컬 denylist(patterns.local) 안내
echo ""
if [ -f "$SCRIPT_DIR/patterns.local" ]; then
  echo "✓ pre-push denylist: .githooks/patterns.local 감지됨 (내부 호스트 + 사설 식별자 검사 활성)."
else
  echo "⚠ pre-push denylist(.githooks/patterns.local)가 없습니다 — 지금은 내장 '일반' 패턴만 검사합니다."
  echo "  (내부 도메인 / 사설 프로젝트 코드네임 / 클론 URL / 시크릿명은 아직 검사되지 않습니다.)"
  echo "  완전한 보호를 위해:"
  echo "    1) cp .githooks/patterns.local.example .githooks/patterns.local"
  echo "    2) patterns.local 을 실제 규칙으로 채우세요. 실제 값은 저장소에 없으므로,"
  echo "       팀 보안 시크릿 저장소(사내 secrets manager / 자격증명 볼트 등)에 보관된"
  echo "       'STRING_CHECK_PATTERN 규칙 세트'를 담당자에게 받아 그대로 반영하세요."
  echo "       (patterns.local 은 .gitignore 되어 커밋되지 않습니다. 실제 값을 .example 에는 절대 넣지 마세요.)"
fi

# (d)(e) STRICT 모드 & 영속화 안내
echo ""
echo "권장: 조직 denylist 미설정 시 '경고'가 아닌 'push 거부(fail-closed)'로 강제하려면 PREPUSH_STRICT=1."
if [ "${PREPUSH_STRICT:-0}" = "1" ]; then
  echo "  현재 셸: PREPUSH_STRICT=1 (활성)."
else
  echo "  현재 셸: PREPUSH_STRICT 미설정."
fi
echo "  ⚠ 한 번의 'export PREPUSH_STRICT=1' 은 새 셸에서 사라집니다. 셸 프로파일에 '영속화'하세요:"
echo "      echo 'export PREPUSH_STRICT=1' >> ~/.zshrc   # 또는 ~/.bashrc / direnv"
echo ""
echo "설치 검증: 합성 canary(실제 코드네임 아님)를 patterns.local 에 넣고 그 문자열을 담은 브랜치명/"
echo "커밋 메시지/파일 경로로 test push 를 시도하면 pre-push 가 차단해야 합니다(내용은 비표시)."
