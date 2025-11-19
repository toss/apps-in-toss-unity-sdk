#!/bin/bash

# Apps in Toss Unity SDK - 전체 테스트 실행 스크립트
# 사용법: ./run-all-tests.sh [options]

set -e

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 현재 디렉토리 확인
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

echo -e "${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║  Apps in Toss Unity SDK - 전체 테스트 실행                    ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# 옵션 파싱
DOWNLOAD_TEST=true
HEADED=false
DEBUG=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --skip-download)
      DOWNLOAD_TEST=false
      shift
      ;;
    --headed)
      HEADED=true
      shift
      ;;
    --debug)
      DEBUG=true
      shift
      ;;
    --help|-h)
      echo "사용법: ./run-all-tests.sh [options]"
      echo ""
      echo "옵션:"
      echo "  --skip-download    다운로드 테스트 제외 (빠른 실행)"
      echo "  --headed           브라우저 표시 (디버깅용)"
      echo "  --debug            디버그 모드"
      echo "  --help, -h         도움말 표시"
      echo ""
      echo "예시:"
      echo "  ./run-all-tests.sh                     # 모든 테스트 실행"
      echo "  ./run-all-tests.sh --skip-download     # 빠른 테스트만"
      echo "  ./run-all-tests.sh --headed            # 브라우저 표시"
      exit 0
      ;;
    *)
      echo -e "${RED}알 수 없는 옵션: $1${NC}"
      echo "도움말: ./run-all-tests.sh --help"
      exit 1
      ;;
  esac
done

# 의존성 확인
echo -e "${BLUE}📦 의존성 확인...${NC}"
if [ ! -d "node_modules" ]; then
  echo -e "${YELLOW}⚠️  node_modules가 없습니다. npm install 실행 중...${NC}"
  npm install
  echo -e "${GREEN}✓ 의존성 설치 완료${NC}"
else
  echo -e "${GREEN}✓ node_modules 존재${NC}"
fi
echo ""

# 테스트 설정
TEST_ARGS="nodejs-downloader.test.js"
REPORTER="--reporter=list"

if [ "$HEADED" = true ]; then
  REPORTER="--headed"
fi

if [ "$DEBUG" = true ]; then
  REPORTER="--debug"
fi

if [ "$DOWNLOAD_TEST" = false ]; then
  TEST_ARGS="$TEST_ARGS --grep-invert=\"REAL DOWNLOAD|npm install\""
  echo -e "${YELLOW}ℹ️  다운로드 테스트 제외 모드${NC}"
  echo -e "${YELLOW}   (빠른 테스트: 플랫폼 감지, 체크섬, URL 접근성)${NC}"
  echo ""
fi

echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}1️⃣  Node.js Downloader E2E 테스트${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
echo ""

START_TIME=$(date +%s)

# 테스트 실행
if [ "$DOWNLOAD_TEST" = false ]; then
  SKIP_BUILD=true npm test -- nodejs-downloader.test.js $REPORTER --grep-invert="REAL DOWNLOAD|npm install"
  TEST_EXIT_CODE=$?
else
  if [ "$HEADED" = true ]; then
    SKIP_BUILD=true npm run test:headed -- nodejs-downloader.test.js
    TEST_EXIT_CODE=$?
  elif [ "$DEBUG" = true ]; then
    SKIP_BUILD=true npm run test:debug -- nodejs-downloader.test.js
    TEST_EXIT_CODE=$?
  else
    SKIP_BUILD=true npm test -- nodejs-downloader.test.js $REPORTER
    TEST_EXIT_CODE=$?
  fi
fi

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"

if [ $TEST_EXIT_CODE -eq 0 ]; then
  echo -e "${GREEN}✅ 모든 테스트 통과!${NC}"
  echo -e "${GREEN}   소요 시간: ${DURATION}초${NC}"
  echo ""

  # 설치된 Node.js 확인
  NODE_PATH="../../../Tools~/NodeJS"
  if [ -d "$NODE_PATH/darwin-arm64/bin" ] || [ -d "$NODE_PATH/darwin-x64/bin" ] || [ -d "$NODE_PATH/win-x64" ]; then
    echo -e "${CYAN}📂 Embedded Node.js 설치 확인:${NC}"

    if [ -d "$NODE_PATH/darwin-arm64" ]; then
      NPM_PATH="$NODE_PATH/darwin-arm64/bin/npm"
      if [ -f "$NPM_PATH" ]; then
        NPM_VERSION=$("$NPM_PATH" --version 2>/dev/null || echo "unknown")
        NODE_VERSION=$("$NODE_PATH/darwin-arm64/bin/node" --version 2>/dev/null || echo "unknown")
        echo -e "${GREEN}   ✓ darwin-arm64: node ${NODE_VERSION}, npm ${NPM_VERSION}${NC}"
      fi
    fi

    if [ -d "$NODE_PATH/darwin-x64" ]; then
      NPM_PATH="$NODE_PATH/darwin-x64/bin/npm"
      if [ -f "$NPM_PATH" ]; then
        NPM_VERSION=$("$NPM_PATH" --version 2>/dev/null || echo "unknown")
        NODE_VERSION=$("$NODE_PATH/darwin-x64/bin/node" --version 2>/dev/null || echo "unknown")
        echo -e "${GREEN}   ✓ darwin-x64: node ${NODE_VERSION}, npm ${NPM_VERSION}${NC}"
      fi
    fi

    if [ -d "$NODE_PATH/win-x64" ]; then
      echo -e "${GREEN}   ✓ win-x64 설치됨${NC}"
    fi

    if [ -d "$NODE_PATH/linux-x64" ]; then
      echo -e "${GREEN}   ✓ linux-x64 설치됨${NC}"
    fi
    echo ""
  fi

  echo -e "${CYAN}📊 테스트 보고서:${NC}"
  echo -e "${CYAN}   playwright-report/ 폴더에 생성됨${NC}"
  echo -e "${CYAN}   확인: npm run report${NC}"
  echo ""

  exit 0
else
  echo -e "${RED}❌ 테스트 실패!${NC}"
  echo -e "${RED}   종료 코드: $TEST_EXIT_CODE${NC}"
  echo -e "${RED}   소요 시간: ${DURATION}초${NC}"
  echo ""
  echo -e "${YELLOW}💡 디버깅 팁:${NC}"
  echo -e "${YELLOW}   - 브라우저 표시: ./run-all-tests.sh --headed${NC}"
  echo -e "${YELLOW}   - 디버그 모드: ./run-all-tests.sh --debug${NC}"
  echo -e "${YELLOW}   - 로그 확인: cat playwright-report/index.html${NC}"
  echo ""
  exit 1
fi
