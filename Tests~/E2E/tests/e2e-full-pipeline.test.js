// @ts-check
import { test, expect } from '@playwright/test';
import {
  PROJECT_ROOT,
  SAMPLE_PROJECT,
  AIT_BUILD,
  DIST_WEB,
  SERVER_PORT as serverPort,
} from './lib/env.js';
import { directoryExists } from './lib/fs-utils.js';
import { killServerProcess, startProductionServer } from './lib/server.js';
import { writeResultFiles } from './lib/results.js';
import { registerBuildAndDevServerTests } from './suites/build-and-dev.suite.js';
import { registerProductionCoreTests } from './suites/production-core.suite.js';
import { registerTutorialTests } from './suites/tutorials.suite.js';
import { registerNestedCallbackTests } from './suites/nested-callback.suite.js';
import { registerPlayerPrefsTests } from './suites/playerprefs.suite.js';

/**
 * Apps in Toss Unity SDK - E2E Full Pipeline Tests
 *
 * 진입 파일은 등록 순서와 공유 세션 훅(서버 기동, Unity 초기화, 결과 집계)만 갖고
 * 있고, 실제 test() 케이스는 기능별 등록 모듈에서 가져온다:
 *   - suites/build-and-dev.suite.js   : 1, 2, 2b (빌드 검증 + AIT dev 서버)
 *   - suites/production-core.suite.js : 3, 3-1, 4, 5 (프로덕션 서버 + 런타임 검증)
 *   - suites/tutorials.suite.js       : 6, 7 (Build Customization 튜토리얼)
 *   - suites/nested-callback.suite.js : 8 (중첩 콜백 동기 왕복)
 *   - suites/playerprefs.suite.js     : 9번대 (PlayerPrefs 영속화)
 *
 * Test 3-8 세션 공유:
 * - 서버 1회 시작, Unity 1회 초기화로 반복 초기화 방지
 * - JavaScript 트리거 함수로 테스트 실행 (TriggerAPITest, TriggerSerializationTest)
 */

// 테스트 2가 띄우는 dev 서버 프로세스 핸들. 정상 종료 시 killServerProcess가 비우고,
// 테스트가 중간에 실패해 그 호출을 못 타면 외곽 afterAll이 비상 정리한다.
const devServer = { process: null };

// ============================================================================
// Test Suite
// ============================================================================

test.describe('Apps in Toss Unity SDK E2E Pipeline', () => {

  test.beforeAll(async () => {
    console.log('🚀 E2E Pipeline Tests Starting...');
    console.log(`📁 Project Root: ${PROJECT_ROOT}`);
    console.log(`📁 Sample Project: ${SAMPLE_PROJECT}`);
    console.log(`📁 AIT Build: ${AIT_BUILD}`);
  });

  test.afterAll(async () => {
    if (devServer.process) {
      devServer.process.kill();
      devServer.process = null;
    }

    writeResultFiles(SAMPLE_PROJECT);
  });


  registerBuildAndDevServerTests({ devServer });


  // -------------------------------------------------------------------------
  // Tests 3-8: Production Server + Runtime Tests (세션 공유)
  // -------------------------------------------------------------------------
  test.describe.serial('Production Tests (shared session)', () => {
    /** @type {{
     *   page: import('@playwright/test').Page | null,
     *   serverProcess: import('child_process').ChildProcess | null,
     *   port: number,
     *   pageLoadTime: number,
     *   unityLoadTime: number,
     *   preloadWarnings: string[],
     * }} */
    const session = {
      page: null,
      serverProcess: null,
      port: serverPort,
      pageLoadTime: 0,
      unityLoadTime: 0,
      preloadWarnings: [],
    };

    test.beforeAll(async ({ browser }) => {
      console.log('\n' + '='.repeat(70));
      console.log('🚀 STARTING SHARED SESSION FOR TESTS 3-5');
      console.log('='.repeat(70));

      expect(directoryExists(DIST_WEB), 'dist/web/ should exist for production server').toBe(true);

      // 1. Production 서버 시작
      const prodServer = await startProductionServer(AIT_BUILD, serverPort);
      session.serverProcess = prodServer.process;
      session.port = prodServer.port;

      let serverReady = false;
      for (let i = 0; i < 20; i++) {
        try {
          const response = await fetch(`http://localhost:${session.port}/`, { method: 'HEAD' });
          if (response.ok) {
            serverReady = true;
            break;
          }
        } catch {}
        await new Promise(r => setTimeout(r, 500));
      }

      if (!serverReady) {
        throw new Error(`Server failed to start on port ${session.port}`);
      }

      // 2. 페이지 생성 + Unity 초기화
      session.page = await browser.newPage();

      session.page.on('console', msg => {
        if (msg.type() === 'warning' && msg.text().includes('credentials mode')) {
          session.preloadWarnings.push(msg.text());
        }
      });

      const startTime = Date.now();
      const response = await session.page.goto(`http://localhost:${session.port}?e2e=true`, {
        waitUntil: 'networkidle',
        timeout: 90000
      });

      expect(response?.status()).toBe(200);
      session.pageLoadTime = Date.now() - startTime;

      const unityStartTime = Date.now();
      try {
        await session.page.waitForFunction(() => {
          return window['unityInstance'] !== undefined;
        }, { timeout: 120000 });
        session.unityLoadTime = Date.now() - unityStartTime;
        console.log(`✅ Unity instance ready in ${session.unityLoadTime}ms`);
      } catch {
        session.unityLoadTime = Date.now() - unityStartTime;
        console.log('⚠️ Unity initialization timeout');
      }

      try {
        await session.page.waitForFunction(() => {
          return typeof window['TriggerAPITest'] === 'function';
        }, { timeout: 10000 });
        console.log('✅ Trigger functions registered');
      } catch {
        console.log('⚠️ Trigger functions not found (tests may use auto-run)');
      }

      console.log('='.repeat(70) + '\n');
    });

    test.afterAll(async () => {
      if (session.page) {
        await session.page.close();
        session.page = null;
      }

      await killServerProcess(session.serverProcess, [session.port]);
      session.serverProcess = null;
    });


    registerProductionCoreTests({ session });
    registerTutorialTests({ session });
    registerNestedCallbackTests({ session });
    registerPlayerPrefsTests({ getPort: () => session.port });

  }); // end of test.describe.serial

});
