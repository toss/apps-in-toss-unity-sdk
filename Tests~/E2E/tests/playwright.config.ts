import { defineConfig, devices } from '@playwright/test';

// 모바일 에뮬레이션 활성화 여부 (macOS CI에서만 true)
const isMobileEmulation = process.env.MOBILE_EMULATION === 'true';

export default defineConfig({
  testDir: './',
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
    video: 'retain-on-failure',
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
      name: isMobileEmulation ? 'Mobile Chrome' : 'chromium',
      use: isMobileEmulation
        ? {
            // iPhone 8 뷰포트/터치 설정 + Chromium 브라우저 (WebGL 지원)
            ...devices['iPhone 8'],
            browserName: 'chromium',  // WebKit 대신 Chromium 사용
          }
        : { ...devices['Desktop Chrome'] },
    },
  ],

});
