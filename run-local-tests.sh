#!/bin/bash
#
# GitHub Actions 테스트를 로컬에서 실행하는 스크립트
# 사용법: ./run-local-tests.sh [옵션]
#
# 옵션:
#   --all           모든 테스트 실행 (Unity 빌드 포함)
#   --quick         빠른 테스트만 (E2E validation)
#   --e2e           E2E 테스트만 (빌드 결과물 필요)
#   --unity-build   Unity WebGL 빌드 실행
#   --help          도움말
#

# set -e 제거 - 각 테스트 함수에서 직접 에러 처리

# NODE_OPTIONS 환경변수 제거 (문제 유발 가능)
unset NODE_OPTIONS 2>/dev/null || true

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 프로젝트 루트 디렉토리
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 결과 저장
PASSED=0
FAILED=0
SKIPPED=0

# 유틸리티 함수
print_header() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${BLUE}▶ $1${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
    ((PASSED++))
}

print_failure() {
    echo -e "${RED}✗ $1${NC}"
    ((FAILED++))
}

print_skip() {
    echo -e "${YELLOW}⊘ $1 (skipped)${NC}"
    ((SKIPPED++))
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

# 도움말
show_help() {
    echo "Apps in Toss Unity SDK - 로컬 테스트 실행 스크립트"
    echo ""
    echo "사용법: $0 [옵션]"
    echo ""
    echo "┌─────────────────────────────────────────────────────────────────────────┐"
    echo "│ 옵션            │ 실행 내용                           │ 소요 시간     │"
    echo "├─────────────────────────────────────────────────────────────────────────┤"
    echo "│ --validate      │ 파일 구조 검증 + Playwright 설정    │ ~30초         │"
    echo "│ --unity-build   │ Unity WebGL 빌드                    │ ~20분         │"
    echo "│ --e2e           │ Playwright 7개 테스트 (빌드 필요)   │ ~5분          │"
    echo "│ --all           │ Unity 빌드 + Playwright 테스트      │ ~30분         │"
    echo "└─────────────────────────────────────────────────────────────────────────┘"
    echo ""
    echo "실행 순서:"
    echo "  --validate    : [1] 파일 구조 검증 → [2] Playwright 설정 검증"
    echo "  --unity-build : [1] Unity WebGL 빌드"
    echo "  --e2e         : [1] Playwright E2E 테스트 (빌드 결과물 필요)"
    echo "  --all         : [1] 파일 검증 → [2] Playwright 설정 → [3] Unity 빌드 → [4] E2E 테스트"
    echo ""
    echo "권장 워크플로우:"
    echo "  1. 처음 실행:     $0 --all           # 전체 빌드 + 테스트"
    echo "  2. 코드 수정 후:  $0 --e2e           # 기존 빌드로 빠른 테스트"
    echo "  3. SDK 변경 후:   $0 --unity-build && $0 --e2e"
    echo ""
    exit 0
}

# 1. E2E 파일 구조 검증
test_e2e_validation() {
    print_header "E2E Test Files Validation"

    local all_found=true

    echo "Checking E2E test structure..."

    if [ ! -f "Tests~/E2E/SampleUnityProject/Assets/Scripts/AutoBenchmarkRunner.cs" ]; then
        echo "  ❌ AutoBenchmarkRunner.cs not found"
        all_found=false
    else
        echo "  ✓ AutoBenchmarkRunner.cs"
    fi

    if [ ! -f "Tests~/E2E/SampleUnityProject/Assets/Scripts/RuntimeAPITester.cs" ]; then
        echo "  ❌ RuntimeAPITester.cs not found"
        all_found=false
    else
        echo "  ✓ RuntimeAPITester.cs"
    fi

    if [ ! -f "Tests~/E2E/SampleUnityProject/Assets/Editor/E2EBuildRunner.cs" ]; then
        echo "  ❌ E2EBuildRunner.cs not found"
        all_found=false
    else
        echo "  ✓ E2EBuildRunner.cs"
    fi

    if [ ! -f "Tests~/E2E/tests/e2e-full-pipeline.test.js" ]; then
        echo "  ❌ e2e-full-pipeline.test.js not found"
        all_found=false
    else
        echo "  ✓ e2e-full-pipeline.test.js"
    fi

    if [ ! -f "Tests~/E2E/tests/playwright.config.ts" ]; then
        echo "  ❌ playwright.config.ts not found"
        all_found=false
    else
        echo "  ✓ playwright.config.ts"
    fi

    if [ "$all_found" = true ]; then
        print_success "E2E Test Files Validation"
    else
        print_failure "E2E Test Files Validation"
        return 1
    fi
}


# 3. Playwright E2E 테스트 (빌드 결과물 필요)
test_e2e_playwright() {
    print_header "E2E Playwright Tests"

    # 빌드 결과물 확인
    if [ ! -d "Tests~/E2E/SampleUnityProject/ait-build/dist/web" ]; then
        print_skip "E2E Playwright Tests - 빌드 결과물 없음 (--unity-build 먼저 실행)"
        return 0
    fi

    cd "$SCRIPT_DIR/Tests~/E2E/tests"

    echo "Installing dependencies..."
    npm ci --silent

    echo "Installing Playwright Chromium..."
    npx playwright install chromium

    echo "Running E2E tests..."
    if npm test; then
        print_success "E2E Playwright Tests"

        # 결과 출력
        if [ -f "benchmark-results.json" ]; then
            echo ""
            echo "📊 Benchmark Results:"
            cat benchmark-results.json | head -30
        fi
    else
        print_failure "E2E Playwright Tests"
        return 1
    fi

    cd "$SCRIPT_DIR"
}

# 4. Unity WebGL 빌드
test_unity_build() {
    print_header "Unity WebGL Build"

    # Unity 경로 찾기
    UNITY_PATH=""
    for path in "/Applications/Unity/Hub/Editor/2021.3."*"/Unity.app/Contents/MacOS/Unity"; do
        if [ -f "$path" ]; then
            UNITY_PATH="$path"
            break
        fi
    done

    if [ -z "$UNITY_PATH" ]; then
        for path in "/Applications/Unity/Hub/Editor/2022.3."*"/Unity.app/Contents/MacOS/Unity"; do
            if [ -f "$path" ]; then
                UNITY_PATH="$path"
                break
            fi
        done
    fi

    if [ -z "$UNITY_PATH" ]; then
        print_skip "Unity WebGL Build - Unity를 찾을 수 없음"
        return 0
    fi

    echo "Using Unity: $UNITY_PATH"

    local PROJECT_PATH="$SCRIPT_DIR/Tests~/E2E/SampleUnityProject"
    local LOG_FILE="$SCRIPT_DIR/Tests~/E2E/unity-build.log"

    echo "Building WebGL..."
    echo "Log file: $LOG_FILE"

    # 기존 빌드 정리 (Library는 패키지 캐시를 위해 유지)
    rm -rf "$PROJECT_PATH/ait-build"
    rm -rf "$PROJECT_PATH/Temp"

    # Unity 빌드 실행
    if "$UNITY_PATH" \
        -quit -batchmode -nographics \
        -projectPath "$PROJECT_PATH" \
        -executeMethod E2EBuildRunner.CommandLineBuild \
        -logFile "$LOG_FILE"; then

        # 빌드 결과 확인
        if [ -d "$PROJECT_PATH/ait-build/dist/web" ]; then
            print_success "Unity WebGL Build"
            echo "Build output: $PROJECT_PATH/ait-build/dist/web"
            du -sh "$PROJECT_PATH/ait-build/dist/web"
        else
            print_failure "Unity WebGL Build - 결과물 없음"
            echo "Check log: $LOG_FILE"
            tail -50 "$LOG_FILE"
            return 1
        fi
    else
        print_failure "Unity WebGL Build"
        echo "Check log: $LOG_FILE"
        tail -50 "$LOG_FILE"
        return 1
    fi
}

# 5. Playwright 설정 검증
test_playwright_config() {
    print_header "Playwright Config Validation"

    cd "$SCRIPT_DIR/Tests~/E2E/tests"

    echo "Installing dependencies..."
    npm install --silent 2>/dev/null || npm install

    echo "Validating Playwright version..."
    npx playwright --version 2>/dev/null

    echo "Checking test file exists..."
    if [ -f "e2e-full-pipeline.test.js" ]; then
        print_success "Playwright Config Validation"
    else
        print_failure "Playwright Config Validation"
        cd "$SCRIPT_DIR"
        return 1
    fi

    cd "$SCRIPT_DIR"
}

# 벤치마크 결과 출력
print_benchmark_results() {
    local RESULTS_FILE="$SCRIPT_DIR/Tests~/E2E/tests/benchmark-results.json"

    if [ ! -f "$RESULTS_FILE" ]; then
        return
    fi

    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${BLUE}📊 Benchmark Results${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    # JSON 파싱 (jq 없이 grep/sed 사용)
    local BUILD_SIZE=$(grep -o '"buildSizeMB": [0-9.]*' "$RESULTS_FILE" | head -1 | grep -o '[0-9.]*')
    local PAGE_LOAD=$(grep -o '"pageLoadTimeMs": [0-9]*' "$RESULTS_FILE" | head -1 | grep -o '[0-9]*')
    local UNITY_LOAD=$(grep -o '"unityLoadTimeMs": [0-9]*' "$RESULTS_FILE" | head -1 | grep -o '[0-9]*')
    local RENDERER=$(grep -o '"renderer": "[^"]*"' "$RESULTS_FILE" | head -1 | sed 's/"renderer": "//;s/"$//')

    echo ""
    echo "  📦 Build Size:      ${BUILD_SIZE:-N/A} MB"
    echo "  ⏱️  Page Load:       ${PAGE_LOAD:-N/A} ms"
    echo "  🎮 Unity Load:      ${UNITY_LOAD:-N/A} ms"
    echo "  🖥️  GPU Renderer:    ${RENDERER:-N/A}"
    echo ""
    echo "  📄 Full results:    $RESULTS_FILE"
}

# 결과 요약 출력
print_summary() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${BLUE}📋 Test Summary${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "  ${GREEN}Passed:  $PASSED${NC}"
    echo -e "  ${RED}Failed:  $FAILED${NC}"
    echo -e "  ${YELLOW}Skipped: $SKIPPED${NC}"

    # 벤치마크 결과 출력
    print_benchmark_results

    echo ""
    if [ $FAILED -eq 0 ]; then
        echo -e "${GREEN}✓ All tests passed!${NC}"
        return 0
    else
        echo -e "${RED}✗ Some tests failed${NC}"
        return 1
    fi
}

# 메인 실행
main() {
    local mode="${1:---validate}"

    echo ""
    echo "╔══════════════════════════════════════════════════════════════════════════╗"
    echo "║           Apps in Toss Unity SDK - Local Test Runner                    ║"
    echo "╚══════════════════════════════════════════════════════════════════════════╝"
    echo ""
    echo "Mode: $mode"
    echo "Directory: $SCRIPT_DIR"

    case "$mode" in
        --help|-h)
            show_help
            ;;
        --all)
            test_e2e_validation
            test_playwright_config
            test_unity_build
            test_e2e_playwright
            ;;
        --e2e)
            test_e2e_playwright
            ;;
        --unity-build)
            test_unity_build
            ;;
        --validate)
            test_e2e_validation
            test_playwright_config
            ;;
        *)
            echo "Unknown option: $mode"
            show_help
            ;;
    esac

    print_summary
}

# 실행
main "$@"
