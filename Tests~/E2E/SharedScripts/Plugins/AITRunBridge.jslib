// AITRunBridge.jslib - mobilegame posture 픽스처(탭 점프 러너)의 상태 보고 브릿지
// Playwright(mobile-game.test.js)가 window.__AIT_RUN 을 폴링해 게임 상태와 프레임 통계를 읽는다.

mergeInto(LibraryManager.library, {
    /**
     * 게임 상태 JSON 을 window.__AIT_RUN 에 객체로 저장한다.
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    AITRun_Report: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        try {
            var s = JSON.parse(json);
            if (window.__AIT_RUN_FIRST_TITLE_MS === undefined && s.state === 'title') {
                window.__AIT_RUN_FIRST_TITLE_MS = performance.now();
            }
            window.__AIT_RUN = s;
            window.__AIT_RUN_REPORTS = (window.__AIT_RUN_REPORTS | 0) + 1;
        } catch (e) {
            window.__AIT_RUN_PARSE_ERROR = String(e) + ' :: ' + json.substring(0, 200);
        }
    }
});
