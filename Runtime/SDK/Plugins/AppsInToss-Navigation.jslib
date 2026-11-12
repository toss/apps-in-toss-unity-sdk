/**
 * AppsInToss-Navigation.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __NavigationBarAddAccessoryButton_Internal: function(options, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NavigationBarAddAccessoryButton called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NavigationBarAddAccessoryButton raw param options:', UTF8ToString(options));

        try {
            var promiseResult = window.AppsInToss.NavigationBar.addAccessoryButton(JSON.parse(UTF8ToString(options)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] addAccessoryButton returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] addAccessoryButton did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] addAccessoryButton resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] addAccessoryButton rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] addAccessoryButton sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __NavigationBarRemoveAccessoryButton_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NavigationBarRemoveAccessoryButton called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.NavigationBar.removeAccessoryButton();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] removeAccessoryButton returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] removeAccessoryButton did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] removeAccessoryButton resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] removeAccessoryButton rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] removeAccessoryButton sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __NavigationBarSubscribeAccessoryButton_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NavigationBarSubscribeAccessoryButton subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.NavigationBar.subscribeAccessoryButton({
                onEvent: function() {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] subscribeAccessoryButton fired (void)');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] subscribeAccessoryButton error:', error);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: false,
                            data: '',
                            error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : ''
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
            console.error('[AIT jslib] NavigationBarSubscribeAccessoryButton subscribe error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : ''
                })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __NavigationBarSetOptions_Internal: function(options, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NavigationBarSetOptions called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NavigationBarSetOptions raw param options:', UTF8ToString(options));

        try {
            var promiseResult = window.AppsInToss.NavigationBar.setOptions(JSON.parse(UTF8ToString(options)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] setOptions returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] setOptions did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] setOptions resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] setOptions rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] setOptions sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __closeView_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] closeView called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.closeView();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] closeView returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] closeView did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] closeView resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] closeView rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] closeView sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __openURL_Internal: function(url, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL raw param url:', UTF8ToString(url));

        try {
            var promiseResult = window.AppsInToss.openURL(UTF8ToString(url));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openURL sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error), errorName: (error && error.name && error.name !== 'Error') ? error.name : '' })
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
