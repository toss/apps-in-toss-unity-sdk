#!/bin/bash

# Unity WebGL 벤치마크 - 완전 자동화 스크립트 (Headless Browser)
# 빌드 → Headless 실행 → 결과 출력 (stdout)

set -e

UNITY_PATH="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
PROJECT_PATH="$(pwd)"
BUILD_PATH="$PROJECT_PATH/WebGLBuild"
BUILD_LOG="$PROJECT_PATH/build.log"

echo "==========================================" >&2
echo "Unity WebGL Auto Benchmark (Headless)" >&2
echo "==========================================" >&2
echo "" >&2

# NODE_OPTIONS 제거 (Emscripten 충돌 방지)
unset NODE_OPTIONS

# 1. WebGL 빌드
echo "[1/3] Building WebGL..." >&2
echo "This may take 5-10 minutes. Please wait..." >&2
echo "" >&2

rm -rf "$BUILD_PATH"

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
    echo "Check build.log for details" >&2
    exit 1
fi

echo "✅ Build complete!" >&2
echo "" >&2

# 2. 압축 해제 및 템플릿 변수 치환
echo "[2/3] Preparing files..." >&2
cd "$BUILD_PATH/Build"
gunzip -kf *.gz 2>/dev/null || true
cd "$PROJECT_PATH"

sed -i '' \
  -e 's/%UNITY_WEBGL_LOADER_FILENAME%/WebGLBuild.loader.js/g' \
  -e 's/%UNITY_WEBGL_DATA_FILENAME%/WebGLBuild.data/g' \
  -e 's/%UNITY_WEBGL_FRAMEWORK_FILENAME%/WebGLBuild.framework.js/g' \
  -e 's/%UNITY_WEBGL_CODE_FILENAME%/WebGLBuild.wasm/g' \
  -e 's/%UNITY_COMPANY_NAME%/DefaultCompany/g' \
  -e 's/%UNITY_PRODUCT_NAME%/Unity WebGL Benchmark/g' \
  -e 's/%UNITY_PRODUCT_VERSION%/1.0/g' \
  "$BUILD_PATH/index.html"

echo "✅ Files prepared!" >&2
echo "" >&2

# 3. 서버 시작 및 Headless 브라우저 실행
echo "[3/3] Running benchmark in headless Chrome..." >&2
echo "" >&2

cd "$BUILD_PATH"

# 커스텀 Python 서버를 백그라운드로 실행
# stdout만 로그에 저장 (JSON 결과), stderr는 그대로 출력 (진행 상황)
python3 "$PROJECT_PATH/server.py" > /tmp/benchmark_server.log 2>&2 &
SERVER_PID=$!

# 서버 시작 대기
sleep 3

echo "Server started on http://localhost:8000" >&2

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
echo "(Benchmark will run for ~70 seconds)" >&2
echo "" >&2

# Headless Chrome 실행 (새로운 headless 모드는 WebGL 지원)
"$CHROME" \
  --headless=new \
  --no-sandbox \
  --disable-setuid-sandbox \
  --disable-dev-shm-usage \
  --disable-web-security \
  --disable-features=IsolateOrigins,site-per-process \
  --autoplay-policy=no-user-gesture-required \
  --enable-features=Vulkan \
  --use-gl=angle \
  --use-angle=swiftshader \
  --window-size=1920,1080 \
  --user-data-dir=/tmp/chrome-benchmark-profile \
  http://localhost:8000 > /dev/null 2>&1 &

CHROME_PID=$!

# 벤치마크 완료 대기
sleep 70

# Chrome 종료
kill $CHROME_PID 2>/dev/null || true

# 서버 종료 대기 (결과가 오면 자동 종료됨)
sleep 3

# 서버 로그에서 결과 추출 (stdout에 JSON만 출력)
if [ -f /tmp/benchmark_server.log ]; then
    cat /tmp/benchmark_server.log
else
    echo "⚠️  No results received." >&2
    echo "   Check if the benchmark completed successfully." >&2
fi

# 서버 종료
kill $SERVER_PID 2>/dev/null || true

echo "" >&2
echo "✅ Benchmark complete!" >&2

# 임시 파일 정리
rm -f /tmp/benchmark_server.log
