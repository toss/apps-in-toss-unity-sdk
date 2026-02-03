/**
 * AppsInToss-GetUserKeyForGame.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __getUserKeyForGame_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] getUserKeyForGame called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.getUserKeyForGame();
            console.log('[AIT jslib] getUserKeyForGame returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                console.log('[AIT jslib] getUserKeyForGame did not return a Promise, sending immediate response');
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
                    console.log('[AIT jslib] getUserKeyForGame resolved:', result);
                    var resultPayload;
                    if (typeof result === 'string') {
                        resultPayload = { _type: "error", _errorCode: result, _successJson: null };
                    } else {
                        resultPayload = { _type: "success", _successJson: result, _errorCode: "" };
                    }
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(resultPayload), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT jslib] getUserKeyForGame rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.log('[AIT jslib] getUserKeyForGame sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

});
