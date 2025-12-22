/**
 * AppsInToss-Advertising.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __GoogleAdMobLoadAdMobInterstitialAd_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : {};

        console.log('[AIT jslib] GoogleAdMobLoadAdMobInterstitialAd called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.GoogleAdMob.loadAdMobInterstitialAd({
                options: optionsObj.options || optionsObj,
                onEvent: function(data) {
                    console.log('[AIT jslib] GoogleAdMobLoadAdMobInterstitialAd event:', data);
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
                    console.log('[AIT jslib] GoogleAdMobLoadAdMobInterstitialAd error:', error);
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
            console.error('[AIT jslib] GoogleAdMobLoadAdMobInterstitialAd error:', error);
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

    __GoogleAdMobShowAdMobInterstitialAd_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : {};

        console.log('[AIT jslib] GoogleAdMobShowAdMobInterstitialAd called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.GoogleAdMob.showAdMobInterstitialAd({
                options: optionsObj.options || optionsObj,
                onEvent: function(data) {
                    console.log('[AIT jslib] GoogleAdMobShowAdMobInterstitialAd event:', data);
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
                    console.log('[AIT jslib] GoogleAdMobShowAdMobInterstitialAd error:', error);
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
            console.error('[AIT jslib] GoogleAdMobShowAdMobInterstitialAd error:', error);
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

    __GoogleAdMobLoadAdMobRewardedAd_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : {};

        console.log('[AIT jslib] GoogleAdMobLoadAdMobRewardedAd called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.GoogleAdMob.loadAdMobRewardedAd({
                options: optionsObj.options || optionsObj,
                onEvent: function(data) {
                    console.log('[AIT jslib] GoogleAdMobLoadAdMobRewardedAd event:', data);
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
                    console.log('[AIT jslib] GoogleAdMobLoadAdMobRewardedAd error:', error);
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
            console.error('[AIT jslib] GoogleAdMobLoadAdMobRewardedAd error:', error);
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

    __GoogleAdMobShowAdMobRewardedAd_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsObj = options ? JSON.parse(UTF8ToString(options)) : {};

        console.log('[AIT jslib] GoogleAdMobShowAdMobRewardedAd called, id:', subId, 'options:', optionsObj);

        try {
            var unsubscribe = window.AppsInToss.GoogleAdMob.showAdMobRewardedAd({
                options: optionsObj.options || optionsObj,
                onEvent: function(data) {
                    console.log('[AIT jslib] GoogleAdMobShowAdMobRewardedAd event:', data);
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
                    console.log('[AIT jslib] GoogleAdMobShowAdMobRewardedAd error:', error);
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
            console.error('[AIT jslib] GoogleAdMobShowAdMobRewardedAd error:', error);
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

    __GoogleAdMobLoadAppsInTossAdMob_Internal: function(options, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);
        var optionsStr = options ? UTF8ToString(options) : null;
        var optionsObj = optionsStr ? JSON.parse(optionsStr) : {};

        console.log('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob called');
        console.log('[AIT jslib]   subscriptionId:', subId);
        console.log('[AIT jslib]   raw options string:', optionsStr);
        console.log('[AIT jslib]   parsed optionsObj:', JSON.stringify(optionsObj, null, 2));
        console.log('[AIT jslib]   optionsObj.options:', JSON.stringify(optionsObj.options, null, 2));

        var apiOptions = optionsObj.options || optionsObj;
        console.log('[AIT jslib]   final apiOptions:', JSON.stringify(apiOptions, null, 2));

        var apiPayload = { options: apiOptions };
        console.log('[AIT jslib]   full API payload:', JSON.stringify(apiPayload, null, 2));

        try {
            console.log('[AIT jslib]   calling window.AppsInToss.GoogleAdMob.loadAppsInTossAdMob...');
            var unsubscribe = window.AppsInToss.GoogleAdMob.loadAppsInTossAdMob({
                options: apiOptions,
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
                    console.log('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob onError callback:');
                    console.log('[AIT jslib]   error type:', typeof error);
                    console.log('[AIT jslib]   error:', error);
                    console.log('[AIT jslib]   error.message:', error?.message);
                    console.log('[AIT jslib]   error.code:', error?.code);
                    console.log('[AIT jslib]   error stringified:', JSON.stringify(error, null, 2));
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

            console.log('[AIT jslib]   loadAppsInTossAdMob returned, unsubscribe:', typeof unsubscribe);

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] GoogleAdMobLoadAppsInTossAdMob catch error:');
            console.error('[AIT jslib]   error type:', typeof error);
            console.error('[AIT jslib]   error:', error);
            console.error('[AIT jslib]   error.stack:', error?.stack);
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
        var optionsStr = options ? UTF8ToString(options) : null;
        var optionsObj = optionsStr ? JSON.parse(optionsStr) : {};

        console.log('[AIT jslib] GoogleAdMobShowAppsInTossAdMob called');
        console.log('[AIT jslib]   subscriptionId:', subId);
        console.log('[AIT jslib]   raw options string:', optionsStr);
        console.log('[AIT jslib]   parsed optionsObj:', JSON.stringify(optionsObj, null, 2));
        console.log('[AIT jslib]   optionsObj.options:', JSON.stringify(optionsObj.options, null, 2));

        var apiOptions = optionsObj.options || optionsObj;
        console.log('[AIT jslib]   final apiOptions:', JSON.stringify(apiOptions, null, 2));

        var apiPayload = { options: apiOptions };
        console.log('[AIT jslib]   full API payload:', JSON.stringify(apiPayload, null, 2));

        try {
            console.log('[AIT jslib]   calling window.AppsInToss.GoogleAdMob.showAppsInTossAdMob...');
            var unsubscribe = window.AppsInToss.GoogleAdMob.showAppsInTossAdMob({
                options: apiOptions,
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
                    console.log('[AIT jslib] GoogleAdMobShowAppsInTossAdMob onError callback:');
                    console.log('[AIT jslib]   error type:', typeof error);
                    console.log('[AIT jslib]   error:', error);
                    console.log('[AIT jslib]   error.message:', error?.message);
                    console.log('[AIT jslib]   error.code:', error?.code);
                    console.log('[AIT jslib]   error stringified:', JSON.stringify(error, null, 2));
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

            console.log('[AIT jslib]   showAppsInTossAdMob returned, unsubscribe:', typeof unsubscribe);

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] GoogleAdMobShowAppsInTossAdMob catch error:');
            console.error('[AIT jslib]   error type:', typeof error);
            console.error('[AIT jslib]   error:', error);
            console.error('[AIT jslib]   error.stack:', error?.stack);
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

    __loadFullScreenAd_Internal: function(adGroupId, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        var adGroupIdVal = UTF8ToString(adGroupId);

        console.log('[AIT jslib] loadFullScreenAd called, id:', subId);

        try {
            var unsubscribe = window.AppsInToss.loadFullScreenAd({
                options: { adGroupId: adGroupIdVal },
                onEvent: function(data) {
                    console.log('[AIT jslib] loadFullScreenAd event:', data);
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
                    console.log('[AIT jslib] loadFullScreenAd error:', error);
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
            console.error('[AIT jslib] loadFullScreenAd error:', error);
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

    __showFullScreenAd_Internal: function(adGroupId, subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        var adGroupIdVal = UTF8ToString(adGroupId);

        console.log('[AIT jslib] showFullScreenAd called, id:', subId);

        try {
            var unsubscribe = window.AppsInToss.showFullScreenAd({
                options: { adGroupId: adGroupIdVal },
                onEvent: function(data) {
                    console.log('[AIT jslib] showFullScreenAd event:', data);
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
                    console.log('[AIT jslib] showFullScreenAd error:', error);
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
            console.error('[AIT jslib] showFullScreenAd error:', error);
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
