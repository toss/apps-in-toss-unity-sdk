# E2E 벤치마크 테스트

Apps in Toss Unity SDK의 E2E (End-to-End) 벤치마크 테스트입니다.

## 개요

실제 Unity 프로젝트를 WebGL로 빌드하고, SDK로 패키징한 후, 헤드리스 Chrome 브라우저에서 실행하여 성능을 측정합니다.

## 테스트 범위

### 1. 빌드 검증
- Unity WebGL 빌드 성공 여부
- SDK 패키징 성공 여부 (ait-build/dist/ 생성)
- 필수 파일 존재 확인 (index.html, Build/, Runtime/)
- Placeholder 치환 완료 (%AIT_*, %UNITY_* 제거)
- 빌드 크기 < 100MB

### 2. 런타임 검증
- 헤드리스 Chrome에서 로딩 성공
- Unity 초기화 성공 (< 120초)
- WebGL 지원 확인
- GPU 가속 활성화 확인

### 3. 성능 벤치마크
- 페이지 로드 시간
- Unity 로드 시간
- 평균 FPS (목표: > 20)
- 최소 FPS (목표: > 10)
- 메모리 사용량 (목표: < 1GB)

## 사전 준비

### 0. 환경 설정 (필수)

벤치마크 스크립트가 샘플 프로젝트를 가져올 수 있도록 설정이 필요합니다.

```bash
cd Tests~/E2E

# .env 파일 생성
cp .env.example .env

# .env 파일 편집
nano .env
```

`.env` 파일 내용:
```bash
# Internal repository URL (required)
SAMPLE_REPO_URL=git@github.com:toss/apps-in-toss-unity-sdk-sample.git
```

**주의사항:**
- `.env` 파일은 `.gitignore`에 포함되어 커밋되지 않습니다
- Internal Git URL이 공개 저장소에 노출되지 않도록 주의하세요
- 벤치마크 실행 시마다 `git clone --depth 1`로 최신 코드를 가져옵니다

### 1. Unity 프로젝트 설정

E2E 테스트를 실행하려면 먼저 Unity에서 샘플 프로젝트를 설정해야 합니다:

```bash
# Unity Hub로 프로젝트 열기
open -a "Unity Hub" Tests/E2E/SampleUnityProject
```

Unity Editor에서:

1. **Scene 생성**
   - File > New Scene
   - 빈 Scene 생성
   - File > Save As > `Assets/Scenes/BenchmarkScene.unity`

2. **E2ETestRunner 추가**
   - Hierarchy에서 Create Empty GameObject
   - 이름을 "E2ETestRunner"로 변경
   - Inspector에서 Add Component > E2ETestRunner

3. **SDK 패키지 추가**
   - Window > Package Manager
   - + 버튼 > Add package from disk
   - SDK의 `package.json` 선택

4. **첫 빌드 실행**
   - Apps in Toss > Build for E2E 메뉴 클릭
   - 또는 Unity CLI 사용 (아래 참조)

### 2. Node.js 및 npm 설치

```bash
# Node.js 18 이상 필요
node --version  # v18.0.0 이상

# Playwright 의존성 설치
cd Tests/E2E/tests
npm install

# Chromium 브라우저 설치
npx playwright install chromium
```

## 테스트 실행

### ⚡ 빠른 C# 빌드 검증 (build-only.sh)

**SDK 코드 변경 후 C# 컴파일만 빠르게 검증:**

```bash
cd Tests~/E2E
./build-only.sh
```

**실행 내용:**
1. 샘플 프로젝트 clone
2. SDK를 local package로 추가
3. Unity WebGL 빌드 수행 (C# 컴파일 검증)
4. 컴파일 에러/경고 출력
5. 벤치마크는 실행하지 않음 (시간 절약)

**장점:**
- ✅ C# 컴파일 에러를 빠르게 발견
- ✅ 벤치마크 실행 생략으로 시간 단축
- ✅ SDK 코드 변경 후 바로 검증 가능

**출력 예시:**
```
[1/3] Cloning sample project...
✅ Sample project ready

[2/3] Adding SDK as local package...
✅ SDK added to manifest.json

[3/3] Building Unity WebGL with SDK...
This will take 5-10 minutes. Validating C# compilation...

✅ Build successful!

Build artifacts:
  Location: Tests~/E2E/temp/.../WebGLBuild
  Size: 47M

✅ No C# compilation errors or warnings!
✅ C# build validation complete!
```

**에러가 있을 경우:**
```
❌ Build failed with exit code: 1

Showing last 100 lines of build log:
======================================
error CS1519: Invalid token '=' in class, struct, or interface member declaration
error CS0246: The type or namespace name 'Action<void>' could not be found
======================================

Full log: Tests~/E2E/build.log
```

---

### ⭐ 방법 1: benchmark.sh로 완전 자동화 (권장)

**가장 간단한 방법:** Unity 빌드부터 헤드리스 Chrome 실행까지 한 번에 자동화

```bash
cd Tests/E2E
./benchmark.sh
```

**실행 프로세스:**
1. Unity WebGL 빌드 (5-10분)
2. SDK 패키징 (npm install + build)
3. Python HTTP 서버 시작
4. Headless Chrome에서 벤치마크 실행 (~70초)
5. 결과를 JSON으로 출력

**출력 예시:**
```
[1/4] Cloning sample project...
Repository: git@github.com:toss/apps-in-toss-unity-sdk-sample.git
✅ Sample project ready

[2/4] Adding SDK as local package...
✅ SDK added to manifest.json

[3/4] Building Unity WebGL with SDK...
This may take 5-10 minutes. Please wait...

✅ Build complete!

[2/3] Starting HTTP server...
Server started on http://localhost:8000

[3/3] Running benchmark in headless Chrome...
(Benchmark will run for ~70 seconds)

==================================================
BENCHMARK RESULTS
==================================================

Performance Metrics:
  Avg FPS: 58.42
  Min FPS: 42.10
  Max FPS: 60.00
  Memory: 234.50 MB
  Run Time: 10.23 s

==================================================

{
  "avgFPS": 58.42,
  "minFPS": 42.1,
  "maxFPS": 60.0,
  "memoryUsageMB": 234.5,
  "totalRunTime": 10.23,
  "aitInitialized": true,
  "unityVersion": "2022.3.16f1",
  "platform": "WebGLPlayer"
}

✅ Benchmark complete!
```

**특징:**
- GPU 가속 활성화 (`--headless=new`, `--use-gl=angle`)
- HTTP POST로 결과 자동 수집
- 사람이 읽기 쉬운 요약 (stderr) + JSON 출력 (stdout)
- Playwright 의존성 불필요

**환경 변수 (선택):**
```bash
# Unity 경로 지정
UNITY_PATH="/Applications/Unity/Hub/Editor/2022.3.16f1/Unity.app/Contents/MacOS/Unity" ./benchmark.sh
```

---

### 방법 2: Unity Editor에서 빌드 + Playwright 테스트

```bash
# 1. Unity Editor에서 "Apps in Toss > Build for E2E" 실행
# 2. 빌드 완료 후 Playwright 테스트 실행
cd Tests/E2E/tests
npm test
```

### 방법 3: Unity CLI로 전체 자동화

```bash
# Unity CLI로 빌드 (느림: 10-20분 소요)
/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath Tests/E2E/SampleUnityProject \
  -executeMethod BuildScript.BuildForE2EInternal \
  -logFile -

# Playwright 테스트 실행
cd Tests/E2E/tests
npm test
```

### 방법 4: 이미 빌드된 결과물로 테스트만 실행

```bash
# ait-build/dist/ 폴더가 이미 있다면
cd Tests/E2E/tests
npm test
```

## 테스트 결과

### 성공 시

```
✓ should verify build artifacts exist
✓ should load in headless browser and measure performance

📊 Build size: 45.23MB
⏱️  Page load time: 1234ms
🎮 Unity load time: 5678ms
🎨 GPU Info: { supported: true, renderer: 'ANGLE (Intel HD Graphics)', isHardwareAccelerated: true }
📈 Load Metrics: { domContentLoaded: 1100, loadComplete: 1234 }
🎯 Benchmark Results: { avgFPS: 58.5, minFPS: 42.1, maxFPS: 60.0, memoryUsageMB: 234.5 }
✅ Benchmark results saved to benchmark-results.json
```

### benchmark-results.json

```json
{
  "timestamp": "2024-01-15T12:34:56.789Z",
  "pageLoadTime": 1234,
  "unityLoadTime": 5678,
  "distSize": 47456789,
  "loadMetrics": {
    "domContentLoaded": 1100,
    "loadComplete": 1234
  },
  "benchmarkData": {
    "avgFPS": 58.5,
    "minFPS": 42.1,
    "maxFPS": 60.0,
    "memoryUsageMB": 234.5,
    "totalRunTime": 10.2
  },
  "gpuInfo": {
    "supported": true,
    "renderer": "ANGLE (Intel HD Graphics)",
    "isHardwareAccelerated": true
  }
}
```

## 문제 해결

### Unity 빌드 실패

```bash
# Unity 라이선스 활성화 확인
/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity -quit -batchmode -nographics -version

# WebGL 플랫폼 지원 설치 확인
# Unity Hub > Installs > [버전] > Add Modules > WebGL Build Support
```

### 빌드 아티팩트가 없음

```
❌ Build artifacts not found. Please build the project first.
Expected path: Tests/E2E/SampleUnityProject/ait-build/dist
```

**해결:** Unity에서 먼저 빌드를 실행하세요.

### Dev server 시작 실패

```bash
# 포트 4173이 이미 사용 중일 수 있음
lsof -ti:4173 | xargs kill -9

# 또는 다른 포트 사용
cd Tests/E2E/SampleUnityProject/ait-build
npm run preview -- --port 5173
```

### GPU 가속 비활성화

```
🎨 GPU Info: { supported: true, renderer: 'SwiftShader', isHardwareAccelerated: false }
```

**원인:** 소프트웨어 렌더링 사용 (성능 저하)

**해결:** Playwright 설정에서 GPU 플래그 확인:
```typescript
// playwright.config.ts
launchOptions: {
  args: [
    '--enable-webgl',
    '--use-angle=default',
  ],
}
```

## CI/CD에서 실행

### GitHub Actions 설정

E2E 벤치마크는 GitHub Actions에서 자동으로 실행됩니다.

**필요한 Secrets:**
```
UNITY_LICENSE - Unity 라이선스 (Personal/Pro)
UNITY_EMAIL - Unity 계정 이메일
UNITY_PASSWORD - Unity 계정 비밀번호
```

### 1. PR 체크 (비활성화됨) - `.github/workflows/pr-check.yml.disabled`

⚠️ **현재 비활성화 상태**

**이유:**
- GitHub Actions에서 Unity 빌드를 실행하려면 **Unity Plus/Pro 라이선스** 필요
- Unity Personal 라이선스는 CI/CD 자동화를 지원하지 않음
- 라이선스 구매 전까지 **로컬 벤치마크**를 사용

**활성화 방법 (Unity Plus/Pro 라이선스 구매 후):**
```bash
# 파일명 변경으로 활성화
mv .github/workflows/pr-check.yml.disabled .github/workflows/pr-check.yml

# GitHub Secrets 설정 필요
# - UNITY_LICENSE (시리얼 번호 또는 .ulf 파일 내용)
# - UNITY_EMAIL
# - UNITY_PASSWORD
```

**활성화 시 동작:**
1. E2E 벤치마크 자동 실행 (~10분)
2. 성능 메트릭 검증 (FPS > 20/10, Memory < 1GB)
3. PR에 성능 결과 코멘트 자동 작성
4. 성능 기준 미달 시 merge 차단

**Unity 라이선스 문의:**
- Unity Plus: $40/월 (회사 연매출 $200K 미만)
- Unity Pro: $150/월 (회사 연매출 $200K 이상)
- 패키지 개발 전용 예외: sales@unity3d.com 문의

### 2. 로컬 벤치마크 (현재 권장 방법)

**PR 제출 전 로컬에서 실행:**
```bash
cd Tests~/E2E
./benchmark.sh
```

**결과를 PR 코멘트에 수동 작성:**
```markdown
## 🎮 E2E Benchmark Results

### Performance Metrics
| Metric | Value | Status |
|--------|-------|--------|
| Avg FPS | 58.5 | ✅ |
| Min FPS | 42.1 | ✅ |
| Memory | 234.5 MB | ✅ |
```

### 3. 정기 벤치마크 (비활성화됨) - `.github/workflows/e2e-benchmark.yml.disabled`

⚠️ **현재 비활성화 상태** (Unity 라이선스 필요)

**활성화 방법 (Unity Plus/Pro 라이선스 구매 후):**
```bash
mv .github/workflows/e2e-benchmark.yml.disabled .github/workflows/e2e-benchmark.yml
```

**활성화 시 동작:**
- 주 1회 (일요일 자정) 자동 실행
- Unity 빌드 + 벤치마크 → Artifacts 업로드

### 로컬 vs CI

- **로컬**: SSH 사용 (`git@github.com:...`)
- **CI**: HTTPS 사용 (`https://github.com/...`)
- 자동 감지: `$CI` 또는 `$GITHUB_ACTIONS` 환경변수

## 디렉토리 구조

```
Tests/E2E/
├── benchmark.sh                      # 🆕 완전 자동화 스크립트
├── server.py                         # 🆕 HTTP 결과 수집 서버
├── SampleUnityProject/               # 최소 Unity 프로젝트
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   └── BenchmarkScene.unity  (수동 생성 필요)
│   │   ├── Scripts/
│   │   │   ├── E2ETestRunner.cs      # 벤치마크 수집 + HTTP POST
│   │   │   └── Editor/
│   │   │       └── BuildScript.cs    # CLI 빌드
│   │   └── Plugins/
│   │       └── E2EBenchmark.jslib    # 🆕 Unity → JS 브릿지
│   ├── Packages/
│   │   └── manifest.json             # SDK 의존성
│   ├── ProjectSettings/
│   │   └── ProjectSettings.asset
│   ├── webgl/                        # Unity 빌드 출력 (자동 생성)
│   └── ait-build/                    # SDK 패키징 출력 (자동 생성)
│       └── dist/                     # 최종 배포 파일
└── tests/
    ├── build-and-benchmark.test.js   # Playwright 테스트
    ├── playwright.config.ts
    ├── package.json
    └── benchmark-results.json        # 테스트 결과 (자동 생성)
```

## 다음 단계

1. ✅ Unity 프로젝트 설정 및 Scene 생성
2. ✅ SDK 패키지 추가
3. ✅ 첫 빌드 실행
4. ✅ Playwright 테스트 실행
5. ✅ benchmark-results.json 확인
6. 🔄 성능 최적화 (필요 시)

## 참고

- GPU 가속이 활성화되어야 정확한 FPS 측정 가능
- 헤드리스 모드에서도 New Headless Chrome 사용 시 하드웨어 가속 지원
- 벤치마크 결과는 실행 환경(CPU, GPU)에 따라 달라질 수 있음
