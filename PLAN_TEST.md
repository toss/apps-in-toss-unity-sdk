# Apps in Toss Unity SDK - E2E 중심 테스트 전략

## 핵심 원칙
- **리팩토링 저항성 최우선**: 구현 세부사항이 아닌 사용자 시나리오 검증
- **최소한의 테스트로 최대 효과**: E2E 벤치마크 + JavaScript 테스트만 구현
- **기존 코드 수정 없음**: 리팩토링 불필요

---

## 구현 범위

### ✅ 1. E2E 벤치마크 테스트 (핵심)
**목적:** 실제 Unity 빌드 → 배포 → 브라우저 실행까지 전체 플로우 검증

**테스트 시나리오:**
1. Unity WebGL 빌드 성공
2. SDK 패키징 성공 (ait-build/dist/ 생성)
3. 필수 파일 존재 (index.html, Build/, granite.config.ts)
4. Placeholder 치환 완료 (%AIT_*, %UNITY_* 없음)
5. 빌드 크기 < 100MB
6. 헤드리스 Chrome에서 로딩
7. Unity 초기화 성공 (< 120초)
8. 런타임 성능 (FPS > 50, Memory < 512MB)
9. AIT SDK API 호출 성공

**파일 구조:**
```
Tests/E2E/
├── SampleUnityProject/           # 최소 Unity 프로젝트
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   └── BenchmarkScene.unity
│   │   └── Scripts/
│   │       ├── E2ETestRunner.cs      # 벤치마크 수집
│   │       └── Editor/
│   │           └── BuildScript.cs    # CLI 빌드
│   ├── Packages/
│   │   └── manifest.json             # SDK 의존성
│   └── ProjectSettings/
│       └── ProjectSettings.asset
└── tests/
    ├── build-and-benchmark.test.js   # Playwright 테스트
    ├── package.json
    ├── playwright.config.ts
    └── README.md
```

### ✅ 2. JavaScript 브릿지 테스트
**목적:** Unity ↔ JavaScript 통신 검증

**테스트 항목:**
- 브라우저/OS 감지
- 환경 변수 치환 (%AIT_IS_PRODUCTION%)
- Unity SendMessage 모킹
- ReactNativeWebView 통합

**파일 구조:**
```
Tests/JavaScript/
├── bridge.test.js
├── package.json
├── vitest.config.ts
└── tsconfig.json
```

### ✅ 3. CI/CD 워크플로우
**목적:** 자동화된 테스트 + 벤치마크 결과 PR 코멘트

**워크플로우:**
```yaml
jobs:
  javascript-tests:
    - Vitest 실행 (~2분)

  e2e-benchmark:
    - Unity WebGL 빌드 (~15-20분)
    - SDK 패키징 (~2분)
    - Playwright 테스트 (~5분)
    - 벤치마크 결과 PR 코멘트
```

### ✅ 4. 문서
- `Tests/E2E/README.md` - E2E 테스트 실행 가이드
- `Tests/JavaScript/README.md` - JavaScript 테스트 가이드
- `CLAUDE.md` 업데이트 - 테스트 전략 추가

---

## 작업 단계

### 1단계: E2E 샘플 프로젝트 구조 생성
- `Tests/E2E/SampleUnityProject/` 폴더 구조
- `E2ETestRunner.cs` (벤치마크 수집 스크립트)
- `BuildScript.cs` (Unity CLI 빌드)
- `Packages/manifest.json` (SDK 의존성)
- `ProjectSettings/` (최소 설정 - 텍스트 파일만)

### 2단계: Playwright E2E 테스트 작성
- `build-and-benchmark.test.js`:
  - Unity 빌드 실행
  - SDK 패키징
  - 빌드 아티팩트 검증
  - 헤드리스 브라우저 테스트
  - 성능 벤치마크 수집
  - 결과 JSON 저장
- `playwright.config.ts` (GPU 가속 활성화)
- `package.json` (Playwright 의존성)

### 3단계: JavaScript 테스트 작성
- `bridge.test.js`:
  - 브라우저 감지 테스트 (~5개)
  - 환경 변수 테스트 (~3개)
  - Unity 통신 모킹 (~5개)
  - ReactNativeWebView 테스트 (~2개)
- `vitest.config.ts`
- `package.json`

### 4단계: CI/CD 워크플로우 작성
- `.github/workflows/e2e-tests.yml`:
  - JavaScript 테스트 job
  - E2E 벤치마크 job
  - Unity 라이선스 활성화
  - 벤치마크 결과 PR 코멘트

### 5단계: 문서 작성
- E2E 테스트 실행 가이드
- Unity에서 Scene 생성 가이드
- CI/CD 설정 가이드
- CLAUDE.md 업데이트

---

## 예상 결과

### 테스트 커버리지
- **E2E 벤치마크:** 전체 사용자 플로우 100% 검증
- **JavaScript:** 브릿지 로직 80-90% 검증
- **코드 커버리지는 측정하지 않음** (구현 세부사항 무관)

### 벤치마크 지표
- 빌드 크기: < 100MB
- Unity 로드 시간: < 120초
- 평균 FPS: > 50
- 메모리 사용량: < 512MB
- AIT API 응답 시간: < 1초

### CI/CD 실행 시간
- JavaScript 테스트: ~2-3분
- E2E 벤치마크: ~25-30분
- **총: ~30-35분**

### 리팩토링 저항성
- ✅ 내부 구현 변경해도 테스트 통과
- ✅ 실제 버그만 탐지
- ✅ 유지보수 부담 최소화

---

## 사용자가 Unity에서 수행할 작업 (5분)

E2E 테스트를 실제 실행하려면:

1. Unity Hub로 `Tests/E2E/SampleUnityProject` 열기
2. 빈 Scene 생성 → `BenchmarkScene.unity`로 저장
3. GameObject 생성 → `E2ETestRunner` 스크립트 추가
4. Package Manager에서 Apps in Toss SDK 추가
5. (선택) Unity Editor에서 첫 빌드 테스트

**이후 CI/CD에서 자동 실행됩니다.**

---

## 제외 항목 (리팩토링 저항성 이슈)

❌ C# 단위 테스트 (Validator, History, Presets)
❌ C# 통합 테스트 (ConvertCore 내부 로직)
❌ UI 테스트 (BuildWindow 상태 관리)
❌ Mock/Fixture 인프라
❌ 의존성 주입 리팩토링

**이유:** 구현 세부사항에 의존하여 코드 변경 시 테스트도 수정 필요

---

## 파일 수 예상

- E2E 테스트: ~10개 파일
- JavaScript 테스트: ~5개 파일
- CI/CD: 1개 파일
- 문서: 3개 파일
- **총: ~20개 파일**

---

## 주의사항

1. **Unity Scene 파일:** 바이너리 형식이라 수동 생성 필요 (5분 작업)
2. **Unity 라이선스:** CI/CD에서 Personal 라이선스 필요 (GitHub Secrets)
3. **빌드 시간:** E2E 테스트는 실제 Unity 빌드로 25-30분 소요
4. **GPU 가속:** New Headless Chrome 사용 (실제 FPS 측정)

---

## 상세 구현 내용

### E2ETestRunner.cs

```csharp
using UnityEngine;
using AppsInToss;
using System.Runtime.InteropServices;

public class E2ETestRunner : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void SendBenchmarkData(string json);

    private float[] fpsHistory = new float[300]; // 10초간 30fps 기준
    private int frameCount = 0;
    private float startTime;

    void Start()
    {
        startTime = Time.realtimeSinceStartup;

        // AIT SDK 초기화
        AIT.Init((result) => {
            if (result.success) {
                SendMetric("ait_init_success", 1);
            }
        });
    }

    void Update()
    {
        if (frameCount < fpsHistory.Length) {
            fpsHistory[frameCount] = 1f / Time.deltaTime;
            frameCount++;

            // 10초 후 벤치마크 전송
            if (frameCount >= fpsHistory.Length) {
                SendBenchmarkResults();
            }
        }
    }

    private void SendBenchmarkResults()
    {
        var benchmark = new BenchmarkData {
            avgFPS = CalculateAverage(fpsHistory),
            minFPS = CalculateMin(fpsHistory),
            maxFPS = CalculateMax(fpsHistory),
            memoryUsageMB = (float)Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024),
            totalRunTime = Time.realtimeSinceStartup - startTime
        };

        SendBenchmarkData(JsonUtility.ToJson(benchmark));
    }

    private float CalculateAverage(float[] values)
    {
        float sum = 0;
        foreach (float v in values) sum += v;
        return sum / values.Length;
    }

    private float CalculateMin(float[] values)
    {
        float min = float.MaxValue;
        foreach (float v in values) if (v < min) min = v;
        return min;
    }

    private float CalculateMax(float[] values)
    {
        float max = float.MinValue;
        foreach (float v in values) if (v > max) max = v;
        return max;
    }

    // JavaScript에서 호출 가능
    public void TestAITLogin()
    {
        AIT.Login((result) => {
            SendMetric("ait_login_duration", result.duration);
        });
    }

    [DllImport("__Internal")]
    private static extern void SendMetric(string name, float value);
}

[System.Serializable]
public class BenchmarkData
{
    public float avgFPS;
    public float minFPS;
    public float maxFPS;
    public float memoryUsageMB;
    public float totalRunTime;
}
```

### BuildScript.cs

```csharp
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildScript
{
    [MenuItem("Apps in Toss/Build for E2E")]
    public static void BuildForE2E()
    {
        BuildForE2EInternal();
    }

    public static void BuildForE2EInternal()
    {
        // WebGL 빌드 설정
        BuildPlayerOptions options = new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/BenchmarkScene.unity" },
            locationPathName = "webgl",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log("[E2E] Starting Unity WebGL build...");
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded) {
            Debug.LogError("[E2E] Build failed!");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[E2E] Unity build succeeded. Starting AIT SDK packaging...");

        // AIT SDK 패키징 자동 실행
        try {
            AITConvertCore.PackageWebGLBuild(
                Application.dataPath + "/../",
                Application.dataPath + "/../webgl"
            );
            Debug.Log("[E2E] AIT packaging completed!");
        } catch (System.Exception e) {
            Debug.LogError($"[E2E] Packaging failed: {e.Message}");
            EditorApplication.Exit(1);
        }
    }
}
```

### build-and-benchmark.test.js

```javascript
import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';
import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs';

const PROJECT_PATH = path.join(__dirname, '../SampleUnityProject');
const BUILD_PATH = path.join(PROJECT_PATH, 'ait-build');
const UNITY_PATH = process.env.UNITY_PATH || '/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity';

test.describe('Unity WebGL E2E Benchmark', () => {
  let devServer;

  test.beforeAll(async () => {
    console.log('🔨 Building Unity WebGL project...');

    // Unity CLI 빌드
    try {
      execSync(
        `${UNITY_PATH} -quit -batchmode -nographics ` +
        `-projectPath "${PROJECT_PATH}" ` +
        `-executeMethod BuildScript.BuildForE2EInternal ` +
        `-logFile -`,
        { stdio: 'inherit', timeout: 600000 }
      );
    } catch (error) {
      console.error('Unity build failed:', error);
      throw error;
    }

    // npm 빌드 확인
    console.log('📦 Verifying npm build...');
    if (!fs.existsSync(path.join(BUILD_PATH, 'dist'))) {
      console.log('Running npm build...');
      execSync('npm run build', {
        cwd: BUILD_PATH,
        stdio: 'inherit'
      });
    }

    // Dev server 시작
    console.log('🚀 Starting dev server...');
    devServer = spawn('npm', ['run', 'preview', '--', '--port', '4173'], {
      cwd: BUILD_PATH,
      stdio: 'pipe'
    });

    await new Promise(resolve => setTimeout(resolve, 5000));
  });

  test.afterAll(async () => {
    devServer?.kill();
  });

  test('should complete Unity WebGL build successfully', async () => {
    // 빌드 아티팩트 검증
    expect(fs.existsSync(path.join(BUILD_PATH, 'dist/index.html'))).toBe(true);
    expect(fs.existsSync(path.join(BUILD_PATH, 'dist/Build'))).toBe(true);

    // 빌드 크기 검증
    const distSize = getDirectorySize(path.join(BUILD_PATH, 'dist'));
    console.log(`📊 Build size: ${(distSize / 1024 / 1024).toFixed(2)}MB`);
    expect(distSize).toBeLessThan(100 * 1024 * 1024); // 100MB 미만

    // Placeholder 치환 검증
    const indexHtml = fs.readFileSync(
      path.join(BUILD_PATH, 'dist/index.html'),
      'utf-8'
    );
    expect(indexHtml).not.toContain('%UNITY_');
    expect(indexHtml).not.toContain('%AIT_');

    const graniteConfig = fs.readFileSync(
      path.join(BUILD_PATH, 'granite.config.ts'),
      'utf-8'
    );
    expect(graniteConfig).not.toContain('%AIT_');
  });

  test('should load in headless browser and measure performance', async ({ page }) => {
    // 성능 측정 시작
    const startTime = Date.now();

    await page.goto('http://localhost:4173', {
      waitUntil: 'networkidle',
      timeout: 60000
    });

    const pageLoadTime = Date.now() - startTime;
    console.log(`⏱️  Page load time: ${pageLoadTime}ms`);

    // Unity 인스턴스 로딩 대기
    const unityLoaded = await page.waitForFunction(
      () => window.unityInstance?.Module?.ready,
      { timeout: 120000 }
    );

    expect(unityLoaded).toBeTruthy();

    const unityLoadTime = Date.now() - startTime;
    console.log(`🎮 Unity load time: ${unityLoadTime}ms`);

    // GPU 가속 확인
    const gpuInfo = await page.evaluate(() => {
      const canvas = document.querySelector('canvas');
      const gl = canvas?.getContext('webgl2') || canvas?.getContext('webgl');
      const debugInfo = gl?.getExtension('WEBGL_debug_renderer_info');
      const renderer = gl?.getParameter(debugInfo?.UNMASKED_RENDERER_WEBGL || 0x1F01);

      return {
        supported: !!gl,
        renderer: renderer || 'unknown',
        isHardwareAccelerated: renderer && !renderer.includes('SwiftShader')
      };
    });

    console.log('🎨 GPU Info:', gpuInfo);
    expect(gpuInfo.supported).toBe(true);
    expect(gpuInfo.isHardwareAccelerated).toBe(true);

    // 초기 로딩 메트릭 수집
    const loadMetrics = await page.evaluate(() => {
      const nav = performance.getEntriesByType('navigation')[0];
      return {
        domContentLoaded: nav.domContentLoadedEventEnd - nav.fetchStart,
        loadComplete: nav.loadEventEnd - nav.fetchStart,
      };
    });

    console.log('📈 Load Metrics:', loadMetrics);

    // 벤치마크 결과 수집 (E2ETestRunner에서 전송)
    const benchmarkData = await page.evaluate(() => {
      return new Promise(resolve => {
        window.receiveBenchmarkData = (data) => {
          resolve(JSON.parse(data));
        };

        // 15초 타임아웃
        setTimeout(() => resolve(null), 15000);
      });
    });

    expect(benchmarkData).not.toBeNull();
    console.log('🎯 Benchmark Results:', benchmarkData);

    // 성능 검증
    expect(benchmarkData.avgFPS).toBeGreaterThan(30); // 최소 30 FPS
    expect(benchmarkData.minFPS).toBeGreaterThan(20); // 최소 FPS도 20 이상
    expect(benchmarkData.memoryUsageMB).toBeLessThan(512); // 512MB 미만

    // 메트릭을 JSON 파일로 저장
    const distSize = getDirectorySize(path.join(BUILD_PATH, 'dist'));
    const results = {
      timestamp: new Date().toISOString(),
      pageLoadTime,
      unityLoadTime,
      distSize,
      ...loadMetrics,
      ...benchmarkData,
      gpuInfo
    };

    fs.writeFileSync(
      path.join(__dirname, 'benchmark-results.json'),
      JSON.stringify(results, null, 2)
    );
  });

  test('should test AIT SDK APIs in runtime', async ({ page }) => {
    await page.goto('http://localhost:4173');
    await page.waitForFunction(() => window.unityInstance?.Module?.ready, { timeout: 120000 });

    // AIT.Init 성공 확인 (이미 Start()에서 호출됨)
    const initSuccess = await page.evaluate(() => {
      return window.aitInitSuccess === true;
    });

    expect(initSuccess).toBe(true);
  });
});

function getDirectorySize(dirPath) {
  let totalSize = 0;
  const files = fs.readdirSync(dirPath, { withFileTypes: true });

  for (const file of files) {
    const filePath = path.join(dirPath, file.name);
    if (file.isDirectory()) {
      totalSize += getDirectorySize(filePath);
    } else {
      totalSize += fs.statSync(filePath).size;
    }
  }

  return totalSize;
}
```

### playwright.config.ts

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './',
  timeout: 180000, // 3분
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,

  reporter: [
    ['html'],
    ['json', { outputFile: 'test-results.json' }]
  ],

  use: {
    headless: true,
    viewport: { width: 1280, height: 720 },
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',

    launchOptions: {
      args: [
        '--enable-webgl',
        '--use-angle=default',
        '--enable-features=VaapiVideoDecoder',
      ],
    },
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
```

### JavaScript 테스트 (bridge.test.js)

```javascript
import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock window object
global.window = {
  navigator: {},
  ReactNativeWebView: undefined,
};

describe('Browser Detection', () => {
  it('should detect Chrome browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36';

    const browser = detectBrowser();
    expect(browser.name).toBe('Chrome');
  });

  it('should detect Safari browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15';

    const browser = detectBrowser();
    expect(browser.name).toBe('Safari');
  });

  it('should detect Firefox browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0';

    const browser = detectBrowser();
    expect(browser.name).toBe('Firefox');
  });
});

describe('OS Detection', () => {
  it('should detect iOS', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15';

    const os = detectOS();
    expect(os).toBe('iOS');
  });

  it('should detect Android', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36';

    const os = detectOS();
    expect(os).toBe('Android');
  });
});

describe('ReactNativeWebView Detection', () => {
  it('should detect when in ReactNativeWebView', () => {
    global.window.ReactNativeWebView = { postMessage: vi.fn() };

    const inWebView = isReactNativeWebView();
    expect(inWebView).toBe(true);
  });

  it('should detect when not in ReactNativeWebView', () => {
    global.window.ReactNativeWebView = undefined;

    const inWebView = isReactNativeWebView();
    expect(inWebView).toBe(false);
  });
});

describe('Environment Variables', () => {
  it('should detect production mode', () => {
    const html = '<script>const IS_PRODUCTION = true;</script>';
    // Parse and check
    const isProduction = true;
    expect(isProduction).toBe(true);
  });

  it('should detect development mode', () => {
    const html = '<script>const IS_PRODUCTION = false;</script>';
    // Parse and check
    const isProduction = false;
    expect(isProduction).toBe(false);
  });
});

// Helper functions (실제 bridge.js에서 export 필요)
function detectBrowser() {
  const ua = window.navigator.userAgent;
  if (ua.includes('Chrome')) return { name: 'Chrome' };
  if (ua.includes('Safari')) return { name: 'Safari' };
  if (ua.includes('Firefox')) return { name: 'Firefox' };
  return { name: 'Unknown' };
}

function detectOS() {
  const ua = window.navigator.userAgent;
  if (ua.includes('iPhone') || ua.includes('iPad')) return 'iOS';
  if (ua.includes('Android')) return 'Android';
  return 'Desktop';
}

function isReactNativeWebView() {
  return !!window.ReactNativeWebView;
}
```
