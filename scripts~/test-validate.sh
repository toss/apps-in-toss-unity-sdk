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

    # IsKnownNonSdkMessageTests partial 분할 구조 확인
    echo ""
    echo "Checking IsKnownNonSdkMessageTests partial structure..."

    local ikns_dir="Tests~/E2E/SharedScripts/Editor/EditModeTests/ErrorTracker"
    local ikns_files=("$ikns_dir"/IsKnownNonSdkMessageTests*.cs)

    if [ ! -e "${ikns_files[0]}" ]; then
        echo -e "  ${YELLOW}!${NC} IsKnownNonSdkMessageTests*.cs not found — skip"
    else
        local fixture_count=0
        local testfixture_string_count=0
        local ikns_ok=true

        for f in "${ikns_files[@]}"; do
            if grep -qE '^\[TestFixture\]' "$f"; then
                fixture_count=$((fixture_count + 1))
            fi

            if grep -q 'TestFixture' "$f"; then
                testfixture_string_count=$((testfixture_string_count + 1))
            fi

            if ! grep -q 'partial class IsKnownNonSdkMessageTests' "$f"; then
                echo -e "    ${RED}✗${NC} $(basename "$f") missing 'partial class IsKnownNonSdkMessageTests' declaration"
                all_found=false
                ikns_ok=false
            fi

            if [ ! -f "$f.meta" ]; then
                echo -e "    ${RED}✗${NC} $(basename "$f").meta not found"
                all_found=false
                ikns_ok=false
            fi
        done

        if [ "$fixture_count" -ne 1 ]; then
            echo -e "    ${RED}✗${NC} exactly one IsKnownNonSdkMessageTests*.cs must carry [TestFixture] (found: $fixture_count)"
            all_found=false
            ikns_ok=false
        fi

        # 문자열 "TestFixture" 자체(주석 포함)는 본 파일에만 있어야 함 — partial 파일이 이 리터럴을
        # 담으면 grep -l '[TestFixture]' IsKnownNonSdkMessageTests*.cs 가 여러 파일을 잘못 반환한다.
        if [ "$testfixture_string_count" -ne 1 ]; then
            echo -e "    ${RED}✗${NC} literal string 'TestFixture' must appear in exactly one file (found in: $testfixture_string_count)"
            all_found=false
            ikns_ok=false
        fi

        # partial 파일에는 클래스 어트리뷰트([Category(...)] 등)를 달지 않는다
        for f in "${ikns_files[@]}"; do
            if [[ "$(basename "$f")" != "IsKnownNonSdkMessageTests.cs" ]] && grep -qE '^\[Category\(' "$f"; then
                echo -e "    ${RED}✗${NC} $(basename "$f") must not carry a class-level [Category(...)] attribute"
                all_found=false
                ikns_ok=false
            fi
        done

        # GUID 유일성 (meta 파일)
        local guid_dupes=$(grep -h '^guid:' "$ikns_dir"/IsKnownNonSdkMessageTests*.cs.meta 2>/dev/null | sort | uniq -d)
        if [ -n "$guid_dupes" ]; then
            echo -e "    ${RED}✗${NC} duplicate .meta GUID(s) found among IsKnownNonSdkMessageTests*.cs.meta"
            all_found=false
            ikns_ok=false
        fi

        if [ "$ikns_ok" = true ] && [ "$fixture_count" -eq 1 ] && [ "$testfixture_string_count" -eq 1 ]; then
            echo -e "  ${GREEN}✓${NC} IsKnownNonSdkMessageTests partial structure (${#ikns_files[@]} files, 1 [TestFixture], all partial + .meta, distinct GUIDs)"
        fi
    fi

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

# 5.7 배포 URL 추출 셸 헬퍼 테스트 (AITDeployManager.ExtractDeployUrl과 케이스 동기화)
test_deploy_url_extraction() {
    print_header "Deploy URL Extraction"

    if bash "$SCRIPT_DIR/.github/scripts/deploy/extract-deploy-url.test.sh"; then
        print_success "Deploy URL Extraction"
        return 0
    else
        print_failure "Deploy URL Extraction"
        return 1
    fi
}
