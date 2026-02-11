/**
 * Firebase-Analytics.jslib
 *
 * Firebase Analytics bridge for Unity WebGL
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __Firebase_logEvent_Internal: function(eventName, eventParams) {
        var eventNameVal = UTF8ToString(eventName);
        var eventParamsVal = eventParams ? JSON.parse(UTF8ToString(eventParams)) : null;

        console.log('[AIT Firebase] logEvent called');

        try {
            window.__AIT_Firebase.logEvent(eventNameVal, eventParamsVal);
        } catch (error) {
            console.error('[AIT Firebase] logEvent error:', error);
        }
    },

    __Firebase_setUserId_Internal: function(userId) {
        var userIdVal = UTF8ToString(userId);

        console.log('[AIT Firebase] setUserId called');

        try {
            window.__AIT_Firebase.setUserId(userIdVal);
        } catch (error) {
            console.error('[AIT Firebase] setUserId error:', error);
        }
    },

    __Firebase_setUserProperties_Internal: function(properties) {
        var propertiesVal = properties ? JSON.parse(UTF8ToString(properties)) : null;

        console.log('[AIT Firebase] setUserProperties called');

        try {
            window.__AIT_Firebase.setUserProperties(propertiesVal);
        } catch (error) {
            console.error('[AIT Firebase] setUserProperties error:', error);
        }
    },

    __Firebase_setAnalyticsCollectionEnabled_Internal: function(enabled) {
        var enabledVal = enabled !== 0;

        console.log('[AIT Firebase] setAnalyticsCollectionEnabled called');

        try {
            window.__AIT_Firebase.setAnalyticsCollectionEnabled(enabledVal);
        } catch (error) {
            console.error('[AIT Firebase] setAnalyticsCollectionEnabled error:', error);
        }
    },

});
