#!/bin/bash
#
# GitHub Actions 테스트를 로컬에서 실행하는 스크립트
# 사용법: ./run-local-tests.sh [옵션]
#
# 옵션:
#   --all                    모든 테스트 실행 (Unity 빌드 포함)
#   --quick                  빠른 테스트만 (E2E validation)
#   --e2e                    E2E 테스트만 (빌드 결과물 필요)
#   --unity-build            Unity WebGL 빌드 실행
#   --unity-version <버전>   특정 Unity 버전 지정 (예: 2022.3, 6000.0)
#   --compression <format>   압축 포맷 지정 (auto, disabled, gzip, brotli)
#   --parallel               다른 모드와 조합하여 병렬 실행 (예: --unity-build --parallel)
#   --list-unity             설치된 Unity 버전 목록 표시
#   --help                   도움말
#

# set -e 제거 - 각 테스트 함수에서 직접 에러 처리

# NODE_OPTIONS 환경변수 제거 (문제 유발 가능)
unset NODE_OPTIONS 2>/dev/null || true

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 프로젝트 루트 디렉토리
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 결과 저장
PASSED=0
FAILED=0
SKIPPED=0

# Unity 버전 설정
UNITY_VERSION=""
UNITY_PATH=""
PARALLEL_MODE=false
COMPRESSION_FORMAT=""

# 지원하는 Unity 버전 패턴 (우선순위 순)
UNITY_VERSION_PATTERNS=(
    "6000.3"    # Unity 6000.3.x (Unity 6.3)
    "6000.2"    # Unity 6000.2.x (Unity 6 LTS)
    "6000.0"    # Unity 6000.0.x (Unity 6)
    "2022.3"    # Unity 2022.3.x LTS
    "2021.3"    # Unity 2021.3.x LTS (최소 지원 버전)
)

# 버전별 프로젝트 경로 매핑
get_project_path_for_version() {
    local version_pattern="$1"

    case "$version_pattern" in
        6000.3*)
            echo "$SCRIPT_DIR/Tests~/E2E/SampleUnityProject-6000.3"
            ;;
        6000.2*)
            echo "$SCRIPT_DIR/Tests~/E2E/SampleUnityProject-6000.2"
            ;;
        6000.0*)
            echo "$SCRIPT_DIR/Tests~/E2E/SampleUnityProject-6000.0"
            ;;
        2022.3*)
            echo "$SCRIPT_DIR/Tests~/E2E/SampleUnityProject-2022.3"
            ;;
        2021.3*)
            echo "$SCRIPT_DIR/Tests~/E2E/SampleUnityProject-2021.3"
            ;;
        *)
            # 기본값: 버전 패턴에서 major.minor 추출
            local short_version=$(echo "$version_pattern" | grep -oE '^[0-9]+\.[0-9]+')
            echo "$SCRIPT_DIR/Tests~/E2E/SampleUnityProject-$short_version"
            ;;
    esac
}

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

# 설치된 Unity 버전 목록 가져오기
get_installed_unity_versions() {
    local versions=()
    local hub_path="/Applications/Unity/Hub/Editor"

    if [ -d "$hub_path" ]; then
        for dir in "$hub_path"/*/; do
            if [ -d "$dir" ]; then
                local version_name=$(basename "$dir")
                local unity_exe="$dir/Unity.app/Contents/MacOS/Unity"
                if [ -f "$unity_exe" ]; then
                    versions+=("$version_name")
                fi
            fi
        done
    fi

    # 버전 역순 정렬 (최신 버전 우선)
    printf '%s\n' "${versions[@]}" | sort -rV
}

# 설치된 Unity 버전 목록 출력
list_unity_versions() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${BLUE}📦 설치된 Unity 버전 목록${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""

    local versions=($(get_installed_unity_versions))

    if [ ${#versions[@]} -eq 0 ]; then
        echo "  ❌ 설치된 Unity 버전이 없습니다."
        echo "  Unity Hub에서 Unity를 설치해주세요."
        return 1
    fi

    echo "  지원 대상 버전:"
    for version in "${versions[@]}"; do
        local is_supported=""
        local project_path=""
        for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
            if [[ "$version" == "$pattern"* ]]; then
                is_supported="✓"
                project_path=$(get_project_path_for_version "$pattern")
                break
            fi
        done

        if [ -n "$is_supported" ]; then
            if [ -d "$project_path" ]; then
                echo -e "    ${GREEN}✓${NC} $version → $(basename "$project_path")"
            else
                echo -e "    ${YELLOW}✓${NC} $version (프로젝트 없음)"
            fi
        else
            echo -e "    ${YELLOW}○${NC} $version (지원 대상 외)"
        fi
    done

    echo ""
    echo "  버전별 프로젝트 디렉토리:"
    for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
        local project_path=$(get_project_path_for_version "$pattern")
        if [ -d "$project_path" ]; then
            echo -e "    ${GREEN}✓${NC} $pattern → $(basename "$project_path")"
        else
            echo -e "    ${RED}✗${NC} $pattern → 프로젝트 없음"
        fi
    done

    echo ""
    echo "  사용법:"
    echo "    ./run-local-tests.sh --unity-build --unity-version 2022.3"
    echo "    ./run-local-tests.sh --all --unity-version 6000.0"
    echo "    ./run-local-tests.sh --unity-build --parallel   # 모든 버전 병렬 빌드"
    echo "    ./run-local-tests.sh --all --parallel           # 모든 버전 빌드 + 테스트"
    echo ""
}

# 특정 버전 패턴에 맞는 Unity 경로 찾기
find_unity_by_pattern() {
    local pattern="$1"
    local hub_path="/Applications/Unity/Hub/Editor"

    # 패턴에 맞는 버전들을 찾아서 최신 버전 반환
    local matching_versions=()
    for dir in "$hub_path"/"$pattern"*/; do
        if [ -d "$dir" ]; then
            local unity_exe="$dir/Unity.app/Contents/MacOS/Unity"
            if [ -f "$unity_exe" ]; then
                matching_versions+=("$dir")
            fi
        fi
    done

    if [ ${#matching_versions[@]} -gt 0 ]; then
        # 버전 역순 정렬 후 첫 번째 (최신) 반환
        local latest=$(printf '%s\n' "${matching_versions[@]}" | sort -rV | head -1)
        echo "${latest}Unity.app/Contents/MacOS/Unity"
    fi
}

# Unity 경로 찾기 (버전 지정 또는 자동 탐지)
find_unity_path() {
    local requested_version="$1"

    if [ -n "$requested_version" ]; then
        # 특정 버전이 요청된 경우
        local found_path=$(find_unity_by_pattern "$requested_version")
        if [ -n "$found_path" ] && [ -f "$found_path" ]; then
            echo "$found_path"
            return 0
        else
            echo ""
            return 1
        fi
    fi

    # 자동 탐지: 우선순위 순으로 찾기
    for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
        local found_path=$(find_unity_by_pattern "$pattern")
        if [ -n "$found_path" ] && [ -f "$found_path" ]; then
            echo "$found_path"
            return 0
        fi
    done

    echo ""
    return 1
}

# Unity 버전 정보 추출
get_unity_version_from_path() {
    local path="$1"
    # /Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/... 에서 버전 추출
    echo "$path" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+[a-z][0-9]+'
}

# 버전 패턴 추출 (예: 2022.3.62f3 -> 2022.3)
get_version_pattern() {
    local full_version="$1"
    echo "$full_version" | grep -oE '^[0-9]+\.[0-9]+'
}

# 도움말
show_help() {
    echo "Apps in Toss Unity SDK - 로컬 테스트 실행 스크립트"
    echo ""
    echo "사용법: $0 [옵션]"
    echo ""
    echo "┌─────────────────────────────────────────────────────────────────────────────────────┐"
    echo "│ 옵션                        │ 실행 내용                          │ 소요 시간    │"
    echo "├─────────────────────────────────────────────────────────────────────────────────────┤"
    echo "│ --validate                  │ 파일 검증 + SDK 유닛 테스트        │ ~30초        │"
    echo "│ --compression <format>      │ 압축 포맷 지정 (auto/disabled/...) │ -            │"
    echo "│ --unity-build               │ Unity WebGL 빌드 (단일 버전)       │ ~20분        │"
    echo "│ --unity-build --parallel    │ 모든 버전 병렬 빌드                │ ~20분 (병렬) │"
    echo "│ --e2e                       │ Playwright 테스트 (빌드 필요)      │ ~5분         │"
    echo "│ --e2e --parallel            │ 모든 빌드에 대해 E2E 테스트        │ ~15분        │"
    echo "│ --all                       │ 빌드 + 테스트 (단일 버전)          │ ~30분        │"
    echo "│ --all --parallel            │ 빌드 + 테스트 (모든 버전 병렬)     │ ~30분 (병렬) │"
    echo "│ --list-unity                │ 설치된 Unity 버전 목록 표시        │ 즉시         │"
    echo "│ --unity-version <버전>      │ 특정 Unity 버전 지정               │ -            │"
    echo "└─────────────────────────────────────────────────────────────────────────────────────┘"
    echo ""
    echo "버전별 프로젝트 구조:"
    echo "  Tests~/E2E/"
    echo "    ├── SampleUnityProject-2021.3/   # Unity 2021.3.x용"
    echo "    ├── SampleUnityProject-2022.3/   # Unity 2022.3.x용"
    echo "    ├── SampleUnityProject-6000.0/   # Unity 6000.0.x용"
    echo "    ├── SampleUnityProject-6000.2/   # Unity 6000.2.x용"
    echo "    └── SampleUnityProject-6000.3/   # Unity 6000.3.x용"
    echo ""
    echo "Unity 버전 지정:"
    echo "  --unity-version 옵션으로 특정 Unity 버전을 지정할 수 있습니다."
    echo "  지정하지 않으면 우선순위에 따라 자동 선택됩니다."
    echo ""
    echo "  지원 버전 (우선순위 순):"
    echo "    • Unity 6000.3.x (Unity 6.3)"
    echo "    • Unity 6000.2.x (Unity 6 LTS)"
    echo "    • Unity 6000.0.x (Unity 6)"
    echo "    • Unity 2022.3.x LTS"
    echo "    • Unity 2021.3.x LTS (최소 지원 버전)"
    echo ""
    echo "  예시:"
    echo "    $0 --unity-build --unity-version 6000.2"
    echo "    $0 --all --unity-version 2022.3"
    echo "    $0 --unity-build --parallel   # 모든 버전 병렬 빌드"
    echo "    $0 --list-unity               # 설치된 버전 확인"
    echo ""
    echo "실행 순서:"
    echo "  --validate    : [1] 파일 구조 검증 → [2] Playwright 설정 → [3] SDK 유닛 테스트"
    echo "  --unity-build : [1] Unity WebGL 빌드"
    echo "  --e2e         : [1] Playwright E2E 테스트 (빌드 결과물 필요)"
    echo "  --all         : [1] 파일 검증 → [2] Playwright 설정 → [3] SDK 유닛 테스트 → [4] Unity 빌드 → [5] E2E 테스트"
    echo ""
    echo "--parallel 플래그:"
    echo "  다른 모드와 조합하여 모든 버전을 병렬로 처리합니다:"
    echo "  --unity-build --parallel : 모든 Unity 버전 병렬 빌드 (E2E 테스트 없음)"
    echo "  --e2e --parallel         : 모든 빌드에 대해 E2E 테스트"
    echo "  --all --parallel         : 전체 파이프라인 병렬 실행"
    echo ""
    echo "권장 워크플로우:"
    echo "  1. 처음 실행:     $0 --all                    # 전체 빌드 + 테스트 (단일 버전)"
    echo "  2. 코드 수정 후:  $0 --e2e                    # 기존 빌드로 빠른 테스트"
    echo "  3. SDK 변경 후:   $0 --unity-build && $0 --e2e"
    echo "  4. 다중 버전:     $0 --unity-build --parallel # 모든 버전 병렬 빌드"
    echo "  5. 전체 병렬:     $0 --all --parallel         # 모든 버전 빌드 + 테스트"
    echo ""
    exit 0
}

# 1. E2E 파일 구조 검증
test_e2e_validation() {
    print_header "E2E Test Files Validation"

    local all_found=true

    echo "Checking E2E test structure..."

    # SharedScripts 패키지 확인 (UPM 패키지)
    if [ -d "Tests~/E2E/SharedScripts" ]; then
        echo -e "  ${GREEN}✓${NC} SharedScripts package"

        # SharedScripts 내부 파일 확인
        if [ ! -f "Tests~/E2E/SharedScripts/Runtime/RuntimeAPITester.cs" ]; then
            echo -e "    ${RED}✗${NC} RuntimeAPITester.cs not found"
            all_found=false
        fi

        if [ ! -f "Tests~/E2E/SharedScripts/Editor/E2EBuildRunner.cs" ]; then
            echo -e "    ${RED}✗${NC} E2EBuildRunner.cs not found"
            all_found=false
        fi
    else
        echo -e "  ${RED}✗${NC} SharedScripts package not found"
        all_found=false
    fi

    # 각 버전별 프로젝트 디렉토리 확인
    echo ""
    echo "Checking Unity project directories..."
    for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
        local project_path=$(get_project_path_for_version "$pattern")
        local project_name=$(basename "$project_path")

        if [ -d "$project_path" ]; then
            # manifest.json에서 SharedScripts 패키지 참조 확인
            if grep -q "im.toss.sdk-test-scripts" "$project_path/Packages/manifest.json" 2>/dev/null; then
                echo -e "  ${GREEN}✓${NC} $project_name (SharedScripts linked)"
            else
                echo -e "  ${YELLOW}!${NC} $project_name (SharedScripts not in manifest)"
            fi
        else
            echo -e "  ${YELLOW}○${NC} $project_name (없음)"
        fi
    done

    # Playwright 테스트 파일 확인
    echo ""
    echo "Checking Playwright test files..."

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
    local version_pattern="$1"
    local project_path=""

    if [ -n "$version_pattern" ]; then
        project_path=$(get_project_path_for_version "$version_pattern")
        print_header "E2E Playwright Tests ($version_pattern)"
    else
        # 자동 탐지: 빌드 결과물이 있는 첫 번째 프로젝트 사용
        for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
            local test_path=$(get_project_path_for_version "$pattern")
            if [ -d "$test_path/ait-build/dist/web" ]; then
                project_path="$test_path"
                version_pattern="$pattern"
                break
            fi
        done
        print_header "E2E Playwright Tests"
    fi

    # 빌드 결과물 확인
    if [ -z "$project_path" ] || [ ! -d "$project_path/ait-build/dist/web" ]; then
        print_skip "E2E Playwright Tests - 빌드 결과물 없음 (--unity-build 먼저 실행)"
        return 0
    fi

    echo "Using project: $(basename "$project_path")"

    cd "$SCRIPT_DIR/Tests~/E2E/tests"

    echo "Installing dependencies..."
    pnpm install --silent

    echo "Installing Playwright Chromium..."
    pnpx playwright install chromium

    # EXPECTED_COMPRESSION 환경변수 설정 (압축 포맷 검증용)
    local expected_compression=""
    if [ -n "$COMPRESSION_FORMAT" ] && [ "$COMPRESSION_FORMAT" != "auto" ]; then
        expected_compression="$COMPRESSION_FORMAT"
        echo "Expected compression: $expected_compression"
    elif [ -n "$COMPRESSION_FORMAT" ] && [ "$COMPRESSION_FORMAT" = "auto" ]; then
        expected_compression="brotli"
        echo "Expected compression: $expected_compression (auto → brotli)"
    fi

    # 테스트 실행 시 프로젝트 경로를 환경변수로 전달
    echo "Running E2E tests..."
    local e2e_env="UNITY_PROJECT_PATH=$project_path"
    if [ -n "$expected_compression" ]; then
        e2e_env="$e2e_env EXPECTED_COMPRESSION=$expected_compression"
    fi
    if env $e2e_env pnpm test; then
        print_success "E2E Playwright Tests ($version_pattern)"

        # 결과 출력
        if [ -f "e2e-test-results.json" ]; then
            echo ""
            echo "📊 Benchmark Results:"
            cat e2e-test-results.json | head -30
        fi
    else
        print_failure "E2E Playwright Tests ($version_pattern)"
        cd "$SCRIPT_DIR"
        return 1
    fi

    cd "$SCRIPT_DIR"
}

# 4. Unity WebGL 빌드
test_unity_build() {
    local version_pattern="${1:-$UNITY_VERSION}"

    print_header "Unity WebGL Build"

    # Unity 경로 찾기
    local unity_path=$(find_unity_path "$version_pattern")

    if [ -z "$unity_path" ]; then
        if [ -n "$version_pattern" ]; then
            print_failure "Unity WebGL Build - Unity $version_pattern 버전을 찾을 수 없음"
            echo ""
            echo "설치된 Unity 버전 확인:"
            list_unity_versions
        else
            print_skip "Unity WebGL Build - Unity를 찾을 수 없음"
        fi
        return 1
    fi

    local detected_version=$(get_unity_version_from_path "$unity_path")
    local detected_pattern=$(get_version_pattern "$detected_version")
    local project_path=$(get_project_path_for_version "$detected_pattern")

    echo "Using Unity: $detected_version"
    echo "Path: $unity_path"
    echo "Project: $(basename "$project_path")"

    # 프로젝트 디렉토리 확인
    if [ ! -d "$project_path" ]; then
        print_failure "Unity WebGL Build - 프로젝트 디렉토리 없음: $project_path"
        return 1
    fi

    local LOG_FILE="$project_path/unity-build.log"

    # 압축 포맷 환경변수 설정
    local compression_env=""
    if [ -n "$COMPRESSION_FORMAT" ]; then
        case "$COMPRESSION_FORMAT" in
            auto)     compression_env="-1" ;;
            disabled) compression_env="0" ;;
            gzip)     compression_env="1" ;;
            brotli)   compression_env="2" ;;
        esac
        echo "Compression format: $COMPRESSION_FORMAT (AIT_COMPRESSION_FORMAT=$compression_env)"
    fi

    echo "Building WebGL..."
    echo "Log file: $LOG_FILE"
    echo "AIT_DEBUG_CONSOLE: true"

    # 기존 빌드 정리 (Library는 패키지 캐시를 위해 유지)
    rm -rf "$project_path/ait-build"
    rm -rf "$project_path/Temp"

    # Unity 빌드 실행 (AIT_DEBUG_CONSOLE=true로 디버그 콘솔 활성화)
    local env_vars="AIT_DEBUG_CONSOLE=true"
    if [ -n "$compression_env" ]; then
        env_vars="$env_vars AIT_COMPRESSION_FORMAT=$compression_env"
    fi
    if env $env_vars "$unity_path" \
        -quit -batchmode -nographics \
        -projectPath "$project_path" \
        -executeMethod E2EBuildRunner.CommandLineBuild \
        -logFile "$LOG_FILE"; then

        # 빌드 결과 확인
        if [ -d "$project_path/ait-build/dist/web" ]; then
            print_success "Unity WebGL Build ($detected_version)"
            echo "Build output: $project_path/ait-build/dist/web"
            du -sh "$project_path/ait-build/dist/web"
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
    pnpm install --silent 2>/dev/null || pnpm install

    echo "Validating Playwright version..."
    pnpx playwright --version 2>/dev/null

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

# 5.5 SDK 생성기 유닛 테스트
test_sdk_generator_unit() {
    print_header "SDK Generator Unit Tests"

    local GENERATOR_PATH="$SCRIPT_DIR/sdk-runtime-generator~"

    if [ ! -d "$GENERATOR_PATH" ]; then
        print_skip "SDK Generator Unit Tests - sdk-runtime-generator~ 디렉토리 없음"
        return 0
    fi

    cd "$GENERATOR_PATH"

    echo "Installing dependencies..."
    pnpm install --silent 2>/dev/null || pnpm install

    local tier2_passed=true

    # C# ↔ jslib 일관성 검증
    echo "Running unit tests (C# ↔ jslib invariants)..."
    if pnpm run test:tier2 2>&1; then
        echo -e "${GREEN}✓${NC} C# ↔ jslib invariants passed"
    else
        echo -e "${RED}✗${NC} C# ↔ jslib invariants failed"
        tier2_passed=false
    fi

    cd "$SCRIPT_DIR"

    if [ "$tier2_passed" = true ]; then
        print_success "SDK Generator Unit Tests"
        return 0
    else
        print_failure "SDK Generator Unit Tests"
        return 1
    fi
}

# 6. 병렬 Unity 빌드만 (E2E 테스트 없음)
run_parallel_unity_builds_only() {
    print_header "Parallel Unity Builds Only"

    local versions_to_build=()
    local pids=()
    local log_files=()

    # 설치된 버전 중 지원하는 버전 찾기
    local installed_versions=($(get_installed_unity_versions))

    for version in "${installed_versions[@]}"; do
        for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
            if [[ "$version" == "$pattern"* ]]; then
                local project_path=$(get_project_path_for_version "$pattern")
                if [ -d "$project_path" ]; then
                    # 중복 패턴 방지 (같은 major.minor에 여러 버전이 설치된 경우)
                    local already_added=false
                    for existing in "${versions_to_build[@]}"; do
                        if [ "$existing" = "$pattern" ]; then
                            already_added=true
                            break
                        fi
                    done
                    if [ "$already_added" = false ]; then
                        versions_to_build+=("$pattern")
                    fi
                fi
                break
            fi
        done
    done

    if [ ${#versions_to_build[@]} -eq 0 ]; then
        print_failure "병렬 빌드 - 빌드 가능한 버전 없음"
        return 1
    fi

    echo "Building ${#versions_to_build[@]} versions in parallel:"
    for pattern in "${versions_to_build[@]}"; do
        echo "  • $pattern"
    done
    echo ""

    # 병렬 빌드 시작
    for pattern in "${versions_to_build[@]}"; do
        local unity_path=$(find_unity_by_pattern "$pattern")
        local project_path=$(get_project_path_for_version "$pattern")
        local log_file="$project_path/unity-build.log"

        echo -e "${CYAN}Starting build for $pattern...${NC}"

        # 빌드 정리
        rm -rf "$project_path/ait-build"
        rm -rf "$project_path/Temp"

        # 압축 포맷 환경변수 설정
        local compression_env=""
        if [ -n "$COMPRESSION_FORMAT" ]; then
            case "$COMPRESSION_FORMAT" in
                auto)     compression_env="-1" ;;
                disabled) compression_env="0" ;;
                gzip)     compression_env="1" ;;
                brotli)   compression_env="2" ;;
            esac
        fi

        # 백그라운드로 빌드 실행 (AIT_DEBUG_CONSOLE=true로 디버그 콘솔 활성화)
        (
            local build_env="AIT_DEBUG_CONSOLE=true"
            if [ -n "$compression_env" ]; then
                build_env="$build_env AIT_COMPRESSION_FORMAT=$compression_env"
            fi
            env $build_env "$unity_path" \
                -quit -batchmode -nographics \
                -projectPath "$project_path" \
                -executeMethod E2EBuildRunner.CommandLineBuild \
                -logFile "$log_file"
        ) &

        pids+=($!)
        log_files+=("$log_file")
    done

    echo ""
    echo "Waiting for ${#pids[@]} builds to complete..."
    echo ""

    # 빌드 완료 대기 및 결과 확인
    for i in "${!pids[@]}"; do
        local pid=${pids[$i]}
        local pattern=${versions_to_build[$i]}
        local project_path=$(get_project_path_for_version "$pattern")
        local log_file=${log_files[$i]}

        wait $pid
        local exit_code=$?

        if [ $exit_code -eq 0 ] && [ -d "$project_path/ait-build/dist/web" ]; then
            echo -e "${GREEN}✓${NC} $pattern build completed"
            ((PASSED++))
        else
            echo -e "${RED}✗${NC} $pattern build failed"
            echo "  Log: $log_file"
            ((FAILED++))
        fi
    done
}

# 7. 병렬 빌드 및 테스트 (전체)
run_parallel_builds() {
    print_header "Parallel Unity Builds & E2E Tests"

    local versions_to_build=()
    local pids=()
    local log_files=()

    # 설치된 버전 중 지원하는 버전 찾기
    local installed_versions=($(get_installed_unity_versions))

    for version in "${installed_versions[@]}"; do
        for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
            if [[ "$version" == "$pattern"* ]]; then
                local project_path=$(get_project_path_for_version "$pattern")
                if [ -d "$project_path" ]; then
                    # 중복 패턴 방지 (같은 major.minor에 여러 버전이 설치된 경우)
                    local already_added=false
                    for existing in "${versions_to_build[@]}"; do
                        if [ "$existing" = "$pattern" ]; then
                            already_added=true
                            break
                        fi
                    done
                    if [ "$already_added" = false ]; then
                        versions_to_build+=("$pattern")
                    fi
                fi
                break
            fi
        done
    done

    if [ ${#versions_to_build[@]} -eq 0 ]; then
        print_failure "병렬 빌드 - 빌드 가능한 버전 없음"
        return 1
    fi

    echo "Building ${#versions_to_build[@]} versions in parallel:"
    for pattern in "${versions_to_build[@]}"; do
        echo "  • $pattern"
    done
    echo ""

    # 병렬 빌드 시작
    for pattern in "${versions_to_build[@]}"; do
        local unity_path=$(find_unity_by_pattern "$pattern")
        local project_path=$(get_project_path_for_version "$pattern")
        local log_file="$project_path/unity-build.log"

        echo -e "${CYAN}Starting build for $pattern...${NC}"

        # 빌드 정리
        rm -rf "$project_path/ait-build"
        rm -rf "$project_path/Temp"

        # 압축 포맷 환경변수 설정
        local compression_env=""
        if [ -n "$COMPRESSION_FORMAT" ]; then
            case "$COMPRESSION_FORMAT" in
                auto)     compression_env="-1" ;;
                disabled) compression_env="0" ;;
                gzip)     compression_env="1" ;;
                brotli)   compression_env="2" ;;
            esac
        fi

        # 백그라운드로 빌드 실행 (AIT_DEBUG_CONSOLE=true로 디버그 콘솔 활성화)
        (
            local build_env="AIT_DEBUG_CONSOLE=true"
            if [ -n "$compression_env" ]; then
                build_env="$build_env AIT_COMPRESSION_FORMAT=$compression_env"
            fi
            env $build_env "$unity_path" \
                -quit -batchmode -nographics \
                -projectPath "$project_path" \
                -executeMethod E2EBuildRunner.CommandLineBuild \
                -logFile "$log_file"
        ) &

        pids+=($!)
        log_files+=("$log_file")
    done

    echo ""
    echo "Waiting for ${#pids[@]} builds to complete..."
    echo ""

    # 빌드 완료 대기 및 결과 확인
    local build_results=()
    for i in "${!pids[@]}"; do
        local pid=${pids[$i]}
        local pattern=${versions_to_build[$i]}
        local project_path=$(get_project_path_for_version "$pattern")
        local log_file=${log_files[$i]}

        wait $pid
        local exit_code=$?

        if [ $exit_code -eq 0 ] && [ -d "$project_path/ait-build/dist/web" ]; then
            echo -e "${GREEN}✓${NC} $pattern build completed"
            build_results+=("$pattern:success")
            ((PASSED++))
        else
            echo -e "${RED}✗${NC} $pattern build failed"
            echo "  Log: $log_file"
            build_results+=("$pattern:failed")
            ((FAILED++))
        fi
    done

    echo ""
    echo "Build Summary:"
    for result in "${build_results[@]}"; do
        local pattern=$(echo "$result" | cut -d: -f1)
        local status=$(echo "$result" | cut -d: -f2)
        if [ "$status" = "success" ]; then
            echo -e "  ${GREEN}✓${NC} $pattern"
        else
            echo -e "  ${RED}✗${NC} $pattern"
        fi
    done

    # E2E 테스트 실행 (성공한 빌드에 대해)
    echo ""
    print_header "Running E2E Tests for Successful Builds"

    for result in "${build_results[@]}"; do
        local pattern=$(echo "$result" | cut -d: -f1)
        local status=$(echo "$result" | cut -d: -f2)

        if [ "$status" = "success" ]; then
            test_e2e_playwright "$pattern"
        fi
    done
}

# 벤치마크 결과 출력
print_benchmark_results() {
    local RESULTS_FILE="$SCRIPT_DIR/Tests~/E2E/tests/e2e-test-results.json"

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
    local mode=""
    local args=("$@")

    # 인수 파싱
    local i=0
    while [ $i -lt ${#args[@]} ]; do
        case "${args[$i]}" in
            --help|-h)
                show_help
                ;;
            --list-unity)
                list_unity_versions
                exit 0
                ;;
            --unity-version)
                i=$((i + 1))
                if [ $i -lt ${#args[@]} ]; then
                    UNITY_VERSION="${args[$i]}"
                else
                    echo -e "${RED}오류: --unity-version 옵션에 버전 값이 필요합니다.${NC}"
                    echo "예: --unity-version 2022.3"
                    exit 1
                fi
                ;;
            --compression)
                i=$((i + 1))
                if [ $i -lt ${#args[@]} ]; then
                    COMPRESSION_FORMAT="${args[$i]}"
                    case "$COMPRESSION_FORMAT" in
                        auto|disabled|gzip|brotli)
                            ;;
                        *)
                            echo -e "${RED}오류: --compression 값이 올바르지 않습니다: '$COMPRESSION_FORMAT'${NC}"
                            echo "유효한 값: auto, disabled, gzip, brotli"
                            exit 1
                            ;;
                    esac
                else
                    echo -e "${RED}오류: --compression 옵션에 포맷 값이 필요합니다.${NC}"
                    echo "유효한 값: auto, disabled, gzip, brotli"
                    exit 1
                fi
                ;;
            --parallel)
                PARALLEL_MODE=true
                # --parallel은 다른 모드와 조합 가능한 플래그 (모드 덮어쓰기 안함)
                ;;
            --all|--e2e|--unity-build|--validate)
                mode="${args[$i]}"
                ;;
            *)
                echo "Unknown option: ${args[$i]}"
                show_help
                ;;
        esac
        i=$((i + 1))
    done

    # 기본 모드 설정
    mode="${mode:---validate}"

    echo ""
    echo "╔══════════════════════════════════════════════════════════════════════════╗"
    echo "║           Apps in Toss Unity SDK - Local Test Runner                    ║"
    echo "╚══════════════════════════════════════════════════════════════════════════╝"
    echo ""
    echo "Mode: $mode"
    if [ -n "$UNITY_VERSION" ]; then
        echo "Unity Version: $UNITY_VERSION"
    fi
    if [ -n "$COMPRESSION_FORMAT" ]; then
        echo "Compression: $COMPRESSION_FORMAT"
    fi
    echo "Directory: $SCRIPT_DIR"

    case "$mode" in
        --all)
            test_e2e_validation
            test_playwright_config
            test_sdk_generator_unit
            if [ "$PARALLEL_MODE" = true ]; then
                run_parallel_builds
            else
                test_unity_build "$UNITY_VERSION"
                test_e2e_playwright "$(get_version_pattern "$(get_unity_version_from_path "$(find_unity_path "$UNITY_VERSION")")")"
            fi
            ;;
        --e2e)
            if [ "$PARALLEL_MODE" = true ]; then
                # 모든 빌드된 프로젝트에 대해 E2E 테스트 실행
                for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
                    local project_path=$(get_project_path_for_version "$pattern")
                    if [ -d "$project_path/ait-build/dist/web" ]; then
                        test_e2e_playwright "$pattern"
                    fi
                done
            elif [ -n "$UNITY_VERSION" ]; then
                test_e2e_playwright "$UNITY_VERSION"
            else
                test_e2e_playwright
            fi
            ;;
        --unity-build)
            if [ "$PARALLEL_MODE" = true ]; then
                # 병렬 Unity 빌드만 실행 (E2E 테스트 없음)
                run_parallel_unity_builds_only
            else
                test_unity_build "$UNITY_VERSION"
            fi
            ;;
        --validate)
            test_e2e_validation
            test_playwright_config
            test_sdk_generator_unit
            ;;
    esac

    print_summary
}

# 실행
main "$@"
