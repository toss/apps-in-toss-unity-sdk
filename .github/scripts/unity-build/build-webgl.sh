#!/usr/bin/env bash

# attempt 카운터 초기화 (매번 — self-hosted 러너 잔존 파일 방지)
ATTEMPT_FILE="/tmp/retry-attempt-${UNITY_VERSION_PATTERN}"
echo "0" > "$ATTEMPT_FILE"

PROJECT_PATH="$GITHUB_WORKSPACE/Tests~/E2E/${PROJECT_DIR_PREFIX}-${UNITY_VERSION_PATTERN}"
LOG_FILE="/tmp/unity-build-${UNITY_VERSION_PATTERN}.log"

# Unity 프로세스 트리 강제 종료 (SIGKILL)
# websockify가 retry 액션의 pipe fd를 상속받으면 EOF가 안 되어 hang
cleanup_unity() {
  [ -n "$UNITY_PID" ] && kill -9 $UNITY_PID 2>/dev/null || true
  # Unity 프로세스 그룹의 자식만 정리 (다른 프로세스 영향 방지)
  [ -n "$UNITY_PID" ] && pkill -9 -P $UNITY_PID 2>/dev/null || true
  pkill -9 -P $$ 2>/dev/null || true
}
trap cleanup_unity EXIT

echo "Unity Version: $UNITY_VERSION_FULL"
echo "Unity Path: ${UNITY_PATH}"
echo "Project Path: ${PROJECT_PATH}"

# perf minimal posture 는 Sentry 를 설치하지 않은 빈 프로젝트를 잰다. Sentry 는 AlwaysLinkAssembly 라
# 옵션 에셋을 지워도 패키지가 설치돼 있으면 어셈블리가 빌드에 들어가므로(wasm ~0.8MB) 패키지 자체를 뺀다.
# 테스트 스크립트의 Sentry 코드는 AIT_SENTRY_AVAILABLE versionDefine 으로 막혀 있다.
if [[ "${AIT_PERF_POSTURE:-}" == minimal* ]]; then
  python3 - "$PROJECT_PATH/Packages/manifest.json" <<'PY'
import json, sys
path = sys.argv[1]
with open(path) as f:
    manifest = json.load(f)
manifest.get("dependencies", {}).pop("com.unity.modules.physics", None)
if True:
    with open(path, "w") as f:
        json.dump(manifest, f, indent=2)
    print("[perf] minimal posture: io.sentry.unity 를 매니페스트에서 제거")
PY
fi

# self-hosted 러너 잔존 로그 파일 정리
rm -f "$LOG_FILE"

# Unity 실행 (로그는 -logFile로 파일에 기록)
# 주의: >/dev/null 사용 불가 — Unity의 -logFile이 stdout fd에 의존함
"$UNITY_PATH" -quit -batchmode -nographics \
  -projectPath "$PROJECT_PATH" \
  -executeMethod "$BUILD_METHOD" \
  -buildTarget WebGL \
  -logFile "$LOG_FILE" &
UNITY_PID=$!
echo "Unity started (PID: $UNITY_PID, log: $LOG_FILE)"

# 로그 파일 생성 대기
for i in $(seq 1 30); do
  [ -f "$LOG_FILE" ] && break
  sleep 1
done

# 로그 시그널 기반 대기 + 증분 로그 출력
# 증분 출력 헬퍼 (tail -f 대신 — 백그라운드 프로세스 0개 유지)
flush_log() {
  [ -f "$LOG_FILE" ] || return
  CURRENT_SIZE=$(stat -f%z "$LOG_FILE" 2>/dev/null || echo "0")
  CURRENT_SIZE=${CURRENT_SIZE:-0}
  if [ "$CURRENT_SIZE" -gt "$LOG_OFFSET" ]; then
    tail -c +$((LOG_OFFSET + 1)) "$LOG_FILE" 2>/dev/null | head -c $((CURRENT_SIZE - LOG_OFFSET))
    LOG_OFFSET=$CURRENT_SIZE
  fi
}
LOG_OFFSET=0
MAX_WAIT=1500
WAIT_SECONDS=0
while [ $WAIT_SECONDS -lt $MAX_WAIT ]; do
  sleep 5
  WAIT_SECONDS=$((WAIT_SECONDS + 5))
  flush_log
  if [ -f "$LOG_FILE" ]; then
    if grep -q "Exiting batchmode successfully now!" "$LOG_FILE" 2>/dev/null; then break; fi
    if grep -qE "FAIL_NPM_BUILD|FAIL_NPM_INSTALL|No valid Unity Editor license found|batch mode failed" "$LOG_FILE" 2>/dev/null; then break; fi
  fi
  if ! kill -0 $UNITY_PID 2>/dev/null; then break; fi
done
flush_log

# 로그 기반 성공 판정 (exit code 대신 — websockify가 exit code를 왜곡할 수 있음)
EXIT_CODE=0
if ! grep -q "Exiting batchmode successfully now!" "$LOG_FILE" 2>/dev/null; then
  EXIT_CODE=1
fi

# Unity 프로세스 트리 강제 종료
cleanup_unity
sleep 1

echo "Unity build finished (log-based exit code: $EXIT_CODE)"

if [ $EXIT_CODE -ne 0 ]; then
  # C# 컴파일 오류 우선 감지 — 결정적 실패이므로 재시도는 순수 낭비다.
  # Unity 로그에는 정상 기동 시에도 "Access token is unavailable" 같은 라이선스
  # 문구가 섞여 있어, 이 검사가 없으면 아래 LICENSE_ERROR가 먼저 걸려 컴파일
  # 오류가 라이선스 오류로 3회 재시도된다 (v3.0.4 릴리즈 실패 시 실제 발생).
  if grep -qE "error CS[0-9]+" "$LOG_FILE" 2>/dev/null; then
    echo "::error::C# compile error detected (not a license issue), failing immediately"
    grep -E "error CS[0-9]+" "$LOG_FILE" 2>/dev/null | sort -u | head -10
    exit $EXIT_CODE
  fi

  # 빌드 도구 에러 우선 감지 (라이선스 오분류 방지)
  BUILD_TOOL_ERROR=false
  if grep -qE "Unknown Syntax Error|FAIL_NPM_BUILD|FAIL_NPM_INSTALL|Command not found.*ait|npm ERR!" "$LOG_FILE" 2>/dev/null; then
    BUILD_TOOL_ERROR=true
  fi

  if [ "$BUILD_TOOL_ERROR" = true ]; then
    echo "::error::Build tool error detected (not a license issue), failing immediately"
    grep -E "Unknown Syntax Error|FAIL_NPM|Command not found|npm ERR!" "$LOG_FILE" 2>/dev/null | tail -5
    exit $EXIT_CODE
  fi

  # 라이선스 관련 오류 감지 (retry 액션이 자동 재시도)
  # signature/handshake/IPC 실패는 같은 self-hosted runner에서 다른 Unity 버전의
  # LicensingClient가 살아있어 충돌하는 케이스 — retry로 회복 가능.
  LICENSE_ERROR=false
  if grep -qE "No valid Unity Editor license found|Unsupported protocol version|ResponseCode: 505|Access token is unavailable|Code 10 while verifying Licensing Client signature|LicensingClient has failed validation|Failed to handshake to channel|IPC channel to LicensingClient doesn't exist" "$LOG_FILE" 2>/dev/null; then
    LICENSE_ERROR=true
  fi
  if [ $EXIT_CODE -eq 199 ]; then
    LICENSE_ERROR=true
  fi

  if [ "$LICENSE_ERROR" = true ]; then
    echo "::warning::Unity license error detected, triggering retry..."
    exit 42
  fi

  echo "::error::Unity build failed with exit code: $EXIT_CODE"
  exit $EXIT_CODE
fi

# 성공 시 attempt 카운터 정리
rm -f "$ATTEMPT_FILE"
echo "Unity build completed successfully"
