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
    }
});
