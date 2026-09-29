#!/bin/bash
# scripts~/lib-globals.sh — 공유 전역 변수, 색상 상수, 버전 패턴 배열
# run-local-tests.sh에서 source되어 사용됨

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 결과 저장
PASSED=0
FAILED=0
SKIPPED=0

# Unity 버전 설정
UNITY_VERSION=""
UNITY_PATH=""
PARALLEL_MODE=false
COMPRESSION_FORMAT=""
RUN_HEAVY=false   # --heavy: 무거운 픽스처 빌드(perf 측정용)
RUN_PERF=false    # --perf:  TTFF 실측 (무거운 빌드 결과물 필요)
EDITMODE_FAILED_VERSIONS=()  # 병렬 EditMode 실패 버전 (빌드 스킵용)

# 지원하는 Unity 버전 패턴 (우선순위 순)
UNITY_VERSION_PATTERNS=(
    "6000.3"    # Unity 6000.3.x (Unity 6.3)
    "6000.2"    # Unity 6000.2.x (Unity 6 LTS)
    "6000.0"    # Unity 6000.0.x (Unity 6)
    "2022.3"    # Unity 2022.3.x LTS
    "2021.3"    # Unity 2021.3.x LTS (최소 지원 버전)
)

# COMPRESSION_FORMAT 문자열을 AIT_COMPRESSION_FORMAT 환경변수 값으로 변환
# 사용: local env_val=$(compression_format_to_env "$COMPRESSION_FORMAT")
# 반환: -1 (auto), 0 (disabled), 1 (gzip), 2 (brotli), "" (미지정)
compression_format_to_env() {
    local fmt="$1"
    case "$fmt" in
        auto)     echo "-1" ;;
        disabled) echo "0" ;;
        gzip)     echo "1" ;;
        brotli)   echo "2" ;;
        *)        echo "" ;;
    esac
}
