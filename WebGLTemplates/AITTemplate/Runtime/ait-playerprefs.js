/**
 * AIT PlayerPrefs 영속화 레이어
 *
 * Unity WebGL의 PlayerPrefs는 Emscripten IDBFS(IndexedDB) 위에 저장되는데,
 * 앱인토스 WebView에서는 IndexedDB 영속성이 보장되지 않는다(iOS ITP 등).
 * 이 스크립트는 Unity가 마운트하는 IDBFS의 syncfs를 감싸서
 * `/idbfs/<hash>/PlayerPrefs` 파일만 앱인토스 Storage(영속 보장)에 미러링한다.
 *
 * - 게임 코드 수정 불필요 (PlayerPrefs API 그대로 사용)
 * - IndexedDB 미러는 그대로 유지 (warm cache / 폴백)
 * - 어떤 실패 경로에서도 부팅을 막지 않는다 (fail-open → vanilla 동작)
 *
 * ⚠️ Unity 로더보다 먼저 로드되어야 하며, index.html이 config 생성 직후
 *    window.__AIT_PP.configure(config)를 호출해야 활성화된다.
 */
(function () {
    'use strict';

    // ===========================================
    // 상수
    // ===========================================
    var LOG_PREFIX = '[AIT-PP]';

    // 앱인토스 Storage 키 (레거시 'ait_' localStorage 접두사와 무충돌)
    var MANIFEST_KEY = 'AITUnityFS_v1_manifest';
    // kill-switch L3: 이번 탭 세션에서 레이어를 완전히 끄는 플래그
    var SESSION_KILL_KEY = '__ait_pp_disabled';
    // 개발용 mock storage 키 접두사
    var MOCK_PREFIX = 'AIT_PP_MOCK_';

    var SNAPSHOT_VERSION = 1;
    var SCOPE = 'playerprefs';
    // 대상 경로: /idbfs/<한 세그먼트>/PlayerPrefs 정확히 일치
    var SCOPE_RE = /^\/idbfs\/[^/]+\/PlayerPrefs$/;
    // 위 파일의 조상 디렉터리(/idbfs/<hash>) — 스냅샷에 함께 담긴다
    var SCOPE_DIR_RE = /^\/idbfs\/[^/]+$/;
    // Unity가 IDBFS를 마운트하는 지점. prejs IdbFs.js가 FS.mkdir('/idbfs') 직후
    // FS.mount(IDBFS, ..., '/idbfs')를 부르므로 마운트포인트는 /idbfs "자체"다 —
    // 앱 디렉터리 /idbfs/<hash>는 그 안쪽이고 마운트포인트가 아니다.
    var IDBFS_ROOT = '/idbfs';

    var STORAGE_POLL_INTERVAL_MS = 50;   // window.AppsInToss.Storage 폴링 간격
    var STORAGE_POLL_TIMEOUT_MS = 1500;  // 폴링 상한
    var DEFAULT_BOOT_TIMEOUT_MS = 2500;  // 스냅샷 대기 상한 (부트 게이트)
    var MAX_MANIFEST_CHARS = 512 * 1024; // 스냅샷 크기 상한 (초과 시 push skip)
    var SETITEM_FAILURE_LIMIT = 3;       // kill-switch L2 임계치
    var BASE64_CHUNK = 8192;             // 8KB 슬라이스 (스택 오버플로 회피)
    var FIRST_PERSIST_LOG_LIMIT = 40;    // 첫 persist 경로 로그 상한

    var LEGACY_READ_TIMEOUT_MS = 1000;   // 레거시 read 자체 상한 (필수 경로인 STORAGE_POLL_TIMEOUT_MS보다 짧게)
    var LEGACY_GATE_RESERVE_MS = 400;    // 부트 게이트 데드라인까지 남겨둘 마진 — 레거시 타임아웃이 항상 먼저 발화하도록
    var LEGACY_MIN_BUDGET_MS = 250;      // 이보다 예산이 적으면 시도조차 하지 않는다
    // 임포트 방향 크기 상한 — push의 MAX_MANIFEST_CHARS와 대칭. base64 인코딩/복호와
    // MEMFS 쓰기는 전부 동기라 타임박스가 선점할 수 없다(거대 페이로드 = 메인 스레드 동결).
    // 상한을 넘긴 덤프는 어차피 MAX_MANIFEST_CHARS 때문에 push도 못 하므로 심을 이유가 없다.
    var LEGACY_MAX_BYTES = 256 * 1024;                              // 원본 바이트 상한
    var LEGACY_MAX_B64_CHARS = 4 * Math.ceil(LEGACY_MAX_BYTES / 3); // 위 상한을 base64 길이로 환산

    // stat.mode 비트 (Emscripten FS와 동일)
    var S_IFMT = 61440;
    var S_IFDIR = 16384;

    // ===========================================
    // 상태
    // ===========================================
    var state = {
        enabled: true,
        isProduction: true,
        bootTimeoutMs: DEFAULT_BOOT_TIMEOUT_MS,
        configured: false,
        preRunRan: false,
        captured: false,
        mode: 'pending',          // 'pending' | 'ait' | 'vanilla' | 'disabled' | 'foreign'
        backend: 'none',          // 'platform' | 'override' | 'none'
        disabled: false,          // AIT 쓰기 금지 여부
        foreign: false,           // manifest 키가 다른 주체(게임 자체 코드 등)의 값으로 이미 사용 중 — 세션 동안 setItem 금지
        restoredBytes: 0,
        mirrorCount: 0,
        persistCount: 0,          // persist(populate=false) 방향이 최종 cb까지 완료된 횟수(성공/실패 무관)
        legacyImport: 'none',     // 'none' | 'skip-mountpoint' | 'skip-budget' | 'skip-unknown-local'
                                  // | 'skip-local-present' | 'skip-unknown-appdir' | 'skip-gate-fired'
                                  // | 'empty' | 'imported' | 'timeout' | 'error'
        legacyBackend: 'none',    // 'none' | 'override' | 'platform'
        legacyBytes: 0,           // 레거시 origin에서 심은 바이트 (restoredBytes와 의미가 다르므로 분리)
        legacyMs: 0,              // readIdbfs 소요 ms (예산 튜닝 관측용)
        lastError: null
    };

    var IDBFS = null;             // 포획한 IDBFS 객체
    var origSyncfs = null;        // 원본 IDBFS.syncfs
    var activeMount = null;       // FS.mount가 만든 mount 객체
    var activeStorage = null;     // 실제 사용 중인 storage 백엔드
    var storagePromise = null;    // resolveStorage() 메모이제이션
    var snapshotPromise = null;   // 스냅샷 fetch (스크립트 로드 즉시 착수)
    var readOk = false;           // 초기 read 성공 여부 — 실패 세션은 절대 쓰지 않는다
    var sessionKilled = false;    // kill-switch L3
    var lastPushedHash = null;    // 변경 없을 때 push 생략
    var remoteHasScoped = false;  // 원격 스냅샷에 scoped 파일이 실려 있는지 — 빈 매니페스트 생성 방지용
    var setItemFailures = 0;      // kill-switch L2 카운터
    var seq = 0;                  // 스냅샷 시퀀스
    var firstPersistLogged = false;
    var legacyImportRan = false;  // 레거시 임포트 세션 내 재진입 가드 (settled는 호출별 지역 변수라 못 막는다)
    var warned = {};              // console.warn 1회 보장용

    // ===========================================
    // 유틸
    // ===========================================
    function log(msg) {
        try { console.log(LOG_PREFIX + ' ' + msg); } catch (e) { /* 로깅 실패는 무시 */ }
    }

    function warnOnce(key, msg) {
        if (warned[key]) return;
        warned[key] = true;
        try { console.warn(LOG_PREFIX + ' ' + msg); } catch (e) { /* 로깅 실패는 무시 */ }
    }

    function recordError(where, e) {
        var detail = e && e.message ? e.message : String(e);
        state.lastError = where + ': ' + detail;
    }

    function setMode(mode) {
        state.mode = mode;
        api.mode = mode;
    }

    function isDirMode(mode) {
        return (mode & S_IFMT) === S_IFDIR;
    }

    function toMillis(ts) {
        if (ts instanceof Date) return ts.getTime();
        var n = Number(ts);
        return isFinite(n) ? n : 0;
    }

    // FNV-1a 32비트 — 변경 감지 전용(암호학적 용도 아님)
    function fnv1a(str) {
        var h = 0x811c9dc5;
        for (var i = 0; i < str.length; i++) {
            h ^= str.charCodeAt(i);
            h = (h + ((h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24))) >>> 0;
        }
        return h.toString(16);
    }

    function encodeBase64(bytes) {
        if (!bytes) return '';
        if (typeof bytes.subarray !== 'function') bytes = new Uint8Array(bytes);
        var parts = [];
        for (var i = 0; i < bytes.length; i += BASE64_CHUNK) {
            parts.push(String.fromCharCode.apply(null, bytes.subarray(i, i + BASE64_CHUNK)));
        }
        return btoa(parts.join(''));
    }

    function decodeBase64(text) {
        var bin = atob(text || '');
        var out = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
        return out;
    }

    function readSessionKill() {
        try { return window.sessionStorage && window.sessionStorage.getItem(SESSION_KILL_KEY) === '1'; }
        catch (e) { return false; }
    }

    function markSessionKill() {
        sessionKilled = true;
        try { if (window.sessionStorage) window.sessionStorage.setItem(SESSION_KILL_KEY, '1'); }
        catch (e) { /* 프라이빗 모드 등 — 무시 */ }
    }

    function clearSessionKill() {
        try { if (window.sessionStorage) window.sessionStorage.removeItem(SESSION_KILL_KEY); }
        catch (e) { /* 무시 */ }
    }

    // ===========================================
    // 설정 (index.html이 주입, 부재 시 기본값 = fail-open)
    // ===========================================
    function readWindowConfig() {
        var cfg = window.__AIT_PLAYERPREFS;
        if (!cfg || typeof cfg !== 'object') return;
        if (typeof cfg.enabled === 'boolean') state.enabled = cfg.enabled;
        if (typeof cfg.isProduction === 'boolean') state.isProduction = cfg.isProduction;
        var t = Number(cfg.bootTimeoutMs);
        if (isFinite(t) && t > 0) state.bootTimeoutMs = t;
    }

    // ===========================================
    // storage 백엔드 해석
    // ===========================================
    function isUsableStorage(s) {
        return !!(s && typeof s.getItem === 'function' && typeof s.setItem === 'function');
    }

    function getOverrideStorage() {
        var o = window.__AIT_PLAYERPREFS_STORAGE__;
        return isUsableStorage(o) ? o : null;
    }

    function getPlatformStorage() {
        var s = window.AppsInToss && window.AppsInToss.Storage;
        return isUsableStorage(s) ? s : null;
    }

    function maybeWarnProdOverride() {
        if (!state.configured || !state.isProduction) return;
        if (!getOverrideStorage()) return;
        warnOnce('prod-override',
            'window.__AIT_PLAYERPREFS_STORAGE__ 오버라이드가 프로덕션에서 사용됩니다. 테스트용 훅이 남아있지 않은지 확인하세요.');
    }

    /**
     * 비프로덕션에서 플랫폼 Storage가 없을 때만 localStorage 기반 mock을 설치한다.
     * ⚠️ window.AppsInToss.Storage에는 절대 대입하지 않는다 — unity-bridge.ts가 통째로 덮어쓴다.
     */
    function installDevMockIfNeeded() {
        if (state.isProduction) return null;
        if (getOverrideStorage()) return getOverrideStorage();
        var mock = {
            getItem: function (key) {
                return new Promise(function (resolve) {
                    resolve(window.localStorage.getItem(MOCK_PREFIX + key));
                });
            },
            setItem: function (key, value) {
                return new Promise(function (resolve) {
                    window.localStorage.setItem(MOCK_PREFIX + key, value);
                    resolve();
                });
            }
        };
        try {
            window.__AIT_PLAYERPREFS_STORAGE__ = mock;
        } catch (e) {
            recordError('dev mock 설치', e);
            return null;
        }
        log('개발용 localStorage mock storage를 설치했습니다 (프로덕션 빌드에서는 동작하지 않음).');
        return mock;
    }

    /** 오버라이드 훅 우선 → 없으면 플랫폼 Storage를 bounded polling으로 대기 */
    function resolveStorage() {
        if (storagePromise) return storagePromise;
        storagePromise = new Promise(function (resolve) {
            var direct = getOverrideStorage();
            if (direct) {
                state.backend = 'override';
                resolve(direct);
                return;
            }
            var waited = 0;
            var tick = function () {
                var override = getOverrideStorage();
                if (override) {
                    state.backend = 'override';
                    maybeWarnProdOverride();
                    resolve(override);
                    return;
                }
                var platform = getPlatformStorage();
                if (platform) {
                    state.backend = 'platform';
                    resolve(platform);
                    return;
                }
                waited += STORAGE_POLL_INTERVAL_MS;
                if (waited >= STORAGE_POLL_TIMEOUT_MS) {
                    var mock = installDevMockIfNeeded();
                    state.backend = mock ? 'override' : 'none';
                    resolve(mock);
                    return;
                }
                setTimeout(tick, STORAGE_POLL_INTERVAL_MS);
            };
            setTimeout(tick, STORAGE_POLL_INTERVAL_MS);
        });
        return storagePromise;
    }

    // ===========================================
    // 스냅샷 fetch
    // ===========================================
    /**
     * manifest 문자열 → 스냅샷 객체({files:{}}).
     * 우리 manifest 형태가 아니면(파싱 실패 포함) throw — 호출자가 foreign으로 분류한다.
     */
    function parseManifest(raw) {
        var parsed = JSON.parse(raw);
        if (!parsed || typeof parsed !== 'object') throw new Error('manifest가 객체가 아닙니다');
        if (typeof parsed.v !== 'number') throw new Error('v 필드가 숫자가 아닙니다');
        if (parsed.v > SNAPSHOT_VERSION) throw new Error('미래 버전 manifest입니다(v=' + parsed.v + ')');
        if (typeof parsed.inline !== 'string') throw new Error('inline 필드가 없습니다');
        var snapshot = JSON.parse(parsed.inline);
        if (!snapshot || typeof snapshot !== 'object') throw new Error('inline 스냅샷이 객체가 아닙니다');
        if (!snapshot.files || typeof snapshot.files !== 'object') throw new Error('files 필드가 없습니다');
        return snapshot;
    }

    function fetchSnapshot() {
        return resolveStorage().then(function (storage) {
            if (!storage) return { kind: 'unavailable' };
            activeStorage = storage;
            return new Promise(function (resolve) {
                // getItem이 동기 throw해도 여기서 잡힌다
                resolve(storage.getItem(MANIFEST_KEY));
            }).then(function (raw) {
                if (raw === null || raw === undefined || raw === '') return { kind: 'absent' };
                try {
                    var snapshot = parseManifest(String(raw));
                    return { kind: 'present', snapshot: snapshot };
                } catch (e) {
                    // 값은 있지만 우리 manifest 형태가 아니다 — 게임이 이 키를 이미 다른
                    // 용도로 쓰고 있을 수 있으므로 "없음"으로 취급해 덮어쓰지 않는다.
                    // 대신 foreign으로 분류해 이번 세션 쓰기를 완전히 차단한다(미래 버전
                    // manifest 포함 — 구버전 SDK가 신버전 포맷을 안전하게 재작성할 수 없다).
                    recordError('스냅샷 파싱', e);
                    return { kind: 'foreign' };
                }
            }, function (e) {
                recordError('스냅샷 읽기', e);
                return { kind: 'unavailable' };
            });
        }).catch(function (e) {
            recordError('스토리지 해석', e);
            return { kind: 'unavailable' };
        });
    }

    function startSnapshotFetch() {
        if (sessionKilled) {
            snapshotPromise = Promise.resolve({ kind: 'unavailable' });
            return;
        }
        snapshotPromise = fetchSnapshot().then(function (res) {
            if (res.kind === 'unavailable') {
                // L1: 가용성 프로브 실패 → 이번 세션만 AIT 쓰기 금지(in-memory).
                // sessionStorage kill(L3)은 걸지 않는다 — 일시적 타이밍 문제(느린 폴링 등)일
                // 수 있으므로 다음 reload에서 다시 시도할 기회를 남겨둔다. L3는 setItem
                // 연속 실패(L2)에서만 사용한다.
                state.disabled = true;
            } else if (res.kind === 'foreign') {
                // 읽기 자체는 성공했지만 우리 manifest가 아니다 — 게임의 기존 값을
                // 보호하기 위해 이번 세션 동안 해당 키에 절대 쓰지 않는다(canWrite 참조).
                readOk = true;
                state.foreign = true;
                warnOnce('foreign-manifest',
                    '저장소 키(' + MANIFEST_KEY + ')가 알 수 없는 값으로 이미 사용 중이라 PlayerPrefs 영속화를 이번 세션에서 비활성화합니다. 기존 값은 보호됩니다.');
            } else {
                readOk = true;
                if (res.kind === 'present') {
                    var s = Number(res.snapshot.seq);
                    if (isFinite(s) && s > seq) seq = s;
                    // 이미 원격에 PlayerPrefs가 있는 스냅샷에서만 "빈 files push"(DeleteAll
                    // 반영)를 허용한다 — pushScoped 참조
                    if (snapshotHasScopedFile(res.snapshot)) remoteHasScoped = true;
                }
            }
            return res;
        });
    }

    // ===========================================
    // scoped 파일 수집 / 직렬화
    // ===========================================
    function loadEntrySync(path) {
        var entry = null;
        var ok = false;
        IDBFS.loadLocalEntry(path, function (err, e) {
            if (!err && e) { ok = true; entry = e; }
        });
        return ok ? entry : null;
    }

    /**
     * /idbfs/<hash>/PlayerPrefs 파일 + 그 조상 디렉터리 엔트리를 동기 수집.
     * 실패 시 null (호출자는 push를 건너뛴다).
     */
    function collectScoped(mount) {
        if (!IDBFS || !mount) return null;
        var localSet = null;
        var called = false;
        var failErr = null;
        try {
            IDBFS.getLocalSet(mount, function (err, set) {
                called = true;
                if (err) failErr = err;
                else localSet = set;
            });
        } catch (e) {
            recordError('getLocalSet', e);
            return null;
        }
        if (!called || failErr || !localSet || !localSet.entries) {
            // 구버전 Emscripten ErrnoError는 message가 없을 수 있어 errno까지 남긴다
            var detail = !called ? '콜백 미호출(비동기 구현?)'
                : failErr ? ('err=' + String(failErr) + (failErr && failErr.errno !== undefined ? ' errno=' + failErr.errno : ''))
                : '결과 집합 없음';
            recordError('getLocalSet', new Error('로컬 파일 목록을 얻지 못했습니다: ' + detail));
            return null;
        }

        var all = Object.keys(localSet.entries);
        var wanted = {};
        var scoped = [];
        for (var i = 0; i < all.length; i++) {
            var p = all[i];
            if (!SCOPE_RE.test(p)) continue;
            scoped.push(p);
            wanted[p] = true;
            wanted[p.slice(0, p.lastIndexOf('/'))] = true; // 조상 디렉터리(/idbfs/<hash>)
        }

        var files = {};
        var keys = Object.keys(wanted).sort();
        for (var k = 0; k < keys.length; k++) {
            var entry = loadEntrySync(keys[k]);
            if (!entry) continue; // 레이스로 사라진 엔트리는 조용히 스킵
            var mode = entry['mode'];
            var rec = { m: mode, t: toMillis(entry['timestamp']) };
            if (!isDirMode(mode)) rec.d = encodeBase64(entry['contents']);
            files[keys[k]] = rec;
        }
        return { files: files, scoped: scoped.sort(), all: all };
    }

    /** 키 오름차순 + 고정 필드 순서 = 해시 안정적인 결정적 직렬화 */
    function serializeFiles(files) {
        var keys = Object.keys(files).sort();
        var out = '';
        for (var i = 0; i < keys.length; i++) {
            var f = files[keys[i]];
            if (i > 0) out += ',';
            out += JSON.stringify(keys[i]) + ':{"m":' + f.m + ',"t":' + f.t;
            if (typeof f.d === 'string') out += ',"d":' + JSON.stringify(f.d);
            out += '}';
        }
        return '{' + out + '}';
    }

    /**
     * 스냅샷에서 읽은 files를 우리 내부 표현으로 정규화 (해시 비교 기준 통일).
     * scope 밖 경로는 버린다 — 손상된 스냅샷이 PlayerPrefs 외 파일을 덮지 못하게 하는 방어선.
     */
    function normalizeFiles(rawFiles) {
        var files = {};
        var keys = Object.keys(rawFiles);
        for (var i = 0; i < keys.length; i++) {
            if (!SCOPE_RE.test(keys[i]) && !SCOPE_DIR_RE.test(keys[i])) continue;
            var v = rawFiles[keys[i]];
            if (!v || typeof v !== 'object') continue;
            var mode = Number(v.m);
            if (!isFinite(mode)) continue;
            var rec = { m: mode, t: toMillis(v.t) };
            if (!isDirMode(mode)) rec.d = typeof v.d === 'string' ? v.d : '';
            files[keys[i]] = rec;
        }
        return files;
    }

    /** V2 검증용: 첫 persist 때 실제 경로 목록을 1회만 남긴다 (경로 가정 확인) */
    function logFirstPersist(mount) {
        if (firstPersistLogged || !IDBFS || !mount) return;
        firstPersistLogged = true;
        var all = [];
        try {
            IDBFS.getLocalSet(mount, function (err, set) {
                if (!err && set && set.entries) all = Object.keys(set.entries).sort();
            });
        } catch (e) {
            recordError('첫 persist 로깅', e);
            return;
        }
        var scoped = all.filter(function (p) { return SCOPE_RE.test(p); });
        log('첫 persist — scoped ' + scoped.length + '개 / 전체 ' + all.length + '개, ' +
            'scoped=' + JSON.stringify(scoped) + ', 전체(최대 ' + FIRST_PERSIST_LOG_LIMIT + '개)=' +
            JSON.stringify(all.slice(0, FIRST_PERSIST_LOG_LIMIT)));
        if (scoped.length === 0 && all.length > 0) {
            warnOnce('scope-miss', 'PlayerPrefs 파일을 찾지 못했습니다. 경로 규칙(/idbfs/<hash>/PlayerPrefs)을 확인하세요.');
        }
    }

    // ===========================================
    // 복원 / 저장
    // ===========================================
    function storeEntrySync(path, entry) {
        var ok = false;
        IDBFS.storeLocalEntry(path, entry, function (err) { ok = !err; if (err) recordError('storeLocalEntry(' + path + ')', err); });
        return ok;
    }

    function removeEntrySync(path) {
        IDBFS.removeLocalEntry(path, function (err) { if (err) recordError('removeLocalEntry(' + path + ')', err); });
    }

    /** AIT 스냅샷을 정본으로 삼아 scoped 영역만 덮어쓴다 */
    function overlayScoped(mount, snapshot) {
        var files = normalizeFiles(snapshot.files);
        var before = collectScoped(mount);
        var keys = Object.keys(files).sort(); // 오름차순 = 디렉터리 우선

        for (var i = 0; i < keys.length; i++) {
            var path = keys[i];
            var rec = files[path];
            var entry = { 'timestamp': rec.t, 'mode': rec.m };
            if (!isDirMode(rec.m)) {
                var bytes;
                try {
                    bytes = decodeBase64(rec.d);
                } catch (e) {
                    recordError('base64 복호(' + path + ')', e);
                    continue;
                }
                entry['contents'] = bytes;
                if (storeEntrySync(path, entry)) state.restoredBytes += bytes.length;
            } else {
                storeEntrySync(path, entry); // storeLocalEntry가 mkdirTree 처리
            }
        }

        // 스냅샷에 없는 scoped 파일 제거 (PlayerPrefs.DeleteAll 반영)
        if (before) {
            for (var s = 0; s < before.scoped.length; s++) {
                var stale = before.scoped[s];
                if (!files[stale]) removeEntrySync(stale);
            }
        }

        // 방금 복원한 내용 = 원격과 동일하므로 곧바로 되밀지 않는다.
        // MEMFS는 storeLocalEntry로 만든 조상 디렉터리의 timestamp를 Date.now()로
        // 덮어쓰므로, 스냅샷 값(files)으로 해시를 계산하면 복원 직후 실제 디스크 상태와
        // 어긋난다. pushScoped와 동일하게 collectScoped(mount)로 실측값을 다시 모아
        // 그 값으로 해시를 계산해야 첫 persist에서 불필요한 push가 발생하지 않는다.
        var after = collectScoped(mount);
        lastPushedHash = fnv1a(serializeFiles(after ? after.files : files));
        log('스냅샷 복원 완료 — 파일 ' + keys.length + '개, ' + state.restoredBytes + 'B');
    }

    // ===========================================
    // 레거시 origin 임포트 (마이그레이션 seam)
    // ===========================================
    function isUsableLegacySource(s) {
        return !!(s && typeof s.readIdbfs === 'function');
    }

    /**
     * 플랫폼의 "옛 origin 저장소 읽기" API를 레거시 소스로 감싼다.
     * 스펙 미확정 — 확정되면 이 함수 본문만 바꾼다.
     *
     * 제약 (본문을 채울 때 반드시 지킬 것):
     *  - 반드시 **동기 탐지**만 한다. resolveStorage()식 폴링을 흉내 내면 레거시 소스가
     *    영영 없는 대다수 부팅에서 부트 게이트 예산을 통째로 태운다.
     *  - activeStorage(플랫폼 Storage) 객체를 경유하지 않는다. 이 레이어의 Storage
     *    접근은 매니페스트 키 1개로 감사되고 있다.
     */
    function getPlatformLegacySource() {
        return null;
    }

    function getOverrideLegacySource() {
        var o = window.__AIT_PP_LEGACY_SOURCE__;
        return isUsableLegacySource(o) ? o : null;
    }

    /** __AIT_PLAYERPREFS_STORAGE__과 동일 위험도의 테스트 훅 — 프로덕션 잔류를 1회 경고한다 */
    function maybeWarnLegacyProdOverride() {
        if (!state.configured || !state.isProduction) return;
        if (!getOverrideLegacySource()) return;
        warnOnce('prod-override-legacy',
            'window.__AIT_PP_LEGACY_SOURCE__ 오버라이드가 프로덕션에서 사용됩니다. 테스트용 훅이 남아있지 않은지 확인하세요.');
    }

    /** 오버라이드 훅 → 플랫폼 seam 순서로 동기 1회 조회. 없으면 null */
    function resolveLegacySource() {
        var o = getOverrideLegacySource();
        if (o) { state.legacyBackend = 'override'; maybeWarnLegacyProdOverride(); return o; }
        var p = getPlatformLegacySource();
        if (isUsableLegacySource(p)) { state.legacyBackend = 'platform'; return p; }
        return null;
    }

    /**
     * 레거시 contents의 바이트 길이. 바이트 배열류가 아니면 -1.
     * ⚠️ 숫자를 여기서 걸러내는 것이 핵심이다 — 플랫폼이 length 필드를 그대로 흘려
     *    contents=500000000 같은 값이 오면 encodeBase64의 `new Uint8Array(bytes)`가
     *    500MB를 조용히 할당해버린다.
     */
    function legacyByteLength(raw) {
        if (typeof raw !== 'object' || raw === null) return -1;
        if (typeof ArrayBuffer !== 'undefined' && raw instanceof ArrayBuffer) return raw.byteLength;
        var n = raw.length;
        if (typeof n !== 'number' || !isFinite(n) || n < 0) return -1;
        return n;
    }

    /**
     * 레거시 IDBFS 덤프(키=절대경로, 값=FILE_DATA 엔트리)에서 scoped 파일 1건만 골라
     * 우리 files 형태로 정규화한다. 판단이 조금이라도 모호하면 null(포기).
     *
     * ⚠️ 조상 디렉터리 엔트리는 만들지 않는다. Emscripten storeLocalEntry는 **디렉터리
     *    엔트리에 한해서만** mkdirTree를 부르고 파일 엔트리에는 FS.writeFile을 부르므로
     *    부모가 이미 있어야 한다. appDir은 호출자가 **현재 로컬 엔트리 목록에 실재하는
     *    경로 중에서** 고른 값이라 존재 자체는 보장된다(resolveAppDir 참조). 그것이
     *    디렉터리인지까지는 확인하지 않는데, getLocalSet의 entries가 timestamp만 싣고
     *    mode는 loadLocalEntry(= 파일이면 내용 전체 읽기)로만 알 수 있어 부트 경로에
     *    올릴 비용이 아니기 때문이다. 만에 하나 파일이면 storeLocalEntry가 ENOTDIR을
     *    콜백으로 돌려주고 0바이트로 끝나(legacyImport='empty') 매니페스트를 건드리지
     *    않으므로 창은 열린 채 남는다. 스코프를 하위 경로까지 넓히면 이 전제가 깨지므로,
     *    그때는 조상 디렉터리 엔트리를 함께 만들어야 한다(overlayScoped 참조).
     */
    function normalizeLegacyDump(dump, appDir) {
        if (!dump || typeof dump !== 'object') return null;
        if (!SCOPE_DIR_RE.test(appDir)) return null;

        var keys = Object.keys(dump);
        var candidates = [];
        for (var i = 0; i < keys.length; i++) {
            if (SCOPE_RE.test(keys[i])) candidates.push(keys[i]);
        }
        candidates.sort();

        // <hash>는 빌드가 서비스되는 URL에서 유도되므로 origin이 바뀌면 값도 바뀐다.
        // 옛 origin의 경로를 그대로 심으면 Unity가 읽지 않는 좌초 경로가 매니페스트에
        // 실려 이후 부팅마다 복원되기만 한다 — 반드시 현재 앱 디렉터리로 리매핑한다.
        var target = appDir + '/PlayerPrefs';
        var picked = null;
        if (candidates.indexOf(target) !== -1) picked = target;
        else if (candidates.length === 1) picked = candidates[0];
        // 정확 일치 없이 후보가 2개 이상이면 어느 게임의 데이터인지 판단할 수 없다
        if (!picked) return null;

        var v = dump[picked];
        if (!v || typeof v !== 'object') return null;
        var mode = Number(v['mode']);
        if (!isFinite(mode) || isDirMode(mode)) return null;

        var d = '';
        var raw = v['contents'];
        if (typeof raw === 'string') {
            // 이미 base64 — 인코딩 없이 길이만으로 상한을 건다
            if (raw.length > LEGACY_MAX_B64_CHARS) {
                recordError('레거시 contents 크기',
                    new Error('base64 ' + raw.length + '자 > 상한 ' + LEGACY_MAX_B64_CHARS + '자'));
                return null;
            }
            d = raw;
        } else if (raw !== null && raw !== undefined) {
            // 인코딩(동기·비선점) 이전에 형태와 크기를 먼저 확정한다
            var len = legacyByteLength(raw);
            if (len < 0) {
                recordError('레거시 contents 형태', new Error('바이트 배열이 아닙니다'));
                return null;
            }
            if (len > LEGACY_MAX_BYTES) {
                recordError('레거시 contents 크기',
                    new Error(len + 'B > 상한 ' + LEGACY_MAX_BYTES + 'B'));
                return null;
            }
            try {
                d = encodeBase64(raw);
            } catch (e) {
                recordError('레거시 contents 인코딩', e);
                return null;
            }
        }

        // 0바이트 파일은 채택하지 않는다(임포트할 내용이 없는 것과 같다). 심어버리면
        // 직후 승격 push에서 col.scoped.length가 1이 되어 "빈 매니페스트를 쓰지 않는다"는
        // 가드를 우회하고, 빈 PlayerPrefs가 정본으로 승격돼 마이그레이션 창이 영구히
        // 닫힌다. 호출자는 이 null을 legacyImport='empty'로 기록한다.
        if (d.length === 0) return null;

        var files = {};
        files[target] = { m: mode, t: toMillis(v['timestamp']), d: d };
        return files;
    }

    /**
     * 레거시 files를 MEMFS에 심고 심은 바이트 수를 돌려준다.
     * ⚠️ overlayScoped와 달리 lastPushedHash를 절대 건드리지 않는다 — 건드리면 직후
     *    승격 push가 "변경 없음"으로 판정돼 통째로 스킵된다(기능이 조용히 무효화).
     * 전제조건상 로컬 scoped 집합이 비어 있으므로 삭제(removeEntrySync)는 하지 않는다.
     */
    function applyLegacyFiles(mount, files) {
        var keys = Object.keys(files).sort();
        var bytes = 0;
        for (var i = 0; i < keys.length; i++) {
            var rec = files[keys[i]];
            var buf;
            try {
                buf = decodeBase64(rec.d);
            } catch (e) {
                recordError('레거시 base64 복호(' + keys[i] + ')', e);
                continue;
            }
            if (storeEntrySync(keys[i], { 'timestamp': rec.t, 'mode': rec.m, 'contents': buf })) {
                bytes += buf.length;
            }
        }
        return bytes;
    }

    /**
     * 현재 세션의 앱 디렉터리(/idbfs/<hash>)를 로컬 파일 목록에서 찾는다.
     *
     * ⚠️ 마운트포인트로 대신할 수 없다. Unity는 IDBFS를 /idbfs 자체에 마운트하고
     *    <hash> 디렉터리는 네이티브가 persistentDataPath를 처음 만질 때 그 안쪽에
     *    만든다 — 즉 mount.mountpoint는 항상 '/idbfs'이지 앱 디렉터리가 아니다.
     *
     * 후보가 0개거나 2개 이상이면 null. 0개는 "이 origin에서 아직 한 번도 부팅한 적
     * 없어 <hash>를 알 방법이 없다"는 뜻이다 — URL에서 유도되는 값이라 우리가 계산할
     * 수 없고, 추측해서 심으면 매니페스트에 좌초 경로가 실려 이후 부팅마다 복원된다.
     * 이 경우 임포트를 포기하는 대신 매니페스트를 쓰지 않아 창을 열어둔다(다음 부팅에
     * 앱 디렉터리가 생기고 나면 그때 발화한다). 2개 이상은 한 origin이 서로 다른 빌드
     * 둘을 서비스한 경우인데, 어느 쪽 데이터인지 알 수 없으므로 똑같이 물러난다.
     *
     * ⚠️ 알려진 한계: "후보 1개"가 곧 "그게 현재 앱 디렉터리"는 아니다. 같은 origin에서
     *    서빙 URL만 바뀌면(경로 버저닝 등) 옛 URL의 디렉터리만 복원된 상태로 이 검사를
     *    통과해 좌초 경로에 심게 되고, 그 결과가 매니페스트로 승격되면 창이 영구히 닫힌다
     *    (옛 디렉터리에 PlayerPrefs가 남아 있으면 skip-local-present로 먼저 빠지므로
     *    위험한 조합은 "PlayerPrefs 없는 stale 디렉터리 1개"뿐이다). 지금은
     *    getPlatformLegacySource()가 stub이라 오버라이드 훅 없이는 도달하지 않는다 —
     *    플랫폼 조회 수단을 연결하기 전에 반드시 해소할 것(TODO.md P2, 테스트 C5).
     *
     * 후보가 디렉터리인지는 검사하지 않는다 — getLocalSet의 entries는 timestamp만 싣고
     * mode는 loadLocalEntry로만 알 수 있는데 그건 파일이면 내용을 통째로 읽는다. 순정
     * Unity 빌드에서 /idbfs 바로 밑에는 앱 디렉터리 말고 아무것도 생기지 않고, 설령
     * 파일이 걸려도 뒤쪽 storeLocalEntry가 ENOTDIR로 조용히 실패할 뿐이라
     * (normalizeLegacyDump 주석 참조) 부트 경로에 동기 읽기를 얹을 값어치가 없다.
     */
    function resolveAppDir(all) {
        var found = null;
        for (var i = 0; i < all.length; i++) {
            if (!SCOPE_DIR_RE.test(all[i])) continue;
            if (found) return null; // 앱 디렉터리 후보가 둘 이상 — 어느 쪽인지 알 수 없다
            found = all[i];
        }
        return found;
    }

    /** 스냅샷에 scoped PlayerPrefs 파일이 하나라도 담겨 있는지 (조상 디렉터리 엔트리는 제외) */
    function snapshotHasScopedFile(snapshot) {
        var files = snapshot && snapshot.files;
        if (!files || typeof files !== 'object') return false;
        var keys = Object.keys(files);
        for (var i = 0; i < keys.length; i++) {
            if (SCOPE_RE.test(keys[i])) return true;
        }
        return false;
    }

    /**
     * 부트 게이트 데드라인까지 LEGACY_GATE_RESERVE_MS를 남기고 쓸 수 있는 read 예산.
     * 호출 시각 기준이라, 사이에 동기 작업이 끼면 반드시 다시 계산해야 한다.
     */
    function legacyBudgetMs(gateArmedAt) {
        return Math.min(
            LEGACY_READ_TIMEOUT_MS,
            state.bootTimeoutMs - (Date.now() - gateArmedAt) - LEGACY_GATE_RESERVE_MS
        );
    }

    /**
     * 마이그레이션 창(= AIT 스냅샷에 PlayerPrefs가 하나도 없는 상태)에서, 로컬도
     * 완전히 빈 부팅에 한해 옛 origin 저장소를 한 번 훑는다.
     *
     * done은 정확히 1회 호출된다. 레거시 소스가 없으면 **동기** 호출이므로 오늘의
     * absent 경로와 같은 tick·같은 순서가 유지된다.
     *
     * 부트 게이트와 레이스하지 않는다 — 자기 타이머 데드라인이 LEGACY_GATE_RESERVE_MS만큼
     * 항상 앞서므로, 레거시 임포트 때문에 게이트가 발화해 vanilla로 강등되는 일이 없다.
     */
    function tryLegacyImport(mount, gateArmedAt, isSettled, done) {
        var finished = false;
        function finishOnce() {
            if (finished) return;
            finished = true;
            done();
        }

        // 재진입: 앞선 실행이 남긴 확정 진단('imported'/'timeout'/...)을 덮어쓰지 않는다
        if (legacyImportRan) { finishOnce(); return; }
        var src = resolveLegacySource();
        if (!src) { state.legacyImport = 'none'; finishOnce(); return; }
        legacyImportRan = true;

        var mp = String((mount && mount.mountpoint) || '').replace(/\/+$/, '');
        if (mp !== IDBFS_ROOT) { state.legacyImport = 'skip-mountpoint'; finishOnce(); return; }

        // 예산이 이미 없으면 collectScoped 비용조차 치르지 않는다
        if (legacyBudgetMs(gateArmedAt) < LEGACY_MIN_BUDGET_MS) {
            state.legacyImport = 'skip-budget'; finishOnce(); return;
        }

        // collectScoped의 getLocalSet은 자체 try/catch를 갖지만 loadEntrySync와
        // encodeBase64는 그렇지 않다. 여기서 동기로 throw하면 populatePath의 try까지
        // 올라가 세션이 통째로 vanilla로 강등되는데(그 경로에서는 legacyImport가 'none'
        // 으로 남아 "훅 미설치"와 구분도 안 된다), 레거시 임포트는 어디까지나 부가
        // 기능이라 실패해도 본 기능을 끌 이유가 없다. 여기서 막고 'skip-unknown-local'
        // 로 기록한다 — pushScoped 경로에서는 Promise executor 안이라 원래 삼켜진다.
        var col = null;
        try {
            col = collectScoped(mount);
        } catch (e) {
            recordError('레거시 임포트용 로컬 수집', e);
        }
        // "비었음"과 "모름"은 다르다 — 모르는 상태에서 심으면 덮어쓰기 금지 원칙을 어긴다
        if (!col) { state.legacyImport = 'skip-unknown-local'; finishOnce(); return; }
        if (col.scoped.length > 0) { state.legacyImport = 'skip-local-present'; finishOnce(); return; }

        // 리매핑 기준. 못 찾으면 심을 곳을 모르는 것이므로 아무것도 하지 않는다.
        var appDir = resolveAppDir(col.all);
        if (!appDir) { state.legacyImport = 'skip-unknown-appdir'; finishOnce(); return; }

        // collectScoped는 전부 동기(getLocalSet + 파일별 loadLocalEntry + base64 인코딩)라
        // 큰 파일 트리에서 수백 ms를 태울 수 있다. 타이머를 거는 **이 시점**의 예산으로
        // 다시 계산해야 실제 게이트 마진이 LEGACY_GATE_RESERVE_MS로 보장된다 —
        // 호출 진입 시각 기준으로 걸면 그 사이 소요분만큼 마진이 잠식돼, 레거시
        // 임포트 때문에 게이트가 먼저 발화하고 vanilla로 강등될 수 있다.
        var budget = legacyBudgetMs(gateArmedAt);
        if (budget < LEGACY_MIN_BUDGET_MS) { state.legacyImport = 'skip-budget'; finishOnce(); return; }

        var t0 = Date.now();
        var timer = setTimeout(function () {
            state.legacyMs = Date.now() - t0;
            state.legacyImport = 'timeout';
            finishOnce();
        }, budget);

        new Promise(function (r) {
            // readIdbfs가 동기 throw해도 여기서 잡힌다
            r(src.readIdbfs());
        }).then(function (dump) {
            if (finished) return; // 타임아웃이 이미 이겼다
            clearTimeout(timer);
            state.legacyMs = Date.now() - t0;
            // 게이트가 이미 발화했다면 Unity가 MEMFS를 읽어간 뒤다 — 절대 건드리지 않는다
            if (isSettled()) { state.legacyImport = 'skip-gate-fired'; finishOnce(); return; }
            var files;
            try {
                files = normalizeLegacyDump(dump, appDir);
                if (files) state.legacyBytes = applyLegacyFiles(mount, files);
            } catch (e) {
                recordError('레거시 임포트 적용', e);
                state.legacyImport = 'error';
                finishOnce();
                return;
            }
            if (!files) { state.legacyImport = 'empty'; finishOnce(); return; }
            state.legacyImport = state.legacyBytes > 0 ? 'imported' : 'empty';
            log('레거시 origin 스냅샷을 채택했습니다 — ' + state.legacyBytes + 'B, ' + state.legacyMs + 'ms');
            finishOnce();
        }, function (e) {
            if (finished) return;
            clearTimeout(timer);
            state.legacyMs = Date.now() - t0;
            recordError('레거시 읽기', e);
            state.legacyImport = 'error';
            finishOnce();
        });
    }

    function canWrite() {
        // state.foreign은 mode !== 'ait'로도 이미 걸리지만(foreign은 vanilla와 동일하게
        // 강등됨), push의 유일한 진입점인 이 함수에서 한 번 더 명시적으로 막아 우회
        // 경로를 남기지 않는다.
        return state.enabled && !state.disabled && !state.foreign && readOk && state.mode === 'ait' && !!activeStorage;
    }

    /** scoped 스냅샷을 앱인토스 Storage에 push. 절대 reject하지 않는다. */
    function pushScoped(mount) {
        return new Promise(function (resolve) {
            logFirstPersist(mount);
            if (!canWrite()) { resolve(false); return; }
            var col = collectScoped(mount);
            if (!col) { resolve(false); return; }

            // 원격에도 로컬에도 PlayerPrefs가 없는 상태에서는 매니페스트를 만들지 않는다.
            // 빈 매니페스트를 한 번 쓰면 다음 부팅부터 스냅샷이 'present'가 되어
            // 마이그레이션 창이 닫힌다 — 지울 것도 실을 것도 없는 push라 얻는 것도 없다.
            // (remoteHasScoped가 true면 DeleteAll 반영이므로 빈 files를 그대로 push한다)
            if (col.scoped.length === 0 && !remoteHasScoped) { resolve(false); return; }

            var filesJson = serializeFiles(col.files);
            var hash = fnv1a(filesJson);
            if (hash === lastPushedHash) { resolve(false); return; } // 변경 없음

            var nextSeq = seq + 1;
            var body = '{"v":' + SNAPSHOT_VERSION + ',"seq":' + nextSeq + ',"scope":"' + SCOPE + '","files":' + filesJson + '}';
            var manifest = '{"v":' + SNAPSHOT_VERSION + ',"seq":' + nextSeq + ',"ts":' + Date.now() + ',"inline":' + JSON.stringify(body) + '}';
            if (manifest.length > MAX_MANIFEST_CHARS) {
                warnOnce('too-large', '스냅샷이 상한(' + MAX_MANIFEST_CHARS + '자)을 초과해 저장하지 않습니다. 크기=' + manifest.length);
                resolve(false);
                return;
            }

            var storage = activeStorage;
            new Promise(function (r) {
                r(storage.setItem(MANIFEST_KEY, manifest));
            }).then(function () {
                seq = nextSeq;
                lastPushedHash = hash;
                remoteHasScoped = col.scoped.length > 0;
                setItemFailures = 0;
                state.mirrorCount++;
                clearSessionKill();
                resolve(true);
            }, function (e) {
                recordError('스냅샷 저장', e);
                setItemFailures++;
                if (setItemFailures >= SETITEM_FAILURE_LIMIT) {
                    // kill-switch L2: 이전 스냅샷은 그대로 두고 이번 세션만 중단 (IndexedDB 미러가 안전망)
                    state.disabled = true;
                    markSessionKill();
                    warnOnce('l2', '앱인토스 Storage 저장이 ' + SETITEM_FAILURE_LIMIT + '회 연속 실패해 PlayerPrefs 영속화를 중단합니다. IndexedDB 미러는 계속 동작합니다.');
                }
                resolve(false);
            });
        }).catch(function (e) {
            recordError('push', e);
            return false;
        });
    }

    // ===========================================
    // syncfs 래퍼
    // ===========================================
    function callOrig(mount, populate, cb) {
        var called = false;
        function once(err) {
            if (called) return;
            called = true;
            cb(err || null);
        }
        try {
            origSyncfs.call(IDBFS, mount, populate, once);
        } catch (e) {
            recordError('origSyncfs(populate=' + populate + ')', e);
            once(e);
        }
    }

    /**
     * 승격 push. 로컬도 레거시도 비어 있는 부팅에서는 pushScoped가 알아서 아무것도
     * 쓰지 않는다 — 레거시 읽기가 timeout/error/skip-budget으로 끝난 부팅에서
     * `{"files":{}}`를 남기면 그 순간 다음 부팅부터 마이그레이션 창이 닫히기 때문이다.
     */
    function scheduleImmediatePush(mount) {
        setTimeout(function () {
            pushScoped(mount).catch(function (e) { recordError('승격 push', e); });
        }, 0);
    }

    function populatePath(mount, callback) {
        var settled = false;
        var gate = null;
        var gateArmedAt = 0;

        function isSettled() { return settled; }

        function finish(mode) {
            if (settled) return;
            settled = true;
            if (gate) clearTimeout(gate);
            setMode(mode);
            // Unity의 addRunDependency 게이트를 푸는 유일한 지점 — 정확히 1회
            try { callback(null); } catch (e) { recordError('populate 콜백', e); }
        }

        /**
         * 마이그레이션 창 처리: 옛 origin을 한 번 훑은 뒤 로컬 상태를 AIT로 승격한다.
         * 레거시 소스가 없으면 tryLegacyImport가 **동기**로 done을 부르므로 어댑터
         * 도입 이전과 같은 tick·같은 순서가 유지된다.
         */
        function importThenPromote() {
            tryLegacyImport(mount, gateArmedAt, isSettled, function () {
                if (settled) return;
                finish('ait');
                scheduleImmediatePush(mount);
            });
        }

        // ① 기존 IndexedDB → MEMFS 복원 (에러는 현행 Unity 동작대로 삼킨다)
        // 부트 게이트 타이머는 ①이 끝난 뒤(= ② 스냅샷 대기 직전)에만 건다 — ①까지
        // 감싸면 저사양 기기/IDB 경합으로 원본 populate가 늦어질 때 게이트가 먼저
        // 발화해 순정 대비 회귀(정상 데이터를 빈 상태로 취급)가 생긴다.
        callOrig(mount, true, function (populateErr) {
            // 원본과 동일하게 삼키되(Unity도 로그만 남기고 진행) 관측 가능하게 기록
            if (populateErr) recordError('원본 populate', populateErr);
            if (settled) return;

            // ② AIT 스냅샷 대기만 타임박스한다
            // 레거시 임포트 예산의 기준점은 반드시 "게이트를 건 시각"이어야 한다
            gateArmedAt = Date.now();
            gate = setTimeout(function () {
                // 스냅샷을 기다리다 부팅을 막을 수는 없다 — vanilla로 강등하고 진행.
                // L3 세션 kill은 여기서 걸지 않는다(가용성 프로브 실패는 일시적일 수
                // 있으므로) — 다음 reload에서 다시 시도할 기회를 남겨둔다.
                recordError('부트 게이트', new Error('스냅샷 대기 타임아웃(' + state.bootTimeoutMs + 'ms)'));
                state.disabled = true;
                finish('vanilla');
            }, state.bootTimeoutMs);

            var pending = snapshotPromise || Promise.resolve({ kind: 'unavailable' });
            pending.then(function (res) {
                if (settled) return;
                try {
                    if (res.kind === 'present') {
                        // ② AIT 스냅샷이 정본 — scoped 영역만 덮어쓴다
                        overlayScoped(mount, res.snapshot);
                        if (snapshotHasScopedFile(res.snapshot)) {
                            finish('ait');
                        } else {
                            // 매니페스트는 있는데 PlayerPrefs가 하나도 없다 = 마이그레이션
                            // 창이 아직 열려 있는 상태다. 'absent'에만 걸어두면 이전 버전이
                            // 남긴 빈 매니페스트(또는 데이터가 생기기 전 부팅)만으로 창이
                            // 닫혀, 정작 이관이 필요한 사용자에게 seam이 영영 발화하지 않는다.
                            importThenPromote();
                        }
                    } else if (res.kind === 'absent') {
                        // 마이그레이션: 기존 IndexedDB 데이터를 채택하고 즉시 AIT로 승격.
                        // 로컬이 완전히 빈 부팅에 한해 그 직전에 옛 origin 저장소를 한 번 훑는다.
                        // Unity는 callback(null) 직후 MEMFS를 읽으므로 finish는 임포트 뒤에 온다.
                        importThenPromote();
                    } else if (res.kind === 'foreign') {
                        // 다른 주체가 이미 이 키를 쓰고 있다 — 오버레이도, 승격 push도 하지 않는다
                        finish('foreign');
                    } else {
                        finish('vanilla');
                    }
                } catch (e) {
                    recordError('스냅샷 적용', e);
                    finish('vanilla');
                }
            }).catch(function (e) {
                recordError('스냅샷 대기', e);
                finish('vanilla');
            });
        });
    }

    function persistPath(mount, callback) {
        var finished = false;
        function done() {
            if (finished) return;
            finished = true;
            // persist(populate=false) 방향의 최종 완료 시점(원본 syncfs + push 처리 포함,
            // 성공/실패 무관) — E2E가 reload 전 커밋 완료를 관측하는 용도.
            state.persistCount++;
            api.persistCount = state.persistCount;
            try { callback(null); } catch (e) { recordError('persist 콜백', e); }
        }

        logFirstPersist(mount);

        // AIT 모드가 아니면 100% 원본 위임 (동작 무회귀)
        if (state.mode !== 'ait') {
            callOrig(mount, false, function (err) {
                // 원본과 동일하게 삼키되(회귀 없음) status().lastError로는 관측 가능하게
                if (err) recordError('원본 persist', err);
                done();
            });
            return;
        }

        // 자체 디바운스 없음 — Unity의 queuePersist(idbPersistState/'again')가 이미 코얼레싱한다
        var pending = 2;
        function step() { if (--pending <= 0) done(); }

        pushScoped(mount).then(step, function (e) { recordError('push', e); step(); });
        callOrig(mount, false, function (err) {
            if (err) recordError('IndexedDB 미러', err);
            step(); // warm mirror 실패는 치명적이지 않다
        });
    }

    function aitSyncfs(mount, populate, callback) {
        if (populate) populatePath(mount, callback);
        else persistPath(mount, callback);
    }

    /**
     * Unity의 IDBFS persist 큐가 완전히 idle인지. mount.idbPersistState는
     * 0/undefined=idle, setTimeout 핸들=시작 대기, 'idb'=진행 중, 'again'=진행 중+추가 대기.
     * persistCount 증가만으로는 "마지막 쓰기가 커밋됐다"를 보장할 수 없다 —
     * 쓰기 이전에 수집을 시작한 persist의 완료일 수 있다. E2E가 reload 전
     * count 증가와 이 idle을 함께 확인하면 코얼레싱 간극 없이 커밋 완료가 보장된다.
     */
    function persistIdle() {
        return !activeMount || !activeMount.idbPersistState;
    }

    /**
     * 진단용(E2E·실기기 콘솔): 현재 MEMFS의 scoped 파일 경로/크기(base64 길이)/mtime 목록.
     * 캡처 전이거나 수집 실패 시 null — 값 내용은 노출하지 않는다.
     */
    function debugScopedFiles() {
        if (!activeMount) return null;
        var col = collectScoped(activeMount);
        if (!col) return null;
        var out = [];
        var keys = Object.keys(col.files).sort();
        for (var i = 0; i < keys.length; i++) {
            var rec = col.files[keys[i]];
            out.push({ path: keys[i], dir: typeof rec.d !== 'string', bytes: typeof rec.d === 'string' ? rec.d.length : 0, t: rec.t });
        }
        return out;
    }

    // ===========================================
    // 마운트 트랩 (preRun)
    // ===========================================
    var REQUIRED_IDBFS_FNS = ['syncfs', 'getLocalSet', 'loadLocalEntry', 'storeLocalEntry', 'removeLocalEntry'];

    function onMountAssigned(mountRoot) {
        var mount = mountRoot && mountRoot.mount;
        var idbfs = mount && mount.type;
        if (!idbfs) {
            warnOnce('no-mount', 'IDBFS 마운트를 인식하지 못했습니다. PlayerPrefs 영속화 없이 진행합니다.');
            setMode('vanilla');
            return;
        }
        for (var i = 0; i < REQUIRED_IDBFS_FNS.length; i++) {
            if (typeof idbfs[REQUIRED_IDBFS_FNS[i]] !== 'function') {
                // self-check 실패: 아무것도 덮지 않고 순정 동작 유지
                warnOnce('self-check', 'IDBFS API(' + REQUIRED_IDBFS_FNS[i] + ')를 찾지 못해 PlayerPrefs 영속화를 건너뜁니다.');
                setMode('vanilla');
                return;
            }
        }
        IDBFS = idbfs;
        activeMount = mount;
        origSyncfs = idbfs.syncfs;
        idbfs.syncfs = aitSyncfs;
        state.captured = true;
        api.captured = true;
        log('IDBFS 포획 완료 (mountpoint=' + mount.mountpoint + ')');
    }

    function trapFn(Module) {
        state.preRunRan = true;
        api.preRunRan = true;
        // Emscripten callRuntimeCallbacks가 Module을 인자로 넘긴다. 없으면 트랩 불가 → vanilla
        if (!Module || typeof Module !== 'object') {
            recordError('preRun', new Error('Module 인자가 없습니다'));
            setMode('vanilla');
            return;
        }
        // 방어적 폴백: buildPreRunArray가 순서를 보장하지만(아래), 혹시라도 다른 preRun
        // 항목이 이보다 먼저 실행되어 __unityIdbfsMount에 이미 값이 대입된 상태라면
        // (Unity의 IdbFs prejs가 FS.mount 결과를 plain 대입으로 써넣은 뒤) 여기서
        // defineProperty로 덮어쓰면 원본 값이 getter 내부의 undefined slot으로
        // 가려져 Unity 자신의 JS_FileSystem_Sync가 깨진다. 이미 값이 대입된 경우
        // 절대 덮어쓰지 않고 이번 세션만 vanilla로 강등한다.
        var existingDesc = Object.getOwnPropertyDescriptor(Module, '__unityIdbfsMount');
        if (existingDesc && 'value' in existingDesc) {
            warnOnce('trap-too-late',
                'IDBFS 마운트가 트랩 설치보다 먼저 완료되어 이번 세션은 PlayerPrefs 영속화를 건너뜁니다 (IndexedDB만 사용).');
            setMode('vanilla');
            return;
        }
        try {
            var slot;
            Object.defineProperty(Module, '__unityIdbfsMount', {
                configurable: true,
                get: function () { return slot; },
                set: function (v) {
                    slot = v;
                    try { onMountAssigned(v); } catch (e) { recordError('마운트 트랩', e); }
                    try {
                        // JS_FileSystem_Sync가 Module.__unityIdbfsMount.mount를 읽으므로 실제 값으로 재정의
                        Object.defineProperty(Module, '__unityIdbfsMount', {
                            configurable: true, enumerable: true, writable: true, value: v
                        });
                    } catch (e) {
                        recordError('트랩 해제', e);
                    }
                }
            });
        } catch (e) {
            recordError('트랩 설치', e);
            setMode('vanilla');
        }
    }

    // ===========================================
    // 강제 flush (백그라운드 전환 대비)
    // ===========================================
    var flushRegistered = false;

    function onFlush() {
        if (state.mode !== 'ait' || !state.captured || !activeMount) return;
        pushScoped(activeMount).catch(function (e) { recordError('flush', e); });
    }

    function registerFlushHandlers() {
        if (flushRegistered) return;
        flushRegistered = true;
        try {
            document.addEventListener('visibilitychange', function () {
                if (document.visibilityState === 'hidden') onFlush();
            });
            window.addEventListener('pagehide', onFlush);
        } catch (e) {
            recordError('flush 핸들러 등록', e);
        }
    }

    // ===========================================
    // 공개 API
    // ===========================================
    function status() {
        return {
            enabled: state.enabled,
            backend: state.backend,
            disabled: state.disabled,
            mode: state.mode,
            restoredBytes: state.restoredBytes,
            mirrorCount: state.mirrorCount,
            legacyImport: state.legacyImport,
            legacyBackend: state.legacyBackend,
            legacyBytes: state.legacyBytes,
            legacyMs: state.legacyMs,
            lastError: state.lastError
        };
    }

    /**
     * config.preRun 배열을 만들되, push를 오버라이드해 trapFn이 항상 배열의
     * "마지막" 원소로 재배치되도록 강제한다.
     *
     * Emscripten의 preRun() 드레인은 Module.preRun을 shift+unshift로 뒤집어 처리한다:
     *   while(Module.preRun.length) __ATPRERUN__.unshift(Module.preRun.shift())
     *   그 다음 callRuntimeCallbacks(__ATPRERUN__)가 __ATPRERUN__을 앞에서부터 shift 실행.
     * 즉 **배열에서 가장 나중에 push된 원소가 가장 먼저 실행**된다(역순).
     * Unity의 IdbFs prejs(unityFileSystemInit)는 loader.js가 config를 Module에 병합한
     * 뒤, framework.js를 비동기로 fetch해서 로드한 다음에야 Module.preRun.push()된다
     * (실측: loader.js `y("frameworkUrl").then(...)` → script 태그 로드 → onload).
     * 우리 config 설정은 항상 이보다 먼저 끝나므로, trapFn을 단순히 배열 앞/뒤
     * 어디에 두든 — 늦게 push되는 프레임워크 콜백이 항상 나중에 push되어 항상 먼저
     * 실행되고, 우리 defineProperty 트랩이 설치되기 전에 __unityIdbfsMount 대입이
     * 끝나버린다(기능 전체 무효화). 이 오버라이드는 이후 어떤 push가 일어나도
     * (loader의 dataUrl 콜백이든 framework의 IdbFs 콜백이든) trapFn을 그때마다
     * 배열 끝으로 재배치해, 실제 실행 순서에서 trapFn이 항상 첫 번째가 되도록
     * 보장한다.
     */
    function buildPreRunArray(existing) {
        var arr = [];
        arr.push = function () {
            var result = Array.prototype.push.apply(this, arguments);
            var idx = this.indexOf(trapFn);
            if (idx !== -1 && idx !== this.length - 1) {
                this.splice(idx, 1);
                Array.prototype.push.call(this, trapFn);
            }
            return result;
        };
        if (Array.isArray(existing)) arr.push.apply(arr, existing);
        else if (typeof existing === 'function') arr.push(existing);
        arr.push(trapFn);
        return arr;
    }

    /**
     * Unity 로더 config에 우리 preRun 트랩을 심는다.
     * index.html이 config 리터럴 생성 직후, createUnityInstance 전에 호출한다.
     */
    function configure(config) {
        readWindowConfig();
        state.configured = true;
        api.bootTimeoutMs = state.bootTimeoutMs;

        if (!state.enabled) {
            // opt-out: config를 건드리지 않는다 (순정 Unity 동작 그대로)
            state.disabled = true;
            setMode('disabled');
            log('PlayerPrefs 영속화가 빌드 설정으로 비활성화되었습니다.');
            return;
        }
        if (sessionKilled) {
            state.disabled = true;
            setMode('vanilla');
            log('이전 실패로 이번 세션에서는 PlayerPrefs 영속화를 건너뜁니다 (IndexedDB만 사용).');
            return;
        }
        if (!config || typeof config !== 'object') {
            recordError('configure', new Error('config 객체가 없습니다'));
            setMode('vanilla');
            return;
        }

        maybeWarnProdOverride();
        maybeWarnLegacyProdOverride();

        try {
            // 파일 close마다 자동 persist → PlayerPrefs.Save() 없이도 미러링 기회 확보
            config.autoSyncPersistentDataPath = true;
            config.preRun = buildPreRunArray(config.preRun);
            registerFlushHandlers();
        } catch (e) {
            recordError('configure', e);
            setMode('vanilla');
        }
    }

    var api = {
        configure: configure,
        status: status,
        preRunRan: false,
        captured: false,
        mode: 'pending',
        bootTimeoutMs: DEFAULT_BOOT_TIMEOUT_MS,
        manifestKey: MANIFEST_KEY,
        persistCount: 0,
        persistIdle: persistIdle,
        debugScopedFiles: debugScopedFiles
    };

    window.__AIT_PP = api;
    window.AITPlayerPrefs = window.AITPlayerPrefs || {};
    window.AITPlayerPrefs.status = status;

    // ===========================================
    // 부트스트랩 — 스크립트 로드 즉시 스냅샷 fetch 착수
    // (index.html의 window.__AIT_PLAYERPREFS 주입보다 먼저 실행될 수 있으므로 기본값으로 시작하고,
    //  configure() 시점에 실제 설정을 반영한다)
    // ===========================================
    readWindowConfig();
    sessionKilled = readSessionKill();
    if (sessionKilled) {
        state.disabled = true;
        snapshotPromise = Promise.resolve({ kind: 'unavailable' });
    } else if (state.enabled) {
        startSnapshotFetch();
    }
})();
