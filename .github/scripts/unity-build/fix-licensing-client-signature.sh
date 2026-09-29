#!/usr/bin/env bash
set -e

UNITY_BASE="/Applications/Unity/Hub/Editor"
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

echo "Verifying code signature: ${TARGET_APP}"

# codesign 검증 — 정상이면 아무것도 하지 않음
if codesign --verify --deep --strict "$TARGET_APP" 2>/dev/null; then
  echo "Code signature is valid, no action needed"
  exit 0
fi

VERIFY_MSG=$(codesign --verify --deep --strict "$TARGET_APP" 2>&1 || true)
echo "::warning::LicensingClient code signature invalid: ${VERIFY_MSG}"

# 서명이 유효한 LicensingClient.app 찾기
# 탐색 순서: Unity Hub 내장 → 다른 Unity 버전
DONOR_APP=""
DONOR_VERSION=""

# 1) Unity Hub 내장 LicensingClient (Hub가 설치되어 있으면 항상 최신 서명)
for hub_candidate in \
  "/Applications/Unity Hub.app/Contents/Frameworks/UnityLicensingClient_V1.app" \
  "/Applications/Unity Hub.app/Contents/Frameworks/UnityLicensingClient.app"; do
  if [ -d "$hub_candidate" ] && codesign --verify --deep --strict "$hub_candidate" 2>/dev/null; then
    DONOR_APP="$hub_candidate"
    DONOR_VERSION="Unity Hub"
    break
  fi
done

# 2) 다른 Unity 버전
if [ -z "$DONOR_APP" ]; then
  for dir in $(ls -d "${UNITY_BASE}"/*/ 2>/dev/null | sort -rV); do
    version=$(basename "$dir")
    [ "$version" = "$CURRENT_VERSION" ] && continue

    for candidate in \
      "${dir}Unity.app/Contents/Helpers/UnityLicensingClient.app" \
      "${dir}Unity.app/Contents/Frameworks/UnityLicensingClient.app"; do
      if [ -d "$candidate" ] && codesign --verify --deep --strict "$candidate" 2>/dev/null; then
        DONOR_APP="$candidate"
        DONOR_VERSION="$version"
        break 2
      fi
    done
  done
fi

if [ -n "$DONOR_APP" ]; then
  DONOR_LC_VERSION=$("${DONOR_APP}/Contents/MacOS/Unity.Licensing.Client" --version 2>&1 || echo "unknown")
  TARGET_LC_VERSION=$("${TARGET_APP}/Contents/MacOS/Unity.Licensing.Client" --version 2>&1 || echo "unknown")
  echo "Replacing invalid LicensingClient (${TARGET_LC_VERSION}) with valid one from ${DONOR_VERSION} (${DONOR_LC_VERSION})"

  mv "$TARGET_APP" "${TARGET_APP}.bak"
  cp -R "$DONOR_APP" "$TARGET_APP"

  if codesign --verify --deep --strict "$TARGET_APP" 2>/dev/null; then
    echo "LicensingClient replaced and verified successfully"
    rm -rf "${TARGET_APP}.bak"
    exit 0
  else
    echo "Replacement also failed, restoring backup and trying re-sign"
    rm -rf "$TARGET_APP"
    mv "${TARGET_APP}.bak" "$TARGET_APP"
  fi
fi

# 다른 Unity 버전이 없거나 교체도 실패한 경우: ad-hoc 재서명
echo "Attempting ad-hoc re-sign of LicensingClient.app..."
codesign --force --deep --sign - "$TARGET_APP" 2>&1 || true

if codesign --verify --deep --strict "$TARGET_APP" 2>/dev/null; then
  echo "LicensingClient re-signed successfully"
else
  echo "::warning::LicensingClient re-sign failed, build may fail due to license issues"
fi
