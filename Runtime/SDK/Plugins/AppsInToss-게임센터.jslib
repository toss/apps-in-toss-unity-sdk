/**
 * AppsInToss-게임센터.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-24T10:39:15.951Z
 * Category: 게임센터
 */

mergeInto(LibraryManager.library, {
    getGameCenterGameProfile: function(callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getGameCenterGameProfile) {
            window.AppsInToss.getGameCenterGameProfile()
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
                    console.error('getGameCenterGameProfile error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.getGameCenterGameProfile not available');
        }
    },

    openGameCenterLeaderboard: function(callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.openGameCenterLeaderboard) {
            window.AppsInToss.openGameCenterLeaderboard()
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
                    console.error('openGameCenterLeaderboard error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.openGameCenterLeaderboard not available');
        }
    },

    submitGameCenterLeaderBoardScore: function(params, callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.submitGameCenterLeaderBoardScore) {
            window.AppsInToss.submitGameCenterLeaderBoardScore(JSON.parse(UTF8ToString(params)))
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
                    console.error('submitGameCenterLeaderBoardScore error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.submitGameCenterLeaderBoardScore not available');
        }
    },

});
