/**
 * AppsInToss-SubmitGameCenterLeaderBoardScore.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __submitGameCenterLeaderBoardScore_Internal: function(params, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] submitGameCenterLeaderBoardScore called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.submitGameCenterLeaderBoardScore(JSON.parse(UTF8ToString(params)));
            console.log('[AIT jslib] submitGameCenterLeaderBoardScore returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                console.log('[AIT jslib] submitGameCenterLeaderBoardScore did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    console.log('[AIT jslib] submitGameCenterLeaderBoardScore resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT jslib] submitGameCenterLeaderBoardScore rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.log('[AIT jslib] submitGameCenterLeaderBoardScore sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

});
