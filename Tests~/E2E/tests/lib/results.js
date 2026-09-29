// @ts-check
// e2e-full-pipeline.test.js에서 추출한 결과 집계 싱글톤과 결과 파일 기록.
// 모든 등록 모듈이 같은 진입 파일에서 같은 워커로 로드되므로 이 ESM 모듈 캐시
// 싱글톤은 지금의 파일 전역 변수와 수명이 같다(실패 후 워커가 재시작되면 둘 다 초기화된다).
import * as fs from 'fs';
import * as path from 'path';
import { TESTS_DIR } from './env.js';

// 결과 저장용
export const testResults = {
  timestamp: new Date().toISOString(),
  tests: {}
};

export function writeResultFiles(sampleProject) {
  // 1. 전체 테스트 결과
  const resultsPath = path.resolve(TESTS_DIR, 'e2e-test-results.json');
  fs.writeFileSync(resultsPath, JSON.stringify(testResults, null, 2));

  // 2. 벤치마크 결과 (workflow에서 업로드하는 파일)
  const benchmarkPath = path.resolve(TESTS_DIR, 'benchmark-results.json');
  const benchmarkResults = {
    timestamp: testResults.timestamp,
    unityProject: sampleProject,
    buildSize: testResults.tests['1_build_validation']?.buildSizeMB,
    pageLoadTime: testResults.tests['3_production_server']?.pageLoadTimeMs,
    unityLoadTime: testResults.tests['3_production_server']?.unityLoadTimeMs,
    webgl: testResults.tests['3_production_server']?.webgl,
    apiTestResults: testResults.tests['4_runtime_api'] ? {
      totalAPIs: testResults.tests['4_runtime_api'].totalAPIs,
      successCount: testResults.tests['4_runtime_api'].successCount,
      unexpectedErrorCount: testResults.tests['4_runtime_api'].unexpectedErrorCount
    } : null,
    compressionValidation: testResults.tests['1_build_validation']?.compressionValidation || null,
    testsPassed: Object.values(testResults.tests || {}).filter(t => t.passed).length,
    testsTotal: Object.keys(testResults.tests || {}).length
  };
  fs.writeFileSync(benchmarkPath, JSON.stringify(benchmarkResults, null, 2));

  // stdout으로 결과 출력
  console.log('\n');
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
  console.log('📊 E2E Test Results');
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');

  const tests = testResults.tests || {};
  const passed = Object.values(tests).filter(t => t.passed).length;
  const total = Object.keys(tests).length;

  console.log(`\n  ✅ Tests Passed: ${passed}/${total}`);

  const buildSize = tests['1_build_validation']?.buildSizeMB;
  const pageLoad = tests['3_production_server']?.pageLoadTimeMs;
  const unityLoad = tests['3_production_server']?.unityLoadTimeMs;
  const renderer = tests['3_production_server']?.webgl?.renderer;

  console.log('\n  📦 Build Size:      ' + (buildSize ? buildSize.toFixed(2) + ' MB' : 'N/A'));
  console.log('  ⏱️  Page Load:       ' + (pageLoad ? pageLoad + ' ms' : 'N/A'));
  console.log('  🎮 Unity Load:      ' + (unityLoad ? unityLoad + ' ms' : 'N/A'));
  console.log('  🖥️  GPU Renderer:    ' + (renderer || 'N/A'));

  console.log('\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
  console.log('📄 Full Results (JSON):');
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
  console.log(JSON.stringify(testResults, null, 2));
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n');
}
