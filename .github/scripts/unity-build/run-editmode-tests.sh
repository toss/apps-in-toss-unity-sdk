#!/usr/bin/env bash
set -e

PROJECT_PATH="$GITHUB_WORKSPACE/Tests~/E2E/${PROJECT_DIR_PREFIX}-${UNITY_VERSION_PATTERN}"
RESULTS_FILE="$PROJECT_PATH/editmode-results.xml"
LOG_FILE="/tmp/unity-editmode-${UNITY_VERSION_PATTERN}.log"
MAX_WAIT=600  # Unity 프로세스 최대 대기 시간 (10분)

echo "Running EditMode Tests..."
echo "Unity Version: $UNITY_VERSION_FULL"
echo "Log file: $LOG_FILE"

# 프로세스 치환으로 stdout/stderr를 tee에 흘려 파일을 보존한다 — 잡 종료 후
# 통합 분류기가 라이선스/Hub/캐시 등 인프라 결함을 grep으로 세분화할 수 있도록 한다.
# 파이프(`| tee`)를 쓰면 `$!`이 tee의 PID가 되어 아래 wait 루프가 깨지므로
# `> >(tee ...)` 형태를 사용해 `$!`이 Unity PID를 가리키도록 한다.
"$UNITY_PATH" -batchmode -nographics \
  -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform EditMode \
  -testResults "$RESULTS_FILE" \
  -logFile - > >(tee "$LOG_FILE") 2>&1 &
UNITY_PID=$!

# 타임아웃 대기: 결과 파일이 생성되면 프로세스 종료를 기다리되, 최대 MAX_WAIT초
WAIT_SECONDS=0
while kill -0 $UNITY_PID 2>/dev/null; do
  sleep 3
  WAIT_SECONDS=$((WAIT_SECONDS + 3))

  # 결과 파일이 생성된 후 60초 내에 프로세스가 종료되지 않으면 강제 종료
  if [ -f "$RESULTS_FILE" ] && [ $WAIT_SECONDS -gt 60 ]; then
    RESULT_AGE=$(( $(date +%s) - $(stat -f %m "$RESULTS_FILE") ))
    if [ $RESULT_AGE -gt 60 ]; then
      echo "::warning::Unity process did not exit within 60s after test results were written. Force killing (pid: $UNITY_PID)..."
      kill -9 $UNITY_PID 2>/dev/null || true
      break
    fi
  fi

  if [ $WAIT_SECONDS -ge $MAX_WAIT ]; then
    echo "::warning::Unity process timed out after ${MAX_WAIT}s. Force killing (pid: $UNITY_PID)..."
    kill -9 $UNITY_PID 2>/dev/null || true
    break
  fi
done
wait $UNITY_PID 2>/dev/null || true

# Unity Test Runner는 테스트 실패 시 exit code 2를 반환
# editmode-results.xml을 파싱하여 실제 실패 여부 확인.
# 결과 파일이 없으면 라이선스/Hub/캐시 등 인프라 문제로 Unity가 테스트를 실행하지 못한 것이므로
# 회귀가 조용히 묻히지 않도록 fail 처리한다 (분류기가 results-missing으로 잡아낼 수 있도록).
if [ -f "$RESULTS_FILE" ]; then
  echo "EditMode test results:"
  if grep -q 'result="Failed"' "$RESULTS_FILE"; then
    echo "::error::EditMode tests failed"
    grep 'result="Failed"' "$RESULTS_FILE" | head -20
    exit 1
  else
    PASSED=$(grep -o 'result="Passed"' "$RESULTS_FILE" | wc -l | tr -d ' ')
    echo "✓ EditMode tests passed (${PASSED} tests)"
  fi
else
  echo "::error::EditMode test results file not found at $RESULTS_FILE — Unity가 테스트를 실행하지 못함 (라이선스/Hub/캐시 의심)"
  exit 1
fi
