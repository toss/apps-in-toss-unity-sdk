/**
 * AppsInToss-TossAds.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __TossAdsInitialize_Internal: function(options, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.TossAds.initialize(JSON.parse(UTF8ToString(options)));
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

    __TossAdsAttach_Internal: function(adGroupId, target, options, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.TossAds.attach(UTF8ToString(adGroupId), target, options);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

    __TossAdsDestroy_Internal: function(slotId, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.TossAds.destroy(UTF8ToString(slotId));
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

    __TossAdsDestroyAll_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.TossAds.destroyAll();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

});
