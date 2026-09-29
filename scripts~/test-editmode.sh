#!/bin/bash
# scripts~/test-editmode.sh — Unity EditMode 테스트 (단일 / 병렬)
# lib-globals.sh, lib-logging.sh, lib-unity-discovery.sh 이후에 source되어야 함

# 6. Unity EditMode 테스트
test_editmode() {
    local version_pattern="${1:-$UNITY_VERSION}"

    print_header "Unity EditMode Tests"

    # Unity 경로 찾기
    local unity_path=$(find_unity_path "$version_pattern")

    if [ -z "$unity_path" ]; then
        if [ -n "$version_pattern" ]; then
            print_failure "Unity EditMode Tests - Unity $version_pattern 버전을 찾을 수 없음"
            echo ""
            echo "설치된 Unity 버전 확인:"
            list_unity_versions
            return 1
        else
            print_skip "Unity EditMode Tests - Unity를 찾을 수 없음"
            return 0
        fi
    fi

    local detected_version=$(get_unity_version_from_path "$unity_path")
    local detected_pattern=$(get_version_pattern "$detected_version")
    local project_path=$(get_project_path_for_version "$detected_pattern")

    echo "Using Unity: $detected_version"
    echo "Path: $unity_path"
    echo "Project: $(basename "$project_path")"

    # 프로젝트 디렉토리 확인
    if [ ! -d "$project_path" ]; then
        print_failure "Unity EditMode Tests - 프로젝트 디렉토리 없음: $project_path"
        return 1
    fi

    local RESULTS_FILE="$project_path/editmode-results.xml"

    # 이전 결과 파일 정리 (stale 결과로 오판 방지)
    rm -f "$RESULTS_FILE"

    echo "Running EditMode Tests..."

    # Unity Test Runner 실행 (-logFile -로 stdout 출력)
    "$unity_path" -batchmode -nographics \
        -projectPath "$project_path" \
        -runTests -testPlatform EditMode \
        -testResults "$RESULTS_FILE" \
        -logFile - 2>&1 || true

    # NUnit XML 파싱으로 결과 확인
    if [ -f "$RESULTS_FILE" ]; then
        if grep -q 'result="Failed"' "$RESULTS_FILE"; then
            echo ""
            echo "Failed tests:"
            grep 'result="Failed"' "$RESULTS_FILE" | head -20
            print_failure "Unity EditMode Tests ($detected_version)"
            return 1
        else
            local passed_count=$(grep -o 'result="Passed"' "$RESULTS_FILE" | wc -l | tr -d ' ')
            echo ""
            echo "EditMode: ${passed_count} tests passed"
            print_success "Unity EditMode Tests ($detected_version)"
        fi
    else
        print_failure "Unity EditMode Tests - 결과 파일 없음 (테스트가 실행되지 않았을 수 있음)"
        return 1
    fi
}

# 6.5 병렬 EditMode 테스트
run_parallel_editmode() {
    print_header "Parallel Unity EditMode Tests"

    local versions_to_test=()
    local pids=()
    local result_files=()
    local log_files=()

    # 설치된 버전 중 지원하는 버전 찾기
    local installed_versions=($(get_installed_unity_versions))

    for version in "${installed_versions[@]}"; do
        for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
            if [[ "$version" == "$pattern"* ]]; then
                local project_path=$(get_project_path_for_version "$pattern")
                if [ -d "$project_path" ]; then
                    # 중복 패턴 방지
                    local already_added=false
                    for existing in "${versions_to_test[@]}"; do
                        if [ "$existing" = "$pattern" ]; then
                            already_added=true
                            break
                        fi
                    done
                    if [ "$already_added" = false ]; then
                        versions_to_test+=("$pattern")
                    fi
                fi
                break
            fi
        done
    done

    if [ ${#versions_to_test[@]} -eq 0 ]; then
        print_failure "병렬 EditMode 테스트 - 테스트 가능한 버전 없음"
        return 1
    fi

    echo "Testing ${#versions_to_test[@]} versions in parallel:"
    for pattern in "${versions_to_test[@]}"; do
        echo "  • $pattern"
    done
    echo ""

    # 병렬 테스트 시작
    for pattern in "${versions_to_test[@]}"; do
        local unity_path=$(find_unity_by_pattern "$pattern")
        local project_path=$(get_project_path_for_version "$pattern")
        local results_file="$project_path/editmode-results.xml"
        local log_file="$project_path/editmode-test.log"

        echo -e "${CYAN}Starting EditMode tests for $pattern...${NC}"

        # 이전 결과 파일 정리
        rm -f "$results_file"

        # 백그라운드로 테스트 실행
        (
            "$unity_path" -batchmode -nographics \
                -projectPath "$project_path" \
                -runTests -testPlatform EditMode \
                -testResults "$results_file" \
                -logFile "$log_file" 2>&1 || true
        ) &

        pids+=($!)
        result_files+=("$results_file")
        log_files+=("$log_file")
    done

    echo ""
    echo "Waiting for ${#pids[@]} EditMode tests to complete..."
    echo ""

    # 완료 대기 및 결과 확인
    for i in "${!pids[@]}"; do
        local pid=${pids[$i]}
        local pattern=${versions_to_test[$i]}
        local results_file=${result_files[$i]}
        local log_file=${log_files[$i]}

        wait $pid

        if [ -f "$results_file" ]; then
            if grep -q 'result="Failed"' "$results_file"; then
                echo -e "${RED}✗${NC} $pattern EditMode tests failed"
                grep 'result="Failed"' "$results_file" | head -5
                echo "  Log: $log_file"
                EDITMODE_FAILED_VERSIONS+=("$pattern")
                ((FAILED++))
            else
                local passed_count=$(grep -o 'result="Passed"' "$results_file" | wc -l | tr -d ' ')
                echo -e "${GREEN}✓${NC} $pattern EditMode tests passed (${passed_count} tests)"
                ((PASSED++))
            fi
        else
            echo -e "${RED}✗${NC} $pattern EditMode tests - 결과 파일 없음"
            echo "  Log: $log_file"
            EDITMODE_FAILED_VERSIONS+=("$pattern")
            ((FAILED++))
        fi
    done
}
