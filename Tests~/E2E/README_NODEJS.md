# Node.js Downloader E2E Tests

Embedded Node.js 다운로드 및 검증 시스템의 E2E 테스트입니다.

## 개요

이 테스트는 `AITNodeJSDownloader.cs`의 기능을 JavaScript로 재현하여 검증합니다:

- ✅ 플랫폼 감지 (darwin-arm64, darwin-x64, win-x64, linux-x64)
- ✅ 다운로드 URL 접근성 (3개 미러: nodejs.org, npmmirror, huawei)
- ✅ 실제 Node.js 다운로드 (40-50MB)
- ✅ SHA256 체크섬 검증 (필수 보안 기능)
- ✅ npm/node 실행 가능성
- ✅ npm install 동작 확인
- ✅ 변조 파일 감지 (체크섬 불일치)

## 테스트 목록

### 1. Platform detection should match current system
현재 시스템의 플랫폼을 감지하여 올바른 플랫폼 문자열을 반환하는지 확인합니다.

**검증 항목**:
- macOS ARM64 → `darwin-arm64`
- macOS Intel → `darwin-x64`
- Windows → `win-x64`
- Linux → `linux-x64`

### 2. Checksum dictionary should have all platforms
4개 플랫폼의 SHA256 체크섬이 모두 정의되어 있는지 확인합니다.

**검증 항목**:
- 체크섬 값 존재 확인
- SHA256 형식 검증 (64자 16진수)

### 3. Download URLs should be accessible
3개 미러의 다운로드 URL이 모두 접근 가능한지 확인합니다.

**검증 항목**:
- nodejs.org (공식) - HTTP 200 필수
- cdn.npmmirror.com (폴백 1) - 실패 허용
- repo.huaweicloud.com (폴백 2) - 실패 허용

### 4. Download and verify checksum (REAL DOWNLOAD) ⚠️
**실제로 Node.js를 다운로드**하여 체크섬을 검증합니다.

**주의**:
- 다운로드 크기: 40-50MB
- 소요 시간: 1-3분
- 타임아웃: 5분
- 이미 존재하면 스킵

**검증 항목**:
- 다운로드 성공
- SHA256 체크섬 일치
- 압축 해제 성공
- npm 실행 파일 존재

### 5. Embedded npm should be executable
다운로드한 npm이 실행 가능한지 확인합니다.

**검증 항목**:
- `npm --version` 실행 성공
- 버전 형식 검증 (예: `10.9.0`)

### 6. Embedded node should be executable
다운로드한 node가 실행 가능하고 버전이 일치하는지 확인합니다.

**검증 항목**:
- `node --version` 실행 성공
- 버전 일치 확인 (예: `v24.11.1`)

### 7. npm install should work in test project
Embedded npm으로 실제 패키지 설치가 가능한지 확인합니다.

**검증 항목**:
- 테스트 프로젝트 생성 (package.json)
- `npm install` 실행 성공
- `node_modules/` 폴더 생성 확인
- 의존성 설치 확인 (lodash)

### 8. Checksum validation should fail for tampered file
변조된 파일에 대해 체크섬 검증이 실패하는지 확인합니다.

**검증 항목**:
- 가짜 파일의 체크섬이 공식 체크섬과 다름
- 변조 감지 성공

## 실행 방법

### 🚀 간편 실행 (권장)

**원클릭 테스트 스크립트 사용:**

#### macOS / Linux
```bash
cd Tests~/E2E/tests

# 모든 테스트 실행 (다운로드 포함)
./run-all-tests.sh

# 빠른 테스트만 (다운로드 제외)
./run-all-tests.sh --skip-download

# 브라우저 표시 (디버깅용)
./run-all-tests.sh --headed

# 도움말
./run-all-tests.sh --help
```

#### Windows
```powershell
cd Tests~\E2E\tests

# 모든 테스트 실행 (다운로드 포함)
run-all-tests.bat

# 빠른 테스트만 (다운로드 제외)
run-all-tests.bat --skip-download

# 브라우저 표시 (디버깅용)
run-all-tests.bat --headed

# 도움말
run-all-tests.bat --help
```

### 수동 실행

```bash
cd Tests~/E2E/tests
npm test -- nodejs-downloader.test.js
```

### 개별 테스트 실행

```bash
# 플랫폼 감지만
npm test -- nodejs-downloader.test.js -g "Platform detection"

# 다운로드 URL 접근성만
npm test -- nodejs-downloader.test.js -g "Download URLs"

# 실제 다운로드 (느림)
npm test -- nodejs-downloader.test.js -g "Download and verify checksum"
```

### Headed 모드 (브라우저 표시)

```bash
npm run test:headed -- nodejs-downloader.test.js
```

### Debug 모드

```bash
npm run test:debug -- nodejs-downloader.test.js
```

## 예상 출력

```
Node.js Embedded Runtime E2E Tests

  ✓ 1. Platform detection should match current system (50ms)
  ✓ 2. Checksum dictionary should have all platforms (10ms)
  ✓ 3. Download URLs should be accessible (2000ms)
  ✓ 4. Download and verify checksum (REAL DOWNLOAD) (120000ms)
  ✓ 5. Embedded npm should be executable (500ms)
  ✓ 6. Embedded node should be executable (500ms)
  ✓ 7. npm install should work in test project (30000ms)
  ✓ 8. Checksum validation should fail for tampered file (100ms)

Cleanup
  ✓ Clean up temp directory (10ms)

9 passed (155s)
```

## 체크섬 값 (Node.js v24.11.1)

출처: https://nodejs.org/dist/v24.11.1/SHASUMS256.txt

```javascript
const CHECKSUMS = {
  'darwin-arm64': 'b05aa3a66efe680023f930bd5af3fdbbd542794da5644ca2ad711d68cbd4dc35',
  'darwin-x64': '096081b6d6fcdd3f5ba0f5f1d44a47e83037ad2e78eada26671c252fe64dd111',
  'win-x64': '5355ae6d7c49eddcfde7d34ac3486820600a831bf81dc3bdca5c8db6a9bb0e76',
  'linux-x64': '60e3b0a8500819514aca603487c254298cd776de0698d3cd08f11dba5b8289a8'
};
```

## 파일 구조

```
Tests~/E2E/
├── tests/
│   ├── nodejs-downloader.test.js  # Node.js 다운로더 E2E 테스트 (신규)
│   ├── build-and-benchmark.test.js  # Unity 빌드 벤치마크 테스트 (기존)
│   ├── package.json
│   └── playwright.config.ts
├── temp/                            # 다운로드 임시 파일 (자동 생성/삭제)
└── README_NODEJS.md                 # 이 문서
```

## 문제 해결

### 다운로드 실패
```
Error: Download failed: 404 Not Found
```
**해결**: Node.js 버전 확인 (v24.11.1이 존재하는지)

### 체크섬 불일치
```
Error: expect(received).toBe(expected)
Expected: "b05aa3a66efe680023f930bd5af3fdbbd542794da5644ca2ad711d68cbd4dc35"
Received: "abc123..."
```
**해결**:
1. 다운로드 중 파일 손상 가능성 → 재시도
2. 공식 SHASUMS256.txt와 체크섬 값 비교

### npm 실행 실패 (macOS/Linux)
```
Error: EACCES: permission denied
```
**해결**: 실행 권한 부여
```bash
chmod +x Tools~/NodeJS/darwin-arm64/bin/npm
chmod +x Tools~/NodeJS/darwin-arm64/bin/node
```

### Timeout 오류
```
Error: Timeout of 300000ms exceeded
```
**해결**: 네트워크 속도 느림 → 타임아웃 증가
```javascript
test.setTimeout(600000); // 10분
```

## CI/CD 통합

### GitHub Actions 예시

```yaml
name: Node.js Downloader E2E Tests

on: [push, pull_request]

jobs:
  e2e-nodejs:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [macos-latest, ubuntu-latest, windows-latest]

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

      - name: Run Node.js downloader tests
        run: |
          cd Tests~/E2E/tests
          npm test -- nodejs-downloader.test.js

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: playwright-report-${{ matrix.os }}
          path: Tests~/E2E/tests/playwright-report/
```

## 주의사항

1. **실제 다운로드**: 테스트 4번은 실제로 40-50MB를 다운로드합니다. CI/CD에서는 캐싱 권장.
2. **네트워크 의존**: 인터넷 연결이 필요합니다.
3. **디스크 공간**: 각 플랫폼당 ~180MB 필요 (압축 해제 후).
4. **실행 권한**: macOS/Linux에서는 `chmod +x` 자동 실행됨.

## 크로스 플랫폼 테스트

이 테스트는 **macOS, Windows, Linux**에서 모두 실행 가능합니다.

### 지원 플랫폼

| 플랫폼 | 스크립트 | 감지되는 플랫폼 |
|--------|----------|-----------------|
| macOS Intel | `run-all-tests.sh` | `darwin-x64` |
| macOS Apple Silicon | `run-all-tests.sh` | `darwin-arm64` |
| Windows | `run-all-tests.bat` | `win-x64` |
| Linux | `run-all-tests.sh` | `linux-x64` |

### 플랫폼별 차이점

**다운로드 파일 형식**:
- macOS/Linux: `.tar.gz` (~45-50MB)
- Windows: `.zip` (~34MB)

**npm 경로**:
- macOS/Linux: `bin/npm`
- Windows: `npm.cmd`

자세한 내용은 [CROSS_PLATFORM_TESTING.md](./CROSS_PLATFORM_TESTING.md) 참조.

## 관련 파일

- `Editor/AITNodeJSDownloader.cs` - C# 다운로더 구현
- `Editor/AITNodeJSDownloaderTest.cs` - Unity 메뉴 테스트 도구
- `Tools~/README.md` - Embedded Node.js 문서
- `Tests~/E2E/CROSS_PLATFORM_TESTING.md` - 크로스 플랫폼 테스트 가이드
