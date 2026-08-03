#!/bin/bash
# scripts~/lib-unity-discovery.sh — Unity 경로/버전 탐색 헬퍼, list_unity_versions, show_help
# lib-globals.sh 이후에 source되어야 함

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

# perf 무거운 픽스처 프로젝트 경로 매핑 (HeavySampleUnityProject-{2021.3,6000.0,6000.3} 3종만 존재)
get_heavy_project_path_for_version() {
    local version_pattern="$1"
    local short_version=$(echo "$version_pattern" | grep -oE '^[0-9]+\.[0-9]+')
    echo "$SCRIPT_DIR/Tests~/E2E/HeavySampleUnityProject-$short_version"
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
    echo "│ --editmode                  │ Unity EditMode 테스트              │ ~10초        │"
    echo "│ --editmode --parallel       │ 모든 버전 EditMode 병렬 테스트     │ ~10초 (병렬) │"
    echo "│ --compression <format>      │ 압축 포맷 지정 (auto/disabled/...) │ -            │"
    echo "│ --unity-build               │ Unity WebGL 빌드 (단일 버전)       │ ~20분        │"
    echo "│ --unity-build --parallel    │ 모든 버전 병렬 빌드                │ ~20분 (병렬) │"
    echo "│ --e2e                       │ Playwright 테스트 (빌드 필요)      │ ~5분         │"
    echo "│ --e2e --parallel            │ 모든 빌드에 대해 E2E 테스트        │ ~15분        │"
    echo "│ --heavy                     │ 무거운 픽스처 릴리즈+gzip 빌드     │ ~25분        │"
    echo "│ --perf                      │ 로딩 성능(TTFF) 실측 (heavy 필요)  │ ~5분         │"
    echo "│ --heavy --perf              │ 무거운 빌드 + TTFF 실측            │ ~30분        │"
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
    echo "  --validate    : [1] 파일 구조 검증 → [2] Playwright 설정 → [3] SDK 유닛 테스트 → [4] Meta GUID 위생"
    echo "  --editmode    : [1] Unity EditMode 테스트 (~10초, 빌드 불필요)"
    echo "  --unity-build : [1] Unity WebGL 빌드"
    echo "  --e2e         : [1] Playwright E2E 테스트 (빌드 결과물 필요)"
    echo "  --heavy       : [1] 무거운 픽스처(HeavySampleUnityProject) 릴리즈+gzip 빌드"
    echo "  --perf        : [1] TTFF 실측 (무거운 빌드 결과물 필요)"
    echo "  --heavy --perf: [1] 무거운 빌드 → [2] TTFF 실측 (CI perf.yml과 동일 경로)"
    echo "  --all         : [1] 파일 검증 → [2] Playwright 설정 → [3] SDK 유닛 테스트 → [4] EditMode → [5] Unity 빌드 → [6] E2E 테스트"
    echo ""
    echo "--parallel 플래그:"
    echo "  다른 모드와 조합하여 모든 버전을 병렬로 처리합니다:"
    echo "  --editmode --parallel    : 모든 Unity 버전 EditMode 병렬 테스트"
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
