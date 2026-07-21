#!/bin/bash
#
# GitHub Actions 테스트를 로컬에서 실행하는 스크립트
# 사용법: ./run-local-tests.sh [옵션]
#
# 옵션:
#   --all                    모든 테스트 실행 (Unity 빌드 포함)
#   --quick                  빠른 테스트만 (E2E validation)
#   --editmode               Unity EditMode 테스트 실행 (~10초)
#   --e2e                    E2E 테스트만 (빌드 결과물 필요)
#   --unity-build            Unity WebGL 빌드 실행
#   --heavy                  무거운 픽스처(HeavySampleUnityProject) 릴리즈+gzip 빌드 (perf용)
#   --perf                   로딩 성능(TTFF) 실측 (무거운 빌드 결과물 필요)
#   --unity-version <버전>   특정 Unity 버전 지정 (예: 2022.3, 6000.0)
#   --compression <format>   압축 포맷 지정 (auto, disabled, gzip, brotli)
#   --parallel               다른 모드와 조합하여 병렬 실행 (예: --unity-build --parallel)
#   --list-unity             설치된 Unity 버전 목록 표시
#   --help                   도움말
#

# set -e 제거 - 각 테스트 함수에서 직접 에러 처리

# NODE_OPTIONS 환경변수 제거 (문제 유발 가능)
unset NODE_OPTIONS 2>/dev/null || true

# 프로젝트 루트 디렉토리 (모든 모듈이 공유)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 모듈 로드 (의존성 순서대로)
# shellcheck source=scripts~/lib-globals.sh
source "$SCRIPT_DIR/scripts~/lib-globals.sh"
# shellcheck source=scripts~/lib-logging.sh
source "$SCRIPT_DIR/scripts~/lib-logging.sh"
# shellcheck source=scripts~/lib-unity-discovery.sh
source "$SCRIPT_DIR/scripts~/lib-unity-discovery.sh"
# shellcheck source=scripts~/test-validate.sh
source "$SCRIPT_DIR/scripts~/test-validate.sh"
# shellcheck source=scripts~/test-editmode.sh
source "$SCRIPT_DIR/scripts~/test-editmode.sh"
# shellcheck source=scripts~/test-e2e.sh
source "$SCRIPT_DIR/scripts~/test-e2e.sh"
# shellcheck source=scripts~/test-unity-build.sh
source "$SCRIPT_DIR/scripts~/test-unity-build.sh"
# shellcheck source=scripts~/test-perf.sh
source "$SCRIPT_DIR/scripts~/test-perf.sh"

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
            --heavy)
                RUN_HEAVY=true
                # --heavy/--perf는 조합 가능한 플래그 (--heavy --perf = 빌드 후 측정)
                ;;
            --perf)
                RUN_PERF=true
                ;;
            --all|--e2e|--editmode|--unity-build|--validate)
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

    # --heavy / --perf 는 일반 모드와 독립적인 perf 하네스 경로.
    # 둘 중 하나라도 켜져 있으면 perf 흐름을 타고 종료(일반 mode case 무시).
    if [ "$RUN_HEAVY" = true ] || [ "$RUN_PERF" = true ]; then
        echo ""
        echo "╔══════════════════════════════════════════════════════════════════════════╗"
        echo "║           Apps in Toss Unity SDK - Perf Harness (TTFF)                  ║"
        echo "╚══════════════════════════════════════════════════════════════════════════╝"
        echo ""
        [ "$RUN_HEAVY" = true ] && echo "  • Heavy build: HeavyBuildRunner.CommandLineHeavyBuild (release + gzip)"
        [ "$RUN_PERF" = true ]  && echo "  • Perf measure: pnpm run test:perf (TTFF median-of-N)"
        if [ -n "$UNITY_VERSION" ]; then
            echo "  • Unity Version: $UNITY_VERSION"
        fi
        echo "  • Directory: $SCRIPT_DIR"

        local heavy_ok=true
        if [ "$RUN_HEAVY" = true ]; then
            if ! test_heavy_build "$UNITY_VERSION"; then
                heavy_ok=false
                print_info "무거운 빌드 실패 — perf 측정을 건너뜁니다"
            fi
        fi

        if [ "$RUN_PERF" = true ] && [ "$heavy_ok" = true ]; then
            test_perf_playwright "$UNITY_VERSION"
        fi

        print_summary
        return $?
    fi

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
                run_parallel_editmode
                run_parallel_builds
            else
                if test_editmode "$UNITY_VERSION"; then
                    test_unity_build "$UNITY_VERSION"
                    test_e2e_playwright "$(get_version_pattern "$(get_unity_version_from_path "$(find_unity_path "$UNITY_VERSION")")")"
                else
                    print_info "EditMode 테스트 실패로 Unity 빌드 및 E2E 테스트를 건너뜁니다"
                fi
            fi
            ;;
        --editmode)
            if [ "$PARALLEL_MODE" = true ]; then
                run_parallel_editmode
            else
                test_editmode "$UNITY_VERSION"
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
            test_meta_guids
            ;;
    esac

    print_summary
}

# 실행
main "$@"
