/**
 * AppsInToss-AppEvents.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __AppsInTossEventSubscribeEntryMessageExited_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] AppsInTossEventSubscribeEntryMessageExited subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.appsInTossEvent.addEventListener('entryMessageExited', {
                onEvent: function() {
                    console.log('[AIT jslib] entryMessageExited fired (void)');
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: null,
                            error: ''
                        })
                    });
                    // Event callbacks go to OnAITEventCallback (persistent)
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    console.log('[AIT jslib] entryMessageExited error:', error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: error.message || String(error)
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                }
            });

            // Store unsubscribe function for later cleanup
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] AppsInTossEventSubscribeEntryMessageExited subscribe error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error)
                })
            });
            SendMessage('AITCore', 'OnAITEventCallback', payload);
        }
    },

    __GraniteEventSubscribeBackEvent_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] GraniteEventSubscribeBackEvent subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.graniteEvent.addEventListener('backEvent', {
                onEvent: function() {
                    console.log('[AIT jslib] backEvent fired (void)');
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: null,
                            error: ''
                        })
                    });
                    // Event callbacks go to OnAITEventCallback (persistent)
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    console.log('[AIT jslib] backEvent error:', error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: error.message || String(error)
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                }
            });

            // Store unsubscribe function for later cleanup
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] GraniteEventSubscribeBackEvent subscribe error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error)
                })
            });
            SendMessage('AITCore', 'OnAITEventCallback', payload);
        }
    },

    __TdsEventSubscribeNavigationAccessoryEvent_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] TdsEventSubscribeNavigationAccessoryEvent subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.tdsEvent.addEventListener('navigationAccessoryEvent', {
                onEvent: function(data) {
                    console.log('[AIT jslib] navigationAccessoryEvent fired:', data);
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
                    console.log('[AIT jslib] navigationAccessoryEvent error:', error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: error.message || String(error)
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                }
            });

            // Store unsubscribe function for later cleanup
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] TdsEventSubscribeNavigationAccessoryEvent subscribe error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error)
                })
            });
            SendMessage('AITCore', 'OnAITEventCallback', payload);
        }
    },

    __AITUnsubscribe_Internal: function(subscriptionId) {
        var subId = UTF8ToString(subscriptionId);

        if (window.__AIT_SUBSCRIPTIONS && window.__AIT_SUBSCRIPTIONS[subId]) {
            console.log('[AIT jslib] Unsubscribing:', subId);
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
