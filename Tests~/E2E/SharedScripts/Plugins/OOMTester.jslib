/**
 * OOMTester.jslib
 *
 * WebView 레벨 메모리 할당을 위한 JavaScript 브릿지
 * Scenario 2: 광고 SDK, 비디오 버퍼 등 브라우저 메모리 할당 시뮬레이션
 */

mergeInto(LibraryManager.library, {
    /**
     * 상태 초기화 헬퍼 함수
     */
    OOMTester_EnsureState: function() {
        if (!window._OOMTesterState) {
            window._OOMTesterState = {
                allocations: [],
                videoElements: [],
                totalAllocatedBytes: 0
            };
        }
        return window._OOMTesterState;
    },

    /**
     * JavaScript ArrayBuffer로 메모리 할당 (광고 SDK 시뮬레이션)
     * @param {number} megabytes - 할당할 메가바이트 수
     * @returns {number} 할당된 바이트 수, 실패 시 -1
     */
    OOMTester_AllocateJSMemory: function(megabytes) {
        try {
            if (!window._OOMTesterState) {
                window._OOMTesterState = { allocations: [], videoElements: [], totalAllocatedBytes: 0 };
            }
            var state = window._OOMTesterState;

            var bytes = megabytes * 1024 * 1024;

            // Uint8Array를 직접 생성하여 GC 방지 (ArrayBuffer만 있으면 GC될 수 있음)
            var view = new Uint8Array(bytes);

            // 전체 데이터를 채워서 실제 물리 메모리 할당 강제
            for (var i = 0; i < bytes; i++) {
                view[i] = (i * 7) % 256;  // 패턴 있는 데이터로 채움
            }

            // view와 buffer 모두 저장하여 참조 유지
            state.allocations.push({
                type: 'arraybuffer',
                view: view,
                buffer: view.buffer,
                size: bytes
            });
            state.totalAllocatedBytes += bytes;

            console.log('[OOMTester-JS] Allocated ' + megabytes + 'MB ArrayBuffer. Total: ' +
                (state.totalAllocatedBytes / (1024 * 1024)) + 'MB, Allocations: ' + state.allocations.length);

            return bytes;
        } catch (error) {
            console.error('[OOMTester-JS] Failed to allocate ' + megabytes + 'MB: ' + error.message);
            return -1;
        }
    },

    /**
     * 비디오 요소 생성 및 대용량 Blob으로 비디오 버퍼 시뮬레이션
     * @param {number} megabytes - 시뮬레이션할 버퍼 크기 (MB)
     * @returns {number} 할당된 바이트 수, 실패 시 -1
     */
    OOMTester_AllocateVideoBuffer: function(megabytes) {
        try {
            if (!window._OOMTesterState) {
                window._OOMTesterState = { allocations: [], videoElements: [], totalAllocatedBytes: 0 };
            }
            var state = window._OOMTesterState;

            var bytes = megabytes * 1024 * 1024;

            // 대용량 Uint8Array 생성 (비디오 데이터 시뮬레이션)
            var videoData = new Uint8Array(bytes);
            for (var i = 0; i < bytes; i += 4096) {
                videoData[i] = i % 256;
            }

            // Blob 생성 (비디오 버퍼 시뮬레이션)
            var blob = new Blob([videoData], { type: 'video/mp4' });
            var blobUrl = URL.createObjectURL(blob);

            // Video 요소 생성
            var video = document.createElement('video');
            video.src = blobUrl;
            video.style.display = 'none';
            document.body.appendChild(video);

            state.videoElements.push({ video: video, blobUrl: blobUrl, blob: blob, data: videoData });
            state.totalAllocatedBytes += bytes;

            console.log('[OOMTester-JS] Allocated ' + megabytes + 'MB video buffer. Total: ' +
                (state.totalAllocatedBytes / (1024 * 1024)) + 'MB');

            return bytes;
        } catch (error) {
            console.error('[OOMTester-JS] Failed to allocate video buffer: ' + error.message);
            return -1;
        }
    },

    /**
     * Canvas를 사용한 이미지 버퍼 할당 (광고 이미지 시뮬레이션)
     * @param {number} megabytes - 할당할 메가바이트 수
     * @returns {number} 할당된 바이트 수, 실패 시 -1
     */
    OOMTester_AllocateCanvasMemory: function(megabytes) {
        try {
            if (!window._OOMTesterState) {
                window._OOMTesterState = { allocations: [], videoElements: [], totalAllocatedBytes: 0 };
            }
            var state = window._OOMTesterState;

            var bytes = megabytes * 1024 * 1024;
            // RGBA이므로 4바이트 per 픽셀
            var pixels = bytes / 4;
            var size = Math.ceil(Math.sqrt(pixels));

            var canvas = document.createElement('canvas');
            canvas.width = size;
            canvas.height = size;
            canvas.style.display = 'none';
            document.body.appendChild(canvas);

            var ctx = canvas.getContext('2d');
            // 실제 픽셀 데이터 생성하여 메모리 할당 강제
            var imageData = ctx.createImageData(size, size);
            for (var i = 0; i < imageData.data.length; i += 4) {
                imageData.data[i] = i % 256;     // R
                imageData.data[i + 1] = 128;     // G
                imageData.data[i + 2] = 64;      // B
                imageData.data[i + 3] = 255;     // A
            }
            ctx.putImageData(imageData, 0, 0);

            var actualBytes = size * size * 4;
            state.allocations.push({ type: 'canvas', canvas: canvas, imageData: imageData, size: actualBytes });
            state.totalAllocatedBytes += actualBytes;

            console.log('[OOMTester-JS] Allocated ' + (actualBytes / (1024 * 1024)).toFixed(2) +
                'MB canvas (' + size + 'x' + size + '). Total: ' +
                (state.totalAllocatedBytes / (1024 * 1024)) + 'MB');

            return actualBytes;
        } catch (error) {
            console.error('[OOMTester-JS] Failed to allocate canvas: ' + error.message);
            return -1;
        }
    },

    /**
     * 현재 JS 레벨에서 할당된 총 메모리 반환 (바이트)
     */
    OOMTester_GetTotalJSAllocated: function() {
        if (!window._OOMTesterState) {
            return 0;
        }
        return window._OOMTesterState.totalAllocatedBytes;
    },

    /**
     * 할당된 모든 JS 메모리 해제
     */
    OOMTester_ClearJSMemory: function() {
        if (!window._OOMTesterState) {
            return 0;
        }
        var state = window._OOMTesterState;
        var freedBytes = state.totalAllocatedBytes;

        // Video 요소 정리
        for (var i = 0; i < state.videoElements.length; i++) {
            var item = state.videoElements[i];
            if (item.video && item.video.parentNode) {
                item.video.parentNode.removeChild(item.video);
            }
            if (item.blobUrl) {
                URL.revokeObjectURL(item.blobUrl);
            }
        }

        // 할당된 메모리 정리 (ArrayBuffer, Canvas 등)
        for (var j = 0; j < state.allocations.length; j++) {
            var alloc = state.allocations[j];
            if (alloc) {
                if (alloc.type === 'canvas' && alloc.canvas && alloc.canvas.parentNode) {
                    alloc.canvas.parentNode.removeChild(alloc.canvas);
                }
                // ArrayBuffer는 참조만 제거하면 GC가 처리
            }
        }

        state.allocations = [];
        state.videoElements = [];
        state.totalAllocatedBytes = 0;

        console.log('[OOMTester-JS] Cleared ' + (freedBytes / (1024 * 1024)) + 'MB of JS memory');

        return freedBytes;
    }
});
