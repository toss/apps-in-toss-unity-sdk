/**
 * AIT 메모리 텔레메트리 레이어
 *
 * 하는 일 (동작은 바꾸지 않고 관측만 한다):
 *  1) WebAssembly.Memory.prototype.grow 래퍼 하나로 wasm heap 성장 기록(횟수·소요 ms·크기·실패)을 남긴다.
 *     실기기에서 grow 가 복사형인지(ms 가 크기에 비례하는지) 판정할 데이터를 얻는 것이 목적이다.
 *  2) 이전 세션이 정상 종료 신호 없이 끝났는지 sessionStorage 마커로 추적한다(crashCount).
 *  3) 'ait:memory' window 이벤트와, Unity 인스턴스가 있으면 C# AITMemoryBridge 로 요약 JSON 을 보낸다.
 *     자동 UnloadUnusedAssets 같은 동작은 하지 않는다(C# 쪽 opt-in 플래그뿐).
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: memoryTelemetry(기본 true). 객체가 없거나 키가 없으면 기본값으로 동작한다(fail-open).
 *      선택 키(C# 이 내보내지 않는다. 실험용): memHighMB(기본 256), memCriticalMB(기본 384) — 둘 다 실기기 검증 전 추정치다.
 *  - window.AITMemory.crashCount: number
 *      이전 세션(들)이 '포그라운드에서' 정상 종료 신호 없이 끝난 연속 횟수. 0 이상의 정수. ait-gl.js 의 tierCap() 이
 *      2 이상이면 DPR 상한을 낮추는 근거로 읽는다. memoryTelemetry 가 꺼져 있으면 항상 0.
 *      백그라운드(hidden)에서 죽은 세션은 crashCount 에 넣지 않고 bgKillCount 로 따로 센다(OS 가 숨은 탭을 정리한 것일 수 있다).
 *      한 세션이 STABLE_MS(60초) 넘게 살아 있으면 저장된 연속 횟수는 0 으로 되돌린다(그 세션의 crashCount 값은 부팅 때 값 그대로).
 *  - window.__AIT_HEAP_GROW: { count, failures, totalMs, maxMs, peakBytes, initialBytes, sequenceMB:[...], events:[...] }
 *      grow 호출마다 갱신된다. events 항목: { n, t(ms, performance.now()), ms, from, to, ok, err? }. 최근 128개만 보관.
 *  - window 'ait:memory' 이벤트 detail: { type: 'crash'|'grow'|'grow-failed'|'pressure', level: 'ok'|'high'|'critical',
 *      heapBytes, peakBytes, growCount, growFailures, growTotalMs, growMaxMs, crashCount, bgKillCount }.
 *      grow 안(wasm 호출 스택 위)에서는 절대 동기 발행하지 않고 setTimeout 으로 미룬다(한 태스크에 최대 1회로 합친다).
 *  - C# 연동: AITMemoryBridge(Runtime/Helpers)가 jslib 로 __bridgeRegister() 를 부르면 그때부터 SendMessage('AITMemoryBridge',
 *      'OnMemoryEvent', json) 를 보낸다(대상 오브젝트가 없을 때 Unity 경고를 피하려는 핸드셰이크). grow 이벤트는 1초 이상 간격으로만 보낸다.
 *  - window.AITPacing.setHint('memory', { level, heapBytes }) — 압력 단계가 바뀔 때 ait-pacing.js 에 알린다(없으면 무시).
 *
 * ⚠️ wasm 인스턴스화보다 먼저 로드되어야 한다 — index.html 의 head, 페이지 캐시/조기 fetch 스크립트 뒤에서 동기 로드된다.
 */
(function () {
    'use strict';

    var flags = (typeof window.__AIT_PERF === 'object' && window.__AIT_PERF) || {};
    var enabled = flags.memoryTelemetry !== false;

    var STORAGE_KEY = '__ait_mem_v1';
    var STABLE_MS = 60000;
    var MAX_EVENTS = 128;
    var BRIDGE_GROW_MIN_INTERVAL_MS = 1000;
    var MB = 1048576;
    var HIGH_BYTES = (Number(flags.memHighMB) > 0 ? Number(flags.memHighMB) : 256) * MB;
    var CRITICAL_BYTES = (Number(flags.memCriticalMB) > 0 ? Number(flags.memCriticalMB) : 384) * MB;

    var mem = {
        enabled: enabled,
        crashCount: 0,
        bgKillCount: 0,
        prevSession: 'none', // 'none' | 'exit' | 'foreground' | 'background'
        thresholds: { highBytes: HIGH_BYTES, criticalBytes: CRITICAL_BYTES },
        bridgeReady: false
    };
    window.AITMemory = mem;
    if (!enabled) return;

    // ---------------------------------------------------------------- 세션 마커 (crash-loop 감지)
    function readState() {
        try {
            var raw = window.sessionStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : null;
        } catch (e) { return null; }
    }
    var sessionState = { crashes: 0, bgKills: 0, phase: 'fg' };
    function writeState() {
        try { window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(sessionState)); } catch (e) { /* 저장 불가 환경 — 감지만 생략 */ }
    }
    function isHidden() {
        try { return document.visibilityState === 'hidden' || document.hidden === true; } catch (e) { return false; }
    }

    (function bootMarker() {
        var prev = readState();
        if (prev && typeof prev === 'object') {
            sessionState.crashes = Math.max(0, prev.crashes | 0);
            sessionState.bgKills = Math.max(0, prev.bgKills | 0);
            if (prev.phase === 'fg') {
                sessionState.crashes++;
                mem.prevSession = 'foreground';
            } else if (prev.phase === 'bg') {
                sessionState.bgKills++;
                mem.prevSession = 'background';
            } else if (prev.phase === 'exit') {
                mem.prevSession = 'exit';
            }
        }
        mem.crashCount = sessionState.crashes;
        mem.bgKillCount = sessionState.bgKills;
        sessionState.phase = isHidden() ? 'bg' : 'fg';
        writeState();
    })();

    // pagehide 뒤에는 스펙상 visibilitychange(hidden) 가 이어서 온다. 'exit' 를 'bg' 로 덮어쓰지 않도록 pageshow 전까지 고정한다.
    var exited = false;
    function setPhase(phase) {
        if (exited && phase !== 'exit') return;
        if (sessionState.phase === phase) return;
        sessionState.phase = phase;
        writeState();
    }
    try {
        document.addEventListener('visibilitychange', function () { setPhase(isHidden() ? 'bg' : 'fg'); });
        window.addEventListener('pagehide', function () { setPhase('exit'); exited = true; });
        // bfcache 복원: pagehide 로 'exit' 가 찍힌 뒤 같은 문서가 되살아난 것이므로 다시 살아 있음으로 되돌린다.
        window.addEventListener('pageshow', function () { exited = false; setPhase(isHidden() ? 'bg' : 'fg'); });
        setTimeout(function () {
            // 이 세션은 STABLE_MS 넘게 살아 있었다 → 다음 부팅의 연속 크래시 횟수는 0 부터 센다.
            if (sessionState.crashes !== 0) {
                sessionState.crashes = 0;
                writeState();
            }
        }, STABLE_MS);
    } catch (e) { /* 리스너 등록 실패는 무시 */ }

    // ---------------------------------------------------------------- grow 기록
    var heap = {
        count: 0,
        failures: 0,
        totalMs: 0,
        maxMs: 0,
        peakBytes: 0,
        initialBytes: 0,
        sequenceMB: [],
        events: []
    };
    window.__AIT_HEAP_GROW = heap;

    var currentBytes = 0;
    var level = 'ok';
    function levelFor(bytes, failed) {
        if (failed || bytes >= CRITICAL_BYTES) return 'critical';
        if (bytes >= HIGH_BYTES) return 'high';
        return 'ok';
    }
    function levelRank(l) { return l === 'critical' ? 2 : (l === 'high' ? 1 : 0); }

    function round2(v) { return Math.round(v * 100) / 100; }

    var pendingGrowLogs = [];   // 다음 flush 에서 한 줄로 합쳐 찍는다
    var flushScheduled = false;
    var pendingTypes = {};      // 이번 flush 에 합쳐진 이벤트 종류
    var lastBridgeGrowAt = 0;
    var bridgeGrowTimer = null;

    // grow 래퍼 안에서 호출된다 — 가볍게 기록만 하고 나머지는 전부 setTimeout 으로 미룬다.
    function recordGrow(fromBytes, toBytes, ms, ok, err, wantedPages) {
        heap.count++;
        heap.totalMs = round2(heap.totalMs + ms);
        if (ms > heap.maxMs) heap.maxMs = round2(ms);
        if (!heap.initialBytes) heap.initialBytes = fromBytes;
        var ev = { n: heap.count, t: round2(performance.now()), ms: round2(ms), from: fromBytes, to: toBytes, ok: ok };
        if (!ok) {
            heap.failures++;
            ev.err = String(err && err.name ? err.name : err).slice(0, 60);
            ev.wantedPages = Number(wantedPages) || 0;
        } else {
            currentBytes = toBytes;
            if (toBytes > heap.peakBytes) heap.peakBytes = toBytes;
            heap.sequenceMB.push(round2(toBytes / MB));
        }
        heap.events.push(ev);
        if (heap.events.length > MAX_EVENTS) heap.events.shift();
        pendingGrowLogs.push(ev);

        var type = ok ? 'grow' : 'grow-failed';
        if (ok) {
            var newLevel = levelFor(currentBytes, false);
            // 압력 단계는 한 번 올라가면 내려가지 않는다(wasm heap 은 줄지 않는다).
            if (levelRank(newLevel) > levelRank(level)) { level = newLevel; type = 'pressure'; }
        } else if (levelRank(level) < 1) {
            // Emscripten 은 grow 실패 시 더 작은 크기로 재시도하므로 실패 한 번이 곧 OOM 은 아니다. 'high' 로만 올린다.
            level = 'high';
            type = 'pressure';
        }
        pendingTypes[type] = true;
        scheduleFlush();
    }

    function scheduleFlush() {
        if (flushScheduled) return;
        flushScheduled = true;
        setTimeout(flush, 0);
    }

    function snapshot(type) {
        return {
            type: type,
            level: level,
            heapBytes: currentBytes,
            peakBytes: heap.peakBytes,
            growCount: heap.count,
            growFailures: heap.failures,
            growTotalMs: heap.totalMs,
            growMaxMs: heap.maxMs,
            crashCount: mem.crashCount,
            bgKillCount: mem.bgKillCount
        };
    }

    function dispatchWindowEvent(detail) {
        try {
            var ev;
            if (typeof CustomEvent === 'function') ev = new CustomEvent('ait:memory', { detail: detail });
            else { ev = document.createEvent('CustomEvent'); ev.initCustomEvent('ait:memory', false, false, detail); }
            window.dispatchEvent(ev);
        } catch (e) { /* 리스너 예외가 부팅을 막지 않게 */ }
    }

    function sendToBridge(detail) {
        try {
            var u = window.unityInstance;
            if (!mem.bridgeReady || !u || typeof u.SendMessage !== 'function') return;
            u.SendMessage('AITMemoryBridge', 'OnMemoryEvent', JSON.stringify(detail));
        } catch (e) { /* Unity 가 아직 못 받는 상태 — 다음 이벤트에서 다시 시도 */ }
    }

    function notifyPacing() {
        try {
            if (window.AITPacing && typeof window.AITPacing.setHint === 'function') {
                window.AITPacing.setHint('memory', { level: level, heapBytes: currentBytes });
            }
        } catch (e) { /* 힌트 전달 실패는 무시 */ }
    }

    var lastHintLevel = 'ok';
    function flush() {
        flushScheduled = false;
        var types = pendingTypes;
        pendingTypes = {};

        if (pendingGrowLogs.length) {
            var logs = pendingGrowLogs;
            pendingGrowLogs = [];
            try {
                var okLogs = logs.filter(function (e) { return e.ok; });
                var failLogs = logs.length - okLogs.length;
                var line = '[AIT-Memory] heap grow x' + logs.length;
                if (okLogs.length) {
                    var chain = [round2(okLogs[0].from / MB)];
                    var ms = 0;
                    for (var i = 0; i < okLogs.length; i++) { chain.push(round2(okLogs[i].to / MB)); ms += okLogs[i].ms; }
                    line += ': ' + chain.join('→') + 'MB (' + round2(ms) + 'ms)';
                }
                if (failLogs) line += ' 실패 ' + failLogs + '회';
                if (level !== 'ok') line += ' 압력=' + level;
                console.log(line);
            } catch (e) { /* 로그 실패 무시 */ }
        }

        // 가장 심각한 종류 하나로 합친다.
        var type = types.pressure ? 'pressure' : (types['grow-failed'] ? 'grow-failed' : (types.crash ? 'crash' : 'grow'));
        var detail = snapshot(type);
        dispatchWindowEvent(detail);

        if (level !== lastHintLevel) {
            lastHintLevel = level;
            notifyPacing();
        }

        if (type === 'grow') {
            // C# 쪽은 grow 를 1초 이상 간격으로 합쳐 보낸다(부팅 중 8~10회 연속 grow 로 SendMessage 가 몰리지 않게).
            var now = Date.now();
            if (now - lastBridgeGrowAt >= BRIDGE_GROW_MIN_INTERVAL_MS) {
                lastBridgeGrowAt = now;
                sendToBridge(detail);
            } else if (!bridgeGrowTimer) {
                bridgeGrowTimer = setTimeout(function () {
                    bridgeGrowTimer = null;
                    lastBridgeGrowAt = Date.now();
                    sendToBridge(snapshot('grow'));
                }, BRIDGE_GROW_MIN_INTERVAL_MS);
            }
        } else {
            sendToBridge(detail);
        }
    }

    // ---------------------------------------------------------------- WebAssembly.Memory.prototype.grow 래퍼
    var wrapped = false;
    try {
        var proto = typeof WebAssembly === 'object' && WebAssembly && WebAssembly.Memory && WebAssembly.Memory.prototype;
        if (proto && typeof proto.grow === 'function' && !proto.grow.__aitMem) {
            var origGrow = proto.grow;
            var growWrapper = function (delta) {
                var t0 = performance.now();
                var oldPages;
                try {
                    oldPages = origGrow.apply(this, arguments);
                } catch (e) {
                    // 크기를 알 수 없으니 buffer 는 건드리지 않는다. 실패 직전 크기는 마지막으로 성공한 값을 쓴다.
                    try { recordGrow(currentBytes, currentBytes, performance.now() - t0, false, e, delta); } catch (e2) { /* 기록 실패 무시 */ }
                    throw e;
                }
                try {
                    var from = oldPages * 65536;
                    var to = (oldPages + (Number(delta) || 0)) * 65536;
                    recordGrow(from, to, performance.now() - t0, true);
                } catch (e3) { /* 기록 실패가 grow 결과를 바꾸면 안 된다 */ }
                return oldPages;
            };
            growWrapper.__aitMem = true;
            proto.grow = growWrapper;
            wrapped = true;
        }
    } catch (e) { /* 래퍼 설치 실패 — 세션 마커만 동작 */ }

    // ---------------------------------------------------------------- 공개 API
    mem.getState = function () {
        var s = snapshot('snapshot');
        s.prevSession = mem.prevSession;
        s.wrapped = wrapped;
        s.initialBytes = heap.initialBytes;
        s.bridgeReady = mem.bridgeReady;
        try {
            // Chrome 계열에서만 있다. JS 힙이고 wasm heap 과 별개다.
            if (performance && performance.memory) s.jsHeapUsedBytes = performance.memory.usedJSHeapSize;
        } catch (e) { /* 미지원 */ }
        return s;
    };
    mem.getLevel = function () { return level; };
    mem.snapshotJson = function () {
        try { return JSON.stringify(mem.getState()); } catch (e) { return '{}'; }
    };
    // C# AITMemoryBridge 가 jslib 를 통해 호출한다. 등록 직후 현재 상태를 한 번 보낸다.
    mem.__bridgeRegister = function () {
        mem.bridgeReady = true;
        // unityInstance 는 createUnityInstance 가 끝나야 생긴다. 준비될 때까지 100ms 간격으로 최대 5초 재시도한다.
        var tries = 0;
        (function first() {
            var u = window.unityInstance;
            if (u && typeof u.SendMessage === 'function') {
                sendToBridge(snapshot(mem.crashCount > 0 ? 'crash' : 'snapshot'));
                return;
            }
            if (++tries <= 50) setTimeout(first, 100);
        })();
        return true;
    };

    // 부팅 요약 로그 + 이전 세션이 크래시였다면 이벤트 1회.
    try {
        console.log('[AIT-Memory] 텔레메트리 활성: 이전 세션=' + mem.prevSession + ' crashCount=' + mem.crashCount +
            ' bgKillCount=' + mem.bgKillCount + ' growHook=' + (wrapped ? 'on' : 'off'));
    } catch (e) { /* 로그 실패 무시 */ }
    if (mem.crashCount > 0) {
        pendingTypes.crash = true;
        scheduleFlush();
    }
})();
