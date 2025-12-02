/**
 * AppsInToss-OpenGameCenterLeaderboard.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-12-01T09:35:24.817Z
 * Category: OpenGameCenterLeaderboard
 */

mergeInto(LibraryManager.library, {
    __openGameCenterLeaderboard_Internal: function(callbackId, typeName) {
        // @apps-in-toss/web-framework 직접 호출 (bridge-core 패턴)
        // 모든 API가 비동기이므로 에러를 callback으로 전달
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);
        var eventId = Math.random().toString(36).substring(2, 15);
        var emitter = window.__GRANITE_NATIVE_EMITTER;
        var webView = window.ReactNativeWebView;

        // bridge-core 에러 체크: 플랫폼 미지원 시 throw 대신 callback으로 에러 전달
        if (!emitter || !webView) {
            var errorMessage = !emitter
                ? '__GRANITE_NATIVE_EMITTER is not available'
                : 'ReactNativeWebView is not available in browser environment';

            var errorPayload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ error: errorMessage })
            });
            SendMessage('AITCore', 'OnAITCallback', errorPayload);
            return;
        }

        // 응답 리스너 등록
        var removeResolve = emitter.on('openGameCenterLeaderboard/resolve/' + eventId, function(result) {
            removeResolve();
            removeReject();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify(result)
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        });

        var removeReject = emitter.on('openGameCenterLeaderboard/reject/' + eventId, function(error) {
            removeResolve();
            removeReject();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        });

        // Native로 메시지 전송
        webView.postMessage(JSON.stringify({
            type: 'method',
            functionName: 'openGameCenterLeaderboard',
            eventId: eventId,
            args: []
        }));
    },

});
