/**
 * Firebase-Init.jslib
 *
 * Firebase Init bridge for Unity WebGL
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __Firebase_initializeApp_Internal: function(callbackId, typeName) {
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT Firebase] initializeApp called, callbackId:', callback);

        try {
            var promise = window.__AIT_Firebase.initializeApp();

            if (!promise || typeof promise.then !== 'function') {
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promise), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promise
                .then(function(result) {
                    console.log('[AIT Firebase] initializeApp resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT Firebase] initializeApp rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.error('[AIT Firebase] initializeApp error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

});
