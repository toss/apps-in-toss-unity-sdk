using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// 페이지-컨텍스트 CacheStorage 인터셉터 스니펫 방출기 (ServiceWorker 불필요).
    ///
    /// index.html 은 SDK 소유이므로 페이지에서 직접 window.fetch 를 패치할 수 있습니다.
    /// 재방문(warm) 시 Unity Build/* 자산(wasm/data/framework)을 CacheStorage 에서 직접 서빙하면
    /// 네트워크 요청 자체가 발생하지 않아(transferSize 0) ServiceWorker 없이도 재방문 TTFF 를 단축합니다.
    ///
    /// 게이팅: config.pageCache tri-state (-1=자동/true, 0=비활성, 1=활성).
    /// 비활성 판정 시 string.Empty 를 반환합니다.
    /// → %AIT_PAGE_CACHE_SCRIPT% 가 빈 문자열로 치환되어 산출물이 byte-identical no-op 이 됩니다.
    /// (AITServiceWorkerEmitter 류의 enableXxx → emitter → %AIT_..._SCRIPT% 치환 컨벤션과 동일.)
    ///
    /// 캐시명 자동 파생: config.pageCacheName 이 비어 있으면 appName 에서 "ait-page-cache-{slug}" 를 파생합니다.
    /// appName 도 비어 있으면 기본값 "ait-page-cache" 를 사용하고 빌드 경고를 출력합니다.
    /// 이 자동 파생은 멀티앱 오리진 공유 시 sweep 상호 간섭을 방지합니다.
    ///
    /// === 백엔드: CacheStorage(주) → IndexedDB(폴백) ===
    ///  · 설치 가드는 isSecureContext 만 하드 요구합니다. CacheStorage 가 없으면(예: iOS WKWebView 에서
    ///    App-Bound Domains 미설정 등으로 caches 가 노출되지 않는 환경) IndexedDB 로 자동 강등합니다.
    ///    둘 다 없으면 기존과 동일하게 전체 no-op 입니다.
    ///  · 호출부(cacheFirst/부팅 sweep/dump)는 getCache() 가 resolve 하는 cache-like 어댑터
    ///    (match/put/keys/delete)에만 의존하므로 백엔드가 무엇이든 동일 코드로 동작합니다.
    ///  · 중요한 한계: 호스트(슈퍼앱)의 백그라운드 warm pre-fill 페이지(ait-warm.html)는 CacheStorage 전용이라
    ///    IndexedDB 폴백 환경에는 pre-fill 이 들어오지 않습니다. 즉 IDB 백엔드가 얻는 이득은 인터셉터 자체가
    ///    콜드 방문에 put 한 자산을 재방문에 서빙하는 self-populated 경로에 한정됩니다(호스트 pre-fill 가속은
    ///    아님). 설계상 수용된 한계입니다.
    ///
    /// === 호스트(슈퍼앱) 연동 계약 (코드 주석으로 명문화) ===
    ///  · 캐시명 규약: 호스트의 백그라운드 pre-fill 페이지와 '동일한 캐시명' 을 써야 같은 버킷을 공유합니다.
    ///    캐시명은 config.pageCacheName(또는 자동 파생값) 이며, 런타임 window.__AIT_CACHE_NAME 으로도 오버라이드됩니다.
    ///    (IndexedDB 백엔드에서는 이 값이 DB 이름으로도 그대로 쓰여 앱별 버킷 격리를 유지합니다.)
    ///  · 캐시 키 규약: 절대 URL 문자열(new URL(resource, location.href).href).
    ///    nameFilesAsHashes=true(기본) 전제 → 파일명이 콘텐츠 해시이므로 키=불변 콘텐츠(스테일 없음).
    ///  · decode-free pre-fill 규약: 캐시에 Content-Encoding 없는 raw(해제된) bytes 가 들어 있으면
    ///    loader.js 가 Unity 마커를 못 찾아 Worker brotli 해제를 자연 스킵합니다.
    ///    이 raw-without-CE 저장은 호스트 pre-fill(또는 서버 CE:br 자동해제 응답)의 책임이며,
    ///    페이지 스니펫은 저장된 bytes 를 신뢰해 가공 없이 그대로 반환만 합니다(CacheStorage 경로).
    ///    IndexedDB 백엔드는 유일 populator 이므로 저장 시점에 스스로 decode-free 정규화를 수행합니다
    ///    (warm page 의 storeDecodeFree() 를 미러링).
    ///
    /// 부팅 무효화(allowlist): 빌드 시 확정된 현재 빌드 Build/* 파일(data/framework/wasm) 의 절대 URL 집합을
    /// 스니펫에 JSON 으로 박아, 설치 직후 1회 비차단으로 allowlist 에 없는 옛 해시 엔트리를 정리합니다.
    /// (loader.js 는 &lt;script src&gt; 라 fetch API 를 안 거치므로 캐시 대상이 아니며 allowlist 에서도 제외.)
    /// 이 콘텐츠 버전 정합(sweep)은 백엔드와 무관하게 동일 코드로 동작하며, IndexedDB 자체의 스키마 버전
    /// (IDB_VERSION)은 이와 독립적인 축입니다(스키마 변경 시에만 bump).
    ///
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITPageCacheEmitter
    {
        internal const string DefaultCacheName = "ait-page-cache";
        internal const string CacheNamePrefix = "ait-page-cache-";

        /// <summary>
        /// appName(앱 식별자)에서 캐시 버킷 이름을 파생합니다.
        /// 영문 소문자/숫자/하이픈으로 정규화하며, 비ASCII 문자는 짧은 해시로 대체합니다.
        /// identifier 가 비어 있으면 null 을 반환합니다(호출자가 폴백 처리).
        /// </summary>
        internal static string DeriveCacheName(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return null;

            // 비ASCII 포함 여부 확인
            bool hasNonAscii = false;
            foreach (char c in identifier)
            {
                if (c > 127) { hasNonAscii = true; break; }
            }

            string slug;
            if (hasNonAscii)
            {
                // 비ASCII 문자가 있으면 UTF-8 바이트의 FNV-1a 32비트 해시(16진수 8자리)로 대체
                slug = ComputeFnv1a32Hex(identifier);
            }
            else
            {
                // ASCII만 있으면 소문자 변환 후 영숫자·하이픈 이외 문자를 하이픈으로 치환
                slug = identifier.ToLowerInvariant();
                slug = Regex.Replace(slug, @"[^a-z0-9\-]", "-");
                // 연속 하이픈·앞뒤 하이픈 정리
                slug = Regex.Replace(slug, @"-{2,}", "-").Trim('-');
            }

            if (string.IsNullOrEmpty(slug))
                return null;

            return CacheNamePrefix + slug;
        }

        /// <summary>
        /// 문자열의 UTF-8 바이트에 대한 FNV-1a 32비트 해시를 16진수 문자열(8자리)로 반환합니다.
        /// 순수 정적 함수: 외부 의존성 없음, 테스트 용이.
        /// </summary>
        internal static string ComputeFnv1a32Hex(string value)
        {
            const uint FnvOffsetBasis = 2166136261u;
            const uint FnvPrime = 16777619u;

            uint hash = FnvOffsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return hash.ToString("x8");
        }

        /// <summary>
        /// pageCache tri-state 값과 appName 을 고려하여 실제 캐시명을 결정합니다.
        /// pageCacheName 이 명시적으로 설정되어 있으면 그대로 사용하고,
        /// 비어 있으면 appName 에서 자동 파생합니다.
        /// </summary>
        internal static string ResolveCacheName(AITEditorScriptObject config)
        {
            // 명시적 캐시명이 있으면 그대로 사용
            if (!string.IsNullOrEmpty(config.pageCacheName))
                return config.pageCacheName;

            // appName 에서 자동 파생
            string derived = DeriveCacheName(config.appName);
            if (derived != null)
                return derived;

            // 식별자도 없으면 기본값 폴백 + 경고
            Debug.LogWarning(
                "[AIT] pageCacheName 과 appName 이 모두 비어 있어 기본 캐시 이름 '" + DefaultCacheName + "' 을 사용합니다. " +
                "멀티앱 오리진 공유 환경에서는 앱별 고유 이름을 설정하세요."
            );
            return DefaultCacheName;
        }

        /// <summary>
        /// index.html 의 %AIT_PAGE_CACHE_SCRIPT% 자리에 인라인 삽입할 &lt;script&gt; IIFE 를 생성합니다.
        /// config 가 null 이거나 pageCache 가 비활성(0) 이면 string.Empty(no-op)를 반환합니다.
        /// pageCache == -1(자동) 이면 AITDefaultSettings.GetDefaultPageCache() == true 이므로 활성으로 동작합니다.
        /// loaderFile 은 캐시 대상이 아니므로 인자로 받지 않습니다.
        ///
        /// 산출 스니펫은 CacheStorage 를 우선 사용하고, 미지원 환경에서는 IndexedDB 로 자동 강등합니다
        /// (클래스 doc-comment "백엔드: CacheStorage(주) → IndexedDB(폴백)" 참고).
        /// </summary>
        internal static string GenerateInterceptorScript(AITEditorScriptObject config, string dataFile, string frameworkFile, string wasmFile)
        {
            // 게이팅: tri-state 해석 (-1=자동→기본값true, 0=비활성, 1=활성).
            bool enabled = config != null && (
                config.pageCache < 0
                    ? AITDefaultSettings.GetDefaultPageCache()
                    : config.pageCache == 1
            );
            if (!enabled)
            {
                return string.Empty;
            }

            // 캐시명: 명시적 설정 > appName 파생 > 기본값 폴백.
            string cacheName = ResolveCacheName(config);

            // 부팅 무효화용 allowlist: 현재 빌드의 Build/* 상대 경로(data/framework/wasm). loader 는 제외.
            var allowlist = new List<string>();
            if (!string.IsNullOrEmpty(dataFile)) allowlist.Add("Build/" + dataFile);
            if (!string.IsNullOrEmpty(frameworkFile)) allowlist.Add("Build/" + frameworkFile);
            if (!string.IsNullOrEmpty(wasmFile)) allowlist.Add("Build/" + wasmFile);

            // JSON 배열 리터럴 (JS 측에서 new URL(...).href 로 절대화하여 allowlist 비교).
            string allowlistJson = "[" + string.Join(",", allowlist.ConvertAll(JsString)) + "]";
            string cacheNameJs = JsString(cacheName);

            // 네이티브 에셋 소스 레버 (tri-state -1=자동→기본값true, 0=비활성, 1=활성).
            // pageCache 가 ON 일 때만(인터셉터 존재 전제) 이 분기에 도달하므로 AND 게이트가 성립.
            bool nativeEnabled = config.nativeAssetSource < 0
                ? AITDefaultSettings.GetDefaultNativeAssetSource()
                : config.nativeAssetSource == 1;
            string nativeEnabledJs = nativeEnabled ? "true" : "false";

            // 주의: 이 스니펫에는 %대문자_퍼센트% 토큰을 포함하지 않습니다(ValidatePlaceholderSubstitution 안전).
            // 설치 순서: index.html 에서 본 스니펫은 %AIT_EARLY_FETCH_SCRIPT% 보다 '앞'에 위치합니다.
            // 따라서 priorFetch 캡처 시점의 window.fetch 는 native 이며, 그 위를 이후 Early Fetch 가 래핑합니다.
            // 부트 fetch 흐름: ait-cache 래퍼 → (활성 시 Early Fetch 래퍼) → network.
            // 캐시 히트는 Early Fetch 소진과 무관하게 단락(short-circuit)합니다.
            //
            // 조립 순서(전체 IIFE 는 하나의 스코프를 공유): ScriptOpenJs(가드/백엔드선택) → 보간 3개(CACHE_NAME/
            // ALLOWLIST/NATIVE_SOURCE) → BakedTailJs(신호/통계/판정 헬퍼) → IdbBackendJs(IndexedDB 폴백 백엔드) →
            // CacheCoreJs(getCache/priorFetch/cacheFirst) → FetchOverrideJs(window.fetch 오버라이드) →
            // SweepDumpCloseJs(부팅 sweep/dump/종료). cacheFirst/sweep/dump 본문은 원본 그대로 이동만 되었습니다.
            return ScriptOpenJs
                 + "\n            var CACHE_NAME = (window.__AIT_CACHE_NAME) || " + cacheNameJs + ";"
                 + "\n            var ALLOWLIST = " + allowlistJson + ";"
                 + "\n            var NATIVE_SOURCE = " + nativeEnabledJs + ";"
                 + BakedTailJs
                 + IdbBackendJs
                 + CacheCoreJs
                 + FetchOverrideJs
                 + SweepDumpCloseJs;
        }

        /// <summary>
        /// 문자열을 안전한 JS 문자열 리터럴로 인코딩합니다(따옴표/역슬래시 이스케이프).
        /// </summary>
        private static string JsString(string value)
        {
            if (value == null) return "\"\"";
            string escaped = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }

        // ================================================================
        // JS 조각 (const, 보간 없음). GenerateInterceptorScript 가 문자열 연결로 최종 스니펫을 조립합니다.
        // 각 조각은 이름 있는 관심사 블록으로 분리되어 diff/리뷰가 쉽고, %[A-Z0-9_]+% 토큰 스캔이 안전합니다.
        // ================================================================

        /// <summary>
        /// &lt;script&gt; 오프닝 + IIFE/try 오프닝 + 설치 가드(보안 컨텍스트) + 백엔드 선택
        /// (CacheStorage 우선, IndexedDB 폴백, 둘 다 없으면 no-op). 캐시명/allowlist/네이티브 소스 보간
        /// 직전까지의 순수 JS 를 담습니다.
        /// </summary>
        private const string ScriptOpenJs = @"<script>
    // AIT Page Cache: 재방문 시 Build/* 자산을 CacheStorage 또는 IndexedDB(폴백)에서 직접 서빙(ServiceWorker 불필요).
    // 미지원/비보안 환경에서는 window.fetch 를 건드리지 않고 원래 로드로 무해 통과합니다.
    (function () {
        try {
            // 설치 가드(최우선): 비보안 컨텍스트면 즉시 return → 원래 fetch 그대로.
            // secure context 는 유지(https 전제 — warm/native 연동 계약과 일치). CacheStorage 하드 요구는 제거하고
            // 백엔드를 선택한다: CacheStorage(주) → IndexedDB(폴백, iOS WKWebView App-Bound Domains 미설정 등).
            // 둘 다 없으면(구형 브라우저 등) 기존과 동일하게 전체 no-op.
            if (!window.isSecureContext) { return; }
            var hasCaches = ('caches' in window);
            var hasIdb = ('indexedDB' in window) && !!window.indexedDB;
            var BACKEND_KIND = hasCaches ? 'caches' : (hasIdb ? 'idb' : null);
            if (!BACKEND_KIND) { return; }

            // 캐시명(CACHE_NAME): 런타임 오버라이드(window.__AIT_CACHE_NAME) 우선, 없으면 빌드 시 박힌 값.
            // 호스트 pre-fill 페이지와 '동일 캐시명' 이어야 같은 버킷을 공유합니다(연동 계약).
            // IDB 백엔드에서는 이 값이 그대로 IndexedDB DB 이름으로도 쓰여 앱별 버킷 격리를 유지합니다.
            // ALLOWLIST: 현재 빌드의 Build/* 상대경로 — 설치 직후 1회 부팅 sweep 정리에 사용.
            // NATIVE_SOURCE: 호스트가 window.__aitResolveAsset(url) 리졸버를 주입하면 native→백엔드→network
            //   순으로 해석합니다(리졸버 미주입 시 신호만 노출). 보안/백엔드 가드(위 return) 이후에 정의되므로
            //   미지원 환경에선 신호가 미정의로 남습니다(의도).";

        /// <summary>
        /// NATIVE_TIMEOUT_MS 상수 + 네이티브 신호 노출 + 통계 훅 + 판정 헬퍼(isCacheable/absUrl/isNonGet).
        /// 원본 로직과 동일(문자 그대로 이동).
        /// </summary>
        private const string BakedTailJs = @"
            var NATIVE_TIMEOUT_MS = 3000;
            // 호스트가 리졸버 주입 가치를 판단할 수 있도록 신호를 노출(레버 OFF 면 false → 호스트가 주입 생략).
            window.__aitNativeSourceEnabled = NATIVE_SOURCE;

            // 통계 훅(perf CI/검증용, 운영 무영향).
            window.__aitCacheStats = { hits: [], misses: [], puts: [], errors: [] };

            // ALLOWLIST 절대 URL 집합(부팅 sweep 과 isCacheable 이 공유). loader.js 등 ALLOWLIST 에
            // 없는 Build/* 경로는(예: early-fetch 가 HTTP 캐시 워밍 목적으로 bare fetch 하는 loader.js)
            // 캐시 대상이 아니므로 이 집합으로 걸러낸다(캐시 버킷에 put 되어 원 목적을 해치지 않도록).
            var ALLOW_ABS = {};
            for (var _ai = 0; _ai < ALLOWLIST.length; _ai++) {
                try { ALLOW_ABS[new URL(ALLOWLIST[_ai], location.href).href] = true; } catch (e) {}
            }

            // 인터셉트 조건: 동일 오리진 && /Build/ 경로 && ALLOWLIST 멤버(현재 빌드의 data/framework/wasm).
            // /Build/ 접두사만으로는 loader.js 같은 '캐시 대상 아님' 경로까지 오판하므로 ALLOWLIST 로 좁힌다.
            function isCacheable(url) {
                try {
                    var u = new URL(url, location.href);
                    if (u.origin !== location.origin) { return false; }
                    if (u.pathname.indexOf('/Build/') < 0) { return false; }
                    return !!ALLOW_ABS[u.href];
                } catch (e) { return false; }
            }
            // 캐시 키: 절대 URL 문자열(콘텐츠 해시 파일명 전제이므로 키=불변 콘텐츠).
            function absUrl(resource) {
                if (typeof resource === 'string') { return new URL(resource, location.href).href; }
                if (resource && resource.url) { return resource.url; }
                return null;
            }
            // 비-GET 판정: init.method 뿐 아니라 Request 객체에 실린 method 도 확인.
            // fetch(new Request(url, {method:'POST'})) 처럼 init 없이 Request 에 method 가 실린 경우를
            // GET 으로 오인해 GET 캐시 엔트리로 응답하는 일을 막는다(견고성). init.method 가 Request.method 를 덮어쓴다.
            function isNonGet(resource, init) {
                var method = (init && init.method) || (resource && typeof resource !== 'string' && resource.method) || 'GET';
                return method.toUpperCase() !== 'GET';
            }";

        /// <summary>
        /// IndexedDB 폴백 백엔드(신규). caches 경로와 동일한 cache-like 인터페이스(match/put/keys/delete)를
        /// 제공하여 getCache()(CacheCoreJs) 뒤에서 캡슐화됩니다. 모든 연산은 실패 시 조용히 강등합니다
        /// (match/keys → 빈 결과, delete → resolve, put → reject 후 상위 cacheFirst 의 catch 가 흡수).
        /// caches 백엔드(cacheFirst 의 c.put)는 Content-Encoding 을 스트립하지 않고 네이티브 Cache 에 그대로
        /// 저장하는 반면(오늘 동작 유지), IDB 백엔드는 유일한 populator 이므로 decode-free 로 저장합니다
        /// (idbPut 참고 — warm page storeDecodeFree() 미러링).
        /// </summary>
        private const string IdbBackendJs = @"

            // ===== IndexedDB 폴백 백엔드 (CacheStorage 미지원 환경: iOS WKWebView App-Bound Domains 미설정 등) =====
            // 계약: caches 경로와 동일한 cache-like 인터페이스(match/put/keys/delete)를 제공한다.
            //       모든 연산은 실패 시 조용히 강등(match/keys→빈결과, delete→resolve, put→reject 후 상위 catch 흡수).
            var IDB_STORE = 'assets';
            var IDB_VERSION = 1;   // 스키마 버전(구조 변경 시 bump → onupgradeneeded 에서 store 재생성)
            // indexedDB.open() 은 onsuccess/onerror/onblocked 중 아무 것도 발동하지 않고 pending 으로
            // 남을 수 있다(구형 WebKit/사파리 프라이빗 브라우징 등 역사적 보고 사례, 최초 방문 디스크
            // 초기화 지연 등). 네이티브 리졸버 분기(NATIVE_TIMEOUT_MS)와 동일하게 상한을 둔다 — 그래야
            // getCache() 를 기다리는 cacheFirst() 가 영구 대기하지 않고 네트워크로 폴백한다(리뷰 후속 수정).
            var IDB_OPEN_TIMEOUT_MS = 3000;

            function openIdbBackend(dbName) {
                return new Promise(function (resolve, reject) {
                    var req;
                    try { req = indexedDB.open(dbName, IDB_VERSION); }
                    catch (e) { reject(e); return; }
                    req.onupgradeneeded = function () {
                        var db = req.result;
                        // 버전 불일치 시 기존 store 폐기 후 재생성(스키마 정합).
                        if (db.objectStoreNames.contains(IDB_STORE)) { db.deleteObjectStore(IDB_STORE); }
                        db.createObjectStore(IDB_STORE, { keyPath: 'url' });
                    };
                    req.onsuccess = function () { resolve(makeIdbAdapter(req.result)); };
                    req.onerror   = function () { reject(req.error || new Error('idb open error')); };
                    req.onblocked = function () { reject(new Error('idb blocked')); };
                });
            }

            function makeIdbAdapter(db) {
                // match: 레코드 → 합성 Response 재구성. 미스/에러는 null(→ cacheFirst 가 네트워크 폴백).
                function idbMatch(url) {
                    return new Promise(function (resolve) {
                        var getReq;
                        try {
                            getReq = db.transaction(IDB_STORE, 'readonly').objectStore(IDB_STORE).get(url);
                        } catch (e) { resolve(null); return; }
                        getReq.onsuccess = function () {
                            var rec = getReq.result;
                            if (!rec) { resolve(null); return; }
                            try {
                                resolve(new Response(rec.body, {
                                    status:     rec.status || 200,
                                    statusText: rec.statusText || '',
                                    headers:    rec.headers || {}
                                }));
                            } catch (e) { resolve(null); }
                        };
                        getReq.onerror = function () { resolve(null); };
                    });
                }
                // put: decode-free 저장(warm page storeDecodeFree() 미러링). content-encoding 이 있으면 브라우저가
                //      이미 해제한 body 를 CE 헤더 없이 저장하고, 없으면 그대로 저장(loader.js 정합 보장).
                //      합성 Response(ArrayBuffer, {headers:{content-encoding}}) 는 CE 헤더가 inert 라 loader 오판
                //      위험이 있어, iOS-IDB 환경의 유일 populator 인 이 경로에서만 스트립한다(caches 경로는 비대칭 유지).
                //      CE 가 있을 때는 storeDecodeFree() 와 동일하게 content-type 만 남기고 나머지 헤더(특히
                //      압축 상태 기준의 원본 content-length)는 버린다 — 해제된 buf.byteLength 와 불일치하는
                //      content-length 를 그대로 들고 가면 이를 신뢰하는 소비자(다운로드 진행률 계산 등)가
                //      재구성된 Response 를 보고 오동작할 수 있다(리뷰 후속 수정).
                function idbPut(url, resp) {
                    var ce;
                    try { ce = resp.headers.get('content-encoding'); } catch (e) { ce = null; }
                    return resp.arrayBuffer().then(function (buf) {
                        var headers;
                        if (ce) {
                            var ct = 'application/octet-stream';
                            try { ct = resp.headers.get('content-type') || ct; } catch (e) {}
                            headers = { 'content-type': ct };
                        } else {
                            headers = {};
                            try {
                                resp.headers.forEach(function (v, k) { headers[k] = v; });
                            } catch (e) {}
                            if (!headers['content-type']) { headers['content-type'] = 'application/octet-stream'; }
                        }
                        var rec = {
                            url: url,
                            body: buf,
                            headers: headers,
                            status: (resp.status >= 200 && resp.status < 300) ? resp.status : 200,
                            statusText: resp.statusText || '',
                            storedAt: Date.now()
                        };
                        return new Promise(function (resolve, reject) {
                            var tx;
                            try {
                                tx = db.transaction(IDB_STORE, 'readwrite');
                                tx.objectStore(IDB_STORE).put(rec);
                            } catch (e) { reject(e); return; }
                            tx.oncomplete = function () { resolve(); };
                            // QuotaExceededError 는 tx.onabort 로 도달 → reject → 상위(cacheFirst) catch 흡수.
                            tx.onabort = function () { reject(tx.error || new Error('idb put abort')); };
                            tx.onerror = function () { reject(tx.error || new Error('idb put error')); };
                        });
                    });
                }
                // keys: 값 없이 키만 순회(openKeyCursor) → 네이티브 keys() 와 동일한 [{url}] shape.
                function idbKeys() {
                    return new Promise(function (resolve) {
                        var out = [];
                        var curReq;
                        try {
                            curReq = db.transaction(IDB_STORE, 'readonly').objectStore(IDB_STORE).openKeyCursor();
                        } catch (e) { resolve(out); return; }
                        curReq.onsuccess = function () {
                            var cur = curReq.result;
                            if (cur) { out.push({ url: cur.key }); cur.continue(); }
                            else { resolve(out); }
                        };
                        curReq.onerror = function () { resolve(out); };
                    });
                }
                // delete: {url} 또는 문자열 수용(sweep 은 keys() 원소를 그대로 넘김). 에러도 resolve(sweep 이 .catch 로 흡수).
                function idbDelete(keyEntry) {
                    var key = (keyEntry && typeof keyEntry === 'object') ? keyEntry.url : keyEntry;
                    return new Promise(function (resolve) {
                        var tx;
                        try {
                            tx = db.transaction(IDB_STORE, 'readwrite');
                            tx.objectStore(IDB_STORE).delete(key);
                        } catch (e) { resolve(); return; }
                        tx.oncomplete = function () { resolve(); };
                        tx.onabort    = function () { resolve(); };
                        tx.onerror    = function () { resolve(); };
                    });
                }
                return { match: idbMatch, put: idbPut, keys: idbKeys, delete: idbDelete };
            }";

        /// <summary>
        /// getCache()(백엔드 선택 캡슐화) + priorFetch 캡처 + cacheFirst 체인. cacheFirst 본문은 원본과
        /// 문자 그대로 동일합니다(백엔드 추상화가 getCache() 뒤로 캡슐화되므로 호출부 불변).
        /// </summary>
        private const string CacheCoreJs = @"

            var cachePromise = null;
            function getCache() {
                if (!cachePromise) {
                    if (BACKEND_KIND === 'caches') {
                        cachePromise = caches.open(CACHE_NAME); // 네이티브 Cache = cache-like 그대로
                    } else {
                        // IDB open 은 행(hang) 가능성이 있으므로 타임아웃으로 상한을 둔다(IDB_OPEN_TIMEOUT_MS).
                        // 타임아웃/실패 시 cachePromise 는 rejected 상태로 메모이즈되며, cacheFirst()/sweep 의
                        // 기존 .catch 경로가 그대로 네트워크 폴백을 수행한다(재시도 없음: 반복 open 이 더 위험).
                        var opener = openIdbBackend(CACHE_NAME);
                        cachePromise = Promise.race([
                            opener,
                            new Promise(function (_resolve, reject) {
                                setTimeout(function () { reject(new Error('idb open timeout')); }, IDB_OPEN_TIMEOUT_MS);
                            })
                        ]);
                        // race 패자로 남아 뒤늦게 reject 되는 opener 가 unhandled rejection 을 띄우지 않도록 흡수.
                        opener.catch(function () {});
                    }
                }
                return cachePromise;
            }

            // 설치 시점의 fetch 를 캡처(여기선 native; Early Fetch 는 아직 미설치).
            var priorFetch = window.fetch.bind(window);

            // cache-first 체인: CacheStorage/IndexedDB 히트 → 단락, 미스 → priorFetch 후 비차단 put.
            // native-first 분기가 실패/미설정/타임아웃일 때의 폴백 경로로도 재사용됩니다.
            function cacheFirst(resource, init, url) {
                return getCache().then(function (cache) {
                    return cache.match(url).then(function (hit) {
                        if (hit) {
                            window.__aitCacheStats.hits.push(url);
                            return hit; // 캐시 히트 → 네트워크 0, transferSize 0 으로 단락.
                        }
                        window.__aitCacheStats.misses.push(url);
                        return priorFetch(resource, init).then(function (resp) {
                            // put 은 절대 await 하지 않음 → 부트 fetch 무지연(비차단).
                            try {
                                if (resp && resp.ok && resp.body !== undefined) {
                                    // decode-free 계약: 응답을 가공 없이 그대로 저장/반환.
                                    var clone = resp.clone();
                                    getCache().then(function (c) { return c.put(url, clone); })
                                        .then(function () { window.__aitCacheStats.puts.push(url); })
                                        .catch(function (e) {
                                            // QuotaExceededError 포함 모든 put 실패 흡수(부팅 무영향).
                                            // 공간 회복은 다음 부팅의 allowlist 정리에 위임.
                                            window.__aitCacheStats.errors.push('put ' + url + ': ' + (e && e.message || e));
                                        });
                                }
                            } catch (e) {
                                window.__aitCacheStats.errors.push('clone ' + url + ': ' + (e && e.message || e));
                            }
                            return resp; // 가공 없이 그대로 반환.
                        });
                    });
                }).catch(function (e) {
                    // match 경로 실패 시 원래 fetch 로 완전 위임(부트 미정지).
                    window.__aitCacheStats.errors.push('match ' + url + ': ' + (e && e.message || e));
                    return priorFetch(resource, init);
                });
            }";

        /// <summary>
        /// window.fetch 오버라이드. 원본 로직과 동일(문자 그대로 이동) — native-first 분기(리졸버 레이스/타임아웃)와
        /// cacheFirst 폴백을 포함합니다.
        /// </summary>
        private const string FetchOverrideJs = @"

            window.fetch = function (resource, init) {
                var url = absUrl(resource);
                // 비대상/비GET 은 그대로 위임(StreamingAssets/TemplateData/Document/loader.js 등).
                if (!url || !isCacheable(url) || isNonGet(resource, init)) {
                    return priorFetch(resource, init);
                }
                // native-first: 레버 ON + 호스트가 리졸버를 주입했으면 네이티브 프리페치 결과를 우선 시도.
                // 리졸버 계약: window.__aitResolveAsset(url) => Promise<Response|null>(동기 Response/null 도 허용).
                //  · Response 반환 → 그대로 서빙(cache.put 하지 않음: 네이티브가 자체 스토어 소유).
                //  · null/throw/reject/타임아웃 → cacheFirst 폴백(native→CacheStorage/IndexedDB→network 우선순위).
                if (NATIVE_SOURCE && typeof window.__aitResolveAsset === 'function') {
                    var nativeCall;
                    try {
                        // 동기/비동기 모두 흡수(동기 Response/null 도 Promise 로 정규화).
                        nativeCall = Promise.resolve(window.__aitResolveAsset(url));
                    } catch (e) {
                        // 동기 throw → 폴백.
                        window.__aitCacheStats.errors.push('native-throw ' + url + ': ' + (e && e.message || e));
                        return cacheFirst(resource, init, url);
                    }
                    var nativeTimer = null;
                    var timeoutP = new Promise(function (resolve) {
                        // setTimeout 콜백은 동기적으로 등록되므로 race 호출 전에 nativeTimer 가 할당됩니다.
                        nativeTimer = setTimeout(function () { resolve(null); }, NATIVE_TIMEOUT_MS);
                    });
                    return Promise.race([nativeCall, timeoutP]).then(function (nativeResp) {
                        if (nativeTimer) { clearTimeout(nativeTimer); nativeTimer = null; } // 타이머 누수 방지.
                        if (nativeResp) {
                            // 방어적 검사: raw-only 계약(네이티브는 해제된 bytes 를 Content-Encoding 없이 반환)을 위반해
                            // 압축 응답을 CE 헤더와 함께 돌려주면 loader.js 가 Unity 마커를 못 찾아 깨질 수 있음.
                            // 차단하진 않되(호스트 책임) 경고를 남겨 진단 가능하게 한다.
                            try {
                                if (nativeResp.headers && nativeResp.headers.get && nativeResp.headers.get('Content-Encoding')) {
                                    window.__aitCacheStats.errors.push('native-ce ' + url + ': resolver Content-Encoding 동반(raw-only 계약 위반)');
                                }
                            } catch (e) {}
                            window.__aitCacheStats.hits.push('native:' + url);
                            return nativeResp; // 네이티브 응답은 cache.put 하지 않음(스토어 이중화 방지).
                        }
                        // null/타임아웃 → cache-first 폴백.
                        return cacheFirst(resource, init, url);
                    }).catch(function (e) {
                        if (nativeTimer) { clearTimeout(nativeTimer); nativeTimer = null; } // 타이머 누수 방지.
                        // 비동기 reject → 폴백.
                        window.__aitCacheStats.errors.push('native-reject ' + url + ': ' + (e && e.message || e));
                        return cacheFirst(resource, init, url);
                    });
                }
                // 레버 OFF 또는 리졸버 미주입 → 기존 cache-first(어떤 네트워크/Early-Fetch 경로보다 우선).
                return cacheFirst(resource, init, url);
            };";

        /// <summary>
        /// 부팅 무효화(allowlist sweep) + 검증용 dump 헬퍼 + 최상위 catch + IIFE/&lt;script&gt; 종료.
        /// sweep/dump 는 getCache() 어댑터의 keys()/delete() 만 사용하므로 백엔드(caches/IndexedDB)와
        /// 무관하게 동일 코드로 동작합니다. allowlist 절대 URL 집합(ALLOW_ABS)은 isCacheable() 과
        /// 공유하며 BakedTailJs 에서 1회만 계산됩니다(리뷰 후속 수정: 중복 계산 제거).
        /// </summary>
        private const string SweepDumpCloseJs = @"

            // 부팅 무효화(설치 직후 1회, 비차단): allowlist 에 없는 옛 해시 엔트리를 백그라운드 정리.
            // await 하지 않으므로 부팅을 막지 않고, 실패는 catch 로 흡수합니다.
            // 멀티앱 주의: 같은 오리진의 여러 미니앱이 동일 CACHE_NAME 버킷을 공유하면, 이 sweep 이
            // 다른 앱의 Build/* 엔트리를 allowlist 외로 오인해 삭제합니다(키 충돌은 없으나 무효화 상호 간섭).
            // 오리진을 공유하는 앱이 둘 이상이면 앱별로 고유한 캐시명(=CACHE_NAME)을 지정하세요(appName 기반 자동 파생).
            try {
                // ALLOW_ABS 는 isCacheable() 과 동일한 값을 공유합니다(BakedTailJs 에서 1회 계산).
                getCache().then(function (c) {
                    return c.keys().then(function (reqs) {
                        for (var j = 0; j < reqs.length; j++) {
                            var k = reqs[j].url;
                            if (!ALLOW_ABS[k]) {
                                c.delete(reqs[j]).catch(function () {});
                            }
                        }
                    });
                }).catch(function (e) {
                    window.__aitCacheStats.errors.push('sweep: ' + (e && e.message || e));
                });
            } catch (e) {
                window.__aitCacheStats.errors.push('sweep-init: ' + (e && e.message || e));
            }

            // 검증/perf CI 용 덤프 헬퍼(운영 무영향).
            window.__aitCacheDump = function () {
                return getCache().then(function (c) { return c.keys(); }).then(function (keys) {
                    return keys.map(function (r) { return r.url; });
                });
            };
        } catch (e) {
            // 설치 과정의 어떤 예외도 부팅을 막지 않습니다(priorFetch=원래 fetch 가 그대로 살아 있음).
        }
    })();
    </script>";
    }
}
