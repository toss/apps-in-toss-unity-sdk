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
    /// === 호스트(슈퍼앱) 연동 계약 (코드 주석으로 명문화) ===
    ///  · 캐시명 규약: 호스트의 백그라운드 pre-fill 페이지와 '동일한 캐시명' 을 써야 같은 버킷을 공유합니다.
    ///    캐시명은 config.pageCacheName(또는 자동 파생값) 이며, 런타임 window.__AIT_CACHE_NAME 으로도 오버라이드됩니다.
    ///  · 캐시 키 규약: 절대 URL 문자열(new URL(resource, location.href).href).
    ///    nameFilesAsHashes=true(기본) 전제 → 파일명이 콘텐츠 해시이므로 키=불변 콘텐츠(스테일 없음).
    ///  · decode-free pre-fill 규약: 캐시에 Content-Encoding 없는 raw(해제된) bytes 가 들어 있으면
    ///    loader.js 가 Unity 마커를 못 찾아 Worker brotli 해제를 자연 스킵합니다.
    ///    이 raw-without-CE 저장은 호스트 pre-fill(또는 서버 CE:br 자동해제 응답)의 책임이며,
    ///    페이지 스니펫은 저장된 bytes 를 신뢰해 가공 없이 그대로 반환만 합니다.
    ///
    /// 부팅 무효화(allowlist): 빌드 시 확정된 현재 빌드 Build/* 파일(data/framework/wasm) 의 절대 URL 집합을
    /// 스니펫에 JSON 으로 박아, 설치 직후 1회 비차단으로 allowlist 에 없는 옛 해시 엔트리를 정리합니다.
    /// (loader.js 는 &lt;script src&gt; 라 fetch API 를 안 거치므로 캐시 대상이 아니며 allowlist 에서도 제외.)
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

            // 주의: 이 스니펫에는 %대문자_퍼센트% 토큰을 포함하지 않습니다(ValidatePlaceholderSubstitution 안전).
            // 설치 순서: index.html 에서 본 스니펫은 %AIT_EARLY_FETCH_SCRIPT% 보다 '앞'에 위치합니다.
            // 따라서 priorFetch 캡처 시점의 window.fetch 는 native 이며, 그 위를 이후 Early Fetch 가 래핑합니다.
            // 부트 fetch 흐름: ait-cache 래퍼 → (활성 시 Early Fetch 래퍼) → network.
            // 캐시 히트는 Early Fetch 소진과 무관하게 단락(short-circuit)합니다.
            return @"<script>
    // AIT Page Cache: 재방문 시 Build/* 자산을 CacheStorage 에서 직접 서빙(ServiceWorker 불필요).
    // 미지원/비보안 환경에서는 window.fetch 를 건드리지 않고 원래 로드로 무해 통과합니다.
    (function () {
        try {
            // 설치 가드(최우선): 비보안 컨텍스트이거나 CacheStorage 미지원이면 즉시 return → 원래 fetch 그대로.
            if (!window.isSecureContext || !('caches' in window)) { return; }

            // 캐시명: 런타임 오버라이드(window.__AIT_CACHE_NAME) 우선, 없으면 빌드 시 박힌 값.
            // 호스트 pre-fill 페이지와 '동일 캐시명' 이어야 같은 버킷을 공유합니다(연동 계약).
            var CACHE_NAME = (window.__AIT_CACHE_NAME) || " + cacheNameJs + @";

            // 부팅 무효화 allowlist: 현재 빌드의 Build/* 상대경로. 설치 직후 1회 정리에 사용.
            var ALLOWLIST = " + allowlistJson + @";

            // 통계 훅(perf CI/검증용, 운영 무영향).
            window.__aitCacheStats = { hits: [], misses: [], puts: [], errors: [] };

            // 인터셉트 조건: 동일 오리진 && /Build/ 경로. (GET 여부는 호출부에서 별도 판정.)
            function isCacheable(url) {
                try {
                    var u = new URL(url, location.href);
                    if (u.origin !== location.origin) { return false; }
                    return u.pathname.indexOf('/Build/') >= 0;
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
            }

            var cachePromise = null;
            function getCache() {
                if (!cachePromise) { cachePromise = caches.open(CACHE_NAME); }
                return cachePromise;
            }

            // 설치 시점의 fetch 를 캡처(여기선 native; Early Fetch 는 아직 미설치).
            var priorFetch = window.fetch.bind(window);

            window.fetch = function (resource, init) {
                var url = absUrl(resource);
                // 비대상/비GET 은 그대로 위임(StreamingAssets/TemplateData/Document/loader.js 등).
                if (!url || !isCacheable(url) || isNonGet(resource, init)) {
                    return priorFetch(resource, init);
                }
                // cache-first: 어떤 네트워크/Early-Fetch 경로보다 CacheStorage 를 먼저 시도.
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
            };

            // 부팅 무효화(설치 직후 1회, 비차단): allowlist 에 없는 옛 해시 엔트리를 백그라운드 정리.
            // await 하지 않으므로 부팅을 막지 않고, 실패는 catch 로 흡수합니다.
            // 멀티앱 주의: 같은 오리진의 여러 미니앱이 동일 CACHE_NAME 버킷을 공유하면, 이 sweep 이
            // 다른 앱의 Build/* 엔트리를 allowlist 외로 오인해 삭제합니다(키 충돌은 없으나 무효화 상호 간섭).
            // 오리진을 공유하는 앱이 둘 이상이면 앱별로 고유한 캐시명(=CACHE_NAME)을 지정하세요(appName 기반 자동 파생).
            try {
                var allowAbs = {};
                for (var i = 0; i < ALLOWLIST.length; i++) {
                    try { allowAbs[new URL(ALLOWLIST[i], location.href).href] = true; } catch (e) {}
                }
                getCache().then(function (c) {
                    return c.keys().then(function (reqs) {
                        for (var j = 0; j < reqs.length; j++) {
                            var k = reqs[j].url;
                            if (!allowAbs[k]) {
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
    }
}
