/**
 * Apps in Toss Unity SDK - Visibility Helper JavaScript Bridge
 * 브라우저 visibilitychange 이벤트를 Unity에 전달
 */
mergeInto(LibraryManager.library, {
    __AITVisibilityHelper_GetIsVisible_Internal: function() {
        // 최초 호출 시 이벤트 리스너 등록 (lazy initialization)
        if (!window.__aitVisibilityInitialized) {
            window.__aitVisibilityInitialized = true;
            document.addEventListener('visibilitychange', function() {
                var isVisible = !document.hidden;
                SendMessage('AITCore', 'OnVisibilityStateChanged',
                    JSON.stringify({ isVisible: isVisible }));
            });
        }
        return document.hidden ? 0 : 1;
    }
});
