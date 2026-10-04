/**
 * AIT 라이프사이클·프레임 페이싱 레이어
 *
 * 1) 라이프사이클 게이트 (mobileLifecycle)
 *    페이지가 hidden / pagehide / freeze 상태이면 Unity 의 Module.preMainLoop 가 false 를 돌려줘 프레임 본문(func)이 실행되지 않는다.
 *    (emscripten runIter: preMainLoop() === false 면 그 프레임을 건너뛰고 스케줄러는 계속 돈다.)
 *    같은 시점에 AudioContext 는 running 이던 것만 suspend, 재생 중이던 HTMLMediaElement 는 pause 하고,
 *    visible 로 돌아오면 '우리가 멈춘 것만' 재개한다. 게임이 그 사이 직접 건드린 것(pause/play 호출)은 게임 소유로 넘어가 재개하지 않는다.
 *    Unity 는 AudioContext 가 suspended 가 되면 자기 채널을 스스로 pause 했다가 running 이 되면 offset 을 보정해 재개한다
 *    (framework 의 onstatechange). 그래서 이 레이어는 context 상태만 바꾸고 Unity 오디오 상태는 건드리지 않는다.
 *    SendMessage/jslib 이벤트 핸들러는 메인 루프와 무관하게 실행되므로 AITVisibilityHelper(visibilitychange → C#)는 그대로 동작한다.
 *
 * 2) 프레임 governor (frameRateCap, adaptiveFrameRate)
 *    rAF 간격 중앙값으로 주사율을 재서 100Hz 이상(= 상한 fps 의 1.6배 이상)이면 preMainLoop 에서 프레임을 건너뛰어 상한 fps 로 맞춘다.
 *    60/90Hz 패널에서는 아무것도 하지 않는다. adaptiveFrameRate 는 opt-in 이고(자동은 꺼짐), 켜져 있어도 AITPacing.setHint 로
 *    압력 신호가 들어왔을 때만 30fps 로 낮춘다. 기본 빌드에서는 힌트를 받아도 아무 일도 일어나지 않는다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: mobileLifecycle(기본 true), frameRateCap(기본 60, 0 이면 상한 없음, 양수면 그 fps), adaptiveFrameRate(기본 false).
 *      객체가 없거나 키가 없으면 위 기본값으로 동작한다(fail-open).
 *  - window.__AIT_PACING.configure(config)
 *      index.html 이 __AIT_PP.configure(config) 직후, createUnityInstance 이전에 호출한다. 로더가 config 를 shallow 병합해 그대로
 *      Module 로 쓰므로 여기서 config.preMainLoop 를 설치한다(이미 있으면 앞단에서 호출하고 false 면 존중). 던져도 부팅은 계속된다.
 *  - window.AITPacing (= window.__AIT_PACING 의 별칭, 같은 객체)
 *      setHint(name, value): 'memory' | 'battery' | 'thermal'. 알 수 없는 name 은 무시한다.
 *        memory : { level: 'ok'|'high'|'critical' }   (ait-mem.js 가 압력 단계가 바뀔 때 보낸다)
 *        battery: { low: bool, charging?: bool } 또는 'low'
 *        thermal: 'serious' | 'critical' 또는 { state: ... }
 *      getState(): 진단용 스냅샷(주사율, 적용된 상한, 프레임/건너뜀 카운트, hidden 횟수 …).
 *      setLifecycleEnabled(bool): 런타임 탈출구. false 면 게이트를 풀고 멈춰 둔 오디오/미디어를 재개한다.
 *  - URL 탈출구: ?aitpacing=off → 이 레이어 전체 비활성, ?aitlifecycle=0 → 게이트만 비활성, ?aitcap=0 → 프레임 상한만 비활성.
 *  - window.AITMemory / window.__AIT_GL 은 존재 여부를 먼저 확인한다(스크립트 로드 순서·부재에 의존하지 않는다).
 *
 * ⚠️ Unity 로더보다 먼저 로드되어야 한다 — index.html 의 body 초입(ait-playerprefs.js 다음)에서 로드된다.
 *    AudioContext 래퍼는 로드 시점에 설치된다(Unity 의 _JS_Sound_Init 보다 앞).
 */
(function () {
    'use strict';

    // ------------------------------------------------------------------ 설정
    var flags = (typeof window.__AIT_PERF === 'object' && window.__AIT_PERF) || {};
    var lifecycleOn = flags.mobileLifecycle !== false;
    var capFps = typeof flags.frameRateCap === 'number' && flags.frameRateCap >= 0 ? flags.frameRateCap : 60;
    var adaptiveOn = flags.adaptiveFrameRate === true;

    try {
        var qs = new URLSearchParams(window.location.search);
        if (qs.get('aitpacing') === 'off') { lifecycleOn = false; capFps = 0; adaptiveOn = false; }
        if (qs.get('aitlifecycle') === '0') lifecycleOn = false;
        if (qs.get('aitcap') === '0') capFps = 0;
    } catch (e) { /* URLSearchParams 미지원 — 플래그 값만 쓴다 */ }

    var ADAPTIVE_FPS = 30;
    var PROBE_WINDOW = 30;            // 주사율 판정에 쓰는 최근 rAF 간격 수
    var PROBE_MAX_CALLBACKS = 360;    // 이 안에 100Hz 급이 안 보이면 그 값으로 확정하고 멈춘다
    var HIGH_REFRESH_MEDIAN_MS = 10.5;
    var HIGH_REFRESH_P75_MS = 13;

    var st = {
        frames: 0,           // preMainLoop 를 통과한(= 실제로 실행된) 프레임 수
        skippedHidden: 0,    // hidden 게이트로 건너뛴 프레임 수
        skippedCap: 0,       // 프레임 상한으로 건너뛴 프레임 수
        hiddenCount: 0,      // hidden 진입 횟수
        hidden: false,
        hz: 0,               // 감지한 주사율(0 = 아직 모름)
        hzMedianMs: 0,
        hzDecided: false,
        capFps: 0,           // 현재 적용 중인 상한(0 = 없음)
        lastHiddenMs: 0
    };

    function log(msg) { try { console.log('[AIT-Pacing] ' + msg); } catch (e) { /* 로그 실패 무시 */ } }
    function noop() { }
    function swallow(p) { try { if (p && typeof p.catch === 'function') p.catch(noop); } catch (e) { /* 무시 */ } }

    // ------------------------------------------------------------------ hidden 판정 / 라이프사이클 상태
    var latchPagehide = false;
    var latchFreeze = false;
    var applied = false;          // hidden 처리(오디오/미디어 정지)를 적용했는가
    var hiddenAt = 0;
    var lifecycleDisabledAtRuntime = false;

    function gateEnabled() { return lifecycleOn && !lifecycleDisabledAtRuntime; }

    function computeHidden() {
        try {
            if (document.visibilityState === 'hidden' || document.hidden === true) return true;
        } catch (e) { /* document 접근 실패는 latch 만 본다 */ }
        return latchPagehide || latchFreeze;
    }

    // ------------------------------------------------------------------ AudioContext 추적/정지/재개
    var contexts = [];
    var suspendedByUs = [];

    function trackContext(ctx) {
        try {
            if (ctx && contexts.indexOf(ctx) < 0) contexts.push(ctx);
        } catch (e) { /* 무시 */ }
        return ctx;
    }

    function wrapAudioContextCtor(name) {
        try {
            var Native = window[name];
            if (typeof Native !== 'function' || typeof Proxy !== 'function' || typeof Reflect === 'undefined') return null;
            if (Native.__aitPacing) return Native;
            var wrapper = new Proxy(Native, {
                construct: function (target, args, newTarget) {
                    var inst = Reflect.construct(target, args, newTarget === wrapper ? target : newTarget);
                    trackContext(inst);
                    return inst;
                },
                get: function (target, key) {
                    if (key === '__aitPacing') return true;
                    return Reflect.get(target, key, target);
                }
            });
            return wrapper;
        } catch (e) { return null; }
    }

    function installAudioContextWrapper() {
        // prototype 는 그대로이므로 instanceof 와 다른 스크립트의 prototype 훅(계측 등)에 영향이 없다.
        var native = window.AudioContext;
        var prefixed = window.webkitAudioContext;
        var wrapped = null;
        try {
            if (native) {
                wrapped = wrapAudioContextCtor('AudioContext');
                if (wrapped) window.AudioContext = wrapped;
            }
            if (prefixed) {
                // 같은 생성자의 별칭이면 래퍼를 공유한다.
                if (wrapped && prefixed === native) window.webkitAudioContext = wrapped;
                else {
                    var w2 = wrapAudioContextCtor('webkitAudioContext');
                    if (w2) window.webkitAudioContext = w2;
                }
            }
        } catch (e) { /* 설치 실패 — AudioContext 정지만 생략 */ }
    }

    function suspendAudioContexts() {
        var n = 0;
        var alive = [];
        for (var i = 0; i < contexts.length; i++) {
            var ctx = contexts[i];
            try {
                if (ctx.state === 'closed') continue;
                alive.push(ctx);
                if (ctx.state === 'running') {
                    swallow(ctx.suspend());
                    if (suspendedByUs.indexOf(ctx) < 0) suspendedByUs.push(ctx);
                    n++;
                }
            } catch (e) { /* 개별 context 실패는 무시 */ }
        }
        contexts = alive;
        return n;
    }

    function resumeAudioContexts() {
        var n = 0;
        var list = suspendedByUs;
        suspendedByUs = [];
        for (var i = 0; i < list.length; i++) {
            try {
                // suspend() 가 아직 처리 중일 수 있어 state 로 거르지 않는다. 이미 running 이면 resume() 은 무해하다.
                if (list[i].state !== 'closed') { swallow(list[i].resume()); n++; }
            } catch (e) { /* 무시 */ }
        }
        return n;
    }

    // ------------------------------------------------------------------ HTMLMediaElement 추적/정지/재개
    var mediaProto = (typeof HTMLMediaElement === 'function' && HTMLMediaElement.prototype) || null;
    var origPlay = mediaProto && mediaProto.play;
    var origPause = mediaProto && mediaProto.pause;
    var playing = [];       // 재생 중이라고 아는 요소
    var heldByUs = [];      // hidden 때문에 우리가 멈춘 요소
    var listened = typeof WeakSet === 'function' ? new WeakSet() : null;

    function arrRemove(arr, item) {
        var i = arr.indexOf(item);
        if (i >= 0) arr.splice(i, 1);
    }

    function trackMedia(el) {
        try {
            if (playing.indexOf(el) < 0) playing.push(el);
            if (listened && !listened.has(el)) {
                listened.add(el);
                el.addEventListener('play', function () { if (playing.indexOf(el) < 0) playing.push(el); });
                var drop = function () { if (heldByUs.indexOf(el) < 0) arrRemove(playing, el); };
                el.addEventListener('pause', drop);
                el.addEventListener('ended', drop);
                el.addEventListener('emptied', drop);
            }
        } catch (e) { /* 추적 실패 무시 */ }
    }

    function installMediaWrappers() {
        if (!mediaProto || typeof origPlay !== 'function' || typeof origPause !== 'function') return;
        if (origPlay.__aitPacing) return;
        var wrappedPlay = function () {
            // 게임이 직접 재생을 시작하면 그 요소는 게임 소유다(우리가 멈춘 목록에서 뺀다).
            arrRemove(heldByUs, this);
            var r = origPlay.apply(this, arguments);
            trackMedia(this);
            return r;
        };
        wrappedPlay.__aitPacing = true;
        var wrappedPause = function () {
            // 게임이 직접 멈추면(Unity 가 context suspend 에 반응해 자기 채널을 pause 하는 경우 포함) 재개 책임도 게임에 있다.
            arrRemove(heldByUs, this);
            return origPause.apply(this, arguments);
        };
        wrappedPause.__aitPacing = true;
        mediaProto.play = wrappedPlay;
        mediaProto.pause = wrappedPause;
    }

    function pauseMedia() {
        if (typeof origPause !== 'function') return 0;
        var candidates = playing.slice();
        try {
            var doms = document.querySelectorAll('audio,video');
            for (var d = 0; d < doms.length; d++) if (candidates.indexOf(doms[d]) < 0) candidates.push(doms[d]);
        } catch (e) { /* DOM 조회 실패 무시 */ }
        var n = 0;
        for (var i = 0; i < candidates.length; i++) {
            var el = candidates[i];
            try {
                if (el.paused || el.ended) continue;
                if (heldByUs.indexOf(el) < 0) heldByUs.push(el);
                origPause.call(el); // 래퍼를 거치지 않는다(거치면 heldByUs 에서 빠진다)
                n++;
            } catch (e) { /* 개별 요소 실패 무시 */ }
        }
        return n;
    }

    function resumeMedia() {
        var list = heldByUs;
        heldByUs = [];
        var n = 0;
        for (var i = 0; i < list.length; i++) {
            var el = list[i];
            try {
                if (el.paused && !el.ended && typeof origPlay === 'function') {
                    swallow(origPlay.call(el));
                    n++;
                }
            } catch (e) { /* 자동 재생 정책 등으로 거부돼도 무시 */ }
        }
        return n;
    }

    // ------------------------------------------------------------------ hidden / visible 전이
    function onHide(source) {
        if (applied) return;
        applied = true;
        st.hidden = true;
        st.hiddenCount++;
        hiddenAt = Date.now();
        var a = suspendAudioContexts();
        var m = pauseMedia();
        log('hidden(' + source + '): 메인 루프 정지, AudioContext ' + a + '개 suspend, 미디어 ' + m + '개 pause');
    }

    function onShow(source) {
        if (!applied) return;
        applied = false;
        st.hidden = false;
        st.lastHiddenMs = hiddenAt ? Date.now() - hiddenAt : 0;
        capCounter = 0; // 숨어 있던 사이의 시간 때문에 상한 계산이 틀어지지 않게
        var a = resumeAudioContexts();
        var m = resumeMedia();
        log('visible(' + source + '): ' + st.lastHiddenMs + 'ms 뒤 재개, AudioContext ' + a + '개 resume, 미디어 ' + m + '개 play');
    }

    function sync(source) {
        if (!gateEnabled()) return;
        var h = computeHidden();
        if (h && !applied) onHide(source);
        else if (!h && applied) onShow(source);
    }

    function installLifecycleListeners() {
        try {
            document.addEventListener('visibilitychange', function () {
                try { if (document.visibilityState === 'visible') latchPagehide = false; } catch (e) { /* 무시 */ }
                sync('visibilitychange');
            });
            window.addEventListener('pagehide', function () { latchPagehide = true; sync('pagehide'); });
            window.addEventListener('pageshow', function () { latchPagehide = false; sync('pageshow'); });
            document.addEventListener('freeze', function () { latchFreeze = true; sync('freeze'); });
            document.addEventListener('resume', function () { latchFreeze = false; sync('resume'); });
        } catch (e) { /* 리스너 등록 실패 — preMainLoop 의 프레임별 점검만 남는다 */ }
    }

    // ------------------------------------------------------------------ 프레임 governor
    var hints = {};
    var capInterval = 0;     // ms. 0 이면 상한 없음
    var capDivisor = 1;      // 매 N 번째 rAF 만 실행(프레임 수 기준). 1 이면 상한 없음
    var capCounter = 0;

    // 패널 주사율의 정수 분주만 쓴다. 비정수 비율(144Hz→60fps 등)은 13.9/20.8ms 로 번갈아 들쭉날쭉해진다.
    // 주사율 측정 오차(약 5%)를 허용해 119Hz 도 120Hz 로 본다. 분주가 2 미만이면 상한을 적용하지 않는다.
    function divisorFor(hz, fps) {
        if (!(hz > 0) || !(fps > 0)) return 1;
        return Math.max(1, Math.floor(hz * 1.05 / fps));
    }

    function adaptiveWantsLow() {
        var m = hints.memory, b = hints.battery, t = hints.thermal;
        if (m && (m === 'critical' || m.level === 'critical')) return 'memory=critical';
        if (b && (b === 'low' || (b.low === true && b.charging !== true))) return 'battery=low';
        if (t) {
            var s = typeof t === 'string' ? t : t.state;
            if (s === 'serious' || s === 'critical') return 'thermal=' + s;
        }
        return '';
    }

    function recomputeCap() {
        var div = 1;
        var why = '';
        var base = 0;
        if (capFps > 0 && st.hz > 0) {
            var d0 = divisorFor(st.hz, capFps);
            if (d0 >= 2) { div = d0; base = capFps; }
        }
        if (adaptiveOn) {
            why = adaptiveWantsLow();
            if (why) {
                var d1 = divisorFor(st.hz || 60, ADAPTIVE_FPS);
                if (d1 >= 2 && d1 > div) { div = d1; base = ADAPTIVE_FPS; }
            }
        }
        var next = div >= 2 ? Math.round((st.hz || 60) / div * 10) / 10 : 0;
        if (next !== st.capFps || div !== capDivisor) {
            st.capFps = next;
            capDivisor = div;
            capCounter = 0;
            capInterval = next > 0 ? 1000 / next : 0;
            if (base === ADAPTIVE_FPS && why) log('적응형 프레임 상한 ' + next + 'fps 적용 (' + why + ', 분주 ' + div + ', 주사율 ' + st.hz + 'Hz)');
            else if (next > 0) log('프레임 상한 ' + next + 'fps 적용 (주사율 ' + st.hz + 'Hz, 분주 ' + div + ')');
            else log('프레임 상한 해제');
        }
    }

    function median(sorted) {
        var n = sorted.length;
        return n % 2 ? sorted[(n - 1) / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    // 독립 rAF 체인으로 첫 수십 프레임의 간격을 잰다. 게임 루프와 무관하며 판정이 나면 멈춘다.
    function startRefreshProbe() {
        if (!(capFps > 0) || typeof window.requestAnimationFrame !== 'function') return;
        var deltas = [];
        var prev = 0;
        var callbacks = 0;
        function decide(med, hz, reason) {
            st.hzDecided = true;
            st.hzMedianMs = Math.round(med * 100) / 100;
            st.hz = hz;
            log('주사율 감지: ' + hz + 'Hz (rAF 간격 중앙값 ' + st.hzMedianMs + 'ms, ' + reason + ')' +
                (divisorFor(hz, capFps) >= 2 ? ' → 주사율 ' + hz + 'Hz 의 ' + divisorFor(hz, capFps) + ' 분주로 상한 적용' : ' → 상한 불필요'));
            recomputeCap();
        }
        function step(ts) {
            callbacks++;
            var hiddenNow = false;
            try { hiddenNow = document.visibilityState === 'hidden'; } catch (e) { /* 무시 */ }
            if (hiddenNow) { prev = 0; deltas.length = 0; }
            else {
                if (prev > 0) {
                    var d = ts - prev;
                    // 100ms 이상 벌어진 간격은 로딩 정지(jank)로 보고 버린다.
                    if (d > 0 && d < 100) { deltas.push(d); if (deltas.length > PROBE_WINDOW) deltas.shift(); }
                }
                prev = ts;
            }
            if (deltas.length >= PROBE_WINDOW) {
                var sorted = deltas.slice().sort(function (a, b) { return a - b; });
                var med = median(sorted);
                var p75 = sorted[Math.floor((sorted.length - 1) * 0.75)];
                if (med < HIGH_REFRESH_MEDIAN_MS && p75 < HIGH_REFRESH_P75_MS) {
                    decide(med, Math.round(1000 / med), '고주사율');
                    return;
                }
                if (callbacks >= PROBE_MAX_CALLBACKS) {
                    decide(med, Math.round(1000 / med), '관찰 한도');
                    return;
                }
            } else if (callbacks >= PROBE_MAX_CALLBACKS * 2) {
                return; // 표본이 안 모이는 환경(대부분 hidden) — 판정 없이 멈춘다(상한 미적용)
            }
            window.requestAnimationFrame(step);
        }
        window.requestAnimationFrame(step);
    }

    // ------------------------------------------------------------------ preMainLoop
    function makePreMainLoop(userPre) {
        return function aitPreMainLoop() {
            if (userPre) {
                var userRet = userPre.apply(this, arguments);
                if (userRet === false) return false;
            }
            if (gateEnabled()) {
                var h = computeHidden();
                if (h !== applied) sync('frame'); // 이벤트를 놓쳤을 때의 자가 치유
                if (h) { st.skippedHidden++; return false; }
            }
            if (capDivisor > 1) {
                var run = capCounter === 0;
                capCounter = (capCounter + 1) % capDivisor;
                if (!run) { st.skippedCap++; return false; }
            }
            st.frames++;
            return true;
        };
    }

    // ------------------------------------------------------------------ 설치
    if (lifecycleOn) {
        installAudioContextWrapper();
        installMediaWrappers();
        installLifecycleListeners();
    }

    var configured = false;

    var api = {
        configure: function (config) {
            if (configured) return;
            configured = true;
            log('설정: lifecycle=' + (lifecycleOn ? 'on' : 'off') + ' frameRateCap=' + capFps +
                ' adaptive=' + (adaptiveOn ? 'on' : 'off'));
            if (!config || typeof config !== 'object') return;
            if (!lifecycleOn && !(capFps > 0) && !adaptiveOn) return; // 할 일이 없으면 preMainLoop 자체를 달지 않는다(프레임당 비용 0)
            var userPre = typeof config.preMainLoop === 'function' ? config.preMainLoop : null;
            config.preMainLoop = makePreMainLoop(userPre);
            startRefreshProbe();
            // ait-mem.js 가 이미 압력 단계를 올렸다면 반영한다.
            try {
                if (window.AITMemory && typeof window.AITMemory.getLevel === 'function') {
                    var lvl = window.AITMemory.getLevel();
                    if (lvl && lvl !== 'ok') api.setHint('memory', { level: lvl });
                }
            } catch (e) { /* 없으면 무시 */ }
        },

        setHint: function (name, value) {
            try {
                if (name !== 'memory' && name !== 'battery' && name !== 'thermal') return;
                hints[name] = value;
                if (adaptiveOn) recomputeCap();
            } catch (e) { /* 힌트 처리 실패는 무시 */ }
        },

        setLifecycleEnabled: function (enabled) {
            lifecycleDisabledAtRuntime = !enabled;
            if (!enabled && applied) onShow('disabled');
            else if (enabled) sync('enabled');
        },

        getState: function () {
            return {
                lifecycle: gateEnabled(),
                frameRateCapSetting: capFps,
                adaptive: adaptiveOn,
                hidden: applied,
                hz: st.hz,
                hzMedianMs: st.hzMedianMs,
                hzDecided: st.hzDecided,
                capFps: st.capFps,
                frames: st.frames,
                skippedHidden: st.skippedHidden,
                skippedCap: st.skippedCap,
                hiddenCount: st.hiddenCount,
                lastHiddenMs: st.lastHiddenMs,
                trackedContexts: contexts.length,
                suspendedContexts: suspendedByUs.length,
                trackedMedia: playing.length,
                heldMedia: heldByUs.length,
                hints: hints
            };
        }
    };

    window.__AIT_PACING = api;
    window.AITPacing = api;
})();
