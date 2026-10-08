import { defineConfig } from '@playwright/test';

/**
 * game posture 플레이 테스트(game-play.test.js) 전용 Playwright 설정.
 *
 * 브라우저/모바일 에뮬레이션은 테스트가 chromium.launchPersistentContext 로 직접 만든다(시나리오마다 IndexedDB 가 유지되는 새 프로필이 필요하다).
 * 그래서 여기에는 타임아웃·직렬 실행·리포터만 둔다. 페어 모드(PERF_PAIR_PROJECT_PATH)는 시나리오가 2개라 시간을 늘린다.
 */
export default defineConfig({
  testDir: './',
  testMatch: 'game-play.test.js',
  timeout: process.env.PERF_PAIR_PROJECT_PATH ? 1500000 : 900000,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['list']],
});
