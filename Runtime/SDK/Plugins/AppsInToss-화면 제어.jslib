/**
 * AppsInToss-화면 제어.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-24T10:39:15.951Z
 * Category: 화면 제어
 */

mergeInto(LibraryManager.library, {
    closeView: function(callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.closeView) {
            window.AppsInToss.closeView()
                .then(function(result) {
                    // 일반 케이스: 결과를 Unity AITCore로 전달
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify(result)
                    });

                    // AITCore.Instance.OnAITCallback 호출
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.error('closeView error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.closeView not available');
        }
    },

    setDeviceOrientation: function(options, callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.setDeviceOrientation) {
            window.AppsInToss.setDeviceOrientation(JSON.parse(UTF8ToString(options)))
                .then(function(result) {
                    // 일반 케이스: 결과를 Unity AITCore로 전달
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify(result)
                    });

                    // AITCore.Instance.OnAITCallback 호출
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.error('setDeviceOrientation error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.setDeviceOrientation not available');
        }
    },

    setIosSwipeGestureEnabled: function(options, callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.setIosSwipeGestureEnabled) {
            window.AppsInToss.setIosSwipeGestureEnabled(JSON.parse(UTF8ToString(options)))
                .then(function(result) {
                    // 일반 케이스: 결과를 Unity AITCore로 전달
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify(result)
                    });

                    // AITCore.Instance.OnAITCallback 호출
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.error('setIosSwipeGestureEnabled error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.setIosSwipeGestureEnabled not available');
        }
    },

    setScreenAwakeMode: function(options, callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.setScreenAwakeMode) {
            window.AppsInToss.setScreenAwakeMode(JSON.parse(UTF8ToString(options)))
                .then(function(result) {
                    // 일반 케이스: 결과를 Unity AITCore로 전달
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify(result)
                    });

                    // AITCore.Instance.OnAITCallback 호출
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.error('setScreenAwakeMode error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.setScreenAwakeMode not available');
        }
    },

    setSecureScreen: function(options, callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.setSecureScreen) {
            window.AppsInToss.setSecureScreen(JSON.parse(UTF8ToString(options)))
                .then(function(result) {
                    // 일반 케이스: 결과를 Unity AITCore로 전달
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify(result)
                    });

                    // AITCore.Instance.OnAITCallback 호출
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.error('setSecureScreen error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.setSecureScreen not available');
        }
    },

});
