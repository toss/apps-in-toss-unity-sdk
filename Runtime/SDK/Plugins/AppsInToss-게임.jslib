/**
 * AppsInToss-게임.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-24T10:39:15.951Z
 * Category: 게임
 */

mergeInto(LibraryManager.library, {
    getUserKeyForGame: function(callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getUserKeyForGame) {
            window.AppsInToss.getUserKeyForGame()
                .then(function(result) {
                    // Discriminated Union 처리: 런타임 타입 체크
                    let resultPayload;
                    if (typeof result === 'string') {
                        // 에러 케이스 (문자열 리터럴)
                        resultPayload = {
                            _type: "error",
                            _errorCode: result,
                            _successJson: ""
                        };
                    } else {
                        // 성공 케이스 (객체)
                        resultPayload = {
                            _type: "success",
                            _successJson: JSON.stringify(result),
                            _errorCode: ""
                        };
                    }

                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify(resultPayload)
                    });

                    // AITCore.Instance.OnAITCallback 호출
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.error('getUserKeyForGame error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.getUserKeyForGame not available');
        }
    },

    grantPromotionRewardForGame: function(options, callbackId, typeName) {
        const callback = UTF8ToString(callbackId);
        const typeNameStr = UTF8ToString(typeName);
        // @apps-in-toss/web-framework API 호출
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.grantPromotionRewardForGame) {
            window.AppsInToss.grantPromotionRewardForGame(JSON.parse(UTF8ToString(options)))
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
                    console.error('grantPromotionRewardForGame error:', error);
                    const payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ error: error.message })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } else {
            console.warn('window.AppsInToss.grantPromotionRewardForGame not available');
        }
    },

});
