// E2ETestBridge.jslib - E2E 테스트용 JavaScript 브릿지
// Unity WebGL 빌드에서 Playwright 테스트로 데이터를 전송하는 용도

mergeInto(LibraryManager.library, {
    /**
     * 벤치마크 결과를 window 객체에 저장하고 콘솔에 출력
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendBenchmarkData: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-BENCHMARK] ' + json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_BENCHMARK_DATA__ = json;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-benchmark-complete', { detail: json });
        window.dispatchEvent(event);
    },

    /**
     * API 테스트 결과를 window 객체에 저장하고 콘솔에 출력
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendAPITestResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-API-TEST] ' + json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_API_TEST_DATA__ = json;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-api-test-complete', { detail: json });
        window.dispatchEvent(event);
    },

    /**
     * 직렬화 테스트 결과를 window 객체에 저장하고 콘솔에 출력
     * @param {string} jsonPtr - JSON 문자열 포인터
     */
    SendSerializationTestResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-SERIALIZATION-TEST] ' + json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_SERIALIZATION_TEST_DATA__ = json;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-serialization-complete', { detail: json });
        window.dispatchEvent(event);
    },

    /**
     * PlayerPrefs 테스트 결과를 window 객체에 저장하고 콘솔에 출력
     * (PlayerPrefs → 앱인토스 Storage 영속화 E2E 검증용)
     * @param {string} jsonPtr - JSON 문자열 포인터 ({ op, key, value, success })
     */
    SendPlayerPrefsResult: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        console.log('[E2E-PLAYERPREFS] ' + json);

        var data = JSON.parse(json);

        // window 객체에 저장하여 Playwright에서 접근 가능하도록 함
        window.__E2E_PLAYERPREFS_DATA__ = data;

        // CustomEvent 발생
        var event = new CustomEvent('e2e-playerprefs-complete', { detail: data });
        window.dispatchEvent(event);
    },

    // =====================================================
    // PlayerPrefs 실기기 실측 진단 (Storage 영속화 레이어)
    // =====================================================

    /**
     * window.AITPlayerPrefs.status()와 window.__AIT_PP 진단 정보를 JSON 문자열로 반환
     * 전역이 없으면(레이어 미주입 빌드 등) '레이어 없음' 문자열을 그대로 반환
     * @returns {string} - JSON 문자열 또는 '레이어 없음'
     */
    PP_GetDiagnosticsStatusJson: function() {
        var s;
        if (typeof window.AITPlayerPrefs === 'undefined' || typeof window.AITPlayerPrefs.status !== 'function') {
            s = '레이어 없음';
        } else {
            try {
                var pp = (typeof window.__AIT_PP !== 'undefined') ? window.__AIT_PP : null;
                var ppInfo = pp ? {
                    captured: pp.captured,
                    preRunRan: pp.preRunRan,
                    mode: pp.mode,
                    persistCount: pp.persistCount,
                    persistIdle: (typeof pp.persistIdle === 'function') ? pp.persistIdle() : null
                } : null;
                s = JSON.stringify({ status: window.AITPlayerPrefs.status(), pp: ppInfo });
            } catch (e) {
                s = 'status 조회 예외: ' + e.message;
            }
        }

        var bufferSize = lengthBytesUTF8(s) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(s, buffer, bufferSize);
        return buffer;
    },

    /**
     * 백그라운드 전환(visibilitychange)과 pagehide 이벤트를 window.__AIT_PP_VISLOG(최대 50개)에 기록하도록 등록
     * 중복 호출해도 리스너가 두 번 붙지 않도록 가드함 (SetupUI가 재호출될 수 있음)
     */
    PP_InitVisibilityLog: function() {
        if (window.__AIT_PP_VISLOG_INIT) return;
        window.__AIT_PP_VISLOG_INIT = true;
        window.__AIT_PP_VISLOG = window.__AIT_PP_VISLOG || [];

        var pushVisEvent = function(ev) {
            window.__AIT_PP_VISLOG.push({ ev: ev, ts: Date.now(), vis: document.visibilityState });
            while (window.__AIT_PP_VISLOG.length > 50) {
                window.__AIT_PP_VISLOG.shift();
            }
        };

        document.addEventListener('visibilitychange', function() {
            pushVisEvent(document.visibilityState === 'hidden' ? 'hidden' : 'visible');
        });
        window.addEventListener('pagehide', function() {
            pushVisEvent('pagehide');
        });

        console.log('[E2E-PLAYERPREFS] Visibility log initialized');
    },

    /**
     * 기록된 백그라운드 전환 로그 + (있으면) __AIT_PP.persistCount를 JSON 문자열로 반환
     * @returns {string} - { log: [...], persistCount } JSON 문자열
     */
    PP_GetVisibilityLogJson: function() {
        var log = window.__AIT_PP_VISLOG || [];
        var persistCount = (typeof window.__AIT_PP !== 'undefined') ? window.__AIT_PP.persistCount : null;
        var s = JSON.stringify({ log: log, persistCount: persistCount });

        var bufferSize = lengthBytesUTF8(s) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(s, buffer, bufferSize);
        return buffer;
    },

    /**
     * sessionStorage['__ait_pp_disabled']를 세팅(1)하거나 제거(0)함
     * 세팅 후 reload하면 그 탭 세션 동안 앱인토스 Storage 레이어가 꺼지고 순정 IndexedDB 모드로 동작
     * @param {number} disabled - 1이면 세팅, 0이면 제거
     */
    PP_SetL3Disabled: function(disabled) {
        try {
            if (disabled) {
                sessionStorage.setItem('__ait_pp_disabled', '1');
            } else {
                sessionStorage.removeItem('__ait_pp_disabled');
            }
        } catch (e) {
            console.error('[E2E-PLAYERPREFS] PP_SetL3Disabled failed: ' + e.message);
        }
    },

    /**
     * sessionStorage['__ait_pp_disabled']가 '1'이면 1, 아니면 0을 반환
     * @returns {number} 0 또는 1
     */
    PP_GetL3Disabled: function() {
        try {
            return sessionStorage.getItem('__ait_pp_disabled') === '1' ? 1 : 0;
        } catch (e) {
            return 0;
        }
    },

    /**
     * 페이지를 세션 유지 상태로 새로고침(location.reload)
     * 실기기(토스 앱 WebView)에는 브라우저 새로고침 UI가 없고, 미니앱을 껐다 다시 열면
     * 새 세션이라 sessionStorage 기반 L3 플래그가 초기화된다 — L3 시나리오는 반드시 이 경로로 reload해야 함
     */
    PP_Reload: function() {
        try {
            location.reload();
        } catch (e) {
            console.error('[E2E-PLAYERPREFS] PP_Reload failed: ' + e.message);
        }
    },

    /**
     * JavaScript에서 JSON 파싱 검증
     * C# → JSON → JavaScript 파싱 → JSON → C# 역직렬화 round-trip 검증용
     * @param {string} jsonPtr - JSON 문자열 포인터
     * @param {string} typeNamePtr - 타입 이름 포인터
     * @returns {string} - 파싱 후 재직렬화한 JSON
     */
    ValidateJsonInJS: function(jsonPtr, typeNamePtr) {
        var json = UTF8ToString(jsonPtr);
        var typeName = UTF8ToString(typeNamePtr);

        try {
            // JavaScript에서 파싱
            var parsed = JSON.parse(json);

            // 재직렬화
            var reserialized = JSON.stringify(parsed);

            console.log('[E2E-JSON-VALIDATE] ' + typeName + ': OK');

            // 결과 문자열을 Unity로 반환
            var bufferSize = lengthBytesUTF8(reserialized) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(reserialized, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.error('[E2E-JSON-VALIDATE] ' + typeName + ': FAIL - ' + e.message);

            var errorMsg = 'ERROR: ' + e.message;
            var bufferSize = lengthBytesUTF8(errorMsg) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorMsg, buffer, bufferSize);
            return buffer;
        }
    },

    // =====================================================
    // JavaScript → Unity 테스트 트리거 함수들
    // Playwright에서 Unity SendMessage를 호출하기 위한 헬퍼
    // =====================================================

    /**
     * API 테스트 트리거 (JavaScript에서 호출 가능)
     * window.TriggerAPITest() 로 호출
     */
    RegisterTriggerFunctions: function() {
        // 글로벌 트리거 함수 등록
        window.TriggerAPITest = function() {
            console.log('[E2E-TRIGGER] Triggering API Test...');
            if (window.unityInstance) {
                window.unityInstance.SendMessage('BenchmarkManager', 'TriggerAPITest');
                return true;
            }
            console.error('[E2E-TRIGGER] Unity instance not available');
            return false;
        };

        window.TriggerSerializationTest = function() {
            console.log('[E2E-TRIGGER] Triggering Serialization Test...');
            if (window.unityInstance) {
                window.unityInstance.SendMessage('BenchmarkManager', 'TriggerSerializationTest');
                return true;
            }
            console.error('[E2E-TRIGGER] Unity instance not available');
            return false;
        };

        /**
         * PlayerPrefs Set+Save 트리거 (JavaScript에서 호출 가능)
         * window.TriggerPlayerPrefsSet('{"key":"...","value":"..."}') 로 호출
         */
        window.TriggerPlayerPrefsSet = function(json) {
            console.log('[E2E-TRIGGER] Triggering PlayerPrefs Set: ' + json);
            if (window.unityInstance) {
                window.unityInstance.SendMessage('BenchmarkManager', 'TriggerPlayerPrefsSet', json);
                return true;
            }
            console.error('[E2E-TRIGGER] Unity instance not available');
            return false;
        };

        /**
         * PlayerPrefs Get 트리거 (JavaScript에서 호출 가능)
         * window.TriggerPlayerPrefsGet('key') 로 호출
         */
        window.TriggerPlayerPrefsGet = function(key) {
            console.log('[E2E-TRIGGER] Triggering PlayerPrefs Get: ' + key);
            if (window.unityInstance) {
                window.unityInstance.SendMessage('BenchmarkManager', 'TriggerPlayerPrefsGet', key);
                return true;
            }
            console.error('[E2E-TRIGGER] Unity instance not available');
            return false;
        };

        console.log('[E2E-TRIGGER] Trigger functions registered');
    },

    /**
     * window.devicePixelRatio 반환
     * Unity WebGL에서 Screen.dpi가 항상 0을 반환하므로 직접 브라우저 API 사용
     * @returns {number} devicePixelRatio (보통 1.0 ~ 3.0)
     */
    E2E_GetDevicePixelRatio: function() {
        return window.devicePixelRatio || 1.0;
    },

    /**
     * 브라우저 visibilitychange + blur/focus 이벤트를 시뮬레이션하여
     * Unity의 Application.focusChanged 트리거
     * Metric Explorer에서 unity_lifecycle 이벤트 검증용
     * @param {number} delayMs - blur 후 focus까지 대기 시간 (밀리초)
     */
    E2E_SimulateFocusChange: function(delayMs) {
        console.log('[E2E] Simulating focus change (hidden → visible, delay: ' + delayMs + 'ms)');

        // 원본 descriptor 백업 (브라우저 네이티브 getter 보존)
        var proto = Object.getPrototypeOf(document);
        var origVisibilityState = Object.getOwnPropertyDescriptor(proto, 'visibilityState');
        var origHidden = Object.getOwnPropertyDescriptor(proto, 'hidden');

        // visibilityState를 hidden으로 오버라이드
        Object.defineProperty(document, 'visibilityState', {
            value: 'hidden', writable: true, configurable: true
        });
        Object.defineProperty(document, 'hidden', {
            value: true, writable: true, configurable: true
        });
        document.dispatchEvent(new Event('visibilitychange'));
        window.dispatchEvent(new Event('blur'));

        setTimeout(function() {
            // 원본 getter 복원 (브라우저가 다시 실제 값을 관리하도록)
            delete document.visibilityState;
            delete document.hidden;
            // prototype descriptor가 사라진 경우 안전하게 복원
            if (origVisibilityState && !Object.getOwnPropertyDescriptor(proto, 'visibilityState')) {
                Object.defineProperty(proto, 'visibilityState', origVisibilityState);
            }
            if (origHidden && !Object.getOwnPropertyDescriptor(proto, 'hidden')) {
                Object.defineProperty(proto, 'hidden', origHidden);
            }
            document.dispatchEvent(new Event('visibilitychange'));
            window.dispatchEvent(new Event('focus'));
            console.log('[E2E] Focus restored (native getters restored)');
        }, delayMs);
    }
});
