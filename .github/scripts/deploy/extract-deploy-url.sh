#!/usr/bin/env bash
set -e
#
# extract-deploy-url.sh - ait deploy 표준출력에서 intoss-private:// 배포 URL 추출
#
# stdin으로 ait deploy 출력을 받아 URL 한 줄을 stdout에 출력한다. URL을 찾지
# 못하면 빈 출력으로 exit 0 — 호출자는 이 경우 자신의 폴백(https URL, 'SUCCESS'
# 등)으로 넘어간다.
#
# 처리 순서: ANSI 이스케이프 제거 -> \r 제거 -> 박스 문자(│) 제거 -> 앞뒤 공백
# trim -> intoss-private:// URL 추출(박스 폭 래핑 접합 포함) -> host 파라미터
# 멱등 부가.
#
# ait CLI는 URL을 고정폭 박스(│ ... │) 안에 출력하므로 긴 URL(예: UUID
# deploymentId)은 여러 줄로 래핑된다. 줄 단위 grep은 URL을 중간에서 자르므로,
# 박스 문자·여백 제거 후 줄 끝까지 이어지는 URL을 연속 줄과 접합해 복원한다.
#
# 이 스크립트는 Editor/Menu/AITDeployManager.cs의 ExtractDeployUrl과 같은
# 의미론(문자 클래스, 래핑 판정, host 멱등)을 유지해야 한다. 케이스를 바꿀
# 때는 extract-deploy-url.test.sh와
# Tests~/E2E/SharedScripts/Editor/EditModeTests/DeployUrlTests.cs를 함께 갱신할 것.

CLEANED=$(cat | sed 's/\x1b\[[0-9;]*m//g' | tr -d '\r' | sed 's/│//g; s/^[[:space:]]*//; s/[[:space:]]*$//')

DEPLOY_URL=$(printf '%s\n' "$CLEANED" | awk '
  !url && match($0, /intoss-private:\/\/[A-Za-z0-9._~%=&?\/-]+/) {
    url = substr($0, RSTART, RLENGTH)
    wrapped = (RSTART + RLENGTH - 1 == length($0))
    next
  }
  wrapped && /^[A-Za-z0-9._~%=&?\/-]+$/ { url = url $0; next }
  { wrapped = 0 }
  END { if (url) print url }')

# SDK 3.0(V3 host) 딥링크는 host 파라미터 필수 — V3로 출시된 적 없는 스킴은
# CDN에 deployment.json이 없어 host 없이는 진입 불가. CLI가 이미 붙였으면 유지.
if [ -n "$DEPLOY_URL" ] && ! printf '%s' "$DEPLOY_URL" | grep -qE '[?&]host='; then
  case "$DEPLOY_URL" in
    *\?*) DEPLOY_URL="${DEPLOY_URL}&host=appsInTossHost" ;;
    *)    DEPLOY_URL="${DEPLOY_URL}?host=appsInTossHost" ;;
  esac
fi

printf '%s\n' "$DEPLOY_URL"
