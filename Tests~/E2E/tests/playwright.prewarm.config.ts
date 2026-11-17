import { defineConfig, devices } from '@playwright/test';

/**
 * 프리웜(perf-prewarm.test.js) 전용 Playwright 설정 — Chromium 과 WebKit 둘 다 돌린다.
 *
 * 합성 페이지 부분은 빌드 없이 돌고, 빌드 부분은 PREWARM_BUILD_DIR(dist/web)이 있을 때만 돈다.
 * 측정 안정성을 위해 retries 0 · workers 1. PERF_CHROME_CHANNEL 이 비면 번들 chromium 을 쓴다.
 */
export default defineConfig({
  testDir: './',
  testMatch: 'perf-prewarm.test.js',
  timeout: 600000,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  use: {
    headless: true,
    viewport: { width: 1280, height: 720 },
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        channel: process.env.PERF_CHROME_CHANNEL === undefined ? undefined : (process.env.PERF_CHROME_CHANNEL || undefined),
        launchOptions: { args: ['--enable-webgl', '--use-angle=default'] },
      },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
});
