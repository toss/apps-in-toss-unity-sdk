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
     * 탭 진단 한 줄을 window.__AIT_TAPLOG에 쌓고 console.log로 흘림
     *
     * 화면 패널과 별개로 콘솔에도 남기는 이유: 진단 대상이 "스크롤 영역 안에서 탭이 안 먹는 증상"이라
     * 화면 UI 조작 자체가 증상의 영향을 받을 수 있다. 원격 인스펙터가 붙는 상황이면 화면을 거치지 않고
     * 여기서 읽는 편이 확실하다.
     *
     * @param {number} linePtr - 진단 한 줄 문자열 포인터
     */
    TAP_Log: function(linePtr) {
        var line = UTF8ToString(linePtr);
        window.__AIT_TAPLOG = window.__AIT_TAPLOG || [];
        window.__AIT_TAPLOG.push(line);
        while (window.__AIT_TAPLOG.length > 200) {
            window.__AIT_TAPLOG.shift();
        }
        console.log('[E2E-TAP] ' + line);
    },

    /**
     * window.__AIT_TAPLOG를 비움
     */
    TAP_Clear: function() {
        window.__AIT_TAPLOG = [];
        console.log('[E2E-TAP] cleared');
    },

    /**
     * 캔버스에 합성 터치 탭을 보냄 (touchstart → holdMs 후 touchend)
     *
     * Unity WebGL의 입력은 emscripten의 registerTouchEventCallback이 캔버스에 건
     * 'touchstart'/'touchmove'/'touchend' 리스너로 들어온다(CoreModule이
     * emscripten_set_touchstart_callback_on_thread 등을 임포트하는 것으로 확인). 그 핸들러는
     * e.touches / e.changedTouches / e.targetTouches와 각 터치의 identifier·clientX/Y·pageX/Y·
     * screenX/Y만 읽고, 터치 객체에 isChanged/onTarget을 되쓴다. 그래서 읽기 전용인 진짜 Touch
     * 인스턴스가 아니라 평범한 객체를 담은 일반 Event가 오히려 알맞다. Safari가 TouchEvent
     * 생성자를 지원하지 않는 문제도 함께 피한다.
     *
     * 좌표는 Unity 스크린 기준 정규화 값(원점 좌하단)으로 받는다. C#이 Screen.width/height로
     * 나눈 값을 그대로 넘기면 되고, devicePixelRatio는 캔버스 rect가 흡수하므로 신경 쓸 필요가 없다.
     *
     * @param {number} nx - 가로 0~1
     * @param {number} ny - 세로 0~1 (0이 화면 아래)
     * @param {number} holdMs - 누르고 있는 시간
     */
    TAP_Tap: function(nx, ny, holdMs) {
        var canvas = Module['canvas'] || document.querySelector('canvas');
        if (!canvas) {
            console.error('[E2E-TAP] canvas를 찾을 수 없어 합성 탭을 보내지 못했다');
            return;
        }

        var rect = canvas.getBoundingClientRect();
        var cx = rect.left + nx * rect.width;
        var cy = rect.top + (1 - ny) * rect.height;

        window.__AIT_TAP_BUSY = (window.__AIT_TAP_BUSY || 0) + 1;
        window.__AIT_TAP_ID = (window.__AIT_TAP_ID || 0) + 1;
        var id = window.__AIT_TAP_ID;

        // emscripten의 터치 콜백은 touch 객체에 isChanged/onTarget을 써 넣는다. 같은 객체를
        // touchstart와 touchend에 재사용하면 touchstart에서 세워진 값이 touchend까지 새어
        // 나가므로 이벤트마다 새로 만든다.
        var makeTouch = function() {
            return {
                identifier: id,
                target: canvas,
                clientX: cx, clientY: cy,
                pageX: cx + window.pageXOffset, pageY: cy + window.pageYOffset,
                screenX: cx, screenY: cy,
                radiusX: 1, radiusY: 1, rotationAngle: 0, force: 1
            };
        };

        var fire = function(type, touches, changed, target) {
            var e = new Event(type, { bubbles: true, cancelable: true });
            e.touches = touches;
            e.changedTouches = changed;
            e.targetTouches = target;
            e.ctrlKey = false; e.shiftKey = false; e.altKey = false; e.metaKey = false;
            canvas.dispatchEvent(e);
        };

        // dispatch를 프레임 밖으로 미룬다. 여기는 C# 코루틴 → jslib 호출 스택 안, 즉 PlayerLoop
        // 한복판이다. touchstart를 동기로 쏘면 엔진의 터치 핸들러가 그 자리에서 입력 처리를
        // 재진입하려 들고, Unity가 "PlayerLoop internal function has been called recursively"로
        // 막는다. 막히기만 하고 이벤트는 다음 프레임에 처리되므로 측정값은 멀쩡했지만, 탭마다
        // 에러가 하나씩 쌓여 실기기 로그를 덮는다.
        //
        // 조준 보정(TapAutoProbe.LeadFrames)은 그대로 둔다. 매크로태스크는 현재 rAF 콜백이
        // 끝난 뒤 다음 rAF 전에 돌므로, 엔진이 터치를 집어가는 프레임은 동기 dispatch 때와
        // 같은 다음 프레임이다.
        setTimeout(function() {
            var down = makeTouch();
            fire('touchstart', [down], [down], [down]);
            setTimeout(function() {
                // 손을 뗀 상태라 touches/targetTouches는 비고 changedTouches에만 남는다.
                var up = makeTouch();
                fire('touchend', [], [up], []);
                window.__AIT_TAP_BUSY = Math.max(0, (window.__AIT_TAP_BUSY || 1) - 1);
            }, holdMs);
        }, 0);
    },

    /**
     * 진행 중인 합성 탭이 있으면 1, 없으면 0
     * @returns {number}
     */
    TAP_DriveBusy: function() {
        return (window.__AIT_TAP_BUSY || 0) > 0 ? 1 : 0;
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
