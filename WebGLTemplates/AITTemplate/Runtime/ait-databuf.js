/**
 * AIT data 응답 버퍼 레이어 — data Response 를 정확한 Content-Length 로 재포장한다.
 *
 * === 왜 필요한가 ===
 * Unity 로더(2021.3 / 6000.x 공통)는 .data 를 받을 때 응답의 Content-Length 로 버퍼를 한 번 잡는다
 *   (u = new Uint8Array(d), d 는 Content-Length, Content-Encoding 이 br/gzip 이면 압축 크기에 배수를 곱한 추정치).
 * .data.br 를 Content-Encoding: br 로 서빙하면 Content-Length 는 압축 크기라 디코드된 스트림이 버퍼를 넘치거나
 * 모자란다. 넘치면 청크를 따로 모았다가 새 버퍼에 한꺼번에 복사하고(일시 피크 = data 2개 분량),
 * 모자라면 slice 로 한 번 더 복사한다. 이 복사 구간이 모바일 jetsam 에 가장 위험하다.
 * data 의 압축 해제 크기(RAW)를 빌드 때 알고 있으니, 응답을 같은 스트림 그대로 Content-Length=RAW 로
 * 다시 감싸면 로더가 u 를 정확한 크기로 한 번만 잡고 청크를 곧장 채운다(복사 없음).
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: exactDataBody(기본 true), dataRawSize(압축 해제 크기 바이트, 없거나 <=0 이면 재포장 안 함),
 *      unityweb(true 면 아무것도 하지 않음), releaseConsumedData(이 파일에서는 아직 쓰지 않음 — 후속 배치).
 *      객체가 없거나 키가 없으면 기본값으로 동작한다(fail-open).
 *  - window.__AIT_DATABUF.configure(config)
 *      index.html 이 __AIT_PP.configure(config) 직후, createUnityInstance 이전에 호출한다. 던져도 부팅은 계속된다.
 *      config.dataUrl 과 같은 리소스(origin + pathname)로 가는 GET fetch 만 가로챈다.
 *  - window.__AIT_DATABUF.getState() : 진단용 상태 스냅샷(설치 여부, 건너뜀 사유, 재포장 결과). E2E 가 읽는다.
 *
 * === 동작 ===
 *  fetch 는 가장 바깥 래퍼로 얹는다(페이지 캐시/조기 fetch 인라인 스크립트가 head 에서 먼저 설치한 래퍼 위).
 *  data 응답이 ok(200)이고 본문이 압축 전송(Content-Encoding: br/gzip/deflate) 이거나 Content-Length 를 알 수 없을 때만
 *    new Response(res.body, {status, statusText, headers: 원본에서 content-encoding/length 만 바꾼 사본})
 *  로 돌려준다. 본문은 원본과 같은 ReadableStream 이라 복사가 없다.
 *  다음 경우에는 응답을 그대로 돌려준다(no-op).
 *   - exactDataBody 꺼짐, RAW 미상(<=0), .unityweb(Decompression Fallback) 빌드
 *   - Content-Encoding 없이 Content-Length 가 이미 있는 응답(레거시 early-fetch 의 완결 버퍼 재합성, 캐시 히트 등:
 *     이미 정확한 크기). dataCaching 경로도 여기에 해당한다. 레거시 early-fetch 는 이 응답의 본문을 이미 받은
 *     버퍼를 한 번 enqueue 하는 스트림(복사 0)으로 만들어 주므로 이 층이 더 할 일이 없다(WebGLBuildCopier.EarlyFetch.cs).
 *   - ok 가 아니거나 body 가 없는 응답, Response/Headers/fetch 가 없는 환경, 그 밖의 모든 예외(fail-open)
 *  RAW 가 낡아도(실제 크기와 다르면) 로더의 종전 overflow/slice 경로로 돌아갈 뿐 실패하지 않는다.
 *
 *  조기 fetch/페이지 캐시가 응답에 붙인 표식은 새 Response 로 옮긴다.
 *   - window.__aitNonNetworkResp(WeakSet): 멤버십을 복사(index.html 의 wasm 판정이 네트워크 직출 여부를 가린다)
 *   - Response 객체에 직접 붙은 own 프로퍼티: 설명자째 복사
 *   - fetch 래퍼 표식 window.fetch.__aitWrapper: 우리 래퍼도 (하위 fetch 가 네이티브이거나 표식이 있을 때) 단다.
 *     index.html 의 wasm 경로가 이 표식으로 "vConsole 류 몽키패치"와 AIT 자체 래퍼를 구분하므로 빠지면 wasm 직접
 *     스트리밍이 꺼진다.
 *
 * === 저메모리 tier: data 요청 보류 ===
 *  window.AITMemory.lowMemTier(ait-mem.js, 부팅 사망 후 저사양 tier) >= 1 이면 로더의 data GET fetch 를 wasm 컴파일이 끝날 때까지 보류한다.
 *  data 본문(heavy 30MB)이 wasm 컴파일 구조(수백 MB)와 같은 시점에 상주하는 피크를 없애려는 것이다. 재포장 설정(exactDataBody 등)과 무관하게 동작하고
 *  fetch 래퍼는 재포장 래퍼 바깥에 얹는다(보류가 풀린 뒤 같은 체인을 그대로 탄다).
 *  컴파일 완료 신호는 WebAssembly.instantiate/instantiateStreaming/compile/compileStreaming 래퍼가 만든다: 1MB 이상의 바이트 배열 또는 Response(Promise)를
 *  받은 첫 호출이 성공하면 풀린다(실패하면 재시도 경로가 이어서 부르므로 기다린다). 신호가 끝내 없어도 DEFER_MAX_MS(60초)가 지나면 풀린다(로드가 영구히 멈추지 않게).
 *  early-fetch(레거시/modern 인라인 스크립트)는 같은 tier 에서 data 선시작을 하지 않는다 — 선시작하면 이 보류가 다운로드를 늦추지 못한다.
 *  getState() 의 tier / deferInstalled / deferred / deferReason / deferWaitMs 로 확인한다. 로그 태그는 [AIT-DataBuf].
 *
 * ⚠️ Unity 로더보다 먼저 로드되어야 한다 — index.html 의 body 초입(ait-playerprefs.js 다음)에서 로드된다.
 */
(function () {
    'use strict';

    var TAG = '[AIT-DataBuf]';

    // 압축 전송으로 보고 Content-Encoding 을 벗겨도 되는 값. 브라우저 fetch 는 이 값들을 이미 디코드해서 body 를 준다.
    var DECODED_ENCODINGS = { 'br': 1, 'gzip': 1, 'x-gzip': 1, 'deflate': 1, 'zstd': 1 };

    var state = {
        configured: false,
        installed: false,
        // 설치하지 않았거나 응답을 건드리지 않은 이유(진단용). 비어 있으면 정상 동작.
        reason: '',
        dataUrl: '',
        rawSize: -1,
        // 응답 처리 결과
        matched: 0,
        rewrapped: 0,
        skipped: 0,
        lastEncoding: '',
        lastContentLength: '',
        lastResult: '',
        // 저메모리 tier data 보류(진단용)
        tier: 0,
        deferInstalled: false,
        deferred: 0,
        deferReason: '',
        deferWaitMs: -1,
        compiled: false
    };

    var logged = {};

    function logOnce(key, msg, level) {
        if (logged[key]) return;
        logged[key] = true;
        try {
            if (level === 'warn') console.warn(TAG + ' ' + msg);
            else console.log(TAG + ' ' + msg);
        } catch (e) { /* 콘솔 접근 불가 무시 */ }
    }

    function readPerf() {
        try {
            var p = window.__AIT_PERF;
            return (p && typeof p === 'object') ? p : {};
        } catch (e) {
            return {};
        }
    }

    function toUrl(u) {
        try { return new URL(u, location.href); } catch (e) { return null; }
    }

    // fetch(resource) 의 resource 가 문자열 / URL / Request 중 무엇이든 절대 URL 객체로 푼다.
    function requestUrl(resource) {
        try {
            if (typeof resource === 'string') return toUrl(resource);
            if (resource && typeof resource.href === 'string') return toUrl(resource.href);
            if (resource && typeof resource.url === 'string') return toUrl(resource.url);
        } catch (e) { /* 아래 null */ }
        return null;
    }

    function requestMethod(resource, init) {
        try {
            var m = (init && init.method) || (resource && typeof resource === 'object' && resource.method) || 'GET';
            return String(m).toUpperCase();
        } catch (e) {
            return 'GET';
        }
    }

    function sameResource(a, b) {
        return !!a && !!b && a.origin === b.origin && a.pathname === b.pathname;
    }

    function isNativeFetch(f) {
        try { return Function.prototype.toString.call(f).indexOf('[native code]') !== -1; } catch (e) { return false; }
    }

    // 원본 Response 에 붙어 있던 표식을 새 Response 로 옮긴다. 실패는 무시(표식이 없어도 동작은 같다).
    function carryMarkers(from, to) {
        try {
            var nn = window.__aitNonNetworkResp;
            if (nn && typeof nn.has === 'function' && nn.has(from) && typeof nn.add === 'function') nn.add(to);
        } catch (e) { /* ignore */ }
        try {
            var names = Object.getOwnPropertyNames(from);
            for (var i = 0; i < names.length; i++) {
                var n = names[i];
                if (Object.prototype.hasOwnProperty.call(to, n)) continue;
                try { Object.defineProperty(to, n, Object.getOwnPropertyDescriptor(from, n)); } catch (e) { /* ignore */ }
            }
        } catch (e) { /* ignore */ }
        try {
            var syms = Object.getOwnPropertySymbols(from);
            for (var j = 0; j < syms.length; j++) {
                try { Object.defineProperty(to, syms[j], Object.getOwnPropertyDescriptor(from, syms[j])); } catch (e) { /* ignore */ }
            }
        } catch (e) { /* ignore */ }
        // 새 Response 는 url 이 빈 문자열이다. 원본 URL 을 보존한다(로더는 쓰지 않지만 진단/호환용).
        try {
            if (from.url && !to.url) Object.defineProperty(to, 'url', { value: from.url, configurable: true });
        } catch (e) { /* ignore */ }
    }

    // 응답 1건을 판정하고 필요하면 재포장한다. 던지지 않는다(호출부가 fail-open 으로도 감싼다).
    function rewrap(res, raw) {
        state.matched++;
        if (!res || typeof res !== 'object' || !res.headers) return res;
        if (res.type === 'opaque' || res.type === 'opaqueredirect') return res;
        if (!res.ok || res.status !== 200 || !res.body || res.bodyUsed) {
            state.skipped++;
            state.lastResult = 'skip:not-ok-or-no-body';
            logOnce('skip-notok', '재포장 건너뜀: data 응답이 정상(200 + body)이 아님 (status=' + res.status + ')');
            return res;
        }

        var ce = res.headers.get('content-encoding');
        var cl = res.headers.get('content-length');
        var ceToken = ce ? String(ce).trim().toLowerCase() : '';
        state.lastEncoding = ceToken;
        state.lastContentLength = cl === null ? '' : String(cl);

        if (!ceToken && cl !== null) {
            // 압축 전송이 아니고 길이도 이미 아는 응답(캐시 히트, 레거시 early-fetch 의 완결 버퍼 등). 이미 정확하다.
            state.skipped++;
            state.lastResult = 'skip:already-exact';
            logOnce('skip-exact', '재포장 불필요: Content-Encoding 없이 Content-Length=' + cl + ' (이미 정확한 크기, RAW=' + raw + ')');
            return res;
        }
        if (ceToken && !DECODED_ENCODINGS[ceToken]) {
            // 모르는 인코딩(identity 포함)은 브라우저가 디코드했다고 단정할 수 없다 → 건드리지 않는다.
            state.skipped++;
            state.lastResult = 'skip:unknown-encoding';
            logOnce('skip-enc', '재포장 건너뜀: 알 수 없는 Content-Encoding=' + ceToken, 'warn');
            return res;
        }
        if (typeof Headers !== 'function' || typeof Response !== 'function') {
            state.skipped++;
            state.lastResult = 'skip:no-api';
            return res;
        }

        var nh = new Headers();
        res.headers.forEach(function (value, key) {
            if (key === 'content-encoding' || key === 'content-length') return;
            nh.append(key, value);
        });
        nh.set('Content-Length', String(raw));

        // 본문은 같은 ReadableStream 이다(복사 없음). 생성자가 던지면 body 는 아직 잠기지 않았으므로 원본을 그대로 돌려줄 수 있다.
        var out = new Response(res.body, { status: res.status, statusText: res.statusText, headers: nh });
        carryMarkers(res, out);

        state.rewrapped++;
        state.lastResult = 'rewrapped';
        logOnce('rewrap',
            'data 응답 재포장: Content-Encoding=' + (ceToken || '(없음)') +
            ', Content-Length ' + (cl === null ? '(없음)' : cl) + ' → ' + raw +
            ' (본문 스트림 그대로, 복사 없음)');
        try {
            if (typeof performance !== 'undefined' && typeof performance.mark === 'function') performance.mark('ait:databuf-rewrap');
        } catch (e) { /* ignore */ }
        return out;
    }

    function configureRewrap(config) {
        state.configured = true;
        if (state.installed) return; // 중복 호출 방지(이중 래핑 금지)

        var perf = readPerf();
        var enabled = perf.exactDataBody !== false; // 키가 없으면 기본 켜짐
        var raw = (typeof perf.dataRawSize === 'number' && isFinite(perf.dataRawSize)) ? Math.floor(perf.dataRawSize) : -1;
        state.rawSize = raw;

        function skip(reason) {
            state.reason = reason;
            logOnce('install', '재포장 비활성: ' + reason);
        }

        if (!enabled) return skip('exactDataBody=off');
        if (perf.unityweb === true) return skip('Decompression Fallback(.unityweb) 빌드');
        if (!(raw > 0)) return skip('data 압축 해제 크기(dataRawSize)를 모름(' + raw + ')');
        if (typeof window.fetch !== 'function' || typeof Response !== 'function' || typeof Headers !== 'function') {
            return skip('fetch/Response/Headers 미지원');
        }

        var cfg = config || window.unityConfig;
        var dataUrlRaw = cfg && cfg.dataUrl;
        if (!dataUrlRaw || typeof dataUrlRaw !== 'string') return skip('config.dataUrl 없음');
        if (/\.unityweb(\?|#|$)/i.test(dataUrlRaw)) return skip('data 가 .unityweb 확장자');

        var dataUrl = toUrl(dataUrlRaw);
        if (!dataUrl) return skip('config.dataUrl 해석 실패: ' + dataUrlRaw);
        state.dataUrl = dataUrl.href;

        var priorFetch = window.fetch;
        var wrapper = function (resource, init) {
            var p = priorFetch.apply(this, arguments);
            var match = false;
            try {
                match = requestMethod(resource, init) === 'GET' && sameResource(requestUrl(resource), dataUrl);
            } catch (e) { match = false; }
            if (!match) return p; // 다른 요청은 하위 fetch 의 promise 를 그대로 돌려준다(동작 변화 없음).
            return p.then(function (res) {
                try {
                    return rewrap(res, raw);
                } catch (e) {
                    // fail-open: 재포장 도중 예외 → 원본 응답을 그대로 로더에 준다.
                    state.skipped++;
                    state.lastResult = 'error:' + (e && e.message);
                    logOnce('error', '재포장 실패, 원본 응답 사용: ' + (e && e.message), 'warn');
                    return res;
                }
            });
        };

        // index.html 의 wasm 경로가 `fetch.__aitWrapper` 로 AIT 자체 래퍼와 외부 몽키패치를 구분한다.
        // 우리는 data 외 요청을 그대로 통과시키므로 하위가 네이티브이거나 이미 AIT 표식이면 표식을 이어 단다.
        try {
            if (priorFetch.__aitWrapper === true || isNativeFetch(priorFetch)) wrapper.__aitWrapper = true;
        } catch (e) { /* ignore */ }

        window.fetch = wrapper;
        state.installed = true;
        state.reason = '';
        logOnce('install', '재포장 활성: dataRawSize=' + raw + ' bytes, dataUrl=' + dataUrlRaw);
    }

    // ------------------------------------------------------------------
    // 저메모리 tier: data 요청 보류 (wasm 컴파일 완료까지)
    // ------------------------------------------------------------------
    var WASM_MIN_BYTES = 1048576;   // 이보다 작은 바이트 배열은 게임 wasm 으로 보지 않는다(보조 모듈 오탐 방지)
    var DEFER_MAX_MS = 60000;       // 컴파일 신호가 없어도 이 시간 뒤에는 푼다
    var compileDone = false;
    var compileWaiters = [];
    var deferTimer = null;

    function nowMs() {
        try { return performance.now(); } catch (e) { return Date.now(); }
    }

    function lowMemTier() {
        try {
            var m = window.AITMemory;
            return (m && typeof m.lowMemTier === 'number') ? (m.lowMemTier | 0) : 0;
        } catch (e) { return 0; }
    }

    function markCompiled(reason) {
        if (compileDone) return;
        compileDone = true;
        state.compiled = true;
        state.deferReason = reason;
        if (deferTimer) { try { clearTimeout(deferTimer); } catch (e) { /* ignore */ } deferTimer = null; }
        var ws = compileWaiters.splice(0);
        for (var i = 0; i < ws.length; i++) {
            try { ws[i](reason); } catch (e) { /* ignore */ }
        }
    }

    function waitCompiled() {
        return new Promise(function (resolve) {
            if (compileDone) { resolve(state.deferReason); return; }
            compileWaiters.push(resolve);
            if (!deferTimer) {
                deferTimer = setTimeout(function () { deferTimer = null; markCompiled('timeout'); }, DEFER_MAX_MS);
            }
        });
    }

    // 게임 wasm 을 컴파일하는 호출인지: Response / Promise<Response>(스트리밍) 이거나 1MB 이상 바이트 배열.
    // WebAssembly.Module 을 받는 instantiate(module, imports) 는 컴파일이 이미 끝난 뒤이므로 제외한다.
    function isGameWasmSource(src) {
        try {
            if (typeof Response === 'function' && src instanceof Response) return true;
            if (src && typeof src.then === 'function') return true;
            if (src && typeof src.byteLength === 'number') return src.byteLength >= WASM_MIN_BYTES;
        } catch (e) { /* 아래 false */ }
        return false;
    }

    function watchWasmCompile() {
        if (typeof WebAssembly !== 'object' || !WebAssembly) return false;
        var names = ['instantiateStreaming', 'instantiate', 'compileStreaming', 'compile'];
        for (var i = 0; i < names.length; i++) {
            (function (name) {
                var orig = WebAssembly[name];
                if (typeof orig !== 'function' || orig.__aitDataBuf) return;
                var w = function (src) {
                    var p = orig.apply(this, arguments);
                    try {
                        if (!compileDone && isGameWasmSource(src) && p && typeof p.then === 'function') {
                            // 성공만 신호로 본다. 실패하면 호출부가 다른 경로(버퍼 인스턴스화 등)로 이어서 부른다.
                            p.then(function () { markCompiled('compiled'); }, function () { /* 다음 호출을 기다린다 */ });
                        }
                    } catch (e) { /* 관측 실패가 컴파일을 바꾸면 안 된다 */ }
                    return p;
                };
                w.__aitDataBuf = true;
                try { WebAssembly[name] = w; } catch (e) { /* 교체 불가 환경 — 타임아웃이 푼다 */ }
            })(names[i]);
        }
        return true;
    }

    function configureDefer(config) {
        if (state.deferInstalled) return;
        var tier = lowMemTier();
        state.tier = tier;
        if (tier < 1) return;
        if (typeof window.fetch !== 'function' || typeof Promise !== 'function') return;

        var cfg = config || window.unityConfig;
        var dataUrlRaw = cfg && cfg.dataUrl;
        if (!dataUrlRaw || typeof dataUrlRaw !== 'string') {
            logOnce('defer-nourl', 'tier=' + tier + ': config.dataUrl 이 없어 data 보류를 건너뜀', 'warn');
            return;
        }
        var dataUrl = toUrl(dataUrlRaw);
        if (!dataUrl) return;

        watchWasmCompile();

        var prior = window.fetch;
        var wrapper = function (resource, init) {
            var match = false;
            try {
                match = requestMethod(resource, init) === 'GET' && sameResource(requestUrl(resource), dataUrl);
            } catch (e) { match = false; }
            if (!match || compileDone) return prior.apply(this, arguments);
            var self = this, args = arguments, t0 = nowMs();
            state.deferred++;
            logOnce('defer', '저메모리 tier=' + tier + ': data 요청을 wasm 컴파일 완료까지 보류 (최대 ' + (DEFER_MAX_MS / 1000) + '초)');
            return waitCompiled().then(function (reason) {
                state.deferWaitMs = Math.round(nowMs() - t0);
                logOnce('release', 'data 요청 재개 (' + (reason === 'compiled' ? 'wasm 컴파일 완료' : '대기 시간 초과') + ', 보류 ' + state.deferWaitMs + 'ms)');
                return prior.apply(self, args);
            });
        };
        try {
            if (prior.__aitWrapper === true || isNativeFetch(prior)) wrapper.__aitWrapper = true;
        } catch (e) { /* ignore */ }
        window.fetch = wrapper;
        state.deferInstalled = true;
    }

    function configure(config) {
        try {
            configureRewrap(config);
        } catch (e) {
            state.reason = 'configure 예외: ' + (e && e.message);
            logOnce('install-error', 'configure 실패, 재포장 없이 진행: ' + (e && e.message), 'warn');
        }
        try {
            configureDefer(config);
        } catch (e) {
            logOnce('defer-error', 'data 보류 설치 실패, 보류 없이 진행: ' + (e && e.message), 'warn');
        }
    }

    window.__AIT_DATABUF = {
        configure: function (config) {
            configure(config);
        },
        // 진단용 스냅샷(복사본). 상태 객체 자체는 노출하지 않는다.
        getState: function () {
            var o = {};
            for (var k in state) {
                if (Object.prototype.hasOwnProperty.call(state, k)) o[k] = state[k];
            }
            return o;
        }
    };
})();
