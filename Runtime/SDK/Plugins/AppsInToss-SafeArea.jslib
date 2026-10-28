/**
 * AppsInToss-SafeArea.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __SafeAreaGet_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.SafeArea.get();
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
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __SafeAreaSubscribe_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] SafeAreaSubscribe subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.SafeArea.subscribe({
                onEvent: function(data) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] subscribe fired:', data);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(data || {}),
                            error: ''
                        })
                    });
                    // Event callbacks go to OnAITEventCallback (persistent)
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] subscribe error:', error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: error.message || String(error)
                        })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
                }
            });

            // Store unsubscribe function for later cleanup
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] SafeAreaSubscribe subscribe error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error)
                })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __SafeAreaInsetsGet_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.SafeAreaInsets.get();
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
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __SafeAreaInsetsSubscribe_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] SafeAreaInsetsSubscribe subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.SafeAreaInsets.subscribe({
                onEvent: function(data) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] subscribe fired:', data);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(data || {}),
                            error: ''
                        })
                    });
                    // Event callbacks go to OnAITEventCallback (persistent)
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] subscribe error:', error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: error.message || String(error)
                        })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
                }
            });

            // Store unsubscribe function for later cleanup
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] SafeAreaInsetsSubscribe subscribe error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error)
                })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __getSafeAreaInsets_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.getSafeAreaInsets();
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
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __AITUnsubscribe_Internal: function(subscriptionId) {
        var subId = UTF8ToString(subscriptionId);

        if (window.__AIT_SUBSCRIPTIONS && window.__AIT_SUBSCRIPTIONS[subId]) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] Unsubscribing:', subId);
            var unsubscribe = window.__AIT_SUBSCRIPTIONS[subId];
            if (typeof unsubscribe === 'function') {
                unsubscribe();
            }
            delete window.__AIT_SUBSCRIPTIONS[subId];
        } else {
            console.warn('[AIT jslib] Unknown subscription:', subId);
        }
    },

});
