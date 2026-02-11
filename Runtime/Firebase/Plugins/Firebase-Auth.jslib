/**
 * Firebase-Auth.jslib
 *
 * Firebase Auth bridge for Unity WebGL
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __Firebase_signInAnonymously_Internal: function(callbackId, typeName) {
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT Firebase] signInAnonymously called, callbackId:', callback);

        try {
            var promise = window.__AIT_Firebase.signInAnonymously();

            if (!promise || typeof promise.then !== 'function') {
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promise), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promise
                .then(function(result) {
                    console.log('[AIT Firebase] signInAnonymously resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT Firebase] signInAnonymously rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.error('[AIT Firebase] signInAnonymously error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

    __Firebase_signInWithCustomToken_Internal: function(token, callbackId, typeName) {
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        var tokenVal = UTF8ToString(token);

        console.log('[AIT Firebase] signInWithCustomToken called, callbackId:', callback);

        try {
            var promise = window.__AIT_Firebase.signInWithCustomToken(tokenVal);

            if (!promise || typeof promise.then !== 'function') {
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promise), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promise
                .then(function(result) {
                    console.log('[AIT Firebase] signInWithCustomToken resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT Firebase] signInWithCustomToken rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.error('[AIT Firebase] signInWithCustomToken error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

    __Firebase_signOut_Internal: function(callbackId, typeName) {
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT Firebase] signOut called, callbackId:', callback);

        try {
            var promise = window.__AIT_Firebase.signOut();

            if (!promise || typeof promise.then !== 'function') {
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promise), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promise
                .then(function(result) {
                    console.log('[AIT Firebase] signOut resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT Firebase] signOut rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.error('[AIT Firebase] signOut error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },

    __Firebase_onAuthStateChanged_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT Firebase] onAuthStateChanged subscribing, id:', subId);

        try {
            var unsubscribe = window.__AIT_Firebase.onAuthStateChanged(function(data) {
                console.log('[AIT Firebase] onAuthStateChanged event:', data);
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
            });

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT Firebase] onAuthStateChanged error:', error);
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

    __Firebase_Unsubscribe_Internal: function(subscriptionId) {
        var subId = UTF8ToString(subscriptionId);

        if (window.__AIT_SUBSCRIPTIONS && window.__AIT_SUBSCRIPTIONS[subId]) {
            console.log('[AIT Firebase] Unsubscribing:', subId);
            var unsubscribe = window.__AIT_SUBSCRIPTIONS[subId];
            if (typeof unsubscribe === 'function') {
                unsubscribe();
            }
            delete window.__AIT_SUBSCRIPTIONS[subId];
        }
    },

});
