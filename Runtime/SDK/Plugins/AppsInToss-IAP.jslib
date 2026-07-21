/**
 * AppsInToss-IAP.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __IAPCreateOneTimePurchaseOrder_Internal: function(params, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var parsedParams = JSON.parse(UTF8ToString(params));

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCreateOneTimePurchaseOrder called, id:', subId);

        try {
            var result = window.AppsInToss.IAP.createOneTimePurchaseOrder({
                options: Object.assign({}, parsedParams, {
                processProductGrant: function(data) {
                    return new Promise(function(resolve) {
                        var requestId = subId + '_processProductGrant_' + Date.now();
                        window.__AIT_NESTED_CALLBACKS = window.__AIT_NESTED_CALLBACKS || {};

                        // (A) Opt-in deadlock guard: when AITCore.NestedCallbackTimeoutMs > 0,
                        // arm a JS-side setTimeout. The JS event loop keeps firing (throttled)
                        // even while the native overlay freezes the Unity player loop, so this
                        // is the timeout that can actually break the circular deadlock. If it
                        // fires first, resolve the grant to false and drop the callback so a
                        // late C# response is a silent no-op.
                        // WARNING: resolving false here does NOT cancel the purchase — it may
                        // already have succeeded server-side. Reconcile "paid but not granted"
                        // orders via IAPGetCompletedOrRefundedOrders.
                        var timeoutMs = window.__AIT_NESTED_TIMEOUT_MS;
                        var timeoutId = null;
                        if (timeoutMs && timeoutMs > 0) {
                            timeoutId = setTimeout(function() {
                                if (window.__AIT_NESTED_CALLBACKS && window.__AIT_NESTED_CALLBACKS[requestId]) {
                                    delete window.__AIT_NESTED_CALLBACKS[requestId];
                                    // Not verbose-gated on purpose: the guard firing means the C#
                                    // callback never answered, which is always worth surfacing.
                                    console.warn('[AIT jslib] Nested callback timed out after ' + timeoutMs + 'ms, resolving false:', requestId);
                                    resolve(false);
                                }
                            }, timeoutMs);
                        }

                        window.__AIT_NESTED_CALLBACKS[requestId] = { resolve: resolve, timeoutId: timeoutId };

                        var payload = JSON.stringify({
                            RequestId: requestId,
                            CallbackId: subId,
                            CallbackName: 'processProductGrant',
                            Data: JSON.stringify(data)
                        });
                        SendMessage('AITCore', 'OnNestedCallback', payload);
                    });
                }
                }),
                onEvent: function(event) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCreateOneTimePurchaseOrder event:', event);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(event || {}),
                            error: ''
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCreateOneTimePurchaseOrder error:', error);
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
                    setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
                }
            });

            // cleanup 함수 저장
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = result;

        } catch (error) {
            console.error('[AIT jslib] IAPCreateOneTimePurchaseOrder error:', error);
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
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __IAPCreateSubscriptionPurchaseOrder_Internal: function(params, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var parsedParams = JSON.parse(UTF8ToString(params));

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCreateSubscriptionPurchaseOrder called, id:', subId);

        try {
            var result = window.AppsInToss.IAP.createSubscriptionPurchaseOrder({
                options: Object.assign({}, parsedParams, {
                processProductGrant: function(data) {
                    return new Promise(function(resolve) {
                        var requestId = subId + '_processProductGrant_' + Date.now();
                        window.__AIT_NESTED_CALLBACKS = window.__AIT_NESTED_CALLBACKS || {};

                        // (A) Opt-in deadlock guard: when AITCore.NestedCallbackTimeoutMs > 0,
                        // arm a JS-side setTimeout. The JS event loop keeps firing (throttled)
                        // even while the native overlay freezes the Unity player loop, so this
                        // is the timeout that can actually break the circular deadlock. If it
                        // fires first, resolve the grant to false and drop the callback so a
                        // late C# response is a silent no-op.
                        // WARNING: resolving false here does NOT cancel the purchase — it may
                        // already have succeeded server-side. Reconcile "paid but not granted"
                        // orders via IAPGetCompletedOrRefundedOrders.
                        var timeoutMs = window.__AIT_NESTED_TIMEOUT_MS;
                        var timeoutId = null;
                        if (timeoutMs && timeoutMs > 0) {
                            timeoutId = setTimeout(function() {
                                if (window.__AIT_NESTED_CALLBACKS && window.__AIT_NESTED_CALLBACKS[requestId]) {
                                    delete window.__AIT_NESTED_CALLBACKS[requestId];
                                    // Not verbose-gated on purpose: the guard firing means the C#
                                    // callback never answered, which is always worth surfacing.
                                    console.warn('[AIT jslib] Nested callback timed out after ' + timeoutMs + 'ms, resolving false:', requestId);
                                    resolve(false);
                                }
                            }, timeoutMs);
                        }

                        window.__AIT_NESTED_CALLBACKS[requestId] = { resolve: resolve, timeoutId: timeoutId };

                        var payload = JSON.stringify({
                            RequestId: requestId,
                            CallbackId: subId,
                            CallbackName: 'processProductGrant',
                            Data: JSON.stringify(data)
                        });
                        SendMessage('AITCore', 'OnNestedCallback', payload);
                    });
                }
                }),
                onEvent: function(event) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCreateSubscriptionPurchaseOrder event:', event);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(event || {}),
                            error: ''
                        })
                    });
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },
                onError: function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCreateSubscriptionPurchaseOrder error:', error);
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
                    setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
                }
            });

            // cleanup 함수 저장
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = result;

        } catch (error) {
            console.error('[AIT jslib] IAPCreateSubscriptionPurchaseOrder error:', error);
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
            setTimeout(function() { SendMessage('AITCore', 'OnAITEventCallback', payload); }, 0);
        }
    },

    __IAPGetProductItemList_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPGetProductItemList called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.IAP.getProductItemList();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getProductItemList returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getProductItemList did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getProductItemList resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getProductItemList rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getProductItemList sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __IAPGetPendingOrders_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPGetPendingOrders called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.IAP.getPendingOrders();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPendingOrders returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPendingOrders did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPendingOrders resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPendingOrders rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPendingOrders sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __IAPGetCompletedOrRefundedOrders_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPGetCompletedOrRefundedOrders called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.IAP.getCompletedOrRefundedOrders();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getCompletedOrRefundedOrders returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getCompletedOrRefundedOrders did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getCompletedOrRefundedOrders resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getCompletedOrRefundedOrders rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getCompletedOrRefundedOrders sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __IAPCompleteProductGrant_Internal: function(args_0, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCompleteProductGrant called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPCompleteProductGrant raw param args_0:', UTF8ToString(args_0));

        try {
            var promiseResult = window.AppsInToss.IAP.completeProductGrant(JSON.parse(UTF8ToString(args_0)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] completeProductGrant returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] completeProductGrant did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] completeProductGrant resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] completeProductGrant rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] completeProductGrant sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __IAPGetSubscriptionInfo_Internal: function(args_0, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPGetSubscriptionInfo called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] IAPGetSubscriptionInfo raw param args_0:', UTF8ToString(args_0));

        try {
            var promiseResult = window.AppsInToss.IAP.getSubscriptionInfo(JSON.parse(UTF8ToString(args_0)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getSubscriptionInfo returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getSubscriptionInfo did not return a Promise, sending immediate response');
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
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getSubscriptionInfo resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getSubscriptionInfo rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getSubscriptionInfo sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __AITRespondToNestedCallback: function(requestId, result) {
        var reqId = UTF8ToString(requestId);
        var resultBool = result !== 0;

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] RespondToNestedCallback:', reqId, resultBool);

        var entry = window.__AIT_NESTED_CALLBACKS && window.__AIT_NESTED_CALLBACKS[reqId];
        if (entry) {
            if (entry.timeoutId) { clearTimeout(entry.timeoutId); }
            delete window.__AIT_NESTED_CALLBACKS[reqId];
            entry.resolve(resultBool);
        } else {
            // Already settled by the (A) JS-side timeout, or never registered — silent no-op.
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] Nested callback already settled, ignoring:', reqId);
        }
    },

    __AITSetNestedCallbackTimeoutMs: function(timeoutMs) {
        window.__AIT_NESTED_TIMEOUT_MS = timeoutMs;
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] NestedCallbackTimeoutMs set:', timeoutMs);
    },

});
