// AITGameBridge.jslib - game posture 픽스처의 상태 보고 브릿지
// Playwright(game-play.test.js)가 window.__AIT_GAME 을 폴링해 게임 상태를 검증한다.

mergeInto(LibraryManager.library, {
    /**
     * 게임 상태 JSON 을 window.__AIT_GAME 에 객체로 저장한다.
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    AITGame_Report: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        try {
            window.__AIT_GAME = JSON.parse(json);
            window.__AIT_GAME_REPORTS = (window.__AIT_GAME_REPORTS | 0) + 1;
            window.__AIT_GAME_TS = performance.now();
        } catch (e) {
            window.__AIT_GAME_PARSE_ERROR = String(e) + ' :: ' + json.substring(0, 200);
        }
    }
});
