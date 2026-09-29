#!/usr/bin/env bash
set -eo pipefail
#
# extract-deploy-url.test.sh - extract-deploy-url.sh 셸 테스트
#
# 입력 케이스는 Tests~/E2E/SharedScripts/Editor/EditModeTests/DeployUrlTests.cs의
# 6개 케이스와 같다. 케이스를 바꿀 때는 그 파일도 함께 갱신할 것.
#
# 실행:
#   bash .github/scripts/deploy/extract-deploy-url.test.sh   (직접, validate.yml에서 사용)
#   scripts~/test-validate.sh의 test_deploy_url_extraction   (run-local-tests.sh --validate)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HELPER="$SCRIPT_DIR/extract-deploy-url.sh"

PASSED=0
FAILED=0

assert_extract() {
  local name="$1"
  local input="$2"
  local expected="$3"
  local actual
  actual=$(printf '%s' "$input" | bash "$HELPER")

  if [ "$actual" = "$expected" ]; then
    echo "  PASS: ${name}"
    PASSED=$((PASSED + 1))
  else
    echo "  FAIL: ${name}"
    echo "    expected: ${expected}"
    echo "    actual:   ${actual}"
    FAILED=$((FAILED + 1))
  fi
}

echo "extract-deploy-url.sh 셸 테스트"

# 박스 래핑 접합 (DeployUrlTests.cs: ExtractDeployUrl_BoxWrappedUrl_JoinsContinuationLines)
BOX_WRAPPED_INPUT="╭──────────────────────────────────────────────────────────────────────────────╮
│  intoss-private://unity-sdk-sample?_deploymentId=01a01868-f10b-7279-b96f-ab  │
│  bcd6865b68  │
╰──────────────────────────────────────────────────────────────────────────────╯
"
assert_extract \
  "박스 래핑된 URL을 접합한다" \
  "$BOX_WRAPPED_INPUT" \
  'intoss-private://unity-sdk-sample?_deploymentId=01a01868-f10b-7279-b96f-abbcd6865b68&host=appsInTossHost'

# host 파라미터가 두 번째 줄로 래핑된 경우 (ExtractDeployUrl_HostParamWrappedToSecondLine_JoinsAndKeepsHost)
HOST_WRAPPED_INPUT="│  intoss-private://ait?_deploymentId=01a018fc-a0e5-7558-9a3a-166fcf  │
│  e4e4e1&host=appsInTossHost  │
"
assert_extract \
  "host 파라미터가 래핑돼도 접합 후 유지한다" \
  "$HOST_WRAPPED_INPUT" \
  'intoss-private://ait?_deploymentId=01a018fc-a0e5-7558-9a3a-166fcfe4e4e1&host=appsInTossHost'

# URL이 줄 끝까지 닿지 않으면(래핑 아님) 다음 줄 텍스트를 삼키지 않는다
# (ExtractDeployUrl_UnwrappedUrlFollowedByText_DoesNotSwallowNextLine)
UNWRAPPED_INPUT="│  intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec 완료  │
│  SUCCESS  │
"
assert_extract \
  "래핑이 아닌 URL 뒤 텍스트를 삼키지 않는다" \
  "$UNWRAPPED_INPUT" \
  'intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec&host=appsInTossHost'

# host가 이미 있으면 중복 부가하지 않는다 (ExtractDeployUrl_HostAlreadyPresent_DoesNotDuplicate)
assert_extract \
  "host가 이미 있으면 중복 부가하지 않는다" \
  "intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec&host=appsInTossHost
" \
  'intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec&host=appsInTossHost'

# 쿼리스트링이 없으면 ?host=로 부가한다 (ExtractDeployUrl_NoQueryString_AppendsHostWithQuestionMark)
assert_extract \
  "쿼리스트링이 없으면 ?host=로 부가한다" \
  "intoss-private://app
" \
  'intoss-private://app?host=appsInTossHost'

# URL이 없으면 빈 출력, 빈 입력이면 빈 출력 (ExtractDeployUrl_NoUrlInOutput_ReturnsNull)
assert_extract \
  "URL이 없으면 빈 출력" \
  "배포 완료. URL 없음.
" \
  ''

assert_extract \
  "빈 입력이면 빈 출력" \
  '' \
  ''

echo ""
echo "PASSED=${PASSED} FAILED=${FAILED}"

if [ "$FAILED" -gt 0 ]; then
  exit 1
fi
exit 0
