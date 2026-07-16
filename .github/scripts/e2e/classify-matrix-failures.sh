#!/usr/bin/env bash
set -eo pipefail

set +e
# 매트릭스 잡별 결과 분류기.
#
# 카테고리:
#   passed                          모든 EditMode 테스트 통과
#   code                            EditMode 테스트 실패 (이름 추출)
#   results-missing                 editmode-results.xml 없음 — 인프라 결함 의심
#   build-no-output                 빌드 산출물 없음 — Brotli/디스크 의심
#   unknown                         분류 불가
#
# results-missing/build-no-output/unknown은 `unity-log-*` 아티팩트를 grep해
# infra:* 서브카테고리로 세분화한다 (라이선스/Hub/캐시/Brotli/디스크 등).
#
# 매트릭스를 명시적으로 enumerate해서 아티팩트 자체가 없는 잡(예: setup 직후
# 라이선스 실패)도 표에 노출되게 한다.
mkdir -p classification
rows=""

# OS × Unity 버전(+레그) 조합 — build/e2e-{macos,windows} 잡의 matrix와 동일하게 유지.
# 레그 접미사가 있는 항목은 version 자리에 "버전<leg-suffix>"를 그대로 쓴다
# (아티팩트 이름이 editmode-results-macos-2022.3-brotli 형태이므로 조회가 일치).
declare -a TARGETS=(
  "macos:2021.3"
  "macos:2022.3"
  "macos:2022.3-brotli"
  "macos:6000.0"
  "macos:6000.2"
  "macos:6000.3"
  "windows:2021.3"
  "windows:2022.3"
  "windows:6000.0"
  "windows:6000.2"
  "windows:6000.3"
)

# 인프라 서브카테고리 grep 패턴 (첫 매치가 detail에 노출됨).
# 패턴 추가 시 Documentation~/internal/github-actions.md에도 반영을 권장.
classify_infra() {
  local logs_glob="$1"
  local hit_subcat="" hit_line=""
  local file
  for file in $logs_glob; do
    [ -f "$file" ] || continue

    # 패키지 의존성 해결 실패 (git 누락 등) — 라이선스 자가복구 후에도 종료시키는 흔한 원인
    hit_line=$(grep -m1 -E "No '?git'? executable|Project has invalid dependencies|Failed to resolve packages|Cannot fetch.*git" "$file" 2>/dev/null)
    if [ -n "$hit_line" ]; then
      hit_subcat="infra:package-resolution"
      printf '%s\t%s\n' "$hit_subcat" "$hit_line"
      return 0
    fi
    # Unity Hub 부재/실행 실패
    hit_line=$(grep -m1 -E 'Unity Hub not found|hub: command not found' "$file" 2>/dev/null)
    if [ -n "$hit_line" ]; then
      hit_subcat="infra:hub-missing"
      printf '%s\t%s\n' "$hit_subcat" "$hit_line"
      return 0
    fi
    # Brotli 압축 크래시 (self-hosted runner 동시 빌드 리소스 경합)
    hit_line=$(grep -m1 -E '\[BUSY [0-9]+s\] Brotli .*\.unityweb' "$file" 2>/dev/null)
    if [ -n "$hit_line" ]; then
      hit_subcat="infra:brotli-crash"
      printf '%s\t%s\n' "$hit_subcat" "$hit_line"
      return 0
    fi
    # 디스크 풀
    hit_line=$(grep -m1 -E 'No space left on device|disk full' "$file" 2>/dev/null)
    if [ -n "$hit_line" ]; then
      hit_subcat="infra:disk-full"
      printf '%s\t%s\n' "$hit_subcat" "$hit_line"
      return 0
    fi
    # Library 잠금 (이전 잡 잔여 프로세스)
    hit_line=$(grep -m1 -E 'Library directory is locked|Lockfile.*Library' "$file" 2>/dev/null)
    if [ -n "$hit_line" ]; then
      hit_subcat="infra:library-locked"
      printf '%s\t%s\n' "$hit_subcat" "$hit_line"
      return 0
    fi
    # 라이선스 만료/시트 부재 — 자가복구 시 false positive를 일으키므로 마지막에 검사하고
    # 같은 로그 안에서 라이선스가 정상 발급된 흔적이 있으면 무시한다.
    # 표면 증상이 다양해도(ULF 부재 / 토큰 부재 / entitlement 0개 / Pro 모듈 미부여)
    # 운영상 해결책은 동일(ULF 갱신)이므로 한 카테고리로 묶는다.
    hit_line=$(grep -m1 -E "No ULF license found|Access token is unavailable|Token not found in cache|Found 0 entitlement groups|'com\.unity\.editor\.[a-z]+' was not found" "$file" 2>/dev/null)
    if [ -n "$hit_line" ]; then
      if grep -qE 'Successfully updated license|Serial number assigned to' "$file"; then
        : # 라이선스가 자가복구되었으므로 진짜 원인이 아님
      else
        hit_subcat="infra:license-token-expired"
        printf '%s\t%s\n' "$hit_subcat" "$hit_line"
        return 0
      fi
    fi
  done
  return 1
}

for target in "${TARGETS[@]}"; do
  os="${target%%:*}"
  version="${target##*:}"
  results_file="artifacts/editmode-results-${os}-${version}/editmode-results.xml"
  category="unknown"
  subcategory=""
  detail=""
  artifact="absent"

  if [ -f "$results_file" ]; then
    artifact="present"
    if grep -q '<test-case [^>]*result="Failed"' "$results_file"; then
      category="code"
      failed_names=$(grep -oE '<test-case [^>]*fullname="[^"]*"[^>]*result="Failed"' "$results_file" \
        | grep -oE 'fullname="[^"]*"' \
        | sed 's/fullname="//; s/"$//' \
        | sort -u)
      count=$(printf '%s\n' "$failed_names" | grep -c .)
      first=$(printf '%s\n' "$failed_names" | head -1)
      if [ "$count" -le 1 ]; then
        detail="failed test: ${first}"
      else
        detail="failed tests (${count}): ${first} +$((count-1)) more"
      fi
    else
      category="passed"
      detail="all editmode tests passed"
    fi
  else
    # 결과 파일이 없는 두 가지 가능성:
    #   1. EditMode 단계가 results-missing으로 실패 (라이선스/Hub/캐시 등)
    #   2. 빌드 단계에서 Brotli/디스크 등으로 실패 → editmode 도달 못함
    # 빌드 산출물 디렉토리가 있는데 파일이 없는 경우는 build-no-output.
    build_artifact_dir=$(ls -d "artifacts/ait-build-${os}-${version}"* 2>/dev/null | head -1)
    if [ -n "$build_artifact_dir" ] && [ -d "$build_artifact_dir" ]; then
      # 압축 레그(brotli/gzip)는 .unityweb 대신 .br/.gz를 산출하므로 함께 센다.
      unityweb_count=$(find "$build_artifact_dir" -type f \( -name '*.unityweb' -o -name '*.br' -o -name '*.gz' \) 2>/dev/null | wc -l | tr -d ' ')
      if [ "$unityweb_count" = "0" ]; then
        category="build-no-output"
        detail="no .unityweb files in build artifact (compression failure suspected)"
      else
        category="results-missing"
        detail="editmode-results.xml not produced (license/setup/crash suspected)"
      fi
    else
      category="results-missing"
      detail="editmode-results.xml not produced (license/setup/crash suspected)"
    fi
  fi

  # 인프라 서브카테고리: 결과가 코드/통과가 아닐 때만 시도.
  if [ "$category" != "passed" ] && [ "$category" != "code" ]; then
    shopt -s nullglob
    # build → unit-test → editmode 순으로 grep (인프라 결함은 보통 빌드/EditMode에서 먼저 노출).
    for kind in build unit-test editmode; do
      infra_match=$(classify_infra "artifacts/unity-log-${kind}-${os}-${version}/*.log" 2>/dev/null)
      if [ -n "$infra_match" ]; then
        subcategory="${infra_match%%	*}"
        hit="${infra_match#*	}"
        # detail에 매칭 라인 한 줄 첨부 (너무 길면 80자로 자름).
        # 마크다운 표 셀이 깨지지 않도록 `|`는 ⎮(U+23AE)로 치환.
        hit_short="${hit:0:80}"
        hit_safe="${hit_short//|/⎮}"
        detail_safe="${detail//|/⎮}"
        detail="${detail_safe} — ${subcategory}: ${hit_safe}"
        break
      fi
    done
    shopt -u nullglob
  fi

  display_category="${category}"
  if [ -n "$subcategory" ]; then
    display_category="${category} → ${subcategory}"
  fi

  rows+="| ${os} | ${version} | \`${display_category}\` | \`${artifact}\` | ${detail:-_(none)_} |"$'\n'
  echo "::notice title=Failure Classification (${os}, ${version})::category=${category} subcategory=${subcategory:-none} artifact=${artifact} detail=${detail}"
done

if [ -z "$rows" ]; then
  echo "has_classification=false" >> "$GITHUB_OUTPUT"
  echo "분류 가능한 매트릭스 결과 없음"
  exit 0
fi

{
  echo "### Failure Classification"
  echo ""
  echo "| OS | Unity | category | results | detail |"
  echo "|---|---|---|---|---|"
  printf '%s' "$rows"
} > classification/classification.md

cat classification/classification.md >> "$GITHUB_STEP_SUMMARY"
echo "has_classification=true" >> "$GITHUB_OUTPUT"

