@echo off
REM Apps in Toss Unity SDK - 전체 테스트 실행 스크립트 (Windows)
REM 사용법: run-all-tests.bat [options]

setlocal enabledelayedexpansion

REM 색상 정의 (Windows 10+)
set "ESC="
set "CYAN=%ESC%[36m"
set "GREEN=%ESC%[32m"
set "YELLOW=%ESC%[33m"
set "BLUE=%ESC%[34m"
set "RED=%ESC%[31m"
set "NC=%ESC%[0m"

echo.
echo ╔════════════════════════════════════════════════════════════════╗
echo ║  Apps in Toss Unity SDK - 전체 테스트 실행                    ║
echo ╚════════════════════════════════════════════════════════════════╝
echo.

REM 옵션 파싱
set DOWNLOAD_TEST=true
set HEADED=false
set DEBUG=false

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="--skip-download" (
    set DOWNLOAD_TEST=false
    shift
    goto parse_args
)
if /i "%~1"=="--headed" (
    set HEADED=true
    shift
    goto parse_args
)
if /i "%~1"=="--debug" (
    set DEBUG=true
    shift
    goto parse_args
)
if /i "%~1"=="--help" goto show_help
if /i "%~1"=="-h" goto show_help

echo 알 수 없는 옵션: %~1
echo 도움말: run-all-tests.bat --help
exit /b 1

:show_help
echo 사용법: run-all-tests.bat [options]
echo.
echo 옵션:
echo   --skip-download    다운로드 테스트 제외 (빠른 실행)
echo   --headed           브라우저 표시 (디버깅용)
echo   --debug            디버그 모드
echo   --help, -h         도움말 표시
echo.
echo 예시:
echo   run-all-tests.bat                     # 모든 테스트 실행
echo   run-all-tests.bat --skip-download     # 빠른 테스트만
echo   run-all-tests.bat --headed            # 브라우저 표시
exit /b 0

:end_parse

REM 의존성 확인
echo 📦 의존성 확인...
if not exist "node_modules\" (
    echo ⚠️  node_modules가 없습니다. npm install 실행 중...
    call npm install
    if errorlevel 1 (
        echo ✗ npm install 실패
        exit /b 1
    )
    echo ✓ 의존성 설치 완료
) else (
    echo ✓ node_modules 존재
)
echo.

REM 테스트 설정
set TEST_ARGS=nodejs-downloader.test.js
set REPORTER=--reporter=list

if "%HEADED%"=="true" set REPORTER=--headed
if "%DEBUG%"=="true" set REPORTER=--debug

if "%DOWNLOAD_TEST%"=="false" (
    echo ℹ️  다운로드 테스트 제외 모드
    echo    (빠른 테스트: 플랫폼 감지, 체크섬, URL 접근성^)
    echo.
)

echo ═══════════════════════════════════════════════════════════════
echo 1️⃣  Node.js Downloader E2E 테스트
echo ═══════════════════════════════════════════════════════════════
echo.

REM 시작 시간 기록
set START_TIME=%time%

REM 테스트 실행
set SKIP_BUILD=true

if "%DOWNLOAD_TEST%"=="false" (
    call npm test -- nodejs-downloader.test.js %REPORTER% --grep-invert="REAL DOWNLOAD|npm install"
) else (
    if "%HEADED%"=="true" (
        call npm run test:headed -- nodejs-downloader.test.js
    ) else if "%DEBUG%"=="true" (
        call npm run test:debug -- nodejs-downloader.test.js
    ) else (
        call npm test -- nodejs-downloader.test.js %REPORTER%
    )
)

set TEST_EXIT_CODE=%errorlevel%

REM 종료 시간 기록
set END_TIME=%time%

echo.
echo ═══════════════════════════════════════════════════════════════

if %TEST_EXIT_CODE% equ 0 (
    echo ✅ 모든 테스트 통과!
    echo.

    REM 설치된 Node.js 확인
    set NODE_PATH=..\..\..\Tools~\NodeJS

    if exist "!NODE_PATH!\win-x64\node.exe" (
        echo 📂 Embedded Node.js 설치 확인:
        for /f "delims=" %%i in ('"!NODE_PATH!\win-x64\node.exe" --version 2^>nul') do set NODE_VERSION=%%i
        for /f "delims=" %%i in ('"!NODE_PATH!\win-x64\npm.cmd" --version 2^>nul') do set NPM_VERSION=%%i
        echo    ✓ win-x64: node !NODE_VERSION!, npm !NPM_VERSION!
        echo.
    )

    echo 📊 테스트 보고서:
    echo    playwright-report/ 폴더에 생성됨
    echo    확인: npm run report
    echo.

    exit /b 0
) else (
    echo ❌ 테스트 실패!
    echo    종료 코드: %TEST_EXIT_CODE%
    echo.
    echo 💡 디버깅 팁:
    echo    - 브라우저 표시: run-all-tests.bat --headed
    echo    - 디버그 모드: run-all-tests.bat --debug
    echo    - 로그 확인: playwright-report\index.html
    echo.
    exit /b 1
)
