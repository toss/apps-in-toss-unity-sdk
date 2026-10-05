/**
 * AIT 소비한 data 구간 해제 레이어 — IL2CPP 가 한 번 읽고 끝나는 global-metadata.dat 의 MEMFS 사본을 놓는다.
 *
 * === 왜 필요한가 ===
 * Unity 로더는 .data 를 ArrayBuffer 하나에 받고, 안의 파일마다 FS_createDataFile(path, null, buf.subarray(..), canRead, canWrite, canOwn=true)
 * 를 부른다. canOwn 이라 MEMFS 는 복사하지 않고 그 subarray 를 node.contents 로 쓴다. 그래서 파일 하나라도 살아 있으면
 * 부모 ArrayBuffer 전체(압축 해제된 .data 전부)가 앱 수명 내내 상주한다. global-metadata.dat(6~25MB)는 IL2CPP 가 부팅 때
 * read() 한 번으로 wasm heap 에 복사한 뒤 다시 읽지 않는다(FS 트레이스, 6000.0/6000.3/2021.3 모두 단일 full read, mmap 없음).
 * data.unity3d / unity default resources 는 엔진이 계속 스트리밍으로 읽으므로 남겨야 한다.
 *
 * === 방법 ===
 *  1) 로더 패치(AITLoaderPatcher, 빌드 타임)가 두 지점에 이 파일의 훅을 건다.
 *       alloc(d, resp)       : 로더가 data 본문 버퍼를 `new Uint8Array(d)` 로 잡는 자리. d 가 data 의 압축 해제 크기(RAW)와 같으면
 *                              크기 조절 가능 ArrayBuffer(new ArrayBuffer(d, {maxByteLength: d}))의 뷰를 돌려준다. 아니면 null(= 로더 원래 동작).
 *       created(node, path)  : FS_createDataFile 호출 직후 결과 노드를 받는다(호출 인자는 그대로, 반환값도 그대로 돌려준다).
 *  2) metadata 파일 노드의 stream_ops 를 노드 단위 사본으로 바꿔 read 를 센다. 파일 전체가 한 번 읽히면(누적 바이트 >= 크기, 마지막 읽기가 EOF)
 *     첫 프레임(rAF, 최대 1.5초 대기) 뒤에 release 한다. 읽기가 한 번 더 일어나면(누적 > 크기) 해제를 영구 취소한다.
 *  3) release: metadata 뒤에 놓인 파일(default resources, 1.6~3.5MB)만 독립 버퍼로 복사해 옮기고, metadata 노드의 contents 를 비우고,
 *     부모 버퍼를 metadata 시작 오프셋으로 resize(shrink)한다. 순수 이득은 metadata 크기만큼이다(옮긴 파일은 새 버퍼가 되므로).
 *     shrink 는 물리 페이지를 돌려준다(Chromium macOS 실측). iOS(JSC)는 실기기 확인 전이라 기본 꺼짐(opt-in)이다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF: releaseConsumedData(true 일 때만 동작), dataRawSize(>0), unityweb(true 면 끔). 객체/키가 없으면 꺼짐(fail-open).
 *      data 가 정확한 Content-Length 로 도착해야(ait-databuf.js 의 exactDataBody) d 가 RAW 와 같다. 아니면 alloc 은 null 이고 stock 동작이다.
 *  - window.__AIT_DATAREL: { alloc, created, getState } — 로더 패치가 호출한다. 로더만 패치되고 이 파일이 없으면 로더가 stock 으로 돌아간다.
 *  - 크기 조절 ArrayBuffer 의 뷰는 TextDecoder.decode 가 거부한다(Chromium: 6000.x 로더가 파일명을 TextDecoder 로 디코드한다).
 *      그래서 alloc 부터 첫 created 직후 매크로태스크까지만 TextDecoder.prototype.decode 를 얕게 감싸 resizable 뷰를 복사해서 넘긴다.
 *      로더의 파일 루프는 동기라 그 구간이 끝나면 원래 decode 로 되돌린다(UTF8ToString 핫패스에 남지 않는다).
 *
 * === 안전 ===
 *  - 지원하지 않는 엔진(ArrayBuffer.prototype.resize 없음: Chrome 111 / iOS 16.4 미만)이면 alloc 이 null → stock.
 *  - metadata 를 mmap 하면 해제하지 않는다. 해제 뒤 읽기가 오면 console.error 로 남긴다(복구는 불가: 엔진이 EOF 를 본다).
 *  - 모든 훅은 던지지 않는다. 실패는 stock 동작 또는 해제 생략으로 끝난다.
 *  - closure 로 원본 뷰를 잡지 않는다(부모 버퍼가 풀리지 않는다).
 */
(function () {
    'use strict';

    var TAG = '[AIT-DataRelease]';
    var MOVE_MAX_BYTES = 16 * 1048576;
    var FRAME_WAIT_MAX_MS = 1500;
    var META_RE = /(^|\/)global-metadata\.dat$/;

    var state = {
        enabled: false,
        reason: 'not-configured',
        rawSize: -1,
        allocated: false,
        files: 0,
        metaFound: false,
        metaReadBytes: 0,
        metaSize: 0,
        armed: false,
        released: false,
        cancelled: '',
        before: 0,
        after: 0,
        movedBytes: 0,
        movedFiles: [],
        lateReads: 0,
        error: ''
    };

    var recs = [];
    var meta = null;
    var shim = null;
    var logged = {};

    function log(key, msg, level) {
        if (key) {
            if (logged[key]) return;
            logged[key] = true;
        }
        try {
            if (level === 'error') console.error(TAG + ' ' + msg);
            else if (level === 'warn') console.warn(TAG + ' ' + msg);
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

    function canResizable() {
        try {
            return typeof ArrayBuffer === 'function' &&
                typeof ArrayBuffer.prototype.resize === 'function' &&
                'resizable' in ArrayBuffer.prototype;
        } catch (e) {
            return false;
        }
    }

    // 동작 조건을 판정한다. 이유가 있으면 state.reason 에 남기고 false.
    function check() {
        var perf = readPerf();
        var raw = (typeof perf.dataRawSize === 'number' && isFinite(perf.dataRawSize)) ? Math.floor(perf.dataRawSize) : -1;
        state.rawSize = raw;
        if (perf.releaseConsumedData !== true) { state.reason = 'releaseConsumedData=off'; return false; }
        if (perf.unityweb === true) { state.reason = 'unityweb'; return false; }
        if (!(raw > 0)) { state.reason = 'dataRawSize=' + raw; return false; }
        if (!canResizable()) { state.reason = 'no-resizable-arraybuffer'; return false; }
        state.reason = '';
        return true;
    }

    // 응답 URL 이 data 가 아니면(조기 fetch 가 만든 합성 Response 는 url 이 빈 문자열이다) 거른다. 판단 불가면 통과.
    function responseIsData(resp) {
        try {
            var cfg = window.unityConfig;
            var du = cfg && cfg.dataUrl;
            if (!resp || !resp.url || !du) return true;
            var a = new URL(resp.url, location.href);
            var b = new URL(du, location.href);
            return a.origin === b.origin && a.pathname === b.pathname;
        } catch (e) {
            return true;
        }
    }

    // ───────────────────────── TextDecoder 구간 shim ─────────────────────────

    function installShim() {
        try {
            if (shim || typeof TextDecoder !== 'function') return;
            var proto = TextDecoder.prototype;
            var orig = proto.decode;
            if (typeof orig !== 'function') return;
            var fn = function (input, opts) {
                try {
                    if (input && input.buffer && input.buffer.resizable) {
                        input = (typeof input.slice === 'function')
                            ? input.slice()
                            : new Uint8Array(input.buffer, input.byteOffset, input.byteLength).slice();
                    }
                } catch (e) { /* 원본에 맡긴다 */ }
                return orig.call(this, input, opts);
            };
            proto.decode = fn;
            shim = { proto: proto, orig: orig, fn: fn };
        } catch (e) {
            shim = null;
        }
    }

    function removeShim() {
        try {
            if (!shim) return;
            // 그 사이 다른 코드가 다시 감쌌다면 건드리지 않는다.
            if (shim.proto.decode === shim.fn) shim.proto.decode = shim.orig;
        } catch (e) { /* ignore */ }
        shim = null;
    }

    // ───────────────────────── 해제 ─────────────────────────

    function cancel(why) {
        if (!state.cancelled) state.cancelled = why;
        log('cancel', '해제 취소: ' + why, 'warn');
        recs = [];
        meta = null;
    }

    function release() {
        if (state.released || state.cancelled || !meta) return;
        try {
            var node = meta.node;
            var buf = node.contents && node.contents.buffer;
            if (!buf || !buf.resizable) return cancel('parent-not-resizable');
            var cut = meta.off;
            var movers = [];
            var total = 0;
            for (var i = 0; i < recs.length; i++) {
                var r = recs[i];
                if (r === meta) continue;
                var c = r.node.contents;
                if (!c || c.buffer !== buf) continue; // 이미 다른 버퍼
                var end = c.byteOffset + c.byteLength;
                if (end > cut) {
                    if (c.byteOffset < cut) return cancel('straddle:' + r.path);
                    movers.push(r);
                    total += c.byteLength;
                }
            }
            if (total > MOVE_MAX_BYTES) return cancel('movers-too-large:' + total);

            // 1) metadata 뒤의 파일을 독립 버퍼로 옮긴다(node.contents 는 MEMFS read 마다 다시 읽는다).
            for (var j = 0; j < movers.length; j++) {
                movers[j].node.contents = movers[j].node.contents.slice();
                state.movedFiles.push(movers[j].path);
            }
            // 2) metadata 를 비우고 부모를 metadata 시작에서 자른다. 앞쪽 파일의 뷰는 길이가 고정이라 그대로 유효하다.
            var size = node.usedBytes;
            node.contents = new Uint8Array(0);
            node.usedBytes = 0;
            state.before = buf.byteLength;
            buf.resize(cut);
            state.after = buf.byteLength;
            state.movedBytes = total;
            state.released = true;
            log('released',
                'metadata ' + size + 'B 해제: 부모 버퍼 ' + state.before + ' → ' + state.after +
                ' (옮긴 파일 ' + movers.length + '개 ' + total + 'B)');
            try {
                if (typeof performance !== 'undefined' && typeof performance.mark === 'function') performance.mark('ait:data-release');
            } catch (e) { /* ignore */ }
        } catch (e) {
            state.error = String((e && e.message) || e);
            log('error', '해제 실패(무시, 메모리만 그대로): ' + state.error, 'warn');
        }
        recs = [];
        meta = null;
    }

    function scheduleRelease() {
        // 읽기가 끝난 시점은 엔진이 wasm 호출 스택 안이다. 매크로태스크로 빠져나온 뒤 첫 프레임(또는 최대 1.5초)에 해제한다.
        try {
            setTimeout(function () {
                var done = false;
                function go() {
                    if (done) return;
                    done = true;
                    release();
                }
                try {
                    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(go);
                } catch (e) { /* 타이머만 */ }
                setTimeout(go, FRAME_WAIT_MAX_MS);
            }, 0);
        } catch (e) {
            cancel('schedule-failed');
        }
    }

    // metadata 노드 하나만 stream_ops 사본으로 바꿔 read/mmap 을 관찰한다(공유 MEMFS.stream_ops 는 건드리지 않는다).
    function watchMeta(node) {
        var ops = node.stream_ops;
        if (!ops || typeof ops.read !== 'function') return cancel('no-stream-ops');
        var copy = {};
        for (var k in ops) copy[k] = ops[k];
        var origRead = ops.read;
        copy.read = function (stream, buffer, offset, length, position) {
            var n = origRead.apply(this, arguments);
            try {
                if (state.released) {
                    state.lateReads++;
                    log('late-read', 'metadata 가 해제된 뒤 읽혔습니다(position=' + position + ', length=' + length + ', n=' + n + ')', 'error');
                } else if (!state.cancelled && meta) {
                    state.metaReadBytes += n > 0 ? n : 0;
                    if (state.metaReadBytes > meta.len) {
                        cancel('metadata-read-more-than-once');
                    } else if (!state.armed && state.metaReadBytes >= meta.len && position + n >= meta.len) {
                        state.armed = true;
                        scheduleRelease();
                    }
                }
            } catch (e) { /* 관찰 실패는 읽기에 영향 없음 */ }
            return n;
        };
        if (typeof ops.mmap === 'function') {
            var origMmap = ops.mmap;
            copy.mmap = function () {
                try {
                    if (!state.released) cancel('metadata-mmap');
                } catch (e) { /* ignore */ }
                return origMmap.apply(this, arguments);
            };
        }
        node.stream_ops = copy;
    }

    // ───────────────────────── 로더 패치가 부르는 훅 ─────────────────────────

    function alloc(d, resp) {
        try {
            if (state.allocated) return null;
            if (!check()) return null;
            if (d !== state.rawSize) return null;
            if (!responseIsData(resp)) return null;
            var ab = new ArrayBuffer(d, { maxByteLength: d });
            var view = new Uint8Array(ab, 0, d);
            state.allocated = true;
            state.enabled = true;
            installShim();
            log('alloc', 'data 본문 버퍼를 크기 조절 가능 ArrayBuffer 로 할당: ' + d + ' bytes');
            return view;
        } catch (e) {
            state.error = String((e && e.message) || e);
            log('alloc-error', '할당 실패, 로더 기본 동작 사용: ' + state.error, 'warn');
            return null;
        }
    }

    function created(node, path) {
        try {
            if (!state.allocated || state.released) return node;
            if (!state.files) {
                // 로더의 파일 루프는 동기다. 첫 파일 직후 매크로태스크면 모든 파일명 디코드가 끝나 있다.
                setTimeout(removeShim, 0);
            }
            state.files++;
            var c = node && node.contents;
            if (!c || !c.buffer || !c.buffer.resizable) return node;
            var rec = { node: node, path: String(path), off: c.byteOffset, len: c.byteLength };
            recs.push(rec);
            if (META_RE.test(rec.path) && !meta) {
                meta = rec;
                state.metaFound = true;
                state.metaSize = rec.len;
                watchMeta(node);
            }
        } catch (e) {
            state.error = String((e && e.message) || e);
        }
        return node;
    }

    window.__AIT_DATAREL = {
        alloc: alloc,
        created: created,
        // 진단용 스냅샷(복사본).
        getState: function () {
            var o = {};
            for (var k in state) {
                if (Object.prototype.hasOwnProperty.call(state, k)) o[k] = state[k];
            }
            return o;
        }
    };
})();
