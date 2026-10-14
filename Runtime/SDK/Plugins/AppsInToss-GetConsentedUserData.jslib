/**
 * AppsInToss-GetConsentedUserData.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __getConsentedUserData_Internal: function(options, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData raw param options:', UTF8ToString(options));

        try {
            var promiseResult = window.AppsInToss.getConsentedUserData(JSON.parse(UTF8ToString(options)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getConsentedUserData sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

});
