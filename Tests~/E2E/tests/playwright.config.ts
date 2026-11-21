import { defineConfig, devices } from '@playwright/test';

// 모바일 에뮬레이션 활성화 여부 (macOS CI에서만 true)
const isMobileEmulation = process.env.MOBILE_EMULATION === 'true';

// Android UA 에뮬레이션 (MOBILE_EMULATION=true와 함께 쓴다). 기본 모바일 프로젝트는 iPhone UA라
// UA로 분기하는 Android 전용 경로(AITTemplate 런타임 스크립트의 /Android/ 판별 등)를 한 번도 타지 않는다.
// Pixel 7은 chromium 기반 프로필이라 시스템 Chrome 위에서 Android Chrome UA·뷰포트·터치를 그대로 재현한다.
const isAndroidEmulation = isMobileEmulation && process.env.ANDROID_EMULATION === 'true';

// AIT_E2E_SHARD=playerprefs-N job은 PlayerPrefs 케이스만 돌린다. 진입 파일 외의 독립 spec
// (audio-resume, perf-ttff 등)은 core 샤드가 맡으므로 여기서 제외해 중복 실행을 막는다.
// 값 검증은 lib/shard.js가 한다.
const isPlayerPrefsShard = (process.env.AIT_E2E_SHARD || '').startsWith('playerprefs-');

// brotli-fallback 샤드는 Brotli 빌드 전용 spec 하나만 돌린다. 다른 샤드와 로컬 전체 실행에서는
// 이 spec을 뺀다(압축을 끈 빌드에서는 전제가 성립하지 않는다).
const isBrotliFallbackShard = (process.env.AIT_E2E_SHARD || '') === 'brotli-fallback';

export default defineConfig({
  testDir: './',
  ...(isPlayerPrefsShard ? { testMatch: ['e2e-full-pipeline.test.js'] } : {}),
  ...(isBrotliFallbackShard
    ? { testMatch: ['brotli-fallback.test.js'] }
    : { testIgnore: ['brotli-fallback.test.js', '**/node_modules/**'] }),
  timeout: 300000, // 5분 (Unity 로딩 포함)
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,

  reporter: [
    ['list']
  ],

  use: {
    headless: true,
    // 데스크톱에서만 viewport 설정 (모바일은 디바이스 프로필에서 자동 설정)
    ...(isMobileEmulation ? {} : { viewport: { width: 1280, height: 720 } }),
    screenshot: 'only-on-failure',
    // video 녹화 비활성화: Playwright 번들 ffmpeg가 필요한데 그 다운로드가
    // cdn.playwright.dev(GCS) 경로라 GitHub Actions runner에서 stall/hang함.
    // 디버깅은 trace(DOM 스냅샷·네트워크·콘솔·액션별 스크린샷)+screenshot으로 충분.
    trace: 'retain-on-failure',

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
      name: isAndroidEmulation ? 'Android Chrome' : isMobileEmulation ? 'Mobile Chrome' : 'chromium',
      use: isAndroidEmulation
        ? {
            ...devices['Pixel 7'],
            browserName: 'chromium',
            channel: 'chrome',
          }
        : isMobileEmulation
        ? {
            // iPhone 8 뷰포트/터치 설정 + 시스템 Chrome (runner image 사전 설치)
            // iPhone 8 device 기본은 webkit이라 chromium으로 명시 후 channel 지정.
            ...devices['iPhone 8'],
            browserName: 'chromium',
            channel: 'chrome',
          }
        : {
            ...devices['Desktop Chrome'],
            channel: 'chrome',
          },
    },
  ],

});
