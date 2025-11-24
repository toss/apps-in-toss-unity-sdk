/**
 * Apps in Toss Unity Bridge
 * Unity WebGL과 웹 플랫폼 간의 브릿지
 */

// ===========================================
// 환경 감지
// ===========================================
// Unity 빌드 설정에서 주입되는 값 (빌드 시 치환됨)
var AIT_BUILD_IS_PRODUCTION = '%AIT_IS_PRODUCTION%';

// 프로덕션 환경 감지: 빌드 설정만 사용
var IS_PRODUCTION = (AIT_BUILD_IS_PRODUCTION === 'true');

// 브라우저 타입 감지 함수
function detectBrowser() {
    var ua = navigator.userAgent;
    var browser = { name: 'Unknown', version: 'Unknown' };

    if (ua.indexOf('Chrome') > -1 && ua.indexOf('Edg') === -1 && ua.indexOf('OPR') === -1) {
        browser.name = 'Chrome';
        browser.version = ua.match(/Chrome\/(\d+)/)[1];
    } else if (ua.indexOf('Safari') > -1 && ua.indexOf('Chrome') === -1) {
        browser.name = 'Safari';
        browser.version = ua.match(/Version\/(\d+)/)?.[1] || 'Unknown';
    } else if (ua.indexOf('Firefox') > -1) {
        browser.name = 'Firefox';
        browser.version = ua.match(/Firefox\/(\d+)/)[1];
    } else if (ua.indexOf('Edg') > -1) {
        browser.name = 'Edge';
        browser.version = ua.match(/Edg\/(\d+)/)[1];
    } else if (ua.indexOf('OPR') > -1 || ua.indexOf('Opera') > -1) {
        browser.name = 'Opera';
        browser.version = ua.match(/(?:OPR|Opera)\/(\d+)/)[1];
    }

    // 모바일/데스크톱 감지
    browser.isMobile = /Mobile|Android|iPhone|iPad|iPod/i.test(ua);
    browser.isTablet = /iPad|Android(?!.*Mobile)/i.test(ua);
    browser.platform = browser.isMobile ? (browser.isTablet ? 'Tablet' : 'Mobile') : 'Desktop';

    // OS 감지
    if (ua.indexOf('Win') > -1) browser.os = 'Windows';
    else if (ua.indexOf('Mac') > -1) browser.os = 'macOS';
    else if (ua.indexOf('Linux') > -1) browser.os = 'Linux';
    else if (ua.indexOf('Android') > -1) browser.os = 'Android';
    else if (ua.indexOf('iOS') > -1 || ua.indexOf('iPhone') > -1 || ua.indexOf('iPad') > -1) browser.os = 'iOS';
    else browser.os = 'Unknown';

    return browser;
}

var browserInfo = detectBrowser();

// 🔥 초기 로그를 window에 저장 (Debug Console보다 먼저 실행되므로)
window._aitEarlyLogs = window._aitEarlyLogs || [];
window._aitEarlyLogs.push('========================================');
window._aitEarlyLogs.push('Apps in Toss Unity Bridge 시작');
window._aitEarlyLogs.push('환경: ' + (IS_PRODUCTION ? '프로덕션 ✅' : '개발 🛠️'));
window._aitEarlyLogs.push('호스트: ' + window.location.hostname);
window._aitEarlyLogs.push('브라우저: ' + browserInfo.name + ' ' + browserInfo.version);
window._aitEarlyLogs.push('OS: ' + browserInfo.os);
window._aitEarlyLogs.push('ReactNativeWebView: ' + (typeof window.ReactNativeWebView !== 'undefined' ? 'YES ✅' : 'NO ❌'));
window._aitEarlyLogs.push('GoogleAdMob 초기: ' + (typeof GoogleAdMob !== 'undefined' ? 'YES ✅' : 'NO ❌'));
window._aitEarlyLogs.push('========================================');

// 일반 콘솔에도 출력 (브라우저 개발자 도구용)
console.log('[AIT] ========================================');
console.log('[AIT] Apps in Toss Unity Bridge 시작');
console.log('[AIT] ========================================');
console.log('[AIT] 환경:', IS_PRODUCTION ? '프로덕션' : '개발');
console.log('[AIT] 호스트:', window.location.hostname);
console.log('[AIT] 브라우저:', browserInfo.name + ' ' + browserInfo.version);
console.log('[AIT] OS:', browserInfo.os);
console.log('[AIT] 플랫폼:', browserInfo.platform);
console.log('[AIT] 화면 크기:', screen.width + 'x' + screen.height + ' (Ratio: ' + (window.devicePixelRatio || 1) + ')');
console.log('[AIT] User Agent:', navigator.userAgent);
console.log('[AIT] GoogleAdMob 존재 여부:', typeof GoogleAdMob !== 'undefined' ? 'YES' : 'NO');
console.log('[AIT] GoogleAdMob 타입:', typeof GoogleAdMob);
console.log('[AIT] window.GoogleAdMob:', typeof window.GoogleAdMob);
console.log('[AIT] "GoogleAdMob" in window:', 'GoogleAdMob' in window ? 'YES' : 'NO');

// ReactNativeWebView 감지
console.log('[AIT] ReactNativeWebView 존재:', typeof window.ReactNativeWebView !== 'undefined' ? 'YES' : 'NO');
if (window.ReactNativeWebView) {
    console.log('[AIT] ReactNativeWebView.postMessage:', typeof window.ReactNativeWebView.postMessage !== 'undefined' ? 'YES' : 'NO');
}

// window 객체의 모든 Google/AdMob/AIT/Toss 관련 속성 출력
var relevantProps = [];
for (var key in window) {
    var lowerKey = key.toLowerCase();
    if (lowerKey.includes('google') || lowerKey.includes('admob') ||
        lowerKey.includes('ait') || lowerKey.includes('toss') ||
        lowerKey.includes('react')) {
        relevantProps.push(key);
    }
}
console.log('[AIT] 관련 전역 변수 (' + relevantProps.length + '개):', relevantProps.length > 0 ? relevantProps.join(', ') : '없음');

// 모든 script 태그 출력 (어떤 스크립트가 로드되었는지 확인)
var scripts = document.getElementsByTagName('script');
console.log('[AIT] 로드된 스크립트 (' + scripts.length + '개):');
for (var i = 0; i < scripts.length; i++) {
    var src = scripts[i].src || 'inline script';
    console.log('[AIT]   - ' + src);
}
console.log('[AIT] ========================================');

// ===========================================
// GoogleAdMob 로딩 감지 (지연 로딩 대응)
// ===========================================
// AIT 번들이 나중에 로드될 수 있으므로 주기적으로 체크
var googleAdMobCheckCount = 0;
var googleAdMobCheckInterval = setInterval(function() {
    googleAdMobCheckCount++;

    if (typeof GoogleAdMob !== 'undefined') {
        console.log('[AIT] ✓ GoogleAdMob 감지됨! (체크 횟수: ' + googleAdMobCheckCount + ')');
        console.log('[AIT] GoogleAdMob 타입:', typeof GoogleAdMob);
        console.log('[AIT] GoogleAdMob.loadAppsInTossAdMob:', typeof GoogleAdMob.loadAppsInTossAdMob);
        console.log('[AIT] GoogleAdMob.showAppsInTossAdMob:', typeof GoogleAdMob.showAppsInTossAdMob);

        // 진짜인지 Mock인지 확인
        if (GoogleAdMob.__isMock === true) {
            console.warn('[AIT] ⚠️⚠️⚠️ 이것은 Mock GoogleAdMob입니다 (개발 환경) ⚠️⚠️⚠️');
            console.warn('[AIT] Mock이 감지된 이유:');
            console.warn('[AIT]   - 호스트:', window.location.hostname);
            console.warn('[AIT]   - IS_PRODUCTION:', IS_PRODUCTION);
            console.warn('[AIT]   - ReactNativeWebView:', typeof window.ReactNativeWebView);
            window._aitEarlyLogs.push('🚨🚨🚨 Mock GoogleAdMob 감지됨! 🚨🚨🚨');
            window._aitEarlyLogs.push('→ 호스트: ' + window.location.hostname);
            window._aitEarlyLogs.push('→ ReactNativeWebView: ' + (typeof window.ReactNativeWebView));
        } else if (GoogleAdMob.loadAdMobInterstitialAd) {
            console.log('[AIT] ✅✅✅ 실제 AIT GoogleAdMob 감지됨! ✅✅✅');
            console.log('[AIT] 호스트:', window.location.hostname);
            window._aitEarlyLogs.push('✅ 실제 GoogleAdMob 감지됨!');
        } else {
            console.log('[AIT] ℹ️ GoogleAdMob 타입 불명 (추가 확인 필요)');
            console.log('[AIT] 사용 가능한 메서드:', Object.keys(GoogleAdMob));
            window._aitEarlyLogs.push('ℹ️ GoogleAdMob 타입 불명');
        }

        // window에도 노출
        if (typeof window.GoogleAdMob === 'undefined') {
            console.log('[AIT] → window.GoogleAdMob로 전역 노출 시도...');
            try {
                window.GoogleAdMob = GoogleAdMob;
                console.log('[AIT] ✓ window.GoogleAdMob 노출 성공!');
            } catch (err) {
                console.error('[AIT] ✗ window.GoogleAdMob 노출 실패:', err);
            }
        }

        clearInterval(googleAdMobCheckInterval);
    } else if (googleAdMobCheckCount >= 20) {
        console.log('[AIT] GoogleAdMob이 20초 후에도 감지되지 않음. 체크 중단.');
        clearInterval(googleAdMobCheckInterval);
    }
}, 1000); // 1초마다 체크

// ===========================================
// GoogleAdMob 확인 (프로덕션 환경)
// ===========================================
// 프로덕션 환경(Apps in Toss 앱)에서는 GoogleAdMob이 네이티브(React Native)에서 자동으로 주입됨
// Unity .jslib 파일에서 직접 GoogleAdMob.loadAppsInTossAdMob / GoogleAdMob.showAppsInTossAdMob 호출

if (IS_PRODUCTION) {
    console.log('[AIT] ========================================');
    console.log('[AIT] 프로덕션 환경 - GoogleAdMob 확인');
    console.log('[AIT] ========================================');
    console.log('[AIT] GoogleAdMob 존재:', typeof GoogleAdMob !== 'undefined' ? 'YES ✅' : 'NO ❌');

    if (typeof GoogleAdMob !== 'undefined') {
        console.log('[AIT] GoogleAdMob.loadAppsInTossAdMob:', typeof GoogleAdMob.loadAppsInTossAdMob === 'function' ? 'YES ✅' : 'NO ❌');
        console.log('[AIT] GoogleAdMob.showAppsInTossAdMob:', typeof GoogleAdMob.showAppsInTossAdMob === 'function' ? 'YES ✅' : 'NO ❌');

        if (GoogleAdMob.loadAppsInTossAdMob && GoogleAdMob.loadAppsInTossAdMob.isSupported) {
            console.log('[AIT] loadAppsInTossAdMob.isSupported:', GoogleAdMob.loadAppsInTossAdMob.isSupported() ? 'YES ✅' : 'NO ❌');
        }

        console.log('[AIT] ✅ 프로덕션 GoogleAdMob 준비 완료 - Unity .jslib에서 직접 호출 가능');
    } else {
        console.error('[AIT] ❌ 프로덕션 환경인데 GoogleAdMob이 없습니다!');
        console.error('[AIT] ❌ Apps in Toss 플랫폼에서 GoogleAdMob을 주입해야 합니다.');
        window._aitEarlyLogs.push('❌ 프로덕션: GoogleAdMob 없음');
    }
    console.log('[AIT] ========================================');
}

// ===========================================
// GoogleAdMob Mock (개발 환경용만)
// ===========================================
if (typeof GoogleAdMob === 'undefined') {
    // 🔥 프로덕션 환경에서는 Mock을 생성하지 않음
    if (IS_PRODUCTION) {
        console.error('[AIT] ❌ 프로덕션 환경인데 GoogleAdMob이 없습니다!');
        console.error('[AIT] Apps in Toss 플랫폼에서 GoogleAdMob을 주입해야 합니다.');
        window._aitEarlyLogs.push('❌ 프로덕션: GoogleAdMob 없음');
    }
    // ReactNativeWebView가 있는데 GoogleAdMob이 없으면 오류
    else if (window.ReactNativeWebView && window.ReactNativeWebView.postMessage) {
        console.error('[AIT] ❌❌❌ 치명적 오류 ❌❌❌');
        console.error('[AIT] GoogleAdMob이 없는데 ReactNativeWebView는 있습니다!');
        console.error('[AIT] 이것은 비정상 상태입니다. Native에서 GoogleAdMob을 제공해야 합니다.');
        console.error('[AIT] 호스트:', window.location.hostname);
        console.error('[AIT] 전체 URL:', window.location.href);
        window._aitEarlyLogs.push('💥💥💥 치명적 오류! 💥💥💥');
        window._aitEarlyLogs.push('→ GoogleAdMob 없지만 ReactNativeWebView는 존재');
        window._aitEarlyLogs.push('→ Native에서 GoogleAdMob 주입 필요!');
        window._aitEarlyLogs.push('→ 호스트: ' + window.location.hostname);
    }
    // 개발 환경에서만 Mock 생성
    else {
        console.log('[AIT Mock] GoogleAdMob 객체 생성 (개발 모드)');
        console.log('[AIT Mock] 호스트:', window.location.hostname);
        console.log('[AIT Mock] ReactNativeWebView 존재:', typeof window.ReactNativeWebView);
        window._aitEarlyLogs.push('🛠️ Mock GoogleAdMob 생성 (개발)');
        window._aitEarlyLogs.push('→ ReactNativeWebView 없음');

        window.GoogleAdMob = {
        __isMock: true,  // Mock 식별자
        loadAppsInTossAdMob: function(config) {
            console.log('[AIT Mock] ========================================');
            console.log('[AIT Mock] 🔴 loadAppsInTossAdMob called');
            console.log('[AIT Mock] ========================================');
            console.log('[AIT Mock] 🔴 adGroupId:', config.options ? config.options.adGroupId : 'unknown');
            console.log('[AIT Mock] 🔴 adType:', config.options ? config.options.adType : 'unknown');
            console.log('[AIT Mock] 🔴 onEvent 콜백 존재:', typeof config.onEvent === 'function' ? 'YES' : 'NO');
            console.log('[AIT Mock] 🔴 이것은 MOCK 광고입니다. 실제 앱에서는 보이면 안 됩니다!');
            console.log('[AIT Mock] ========================================');

            // isSupported 함수 제공
            if (!GoogleAdMob.loadAppsInTossAdMob.isSupported) {
                GoogleAdMob.loadAppsInTossAdMob.isSupported = function() {
                    return true; // 개발 모드에서는 항상 지원
                };
            }

            // 광고 로드 시뮬레이션
            console.log('[AIT Mock] 500ms 후 loaded 이벤트 발송 예정...');
            setTimeout(function() {
                console.log('[AIT Mock] ✅ loaded 이벤트 발송!');
                if (config.onEvent) {
                    console.log('[AIT Mock] → onEvent 콜백 호출 중...');
                    config.onEvent({
                        type: 'loaded',
                        data: { adGroupId: config.options.adGroupId }
                    });
                    console.log('[AIT Mock] → onEvent 콜백 호출 완료!');
                } else {
                    console.error('[AIT Mock] ✗ onEvent 콜백이 없습니다!');
                }
            }, 500);

            return function cleanup() {
                console.log('[AIT Mock] cleanup 호출');
            };
        },

        showAppsInTossAdMob: function(config) {
            console.log('[AIT Mock] 🔴 showAppsInTossAdMob called');
            console.log('[AIT Mock] 🔴 adGroupId:', config.options ? config.options.adGroupId : 'unknown');
            console.log('[AIT Mock] 🔴 이것은 MOCK 광고입니다. 실제 앱에서는 보이면 안 됩니다!');

            // isSupported 함수 제공
            if (!GoogleAdMob.showAppsInTossAdMob.isSupported) {
                GoogleAdMob.showAppsInTossAdMob.isSupported = function() {
                    return true; // 개발 모드에서는 항상 지원
                };
            }

            // 광고 타입 확인
            var isRewarded = config.options.adGroupId && config.options.adGroupId.includes('rewarded');

            // Mock 광고 모달 생성
            var adModal = document.createElement('div');
            adModal.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.95);z-index:99999;display:flex;flex-direction:column;justify-content:center;align-items:center;color:white;font-family:Arial,sans-serif;';

            var adContent = document.createElement('div');
            adContent.style.cssText = 'text-align:center;padding:40px;max-width:500px;';

            var adTitle = document.createElement('div');
            adTitle.textContent = isRewarded ? '🎁 보상형 광고 (Mock)' : '📢 전면 광고 (Mock)';
            adTitle.style.cssText = 'font-size:24px;font-weight:bold;margin-bottom:20px;';

            var adDescription = document.createElement('div');
            adDescription.textContent = isRewarded ? '광고를 시청하면 코인 100개를 받을 수 있습니다!' : '잠시 후 광고가 자동으로 닫힙니다.';
            adDescription.style.cssText = 'font-size:16px;margin-bottom:30px;color:#ccc;';

            var adImage = document.createElement('div');
            adImage.textContent = '🎮';
            adImage.style.cssText = 'font-size:80px;margin-bottom:20px;';

            var adInfo = document.createElement('div');
            adInfo.textContent = 'This is a MOCK advertisement for development';
            adInfo.style.cssText = 'font-size:12px;color:#666;margin-top:20px;';

            var closeButton = document.createElement('button');
            closeButton.textContent = '✕ 닫기';
            closeButton.style.cssText = 'margin-top:20px;padding:12px 24px;background:#444;color:white;border:2px solid #666;border-radius:8px;font-size:16px;cursor:pointer;';
            closeButton.onmouseover = function() { this.style.background = '#555'; };
            closeButton.onmouseout = function() { this.style.background = '#444'; };

            adContent.appendChild(adTitle);
            adContent.appendChild(adImage);
            adContent.appendChild(adDescription);
            adContent.appendChild(adInfo);
            adContent.appendChild(closeButton);
            adModal.appendChild(adContent);

            // 모달 닫기 함수
            var closeAd = function() {
                if (adModal.parentNode) {
                    document.body.removeChild(adModal);
                }
                if (config.onEvent) {
                    config.onEvent({ type: 'dismissed' });
                }
            };

            closeButton.onclick = closeAd;

            // 광고 표시 시뮬레이션
            setTimeout(function() {
                document.body.appendChild(adModal);

                if (config.onEvent) {
                    // 광고 표시 이벤트
                    config.onEvent({ type: 'show' });

                    // 보상형 광고면 보상 지급 (2초 후)
                    if (isRewarded) {
                        setTimeout(function() {
                            config.onEvent({
                                type: 'userEarnedReward',
                                data: {
                                    unitType: 'coins',
                                    unitAmount: 100
                                }
                            });

                            // 보상 알림 표시
                            adDescription.textContent = '✅ 보상을 받았습니다! 코인 +100';
                            adDescription.style.color = '#4ade80';
                        }, 2000);
                    }

                    // 자동으로 닫기 (보상형: 5초, 전면: 3초)
                    setTimeout(function() {
                        closeAd();
                    }, isRewarded ? 5000 : 3000);
                }
            }, 100);

            return function cleanup() {
                console.log('[AIT Mock] cleanup 호출');
                closeAd();
            };
        }
    };
    }  // else 블록 닫기
}

// ===========================================
// Advertisement State Helper (전역 함수)
// ===========================================
// .jslib 콜백에서 접근 가능한 전역 광고 상태 저장소
window._aitLoadedAds = {};

window.getLoadedAds = function() {
    return window._aitLoadedAds;
};

console.log('[AIT Bridge] 광고 상태 헬퍼 함수 초기화 완료');

// ===========================================
// AppsInTossAdPlugin - .jslib 사용 (Mock 비활성화)
// ===========================================
// Unity .jslib 파일이 빌드에 포함되어 있으므로 여기서는 Mock을 생성하지 않음
// .jslib의 AppsInTossAdPlugin이 자동으로 로드됨
console.log('[AIT Bridge] AppsInTossAdPlugin은 Unity .jslib에서 제공됩니다');

// 프로덕션 환경에서는 이미 존재하는 GoogleAdMob을 Unity .jslib에서 직접 호출함
// 스텁 함수 불필요

window.AppsInTossUnityBridge = (function() {
    'use strict';

    var unityInstance = null;
    var debugMode = true;

    function log(message, data) {
        if (debugMode) {
            console.log('[AIT Bridge]', message, data || '');
        }
    }

    function error(message, data) {
        console.error('[AIT Bridge Error]', message, data || '');
    }

    // Unity에 메시지 전송
    function sendMessageToUnity(gameObject, method, data) {
        if (!unityInstance) {
            error('Unity instance not initialized');
            return;
        }

        try {
            if (typeof data === 'object') {
                data = JSON.stringify(data);
            }
            unityInstance.SendMessage(gameObject, method, data || '');
            log('→ Unity:', { gameObject, method, data });
        } catch (e) {
            error('Failed to send message:', e);
        }
    }

    // 콜백 결과를 Unity로 전송
    function sendCallback(gameObject, callbackName, result) {
        sendMessageToUnity(gameObject, 'OnAITCallback', JSON.stringify({
            callbackName: callbackName,
            result: JSON.stringify(result)
        }));
    }

    // ===========================================
    // Storage API (실제 작동)
    // ===========================================

    function aitSetStorageData(key, value, gameObject, callback) {
        log('setStorageData:', { key, value });

        try {
            localStorage.setItem('ait_' + key, value);
            if (callback) {
                sendCallback(gameObject, callback, {
                    success: true,
                    message: 'Data saved'
                });
            }
        } catch (error) {
            if (callback) {
                sendCallback(gameObject, callback, {
                    success: false,
                    message: error.message
                });
            }
        }
    }

    function aitGetStorageData(key, gameObject, callback) {
        log('getStorageData:', key);

        try {
            var value = localStorage.getItem('ait_' + key) || '';
            if (callback) {
                sendCallback(gameObject, callback, {
                    success: true,
                    key: key,
                    value: value
                });
            }
        } catch (error) {
            if (callback) {
                sendCallback(gameObject, callback, {
                    success: false,
                    key: key,
                    value: '',
                    message: error.message
                });
            }
        }
    }

    function aitRemoveStorageData(key, gameObject, callback) {
        log('removeStorageData:', key);

        try {
            localStorage.removeItem('ait_' + key);
            if (callback) {
                sendCallback(gameObject, callback, {
                    success: true,
                    message: 'Data removed'
                });
            }
        } catch (error) {
            if (callback) {
                sendCallback(gameObject, callback, {
                    success: false,
                    message: error.message
                });
            }
        }
    }

    // ===========================================
    // UI: Toast & Dialog (실제 작동)
    // ===========================================

    function aitShowToast(options) {
        var opts = JSON.parse(options);
        log('showToast:', opts);

        var toast = document.createElement('div');
        toast.className = 'ait-toast ' + (opts.position || 'bottom');
        toast.textContent = opts.message;

        document.body.appendChild(toast);

        setTimeout(function() {
            toast.classList.add('show');
        }, 10);

        setTimeout(function() {
            toast.classList.remove('show');
            setTimeout(function() {
                if (toast.parentNode) {
                    document.body.removeChild(toast);
                }
            }, 300);
        }, opts.duration || 2000);
    }

    function aitShowDialog(options) {
        var opts = JSON.parse(options);
        log('showDialog:', opts);

        var modal = document.getElementById('ait-modal');
        if (!modal) {
            error('Modal element not found');
            return;
        }

        var title = document.getElementById('ait-modal-title');
        var message = document.getElementById('ait-modal-message');
        var confirmBtn = document.getElementById('ait-modal-confirm');
        var cancelBtn = document.getElementById('ait-modal-cancel');

        title.textContent = opts.title || '';
        message.textContent = opts.message || '';
        confirmBtn.textContent = opts.confirmText || '확인';
        cancelBtn.textContent = opts.cancelText || '취소';

        if (opts.showCancel) {
            cancelBtn.style.display = 'block';
        } else {
            cancelBtn.style.display = 'none';
        }

        confirmBtn.onclick = function() {
            modal.style.display = 'none';
            if (opts.confirmCallback) {
                sendCallback(opts.gameObject, opts.confirmCallback, { confirmed: true });
            }
        };

        cancelBtn.onclick = function() {
            modal.style.display = 'none';
            if (opts.cancelCallback) {
                sendCallback(opts.gameObject, opts.cancelCallback, { confirmed: false });
            }
        };

        modal.style.display = 'flex';
    }

    // ===========================================
    // Device: Haptic Feedback (실제 작동)
    // ===========================================

    function aitVibrate(type) {
        log('vibrate:', type);

        if (navigator.vibrate) {
            // type: 0=light(50ms), 1=medium(100ms), 2=heavy(200ms)
            var pattern = [50, 100, 200][type] || 50;
            navigator.vibrate(pattern);
        }
    }

    // ===========================================
    // Device: Network & Device Info (실제 작동)
    // ===========================================

    function aitGetNetworkType(gameObject, callback) {
        log('getNetworkType');

        var networkType = 'unknown';
        var isConnected = navigator.onLine;

        if (navigator.connection) {
            networkType = navigator.connection.effectiveType || navigator.connection.type || 'unknown';
        }

        if (callback) {
            sendCallback(gameObject, callback, {
                success: true,
                networkType: networkType,
                isConnected: isConnected
            });
        }
    }

    function aitGetDeviceInfo(gameObject, callback) {
        log('getDeviceInfo');

        var result = {
            success: true,
            platform: 'Web',
            system: navigator.platform,
            userAgent: navigator.userAgent,
            language: navigator.language,
            screenWidth: screen.width,
            screenHeight: screen.height,
            pixelRatio: window.devicePixelRatio || 1,
            isOnline: navigator.onLine
        };

        if (callback) {
            sendCallback(gameObject, callback, result);
        }
    }

    // ===========================================
    // Share API (실제 작동)
    // ===========================================

    function aitShareText(options) {
        var opts = JSON.parse(options);
        log('shareText:', opts);

        if (navigator.share) {
            navigator.share({
                title: opts.title,
                text: opts.text
            }).then(function() {
                if (opts.completeCallback) {
                    sendCallback(opts.gameObject, opts.completeCallback, {
                        success: true
                    });
                }
            }).catch(function() {
                if (opts.cancelCallback) {
                    sendCallback(opts.gameObject, opts.cancelCallback, {
                        success: false,
                        message: 'Share cancelled'
                    });
                }
            });
        } else {
            // Fallback: 클립보드 복사
            navigator.clipboard.writeText(opts.text).then(function() {
                alert('텍스트가 클립보드에 복사되었습니다.');
                if (opts.completeCallback) {
                    sendCallback(opts.gameObject, opts.completeCallback, {
                        success: true,
                        message: 'Copied to clipboard'
                    });
                }
            });
        }
    }

    // ===========================================
    // 개발 모드 전용 함수들 (모의 응답)
    // ===========================================

    function aitInit(gameObject, callback) {
        log('init (dev mode)');

        if (callback) {
            sendCallback(gameObject, callback, {
                success: true,
                message: 'Running in development mode',
                mode: 'development'
            });
        }
    }

    function aitCheckLoginStatus(gameObject, callback) {
        log('checkLoginStatus (dev mode)');

        if (callback) {
            sendCallback(gameObject, callback, {
                success: true,
                isLoggedIn: false
            });
        }
    }

    function aitGetUserInfo(gameObject, callback) {
        log('getUserInfo (dev mode)');

        if (callback) {
            sendCallback(gameObject, callback, {
                success: true,
                userId: 'dev_user_001',
                nickname: 'Dev User'
            });
        }
    }

    // ===========================================
    // 공개 인터페이스
    // ===========================================

    return {
        setUnityInstance: function(instance) {
            unityInstance = instance;
            log('Unity instance initialized');
        },

        setDebugMode: function(enabled) {
            debugMode = enabled;
            log('Debug mode:', enabled);
        },

        // 실제 작동하는 함수들
        aitSetStorageData: aitSetStorageData,
        aitGetStorageData: aitGetStorageData,
        aitRemoveStorageData: aitRemoveStorageData,
        aitShowToast: aitShowToast,
        aitShowDialog: aitShowDialog,
        aitVibrate: aitVibrate,
        aitGetNetworkType: aitGetNetworkType,
        aitGetDeviceInfo: aitGetDeviceInfo,
        aitShareText: aitShareText,

        // 개발 모드 함수들
        aitInit: aitInit,
        aitCheckLoginStatus: aitCheckLoginStatus,
        aitGetUserInfo: aitGetUserInfo
    };
})();

// 전역 함수로 등록 (Unity에서 직접 호출 가능)
(function() {
    var bridge = window.AppsInTossUnityBridge;

    // 실제 작동하는 함수들
    window.aitSetStorageData = function(key, value, gameObject, callback) {
        bridge.aitSetStorageData(key, value, gameObject, callback);
    };

    window.aitGetStorageData = function(key, gameObject, callback) {
        bridge.aitGetStorageData(key, gameObject, callback);
    };

    window.aitRemoveStorageData = function(key, gameObject, callback) {
        bridge.aitRemoveStorageData(key, gameObject, callback);
    };

    window.aitShowToast = function(options) {
        bridge.aitShowToast(options);
    };

    window.aitShowDialog = function(options) {
        bridge.aitShowDialog(options);
    };

    window.aitVibrate = function(type) {
        bridge.aitVibrate(type);
    };

    window.aitGetNetworkType = function(gameObject, callback) {
        bridge.aitGetNetworkType(gameObject, callback);
    };

    window.aitGetDeviceInfo = function(gameObject, callback) {
        bridge.aitGetDeviceInfo(gameObject, callback);
    };

    window.aitShareText = function(options) {
        bridge.aitShareText(options);
    };

    // 개발 모드 함수들
    window.aitInit = function(gameObject, callback) {
        bridge.aitInit(gameObject, callback);
    };

    window.aitCheckLoginStatus = function(gameObject, callback) {
        bridge.aitCheckLoginStatus(gameObject, callback);
    };

    window.aitGetUserInfo = function(gameObject, callback) {
        bridge.aitGetUserInfo(gameObject, callback);
    };

    if (IS_PRODUCTION) {
        console.log('✓ Apps in Toss Unity Bridge loaded (Production Mode)');
        console.log('  - GoogleAdMob: ' + (typeof GoogleAdMob !== 'undefined' ? '✓ Platform' : '✗ Not Available'));
        console.log('  - AppsInTossAdPlugin: ' + (typeof AppsInTossAdPlugin !== 'undefined' ? '✓ Platform' : '✗ Not Available'));
    } else {
        console.log('✓ Apps in Toss Unity Bridge loaded (Development Mode)');
        console.log('  - GoogleAdMob Mock: ' + (typeof GoogleAdMob !== 'undefined' ? '✓ Ready' : '✗ Not Created'));
        console.log('  - AppsInTossAdPlugin Mock: ' + (typeof AppsInTossAdPlugin !== 'undefined' ? '✓ Ready' : '✗ Not Created'));
    }
    console.log('  - Storage: ✓ Working');
    console.log('  - UI (Toast/Dialog): ✓ Working');
    console.log('  - Haptic Feedback: ✓ Working');
    console.log('  - Device Info: ✓ Working');
    console.log('  - Share: ✓ Working');
})();
