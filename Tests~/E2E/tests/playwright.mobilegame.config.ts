import { defineConfig } from '@playwright/test';

/**
 * mobilegame posture 플레이 벤치마크(mobile-game.test.js) 전용 Playwright 설정.
 *
 * 브라우저는 테스트가 세션마다 chromium.launch 로 직접 만든다(세션마다 새 프로세스여야 렌더러 메모리를 분리해 잴 수 있다).
 * 여기에는 타임아웃·직렬 실행·리포터만 둔다. 세션당 최대 ~4분, 페어 모드는 세션 수가 두 배다.
 */
export default defineConfig({
  testDir: './',
  testMatch: 'mobile-game.test.js',
  timeout: (process.env.PERF_PAIR_PROJECT_PATH ? 2 : 1) * Math.max(1, parseInt(process.env.RUN_ROUNDS || '3', 10)) * 240000,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['list']],
});
