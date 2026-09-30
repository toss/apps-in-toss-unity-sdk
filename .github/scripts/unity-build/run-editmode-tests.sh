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

  # <test-run>의 집계 속성에서 카운트를 읽는다. `grep -o 'result="Passed"'`는
  # <test-suite>/<test-run> 롤업 요소까지 함께 세어 부풀려진 값을 낸다
  # (예: 실제 통과 1450건인데 1601건으로 표시되는 문제).
  extract_attr() {
    printf '%s' "$1" | grep -oE "${2}=\"[0-9]+\"" | grep -oE '[0-9]+' | head -1
  }

  TESTRUN_LINE=$(grep -m1 '<test-run ' "$RESULTS_FILE" 2>/dev/null || true)

  TOTAL=""
  PASSED=""
  FAILED=""
  INCONCLUSIVE=""
  SKIPPED=""
  if [ -n "$TESTRUN_LINE" ]; then
    TOTAL=$(extract_attr "$TESTRUN_LINE" "total")
    PASSED=$(extract_attr "$TESTRUN_LINE" "passed")
    FAILED=$(extract_attr "$TESTRUN_LINE" "failed")
    INCONCLUSIVE=$(extract_attr "$TESTRUN_LINE" "inconclusive")
    SKIPPED=$(extract_attr "$TESTRUN_LINE" "skipped")
  fi

  # <test-run> 파싱에 실패하면(포맷 변경 등) <test-case> 요소를 직접 세어 대체한다.
  if [ -z "$PASSED" ]; then
    echo "::warning::<test-run> 속성을 읽지 못해 <test-case> 개별 카운트로 대체합니다"
    PASSED=$(grep -o '<test-case[^>]*result="Passed"' "$RESULTS_FILE" | wc -l | tr -d ' ')
    FAILED=$(grep -o '<test-case[^>]*result="Failed"' "$RESULTS_FILE" | wc -l | tr -d ' ')
    TOTAL=""
    INCONCLUSIVE=""
    SKIPPED=""
  fi

  HAS_FAILED_CASE=false
  if grep -q '<test-case[^>]*result="Failed"' "$RESULTS_FILE"; then
    HAS_FAILED_CASE=true
  fi

  if [ "$HAS_FAILED_CASE" = true ] || { [ -n "$FAILED" ] && [ "$FAILED" -gt 0 ]; }; then
    echo "::error::EditMode tests failed"
    grep -o '<test-case[^>]*result="Failed"[^>]*' "$RESULTS_FILE" | head -20
    exit 1
  else
    echo "✓ EditMode tests passed (passed=${PASSED:-0}, failed=${FAILED:-0}, inconclusive=${INCONCLUSIVE:-0}, skipped=${SKIPPED:-0}, total=${TOTAL:-N/A})"
  fi
else
  echo "::error::EditMode test results file not found at $RESULTS_FILE — Unity가 테스트를 실행하지 못함 (라이선스/Hub/캐시 의심)"
  exit 1
fi
