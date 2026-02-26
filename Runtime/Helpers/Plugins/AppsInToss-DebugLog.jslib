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
    }
});
