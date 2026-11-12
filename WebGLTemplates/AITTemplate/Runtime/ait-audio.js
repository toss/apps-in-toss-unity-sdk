/**
 * AIT 오디오 복귀 복구 레이어
 *
 * iOS 는 앱 전환·통화·Siri 뒤 WebView 의 AudioContext 를 'interrupted' 로 옮기거나, 'running' 으로 표시하면서도 currentTime 을
 * 멈춘 채 소리를 내지 않는다. Unity 의 자체 resume interval 은 state == 'suspended' 일 때만 돌아서 이 상태를 복구하지 못하고,
 * 그 결과 앱에 복귀한 뒤 화면을 터치하기 전까지(또는 끝내) 소리가 나지 않았다. 이 파일이 그 복구를 맡는다.
 *
 * 동작:
 *  - AudioContext / webkitAudioContext 생성자를 Proxy 로 감싸 생성되는 context 를 추적한다(prototype 은 그대로).
 *    각 context 에 statechange 리스너를 달아 audioLog(최근 120건 링 버퍼)에 남긴다. navigator.audioSession 이 있으면 그 statechange 도 남긴다.
 *  - hidden / pagehide / freeze: 마스터 gain 을 0 으로 내리고 running 이던 context 를 suspend 한다.
 *    iOS 가 hidden 보다 먼저 'interrupted' 로 옮긴 context 도 복귀 복구 대상(suspendedByUs)으로 같이 맡는다.
 *    재생 중이던 HTMLMediaElement 는 pause 하고, visible 로 돌아오면 '우리가 멈춘 것만' 재개한다.
 *    게임이 그 사이 직접 건드린 것(pause/play 호출)은 게임 소유로 넘어가 재개하지 않는다.
 *    Unity 는 context 가 suspended 가 되면 자기 채널을 스스로 pause 했다가 running 이 되면 offset 을 보정해 재개한다
 *    (framework 의 onstatechange). 그래서 이 레이어는 context 상태만 바꾸고 Unity 오디오 상태는 건드리지 않는다.
 *  - visible 복귀: 우리가 멈춘 context 에 resume() 을 부르고, running 이 될 때까지 1초 간격 최대 10회 재시도한다(startRetry).
 *    iOS 가 resume 을 거절해 plain 'suspended' 로 남는 경우도 같다. interrupted 인 context 는 resumeInterrupted 가 resume 한다.
 *    첫 터치/클릭(touchend/pointerup/click, capture, passive)에서도 한 번 더 시도한다(installInterruptedTap).
 *    running 에 한 번 도달하면 다시 게임/Unity 소유로 돌려준다. 우리가 suspend 하지 않은 plain 'suspended' context 는 건드리지 않는다.
 *  - 복귀 resume 이 거절됐거나(rejected) running 인데 currentTime 이 멈춰 있으면 출력을 다시 연다(watchClock). 0.5초마다 시계를 재고,
 *    2틱 연속 멈춰 있으면 suspend → resume 한다(kickFrozen, 자동 5회, 이후는 터치마다). KICK_METHODS 는 cycle → wait 순이다.
 *    거절됐던 context 는 running 이 되자마자 멈춤 확인 없이 바로 다시 연다. 시계가 4틱 연속 움직이면 감시를 끝낸다.
 *  - 복귀 때 소리가 파형 중간에서 갑자기 시작해 '틱' 하지 않게, context 출력을 마스터 gain 에 물려 40ms 페이드 인한다
 *    (installMaster / muteMaster / fadeInMaster).
 *  - Unity 는 프레임 지연을 메우려고 짧은 효과음을 수 ms 뒤부터 재생해(offset) 페이드 인을 건너뛰고 파형 중간에서 시작한다.
 *    또 효과음을 start 한 직후 pause·resume 으로 끊고 새 노드로 다시 틀어 첫 노드가 한 렌더 블록만 울리고 잘린다(클릭음).
 *    그래서 2초 미만 AudioBufferSourceNode 는 시작을 10ms 늦추고, 50ms 이하 offset 은 0 으로 둔다(루프는 offset 유지, installStartOffsetFix).
 *
 * === 켜고 끄기 ===
 *  기본은 켜짐. 아래 중 하나라도 해당하면 이 파일은 아무것도 설치하지 않는다(생성자·prototype 을 건드리지 않는다).
 *   - window.__AIT_AUDIO_RESUME === false  (이 레이어 전용 스위치. 이 스크립트보다 먼저 정의해야 한다)
 *   - window.__AIT_PERF.mobileLifecycle === false  (라이프사이클 계열 공용 스위치. 객체나 키가 없으면 켜짐 — fail-open)
 *   - URL ?aitaudio=0
 *  URL ?aitaudiofade=0 은 마스터 gain 과 start 보정만 끄고 나머지 복구는 그대로 둔다.
 *
 * === 진단 ===
 *  window.AITAudio.getState() -> { enabled, hidden, hiddenCount, lastHiddenMs, trackedContexts, suspendedContexts, trackedMedia,
 *    heldMedia, audioLog, audioContexts }
 *    audioLog: { ev:'ctx'|'session'|'hide'|'show'|'tap'|'resume'|'resumed'|'resumeErr'|'kick'|'kickCheck'|'clockOk', t, vis, ... } 복사본.
 *    audioContexts: 추적 중인 context 의 { state, t: currentTime }.
 *
 * ⚠️ Unity 로더보다 먼저 로드되어야 한다 — index.html 에서 ait-playerprefs.js 다음에 로드된다.
 *    AudioContext 래퍼는 로드 시점에 설치된다(Unity 의 _JS_Sound_Init 보다 앞).
 */
(function () {
    'use strict';

    // ------------------------------------------------------------------ 설정
    var flags = (typeof window.__AIT_PERF === 'object' && window.__AIT_PERF) || {};
    var enabled = flags.mobileLifecycle !== false && window.__AIT_AUDIO_RESUME !== false;
    var audioFadeOn = enabled;

    try {
        var qs = new URLSearchParams(window.location.search);
        if (qs.get('aitaudio') === '0') enabled = false;
        audioFadeOn = enabled && qs.get('aitaudiofade') !== '0';
    } catch (e) { /* URLSearchParams 미지원 — 플래그 값만 쓴다 */ }

    if (!enabled) {
        window.AITAudio = {
            getState: function () { return { enabled: false, audioLog: [], audioContexts: [] }; }
        };
        return;
    }

    var hiddenCount = 0;

    function log(msg) { try { console.log('[AIT-Audio] ' + msg); } catch (e) { /* 로그 실패 무시 */ } }
    function swallow(p) { try { if (p && typeof p.catch === 'function') p.catch(function () { }); } catch (e) { /* 무시 */ } }

    // ------------------------------------------------------------------ hidden 판정 / 라이프사이클 상태
    var latchPagehide = false;
    var latchFreeze = false;
    var applied = false;          // hidden 처리(오디오/미디어 정지)를 적용했는가
    var hiddenAt = 0;
    var lastHiddenMs = 0;

    function computeHidden() {
        try {
            if (document.visibilityState === 'hidden' || document.hidden === true) return true;
        } catch (e) { /* document 접근 실패는 latch 만 본다 */ }
        return latchPagehide || latchFreeze;
    }

    // ------------------------------------------------------------------ AudioContext 추적/정지/재개
    var contexts = [];
    var suspendedByUs = [];
    var resumePending = [];   // visible 복귀 때 resume 을 요청했지만 아직 running 을 확인하지 못한 우리 context
    var rejected = [];        // 이번 복귀에서 resume 이 거절된 context. running 이 돼도 시계가 멈춰 있어 바로 다시 연다
    var masters = [];         // {ctx, gain, faded} 마스터 gain

    // 복구 대상: iOS 가 가로챈 context, 또는 우리가 멈췄다가 아직 돌려놓지 못한 context
    function needsResume(ctx) {
        return ctx.state === 'interrupted' || (ctx.state === 'suspended' && resumePending.indexOf(ctx) >= 0);
    }

    function clearPending(ctx) {
        var i = resumePending.indexOf(ctx);
        if (i >= 0) resumePending.splice(i, 1);
    }

    // 오디오 진단 링 버퍼(최근 AUDIO_LOG_MAX 건). getState().audioLog 로 복사본을 노출한다.
    var AUDIO_LOG_MAX = 120;
    var audioLog = [];

    function alog(o) {
        try {
            o.t = Math.round(performance.now());
            o.vis = document.visibilityState;
            audioLog.push(o);
            if (audioLog.length > AUDIO_LOG_MAX) audioLog.shift();
        } catch (e) { /* 진단 기록 실패 무시 */ }
    }

    // resume 결과까지 audioLog 에 남긴다. 'resume' 뒤에 'resumed'/'resumeErr' 가 없으면 약속이 끝나지 않은 것이다.
    function resumeCtx(ctx, why) {
        var p = ctx.resume();
        alog({ ev: 'resume', why: why, state: ctx.state, cur: ctx.currentTime });
        try {
            if (p && typeof p.then === 'function') {
                p.then(function () { alog({ ev: 'resumed', why: why, state: ctx.state }); },
                    function (e) {
                        alog({ ev: 'resumeErr', why: why, state: ctx.state, err: String(e && e.name) });
                        if (rejected.indexOf(ctx) < 0) rejected.push(ctx);
                    });
            }
        } catch (e) { /* 진단 기록 실패 무시 */ }
    }

    function isVisible() {
        try { return document.visibilityState === 'visible'; } catch (e) { return false; }
    }

    // iOS 가 context 를 'interrupted' 로 옮기면 Unity 의 resume interval(state == 'suspended' 일 때만 동작)이 멈춘다.
    // 그래서 여기서 직접 재시도한다. 우리가 suspend 한 것이 아닌 plain 'suspended' 는 건드리지 않는다.
    var RESUME_RETRY_MS = 1000;
    var RESUME_RETRY_MAX = 10;
    var ctxRetries = [];      // {ctx, timer, n}
    var tapInstalled = false;
    var TAP_EVENTS = ['touchend', 'pointerup', 'click'];
    var CLOCK_TICK_MS = 500;
    var CLOCK_FROZEN_TICKS = 2;   // running 인데 이만큼 안 움직이면 멈춘 것으로 본다
    var CLOCK_OK_TICKS = 4;       // 연속으로 이만큼 움직이면 정상으로 보고 감시를 끝낸다
    var CLOCK_KICK_MAX = 5;
    var MASTER_FADE_S = 0.04;
    var SHORT_CLIP_S = 2;
    var MAX_SKIP_OFFSET_S = 0.05;
    var SHORT_CLIP_LEAD_S = 0.01;
    // cycle: suspend → resume, wait: 그 사이에 쉼. 실기기에서 cycle 로 풀렸고, wait 은 그래도 안 풀릴 때의 다음 단계다.
    var KICK_METHODS = ['cycle', 'wait'];
    var KICK_WAIT_MS = 400;

    function stopRetry(ctx) {
        for (var i = ctxRetries.length - 1; i >= 0; i--) {
            if (ctxRetries[i].ctx === ctx) {
                try { clearInterval(ctxRetries[i].timer); } catch (e) { /* 무시 */ }
                ctxRetries.splice(i, 1);
            }
        }
    }

    function startRetry(ctx) {
        if (typeof setInterval !== 'function') return;
        for (var i = 0; i < ctxRetries.length; i++) if (ctxRetries[i].ctx === ctx) return;
        var rec = { ctx: ctx, timer: 0, n: 0 };
        rec.timer = setInterval(function () {
            try {
                if (ctx.state === 'running' || ctx.state === 'closed') clearPending(ctx);
                if (applied || !needsResume(ctx)) { stopRetry(ctx); return; }
                resumeCtx(ctx, 'retry');
                rec.n++;
                if (rec.n >= RESUME_RETRY_MAX) stopRetry(ctx);
            } catch (e) { stopRetry(ctx); }
        }, RESUME_RETRY_MS);
        ctxRetries.push(rec);
    }

    function resumeInterrupted(skip) {
        var n = 0;
        for (var i = 0; i < contexts.length; i++) {
            try {
                if (skip && skip.indexOf(contexts[i]) >= 0) continue;
                if (needsResume(contexts[i])) { resumeCtx(contexts[i], skip ? 'show' : 'tap'); n++; }
            } catch (e) { /* 개별 context 실패는 무시 */ }
        }
        return n;
    }

    // 사용자 제스처가 있어야 resume 이 허용되는 환경 대비: 첫 터치/클릭에서 한 번 시도하고 스스로 걷는다.
    var clockWatches = [];    // {ctx, timer, lastT, same, moved, kicks}

    function stopClockWatch(rec) {
        try { clearInterval(rec.timer); } catch (e) { /* 무시 */ }
        var i = clockWatches.indexOf(rec);
        if (i >= 0) clockWatches.splice(i, 1);
    }

    function kickFrozen(rec) {
        var ctx = rec.ctx;
        rec.same = 0;
        rec.kicks++;
        muteMaster(ctx);
        var m = KICK_METHODS[Math.min(rec.kicks, KICK_METHODS.length) - 1];
        rec.method = m;
        alog({ ev: 'kick', n: rec.kicks, method: m, cur: ctx.currentTime });
        if (resumePending.indexOf(ctx) < 0) resumePending.push(ctx); // resume 이 거절돼도 재시도·터치 복구로 이어지게
        var again = function () { if (!applied) resumeCtx(ctx, 'kick:' + m); };
        var next = m === 'wait' ? function () { setTimeout(again, KICK_WAIT_MS); } : again;
        var p = ctx.suspend();
        if (p && typeof p.then === 'function') p.then(next, next);
        else next();
        [300, 1200].forEach(function (ms) {
            setTimeout(function () { alog({ ev: 'kickCheck', ms: ms, method: m, state: ctx.state, cur: ctx.currentTime }); }, KICK_WAIT_MS + ms);
        });
        installInterruptedTap();
    }

    function watchClock(ctx) {
        if (typeof setInterval !== 'function' || typeof ctx.currentTime !== 'number') return;
        for (var i = 0; i < clockWatches.length; i++) if (clockWatches[i].ctx === ctx) return;
        var rec = { ctx: ctx, timer: 0, lastT: ctx.currentTime, same: 0, moved: 0, kicks: 0 };
        rec.timer = setInterval(function () {
            try {
                if (applied || ctx.state === 'closed' || (ctx.state === 'suspended' && resumePending.indexOf(ctx) < 0)) { stopClockWatch(rec); return; }
                if (ctx.state !== 'running') return; // suspended/interrupted 는 startRetry 가 맡는다
                var t = ctx.currentTime;
                if (t > rec.lastT) {
                    fadeInMaster(ctx); // suspend 가 처리되기 전에 돌아오면 statechange 가 없다
                    rec.lastT = t;
                    rec.same = 0;
                    if (++rec.moved >= CLOCK_OK_TICKS) { alog({ ev: 'clockOk', kicks: rec.kicks, method: rec.method }); stopClockWatch(rec); }
                    return;
                }
                rec.moved = 0;
                rec.same++;
                if (rec.same >= CLOCK_FROZEN_TICKS && rec.kicks < CLOCK_KICK_MAX) kickFrozen(rec);
            } catch (e) { stopClockWatch(rec); }
        }, CLOCK_TICK_MS);
        clockWatches.push(rec);
    }

    function installInterruptedTap() {
        if (tapInstalled) return;
        try {
            tapInstalled = true;
            var handler = function () {
                for (var i = 0; i < TAP_EVENTS.length; i++) {
                    try { window.removeEventListener(TAP_EVENTS[i], handler, true); } catch (e) { /* 무시 */ }
                }
                tapInstalled = false;
                alog({ ev: 'tap' });
                resumeInterrupted();
                // 자동 시도를 다 쓴 뒤에도 터치마다 다시 연다. kickFrozen 이 다음 터치를 위해 리스너를 다시 건다.
                for (var w = clockWatches.length - 1; w >= 0; w--) {
                    if (clockWatches[w].same > 0 && clockWatches[w].ctx.state === 'running') kickFrozen(clockWatches[w]);
                }
            };
            for (var i = 0; i < TAP_EVENTS.length; i++) window.addEventListener(TAP_EVENTS[i], handler, { capture: true, passive: true });
        } catch (e) { tapInstalled = false; }
    }

    function masterOf(ctx) {
        for (var i = 0; i < masters.length; i++) if (masters[i].ctx === ctx) return masters[i];
        return null;
    }

    // Unity 등이 ctx.destination 에 연결하면 이 gain 을 거쳐 나가게 한다.
    function installMaster(ctx) {
        if (!audioFadeOn) return;
        try {
            var dest = ctx.destination;
            var gain = ctx.createGain();
            gain.connect(dest);
            gain.__aitMaster = true;
            Object.defineProperty(ctx, 'destination', { configurable: true, get: function () { return gain; } });
            masters.push({ ctx: ctx, gain: gain, muted: false });
        } catch (e) { /* 설치 실패 — 페이드 없이 그대로 재생한다 */ }
    }

    function muteMaster(ctx) {
        var m = masterOf(ctx);
        if (!m) return;
        try {
            m.muted = true;
            m.gain.gain.cancelScheduledValues(0);
            m.gain.gain.setValueAtTime(0, ctx.currentTime);
        } catch (e) { /* 무시 */ }
    }

    function fadeInMaster(ctx) {
        var m = masterOf(ctx);
        if (!m || !m.muted) return;
        try {
            m.muted = false;
            var t = ctx.currentTime;
            m.gain.gain.cancelScheduledValues(0);
            m.gain.gain.setValueAtTime(0, t);
            m.gain.gain.linearRampToValueAtTime(1, t + MASTER_FADE_S);
        } catch (e) { try { m.gain.gain.value = 1; } catch (e2) { /* 무시 */ } }
    }

    function onCtxState(ctx) {
        try {
            if (ctx.state === 'running' || ctx.state === 'closed') clearPending(ctx);
            if (ctx.state === 'interrupted') muteMaster(ctx);
            if (ctx.state === 'running') {
                fadeInMaster(ctx);
                var r = rejected.indexOf(ctx);
                if (r >= 0 && !applied) {
                    rejected.splice(r, 1);
                    for (var i = 0; i < clockWatches.length; i++) if (clockWatches[i].ctx === ctx) kickFrozen(clockWatches[i]);
                }
            }
            if (needsResume(ctx)) {
                if (applied || !isVisible()) return; // hidden 이면 onShow 가 처리한다
                installInterruptedTap();
                startRetry(ctx);
            } else {
                stopRetry(ctx);
            }
        } catch (e) { /* 무시 */ }
    }

    function trackContext(ctx) {
        try {
            if (ctx && contexts.indexOf(ctx) < 0) {
                contexts.push(ctx);
                installMaster(ctx);
                try {
                    ctx.addEventListener('statechange', function () {
                        alog({ ev: 'ctx', state: ctx.state, cur: ctx.currentTime });
                        onCtxState(ctx);
                    });
                } catch (e) { /* 리스너 등록 실패 — 진단/복구만 생략 */ }
            }
        } catch (e) { /* 무시 */ }
        return ctx;
    }

    function installAudioSessionListener() {
        try {
            var as = navigator.audioSession;
            if (as && typeof as.addEventListener === 'function') {
                as.addEventListener('statechange', function () { alog({ ev: 'session', state: as.state, type: as.type }); });
            }
        } catch (e) { /* audioSession 미지원 */ }
    }

    function wrapAudioContextCtor(name) {
        try {
            var Native = window[name];
            if (typeof Native !== 'function' || typeof Proxy !== 'function' || typeof Reflect === 'undefined') return null;
            if (Native.__aitAudio) return Native;
            var wrapper = new Proxy(Native, {
                construct: function (target, args, newTarget) {
                    var inst = Reflect.construct(target, args, newTarget === wrapper ? target : newTarget);
                    trackContext(inst);
                    return inst;
                },
                get: function (target, key) {
                    if (key === '__aitAudio') return true;
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

    function installStartOffsetFix() {
        if (!audioFadeOn) return;
        try {
            var proto = window.AudioBufferSourceNode && window.AudioBufferSourceNode.prototype;
            var orig = proto && proto.start;
            if (!orig || orig.__aitAudio) return;
            var wrapped = function (when, offset, duration) {
                try {
                    if (this.buffer && this.buffer.duration < SHORT_CLIP_S && !(offset > MAX_SKIP_OFFSET_S)) {
                        // Unity 는 효과음을 start 한 직후 pause·resume 으로 끊고 새 노드로 다시 튼다.
                        // 바로 시작하면 첫 노드가 한 렌더 블록만 울리고 잘려 클릭이 나므로, 시작을
                        // 살짝 미뤄 끊기기 전에는 소리가 나지 않게 한다.
                        var cur = this.context.currentTime;
                        var at = when > cur + SHORT_CLIP_LEAD_S ? when : cur + SHORT_CLIP_LEAD_S;
                        var off = this.loop ? (offset || 0) : 0;
                        return duration === undefined ? orig.call(this, at, off) : orig.call(this, at, off, duration);
                    }
                } catch (e) { /* 원본 호출로 넘긴다 */ }
                return orig.apply(this, arguments);
            };
            wrapped.__aitAudio = true;
            proto.start = wrapped;
        } catch (e) { /* 설치 실패 — 원본 그대로 */ }
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
                } else if (ctx.state === 'interrupted' && suspendedByUs.indexOf(ctx) < 0) {
                    // 앱 전환에서는 iOS 의 interrupted 가 hidden 보다 먼저 온다. 복귀 복구 대상에서 빠지지 않게 같이 맡는다.
                    suspendedByUs.push(ctx);
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
                if (list[i].state !== 'closed') { resumeCtx(list[i], 'show'); n++; }
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
        if (origPlay.__aitAudio) return;
        var wrappedPlay = function () {
            // 게임이 직접 재생을 시작하면 그 요소는 게임 소유다(우리가 멈춘 목록에서 뺀다).
            arrRemove(heldByUs, this);
            var r = origPlay.apply(this, arguments);
            trackMedia(this);
            return r;
        };
        wrappedPlay.__aitAudio = true;
        var wrappedPause = function () {
            // 게임이 직접 멈추면(Unity 가 context suspend 에 반응해 자기 채널을 pause 하는 경우 포함) 재개 책임도 게임에 있다.
            arrRemove(heldByUs, this);
            return origPause.apply(this, arguments);
        };
        wrappedPause.__aitAudio = true;
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
        hiddenCount++;
        hiddenAt = Date.now();
        resumePending = [];
        rejected = [];
        while (clockWatches.length) stopClockWatch(clockWatches[0]);
        alog({ ev: 'hide', source: source });
        for (var i = 0; i < contexts.length; i++) muteMaster(contexts[i]);
        var a = suspendAudioContexts();
        var m = pauseMedia();
        log('hidden(' + source + '): AudioContext ' + a + '개 suspend, 미디어 ' + m + '개 pause');
    }

    function onShow(source) {
        if (!applied) return;
        applied = false;
        lastHiddenMs = hiddenAt ? Date.now() - hiddenAt : 0;
        alog({ ev: 'show', source: source, ms: lastHiddenMs });
        var ours = suspendedByUs.slice();
        var a = resumeAudioContexts();
        // resume() 이 거절돼 suspended 로 남는 경우(statechange 가 오지 않는다)를 위해 running 확인까지 재시도·첫 터치 복구를 건다
        for (var i = 0; i < ours.length; i++) {
            if (ours[i].state === 'closed') continue;
            resumePending.push(ours[i]);
            startRetry(ours[i]);
            watchClock(ours[i]);
        }
        if (resumePending.length) installInterruptedTap();
        var ia = resumeInterrupted(ours); // hidden 사이에 iOS 가 interrupted 로 옮긴 context
        for (var j = 0; j < contexts.length; j++) if (contexts[j].state === 'running') fadeInMaster(contexts[j]);
        var m = resumeMedia();
        log('visible(' + source + '): ' + lastHiddenMs + 'ms 뒤 재개, AudioContext ' + a + '개 resume' +
            (ia ? '(+interrupted ' + ia + '개)' : '') + ', 미디어 ' + m + '개 play');
    }

    function sync(source) {
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
        } catch (e) { /* 리스너 등록 실패 — 이 레이어의 hidden/visible 처리만 생략 */ }
    }

    // ------------------------------------------------------------------ 설치
    installAudioContextWrapper();
    installStartOffsetFix();
    installMediaWrappers();
    installLifecycleListeners();
    installAudioSessionListener();

    window.AITAudio = {
        getState: function () {
            return {
                enabled: true,
                hidden: applied,
                hiddenCount: hiddenCount,
                lastHiddenMs: lastHiddenMs,
                trackedContexts: contexts.length,
                suspendedContexts: suspendedByUs.length,
                trackedMedia: playing.length,
                heldMedia: heldByUs.length,
                audioLog: audioLog.slice(),
                audioContexts: contexts.map(function (c) { return { state: c.state, t: c.currentTime }; })
            };
        }
    };
})();
