using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    internal static partial class WebGLBuildCopier
    {
        /// <summary>
        /// Unity WebGL 리소스를 조기 fetch하고 Unity loader의 fetch를 인터셉트하는 스크립트를 생성합니다.
        ///
        /// 배경: &lt;link rel="preload"&gt;는 CDN의 cache-control: max-age=0 환경에서 동작하지 않습니다.
        /// preload로 받은 리소스가 즉시 stale 처리되어 Unity loader가 재사용하지 못하고 revalidation 요청을 보내며,
        /// 이는 이중 로딩과 "preload is found, but is not used" 경고를 유발합니다.
        ///
        /// 해결: head에서 JS fetch()를 즉시 시작하고, window.fetch를 일회성으로 인터셉트하여
        /// Unity loader가 같은 URL을 요청할 때 이미 받은 Response를 반환합니다.
        /// </summary>
        private static string GenerateEarlyFetchScript(string dataFile, string wasmFile, string buildSrc, string bundleVersion)
        {
            var urls = new List<string>();
            if (!string.IsNullOrEmpty(dataFile)) urls.Add($"Build/{dataFile}");
            if (!string.IsNullOrEmpty(wasmFile)) urls.Add($"Build/{wasmFile}");

            if (urls.Count == 0) return "";

            // JSON 배열로 URL 목록 생성
            var urlsJson = "[" + string.Join(",", urls.ConvertAll(u => $"\"{u}\"")) + "]";

            // Unity 6000.x 로더는 자체 IndexedDB(UnityCache)로 데이터를 검증 캐싱하므로 warm reload에서
            // 재다운로드가 없다 → 우리 Cache-Storage 오버라이드는 순이득이 없고 스테일/이중 저장 위험만 더한다.
            // 레거시(2021/2022) 로더는 데이터 캐시가 없어 warm reload마다 100MB를 재다운로드 → 로컬 preview/CDN
            // 순단에 노출된다. 따라서 Cache-Storage 워밍은 레거시에만 적용하고 6000.x는 기존 스크립트를 유지한다.
            if (!IsLegacyUnityLoader())
            {
                return GenerateEarlyFetchScriptModern(urlsJson);
            }

            string cacheName = BuildDataCacheName(dataFile, wasmFile, buildSrc, bundleVersion);
            return GenerateEarlyFetchScriptLegacyCaching(urlsJson, cacheName);
        }

        /// <summary>
        /// Application.unityVersion의 메이저가 6000 미만이면 레거시 로더(자체 데이터 캐시 없음)로 판정한다.
        /// WebGL 빌드는 에디터 버전의 로더를 임베드하므로 빌드 시 에디터 버전 == 런타임 로더 버전이다.
        /// 파싱 실패 시 보수적으로 false(기존 무캐시 동작 유지)를 반환한다.
        /// </summary>
        private static bool IsLegacyUnityLoader()
        {
            try
            {
                string v = Application.unityVersion;
                int dot = v.IndexOf('.');
                string majorStr = dot > 0 ? v.Substring(0, dot) : v;
                if (int.TryParse(majorStr, out int major))
                {
                    return major < 6000;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Cache-Storage 캐시명 생성. 콘텐츠 변경 버스팅을 위해 data/wasm 파일의 바이트 크기와 bundleVersion을
        /// 캐시명에 포함한다. 2021 고정 파일명(webgl.data)에서도 콘텐츠(에셋/코드)가 바뀌면 최소 한 파일의 크기가
        /// 달라져 새 캐시명이 되고, 이전 빌드 캐시는 콜드 로드 시 정리된다(스테일 데이터/wasm 서빙 방지).
        /// </summary>
        private static string BuildDataCacheName(string dataFile, string wasmFile, string buildSrc, string bundleVersion)
        {
            long dataSize = FileSizeSafe(Path.Combine(buildSrc, dataFile));
            long wasmSize = string.IsNullOrEmpty(wasmFile) ? 0L : FileSizeSafe(Path.Combine(buildSrc, wasmFile));
            string ver = string.IsNullOrEmpty(bundleVersion) ? "0" : bundleVersion;
            return $"ait-unity-{SanitizeCacheToken(dataFile)}-{dataSize}-{wasmSize}-{SanitizeCacheToken(ver)}";
        }

        private static long FileSizeSafe(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0L; }
            catch { return 0L; }
        }

        private static string SanitizeCacheToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "0";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!ok) chars[i] = '_';
            }
            return new string(chars);
        }

        /// <summary>
        /// 6000.x용 기존 Early Fetch 스크립트(무캐시): 콜드 로드에서 병렬 prefetch → 로더에 전달, 재로드에서는 미설치.
        /// </summary>
        private static string GenerateEarlyFetchScriptModern(string urlsJson)
        {
            return $@"<script>
    // Early Fetch: HTML 파싱과 동시에 리소스 다운로드를 시작하고,
    // Unity loader가 같은 URL을 요청할 때 이미 받은 Response를 반환합니다.
    (function() {{
        // 재로드 내비게이션에서는 early fetch를 건너뛴다.
        // 재로드 시점에는 이전 문서의 keep-alive 소켓이 정리되는 중이라 파싱 시점 fetch가
        // 그 해체와 경합해 ERR_CONNECTION_CLOSED로 죽을 수 있고, 레거시 Unity(2021/2022)
        // 로더는 데이터 다운로드 실패를 삼키고 undefined를 resolve해 영구 행으로 이어진다.
        // 재로드에서는 HTTP 캐시가 이미 따뜻하므로 early fetch의 이득도 거의 없다 —
        // 인터셉터를 아예 설치하지 않으면 로더가 자체 단일 fetch를 수행한다(이중 다운로드 없음).
        try {{
            var navEntries = performance.getEntriesByType && performance.getEntriesByType('navigation');
            var isReload = (navEntries && navEntries[0])
                ? navEntries[0].type === 'reload'
                : (performance.navigation && performance.navigation.type === 1);
            if (isReload) return;
        }} catch (e) {{}}
        var earlyFetchMap = {{}};
        var urls = {urlsJson};
        for (var i = 0; i < urls.length; i++) {{
            (function(href, fetchUrl) {{
                var p = fetch(fetchUrl).catch(function() {{ delete earlyFetchMap[href]; return null; }});
                earlyFetchMap[href] = p;
            }})(new URL(urls[i], location.href).href, urls[i]);
        }}
        var originalFetch = window.fetch;
        window.fetch = function(resource, init) {{
            var url = (typeof resource === 'string') ? new URL(resource, location.href).href : resource.url;
            var pending = earlyFetchMap[url];
            if (pending) {{
                delete earlyFetchMap[url];
                if (Object.keys(earlyFetchMap).length === 0) {{
                    window.fetch = originalFetch;
                }}
                // early fetch 실패(null)·비정상 응답(!ok)·body 소진 시 원본 fetch로 재시도
                var self = this, args = arguments;
                return pending.then(function(r) {{
                    return (r && r.ok && !r.bodyUsed) ? r : originalFetch.apply(self, args);
                }});
            }}
            return originalFetch.apply(this, arguments);
        }};
    }})();
    </script>";
        }

        /// <summary>
        /// 레거시(2021/2022) 로더용 데이터 캐싱 + 버퍼링 재시도 fetch 인터셉터.
        ///
        /// 문제: 레거시 로더는 데이터 캐시가 없어 warm reload마다 webgl.data/wasm(~100MB)를 재다운로드한다.
        /// 부하 상태의 로컬 vite preview/CDN이 본문 스트림을 중단(ERR_CONNECTION_CLOSED)하면 로더의
        /// downloadBinary().catch가 실패를 삼키고 undefined를 resolve → new DataView(undefined.buffer)
        /// throw(loader.js:620) → 영구 로딩 행.
        ///
        /// 대책(버퍼링-재시도 모델 — 캐시가 아니라 네트워크 경로 자체가 순단에서 복구):
        ///  - window.fetch 오버라이드가 data/wasm 요청을 가로챈다. 캐시 HIT면 네트워크 없이 서빙(warm reload
        ///    순단 원천 차단). MISS면 bufferedFetch로 진행.
        ///  - bufferedFetch: 본문을 arrayBuffer()로 끝까지 받고 Content-Length와 대조. 스트림 중단 →
        ///    arrayBuffer reject, 길이 불일치 → throw → 최대 MAX_TRIES회 재시도. 성공 시 완결 버퍼를
        ///    Cache Storage에 저장(버퍼가 이미 완전 → put 원자적, 부분/오염 엔트리 없음)하고 로더에는
        ///    완결 Response(body 스트림 + Content-Length)를 반환한다 → 로더는 undefined를 볼 수 없다.
        ///  - 콜드 로드에서 data/wasm 모두 결정적으로 캐싱되므로(이전 clone-tee가 CI에서 wasm을 못 담던 문제
        ///    해소) 이후 reload는 전량 HIT → 네트워크 접촉 0.
        ///  - 저메모리 기기(deviceMemory<4)는 ~80MB 버퍼링이 OOM 위험 → 버퍼링/캐싱을 생략하고 원본
        ///    스트리밍 fetch로 폴백(기존 동작 유지, 제품 워치독이 방어).
        ///  - 워치독 복구 reload는 SKIP_KEY로 캐시를 1회 우회 → 오염 캐시가 복구를 막지 않음(self-amplification 차단).
        ///  - 캐시명에 data/wasm 바이트 크기 포함 → 콘텐츠 변경 시 자동 버스팅(2021 고정 파일명 스테일 방지).
        /// </summary>
        private static string GenerateEarlyFetchScriptLegacyCaching(string urlsJson, string cacheName)
        {
            return $@"<script>
    (function() {{
        var urls = {urlsJson};
        if (!urls || !urls.length) return;
        var CACHE_NAME = '{cacheName}';
        var SKIP_KEY = '__ait_skip_data_cache__';
        var MAX_TRIES = 3;

        var knownSet = {{}};
        for (var i = 0; i < urls.length; i++) {{
            knownSet[new URL(urls[i], location.href).href] = urls[i];
        }}

        // Cache Storage 가용성(보안 컨텍스트 필요: https 또는 localhost — E2E/프로덕션 모두 충족).
        var hasCache = false;
        try {{ hasCache = !!(self.caches && self.caches.open); }} catch (e) {{ hasCache = false; }}

        // 저메모리 기기(모바일 WebView)는 큰 파일 전체 버퍼링(~80MB)이 OOM 위험 → deviceMemory<4면
        // 버퍼링/캐싱을 생략하고 원본 스트리밍 fetch로 폴백(기존 동작 유지 + 제품 워치독 방어).
        // deviceMemory 미지원(undefined)이면 허용(데스크톱/CI 등 메모리 충분 가정).
        var bufOK = true;
        try {{ if (typeof navigator.deviceMemory === 'number' && navigator.deviceMemory < 4) bufOK = false; }} catch (e) {{}}
        var cacheOK = hasCache && bufOK;

        var isReload = false;
        try {{
            var navEntries = performance.getEntriesByType && performance.getEntriesByType('navigation');
            isReload = (navEntries && navEntries[0])
                ? navEntries[0].type === 'reload'
                : (performance.navigation && performance.navigation.type === 1);
        }} catch (e) {{}}

        // 워치독 복구 reload는 캐시를 1회 우회하고 네트워크(버퍼링 재시도)로 직행(오염 캐시에 복구가 갇히지 않도록).
        var skipCacheOnce = false;
        try {{
            if (sessionStorage.getItem(SKIP_KEY)) {{ skipCacheOnce = true; sessionStorage.removeItem(SKIP_KEY); }}
        }} catch (e) {{}}

        try {{ console.log('[AIT] cache: legacy active isReload=' + isReload + ' cacheOK=' + cacheOK + ' skip=' + skipCacheOnce + ' devMem=' + (navigator.deviceMemory)); }} catch (e) {{}}

        var originalFetch = window.fetch;

        // 콜드 로드에서 이전(스테일) 빌드 캐시 정리(현재 캐시명은 data/wasm 바이트 크기로 버스팅됨).
        if (!isReload && cacheOK) {{
            try {{
                self.caches.keys().then(function(names) {{
                    names.forEach(function(n) {{
                        if (n.indexOf('ait-unity-') === 0 && n !== CACHE_NAME) self.caches.delete(n);
                    }});
                }}).catch(function() {{}});
            }} catch (e) {{}}
        }}

        // 완결 버퍼로 Cache Storage에 저장. 버퍼가 이미 완전하므로 라이브 소켓/스트림 중단과 무관하게
        // 저장이 원자적으로 성공한다(부분/오염 엔트리 없음). fire-and-forget: 이후 reload가 HIT로 서빙.
        function storeBuffer(url, buf, ct) {{
            try {{
                var h = {{ 'Content-Type': ct || 'application/octet-stream', 'Content-Length': String(buf.byteLength) }};
                self.caches.open(CACHE_NAME).then(function(c) {{
                    return c.put(url, new Response(buf, {{ status: 200, headers: h }}));
                }}).then(function() {{
                    try {{ console.log('[AIT] cache: stored ' + url); }} catch (e) {{}}
                }}).catch(function() {{
                    try {{ console.warn('[AIT] cache: put failed ' + url); }} catch (e) {{}}
                }});
            }} catch (e) {{}}
        }}

        // 버퍼링 다운로드(재시도): 본문을 끝까지 받고 Content-Length와 대조.
        // 스트림 중단(ERR_CONNECTION_CLOSED) → arrayBuffer reject, 길이 불일치 → 재시도.
        // 성공 시 (cacheOK면) 캐시에 저장하고 로더에는 완결 Response(body 스트림 + Content-Length 보유)를 반환한다.
        // 모두 소진 시 원본 fetch로 폴백(로더가 실패를 삼키면 제품 워치독 reload가 처리).
        function bufferedFetch(url, left) {{
            return originalFetch(url, {{ method: 'GET' }}).then(function(r) {{
                if (!r || !r.ok) throw new Error('bad status ' + (r && r.status));
                var ct = r.headers.get('Content-Type') || 'application/octet-stream';
                var expected = parseInt(r.headers.get('Content-Length') || '-1', 10);
                return r.arrayBuffer().then(function(ab) {{
                    var buf = new Uint8Array(ab);
                    if (expected >= 0 && buf.byteLength !== expected) {{
                        throw new Error('short read ' + buf.byteLength + '/' + expected);
                    }}
                    if (cacheOK) {{ storeBuffer(url, buf, ct); }}
                    return new Response(buf, {{ status: 200, headers: {{ 'Content-Type': ct, 'Content-Length': String(buf.byteLength) }} }});
                }});
            }}).catch(function(e) {{
                if (left > 1) {{
                    try {{ console.warn('[AIT] cache: retry ' + url + ' (' + (left - 1) + ' left): ' + (e && e.message)); }} catch (x) {{}}
                    return bufferedFetch(url, left - 1);
                }}
                try {{ console.error('[AIT] cache: giveup ' + url + ': ' + (e && e.message)); }} catch (x) {{}}
                return originalFetch(url, {{ method: 'GET' }});
            }});
        }}

        window.fetch = function(resource, init) {{
            var url = (typeof resource === 'string') ? new URL(resource, location.href).href : resource.url;
            if (!knownSet[url]) return originalFetch.apply(this, arguments);
            var self2 = this, args = arguments;

            // 저메모리/무캐시: 버퍼링 없이 원본 스트리밍 fetch(기존 동작, 제품 워치독 방어).
            if (!cacheOK) return originalFetch.apply(self2, args);

            // 캐시 우선(skip 플래그면 우회). HIT → 네트워크 없이 서빙(warm reload 순단 원천 차단).
            if (!skipCacheOnce) {{
                return self.caches.open(CACHE_NAME).then(function(c) {{
                    return c.match(url, {{ ignoreSearch: true }});
                }}).then(function(cached) {{
                    if (cached && cached.ok) {{
                        try {{ console.log('[AIT] cache: HIT ' + url); }} catch (e) {{}}
                        return cached;
                    }}
                    try {{ console.log('[AIT] cache: MISS ' + url); }} catch (e) {{}}
                    return bufferedFetch(url, MAX_TRIES);
                }}).catch(function() {{
                    return bufferedFetch(url, MAX_TRIES);
                }});
            }}
            return bufferedFetch(url, MAX_TRIES);
        }};
    }})();
    </script>";
        }
    }
}
