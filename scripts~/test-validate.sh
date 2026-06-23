#!/bin/bash
# scripts~/test-validate.sh — 파일 구조 검증, Playwright 설정, SDK 유닛 테스트, Meta GUID 위생
# lib-globals.sh, lib-logging.sh, lib-unity-discovery.sh 이후에 source되어야 함

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

    local invariants_passed=true

    # C# ↔ jslib 일관성 검증
    echo "Running unit tests (C# ↔ jslib invariants)..."
    if pnpm run test:invariants 2>&1; then
        echo -e "${GREEN}✓${NC} C# ↔ jslib invariants passed"
    else
        echo -e "${RED}✗${NC} C# ↔ jslib invariants failed"
        invariants_passed=false
    fi

    cd "$SCRIPT_DIR"

    if [ "$invariants_passed" = true ]; then
        print_success "SDK Generator Unit Tests"
        return 0
    else
        print_failure "SDK Generator Unit Tests"
        return 1
    fi
}

# 5.6 .meta GUID 위생 검사 (중복/손-작성 순차 GUID — techchat #4076 재발 방지)
test_meta_guids() {
    print_header "Meta GUID Hygiene"

    if ! command -v node >/dev/null 2>&1; then
        print_skip "Meta GUID Hygiene - node 없음"
        return 0
    fi

    if node "$SCRIPT_DIR/scripts~/check-meta-guids.js"; then
        print_success "Meta GUID Hygiene"
        return 0
    else
        print_failure "Meta GUID Hygiene"
        return 1
    fi
}
