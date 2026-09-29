import { defineConfig, devices } from '@playwright/test';

/**
 * 로딩 성능 실측(perf) 전용 Playwright 설정.
 *
 * 일반 E2E(playwright.config.ts)와 분리한 이유:
 *  - perf-ttff.test.js 만 실행(무거운 픽스처 + median-of-N 반복 측정).
 *  - 무거운 빌드 + 스로틀 + N회 반복으로 단일 테스트가 길어 timeout을 600s로 확대.
 *  - 측정 안정성을 위해 retries 0(재시도가 캐시/부하 상태를 바꿔 측정 오염) · workers 1.
 *  - 데스크톱 Chrome 고정(모바일 에뮬레이션 분기 없음 — 스로틀은 테스트가 CDP로 직접 제어).
 */
export default defineConfig({
  testDir: './',
  testMatch: 'perf-ttff.test.js',
  timeout: 600000, // 10분 (무거운 빌드 + 스로틀 + N회 반복)
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,

  reporter: [
    ['list'],
  ],

  use: {
    headless: true,
    viewport: { width: 1280, height: 720 },
    screenshot: 'only-on-failure',
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
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        channel: 'chrome',
      },
    },
  ],
});
