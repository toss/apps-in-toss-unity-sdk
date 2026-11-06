/**
 * AIT 프리웜 레이어 — 숨김 WebView 로 미리 열린 페이지가 wasm 컴파일까지만 해 두고 엔진 시작을 미룬다.
 *
 * === 왜 필요한가 ===
 * iOS WebKit 은 wasm 컴파일 결과를 디스크에 남기지 않아 매 콜드 로드가 풀 컴파일이다. 호스트(토스 앱)가 사용자 진입 전에
 * 숨긴 WebView 로 같은 index.html 을 열어 두고, 그 WebView 가 보이는 순간 미리 컴파일한 Module 로 바로 인스턴스화하면
 * 같은 WebView 프로세스 안에서 컴파일 비용이 사용자 대기 시간 밖으로 빠진다.
 *
 * === 켜는 방법 ===
 * 쿼리 파라미터가 아니라 빌드 설정(AITEditorScriptObject.prewarm, tri-state, 자동=꺼짐)이다. 빌드가 index.html 의
 * '%AIT_PREWARM%' 를 "true"/"false" 로 치환하고 head 의 인라인 스크립트가 window.__AIT_PREWARM_ENABLED 로 노출한다.
 * 미치환 리터럴은 "true" 가 아니므로 꺼짐(fail-closed = 기존 동작).
 *
 * === 진입 판단 ===
 * 설정이 켜져 있고 이 스크립트가 실행되는 시점의 document.visibilityState === 'hidden' 이면 프리웜 대기에 들어간다.
 * 보이는 상태로 로드되면 아무것도 설치하지 않는다 — gate() 는 콜백을 동기 실행해 기존 경로와 지연 0 으로 같다.
 *
 * === 대기 중에 하는 일 / 하지 않는 일 ===
 *  하는 일: (1) wasm 을 fetch → WebAssembly.compileStreaming 까지(index.html 의 instantiateWasm 과 같은 direct/rewrap 판정),
 *           (2) data 를 fetch 로 끝까지 받아 청크 배열로 보관(저메모리 tier>=1 이면 생략 — 로더가 시작 때 받는다).
 *  하지 않는 일: createUnityInstance 호출(= 프레임워크 실행·wasm Memory 할당·callMain), 오디오, rAF 루프.
 *           index.html 이 createUnityInstance 로 이어지는 블록 전체를 gate() 뒤로 미룬다.
 *  ait-pacing.js 와의 관계: pacing 은 Module.preMainLoop 와 AudioContext 래퍼만 건드린다. 엔진이 없는 동안은 정지할 루프도
 *  오디오도 없고, visible 전이에서 pacing 의 onShow 와 이 파일의 시작이 같은 visibilitychange 로 순서 무관하게 처리된다.
 *
 * === 시작 트리거 (먼저 오는 것 하나) ===
 *  · document visibilitychange → visible
 *  · window.postMessage({type:'ait:prewarm:start'}) (문자열 JSON 도 허용)
 *  · window.AITPrewarm.start()
 *  숨김이 잡히지 않는 WebView(Android 등)는 뒤 둘을 쓴다.
 *
 * === 준비 신호 ===
 *  wasm 컴파일과 data 수신이 끝나면(성공/실패 무관, 상태는 payload) 한 번:
 *    window.postMessage({type:'ait:prewarm:ready', compiled, dataReady, ...}, '*')
 *    window.ReactNativeWebView.postMessage(JSON.stringify(같은 payload))   // 있을 때만
 *  window.AITPrewarm.getState() 로 폴링도 된다.
 *
 * === 폴백 ===
 *  컴파일이 실패하면 Module 을 보관하지 않고, 시작 때 index.html 의 기존 instantiateWasm 경로가 다시 받아 컴파일한다.
 *  data 수신이 실패하면 보관 없이 로더의 fetch 가 원래 경로로 간다. 어떤 예외도 부팅을 막지 않는다(fail-open).
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PREWARM_ENABLED (index.html head 인라인 스크립트)
 *  - window.__AIT_PREWARM.isWaiting() / onStart(fn) / begin(config) / takeModule()  (index.html 이 호출)
 *  - window.AITPrewarm.{start, getState}  (호스트용 공개 API; 꺼져 있어도 존재하며 getState().enabled 가 false)
 *  - 로그 태그: [AIT-Prewarm]
 *
 * ⚠️ ait-databuf.js 다음, Unity 로더보다 먼저 로드한다. begin() 은 fetch 래퍼(early-fetch/page-cache/databuf)가 모두 설치된 뒤
 *    (index.html 메인 스크립트의 configure 블록 다음)에 불러야 같은 fetch 체인을 탄다.
 */
(function () {
    'use strict';

    var TAG = '[AIT-Prewarm]';
    var START_TYPE = 'ait:prewarm:start';
    var READY_TYPE = 'ait:prewarm:ready';

    function nowMs() {
        try { return Math.round(performance.now()); } catch (e) { return 0; }
    }
    function mark(name) {
        try { if (typeof window.__aitMark === 'function') window.__aitMark(name); } catch (e) { /* 무시 */ }
    }
    function log(msg, level) {
        try { (level === 'warn' ? console.warn : console.log)(TAG + ' ' + msg); } catch (e) { /* 무시 */ }
    }
    function isHidden() {
        try { return document.visibilityState === 'hidden'; } catch (e) { return false; }
    }

    var enabled = window.__AIT_PREWARM_ENABLED === true;
    var hiddenAtLoad = isHidden();
    var waiting = enabled && hiddenAtLoad;

    var state = {
        enabled: enabled,
        hiddenAtLoad: hiddenAtLoad,
        // 'off'(꺼짐 또는 보이는 상태로 로드) | 'preparing' | 'ready' | 'started'
        phase: waiting ? 'preparing' : 'off',
        reason: !enabled ? 'disabled' : (hiddenAtLoad ? '' : 'visible-at-load'),
        compiled: false,
        compileMode: '',
        compileMs: -1,
        compileError: '',
        dataReady: false,
        dataBytes: 0,
        dataMs: -1,
        dataSkipped: '',
        dataError: '',
        readyAt: -1,
        startSource: '',
        startAt: -1,
        moduleUsed: false,
        dataServed: false
    };

    var startFns = [];
    var readySent = false;

    // 공개 API는 항상 존재한다(호스트가 꺼진 빌드에서도 안전하게 부를 수 있도록).
    window.AITPrewarm = {
        start: function () { return start('api'); },
        getState: function () {
            var s = {};
            for (var k in state) if (Object.prototype.hasOwnProperty.call(state, k)) s[k] = state[k];
            s.waiting = waiting;
            return s;
        }
    };

    // ------------------------------------------------------------------ 시작
    function start(source) {
        if (!waiting) return false;
        waiting = false;
        state.phase = 'started';
        state.startSource = source;
        state.startAt = nowMs();
        mark('prewarm-start');
        log('시작(' + source + ') — compiled=' + state.compiled + ' dataReady=' + state.dataReady +
            ' preparing=' + (!readySent));
        var fns = startFns;
        startFns = [];
        for (var i = 0; i < fns.length; i++) {
            try { fns[i](); } catch (e) { log('시작 콜백 예외: ' + (e && e.message), 'warn'); }
        }
        return true;
    }

    if (waiting) {
        log('숨김 로드 감지 — 엔진 시작을 미루고 wasm 컴파일/data 수신만 준비');
        try {
            document.addEventListener('visibilitychange', function () {
                if (document.visibilityState === 'visible') start('visible');
            });
            // 일부 WebView 는 숨김 → 표시를 pageshow 로만 알린다(persisted).
            window.addEventListener('pageshow', function (e) {
                if (e && e.persisted && !isHidden()) start('visible');
            });
            window.addEventListener('message', function (e) {
                var d = e && e.data;
                if (typeof d === 'string') {
                    if (d.charAt(0) !== '{') return;
                    try { d = JSON.parse(d); } catch (err) { return; }
                }
                if (d && d.type === START_TYPE) start('message');
            });
        } catch (e) { /* 리스너를 못 달면 start() API 로만 시작된다 */ }
    }

    // ------------------------------------------------------------------ 준비 신호
    function maybeReady() {
        if (readySent || !waiting) return; // 이미 시작했다면 신호가 의미 없다
        if (!state.compileSettled || !state.dataSettled) return;
        readySent = true;
        state.phase = 'ready';
        state.readyAt = nowMs();
        mark('prewarm-ready');
        var payload = {
            type: READY_TYPE,
            compiled: state.compiled, compileMs: state.compileMs, compileMode: state.compileMode,
            dataReady: state.dataReady, dataBytes: state.dataBytes, dataMs: state.dataMs
        };
        log('준비 완료 — compiled=' + state.compiled + ' (' + state.compileMs + 'ms) dataReady=' + state.dataReady +
            ' (' + state.dataBytes + 'B, ' + state.dataMs + 'ms)');
        try { window.postMessage(payload, '*'); } catch (e) { /* 무시 */ }
        try {
            var rn = window.ReactNativeWebView;
            if (rn && typeof rn.postMessage === 'function') rn.postMessage(JSON.stringify(payload));
        } catch (e) { /* 무시 */ }
    }
    state.compileSettled = false;
    state.dataSettled = false;

    // ------------------------------------------------------------------ wasm 컴파일
    var modulePromise = null;   // Promise<WebAssembly.Module|null>
    var moduleValue = null;

    function lastSeg(u) {
        try { return String(u).split('?')[0].split('/').pop(); } catch (e) { return String(u); }
    }

    function compileWasm(config, innerFetch) {
        var codeUrl = config.codeUrl || '';
        var nativeServed = codeUrl.indexOf('.unityweb') === -1;
        var canCompile = typeof WebAssembly !== 'undefined'
            && typeof WebAssembly.compileStreaming === 'function'
            && typeof fetch === 'function' && typeof Response === 'function';
        if (!codeUrl || !nativeServed || !canCompile) {
            state.compileError = !codeUrl ? 'no-code-url' : (!nativeServed ? 'unityweb' : 'no-compile-streaming');
            state.compileSettled = true;
            return Promise.resolve(null);
        }
        var t0 = nowMs();
        var aitWasmPath = window.__AIT_WASM_PATH || {};
        var fetchLooksWrapped = false;
        try { fetchLooksWrapped = innerFetch.toString().indexOf('[native code]') === -1 && !innerFetch.__aitWrapper; } catch (e) {}
        var okKey = (typeof window.__aitWasmHttpOkKey === 'string') ? window.__aitWasmHttpOkKey : '';
        var judgeable = false;
        function setWasmHttpOk(on) {
            if (!okKey || !judgeable) return;
            try {
                if (on) window.localStorage.setItem(okKey, '1');
                else window.localStorage.removeItem(okKey);
            } catch (e) {}
        }
        mark('prewarm-wasm-fetch-start');
        return innerFetch(codeUrl, { credentials: 'same-origin' }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status + ' for ' + lastSeg(codeUrl));
            if (!r.body) throw new Error('no-body');
            var ct = null;
            try { ct = r.headers.get('content-type'); } catch (e) {}
            try {
                aitWasmPath.ct = ct;
                var ce = r.headers.get('content-encoding');
                aitWasmPath.ce = ce ? String(ce).toLowerCase() : null;
                aitWasmPath.jsDecompress = false;
            } catch (e) {}
            try { judgeable = !(window.__aitNonNetworkResp && window.__aitNonNetworkResp.has(r)); } catch (e) { judgeable = false; }
            var httpCacheable = true;
            try { httpCacheable = String(r.headers.get('cache-control') || '').toLowerCase().indexOf('no-store') === -1; } catch (e) {}
            var wasmMime = !!ct && ct.indexOf('application/wasm') !== -1;
            var directOK = false;
            try { directOK = !fetchLooksWrapped && r instanceof Response && !!r.url && wasmMime; } catch (e) {}
            if (directOK) {
                state.compileMode = 'direct';
                return WebAssembly.compileStreaming(r).then(function (m) { setWasmHttpOk(httpCacheable); return m; });
            }
            if (!fetchLooksWrapped && !wasmMime) setWasmHttpOk(false);
            state.compileMode = 'rewrap';
            return WebAssembly.compileStreaming(new Response(r.body, { status: 200, headers: { 'Content-Type': 'application/wasm' } }));
        }).then(function (m) {
            moduleValue = m;
            state.compiled = true;
            state.compileMs = nowMs() - t0;
            try { aitWasmPath.mode = state.compileMode; aitWasmPath.prewarmed = true; } catch (e) {}
            mark('prewarm-wasm-compiled');
            return m;
        }).catch(function (err) {
            // 아무것도 보관하지 않는다 — 시작 때 기존 instantiateWasm 경로가 처음부터 다시 한다.
            moduleValue = null;
            state.compiled = false;
            state.compileError = String((err && err.message) || err).slice(0, 120);
            log('wasm 선컴파일 실패 — 시작 때 정상 경로로 폴백: ' + state.compileError, 'warn');
            return null;
        }).then(function (m) {
            state.compileSettled = true;
            maybeReady();
            return m;
        });
    }

    // ------------------------------------------------------------------ data 선수신 + 서빙
    var dataEntry = null;       // { promise, chunks, total, ct } — 서빙되면 null
    var dataAbs = null;

    function sameResource(a, b) {
        return !!a && !!b && a.origin === b.origin && a.pathname === b.pathname;
    }

    function prefetchData(config, innerFetch) {
        var dataUrl = config.dataUrl || '';
        var tier = 0;
        try { tier = (window.AITMemory && window.AITMemory.lowMemTier) | 0; } catch (e) {}
        if (!dataUrl || typeof fetch !== 'function' || typeof Response !== 'function') {
            state.dataSkipped = 'unsupported';
            state.dataSettled = true;
            return;
        }
        if (tier >= 1) {
            // 저메모리: data 본문을 숨김 WebView 에 상주시키지 않는다(early-fetch/databuf 가 시작 때 받는다).
            state.dataSkipped = 'low-mem-tier';
            state.dataSettled = true;
            return;
        }
        var t0 = nowMs();
        var chunks = [];
        var entry = { chunks: chunks, total: 0, ct: '', promise: null };
        entry.promise = innerFetch(dataUrl, { credentials: 'same-origin' }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            try { entry.ct = r.headers.get('content-type') || ''; } catch (e) {}
            if (!r.body || typeof r.body.getReader !== 'function') {
                return r.arrayBuffer().then(function (buf) { chunks.push(new Uint8Array(buf)); entry.total = buf.byteLength; });
            }
            var reader = r.body.getReader();
            function pump() {
                return reader.read().then(function (res) {
                    if (res.done) return;
                    chunks.push(res.value);
                    entry.total += res.value.byteLength;
                    return pump();
                });
            }
            return pump();
        }).then(function () {
            state.dataReady = true;
            state.dataBytes = entry.total;
            state.dataMs = nowMs() - t0;
            mark('prewarm-data-ready');
            return entry;
        }).catch(function (err) {
            // 부분 수신본은 버린다 — 로더의 fetch 가 원래 경로로 간다.
            chunks.length = 0;
            entry.total = 0;
            state.dataReady = false;
            state.dataError = String((err && err.message) || err).slice(0, 120);
            log('data 선수신 실패 — 시작 때 정상 경로로 폴백: ' + state.dataError, 'warn');
            return null;
        }).then(function (e) {
            state.dataSettled = true;
            maybeReady();
            return e;
        });
        dataEntry = entry;
        try { dataAbs = new URL(dataUrl, location.href); } catch (e) { dataAbs = null; }
        installDataFetch(innerFetch);
    }

    function installDataFetch(innerFetch) {
        var wrapper = function (resource, init) {
            var entry = dataEntry;
            if (entry && dataAbs) {
                var u = null;
                try {
                    var href = typeof resource === 'string' ? resource
                        : (resource && typeof resource.href === 'string' ? resource.href : (resource && resource.url));
                    u = new URL(href, location.href);
                } catch (e) {}
                var method = 'GET';
                try { method = String((init && init.method) || (resource && typeof resource === 'object' && resource.method) || 'GET').toUpperCase(); } catch (e) {}
                if (sameResource(u, dataAbs) && method === 'GET' && !(init && init.signal)) {
                    dataEntry = null; // 한 번만 서빙
                    var self = this, args = arguments;
                    return entry.promise.then(function (e) {
                        if (!e || !e.chunks.length) return innerFetch.apply(self, args);
                        state.dataServed = true;
                        mark('prewarm-data-served');
                        return responseFromChunks(e);
                    });
                }
            }
            return innerFetch.apply(this, arguments);
        };
        // index.html 의 wasm 경로가 "몽키패치된 fetch"와 AIT 자체 래퍼를 구분하는 표식. 하위 fetch 의 판정을 그대로 물려준다.
        try {
            var innerOk = !!innerFetch.__aitWrapper || Function.prototype.toString.call(innerFetch).indexOf('[native code]') !== -1;
            if (innerOk) wrapper.__aitWrapper = true;
        } catch (e) {}
        window.fetch = wrapper;
    }

    // 받아 둔 청크를 정확한 Content-Length 의 새 Response 로 돌려준다(복사 없음, 소비한 청크는 즉시 놓는다).
    function responseFromChunks(e) {
        var headers = { 'Content-Length': String(e.total) };
        if (e.ct) headers['Content-Type'] = e.ct;
        var chunks = e.chunks;
        if (typeof ReadableStream === 'function') {
            var i = 0;
            return new Response(new ReadableStream({
                pull: function (controller) {
                    if (i >= chunks.length) { controller.close(); return; }
                    var c = chunks[i];
                    chunks[i++] = null;
                    controller.enqueue(c);
                }
            }), { status: 200, headers: headers });
        }
        return new Response(new Blob(chunks), { status: 200, headers: headers });
    }

    // ------------------------------------------------------------------ index.html 이 부르는 내부 계약
    window.__AIT_PREWARM = {
        isWaiting: function () { return waiting; },
        // 대기 중에만 쓴다. 시작되면 등록 순서대로 한 번 호출된다.
        onStart: function (fn) {
            if (!waiting) { fn(); return; }
            startFns.push(fn);
        },
        // 대기 중이면 wasm 선컴파일과 data 선수신을 시작한다. 대기가 아니면 아무것도 하지 않는다.
        begin: function (config) {
            if (!waiting || !config) return;
            var innerFetch = window.fetch;
            try {
                modulePromise = compileWasm(config, innerFetch);
            } catch (e) {
                state.compileError = String((e && e.message) || e).slice(0, 120);
                state.compileSettled = true;
                modulePromise = Promise.resolve(null);
            }
            try {
                prefetchData(config, innerFetch);
            } catch (e) {
                state.dataError = String((e && e.message) || e).slice(0, 120);
                state.dataSettled = true;
            }
            maybeReady();
        },
        // 선컴파일 시도가 있었으면 Promise<Module|null>, 없으면 null. 한 번만 넘기고 보관 참조는 놓는다.
        takeModule: function () {
            var p = modulePromise;
            if (!p) return null;
            modulePromise = null;
            return p.then(function (m) {
                var out = moduleValue || m;
                moduleValue = null;
                if (out) state.moduleUsed = true;
                return out;
            });
        }
    };
})();
