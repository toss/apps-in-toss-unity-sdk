/**
 * Apps in Toss Unity SDK - DebugLog JavaScript Bridge
 * window.AppsInToss.debugLog()를 호출하는 fire-and-forget 브릿지
 */
mergeInto(LibraryManager.library, {
    __AITDebugLog_Send: function(jsonStr) {
        try {
            var data = JSON.parse(UTF8ToString(jsonStr));
            if (window.AppsInToss && typeof window.AppsInToss.debugLog === 'function') {
                window.AppsInToss.debugLog(data);
            }
        } catch (e) {
            console.warn('[AIT] debugLog failed', e);
        }
    },
    __AITDebugLog_FirstInteractiveEnabled: function() {
        try {
            return (window.__AIT_FIRST_INTERACTIVE_LOG === false) ? 0 : 1; // 부재/미치환 → 기본 활성(fail-open)
        } catch (e) {
            return 1;
        }
    },

    /**
     * 훅 계측용 performance.mark 시작점을 남긴다 (devtools Performance 타임라인용).
     * performance API가 없는 환경(구형 브라우저 등)에서는 조용히 no-op.
     */
    __AITDebugLog_MarkStart: function(hookNamePtr) {
        try {
            if (typeof performance === 'undefined' || typeof performance.mark !== 'function') return;
            var name = UTF8ToString(hookNamePtr);
            performance.mark('ait-hook:' + name + ':start');
        } catch (e) {
            // devtools mark 실패는 부팅에 영향을 주면 안 되므로 무시
        }
    },

    /**
     * 훅 계측용 performance.mark 종료점을 남기고, 가능하면 measure까지 생성한다.
     */
    __AITDebugLog_MarkEnd: function(hookNamePtr) {
        try {
            if (typeof performance === 'undefined' || typeof performance.mark !== 'function') return;
            var name = UTF8ToString(hookNamePtr);
            var startMark = 'ait-hook:' + name + ':start';
            var endMark = 'ait-hook:' + name + ':end';
            performance.mark(endMark);
            if (typeof performance.measure === 'function') {
                performance.measure('ait-hook:' + name, startMark, endMark);
            }
        } catch (e) {
            console.warn('[AIT] hook mark 실패:', e);
        }
    },

    /**
     * wasm 스트리밍 컴파일 폴백 사유를 반환한다.
     * WebGL 템플릿이 window.__AIT_WASM_STREAMING_FALLBACK__ 에 짧은 사유 문자열을 설정해두면
     * 그 값을, 폴백이 없었거나(정상 스트리밍) 템플릿이 이 값을 채우지 않으면 빈 문자열을 반환한다.
     */
    __AITDebugLog_GetWasmStreamingFallbackReason: function() {
        var reason = '';
        try {
            var v = window.__AIT_WASM_STREAMING_FALLBACK__;
            if (typeof v === 'string' && v.length > 0) reason = v;
        } catch (e) {
            // window 접근 실패 시 빈 문자열로 폴백
        }
        var bufferSize = lengthBytesUTF8(reason) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(reason, buffer, bufferSize);
        return buffer;
    }
});
