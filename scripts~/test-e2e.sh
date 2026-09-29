#!/bin/bash
# scripts~/test-e2e.sh — Playwright E2E 테스트
# lib-globals.sh, lib-logging.sh, lib-unity-discovery.sh 이후에 source되어야 함

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

    # 빌드 단계에서 사용한 AIT_COMPRESSION_FORMAT을 test에도 전파 — test가 expectedFormat을 분기.
    local compression_env_for_test=$(compression_format_to_env "$COMPRESSION_FORMAT")
    if [ -n "$compression_env_for_test" ]; then
        echo "Expected compression: $COMPRESSION_FORMAT (AIT_COMPRESSION_FORMAT=$compression_env_for_test)"
    fi

    # 테스트 실행 시 프로젝트 경로를 환경변수로 전달
    echo "Running E2E tests..."
    local e2e_env="UNITY_PROJECT_PATH=$project_path"
    if [ -n "$compression_env_for_test" ]; then
        e2e_env="$e2e_env AIT_COMPRESSION_FORMAT=$compression_env_for_test"
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
