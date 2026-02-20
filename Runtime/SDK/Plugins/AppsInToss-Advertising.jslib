/**
 * AppsInToss-Advertising.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __GoogleAdMobLoadAppsInTossAdMob_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : {};

        console.log('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.GoogleAdMob.loadAppsInTossAdMob({
                options: optionsObj,
                onEvent: function(data) {
                    console.log('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob event:', data);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(data || {}),
                            error: ''
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    console.log('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob error:', error);
                    var errorMessage = error instanceof Error ? error.message : String(error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: errorMessage
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                }
            });

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob error:', error);
            var errorMessage = error instanceof Error ? error.message : String(error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: errorMessage
                })
            });
            SendMessage('AITCore', 'OnAITEventCallback', payload);
        }
    },

    __GoogleAdMobShowAppsInTossAdMob_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : {};

        console.log('[AIT jslib] GoogleAdMobShowAppsInTossAdMob called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.GoogleAdMob.showAppsInTossAdMob({
                options: optionsObj,
                onEvent: function(data) {
                    console.log('[AIT jslib] GoogleAdMobShowAppsInTossAdMob event:', data);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(data || {}),
                            error: ''
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    console.log('[AIT jslib] GoogleAdMobShowAppsInTossAdMob error:', error);
                    var errorMessage = error instanceof Error ? error.message : String(error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: errorMessage
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                }
            });

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] GoogleAdMobShowAppsInTossAdMob error:', error);
            var errorMessage = error instanceof Error ? error.message : String(error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: errorMessage
                })
            });
            SendMessage('AITCore', 'OnAITEventCallback', payload);
        }
    },

});
