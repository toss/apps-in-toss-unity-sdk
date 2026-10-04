/**
 * Apps in Toss Unity SDK - Memory Bridge JavaScript Bridge
 * 템플릿의 ait-mem.js(window.AITMemory)와 C# AITMemoryBridge 를 잇는다.
 */
mergeInto(LibraryManager.library, {
    /**
     * C# 이 이벤트를 받을 준비가 됐음을 알린다. 텔레메트리가 꺼져 있거나 ait-mem.js 가 없으면 0.
     * 등록되면 ait-mem.js 가 setTimeout 으로 현재 요약을 한 번 SendMessage 한다.
     */
    __AITMemoryBridge_Register: function() {
        try {
            if (window.AITMemory && typeof window.AITMemory.__bridgeRegister === 'function') {
                return window.AITMemory.__bridgeRegister() ? 1 : 0;
            }
        } catch (e) {
            console.warn('[AIT] memory bridge 등록 실패', e);
        }
        return 0;
    },

    /**
     * 현재 요약 JSON(type: "snapshot"). 텔레메트리가 꺼져 있으면 빈 문자열.
     */
    __AITMemoryBridge_GetSnapshot: function() {
        var s = '';
        try {
            if (window.AITMemory && typeof window.AITMemory.snapshotJson === 'function') {
                s = window.AITMemory.snapshotJson() || '';
            }
        } catch (e) {
            // 읽기 실패 시 빈 문자열
        }
        var bufferSize = lengthBytesUTF8(s) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(s, buffer, bufferSize);
        return buffer;
    }
});
