/**
 * AppsInToss-Notification.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __NotificationRequestAgreement_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : undefined;
        if (optionsObj === null) optionsObj = undefined;

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NotificationRequestAgreement called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.Notification.requestAgreement({
                options: optionsObj,
                onEvent: function(data) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] NotificationRequestAgreement event:', data);
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] NotificationRequestAgreement error:', error);
                    var errorMessage = error instanceof Error ? error.message : String(error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: errorMessage,
                            errorName: (error && error.name && error.name !== 'Error') ? error.name : ''
                        })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
                }
            });

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] NotificationRequestAgreement error:', error);
            var errorMessage = error instanceof Error ? error.message : String(error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: errorMessage,
                    errorName: (error && error.name && error.name !== 'Error') ? error.name : ''
                })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __NotificationRequestAgreementIsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.Notification.requestAgreement.isSupported();
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
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __requestNotificationAgreement_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : undefined;
        if (optionsObj === null) optionsObj = undefined;

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] requestNotificationAgreement called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.requestNotificationAgreement({
                options: optionsObj,
                onEvent: function(data) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] requestNotificationAgreement event:', data);
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] requestNotificationAgreement error:', error);
                    var errorMessage = error instanceof Error ? error.message : String(error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: errorMessage,
                            errorName: (error && error.name && error.name !== 'Error') ? error.name : ''
                        })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
                }
            });

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] requestNotificationAgreement error:', error);
            var errorMessage = error instanceof Error ? error.message : String(error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: errorMessage,
                    errorName: (error && error.name && error.name !== 'Error') ? error.name : ''
                })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __requestNotificationAgreementIsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.requestNotificationAgreement.isSupported();
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
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

});
