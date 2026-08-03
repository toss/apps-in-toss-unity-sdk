#!/bin/bash
# scripts~/test-perf.sh — 무거운 픽스처 빌드(perf용) + TTFF 실측
# lib-globals.sh, lib-logging.sh, lib-unity-discovery.sh 이후에 source되어야 함

# 4.5 무거운 픽스처 WebGL 빌드 (perf 측정용)
# test_unity_build의 perf 변형: HeavySampleUnityProject + HeavyBuildRunner + 릴리즈/gzip.
test_heavy_build() {
    local version_pattern="${1:-$UNITY_VERSION}"

    print_header "Heavy WebGL Build (perf fixture)"

    local unity_path=$(find_unity_path "$version_pattern")
    if [ -z "$unity_path" ]; then
        if [ -n "$version_pattern" ]; then
            print_failure "Heavy WebGL Build - Unity $version_pattern 버전을 찾을 수 없음"
            echo ""
            echo "설치된 Unity 버전 확인:"
            list_unity_versions
        else
            print_skip "Heavy WebGL Build - Unity를 찾을 수 없음"
        fi
        return 1
    fi

    local detected_version=$(get_unity_version_from_path "$unity_path")
    local detected_pattern=$(get_version_pattern "$detected_version")
    local project_path=$(get_heavy_project_path_for_version "$detected_pattern")

    echo "Using Unity: $detected_version"
    echo "Path: $unity_path"
    echo "Heavy project: $(basename "$project_path")"

    if [ ! -d "$project_path" ]; then
        print_failure "Heavy WebGL Build - 무거운 프로젝트 없음: $(basename "$project_path")"
        echo "  (HeavySampleUnityProject는 2021.3 / 6000.0 / 6000.3 만 존재)"
        return 1
    fi

    local LOG_FILE="$project_path/unity-build.log"

    # 압축 포맷: 미지정 시 perf 기본값 gzip(1). on-wire 현실성을 위해 압축 ON.
    local compression_env="1"
    if [ -n "$COMPRESSION_FORMAT" ]; then
        compression_env=$(compression_format_to_env "$COMPRESSION_FORMAT")
    fi
    echo "Compression: AIT_COMPRESSION_FORMAT=$compression_env (perf 기본 gzip)"
    echo "Build flags: AIT_DEVELOPMENT_BUILD=false, AIT_IL2CPP_CONFIGURATION=Release"

    # 기존 빌드 정리 (Library는 패키지 캐시를 위해 유지)
    rm -rf "$project_path/ait-build"
    rm -rf "$project_path/Temp"

    echo "Building heavy WebGL..."
    echo "Log file: $LOG_FILE"

    # perf CI(perf.yml)와 동일한 환경: 릴리즈 빌드 + gzip + HeavyBuildRunner 진입점
    if env \
        AIT_DEBUG_CONSOLE=true \
        AIT_COMPRESSION_FORMAT="$compression_env" \
        AIT_DEVELOPMENT_BUILD=false \
        AIT_IL2CPP_CONFIGURATION=Release \
        "$unity_path" \
        -quit -batchmode -nographics \
        -projectPath "$project_path" \
        -executeMethod HeavyBuildRunner.CommandLineHeavyBuild \
        -logFile "$LOG_FILE"; then

        if [ -d "$project_path/ait-build/dist/web" ]; then
            print_success "Heavy WebGL Build ($detected_version)"
            echo "Build output: $project_path/ait-build/dist/web"
            du -sh "$project_path/ait-build/dist/web"
        else
            print_failure "Heavy WebGL Build - 결과물 없음"
            echo "Check log: $LOG_FILE"
            tail -50 "$LOG_FILE"
            return 1
        fi
    else
        print_failure "Heavy WebGL Build"
        echo "Check log: $LOG_FILE"
        tail -50 "$LOG_FILE"
        return 1
    fi
}

# 4.6 로딩 성능(TTFF) 실측 — test_e2e_playwright의 perf 변형 (pnpm run test:perf)
test_perf_playwright() {
    local version_pattern="$1"
    local project_path=""

    if [ -n "$version_pattern" ]; then
        project_path=$(get_heavy_project_path_for_version "$version_pattern")
        print_header "Perf TTFF Measurement ($version_pattern)"
    else
        # 자동 탐지: 무거운 빌드 결과물이 있는 첫 번째 프로젝트 사용
        for pattern in "${UNITY_VERSION_PATTERNS[@]}"; do
            local test_path=$(get_heavy_project_path_for_version "$pattern")
            if [ -d "$test_path/ait-build/dist/web" ]; then
                project_path="$test_path"
                version_pattern="$pattern"
                break
            fi
        done
        print_header "Perf TTFF Measurement"
    fi

    if [ -z "$project_path" ] || [ ! -d "$project_path/ait-build/dist/web" ]; then
        print_skip "Perf TTFF - 무거운 빌드 결과물 없음 (--heavy 먼저 실행)"
        return 0
    fi

    local short_version=$(echo "$version_pattern" | grep -oE '^[0-9]+\.[0-9]+')
    echo "Using heavy project: $(basename "$project_path")"

    cd "$SCRIPT_DIR/Tests~/E2E/tests"

    echo "Installing dependencies..."
    pnpm install --silent

    echo "Running perf (TTFF median-of-N)..."
    if env \
        UNITY_PROJECT_PATH="$project_path" \
        PERF_UNITY_VERSION="$short_version" \
        AIT_DEVELOPMENT_BUILD=false \
        AIT_COMPRESSION_FORMAT=1 \
        pnpm run test:perf; then
        print_success "Perf TTFF Measurement ($version_pattern)"

        local result_file="perf-results-${short_version}.json"
        if [ -f "$result_file" ]; then
            echo ""
            echo "📊 Perf Result ($result_file):"
            cat "$result_file" | head -40
        fi
    else
        print_failure "Perf TTFF Measurement ($version_pattern)"
        cd "$SCRIPT_DIR"
        return 1
    fi

    cd "$SCRIPT_DIR"
}
