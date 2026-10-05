/**
 * AIT WebGL 컨텍스트 레이어
 *
 * 하는 일(전부 try/catch, 실패하면 원래 동작으로 fail-open):
 *  1. HTMLCanvasElement.prototype.getContext 훅(Unity 로더보다 먼저 설치).
 *     - 실제 Unity 캔버스(#unity-canvas)의 WebGL 컨텍스트 요청/실제 속성(getContextAttributes, SAMPLES, 드로잉 버퍼 크기)을
 *       window.__AIT_GL.contexts 에 남기고 "[AIT-GL] ..." 로그를 한 번 찍는다.
 *     - glDropAntialias 플래그가 켜져 있고(명시 1일 때만) 모바일이며 DPR >= 1.5 또는 deviceMemory < 6 이면 antialias 요청을 false 로 바꾼다.
 *       바꾼 요청으로 컨텍스트 생성이 실패하면 원래 속성으로 한 번 더 시도한다.
 *     - Unity 로더가 기기 정보를 읽으려고 만드는 probe 컨텍스트와 템플릿의 testCanvas 컨텍스트(둘 다 DOM 에 붙지 않은
 *       캔버스에서, 실제 Unity 컨텍스트가 생기기 전에 만들어진 것)를 한 태스크 뒤 WEBGL_lose_context 로 해제한다.
 *       Unity 캔버스(id=unity-canvas 이거나 DOM 에 붙은 캔버스)는 어떤 경우에도 해제하지 않는다.
 *  2. webglcontextlost 복구(bindContextLoss): preventDefault 없이 페이지를 reload 한다. 120초 안에 2번째 손실이면 루프 가드로
 *     reload 대신 안내 overlay 를 띄운다. 문서가 hidden 이면 visible 이 될 때까지 reload 를 미룬다.
 *  3. DPR tier 상한(tierCap): localStorage __ait_gl_tier(24시간 만료)에 저장된 값, window.AITMemory.crashCount 에서 구한 값,
 *     window.AITMemory.lowMemTier(부팅 사망 후 저사양 tier: 1 → 1.5, 2 → 1)에서 구한 값 중 가장 낮은 것. 손실이 날 때마다 현재 DPR 한 단계 아래(2 → 1.5 → 1)로 내린다.
 *  4. ?aitglprobe=1 일 때만 RT 인벤토리(renderbufferStorage(Multisample)/texStorage2D/texImage2D(null) 크기)를 window.__AIT_GL.rt 에 모은다.
 *     같은 모드에서 텍스처 업로드 바이트도 센다(P0-1): texImage2D(데이터 있음)/compressedTexImage2D/texSubImage2D(+compressedTexSubImage2D)/
 *     texImage3D·texSubImage3D 의 ArrayBufferView byteLength 합. 데이터 없이 할당만 하는 texImage2D(null)/texStorage2D 크기는 alloc 으로 따로.
 *     window.__AIT_GL.rt.upload() → { texImage:{n,bytes}, compressed:{n,bytes}, sub:{n,bytes}, view3d:{n,bytes}, nonView:n, totalBytes, allocBytes }
 *     (nonView 는 이미지/캔버스/ImageBitmap/PBO 오프셋처럼 byteLength 를 알 수 없는 업로드 호출 수). 로그는 "[AIT-GL] 텍스처 업로드 ..." 한 줄을 4초 디바운스로 남긴다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: glHook(기본 true), glDropAntialias(기본 false), glContextRecovery(기본 true).
 *      객체가 없거나 키가 없으면 위 기본값으로 동작한다(fail-open).
 *      glHook=false 면 getContext 를 건드리지 않는다. glContextRecovery=false 면 bindContextLoss 는 false, tierCap 은 저장값/crashCount 를 무시하고
 *      lowMemTier 로 정한 값만 돌려준다(저사양 tier 가 없으면 0).
 *  - window.__AIT_GL.tierCap(): number
 *      부팅 시점 DPR 상한. 0 = 상한 없음, 그 외 2 / 1.5 / 1 중 하나. index.html 의 getOptimalDevicePixelRatio 가
 *      자동 DPR 과 min 을 취한다(명시적 DPR 설정이 있으면 호출하지 않음 — 그래서 이 파일은 명시 설정을 모른다).
 *      근거로 window.AITMemory.crashCount, window.AITMemory.lowMemTier(ait-mem.js)를 읽는다(없으면 0 으로 취급).
 *      lowMemTier 는 glContextRecovery 와 무관하게 반영한다(저사양 tier 는 컨텍스트 복구와 별개 레버다).
 *  - window.__AIT_GL.bindContextLoss(canvas): boolean
 *      index.html 이 createUnityInstance 직전에 호출한다. true 를 돌려주면 이 레이어가 webglcontextlost 를
 *      전담하고 index.html 의 기본 핸들러는 설치되지 않는다. false 면 index.html 이 기존 핸들러를 쓴다.
 *  - window.__AIT_EFFECTIVE_DPR / window.unityConfig: index.html 이 노출. 손실 시 tier 를 계산할 때 읽는다.
 *  - window.AITPacing.setHint(name, value): 손실이 났을 때 'glContextLost' 힌트를 선택적으로 전달한다(없으면 무시).
 *
 * ⚠️ Unity 로더(컨텍스트 생성)보다 먼저 로드되어야 한다 — index.html 에서 "Unity Loader Script" 앞에 위치한다.
 */
(function () {
    'use strict';

    // 같은 페이지에 두 번 로드돼도 훅·리스너가 이중으로 걸리지 않게 한다.
    if (window.__AIT_GL && window.__AIT_GL.__loaded) return;

    var LOG = '[AIT-GL]';
    var LOSS_KEY = '__ait_gl_loss__';          // sessionStorage: 최근 손실 시각 배열
    var LOSS_WINDOW_MS = 120000;               // 루프 가드 창
    var TIER_KEY = '__ait_gl_tier';            // localStorage: {cap, ts}
    var TIER_TTL_MS = 24 * 60 * 60 * 1000;     // tier 만료
    var TIER_STEPS = [2, 1.5, 1];
    var PROBE_WINDOW_MS = 30000;               // 스크립트 로드 후 이 시간 안에 만든 detached 컨텍스트만 probe 로 본다
    var CONTEXT_LIST_MAX = 32;
    var OVERLAY_ID = 'ait-gl-lost-overlay';

    var PERF = (window.__AIT_PERF && typeof window.__AIT_PERF === 'object') ? window.__AIT_PERF : {};
    function flag(key, def) {
        var v = PERF[key];
        return typeof v === 'boolean' ? v : def;
    }
    var HOOK = flag('glHook', true);
    var DROP_AA = flag('glDropAntialias', false);
    var RECOVERY = flag('glContextRecovery', true);

    var IS_MOBILE = false;
    try { IS_MOBILE = /iPhone|iPad|iPod|Android/i.test(navigator.userAgent); } catch (e) {}

    var T0 = 0;
    try { T0 = (window.performance && performance.now) ? performance.now() : 0; } catch (e) {}

    function nowMs() {
        try { return (window.performance && performance.now) ? performance.now() : 0; } catch (e) { return 0; }
    }
    function round1(n) { return Math.round(n * 10) / 10; }
    function warn() { try { console.warn.apply(console, arguments); } catch (e) {} }
    function log() { try { console.log.apply(console, arguments); } catch (e) {} }

    var GL = {
        __loaded: true,
        version: 1,
        flags: { hook: HOOK, dropAntialias: DROP_AA, recovery: RECOVERY },
        contexts: [],
        requested: null,          // Unity 캔버스가 요청한 속성(복사본)
        actual: null,             // 실제로 받은 속성
        samples: null,
        aaDecision: HOOK ? 'pending' : 'hook-off',
        probeReleased: 0,
        lossCount: 0,
        reloadScheduled: false,
        overlayShown: false,
        tier: { stored: 0, crash: 0, lowMem: 0, applied: 0 },
        rt: null,
        tierCap: function () { return 0; },
        bindContextLoss: function () { return false; }
    };
    window.__AIT_GL = GL;

    // ------------------------------------------------------------------
    // 캔버스 판별
    // ------------------------------------------------------------------
    function isUnityCanvas(canvas) {
        try { return !!canvas && canvas.id === 'unity-canvas'; } catch (e) { return false; }
    }
    function isAttached(canvas) {
        try {
            if (typeof canvas.isConnected === 'boolean') return canvas.isConnected;
            return !!(document.documentElement && document.documentElement.contains(canvas));
        } catch (e) {
            return true; // 판단 불가면 붙어 있는 것으로 보고(= 해제하지 않음) 안전한 쪽을 택한다.
        }
    }
    function isGlType(type) {
        return type === 'webgl2' || type === 'webgl' || type === 'experimental-webgl';
    }

    // ------------------------------------------------------------------
    // 속성 스냅샷
    // ------------------------------------------------------------------
    var ATTR_KEYS = ['alpha', 'antialias', 'depth', 'stencil', 'premultipliedAlpha', 'preserveDrawingBuffer',
                     'powerPreference', 'failIfMajorPerformanceCaveat', 'desynchronized'];
    function snapshotAttrs(attrs) {
        var out = {};
        if (!attrs || typeof attrs !== 'object') return out;
        for (var i = 0; i < ATTR_KEYS.length; i++) {
            var k = ATTR_KEYS[i];
            try { if (attrs[k] !== undefined) out[k] = attrs[k]; } catch (e) {}
        }
        return out;
    }
    function copyAttrs(attrs) {
        var out = {};
        if (attrs && typeof attrs === 'object') {
            for (var k in attrs) {
                try { out[k] = attrs[k]; } catch (e) {}
            }
        }
        return out;
    }

    // ------------------------------------------------------------------
    // antialias 해제 판정 (명시 플래그 + 모바일 + DPR/메모리 조건)
    // ------------------------------------------------------------------
    function decideAntialias(canvas, attrs) {
        if (!DROP_AA) return 'flag-off';
        if (!IS_MOBILE) return 'not-mobile';
        if (!isUnityCanvas(canvas)) return 'not-unity-canvas';
        if (attrs && typeof attrs === 'object' && attrs.antialias === false) return 'already-off';
        var dpr = Number(window.__AIT_EFFECTIVE_DPR) || window.devicePixelRatio || 1;
        var mem = navigator.deviceMemory;
        if (dpr >= 1.5 || (typeof mem === 'number' && mem < 6)) return 'drop';
        return 'device-ok';
    }

    // ------------------------------------------------------------------
    // 컨텍스트 기록 / probe 해제
    // ------------------------------------------------------------------
    var seen = (typeof WeakSet === 'function') ? new WeakSet() : null;
    var bootPhase = true;      // Unity 컨텍스트가 만들어지기 전까지 true
    var loggedUnity = false;

    function pushContext(rec) {
        if (GL.contexts.length < CONTEXT_LIST_MAX) GL.contexts.push(rec);
    }

    function readActual(ctx) {
        var out = null;
        try {
            var a = ctx.getContextAttributes();
            if (a) {
                out = {};
                for (var i = 0; i < ATTR_KEYS.length; i++) {
                    var k = ATTR_KEYS[i];
                    if (a[k] !== undefined) out[k] = a[k];
                }
            }
        } catch (e) {}
        return out;
    }

    function releaseProbe(canvas, ctx, rec) {
        try {
            // 한 태스크가 지난 뒤에도 여전히 "쓰이지 않은 detached probe" 일 때만 해제한다.
            if (isUnityCanvas(canvas) || isAttached(canvas)) { rec.releaseSkipped = 'attached'; return; }
            if (canvas.width !== 300 || canvas.height !== 150) { rec.releaseSkipped = 'resized'; return; }
            if (ctx.isContextLost && ctx.isContextLost()) { rec.released = true; return; }
            var ext = ctx.getExtension('WEBGL_lose_context');
            if (!ext) { rec.releaseSkipped = 'no-ext'; return; }
            ext.loseContext();
            rec.released = true;
            GL.probeReleased++;
            log(LOG, 'probe 컨텍스트 해제 (' + rec.type + ', 누적 ' + GL.probeReleased + '개)');
        } catch (e) {
            rec.releaseSkipped = 'error';
        }
    }

    function onContext(canvas, type, ctx, requested, dropped, decision) {
        if (seen) {
            if (seen.has(ctx)) return;
            seen.add(ctx);
        }
        var unity = isUnityCanvas(canvas);
        var attached = isAttached(canvas);
        var probe = !unity && !attached && bootPhase && (nowMs() - T0) < PROBE_WINDOW_MS;
        var rec = {
            type: type,
            role: unity ? 'unity' : (probe ? 'probe' : 'other'),
            t: round1(nowMs()),
            released: false
        };

        if (probe) {
            pushContext(rec);
            // 로더의 probe 는 컨텍스트를 만든 같은 태스크 안에서 getExtension/getParameter 만 읽고 버린다.
            setTimeout(function () { releaseProbe(canvas, ctx, rec); }, 0);
            return;
        }

        rec.requested = requested;
        rec.aaDropped = !!dropped;
        rec.actual = readActual(ctx);
        try { rec.samples = ctx.getParameter(ctx.SAMPLES); } catch (e) {}
        try { rec.drawingBuffer = [ctx.drawingBufferWidth, ctx.drawingBufferHeight]; } catch (e) {}
        pushContext(rec);

        if (unity) {
            bootPhase = false;
            GL.requested = requested;
            GL.actual = rec.actual;
            GL.samples = rec.samples;
            GL.aaDecision = dropped ? 'dropped' : decision;
            if (!loggedUnity) {
                loggedUnity = true;
                try {
                    log(LOG, 'context ' + type
                        + ' requested=' + JSON.stringify(requested)
                        + ' actual=' + JSON.stringify(rec.actual)
                        + ' samples=' + rec.samples
                        + ' drawingBuffer=' + (rec.drawingBuffer ? rec.drawingBuffer.join('x') : '?')
                        + ' dpr=' + (Number(window.__AIT_EFFECTIVE_DPR) || window.devicePixelRatio || 1)
                        + ' aa=' + GL.aaDecision);
                } catch (e) {}
            }
        }
    }

    // ------------------------------------------------------------------
    // getContext 훅
    // ------------------------------------------------------------------
    function installGetContextHook() {
        var proto = window.HTMLCanvasElement && window.HTMLCanvasElement.prototype;
        if (!proto || typeof proto.getContext !== 'function') { GL.aaDecision = 'hook-unavailable'; return; }
        var origGetContext = proto.getContext;

        proto.getContext = function (type, attrs) {
            if (!isGlType(type)) return origGetContext.apply(this, arguments);

            var useArgs = arguments;
            var requested = null;
            var decision = 'unknown';
            var dropped = false;
            try {
                requested = snapshotAttrs(attrs);
                decision = decideAntialias(this, attrs);
                if (decision === 'drop') {
                    var copy = copyAttrs(attrs);
                    copy.antialias = false;
                    useArgs = [type, copy];
                    dropped = true;
                }
            } catch (e) {
                useArgs = arguments;
                dropped = false;
            }

            var ctx = origGetContext.apply(this, useArgs);
            if (!ctx && dropped) {
                // 바꾼 속성으로 생성에 실패했다 — 원래 속성으로 한 번 더 시도한다.
                dropped = false;
                decision = 'fallback-original';
                ctx = origGetContext.apply(this, arguments);
            }
            try { if (ctx) onContext(this, type, ctx, requested, dropped, decision); } catch (e) {}
            return ctx;
        };
    }

    // ------------------------------------------------------------------
    // tier 저장소
    // ------------------------------------------------------------------
    function normalizeCap(cap) {
        for (var i = 0; i < TIER_STEPS.length; i++) {
            if (Math.abs(TIER_STEPS[i] - cap) < 0.001) return TIER_STEPS[i];
        }
        return 0;
    }
    function readStoredTier() {
        try {
            var raw = localStorage.getItem(TIER_KEY);
            if (!raw) return 0;
            var o = JSON.parse(raw);
            var age = o && typeof o.ts === 'number' ? Date.now() - o.ts : -1;
            var cap = o && typeof o.cap === 'number' ? normalizeCap(o.cap) : 0;
            if (!cap || age < 0 || age > TIER_TTL_MS) {
                try { localStorage.removeItem(TIER_KEY); } catch (e) {}
                return 0;
            }
            return cap;
        } catch (e) {
            return 0;
        }
    }
    function writeStoredTier(cap) {
        try { localStorage.setItem(TIER_KEY, JSON.stringify({ cap: cap, ts: Date.now() })); return true; } catch (e) { return false; }
    }
    function crashTier() {
        try {
            var n = Number(window.AITMemory && window.AITMemory.crashCount) || 0;
            if (n >= 3) return 1;
            if (n >= 2) return 1.5;
        } catch (e) {}
        return 0;
    }
    // 부팅 사망 후 저사양 tier(ait-mem.js): 1 → DPR 상한 1.5, 2 → 1.0. 값은 부팅 때 한 번 정해진다.
    function lowMemTierCap() {
        try {
            var t = Number(window.AITMemory && window.AITMemory.lowMemTier) | 0;
            if (t >= 2) return 1;
            if (t >= 1) return 1.5;
        } catch (e) {}
        return 0;
    }
    function lowerPositive(a, b) {
        if (a > 0 && b > 0) return Math.min(a, b);
        return a > 0 ? a : (b > 0 ? b : 0);
    }

    var tierLogged = false;
    GL.tierCap = function () {
        try {
            var lowMem = lowMemTierCap();
            var stored = RECOVERY ? readStoredTier() : 0;
            var crash = RECOVERY ? crashTier() : 0;
            var cap = lowerPositive(lowerPositive(stored, crash), lowMem);
            GL.tier = { stored: stored, crash: crash, lowMem: lowMem, applied: cap };
            if (cap > 0 && !tierLogged) {
                tierLogged = true;
                log(LOG, 'DPR 상한 tier=' + cap + ' (저장값=' + stored + ', crashCount 기준=' + crash + ', lowMemTier 기준=' + lowMem + ')');
            }
            return cap;
        } catch (e) {
            return 0;
        }
    };

    // 현재 실효 DPR 보다 한 단계 낮은 tier. 이미 저장된 tier 가 더 낮으면 그 값을 유지한다.
    function nextTier() {
        var eff = Number(window.__AIT_EFFECTIVE_DPR) || window.devicePixelRatio || 1;
        var next = 1;
        for (var i = 0; i < TIER_STEPS.length; i++) {
            if (TIER_STEPS[i] < eff - 0.001) { next = TIER_STEPS[i]; break; }
        }
        var stored = readStoredTier();
        if (stored > 0 && stored < next) next = stored;
        return next;
    }

    // ------------------------------------------------------------------
    // context 손실 복구
    // ------------------------------------------------------------------
    // 손실 시각을 기록하고 직전 120초 안의 손실 횟수를 돌려준다. 저장소를 못 쓰면 ok=false(루프 가드를 보장할 수 없다).
    function recordLoss(now) {
        try {
            var arr = [];
            var raw = sessionStorage.getItem(LOSS_KEY);
            if (raw) {
                var parsed = JSON.parse(raw);
                if (parsed && typeof parsed.length === 'number') {
                    for (var i = 0; i < parsed.length; i++) {
                        var t = parsed[i];
                        if (typeof t === 'number' && now - t >= 0 && now - t < LOSS_WINDOW_MS) arr.push(t);
                    }
                }
            }
            var prior = arr.length;
            arr.push(now);
            sessionStorage.setItem(LOSS_KEY, JSON.stringify(arr));
            return { prior: prior, ok: true };
        } catch (e) {
            return { prior: 0, ok: false };
        }
    }

    function showOverlay() {
        if (GL.overlayShown) return;
        try {
            var d = document;
            if (d.getElementById(OVERLAY_ID)) { GL.overlayShown = true; return; }
            var root = d.body || d.documentElement;
            if (!root) return;
            var el = d.createElement('div');
            el.id = OVERLAY_ID;
            el.setAttribute('role', 'alert');
            el.style.cssText = 'position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483647;background:#111;color:#fff;'
                + 'display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;text-align:center;'
                + 'font-family:-apple-system,BlinkMacSystemFont,"Apple SD Gothic Neo","Noto Sans KR",sans-serif;';
            var title = d.createElement('div');
            title.style.cssText = 'font-size:18px;font-weight:600;margin-bottom:12px;';
            title.textContent = '화면을 표시할 수 없습니다';
            var desc = d.createElement('div');
            desc.style.cssText = 'font-size:14px;line-height:1.6;opacity:0.85;max-width:320px;';
            desc.textContent = '그래픽 처리가 반복해서 중단되었습니다. 앱을 완전히 종료한 뒤 다시 실행해 주세요.';
            var btn = d.createElement('button');
            btn.type = 'button';
            btn.textContent = '다시 시도';
            btn.style.cssText = 'margin-top:20px;padding:10px 20px;background:#4a90d9;color:#fff;border:none;border-radius:6px;font-size:14px;cursor:pointer;';
            btn.onclick = function () {
                try { sessionStorage.removeItem(LOSS_KEY); } catch (e) {}
                location.reload();
            };
            el.appendChild(title);
            el.appendChild(desc);
            el.appendChild(btn);
            root.appendChild(el);
            GL.overlayShown = true;
        } catch (e) {}
    }

    function doReload() {
        if (GL.reloadScheduled) return;
        GL.reloadScheduled = true;
        warn(LOG, 'WebGL 컨텍스트 손실 복구: 페이지를 다시 불러옵니다');
        try { location.reload(); } catch (e) {}
    }

    function reloadWhenVisible() {
        var hidden = false;
        try { hidden = document.hidden === true; } catch (e) {}
        if (!hidden) {
            setTimeout(doReload, 50);
            return;
        }
        log(LOG, '백그라운드 상태 — 화면이 다시 보일 때 reload 합니다');
        var onVisible = function () {
            try { if (document.hidden) return; } catch (e) {}
            try { document.removeEventListener('visibilitychange', onVisible); } catch (e) {}
            doReload();
        };
        document.addEventListener('visibilitychange', onVisible);
    }

    var lossHandled = false;
    function onContextLost(ev) {
        GL.lossCount++;
        warn(LOG, 'webglcontextlost 이벤트 (' + GL.lossCount + '번째)');
        if (lossHandled) return;
        lossHandled = true;

        try {
            if (window.AITPacing && typeof window.AITPacing.setHint === 'function') window.AITPacing.setHint('glContextLost', true);
        } catch (e) {}

        var cap = 0;
        try {
            cap = nextTier();
            writeStoredTier(cap);
        } catch (e) {}

        var guard = recordLoss(Date.now());
        GL.lastLoss = { tier: cap, prior: guard.prior, guardStorage: guard.ok, hidden: (function () { try { return document.hidden === true; } catch (e) { return false; } })() };

        // 120초 안 두 번째 손실이거나 가드 저장소를 못 쓰면 reload 루프를 막기 위해 reload 하지 않는다.
        if (!guard.ok || guard.prior >= 1) {
            warn(LOG, 'reload 루프 가드 작동 (최근 120초 손실 ' + (guard.prior + 1) + '회, 저장소=' + (guard.ok ? 'ok' : '사용 불가') + ') — 안내 화면을 표시합니다');
            showOverlay();
            return;
        }
        log(LOG, '다음 부팅 DPR 상한 tier=' + cap);
        reloadWhenVisible();
    }

    var boundCanvas = null;
    GL.bindContextLoss = function (canvas) {
        try {
            if (!RECOVERY) return false;
            if (!canvas || typeof canvas.addEventListener !== 'function') return false;
            if (boundCanvas === canvas) return true;
            // preventDefault 를 호출하지 않는다 — 컨텍스트는 복원되지 않고 reload 로 새로 만든다.
            canvas.addEventListener('webglcontextlost', onContextLost, false);
            boundCanvas = canvas;
            return true;
        } catch (e) {
            return false;
        }
    };

    // ------------------------------------------------------------------
    // RT 인벤토리 (?aitglprobe=1)
    // ------------------------------------------------------------------
    var BYTES_PER_PIXEL = {
        0x8058: 4, 0x8051: 3, 0x8056: 2, 0x8057: 2, 0x8D62: 2, 0x1908: 4, 0x1907: 3,
        0x81A5: 2, 0x81A6: 4, 0x8CAC: 4, 0x88F0: 4, 0x8CAD: 8, 0x8D48: 1, 0x84F9: 4,
        0x881A: 8, 0x8814: 16, 0x8C43: 4, 0x8229: 1, 0x822B: 2, 0x822D: 2, 0x822F: 4,
        0x8C3A: 4, 0x8059: 4
    };
    var RT_ROW_MAX = 256;

    function installRtProbe() {
        var rows = {};
        var rowCount = 0;
        var logTimer = null;
        var dirty = false;

        function summary() {
            var list = [];
            var total = 0;
            var ms = 0;
            for (var k in rows) {
                var r = rows[k];
                var each = r.w * r.h * (BYTES_PER_PIXEL[r.format] || 4) * (r.samples > 0 ? r.samples : 1);
                var sum = each * r.count;
                total += sum;
                if (r.kind === 'rbMS') ms += r.count;
                list.push({ kind: r.kind, w: r.w, h: r.h, format: '0x' + r.format.toString(16), samples: r.samples, count: r.count, bytes: sum });
            }
            list.sort(function (a, b) { return b.bytes - a.bytes; });
            return { rows: list, totalBytes: total, rbMSCount: ms };
        }

        // 텍스처 업로드 바이트(P0-1). 래퍼 안에서는 산술만 한다.
        var up = {
            texImage: { n: 0, bytes: 0 },
            compressed: { n: 0, bytes: 0 },
            sub: { n: 0, bytes: 0 },
            view3d: { n: 0, bytes: 0 },
            nonView: 0,
            allocBytes: 0
        };
        var uploadDirty = false;

        function uploadSummary() {
            var total = up.texImage.bytes + up.compressed.bytes + up.sub.bytes + up.view3d.bytes;
            return {
                texImage: { n: up.texImage.n, bytes: up.texImage.bytes },
                compressed: { n: up.compressed.n, bytes: up.compressed.bytes },
                sub: { n: up.sub.n, bytes: up.sub.bytes },
                view3d: { n: up.view3d.n, bytes: up.view3d.bytes },
                nonView: up.nonView,
                totalBytes: total,
                allocBytes: up.allocBytes
            };
        }

        function flushLog() {
            logTimer = null;
            if (dirty) {
                dirty = false;
                try {
                    var s = summary();
                    log(LOG, 'RT 인벤토리 rows=' + s.rows.length + ' 합계=' + (s.totalBytes / 1048576).toFixed(1) + 'MB rbMS=' + s.rbMSCount
                        + ' top=' + JSON.stringify(s.rows.slice(0, 8)));
                } catch (e) {}
            }
            if (uploadDirty) {
                uploadDirty = false;
                try {
                    var u = uploadSummary();
                    var mb = function (b) { return (b / 1048576).toFixed(1); };
                    log(LOG, '텍스처 업로드 합계=' + mb(u.totalBytes) + 'MB (raw ' + mb(u.texImage.bytes) + 'MB x' + u.texImage.n
                        + ', compressed ' + mb(u.compressed.bytes) + 'MB x' + u.compressed.n
                        + ', sub ' + mb(u.sub.bytes) + 'MB x' + u.sub.n
                        + ', 3d ' + mb(u.view3d.bytes) + 'MB x' + u.view3d.n
                        + ') 뷰없음=' + u.nonView + ' alloc=' + mb(u.allocBytes) + 'MB');
                } catch (e) {}
            }
        }

        function touchUpload() {
            uploadDirty = true;
            if (logTimer === null) logTimer = setTimeout(flushLog, 4000);
        }

        // 데이터 인자(ArrayBufferView)의 바이트 수. 뷰가 아니면 -1(이미지/캔버스/PBO 오프셋 등). 범위(srcOffset/length) 인자는 원소 단위로 반영한다.
        function viewBytes(data, srcOffset, length) {
            if (!data || typeof data.byteLength !== 'number' || !ArrayBuffer.isView(data)) return -1;
            var per = data.BYTES_PER_ELEMENT || 1;
            var off = typeof srcOffset === 'number' && srcOffset > 0 ? srcOffset : 0;
            var n = typeof length === 'number' && length > 0 ? length : (data.byteLength / per - off);
            return n > 0 ? n * per : 0;
        }
        function addUpload(bucket, bytes) {
            if (bytes < 0) { up.nonView++; }
            else { bucket.n++; bucket.bytes += bytes; }
            touchUpload();
        }
        // 채널 수 × 채널 크기(또는 패킹 크기)로 픽셀당 바이트 추정. 알 수 없으면 4.
        var FORMAT_CHANNELS = { 0x1908: 4, 0x1907: 3, 0x190A: 2, 0x1909: 1, 0x1906: 1, 0x1903: 1, 0x8227: 2, 0x8228: 2, 0x8D94: 1, 0x8D98: 3, 0x8D99: 4 };
        function pixelBytes(format, type) {
            // 패킹 타입: 4_4_4_4 / 5_5_5_1 / 5_6_5 → 2바이트, 10F_11F_11F_REV / 2_10_10_10_REV → 4바이트
            if (type === 0x8033 || type === 0x8034 || type === 0x8363) return 2;
            if (type === 0x8C3B || type === 0x8368 || type === 0x8C3E || type === 0x84FA) return 4;
            var ch = FORMAT_CHANNELS[format] || 4;
            var size = type === 0x1406 ? 4 : ((type === 0x8D61 || type === 0x140B) ? 2 : (type === 0x1403 || type === 0x1402) ? 2 : (type === 0x1405 || type === 0x1404) ? 4 : 1);
            return ch * size;
        }

        function record(kind, w, h, format, samples) {
            if (!(w > 0) || !(h > 0)) return;
            var key = kind + '|' + w + '|' + h + '|' + format + '|' + samples;
            var r = rows[key];
            if (!r) {
                if (rowCount >= RT_ROW_MAX) return;
                r = rows[key] = { kind: kind, w: w, h: h, format: format, samples: samples, count: 0 };
                rowCount++;
            }
            r.count++;
            dirty = true;
            if (logTimer === null) logTimer = setTimeout(flushLog, 4000);
        }

        function wrap(proto, name, rec) {
            if (!proto || !Object.prototype.hasOwnProperty.call(proto, name)) return;
            var orig = proto[name];
            if (typeof orig !== 'function') return;
            proto[name] = function () {
                try { rec(arguments); } catch (e) {}
                return orig.apply(this, arguments);
            };
        }

        var protos = [];
        try { if (window.WebGLRenderingContext) protos.push(WebGLRenderingContext.prototype); } catch (e) {}
        try { if (window.WebGL2RenderingContext) protos.push(WebGL2RenderingContext.prototype); } catch (e) {}
        for (var i = 0; i < protos.length; i++) {
            var p = protos[i];
            // renderbufferStorage(target, internalformat, width, height)
            wrap(p, 'renderbufferStorage', function (a) { record('rb', a[2], a[3], a[1], 0); });
            // renderbufferStorageMultisample(target, samples, internalformat, width, height)
            wrap(p, 'renderbufferStorageMultisample', function (a) { record('rbMS', a[3], a[4], a[2], a[1]); });
            // texStorage2D(target, levels, internalformat, width, height)
            wrap(p, 'texStorage2D', function (a) { record('texStorage', a[3], a[4], a[2], 0); });
            // texImage2D(target, level, internalformat, width, height, border, format, type, pixels): 픽셀 없이 할당하는 경우만(RT 후보)
            wrap(p, 'texImage2D', function (a) {
                if (a.length === 9 && a[1] === 0 && (a[8] === null || a[8] === undefined)) record('texNull', a[3], a[4], a[2], 0);
                // 업로드 바이트(P0-1). 9인자(+WebGL2 의 srcOffset 10번째): pixels 가 뷰면 byteLength, null 이면 할당만(alloc 추정)
                if (a.length >= 9) {
                    if (a[8] === null || a[8] === undefined) {
                        if (a[3] > 0 && a[4] > 0) { up.allocBytes += a[3] * a[4] * pixelBytes(a[6], a[7]); touchUpload(); }
                    } else {
                        addUpload(up.texImage, viewBytes(a[8], a[9], 0));
                    }
                } else {
                    addUpload(up.texImage, -1); // (target, level, internalformat, format, type, source): 이미지/캔버스 소스
                }
            });
            // compressedTexImage2D(target, level, internalformat, width, height, border, data[, srcOffset[, srcLengthOverride]])
            wrap(p, 'compressedTexImage2D', function (a) { addUpload(up.compressed, viewBytes(a[6], a[7], a[8])); });
            // compressedTexSubImage2D(target, level, xoffset, yoffset, width, height, format, data[, srcOffset[, srcLengthOverride]])
            wrap(p, 'compressedTexSubImage2D', function (a) { addUpload(up.compressed, viewBytes(a[7], a[8], a[9])); });
            // texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, pixels) | (…, format, type, source)
            wrap(p, 'texSubImage2D', function (a) {
                addUpload(up.sub, a.length >= 9 ? viewBytes(a[8], a[9], 0) : -1);
            });
            // WebGL2 3D/배열 텍스처: texImage3D(target, level, internalformat, w, h, d, border, format, type, pixels[, srcOffset])
            wrap(p, 'texImage3D', function (a) { if (a.length >= 10 && a[9] !== null && a[9] !== undefined) addUpload(up.view3d, viewBytes(a[9], a[10], 0)); });
            // texSubImage3D(target, level, x, y, z, w, h, d, format, type, pixels[, srcOffset])
            wrap(p, 'texSubImage3D', function (a) { if (a.length >= 11 && a[10] !== null && a[10] !== undefined) addUpload(up.view3d, viewBytes(a[10], a[11], 0)); });
        }

        GL.rt = { enabled: true, summary: summary, upload: uploadSummary };
        log(LOG, 'RT 인벤토리 활성 (?aitglprobe=1)');
    }

    // ------------------------------------------------------------------
    // 설치
    // ------------------------------------------------------------------
    try { if (HOOK) installGetContextHook(); } catch (e) { GL.aaDecision = 'hook-error'; warn(LOG, 'getContext 훅 설치 실패 — 기본 동작 유지:', e); }
    try {
        if (/[?&]aitglprobe=1(?:&|#|$)/.test(location.search + location.hash)) installRtProbe();
    } catch (e) { warn(LOG, 'RT 인벤토리 설치 실패:', e); }
})();
