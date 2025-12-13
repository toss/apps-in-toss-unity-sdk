// E2ETestBridge.jslib - E2E 테스트용 JavaScript 브릿지
// Unity WebGL 빌드에서 Playwright 테스트로 데이터를 전송하는 용도

mergeInto(LibraryManager.library, {
    /**
     * 벤치마크 결과를 window 객체에 저장하고 콘솔에 출력
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendBenchmarkData: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-BENCHMARK] ' + json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_BENCHMARK_DATA__ = json;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-benchmark-complete', { detail: json });
        window.dispatchEvent(event);
    },

    /**
     * API 테스트 결과를 window 객체에 저장하고 콘솔에 출력
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendAPITestResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-API-TEST] ' + json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_API_TEST_DATA__ = json;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-api-test-complete', { detail: json });
        window.dispatchEvent(event);
    },

    /**
     * 직렬화 테스트 결과를 window 객체에 저장하고 콘솔에 출력
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendSerializationTestResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-SERIALIZATION-TEST] ' + json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_SERIALIZATION_TEST_DATA__ = json;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-serialization-complete', { detail: json });
        window.dispatchEvent(event);
    },

    /**
     * JavaScript에서 JSON 파싱 검증
     * C# → JSON → JavaScript 파싱 → JSON → C# 역직렬화 round-trip 검증용
     * @param {string} jsonPtr - JSON 문자열 포인터
     * @param {string} typeNamePtr - 타입 이름 포인터
     * @returns {string} - 파싱 후 재직렬화한 JSON
     */
    ValidateJsonInJS: function(jsonPtr, typeNamePtr) {
        var json = UTF8ToString(jsonPtr);
        var typeName = UTF8ToString(typeNamePtr);

        try {
            // JavaScript에서 파싱
            var parsed = JSON.parse(json);

            // 재직렬화
            var reserialized = JSON.stringify(parsed);

            console.log('[E2E-JSON-VALIDATE] ' + typeName + ': OK');

            // 결과 문자열을 Unity로 반환
            var bufferSize = lengthBytesUTF8(reserialized) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(reserialized, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.error('[E2E-JSON-VALIDATE] ' + typeName + ': FAIL - ' + e.message);

            var errorMsg = 'ERROR: ' + e.message;
            var bufferSize = lengthBytesUTF8(errorMsg) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorMsg, buffer, bufferSize);
            return buffer;
        }
    },

    // =====================================================
    // 메모리 압박 테스트 함수들
    // WASM 힙 + JavaScript 힙 + Canvas(GPU) 메모리 압박
    // =====================================================

    /**
     * WASM 힙 메모리 압박 - Unity WASM 힙에서 직접 할당
     * @param {number} sizeMB - 할당할 메모리 크기 (MB)
     * @returns {number} - 할당된 포인터 (0이면 실패)
     */
    AllocateWasmMemory: function(sizeMB) {
        var bytes = sizeMB * 1024 * 1024;
        var ptr = _malloc(bytes);

        if (ptr === 0) {
            console.error('[E2E-MEMORY] WASM malloc failed for ' + sizeMB + 'MB');
            return 0;
        }

        // 메모리를 실제로 터치해서 물리 메모리 할당 강제
        try {
            HEAPU8.fill(0xAB, ptr, ptr + bytes);
        } catch (e) {
            console.error('[E2E-MEMORY] WASM memory fill failed: ' + e.message);
            _free(ptr);
            return 0;
        }

        window._wasmAllocations = window._wasmAllocations || [];
        window._wasmAllocations.push({ ptr: ptr, size: bytes });

        console.log('[E2E-MEMORY] WASM allocated ' + sizeMB + 'MB at ' + ptr);
        return ptr;
    },

    /**
     * WASM 힙 메모리 해제
     */
    FreeWasmMemory: function() {
        var allocations = window._wasmAllocations || [];
        var totalFreed = 0;

        allocations.forEach(function(alloc) {
            _free(alloc.ptr);
            totalFreed += alloc.size;
        });

        window._wasmAllocations = [];
        console.log('[E2E-MEMORY] WASM freed ' + (totalFreed / 1024 / 1024).toFixed(2) + 'MB');
    },

    /**
     * JavaScript 힙 메모리 압박 - ArrayBuffer로 WebView 힙 압박
     * @param {number} sizeMB - 할당할 메모리 크기 (MB)
     * @returns {number} - 할당된 버퍼 개수
     */
    AllocateJSMemory: function(sizeMB) {
        window._jsAllocations = window._jsAllocations || [];

        try {
            var buffer = new ArrayBuffer(sizeMB * 1024 * 1024);
            // TypedArray로 터치해서 실제 메모리 할당
            new Uint8Array(buffer).fill(0xCD);
            window._jsAllocations.push(buffer);
            console.log('[E2E-MEMORY] JS allocated ' + sizeMB + 'MB (total: ' + window._jsAllocations.length + ' buffers)');
            return window._jsAllocations.length;
        } catch (e) {
            console.error('[E2E-MEMORY] JS allocation failed: ' + e.message);
            return -1;
        }
    },

    /**
     * JavaScript 힙 메모리 해제
     */
    FreeJSMemory: function() {
        var count = (window._jsAllocations || []).length;
        window._jsAllocations = [];

        // GC 힌트 (보장되지 않음)
        if (typeof gc === 'function') {
            gc();
        }

        console.log('[E2E-MEMORY] JS freed ' + count + ' buffers');
    },

    /**
     * Canvas 메모리 압박 (GPU 메모리)
     * @param {number} count - 생성할 Canvas 개수
     * @param {number} width - Canvas 너비
     * @param {number} height - Canvas 높이
     * @returns {number} - 생성된 Canvas 개수
     */
    AllocateCanvasMemory: function(count, width, height) {
        window._canvasAllocations = window._canvasAllocations || [];

        var created = 0;
        for (var i = 0; i < count; i++) {
            try {
                var canvas = document.createElement('canvas');
                canvas.width = width || 2048;
                canvas.height = height || 2048;

                var ctx = canvas.getContext('2d');
                if (ctx) {
                    ctx.fillStyle = '#' + Math.floor(Math.random() * 16777215).toString(16).padStart(6, '0');
                    ctx.fillRect(0, 0, canvas.width, canvas.height);
                }

                window._canvasAllocations.push(canvas);
                created++;
            } catch (e) {
                console.error('[E2E-MEMORY] Canvas allocation failed: ' + e.message);
                break;
            }
        }

        var totalPixels = created * (width || 2048) * (height || 2048);
        var estimatedMB = (totalPixels * 4) / 1024 / 1024;
        console.log('[E2E-MEMORY] Canvas allocated ' + created + ' canvases (~' + estimatedMB.toFixed(2) + 'MB)');

        return created;
    },

    /**
     * Canvas 메모리 해제
     */
    FreeCanvasMemory: function() {
        var canvases = window._canvasAllocations || [];
        var count = canvases.length;

        canvases.forEach(function(canvas) {
            // Canvas 크기를 0으로 설정하여 GPU 메모리 해제 유도
            canvas.width = 0;
            canvas.height = 0;
        });

        window._canvasAllocations = [];
        console.log('[E2E-MEMORY] Canvas freed ' + count + ' canvases');
    },

    /**
     * 메모리 상태 조회
     * @returns {string} - 메모리 상태 JSON (포인터)
     */
    GetMemoryStatus: function() {
        var status = {
            wasmHeapSize: HEAPU8.length,
            wasmAllocatedMB: 0,
            jsAllocatedMB: 0,
            canvasCount: 0,
            canvasEstimatedMB: 0,
            jsHeapUsedMB: null,
            jsHeapTotalMB: null,
            jsHeapLimitMB: null
        };

        // WASM 할당량 계산
        var wasmAllocs = window._wasmAllocations || [];
        wasmAllocs.forEach(function(a) { status.wasmAllocatedMB += a.size; });
        status.wasmAllocatedMB = status.wasmAllocatedMB / 1024 / 1024;

        // JS 할당량 계산
        var jsAllocs = window._jsAllocations || [];
        jsAllocs.forEach(function(b) { status.jsAllocatedMB += b.byteLength; });
        status.jsAllocatedMB = status.jsAllocatedMB / 1024 / 1024;

        // Canvas 계산
        var canvases = window._canvasAllocations || [];
        status.canvasCount = canvases.length;
        canvases.forEach(function(c) {
            status.canvasEstimatedMB += (c.width * c.height * 4) / 1024 / 1024;
        });

        // Chrome에서만 사용 가능한 performance.memory
        if (performance.memory) {
            status.jsHeapUsedMB = performance.memory.usedJSHeapSize / 1024 / 1024;
            status.jsHeapTotalMB = performance.memory.totalJSHeapSize / 1024 / 1024;
            status.jsHeapLimitMB = performance.memory.jsHeapSizeLimit / 1024 / 1024;
        }

        var json = JSON.stringify(status);
        console.log('[E2E-MEMORY] Status: ' + json);

        var bufferSize = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(json, buffer, bufferSize);
        return buffer;
    },

    /**
     * 메모리 압박 테스트 결과 전송
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendMemoryPressureResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-MEMORY-TEST] ' + json);

        window.__E2E_MEMORY_TEST_DATA__ = json;

        var event = new CustomEvent('e2e-memory-test-complete', { detail: json });
        window.dispatchEvent(event);
    }
});
