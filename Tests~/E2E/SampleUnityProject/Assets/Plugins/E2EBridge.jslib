/**
 * E2E Test Bridge - Unity -> JavaScript
 * RuntimeAPITester와 AutoBenchmarkRunner에서 사용
 */

mergeInto(LibraryManager.library, {
    SendAPITestResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2EBridge] API Test Results:', json);

        // Window에 결과 저장
        window.__E2E_API_TEST_RESULTS__ = JSON.parse(json);

        // CustomEvent 발생
        var event = new CustomEvent('e2e-api-test-complete', {
            detail: JSON.parse(json)
        });
        window.dispatchEvent(event);
    },

    SendBenchmarkData: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2EBridge] Benchmark Data:', json);

        // Window에 결과 저장
        window.__E2E_BENCHMARK_DATA__ = JSON.parse(json);

        // CustomEvent 발생
        var event = new CustomEvent('e2e-benchmark-complete', {
            detail: JSON.parse(json)
        });
        window.dispatchEvent(event);
    }
});
