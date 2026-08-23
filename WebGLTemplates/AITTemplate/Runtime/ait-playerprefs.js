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
    // 레거시 origin 덤프 보관용 **별도** 키 (write-once — 한 번 쓰면 덮지 않는다).
    // 매니페스트에 함께 싣지 않는 이유:
    //  (a) 구버전 SDK의 pushScoped는 v:1 매니페스트를 files로부터 처음부터 재구성하므로
    //      매니페스트에 실은 stash는 구버전이 한 번만 push해도 파괴된다.
    //  (b) 진짜 세이브와 stash가 MAX_MANIFEST_CHARS(512KB) 상한을 나눠 쓰게 되어,
    //      둘 다 상한 이내인데도 합계가 넘겨 **정상 세이브 push까지 영구 차단**된다.
    // 별도 키면 두 문제가 모두 소멸한다.
    var STASH_KEY = 'AITUnityFS_v1_legacy';
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
    // normalizeLegacyCandidates가 재는 것은 base64 누적 길이뿐인데, writeLegacyStash는
    // 같은 LEGACY_MAX_B64_CHARS를 JSON 봉투 전체 길이(경로 키 quoting + 파일당 "m"/"t"/"d"
    // 필드 + 콤마·중괄호 + 바깥 "v"/"ts"/"files" 헤더)에 적용한다. 두 곳이 같은 상수를
    // 다른 대상에 적용하면 누적 base64가 상한에 근접한 덤프는 normalize는 통과하고
    // stash에서만 조용히·영구히 탈락한다(재시도군이라 기록도 안 남는 비수렴 실패 모드).
    // normalize 쪽 실효 상한을 프레이밍만큼 미리 낮춰 항상 write 쪽보다 엄격하게
    // 만든다 — 마진 1024자는 LEGACY_MAX_CANDIDATES(8후보) × (경로 ~40자 + 파일당
    // JSON 오버헤드 `"path":{"m":000,"t":0000000000000,"d":""}` ~40자)의 최악치보다
    // 크게 잡은 보수치다.
    var LEGACY_NORMALIZE_MAX_B64_CHARS = LEGACY_MAX_B64_CHARS - 1024;
    // 후보 개수 상한. 심기가 "관측된 앱 디렉터리"까지 지연되면서 후보 맵이 그 관측
    // 시점까지 메모리에 상주하게 됐다 — 개수와 누적 base64 길이(LEGACY_MAX_B64_CHARS)를
    // 함께 묶어 상주량을 push 방향과 같은 크기 예산 안에 가둔다.
    var LEGACY_MAX_CANDIDATES = 8;
    // 후보를 들고 앱 디렉터리 관측을 기다리는 창의 상한(ms). 이 세션에서 PlayerPrefs를
    // 한 번도 열지 않는 게임이면 여기서 포기하고 페이로드를 놓아준다(다음 부팅 재시도).
    // window.__AIT_PLAYERPREFS.legacyWatchMs로 덮어쓸 수 있다(테스트/튜닝용).
    var LEGACY_WATCH_MS = 20000;
    // 감시 대상 파일 이름. SCOPE_RE의 마지막 세그먼트와 반드시 같은 값이어야 한다.
    var PLAYERPREFS_NAME = 'PlayerPrefs';

    // stat.mode 비트 (Emscripten FS와 동일)
    var S_IFMT = 61440;
    var S_IFDIR = 16384;
    var S_IFREG = 32768;

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
        collectFallbackCount: 0,  // collectScopedInner가 노드 그래프 폴백(collectScopedFromNodes)으로
                                  // 성공한 횟수 — getLocalSet/loadEntrySync가 죽는 2021.3 IDBFS 세션
                                  // 노화 결함에서 매니페스트가 동결되지 않았음을 관측하는 용도
        legacyImport: 'none',     // 'none' | 'skip-mountpoint' | 'skip-budget' | 'skip-unknown-local'
                                  // | 'skip-local-present' | 'skip-no-watcher' | 'skip-gate-fired'
                                  // | 'skip-ambiguous' | 'deferred' | 'expired'
                                  // | 'empty' | 'imported' | 'skip-truncated' | 'stashed'
                                  // | 'timeout' | 'error'
        legacyBackend: 'none',    // 'none' | 'override' | 'platform'
        legacyBytes: 0,           // 레거시 origin에서 심은 바이트 (restoredBytes와 의미가 다르므로 분리)
        legacyMs: 0,              // readIdbfs 소요 ms (예산 튜닝 관측용)
        legacyAppDir: null,       // 실제로 심은 앱 디렉터리(/idbfs/<hash>) — 관측값이므로 진단 가치가 크다
        legacyWatchMs: LEGACY_WATCH_MS,
        // 이관 창 부기. null이면 "아직 시도한 적 없음"(= 창 열림, grandfather).
        // { checked: true, result: 'imported'|'stashed', ts: <ms> }
        legacyChecked: null,
        plantedBy: null,          // 심기를 발화시킨 앵커: 'mkdir' | 'lookup' | null
        plantSeenRead: false,     // 심은 파일을 Unity가 실제로 **읽었는가**(파수꾼 관측)
        truncatedAtMs: null,      // 심은 뒤 잘림까지 걸린 ms — 부팅 직후(엔진) vs 늦은 시점(게임) 구분용
        legacyStashState: null,   // 'written' | 'existing' | 'skipped' | null
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
    var legacyStashRan = false;   // 레거시 stash 세션 내 재진입 가드 (위와 같은 이유)
    var plantedAtMs = 0;          // 심은 시각 — truncatedAtMs 계산의 기준점
    // 앱 디렉터리 관측(Unity가 "아직 없는 PlayerPrefs"를 열려는 순간을 잡는 lookup 훅) 상태.
    // 훅은 레거시 후보가 실제로 park될 때(armAppDirWatch)만 설치된다 — 레거시 소스가 없는
    // 대다수 부팅에서는 엔진 객체를 참조 동일성까지 그대로 둔다(installNodeOpsHook 참조).
    var mountRootNode = null;     // FS.mount의 반환값 = 마운트 루트 FSNode('/idbfs')
    var watchInstalled = false;
    var appDirWatch = null;       // { files, timer } — 관측을 기다리는 레거시 후보. null이면 미무장
    var inSelfFs = false;         // 우리 자신의 FS 호출 재진입 가드 (훅이 우리 쓰기에 반응하지 않게)
    // 엔진이 유발한 populate(callOrig(mount, true, ...)) 구간 표시. 늦은 syncfs(true)
    // reconcile의 FS.mkdirTree가 좌초 디렉터리를 복원하며 mkdir-plant 앵커를 오발화시키는
    // 경로를 차단한다. persistPath(callOrig false)는 이 플래그와 무관하다(무회귀 계약 7).
    // ⚠️ 단순 불리언이 아니라 진행 중 카운터다 — Emscripten FS.syncfs는 동시 호출을
    // 직렬화하지 않으므로 populate가 겹치면(예: reload 하니스의 순단 재시도) 안쪽
    // 콜백이 먼저 끝나 바깥 구간이 아직 진행 중인데도 표시를 꺼버릴 수 있다. 증감으로
    // 바꿔 가장 안쪽 콜백이 끝나야 비로소 0이 되게 한다. 판정은 `> 0`(진행 중).
    var inEnginePopulate = 0;
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

    /**
     * 정규 파일 여부. Emscripten IDBFS.storeLocalEntry는 디렉터리도 정규 파일도 아닌
     * mode에 대해 'node type not supported'로 **실패**하므로, 심기 전에 여기서 거른다.
     */
    function isFileMode(mode) {
        return (mode & S_IFMT) === S_IFREG;
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
        // 레거시 후보를 들고 앱 디렉터리 관측을 기다리는 창. 부트 게이트와 무관한
        // (게이트 해제 이후에만 발화하는) 타이머라 bootTimeoutMs와 독립적으로 잡는다.
        var w = Number(cfg.legacyWatchMs);
        if (isFinite(w) && w > 0) state.legacyWatchMs = w;
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
    /**
     * 우리 자신의 FS 접근 구간을 표시한다(lookup 훅 재진입 가드).
     *
     * 훅은 "Unity가 없는 PlayerPrefs를 열려 한다"에만 반응해야 하는데, Emscripten에서
     * **우리 쪽 쓰기도 lookup 미스를 만든다**(FS.writeFile → FS.open → FS.lookupPath →
     * lookupNode, library_fs.js:225-244). 이 표시가 그 둘을 가른다.
     *
     * 1차 방어선은 tryPlantAt의 즉시 disarm이라 오늘의 심기 경로에서는 이 플래그가
     * 없어도 결과가 같다. 그럼에도 유지하는 이유는 **쓰기** 접근면이 심기 하나가
     * 아니기 때문이다 — overlayScoped의 복원 쓰기가 무장 중인 창과 겹치면(다중 탭 등)
     * 스냅샷 복원이 레거시 심기를 촉발할 수 있다.
     *
     * ⚠️ 커버리지를 과장하지 말 것: **읽기 경로는 훅을 건드릴 수 없다.**
     *    IDBFS.getLocalSet은 FS.readdir + FS.stat(library_idbfs.js:94-107)만,
     *    loadLocalEntry는 FS.lookupPath + FS.stat(:151-153)만 쓰는데 대상이 전부 이미
     *    존재하는 경로라 FS.lookupNode가 nameTable에서 끝난다(library_fs.js:225-244).
     *    node_ops.lookup은 미스일 때만 불리므로(:244 → :615) collectScoped/loadEntrySync/
     *    logFirstPersist 구간에서는 훅이 애초에 발화할 수 없다 — 그 셋을 감싸는 것은
     *    "우리 구간을 한 규칙으로 표시한다"는 일관성 때문이지 실제 방어가 아니다.
     *    실제로 미스를 만드는 우리 호출은 storeEntrySync(→ IDBFS.storeLocalEntry →
     *    FS.writeFile → FS.open → lookupPath, library_idbfs.js:174)뿐이다.
     *
     * ⚠️ 끝날 때 무조건 false로 되돌리지 않고 **이전 값을 복원**한다. 이 구간들은
     *    중첩되기 때문이다(collectScoped → loadEntrySync). false로 되돌리면 안쪽
     *    호출이 끝나는 순간 바깥 구간의 표시가 풀려, 그 뒤의 우리 FS 호출이 Unity의
     *    접근으로 오인돼 훅이 발화한다.
     */
    function enterSelfFs() {
        var prev = inSelfFs;
        inSelfFs = true;
        return prev;
    }

    function exitSelfFs(prev) {
        inSelfFs = prev;
    }

    function loadEntrySync(path) {
        var prev = enterSelfFs();
        try {
            var entry = null;
            var ok = false;
            IDBFS.loadLocalEntry(path, function (err, e) {
                if (!err && e) { ok = true; entry = e; }
            });
            return ok ? entry : null;
        } finally {
            exitSelfFs(prev);
        }
    }

    /**
     * /idbfs/<hash>/PlayerPrefs 파일 + 그 조상 디렉터리 엔트리를 동기 수집.
     * 실패 시 null (호출자는 push를 건너뛴다).
     */
    function collectScoped(mount) {
        var prev = enterSelfFs();
        try {
            return collectScopedInner(mount);
        } finally {
            exitSelfFs(prev);
        }
    }

    function collectScopedInner(mount) {
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
            // getLocalSet은 FS.readdir/FS.stat(경로 lookup)에 의존한다. 2021.3의 순정
            // IDBFS 세션 노화 결함(실측: 세션 ~60초 후 errno=44)이 이 lookup 자체를
            // 죽이는데, MEMFS 노드 그래프를 직접 순회하는 아래 폴백은 lookup을 전혀
            // 쓰지 않으므로 이 결함에 면역이다.
            return collectScopedFallback(mount);
        }
        if (!called || failErr || !localSet || !localSet.entries) {
            // 구버전 Emscripten ErrnoError는 message가 없을 수 있어 errno까지 남긴다
            var detail = !called ? '콜백 미호출(비동기 구현?)'
                : failErr ? ('err=' + String(failErr) + (failErr && failErr.errno !== undefined ? ' errno=' + failErr.errno : ''))
                : '결과 집합 없음';
            recordError('getLocalSet', new Error('로컬 파일 목록을 얻지 못했습니다: ' + detail));
            return collectScopedFallback(mount);
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
        var dropped = false;
        for (var k = 0; k < keys.length; k++) {
            var entry = loadEntrySync(keys[k]);
            // getLocalSet 콜백과 이 루프는 전부 동기이므로 그 사이 정당한 삭제는
            // 구조적으로 불가능하다 — null은 "레이스로 사라진 엔트리"가 아니라
            // loadLocalEntry의 lookup 기계 고장이다(2021.3 IDBFS 세션 노화 결함이
            // getLocalSet뿐 아니라 loadEntrySync도 같이 죽인다). 조용히 스킵하고
            // 부분 결과를 push하면 Storage 매니페스트에서 해당 파일이 빠진 채 정본이
            // 갱신되는 원격 데이터 유실이 되므로, 하나라도 드롭되면 부분 결과를 버리고
            // 아래에서 노드 그래프 폴백으로 전환한다.
            if (!entry) { dropped = true; continue; }
            var mode = entry['mode'];
            var rec = { m: mode, t: toMillis(entry['timestamp']) };
            if (!isDirMode(mode)) rec.d = encodeBase64(entry['contents']);
            files[keys[k]] = rec;
        }
        if (dropped) {
            recordError('loadEntrySync', new Error('scoped 엔트리 로드 중 일부가 드롭되었습니다(lookup 고장 의심)'));
            return collectScopedFallback(mount);
        }
        // all(전체 엔트리 경로 목록)은 더 이상 돌려주지 않는다 — 유일한 소비자가 앱
        // 디렉터리를 추측하던 resolveAppDir였고, 그 추측을 코드에서 없앴다.
        return { files: files, scoped: scoped.sort() };
    }

    /**
     * MEMFS 노드 그래프를 mountRootNode부터 직접 순회해 collectScopedInner와 동일한
     * 반환 형태({files, scoped})를 만드는 폴백. FS API·lookupPath·stream_ops를 전혀
     * 호출하지 않는 순수 객체 접근이라, getLocalSet/loadLocalEntry가 의존하는 경로
     * lookup이 죽어도(2021.3 IDBFS 세션 노화 결함) 영향을 받지 않는다.
     *
     * ⚠️ 파수꾼 read/mmap 훅(installTruncationSentinel)을 오발화시키지 않도록 stream_ops를
     *    절대 건드리지 않는다 — node['contents']/node['mode']/node['timestamp']만 읽는다.
     * 실패(mountRootNode 없음, 노드 형태 이상 등)는 전부 삼키고 null.
     */
    function collectScopedFromNodes(mount) {
        var root = mountRootNode;
        if (!root) return null;
        try {
            var base = (activeMount && activeMount.mountpoint) || IDBFS_ROOT;
            base = String(base).replace(/\/+$/, '') || IDBFS_ROOT;

            var files = {};
            var scoped = [];

            function fileBytes(node) {
                var c = node['contents'];
                if (!c) return new Uint8Array(0); // 빈 파일 — contents가 null일 수 있다
                var len = typeof node['usedBytes'] === 'number' ? node['usedBytes'] : c.length;
                if (typeof c.subarray === 'function') return len === c.length ? c : c.subarray(0, len);
                return new Uint8Array(Array.prototype.slice.call(c, 0, len)); // 구버전 Emscripten: plain Array
            }

            function walk(node, path, parent) {
                if (!node) return;
                var mode = node['mode'];
                if (isDirMode(mode)) {
                    var contents = node['contents'] || {};
                    var names = Object.keys(contents);
                    for (var i = 0; i < names.length; i++) {
                        walk(contents[names[i]], path + '/' + names[i], node);
                    }
                    return;
                }
                if (!SCOPE_RE.test(path)) return;
                scoped.push(path);
                files[path] = { m: mode, t: toMillis(node['timestamp']), d: encodeBase64(fileBytes(node)) };
                // 조상 디렉터리(/idbfs/<hash>) — collectScopedInner의 wanted 구성과 대칭
                var dirPath = path.slice(0, path.lastIndexOf('/'));
                if (!files[dirPath] && parent) {
                    files[dirPath] = { m: parent['mode'], t: toMillis(parent['timestamp']) };
                }
            }

            var rootContents = root['contents'] || {};
            var rootNames = Object.keys(rootContents);
            for (var i = 0; i < rootNames.length; i++) {
                walk(rootContents[rootNames[i]], base + '/' + rootNames[i], root);
            }

            return { files: files, scoped: scoped.sort() };
        } catch (e) {
            recordError('nodewalk', e);
            return null;
        }
    }

    /** collectScopedFromNodes를 호출하고 성공 시에만 진단 카운터를 올린다 */
    function collectScopedFallback(mount) {
        var res = collectScopedFromNodes(mount);
        if (res) state.collectFallbackCount++;
        return res;
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
     * 스냅샷의 legacy 필드 → 유효하면 정규화된 창 부기, 아니면 null.
     *
     * - **필드 부재 = 미시도 = 창 열림**(grandfather). 이 레이어가 나가기 전에 쓰인
     *   매니페스트에는 이 필드가 없으므로, 부재를 "이관 완료"로 읽으면 안 된다.
     * - **형태 검증 실패도 부재 취급**한다. 적대적/손상된 매니페스트(legacy:true 같은
     *   값)가 마이그레이션 창을 영구히 닫아버리지 못하게 하는 방어선이다.
     */
    function readLegacyChecked(snapshot) {
        var l = snapshot && snapshot.legacy;
        if (!l || typeof l !== 'object') return null;
        if (l.checked !== true) return null;
        if (typeof l.result !== 'string' || l.result.length === 0) return null;
        return { checked: true, result: l.result, ts: toMillis(l.ts) };
    }

    /**
     * 창 부기를 매니페스트 직렬화 형태로. 미설정이면 '' — 그때는 legacy 필드 자체를
     * 싣지 않는다(무회귀 계약 1: 소스 부재 부팅의 push 페이로드가 오늘과 동일).
     * 키 순서를 고정해야 변경 감지 해시가 안정적이다.
     */
    function serializeLegacyChecked() {
        var l = state.legacyChecked;
        if (!l || l.checked !== true || typeof l.result !== 'string') return '';
        return '{"checked":true,"result":' + JSON.stringify(l.result) + ',"ts":' + (toMillis(l.ts) || 0) + '}';
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
        var prev = enterSelfFs();
        try {
            IDBFS.getLocalSet(mount, function (err, set) {
                if (!err && set && set.entries) all = Object.keys(set.entries).sort();
            });
        } catch (e) {
            recordError('첫 persist 로깅', e);
            return;
        } finally {
            exitSelfFs(prev);
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
        var prev = enterSelfFs();
        try {
            var ok = false;
            IDBFS.storeLocalEntry(path, entry, function (err) { ok = !err; if (err) recordError('storeLocalEntry(' + path + ')', err); });
            return ok;
        } finally {
            exitSelfFs(prev);
        }
    }

    function removeEntrySync(path) {
        var prev = enterSelfFs();
        try {
            IDBFS.removeLocalEntry(path, function (err) { if (err) recordError('removeLocalEntry(' + path + ')', err); });
        } finally {
            exitSelfFs(prev);
        }
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
        // 해시 조합은 pushScoped와 **반드시 동일**해야 한다(files + legacy 부기).
        // 한쪽만 legacy를 빼면 복원 직후 불필요한 push가 나거나(또는 그 반대로)
        // legacy 필드 변경이 "변경 없음"으로 판정돼 영영 영속화되지 않는다.
        var after = collectScoped(mount);
        lastPushedHash = fnv1a(serializeFiles(after ? after.files : files) + '|' + serializeLegacyChecked());
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
     *  - activeStorage(플랫폼 Storage) 객체를 경유하지 않는다. 이 레이어가 **쓰는**
     *    Storage 키는 정확히 2개다 — MANIFEST_KEY(매 push)와 STASH_KEY(write-once,
     *    레거시 보존 1회). 레거시 소스 읽기는 이 둘 중 어느 것도 경유하지 않는다.
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
     * 레거시 IDBFS 덤프(키=절대경로, 값=FILE_DATA 엔트리)에서 scoped PlayerPrefs 후보를
     * 검증해 **원래 경로 키를 유지한 채** 우리 files 형태로 정규화한다.
     *
     * ⚠️ 여기서 심을 위치를 정하지 않는다. 옛 규칙은 이 안에서 "현재 앱 디렉터리"로
     *    리매핑까지 해버렸는데, 그 앱 디렉터리 자체가 로컬 엔트리 목록에서 **추측한**
     *    값이었다(구 resolveAppDir). 리매핑 대상은 엔진이 직접 통보한 경로여야 하므로
     *    이 함수는 검증만 하고 후보를 그대로 돌려준다 — 선택은 호출자 몫이다.
     *
     * 후보 하나가 형태·크기 검증에 실패해도 그 후보만 버리고 나머지는 살린다.
     * 유효 후보가 하나도 없으면 null(호출자가 legacyImport='empty'로 기록).
     *
     * ⚠️ 조상 디렉터리 엔트리는 만들지 않는다. Emscripten storeLocalEntry는 **디렉터리
     *    엔트리에 한해서만** mkdirTree를 부르고 파일 엔트리에는 FS.writeFile을 부르므로
     *    심는 시점에 부모가 이미 있어야 한다. 심을 위치가 "엔진이 방금 만들고 지금 그
     *    안의 PlayerPrefs를 열려는 디렉터리"로 좁혀지면 그 존재는 관측으로 보장된다.
     *    만에 하나 아니더라도 storeLocalEntry가 ENOTDIR을 콜백으로 돌려주고 0바이트로
     *    끝나 매니페스트를 건드리지 않으므로 창은 열린 채 남는다. 스코프를 하위 경로까지
     *    넓히면 이 전제가 깨지므로, 그때는 조상 디렉터리 엔트리를 함께 만들어야 한다
     *    (overlayScoped 참조).
     */
    function normalizeLegacyCandidates(dump) {
        if (!dump || typeof dump !== 'object') return null;

        var keys = Object.keys(dump);
        var paths = [];
        for (var i = 0; i < keys.length; i++) {
            if (SCOPE_RE.test(keys[i])) paths.push(keys[i]);
        }
        paths.sort(); // 채택 순서를 덤프의 키 순서와 무관하게 고정

        // 후보가 이만큼 많은 덤프는 우리가 이해하는 모양이 아니다(플랫폼이 여러 앱/origin의
        // 데이터를 한 덤프에 섞어 준 경우 등). 어느 게임 것인지 가릴 근거가 없는 상태로
        // 관측 시점까지 들고 있을 이유가 없으므로 통째로 포기한다.
        if (paths.length > LEGACY_MAX_CANDIDATES) {
            recordError('레거시 후보 수',
                new Error(paths.length + '개 > 상한 ' + LEGACY_MAX_CANDIDATES + '개'));
            return null;
        }

        var files = {};
        var picked = 0;
        var totalB64 = 0;
        for (var j = 0; j < paths.length; j++) {
            var path = paths[j];
            var v = dump[path];
            if (!v || typeof v !== 'object') continue;
            var mode = Number(v['mode']);
            // 디렉터리를 거르는 것만으로는 부족하다 — **정규 파일임을 요구**한다.
            // storeLocalEntry는 파일도 디렉터리도 아닌 mode를 'node type not supported'로
            // 거부하는데, 그 실패는 심는 시점에야 드러난다. tryPlantAt은 진입 즉시
            // disarm하므로 이 세션의 유일한 관측 기회를 태우고 진단은 'empty'("가져올
            // 내용 없음")라고 잘못 말하게 된다. 타입 비트가 빠진 mode(0o644 등)를 주는
            // 덤프가 여기 걸린다 — 여기서 걸러야 무장 자체를 하지 않고 창이 열린 채 남는다.
            if (!isFinite(mode) || !isFileMode(mode)) continue;

            var d = '';
            var raw = v['contents'];
            if (typeof raw === 'string') {
                // 이미 base64 — 인코딩 없이 길이만으로 상한을 건다(상한은 stash 프레이밍
                // 예약분만큼 낮춘 실효 상한 — 위 LEGACY_NORMALIZE_MAX_B64_CHARS 주석 참조)
                if (raw.length > LEGACY_NORMALIZE_MAX_B64_CHARS) {
                    recordError('레거시 contents 크기',
                        new Error('base64 ' + raw.length + '자 > 상한 ' + LEGACY_NORMALIZE_MAX_B64_CHARS + '자'));
                    continue;
                }
                d = raw;
            } else if (raw !== null && raw !== undefined) {
                // 인코딩(동기·비선점) 이전에 형태와 크기를 먼저 확정한다
                var len = legacyByteLength(raw);
                if (len < 0) {
                    recordError('레거시 contents 형태', new Error('바이트 배열이 아닙니다'));
                    continue;
                }
                if (len > LEGACY_MAX_BYTES) {
                    recordError('레거시 contents 크기',
                        new Error(len + 'B > 상한 ' + LEGACY_MAX_BYTES + 'B'));
                    continue;
                }
                try {
                    d = encodeBase64(raw);
                } catch (e) {
                    recordError('레거시 contents 인코딩', e);
                    continue;
                }
            }

            // 0바이트 파일은 채택하지 않는다(임포트할 내용이 없는 것과 같다). 심어버리면
            // 직후 승격 push에서 col.scoped.length가 1이 되어 "빈 매니페스트를 쓰지 않는다"는
            // 가드를 우회하고, 빈 PlayerPrefs가 정본으로 승격돼 마이그레이션 창이 영구히
            // 닫힌다. 후보가 전부 이렇게 걸러지면 호출자가 legacyImport='empty'로 기록한다.
            if (d.length === 0) continue;

            // 누적 상한 — 여러 후보를 들고 있어도 push 방향의 크기 예산을 넘기지 않는다
            // (실효 상한 LEGACY_NORMALIZE_MAX_B64_CHARS — writeLegacyStash의 봉투
            // 프레이밍을 미리 예약해둔 값)
            if (totalB64 + d.length > LEGACY_NORMALIZE_MAX_B64_CHARS) {
                recordError('레거시 후보 누적 크기',
                    new Error('base64 누적 ' + (totalB64 + d.length) + '자 > 상한 ' + LEGACY_NORMALIZE_MAX_B64_CHARS + '자'));
                break;
            }
            totalB64 += d.length;
            picked++;
            files[path] = { m: mode, t: toMillis(v['timestamp']), d: d };
        }

        return picked > 0 ? files : null;
    }

    /**
     * 레거시 files를 MEMFS에 심고 심은 바이트 수를 돌려준다.
     * ⚠️ overlayScoped와 달리 lastPushedHash를 절대 건드리지 않는다 — 건드리면 직후
     *    승격 push가 "변경 없음"으로 판정돼 통째로 스킵된다(기능이 조용히 무효화).
     * 전제조건상 로컬 scoped 집합이 비어 있으므로 삭제(removeEntrySync)는 하지 않는다.
     *
     * 유일한 호출부는 tryPlantAt이다 — 즉 심기는 "Unity가 방금 이 경로의 PlayerPrefs를
     * 열려고 했다"는 관측 이후에만 일어난다.
     */
    function applyLegacyFiles(files) {
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

    // ===========================================
    // 앱 디렉터리 관측 (lookup 훅)
    // ===========================================
    /**
     * IDBFS 마운트 루트의 node_ops를 클론해 lookup/mknod를 감싼다.
     *
     * ⚠️ 절대 in-place로 고치지 않는다. autoPersist가 꺼진 빌드에서는
     *    mountRoot.node_ops === MEMFS.ops_table.dir.node(전역 공유 싱글턴,
     *    library_memfs.js:20-32)라서 in-place 변경이 /tmp 등 **모든** MEMFS
     *    디렉터리로 번진다. (이 SDK는 configure에서 autoSyncPersistentDataPath를
     *    항상 true로 강제하므로 실배포에선 IdbFs.js:41이 이미 1차 클론을 해둔
     *    상태지만, 그 전제에 의존하지 않는다.)
     *
     * ⚠️ 호출 시점도 계약의 일부다. onMountAssigned가 아니라 armAppDirWatch에서만
     *    부른다 — 레거시 소스가 없는 오늘의 전 배포에서는 엔진 객체를 참조 동일성까지
     *    그대로 둔다(= 훅 없는 다수 경로의 회귀 반경 0).
     *
     * 셀프체크에 실패하면 throw하지 않고 false를 돌려준다. 호출자는 'skip-no-watcher'로
     * 오늘 거동(아무것도 심지 않음)으로 후퇴할 뿐 레이어 전체를 끄지 않는다.
     */
    function installNodeOpsHook(root) {
        if (watchInstalled) return true;
        var base = root && root.node_ops;
        if (!base || typeof base.lookup !== 'function' || typeof base.mknod !== 'function') return false;

        var origLookup = base.lookup;
        var origMknod = base.mknod;
        var ours;
        try {
            ours = Object.assign({}, base);
        } catch (e) {
            recordError('node_ops 클론', e);
            return false;
        }

        // 새로 생기는 **디렉터리** 노드에만 우리 테이블을 전파한다. 파일 노드에는 붙이지
        // 않는다 — IdbFs.js:45가 이미 무조건 전파하고 있어 실동작은 같지만, 향후
        // Emscripten이 file/dir node_ops를 분리하면 우리 쪽이 먼저 깨지지 않게 한다.
        ours.mknod = function (parent, name, mode, dev) {
            var node = origMknod.apply(this, arguments);
            try {
                if (node && isDirMode(mode) && node.node_ops !== ours) node.node_ops = ours;
            } catch (e) { /* 전파 실패는 훅 미발화로 끝날 뿐이므로 삼킨다 */ }
            // ── mkdir-plant 앵커 ───────────────────────────────────────────────
            // lookup 앵커는 "첫 접근이 read-open"을 전제하는데 2021.3에서 그 전제가
            // 거짓임이 실측됐다(tryPlantAt 주석). 앱 디렉터리가 **막 생긴 직후**,
            // 즉 Unity가 그 안의 PlayerPrefs를 아직 열기도 전에 심어두면 그 전제 없이
            // 이관이 성립할 수 있다(모델 i-b: 엔진이 readdir 내용을 보고 rb로 연다).
            // 모델 i-a(디렉터리 유무만 보고 곧장 wb=O_TRUNC)라면 심어도 잘리는데,
            // 그 경우는 수정 C의 파수꾼이 'skip-truncated'로 관측해 창을 열어 둔다.
            // ⚠️ 반환 계약: 심기 성패와 무관하게 반드시 origMknod의 노드를 돌려준다.
            try {
                if (node && isDirMode(mode) && appDirWatch && !inSelfFs && inEnginePopulate === 0 &&
                    parent === mountRootNode && typeof name === 'string' &&
                    SCOPE_DIR_RE.test(IDBFS_ROOT + '/' + name)) {
                    tryPlantOnMkdir(node, name);
                }
            } catch (e) {
                recordError('mkdir-plant 앵커', e);
                if (state.legacyImport === 'deferred') state.legacyImport = 'error';
                disarmAppDirWatch(); // 에러는 disarm (아래 disarm 정책 참조)
            }
            return node;
        };

        // FS.lookupNode(library_fs.js:225-244)는 nameTable을 먼저 뒤지고 **미스일 때만**
        // FS.lookup → node_ops.lookup(:614-616)을 부르며 그 반환값을 그대로 노드로 쓴다.
        // MEMFS.node_ops.lookup(library_memfs.js:183-185)은 무조건 ENOENT throw다.
        // 즉 여기 도달했다는 것은 "이 이름은 지금 존재하지 않는다"는 뜻이고, 존재하는
        // 파일 조회는 여기까지 오지 않으므로 (a) 정상 경로 비용이 0이고 (b) 라이브
        // 데이터를 덮어쓰는 것이 구조적으로 불가능하다.
        ours.lookup = function (parent, name) {
            if (appDirWatch && !inSelfFs && name === PLAYERPREFS_NAME) {
                var planted = null;
                try {
                    planted = tryPlantAt(parent);
                } catch (e) {
                    recordError('앱 디렉터리 훅', e);
                    // 감시만 접고 끝내면 legacyImport가 'deferred'에 영구 고착돼
                    // status()/텔레메트리에서 "아직 대기 중"과 구분되지 않는다.
                    // 확정 진단(imported/skip-ambiguous/...)은 덮지 않는다.
                    if (state.legacyImport === 'deferred') state.legacyImport = 'error';
                    disarmAppDirWatch();
                }
                if (planted) return planted;
            }
            return origLookup.apply(this, arguments); // 실패 시 원본 ENOENT 그대로
        };

        try {
            root.node_ops = ours;
        } catch (e) {
            // 여기서 실패하면 엔진 객체는 손대지 않은 상태 그대로다 — 호출자가
            // 'skip-no-watcher'로 후퇴해도 진단과 실제 상태가 일치한다.
            recordError('node_ops 설치', e);
            return false;
        }
        // ⚠️ 설치 성공은 **여기서** 확정한다. 아래 backfill 실패까지 false로 묶으면
        //    엔진 객체는 이미 우리 클론으로 바뀐 상태인데 호출자는 'skip-no-watcher'
        //    (= 훅 미설치)로 기록해 진단이 실제와 어긋나고, 재호출 시 base가 우리 클론이라
        //    그것을 다시 감싸 이중 래핑이 된다.
        watchInstalled = true;

        // populate로 이미 복원돼 있는 depth-1 디렉터리에도 backfill한다. warm boot에서
        // 앱 디렉터리가 이미 존재하면 mknod를 거치지 않으므로 전파 기회가 없다.
        // 실패는 비치명이고 **디렉터리 단위로 격리**한다 — 한 디렉터리의 실패가 나머지
        // backfill을 막으면 정작 앱 디렉터리에 훅이 못 붙어 관측 기회가 통째로 사라진다.
        var c = root.contents || {};
        var k = Object.keys(c);
        for (var i = 0; i < k.length; i++) {
            try {
                var ch = c[k[i]];
                if (ch && typeof ch.mode === 'number' && isDirMode(ch.mode)) ch.node_ops = ours;
            } catch (e2) {
                recordError('node_ops backfill(' + k[i] + ')', e2);
            }
        }
        return true;
    }

    /**
     * 레거시 후보를 park하고 앱 디렉터리 관측을 기다린다. 성공하면 true.
     *
     * ⚠️ 반드시 부트 게이트의 finish() **이전**에 호출된다. 순서가 뒤집히면 Unity가
     *    callback(null) 직후 MEMFS를 읽으며 여는 첫 PlayerPrefs가 무장보다 앞서고,
     *    그 lookup 미스가 우리 훅 없이 지나가 임포트가 조용히 누락된다(테스트 W9).
     */
    function armAppDirWatch(files) {
        if (!installNodeOpsHook(mountRootNode)) return false;
        disarmAppDirWatch();
        var timer = setTimeout(function () {
            if (!appDirWatch) return;
            appDirWatch = null;
            // 확정 진단(imported/error/...)을 덮어쓰지 않는다 — 아직 park 중일 때만 기록
            if (state.legacyImport === 'deferred') state.legacyImport = 'expired';
            log('앱 디렉터리를 ' + state.legacyWatchMs + 'ms 안에 관측하지 못해 레거시 임포트를 이번 세션에서 포기합니다.');
        }, state.legacyWatchMs);
        appDirWatch = { files: files, timer: timer };
        return true;
    }

    function disarmAppDirWatch() {
        if (!appDirWatch) return;
        if (appDirWatch.timer) clearTimeout(appDirWatch.timer);
        appDirWatch = null;
    }

    /**
     * park한 후보 중 이 앱 디렉터리에 심을 것 하나를 고른다.
     *  ① 경로가 정확히 일치하는 후보(같은 해시 = 같은 origin) 우선
     *  ② 없으면 후보가 정확히 1개일 때만 현재 앱 디렉터리로 리매핑한다
     *  ③ 그 외(정확일치 없음 + 후보 2개 이상)는 어느 origin 것인지 가릴 근거가 없어 포기
     * 반환 키는 항상 **관측된** appDir 아래 경로다 — 좌초 경로에는 절대 쓰지 않는다.
     */
    function pickLegacyTarget(files, appDir) {
        var target = appDir + '/' + PLAYERPREFS_NAME;
        var out = {};
        if (files[target]) { out[target] = files[target]; return out; }
        var keys = Object.keys(files);
        if (keys.length !== 1) return null;
        out[target] = files[keys[0]];
        return out;
    }

    /**
     * 잘림 파수꾼 — 심은 파일 노드 **하나에만** 설치한다.
     *
     * ⚠️ 순서 불변식: 반드시 `applyLegacyFiles`가 성공적으로 반환한 **뒤에** 부른다.
     *    우리 자신의 쓰기(storeLocalEntry → FS.writeFile = O_TRUNC 경유)가 파수꾼을
     *    오발화시키지 않게 하는 유일한 근거가 이 순서다.
     * ⚠️ 공유 테이블(MEMFS.ops_table)을 in-place로 고치지 않는다 — 반드시 클론해서
     *    이 노드에만 지정한다(무회귀 계약 5).
     *
     * 관측하는 것:
     *  - stream_ops.read / mmap → plantSeenRead = true (Unity가 심은 바이트를 실제로 읽었다)
     *  - node_ops.setattr에서 size === 0 → 'skip-truncated' (읽기 전에 잘렸다 = 모델 i-a)
     *
     * 한계(과장하지 말 것):
     *  - unlink/rename 교체는 미탐이다. §1-5 실측 증상은 O_TRUNC라 우선순위가 낮다.
     *  - read 없이 게임이 곧바로 DeleteAll/Save하면 오탐이다. 결과는 "창 유지 + 다음
     *    부팅 stash"라 무손실이고, truncatedAtMs로 부팅 직후(엔진) vs 늦은 시점(게임)을
     *    구분할 수 있다.
     */
    function installTruncationSentinel(node) {
        if (!node) return;
        try {
            var sops = node.stream_ops;
            if (sops && typeof sops === 'object') {
                var cloneS = Object.assign({}, sops);
                var origRead = sops.read;
                if (typeof origRead === 'function') {
                    cloneS.read = function () {
                        // !inSelfFs: 우리 자신의 읽기(collectScoped 등)는 "Unity가 읽었다"의
                        // 증거가 아니다 — setattr 게이트와 같은 면제를 대칭으로 건다.
                        // 엔진 읽기가 selfFs 구간과 겹칠 수는 없다(단일 스레드, selfFs는
                        // 우리 동기 구간 한정)이므로 위음성은 구조적으로 불가능하다.
                        if (!inSelfFs) state.plantSeenRead = true; // 1회 기록 후 원본에 위임
                        return origRead.apply(this, arguments);
                    };
                }
                var origMmap = sops.mmap;
                if (typeof origMmap === 'function') {
                    cloneS.mmap = function () {
                        if (!inSelfFs) state.plantSeenRead = true;
                        return origMmap.apply(this, arguments);
                    };
                }
                node.stream_ops = cloneS;
            }

            var nops = node.node_ops;
            var origSetattr = nops && nops.setattr;
            if (typeof origSetattr === 'function') {
                var cloneN = Object.assign({}, nops);
                cloneN.setattr = function (n, attr) {
                    try {
                        // chmod/utime은 size가 undefined라 여기서 발화하지 않는다.
                        // !inSelfFs: overlayScoped 재기록 등 우리 구간은 면제.
                        if (attr && attr.size === 0 && !state.plantSeenRead && !inSelfFs &&
                            state.legacyImport !== 'skip-truncated') {
                            state.legacyImport = 'skip-truncated';
                            state.truncatedAtMs = Date.now() - plantedAtMs;
                        }
                    } catch (e) { /* 관측 실패가 FS 동작을 막아서는 안 된다 */ }
                    return origSetattr.apply(this, arguments);
                };
                node.node_ops = cloneN;
            }
        } catch (e) {
            recordError('잘림 파수꾼 설치', e);
        }
    }

    /**
     * 후보를 **관측된** 앱 디렉터리에 실제로 심는다.
     * 반환: { result: 'planted'|'skip-ambiguous'|'empty'|'error', bytes, node }
     *
     * ⚠️ disarm은 여기서 하지 않는다 — 앵커(lookup/mkdir)마다 정책이 다르므로 호출자 몫이다.
     */
    function plantLegacyInto(dirNode, appDir, pendingFiles, plantedBy) {
        var files = pickLegacyTarget(pendingFiles, appDir);
        if (!files) return { result: 'skip-ambiguous', bytes: 0, node: null };

        var bytes = 0;
        var prev = enterSelfFs();
        try {
            bytes = applyLegacyFiles(files);
        } catch (e) {
            recordError('레거시 지연 적용', e);
            return { result: 'error', bytes: 0, node: null };
        } finally {
            exitSelfFs(prev);
        }
        if (!bytes) return { result: 'empty', bytes: 0, node: null };

        state.legacyBytes = bytes;
        state.legacyAppDir = appDir;
        state.legacyImport = 'imported';
        state.plantedBy = plantedBy;
        plantedAtMs = Date.now();

        var node = (dirNode.contents && dirNode.contents[PLAYERPREFS_NAME]) || null;
        // 순서 불변식 — 우리 쓰기가 끝난 **뒤에만** 파수꾼을 건다(위 주석 참조)
        installTruncationSentinel(node);
        return { result: 'planted', bytes: bytes, node: node };
    }

    /**
     * mkdir-plant 앵커의 본체. Unity가 앱 디렉터리를 막 만든 직후(=비어 있는 것이
     * 보장된 순간) 미리 심어 둔다.
     *
     * disarm 정책(lookup 앵커와 다르다):
     *  - 실제로 심었거나 에러(쓰기 시도 실패 포함)면 disarm — 1회성 유지.
     *  - 후보 불일치로 **심지 않은** 경우(skip)는 armed 유지. 첫 depth-1 mkdir 하나가
     *    이 세션의 관측 기회를 소진하고 lookup 폴백까지 죽이는 것을 막는다.
     *    이때 legacyImport는 'deferred' 그대로 둔다 — 창이 실제로 열려 있기 때문이다.
     */
    function tryPlantOnMkdir(dirNode, name) {
        var pending = appDirWatch;
        if (!pending || !dirNode) return;
        var appDir = IDBFS_ROOT + '/' + name;
        if (!SCOPE_DIR_RE.test(appDir)) return;
        // 갓 생긴 디렉터리라 비어 있는 것이 정상이지만, 존재하는 파일은 어떤 경로로도
        // 덮지 않는다(무회귀 계약 3). 이 경우도 skip이므로 armed를 유지한다.
        if (dirNode.contents && dirNode.contents[PLAYERPREFS_NAME]) return;

        var r = plantLegacyInto(dirNode, appDir, pending.files, 'mkdir');
        if (r.result === 'skip-ambiguous') return; // armed 유지 — lookup 폴백 기회를 남긴다

        disarmAppDirWatch(); // 심었거나(planted) 쓰기까지 갔다가 실패(empty/error)한 경우
        if (r.result === 'empty') { state.legacyImport = 'empty'; return; }
        if (r.result === 'error') { state.legacyImport = 'error'; return; }

        try {
            log('레거시 스냅샷을 앱 디렉터리(' + appDir + ')에 mkdir 직후 심었습니다 — ' + r.bytes + 'B');
            scheduleImmediatePush(activeMount);
        } catch (e) {
            recordError('레거시 심기 뒤처리', e);
        }
    }

    /**
     * Unity가 "아직 없는 PlayerPrefs"를 처음 열려는 순간. parent가 곧 현재 앱 디렉터리다.
     * 심었으면 그 노드를, 아니면 null(호출자가 원본 lookup으로 위임 = ENOENT)을 돌려준다.
     *
     * mkdir-plant 앵커(위)가 도입된 뒤에도 **폴백으로 유지**한다 — warm boot처럼 앱
     * 디렉터리가 이미 존재해 mknod를 거치지 않는 경로가 남아 있기 때문이다(W8).
     *
     * ⚠️ 이 앵커의 전제는 "그 첫 접근이 read-open이어야 한다"인데, **2021.3에서 거짓임이
     *    실측됐다**(E2E run 32589182104, macOS/Windows 양쪽). lookup 미스는 read/write를
     *    구분하지 못하는데, FS.open은 lookup 성공 후 O_TRUNC면 곧바로 자른다
     *    (library_fs.js:1042-1045 — created 여부와 무관하게 무조건). 즉 첫 미스가
     *    write-open(fopen(path,"wb") = O_WRONLY|O_CREAT|O_TRUNC)에서 나면 우리가 심은
     *    내용은 심자마자 잘려나가고, 그럼에도 legacyImport는 'imported'/legacyBytes>0으로
     *    남아 승격 push가 잘린 내용을 정본으로 올린다 = 창이 닫힌 채 이관은 0바이트.
     *
     *    실측 결과: 2021.3은 두 OS 모두 잘림(값 ""), 2022.3·6000.x는 두 OS 모두 정상(v8).
     *    OS와도, 시드 크기와도 무관한 **Unity 버전 게이팅**이다.
     *
     *    오늘 이 결함은 프로덕션에서 관측되지 않는다 — getPlatformLegacySource()가
     *    null이라 레거시 경로 자체가 비활성이고, 오버라이드 훅을 심는 E2E에서만 발화한다.
     *    대응은 세 겹이다: ① mkdir-plant 앵커(tryPlantOnMkdir)가 심는 시점을 앱 디렉터리
     *    생성 순간으로 앞당기고(엔진의 존재 판정이 readdir 기반[모델 i-b]이면 해소),
     *    ② 잘림 파수꾼(installTruncationSentinel)이 읽기 관측 전 잘림을 'skip-truncated'로
     *    정직화해 legacyChecked를 기록하지 않으며, ③ 다음 부팅의 present+scoped+미체크
     *    경로가 레거시 원본을 stash(STASH_KEY)로 보존한다. 이 lookup 앵커 단독으로 재시도가
     *    성립하지 않는 점은 그대로다: 잘린 파일이 로컬에 남아 다음 부팅에서 lookup 미스가
     *    다시 나지 않는다. 모델 판별(i-a/i-b)은 E2E 관측 라운드가 결정한다
     *    (TODO.md P2 선결 과제 2).
     */
    function tryPlantAt(parent) {
        var pending = appDirWatch;
        if (!pending) return null;
        // 깊이 1의 디렉터리만 앱 디렉터리 후보다(/idbfs/<hash>). 마운트 루트 자신과
        // 그 아래 하위 디렉터리(/idbfs/<hash>/sub)는 대상이 아니다.
        if (!parent || parent === mountRootNode || parent.parent !== mountRootNode) return null;
        if (typeof parent.name !== 'string') return null;
        if (typeof parent.mode === 'number' && !isDirMode(parent.mode)) return null;
        var appDir = IDBFS_ROOT + '/' + parent.name;
        if (!SCOPE_DIR_RE.test(appDir)) return null;
        // 이미 있으면 절대 덮지 않는다. (lookup 미스에서만 불리므로 도달할 수 없는
        // 조건이지만, 이 설계의 안전성 근거를 호출부에도 명시적으로 남긴다.)
        if (parent.contents && parent.contents[PLAYERPREFS_NAME]) return null;

        // lookup 앵커는 기존의 1회성 disarm을 유지한다 — 재진입·DeleteAll 부활 방지
        // 근거가 그대로 유효하기 때문이다(pushScoped의 disarm 주석, W11).
        disarmAppDirWatch(); // 성패 무관 1회성 — 재진입·DeleteAll 부활 방지

        var planted = plantLegacyInto(parent, appDir, pending.files, 'lookup');
        if (planted.result !== 'planted') {
            if (planted.result === 'skip-ambiguous') state.legacyImport = 'skip-ambiguous';
            else if (planted.result === 'empty') state.legacyImport = 'empty';
            else state.legacyImport = 'error';
            return null;
        }
        var bytes = planted.bytes;

        // ⚠️ 심기가 성공한 뒤로는 **어떤 경우에도 노드를 돌려줘야 한다.** 여기서 예외가
        //    새면 호출부(ours.lookup)의 catch가 그것을 삼키고 원본 lookup으로 위임하는데,
        //    그 원본은 ENOENT를 던진다 — 방금 심어 parent.contents와 FS.nameTable에 올라간
        //    파일에 대해 "없음"을 통보하는 셈이다. 그러면 Unity의 FS.open이 이어서 부르는
        //    FS.mknod → FS.mayCreate가 이번에는 nameTable 히트로 EEXIST를 던져
        //    (library_fs.js:618-634) fopen 자체가 실패한다. 즉 예외가 새는 것이 아니라
        //    **삼킨 결과가 FS 실제 상태와 어긋나서** 다음 FS 호출이 죽는 형태다.
        //    그래서 노드를 먼저 확정하고, 뒤처리(로그/승격 push 예약)는 따로 감싼다.
        var node = planted.node;
        try {
            log('레거시 스냅샷을 앱 디렉터리(' + appDir + ')에 심었습니다 — ' + bytes + 'B');
            scheduleImmediatePush(activeMount);
        } catch (e) {
            recordError('레거시 심기 뒤처리', e);
        }
        return node;
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
     * 레거시 소스 읽기를 예산 안에서 **정확히 1회** 수행하는 공유 타임박스.
     * 심기(tryLegacyImport)와 보존(tryLegacyStash) 두 경로가 같은 예산·게이트 규칙을
     * 쓰도록 추출했다 — 한쪽만 게이트를 빠뜨리면 그 경로가 부트를 늦추거나, Unity가
     * MEMFS를 읽어간 뒤에 손을 대게 된다.
     *
     * handlers의 콜백은 통틀어 정확히 1회만 불린다:
     *  onSkipBudget / onTimeout / onGateFired / onError / onDump
     *
     * ⚠️ 타이머를 거는 **이 시점**의 예산으로 다시 계산한다. 호출자 진입 시각 기준으로
     *    걸면 그 사이 동기 작업(collectScoped 등) 소요분만큼 마진이 잠식돼, 레거시
     *    작업 때문에 부트 게이트가 먼저 발화하고 vanilla로 강등될 수 있다.
     */
    function readLegacyWithBudget(src, gateArmedAt, isSettled, handlers) {
        var budget = legacyBudgetMs(gateArmedAt);
        if (budget < LEGACY_MIN_BUDGET_MS) { handlers.onSkipBudget(); return; }

        var done = false;
        var t0 = Date.now();
        var timer = setTimeout(function () {
            if (done) return;
            done = true;
            state.legacyMs = Date.now() - t0;
            handlers.onTimeout();
        }, budget);

        new Promise(function (r) {
            // readIdbfs가 동기 throw해도 여기서 잡힌다
            r(src.readIdbfs());
        }).then(function (dump) {
            if (done) return; // 타임아웃이 이미 이겼다
            done = true;
            clearTimeout(timer);
            state.legacyMs = Date.now() - t0;
            // 게이트가 이미 발화했다면 Unity가 MEMFS를 읽어간 뒤다 — 절대 건드리지 않는다
            if (isSettled()) { handlers.onGateFired(); return; }
            handlers.onDump(dump);
        }, function (e) {
            if (done) return;
            done = true;
            clearTimeout(timer);
            state.legacyMs = Date.now() - t0;
            handlers.onError(e);
        });
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
     *
     * ⚠️ 이 함수는 후보 검증까지만 하고 **직접 심지 않는다**. 심을 앱 디렉터리는 추측이
     *    아니라 관측으로만 정해지므로, 후보를 park(armAppDirWatch)하고 'deferred'로
     *    끝난다 — 실제 심기는 Unity가 그 경로의 PlayerPrefs를 여는 순간 tryPlantAt에서
     *    일어난다. 여기서 매니페스트를 쓰지 않으므로 관측이 없으면 창은 그대로 유지된다.
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

        // collectScoped는 전부 동기(getLocalSet + 파일별 loadLocalEntry + base64 인코딩)라
        // 큰 파일 트리에서 수백 ms를 태울 수 있다. 예산 재계산은 공유 헬퍼가 타이머를
        // 거는 시점에 수행한다(readLegacyWithBudget 주석 참조).
        readLegacyWithBudget(src, gateArmedAt, isSettled, {
            onSkipBudget: function () { state.legacyImport = 'skip-budget'; finishOnce(); },
            onTimeout: function () { state.legacyImport = 'timeout'; finishOnce(); },
            onGateFired: function () { state.legacyImport = 'skip-gate-fired'; finishOnce(); },
            onError: function (e) {
                recordError('레거시 읽기', e);
                state.legacyImport = 'error';
                finishOnce();
            },
            onDump: function (dump) {
                var cand;
                try {
                    cand = normalizeLegacyCandidates(dump);
                } catch (e) {
                    recordError('레거시 임포트 적용', e);
                    state.legacyImport = 'error';
                    finishOnce();
                    return;
                }
                if (!cand) { state.legacyImport = 'empty'; finishOnce(); return; }

                // ── 심기 분기 ────────────────────────────────────────────────
                // 앱 디렉터리(/idbfs/<hash>)를 **추측하지 않는다.** 로컬 엔트리 목록의 유일
                // 후보를 현재 앱 디렉터리로 간주하던 옛 규칙(resolveAppDir)은, 같은 origin에서
                // 서빙 URL만 바뀐 설치(경로 버저닝 등)에서 옛 URL이 남긴 좌초 디렉터리를 그대로
                // 통과시켰다. 그러면 Unity가 절대 읽지 않는 경로에 심고 그것이 매니페스트로
                // 승격되면서 마이그레이션 창이 **영구히** 닫힌다. 심을 위치는 오직 Unity 자신이
                // "이 경로의 PlayerPrefs를 연다/만든다"고 알려준 값 — 관측값이어야 한다.
                //
                // 그래서 여기서는 심지 않고 후보를 park만 한다. 관측(앱 디렉터리 mkdir 또는
                // PlayerPrefs lookup 미스)이 오면 그때 심는다. 관측이 없으면 창은 열린 채 남는다.
                //
                // ⚠️ park(armAppDirWatch)는 반드시 finish() **이전**에 끝나야 한다. Unity는
                //    callback(null) 직후 MEMFS를 읽으므로, 순서가 뒤집히면 첫 PlayerPrefs
                //    open이 감시자보다 앞서 임포트가 조용히 누락된다(테스트 W9).
                //
                // 엔진 계약 셀프체크(node_ops.lookup/mknod)에 실패하면 감시자를 못 붙이므로
                // 아무것도 심지 않는 오늘 거동으로 후퇴한다 — 레이어 전체를 끄지는 않는다.
                if (!armAppDirWatch(cand)) { state.legacyImport = 'skip-no-watcher'; finishOnce(); return; }
                state.legacyImport = 'deferred';
                finishOnce();
            }
        });
    }

    /**
     * 레거시 stash(보존) — 이관 창은 열려 있는데 로컬에 이미 정본 PlayerPrefs가 있어
     * **심을 수 없는** 부팅에서, 옛 origin의 덤프를 별도 write-once 키에 한 번 보관한다.
     *
     * ⚠️ tryLegacyImport를 재사용하지 않는다:
     *    (a) 이 경로의 전제 자체가 "로컬에 scoped 파일이 있다"라서 skip-local-present
     *        관문에 즉사한다.
     *    (b) 성공 종점의 armAppDirWatch가 훅을 설치하고 **심기를 유발**한다 — 라이브
     *        데이터가 있는 부팅에서 절대 해서는 안 되는 일이다.
     *    그래서 stash 경로는 armAppDirWatch/installNodeOpsHook을 **절대 호출하지 않는다**
     *    (무회귀 계약 6). 예산·게이트 규칙만 readLegacyWithBudget으로 공유한다.
     *
     * done은 정확히 1회 호출된다. 실제 Storage 쓰기는 done 이후 fire-and-forget이라
     * 부팅을 블록하지 않는다(실패하면 다음 부팅에 재시도 — 기록을 남기지 않으므로).
     */
    function tryLegacyStash(mount, gateArmedAt, isSettled, done) {
        var finished = false;
        function finishOnce() {
            if (finished) return;
            finished = true;
            done();
        }

        if (legacyStashRan) { finishOnce(); return; }
        var src = resolveLegacySource();
        if (!src) { finishOnce(); return; }
        legacyStashRan = true;

        var mp = String((mount && mount.mountpoint) || '').replace(/\/+$/, '');
        if (mp !== IDBFS_ROOT) { finishOnce(); return; }

        readLegacyWithBudget(src, gateArmedAt, isSettled, {
            // 아래 넷은 전부 **기록 없음** — 재시도군으로 남겨 다음 부팅에 다시 시도한다
            onSkipBudget: finishOnce,
            onTimeout: finishOnce,
            onGateFired: finishOnce,
            onError: function (e) { recordError('레거시 stash 읽기', e); finishOnce(); },
            onDump: function (dump) {
                var cand = null;
                try {
                    cand = normalizeLegacyCandidates(dump); // 형태·크기 상한 재사용
                } catch (e) {
                    recordError('레거시 stash 정규화', e);
                    finishOnce();
                    return;
                }
                // 부팅을 먼저 놓아준다. 뒤이은 Storage 쓰기는 canWrite()를 경유하는데,
                // 그 가드가 mode === 'ait'를 요구하므로 finish 이후여야 성립한다.
                finishOnce();
                if (!cand) return; // 후보 없음 = 'empty' — 아무것도 기록하지 않는다(결정 변경 3)
                writeLegacyStash(cand);
            }
        });
    }

    /**
     * stash 페이로드를 STASH_KEY에 write-once로 쓴다. 성공(또는 기존재) 확인 후에만
     * 창 부기(legacyChecked)를 남긴다 — 쓰지도 못했는데 창을 닫으면 이관이 영구 실패한다.
     */
    function writeLegacyStash(files) {
        if (!canWrite()) { state.legacyStashState = 'skipped'; return; }

        var payload = '{"v":' + SNAPSHOT_VERSION + ',"ts":' + Date.now() + ',"files":' + serializeFiles(files) + '}';
        if (payload.length > LEGACY_MAX_B64_CHARS) {
            // 상한 초과 — 쓰지 않고 **기록도 남기지 않는다**(재시도군)
            recordError('레거시 stash 크기',
                new Error(payload.length + '자 > 상한 ' + LEGACY_MAX_B64_CHARS + '자'));
            state.legacyStashState = 'skipped';
            return;
        }

        var storage = activeStorage;
        new Promise(function (r) {
            r(storage.getItem(STASH_KEY));
        }).then(function (existing) {
            if (existing !== null && existing !== undefined && existing !== '') {
                // write-once: 먼저 보관된 덤프가 정본이다. 덮으면 재부팅마다 최신
                // (=이미 이관된 뒤일 수 있는) 상태로 갈아치울 위험이 있다.
                state.legacyStashState = 'existing';
                markLegacyStashed();
                return null;
            }
            return new Promise(function (r) {
                r(storage.setItem(STASH_KEY, payload));
            }).then(function () {
                state.legacyStashState = 'written';
                log('레거시 덤프를 ' + STASH_KEY + '에 보관했습니다 — ' + payload.length + '자');
                markLegacyStashed();
            });
        }).catch(function (e) {
            // 실패는 기록을 남기지 않는다 = 다음 부팅에 그대로 재시도된다
            recordError('레거시 stash 저장', e);
        });
    }

    /** stash 성공(또는 기존재) 확정 — 창을 닫고 그 사실을 매니페스트에 싣는다 */
    function markLegacyStashed() {
        state.legacyImport = 'stashed';
        state.legacyChecked = { checked: true, result: 'stashed', ts: Date.now() };
        scheduleImmediatePush(activeMount);
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

            // 창 부기 기록 조건 ①: 'imported'는 **plantSeenRead === true** 이후에만.
            // 심기→즉시 push→(다음 프레임)잘림 레이스에서 창이 닫힌 채 0바이트가 정본이
            // 되는 위음성을 차단한다. 읽기가 관측되기 전에는 legacyBytes/legacyAppDir만
            // 진단으로 남기고 창은 열어 둔다(다음 부팅에 stash로 수렴).
            // ('stashed'는 writeLegacyStash가 쓰기 성공 확인 후 직접 기록한다.
            //  'empty'/skip-*/error/timeout/expired/skip-truncated와 소스 부재 부팅은
            //  전부 미기록 = 재시도군이다. 특히 'empty'를 기록하지 않는 것은 의도된
            //  결정이다 — 빈 매니페스트 가드와 충돌하는데다, 미출시 플랫폼 API의
            //  lazy-backfill 가능성 하에서 첫 빈 응답으로 창을 닫는 것은 조기 종결이다.
            //  대가는 "소스가 있는 한 매 부팅 재조회(≤1초 타임박스)"이며, 플랫폼 API
            //  실성능이 확인되면 재결정한다.)
            if (!state.legacyChecked && state.legacyImport === 'imported' && state.plantSeenRead) {
                state.legacyChecked = { checked: true, result: 'imported', ts: Date.now() };
            }

            var filesJson = serializeFiles(col.files);
            var legacyJson = serializeLegacyChecked();
            // ⚠️ 변경 감지 해시에 legacy를 **반드시** 포함한다. files가 그대로이고
            //    legacy 부기만 새로 생긴 push(= stash 직후, 또는 읽기 관측 직후)가
            //    "변경 없음"으로 스킵되면 창이 영영 닫히지 않는다.
            var hash = fnv1a(filesJson + '|' + legacyJson);
            if (hash === lastPushedHash) { resolve(false); return; } // 변경 없음

            var nextSeq = seq + 1;
            var body = '{"v":' + SNAPSHOT_VERSION + ',"seq":' + nextSeq + ',"scope":"' + SCOPE + '","files":' + filesJson +
                (legacyJson ? ',"legacy":' + legacyJson : '') + '}';
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
                // scoped 파일이 매니페스트에 올랐다 = 이 세션의 PlayerPrefs에는 이미
                // 정본이 있다. lookup 미스를 거치지 않고 scoped 파일이 생기는 경로(늦은
                // syncfs(true)가 populate와 겹쳐 파일을 복원하는 경우 등)가 남아 있으면,
                // 그 뒤에 오는 DeleteAll이 만드는 lookup 미스에서 레거시가 되살아나 방금
                // 지운 값을 되돌려 놓을 수 있다. tryPlantAt이 진입 즉시 disarm하므로
                // 대개 no-op이지만 **제거하지 말 것**.
                if (col.scoped.length > 0) disarmAppDirWatch();
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
            // 잘림 파수꾼이 발화했으면 이 승격 push는 하지 않는다 — 잘린 0바이트를
            // 정본으로 올리지 않기 위해서다.
            // ⚠️ 이 게이트의 효과는 **"즉시 승격 push 1회 억제"뿐이다.** Unity의 후속
            //    저장이 유발하는 persistPath→pushScoped는 게이트하지 않는다(게임 쓰기가
            //    정본 — 무회귀 계약 7). 데이터 안전성의 근거는 이 게이트가 아니라
            //    **"skip-truncated면 legacyChecked 미기록 → 다음 부팅에 present+scoped+
            //    미체크 → stash 경로로 보존"** 이라는 사슬이다.
            if (state.legacyImport === 'skip-truncated') return;
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

        /**
         * 이관 창은 열려 있는데(legacy 부기 없음) 로컬에 이미 정본 PlayerPrefs가 있는
         * 부팅. 심는 것은 금지(무회귀 계약 3)이므로, 옛 origin 덤프를 별도 키에 한 번
         * 보관만 하고 종결한다 — 그래야 잘림/미관측으로 이관하지 못한 데이터가 사라지지
         * 않는다.
         *
         * 레거시 소스가 없으면(= 오늘의 전 프로덕션) **동기** 종결이라 관측 가능한
         * 동작 변화가 0이다(무회귀 계약 1).
         */
        function stashThenFinish() {
            var src = getOverrideLegacySource() || getPlatformLegacySource();
            if (!isUsableLegacySource(src)) { finish('ait'); return; }
            tryLegacyStash(mount, gateArmedAt, isSettled, function () { finish('ait'); });
        }

        // ① 기존 IndexedDB → MEMFS 복원 (에러는 현행 Unity 동작대로 삼킨다)
        // 부트 게이트 타이머는 ①이 끝난 뒤(= ② 스냅샷 대기 직전)에만 건다 — ①까지
        // 감싸면 저사양 기기/IDB 경합으로 원본 populate가 늦어질 때 게이트가 먼저
        // 발화해 순정 대비 회귀(정상 데이터를 빈 상태로 취급)가 생긴다.
        // inEnginePopulate: 이 구간의 FS.mkdirTree(좌초 디렉터리 복원 등)가 mkdir-plant
        // 앵커를 오발화시키지 않게 한다. persistPath는 이 플래그와 무관하다(계약 7).
        // 카운터이므로 겹친 populate가 있어도 가장 안쪽 콜백이 끝나야 0으로 내려간다.
        inEnginePopulate++;
        callOrig(mount, true, function (populateErr) {
            inEnginePopulate--;
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
                        // 창 부기를 **overlayScoped보다 먼저** 적재한다 — overlayScoped가
                        // 세우는 lastPushedHash가 files+legacy 조합이라, 순서가 뒤집히면
                        // 첫 persist에서 불필요한 push가 한 번 더 발생한다.
                        //
                        // ⚠️ 세션 중 세운 값을 덮지 않는다(|| 적재). snapshotPromise는
                        // 스크립트 로드 시 1회 메모이제이션되므로, 늦은 syncfs(true)
                        // reconcile로 populatePath가 재진입하면 res.snapshot은 stash/import
                        // 이전의 **원본**(legacy 필드 없음)이다. 무조건 대입하면 이번 부팅에
                        // 세운 legacyChecked가 지워지고, 직후 push가 legacy 없는 매니페스트를
                        // 써서 이미 닫힌 이관 창이 다시 열린다(매 부팅 레거시 재조회+재stash).
                        state.legacyChecked = state.legacyChecked || readLegacyChecked(res.snapshot);
                        // ② AIT 스냅샷이 정본 — scoped 영역만 덮어쓴다
                        overlayScoped(mount, res.snapshot);
                        if (state.legacyChecked) {
                            // 이 origin에서는 이관 시도가 이미 끝났다(imported/stashed) —
                            // 창을 닫는다. 이후 부팅에서 레거시 소스를 다시 훑지 않는다.
                            finish('ait');
                        } else if (!snapshotHasScopedFile(res.snapshot)) {
                            // 매니페스트는 있는데 PlayerPrefs가 하나도 없다 = 마이그레이션
                            // 창이 아직 열려 있는 상태다. 'absent'에만 걸어두면 이전 버전이
                            // 남긴 빈 매니페스트(또는 데이터가 생기기 전 부팅)만으로 창이
                            // 닫혀, 정작 이관이 필요한 사용자에게 seam이 영영 발화하지 않는다.
                            importThenPromote();
                        } else {
                            // 정본이 이미 있는데 창은 열려 있다 = 심을 수는 없고 보존만
                            // 가능한 상태(잘림으로 이관에 실패한 다음 부팅이 여기로 온다).
                            stashThenFinish();
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
        // FS.mount의 반환값 = 마운트 루트 FSNode('/idbfs'). 여기서는 **캡처만** 한다 —
        // node_ops 훅 설치는 레거시 후보가 실제로 park될 때(armAppDirWatch)로 미룬다.
        // REQUIRED_IDBFS_FNS에 lookup/mknod를 넣지 않는 이유도 같다: 이 셀프체크의
        // 실패는 레이어 전체를 vanilla로 떨구지만, 훅은 없어도 본 기능이 멀쩡하다.
        mountRootNode = mountRoot;
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
            collectFallbackCount: state.collectFallbackCount,
            legacyImport: state.legacyImport,
            legacyBackend: state.legacyBackend,
            legacyBytes: state.legacyBytes,
            legacyMs: state.legacyMs,
            legacyAppDir: state.legacyAppDir,
            // 이관 창 부기와 판별 계측 — E2E/실기기 콘솔이 모델(i-a/i-b)을 가리는 근거다
            legacyChecked: state.legacyChecked,
            plantedBy: state.plantedBy,
            plantSeenRead: state.plantSeenRead,
            truncatedAtMs: state.truncatedAtMs,
            legacyStashState: state.legacyStashState,
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
