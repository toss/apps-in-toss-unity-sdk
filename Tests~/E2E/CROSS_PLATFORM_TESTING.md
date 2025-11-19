# 크로스 플랫폼 E2E 테스트 가이드

이 문서는 macOS와 Windows에서 E2E 테스트를 실행하는 방법을 설명합니다.

## 지원 플랫폼

| 플랫폼 | 스크립트 | 테스트 대상 |
|--------|----------|-------------|
| macOS (Intel) | `run-all-tests.sh` | darwin-x64 |
| macOS (Apple Silicon) | `run-all-tests.sh` | darwin-arm64 |
| Windows | `run-all-tests.bat` | win-x64 |
| Linux | `run-all-tests.sh` | linux-x64 |

## macOS 실행 방법

### 전제 조건
```bash
# Node.js 확인 (선택사항 - Embedded 사용 가능)
node --version

# Playwright 설치
cd Tests~/E2E/tests
npm install
```

### 실행

```bash
cd Tests~/E2E/tests

# 모든 테스트 (다운로드 포함)
./run-all-tests.sh

# 빠른 테스트 (다운로드 제외)
./run-all-tests.sh --skip-download

# 브라우저 표시
./run-all-tests.sh --headed
```

### 예상 결과 (macOS ARM64)

```
╔════════════════════════════════════════════════════════════════╗
║  Apps in Toss Unity SDK - 전체 테스트 실행                    ║
╚════════════════════════════════════════════════════════════════╝

✓ 1. Platform detection (darwin-arm64)
✓ 2. Checksum dictionary (4개 플랫폼)
✓ 3. Download URLs accessible (3개 미러)
✓ 4. Download and verify checksum (48.81 MB)
✓ 5. Embedded npm executable (npm 11.6.2)
✓ 6. Embedded node executable (v24.11.1)
✓ 7. npm install functionality
✓ 8. Checksum validation failure
✓ 9. Cleanup

9 passed (10.5s)

📂 Embedded Node.js 설치 확인:
   ✓ darwin-arm64: node v24.11.1, npm 11.6.2
```

## Windows 실행 방법

### 전제 조건
```powershell
# Node.js 확인 (선택사항 - Embedded 사용 가능)
node --version

# Playwright 설치
cd Tests~\E2E\tests
npm install
```

### 실행

```powershell
cd Tests~\E2E\tests

# 모든 테스트 (다운로드 포함)
run-all-tests.bat

# 빠른 테스트 (다운로드 제외)
run-all-tests.bat --skip-download

# 브라우저 표시
run-all-tests.bat --headed
```

### 예상 결과 (Windows)

```
╔════════════════════════════════════════════════════════════════╗
║  Apps in Toss Unity SDK - 전체 테스트 실행                    ║
╚════════════════════════════════════════════════════════════════╝

✓ 1. Platform detection (win-x64)
✓ 2. Checksum dictionary (4개 플랫폼)
✓ 3. Download URLs accessible (3개 미러)
✓ 4. Download and verify checksum (33.73 MB)
✓ 5. Embedded npm executable (npm 11.6.2)
✓ 6. Embedded node executable (v24.11.1)
✓ 7. npm install functionality
✓ 8. Checksum validation failure
✓ 9. Cleanup

9 passed (12.3s)

📂 Embedded Node.js 설치 확인:
   ✓ win-x64: node v24.11.1, npm 11.6.2
```

## 플랫폼별 차이점

### 다운로드 파일 형식

| 플랫폼 | 파일 형식 | 압축 해제 도구 |
|--------|----------|----------------|
| macOS | `.tar.gz` | tar |
| Windows | `.zip` | unzip / PowerShell |
| Linux | `.tar.gz` | tar |

### npm 실행 파일 경로

| 플랫폼 | npm 경로 |
|--------|----------|
| macOS | `Tools~/NodeJS/darwin-arm64/bin/npm` |
| Windows | `Tools~/NodeJS/win-x64/npm.cmd` |
| Linux | `Tools~/NodeJS/linux-x64/bin/npm` |

### 실행 권한

**macOS/Linux**: 자동으로 `chmod +x` 실행
```bash
chmod +x Tools~/NodeJS/darwin-arm64/bin/node
chmod +x Tools~/NodeJS/darwin-arm64/bin/npm
```

**Windows**: 실행 권한 불필요

## 크로스 플랫폼 테스트 매트릭스

### 로컬 테스트

| 테스트 항목 | macOS Intel | macOS ARM | Windows | Linux |
|------------|-------------|-----------|---------|-------|
| Platform detection | darwin-x64 | darwin-arm64 | win-x64 | linux-x64 |
| Checksum validation | ✅ | ✅ | ✅ | ✅ |
| Download URLs | ✅ | ✅ | ✅ | ✅ |
| Download & verify | ✅ | ✅ | ✅ | ✅ |
| npm executable | ✅ | ✅ | ✅ | ✅ |
| node executable | ✅ | ✅ | ✅ | ✅ |
| npm install | ✅ | ✅ | ✅ | ✅ |
| Tampered file detection | ✅ | ✅ | ✅ | ✅ |

### CI/CD 환경

#### GitHub Actions 예시

```yaml
name: Cross-Platform E2E Tests

on: [push, pull_request]

jobs:
  e2e-tests:
    strategy:
      matrix:
        os: [macos-latest, macos-13, windows-latest, ubuntu-latest]
        # macos-latest: ARM64 (M1/M2)
        # macos-13: Intel x64
        # windows-latest: Windows x64
        # ubuntu-latest: Linux x64

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/checkout@v3

      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'

      - name: Install dependencies
        run: |
          cd Tests~/E2E/tests
          npm install

      - name: Run E2E tests (macOS/Linux)
        if: runner.os != 'Windows'
        run: |
          cd Tests~/E2E/tests
          ./run-all-tests.sh

      - name: Run E2E tests (Windows)
        if: runner.os == 'Windows'
        run: |
          cd Tests~/E2E/tests
          run-all-tests.bat

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: playwright-report-${{ matrix.os }}
          path: Tests~/E2E/tests/playwright-report/
```

## 문제 해결

### macOS

#### 권한 오류
```bash
# 스크립트 실행 권한 부여
chmod +x run-all-tests.sh

# npm 실행 권한 부여
chmod +x Tools~/NodeJS/darwin-arm64/bin/npm
chmod +x Tools~/NodeJS/darwin-arm64/bin/node
```

#### Rosetta 2 (Intel Mac에서 ARM64 바이너리)
```bash
# ARM64 바이너리를 Intel Mac에서 실행하려면 Rosetta 2 필요
softwareupdate --install-rosetta --agree-to-license
```

### Windows

#### PowerShell 실행 정책
```powershell
# 실행 정책 확인
Get-ExecutionPolicy

# 실행 정책 변경 (관리자 권한)
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

#### 경로 문제
Windows에서는 백슬래시(`\`) 사용:
```powershell
cd Tests~\E2E\tests
```

#### 압축 해제 오류
```powershell
# unzip이 없으면 PowerShell 사용
Expand-Archive -Path node.zip -DestinationPath .\
```

### Linux

#### tar 명령어 없음
```bash
sudo apt-get install tar
```

#### 권한 오류
```bash
chmod +x run-all-tests.sh
chmod +x Tools~/NodeJS/linux-x64/bin/*
```

## 플랫폼별 다운로드 크기

| 플랫폼 | 압축 파일 | 압축 해제 후 |
|--------|-----------|--------------|
| darwin-arm64 | ~48.8 MB | ~180 MB |
| darwin-x64 | ~44.5 MB | ~165 MB |
| win-x64 | ~33.7 MB | ~140 MB |
| linux-x64 | ~26.2 MB | ~130 MB |

## 테스트 소요 시간

| 테스트 모드 | macOS | Windows | Linux |
|-------------|-------|---------|-------|
| 전체 (다운로드 포함) | ~10.5s | ~12.3s | ~9.8s |
| 빠른 테스트 | ~3.7s | ~4.2s | ~3.5s |

## 검증 체크리스트

각 플랫폼에서 다음 항목을 확인하세요:

- [ ] 플랫폼 감지 정확성
- [ ] SHA256 체크섬 4개 플랫폼 모두 일치
- [ ] 다운로드 URL 3개 미러 모두 접근 가능
- [ ] 실제 Node.js 다운로드 성공 (해당 플랫폼)
- [ ] 압축 해제 성공
- [ ] npm 실행 가능
- [ ] node 실행 가능 및 버전 일치 (v24.11.1)
- [ ] npm install 정상 작동
- [ ] 변조 파일 감지 성공

## 참고 자료

- [Playwright 크로스 플랫폼 테스트](https://playwright.dev/docs/test-runners)
- [Node.js 플랫폼별 배포본](https://nodejs.org/dist/)
- [GitHub Actions 매트릭스 빌드](https://docs.github.com/en/actions/using-jobs/using-a-matrix-for-your-jobs)
