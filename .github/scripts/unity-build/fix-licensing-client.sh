#!/usr/bin/env bash
set -e

# macOS self-hosted 러너는 버전별 라벨(unity-2021.3 등)이 붙어있지만 실제로는
# 여러 라벨이 한 물리 머신을 공유하고, Unity 라이선싱 IPC 채널
# ("LicenseClient-<host>")도 그 머신에 하나뿐이다. 어떤 잡이든 먼저 뜬 에디터가
# 채널을 만들면 이후 잡은 자기 번들 클라이언트를 새로 띄우지 않고 기존 채널에
# 붙는데, 그 채널을 처음 만든 클라이언트가 오래된 버전(예 v1.15.x)이면
# entitlement 조회가 비어("Found 0 entitlement groups") 라이선스 인증에
# 실패한다. 그래서 서명이 유효하더라도 "이 머신에서 구할 수 있는 가장 최신
# 클라이언트보다 오래된" 번들 클라이언트는 최신으로 교체해 항상 최신
# 핸드셰이크 프로토콜로 채널을 만들도록 한다.
#
# 오버라이드 가능한 환경변수(로컬 단위테스트용): UNITY_BASE, UNITY_HUB_APP

UNITY_BASE="${UNITY_BASE:-/Applications/Unity/Hub/Editor}"
UNITY_HUB_APP="${UNITY_HUB_APP:-/Applications/Unity Hub.app}"
CURRENT_VERSION="$UNITY_VERSION_FULL"
CURRENT_DIR="${UNITY_BASE}/${CURRENT_VERSION}"

# LicensingClient.app 경로 탐색 (버전별로 위치가 다를 수 있음)
TARGET_APP=""
for candidate in \
  "${CURRENT_DIR}/Unity.app/Contents/Frameworks/UnityLicensingClient.app" \
  "${CURRENT_DIR}/Unity.app/Contents/Helpers/UnityLicensingClient.app"; do
  if [ -d "$candidate" ]; then
    TARGET_APP="$candidate"
    break
  fi
done

if [ -z "$TARGET_APP" ]; then
  echo "LicensingClient.app not found, skipping"
  exit 0
fi

# bundle의 버전 문자열을 최대한 견고하게 읽는다.
# 1) Info.plist의 CFBundleShortVersionString (우선), 없으면 CFBundleVersion
# 2) 그마저 없으면 실행 파일의 --version 출력에서 x.y.z 패턴을 정규식으로 추출
# 버전을 못 구하면 빈 문자열을 반환 — 호출부는 이를 "버전 근거 교체 불가"로 처리한다.
read_lc_version() {
  local app="$1"
  local plist="${app}/Contents/Info.plist"
  local version=""

  if [ -f "$plist" ]; then
    version=$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' "$plist" 2>/dev/null || true)
    if [ -z "$version" ]; then
      version=$(/usr/libexec/PlistBuddy -c 'Print CFBundleVersion' "$plist" 2>/dev/null || true)
    fi
  fi

  if [ -z "$version" ]; then
    local bin="${app}/Contents/MacOS/Unity.Licensing.Client"
    if [ -x "$bin" ]; then
      version=$("$bin" --version 2>&1 | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || true)
    fi
  fi

  printf '%s' "$version"
}

# a가 b보다 낮은 버전이면 성공(0)을 반환. 둘 중 하나라도 비어있으면 실패(1) —
# 호출부에서 항상 두 값이 채워졌는지 먼저 확인할 것.
version_lt() {
  local a="$1" b="$2"
  [ -n "$a" ] && [ -n "$b" ] || return 1
  [ "$a" = "$b" ] && return 1
  [ "$(printf '%s\n%s\n' "$a" "$b" | sort -V | head -1)" = "$a" ]
}

TARGET_SIG_VALID=false
if codesign --verify --deep --strict "$TARGET_APP" 2>/dev/null; then
  TARGET_SIG_VALID=true
fi
TARGET_LC_VERSION=$(read_lc_version "$TARGET_APP")

echo "Current editor LicensingClient: ${TARGET_APP}"
echo "  signature valid: ${TARGET_SIG_VALID}"
echo "  version: ${TARGET_LC_VERSION:-unknown}"

if [ "$TARGET_SIG_VALID" = false ]; then
  VERIFY_MSG=$(codesign --verify --deep --strict "$TARGET_APP" 2>&1 || true)
  echo "::warning::LicensingClient code signature invalid: ${VERIFY_MSG}"
fi

# 이 머신에서 서명이 유효한 후보 중 가장 최신 버전을 찾는다.
# 탐색 순서(동률일 때의 우선순위): Unity Hub 내장 → 다른 Unity 에디터 버전(최신순).
BEST_APP=""
BEST_VERSION=""
BEST_SOURCE=""

consider_candidate() {
  local app="$1" source="$2"
  [ -d "$app" ] || return 0
  codesign --verify --deep --strict "$app" 2>/dev/null || return 0

  local version
  version=$(read_lc_version "$app")

  if [ -z "$BEST_VERSION" ]; then
    BEST_APP="$app"
    BEST_VERSION="$version"
    BEST_SOURCE="$source"
    return 0
  fi

  # 후보 버전을 알 수 없으면(비교 불가) 현재 BEST를 그대로 유지 — 알 수 없는
  # 버전으로 "더 낫다"고 섣불리 판단하지 않는다.
  [ -n "$version" ] || return 0
  [ -n "$BEST_VERSION" ] || { BEST_APP="$app"; BEST_VERSION="$version"; BEST_SOURCE="$source"; return 0; }

  if version_lt "$BEST_VERSION" "$version"; then
    BEST_APP="$app"
    BEST_VERSION="$version"
    BEST_SOURCE="$source"
  fi
}

for hub_candidate in \
  "${UNITY_HUB_APP}/Contents/Frameworks/UnityLicensingClient_V1.app" \
  "${UNITY_HUB_APP}/Contents/Frameworks/UnityLicensingClient.app"; do
  consider_candidate "$hub_candidate" "Unity Hub"
done

if [ -d "$UNITY_BASE" ]; then
  for dir in $(ls -d "${UNITY_BASE}"/*/ 2>/dev/null | sort -rV); do
    version_dir=$(basename "$dir")
    [ "$version_dir" = "$CURRENT_VERSION" ] && continue

    for candidate in \
      "${dir}Unity.app/Contents/Frameworks/UnityLicensingClient.app" \
      "${dir}Unity.app/Contents/Helpers/UnityLicensingClient.app"; do
      consider_candidate "$candidate" "Unity ${version_dir}"
    done
  done
fi

if [ -n "$BEST_APP" ]; then
  echo "Best available signed LicensingClient: ${BEST_APP} (source: ${BEST_SOURCE}, version: ${BEST_VERSION:-unknown})"
else
  echo "No alternative signed LicensingClient found on this machine"
fi

# 교체 여부 판단
SHOULD_REPLACE=false
REPLACE_REASON=""

if [ "$TARGET_SIG_VALID" = false ] && [ -n "$BEST_APP" ]; then
  SHOULD_REPLACE=true
  REPLACE_REASON="현재 클라이언트 서명이 무효함"
elif [ "$TARGET_SIG_VALID" = true ] && [ -n "$BEST_APP" ]; then
  if [ -n "$TARGET_LC_VERSION" ] && [ -n "$BEST_VERSION" ]; then
    if version_lt "$TARGET_LC_VERSION" "$BEST_VERSION"; then
      SHOULD_REPLACE=true
      REPLACE_REASON="현재 클라이언트(${TARGET_LC_VERSION})가 이 머신의 최신 서명된 클라이언트(${BEST_VERSION})보다 오래됨"
    fi
  else
    echo "::warning::LicensingClient 버전을 확인할 수 없어 버전 비교로는 교체하지 않습니다 (현재: ${TARGET_LC_VERSION:-unknown}, 후보: ${BEST_VERSION:-unknown})"
  fi
fi

if [ "$SHOULD_REPLACE" = true ]; then
  echo "Replacing LicensingClient (${TARGET_LC_VERSION:-unknown}) with ${BEST_SOURCE} (${BEST_VERSION:-unknown}) — reason: ${REPLACE_REASON}"

  mv "$TARGET_APP" "${TARGET_APP}.bak"
  cp -R "$BEST_APP" "$TARGET_APP"

  if codesign --verify --deep --strict "$TARGET_APP" 2>/dev/null; then
    echo "LicensingClient replaced and verified successfully"
    rm -rf "${TARGET_APP}.bak"
    exit 0
  else
    echo "Replacement also failed signature verification, restoring backup"
    rm -rf "$TARGET_APP"
    mv "${TARGET_APP}.bak" "$TARGET_APP"
  fi
fi

if [ "$TARGET_SIG_VALID" = true ] && [ "$SHOULD_REPLACE" = false ]; then
  echo "Current LicensingClient signature is valid and no newer signed candidate found, no action needed"
  exit 0
fi

# 여기 도달하는 경우: 서명이 무효인데 교체할 대안도 없거나 교체가 실패한 경우.
# ad-hoc 재서명을 최후 수단으로 시도한다.
echo "Attempting ad-hoc re-sign of LicensingClient.app..."
codesign --force --deep --sign - "$TARGET_APP" 2>&1 || true

if codesign --verify --deep --strict "$TARGET_APP" 2>/dev/null; then
  echo "LicensingClient re-signed successfully"
else
  echo "::warning::LicensingClient re-sign failed, build may fail due to license issues"
fi
