/**
 * AppsInToss-Advertising.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __GoogleAdMobLoadAdMobInterstitialAd_Internal: function(args, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.GoogleAdMob.loadAdMobInterstitialAd(args);
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

    __GoogleAdMobShowAdMobInterstitialAd_Internal: function(args, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.GoogleAdMob.showAdMobInterstitialAd(args);
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

    __GoogleAdMobLoadAdMobRewardedAd_Internal: function(args, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.GoogleAdMob.loadAdMobRewardedAd(args);
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

    __GoogleAdMobShowAdMobRewardedAd_Internal: function(args, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.GoogleAdMob.showAdMobRewardedAd(args);
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

    __GoogleAdMobLoadAppsInTossAdMob_Internal: function(args, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.GoogleAdMob.loadAppsInTossAdMob(args);
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

    __GoogleAdMobShowAppsInTossAdMob_Internal: function(args, callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.GoogleAdMob.showAppsInTossAdMob(args);
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
