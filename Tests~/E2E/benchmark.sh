#!/bin/bash

# Apps in Toss Unity SDK - E2E 벤치마크 자동화 스크립트
# Git clone 샘플 프로젝트 → SDK 추가 → Unity 빌드 → Headless Chrome → 결과 출력

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
    SAMPLE_REPO="${SAMPLE_REPO_URL:-https://github.com/toss/apps-in-toss-unity-sdk-sample.git}"
else
    # 로컬 환경: SSH 사용
    SAMPLE_REPO="${SAMPLE_REPO_URL:-git@github.com:toss/apps-in-toss-unity-sdk-sample.git}"
fi

# Unity 경로 자동 탐색 (2022.3.* 우선)
if [ -z "$UNITY_PATH" ]; then
    UNITY_PATH=$(ls -d /Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | head -1)
    if [ -z "$UNITY_PATH" ]; then
        UNITY_PATH=$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | head -1)
    fi
fi

echo "==========================================" >&2
echo "Apps in Toss SDK E2E Benchmark" >&2
echo "==========================================" >&2
echo "" >&2

# NODE_OPTIONS 제거 (Emscripten 충돌 방지)
unset NODE_OPTIONS

# 0. 기존 temp 디렉토리 정리
if [ -d "$TEMP_DIR" ]; then
    echo "[0/4] Cleaning previous build..." >&2
    rm -rf "$TEMP_DIR"
fi

mkdir -p "$TEMP_DIR"

# 1. 샘플 프로젝트 클론
echo "[1/4] Cloning sample project..." >&2
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
echo "[2/4] Adding SDK as local package..." >&2

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
echo "[3/4] Building Unity WebGL with SDK..." >&2
echo "This may take 5-10 minutes. Please wait..." >&2
echo "" >&2

# Unity CLI 빌드 실행
"$UNITY_PATH" \
  -quit \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -executeMethod BuildAndRunBenchmark.CommandLineBuild \
  -logFile "$BUILD_LOG"

# 빌드 성공 확인
if [ ! -f "$BUILD_PATH/index.html" ]; then
    echo "❌ Build failed!" >&2
    echo "Check $BUILD_LOG for details" >&2
    tail -50 "$BUILD_LOG" >&2
    exit 1
fi

echo "✅ Build complete!" >&2
echo "" >&2

# 4. Python 서버 시작 + Headless Chrome
echo "[4/4] Running benchmark in headless Chrome..." >&2
echo "(Benchmark will run for ~70 seconds)" >&2
echo "" >&2

cd "$BUILD_PATH"

# Python 서버를 백그라운드로 실행
python3 "$SCRIPT_DIR/server.py" > /tmp/ait_benchmark_server.log 2>&1 &
SERVER_PID=$!

# 서버 시작 대기
sleep 3

echo "Server started on http://localhost:8000" >&2
echo "" >&2

# Chrome 경로 찾기
CHROME=""
if [ -f "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" ]; then
    CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
elif [ -f "/Applications/Chromium.app/Contents/MacOS/Chromium" ]; then
    CHROME="/Applications/Chromium.app/Contents/MacOS/Chromium"
else
    echo "❌ Chrome not found!" >&2
    echo "   Please install Google Chrome from:" >&2
    echo "   https://www.google.com/chrome/" >&2
    kill $SERVER_PID 2>/dev/null
    exit 1
fi

echo "Running headless Chrome..." >&2

# Headless Chrome 실행 (GPU 가속 활성화)
"$CHROME" \
  --headless=new \
  --no-sandbox \
  --disable-setuid-sandbox \
  --disable-dev-shm-usage \
  --enable-webgl \
  --use-gl=angle \
  --use-angle=default \
  --autoplay-policy=no-user-gesture-required \
  --window-size=1920,1080 \
  --user-data-dir=/tmp/ait-chrome-benchmark-profile \
  http://localhost:8000 > /dev/null 2>&1 &

CHROME_PID=$!

# 벤치마크 완료 대기
sleep 70

# Chrome 종료
kill $CHROME_PID 2>/dev/null || true

# 서버 종료 대기
sleep 3

# 서버 로그에서 결과 추출 (stdout에 JSON만 출력)
if [ -f /tmp/ait_benchmark_server.log ]; then
    cat /tmp/ait_benchmark_server.log
else
    echo "⚠️  No results received." >&2
    echo "   Check if the benchmark completed successfully." >&2
fi

# 서버 종료
kill $SERVER_PID 2>/dev/null || true

echo "" >&2
echo "✅ Benchmark complete!" >&2

# 임시 파일 정리
rm -f /tmp/ait_benchmark_server.log
rm -rf /tmp/ait-chrome-benchmark-profile
rm -rf "$TEMP_DIR"

echo "✅ Cleaned up temporary files" >&2
