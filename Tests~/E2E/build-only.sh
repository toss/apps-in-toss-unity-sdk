#!/bin/bash

# Apps in Toss Unity SDK - C# 빌드 검증 스크립트
# Git clone 샘플 프로젝트 → SDK 추가 → Unity 빌드만 수행

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SDK_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEMP_DIR="$SCRIPT_DIR/temp"
PROJECT_PATH="$TEMP_DIR/apps-in-toss-unity-sdk-sample"
BUILD_PATH="$PROJECT_PATH/WebGLBuild"
BUILD_LOG="$SCRIPT_DIR/build.log"

# Load configuration from .env if exists
if [ -f "$SCRIPT_DIR/.env" ]; then
    export $(grep -v '^#' "$SCRIPT_DIR/.env" | xargs)
fi

# Set default repository URL if not provided in .env
# CI 환경에서는 HTTPS로, 로컬에서는 SSH로 기본 설정
if [ -n "$CI" ] || [ -n "$GITHUB_ACTIONS" ]; then
    # CI 환경: HTTPS 사용 (GITHUB_TOKEN 자동 인증)
    SAMPLE_REPO="${SAMPLE_REPO_URL:-https://github.toss.bz/toss/apps-in-toss-unity-sdk-sample.git}"
else
    # 로컬 환경: SSH 사용
    SAMPLE_REPO="${SAMPLE_REPO_URL:-git@github.toss.bz:toss/apps-in-toss-unity-sdk-sample.git}"
fi

# Unity 경로 자동 탐색 (2022.3.* 우선)
if [ -z "$UNITY_PATH" ]; then
    UNITY_PATH=$(ls -d /Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | head -1)
    if [ -z "$UNITY_PATH" ]; then
        UNITY_PATH=$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | head -1)
    fi
fi

if [ -z "$UNITY_PATH" ]; then
    echo "❌ Unity not found!" >&2
    echo "   Please install Unity 2022.3 LTS from Unity Hub" >&2
    exit 1
fi

echo "==========================================" >&2
echo "Apps in Toss SDK - C# Build Validation" >&2
echo "==========================================" >&2
echo "" >&2
echo "Unity: $(basename $(dirname $(dirname $(dirname $(dirname "$UNITY_PATH")))))" >&2
echo "SDK: $SDK_ROOT" >&2
echo "" >&2

# NODE_OPTIONS 제거 (Emscripten 충돌 방지)
unset NODE_OPTIONS

# 0. 기존 temp 디렉토리 정리
if [ -d "$TEMP_DIR" ]; then
    echo "[0/3] Cleaning previous build..." >&2
    rm -rf "$TEMP_DIR"
fi

mkdir -p "$TEMP_DIR"

# 1. 샘플 프로젝트 클론
echo "[1/3] Cloning sample project..." >&2
echo "Repository: $SAMPLE_REPO" >&2

cd "$TEMP_DIR"
git clone --depth 1 "$SAMPLE_REPO" 2>&1 | grep -v "^Cloning" || true

# 백업 폴더 삭제 (컴파일 충돌 방지)
if [ -d "$PROJECT_PATH" ]; then
    rm -rf "$PROJECT_PATH/Assets_backup_2021"
    rm -rf "$PROJECT_PATH/ProjectSettings_backup_2021"
fi

if [ ! -d "$PROJECT_PATH" ]; then
    echo "❌ Failed to get sample project!" >&2
    exit 1
fi

echo "✅ Sample project ready" >&2
echo "" >&2

# 2. SDK를 local package로 추가
echo "[2/3] Adding SDK as local package..." >&2

# manifest.json에 SDK 추가
cd "$PROJECT_PATH"
SDK_PACKAGE_PATH="file:$SDK_ROOT"

# jq가 있으면 사용, 없으면 수동으로 추가
if command -v jq &> /dev/null; then
    jq --arg path "$SDK_PACKAGE_PATH" '.dependencies += {"im.toss.apps-in-toss-unity-sdk": $path}' Packages/manifest.json > Packages/manifest.json.tmp
    mv Packages/manifest.json.tmp Packages/manifest.json
else
    # jq 없으면 수동 추가 (마지막 dependency 앞에 추가)
    sed -i.bak "s|\"com.unity.modules.ai\": \"1.0.0\"|\"im.toss.apps-in-toss-unity-sdk\": \"$SDK_PACKAGE_PATH\",\n    \"com.unity.modules.ai\": \"1.0.0\"|" Packages/manifest.json
    rm Packages/manifest.json.bak
fi

echo "✅ SDK added to manifest.json" >&2
echo "" >&2

# 3. Unity WebGL 빌드 실행
echo "[3/3] Building Unity WebGL with SDK..." >&2
echo "This will take 5-10 minutes. Validating C# compilation..." >&2
echo "" >&2

# 빌드 로그 초기화
> "$BUILD_LOG"

# Unity CLI 빌드 실행
"$UNITY_PATH" \
  -quit \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -executeMethod BuildAndRunBenchmark.CommandLineBuild \
  -logFile "$BUILD_LOG"

BUILD_EXIT_CODE=$?

echo "" >&2

# 빌드 결과 확인
if [ $BUILD_EXIT_CODE -ne 0 ]; then
    echo "❌ Build failed with exit code: $BUILD_EXIT_CODE" >&2
    echo "" >&2
    echo "Showing last 100 lines of build log:" >&2
    echo "======================================" >&2
    tail -100 "$BUILD_LOG" >&2
    echo "======================================" >&2
    echo "" >&2
    echo "Full log: $BUILD_LOG" >&2

    # 임시 파일 정리
    rm -rf "$TEMP_DIR"
    exit 1
fi

# 빌드 출력물 확인
if [ ! -f "$BUILD_PATH/index.html" ]; then
    echo "❌ Build completed but artifacts not found!" >&2
    echo "Expected: $BUILD_PATH/index.html" >&2
    echo "" >&2
    echo "Showing last 100 lines of build log:" >&2
    echo "======================================" >&2
    tail -100 "$BUILD_LOG" >&2
    echo "======================================" >&2
    echo "" >&2
    echo "Full log: $BUILD_LOG" >&2

    # 임시 파일 정리
    rm -rf "$TEMP_DIR"
    exit 1
fi

echo "✅ Build successful!" >&2
echo "" >&2

# 빌드 통계
BUILD_SIZE=$(du -sh "$BUILD_PATH" | awk '{print $1}')
echo "Build artifacts:" >&2
echo "  Location: $BUILD_PATH" >&2
echo "  Size: $BUILD_SIZE" >&2
echo "" >&2

# C# 컴파일 경고/에러 확인
ERRORS=$(grep -i "error CS[0-9]" "$BUILD_LOG" | wc -l | tr -d ' ')
WARNINGS=$(grep -i "warning CS[0-9]" "$BUILD_LOG" | wc -l | tr -d ' ')

if [ "$ERRORS" -gt 0 ]; then
    echo "⚠️  Found $ERRORS C# errors in build log" >&2
    echo "Showing C# errors:" >&2
    echo "======================================" >&2
    grep -i "error CS[0-9]" "$BUILD_LOG" | head -20 >&2
    echo "======================================" >&2
fi

if [ "$WARNINGS" -gt 0 ]; then
    echo "⚠️  Found $WARNINGS C# warnings in build log" >&2
    echo "First 10 warnings:" >&2
    echo "======================================" >&2
    grep -i "warning CS[0-9]" "$BUILD_LOG" | head -10 >&2
    echo "======================================" >&2
fi

if [ "$ERRORS" -eq 0 ] && [ "$WARNINGS" -eq 0 ]; then
    echo "✅ No C# compilation errors or warnings!" >&2
fi

echo "" >&2
echo "Full build log: $BUILD_LOG" >&2

# 임시 파일 정리
echo "" >&2
echo "Cleaning up temporary files..." >&2
rm -rf "$TEMP_DIR"

echo "✅ C# build validation complete!" >&2
