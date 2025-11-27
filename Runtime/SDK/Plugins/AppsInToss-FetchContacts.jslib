/**
 * AppsInToss-FetchContacts.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-27T13:37:25.164Z
 * Category: FetchContacts
 */

mergeInto(LibraryManager.library, {
    __fetchContacts_Internal: function(options, callbackId, typeName) {
        // @apps-in-toss/web-framework 직접 호출 (bridge-core 패턴)
        // bridge-core와 동일한 에러 메시지 사용
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);
        var eventId = Math.random().toString(36).substring(2, 15);
        var emitter = window.__GRANITE_NATIVE_EMITTER;
        var webView = window.ReactNativeWebView;

        // bridge-core 에러 체크: __GRANITE_NATIVE_EMITTER
        if (!emitter) {
            throw new Error('__GRANITE_NATIVE_EMITTER is not available');
        }

        // bridge-core 에러 체크: ReactNativeWebView
        if (!webView) {
            throw new Error('ReactNativeWebView is not available in browser environment');
        }

        // 응답 리스너 등록
        var removeResolve = emitter.on('fetchContacts/resolve/' + eventId, function(result) {
            removeResolve();
            removeReject();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify(result)
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        });

        var removeReject = emitter.on('fetchContacts/reject/' + eventId, function(error) {
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
            functionName: 'fetchContacts',
            eventId: eventId,
            args: [JSON.parse(UTF8ToString(options))]
        }));
    },

});
