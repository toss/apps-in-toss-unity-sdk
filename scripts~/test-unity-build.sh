#!/bin/bash
# scripts~/test-unity-build.sh — Unity WebGL 빌드 (단일 / 병렬)
# lib-globals.sh, lib-logging.sh, lib-unity-discovery.sh 이후에 source되어야 함

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
    local compression_env=$(compression_format_to_env "$COMPRESSION_FORMAT")
    if [ -n "$compression_env" ]; then
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

# 7a. 병렬 Unity 빌드만 (E2E 테스트 없음)
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
                # EditMode 실패 버전 스킵
                local editmode_failed=false
                for failed in "${EDITMODE_FAILED_VERSIONS[@]}"; do
                    if [ "$failed" = "$pattern" ]; then
                        editmode_failed=true
                        break
                    fi
                done
                if [ "$editmode_failed" = true ]; then
                    echo -e "${YELLOW}⊘${NC} $pattern 건너뜀 (EditMode 테스트 실패)"
                    break
                fi

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

    # 압축 포맷 환경변수 (병렬 빌드에서 공통으로 사용)
    local compression_env=$(compression_format_to_env "$COMPRESSION_FORMAT")

    # 병렬 빌드 시작
    for pattern in "${versions_to_build[@]}"; do
        local unity_path=$(find_unity_by_pattern "$pattern")
        local project_path=$(get_project_path_for_version "$pattern")
        local log_file="$project_path/unity-build.log"

        echo -e "${CYAN}Starting build for $pattern...${NC}"

        # 빌드 정리
        rm -rf "$project_path/ait-build"
        rm -rf "$project_path/Temp"

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

# 7b. 병렬 빌드 및 테스트 (전체)
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
                # EditMode 실패 버전 스킵
                local editmode_failed=false
                for failed in "${EDITMODE_FAILED_VERSIONS[@]}"; do
                    if [ "$failed" = "$pattern" ]; then
                        editmode_failed=true
                        break
                    fi
                done
                if [ "$editmode_failed" = true ]; then
                    echo -e "${YELLOW}⊘${NC} $pattern 건너뜀 (EditMode 테스트 실패)"
                    break
                fi

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

    # 압축 포맷 환경변수 (병렬 빌드에서 공통으로 사용)
    local compression_env=$(compression_format_to_env "$COMPRESSION_FORMAT")

    # 병렬 빌드 시작
    for pattern in "${versions_to_build[@]}"; do
        local unity_path=$(find_unity_by_pattern "$pattern")
        local project_path=$(get_project_path_for_version "$pattern")
        local log_file="$project_path/unity-build.log"

        echo -e "${CYAN}Starting build for $pattern...${NC}"

        # 빌드 정리
        rm -rf "$project_path/ait-build"
        rm -rf "$project_path/Temp"

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
