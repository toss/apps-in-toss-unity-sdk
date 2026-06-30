using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// 빌드 시 <c>ait-warm.html</c> (self-warming 페이지)을 산출합니다.
    ///
    /// 목적: 호스트(슈퍼앱)가 게임 진입 전에 숨김 WebView 로 이 페이지를 열면,
    /// <c>ait-warm-manifest.json</c> 을 읽어 변경분(diff warm)만 내려받아
    /// CacheStorage(인터셉터와 동일 버킷)에 decode-free 로 적재합니다.
    ///
    /// === 게이팅 규칙 (tri-state) ===
    ///  · <c>config == null</c> → stale 파일 삭제 후 no-op 반환.
    ///  · warmPage 실효값 = (warmPage == -1) ? GetDefaultWarmPage() : (warmPage == 1)
    ///  · 실효값 false → stale 파일 삭제 후 no-op 반환.
    ///  · warmPage 실효값 true 이지만 warmManifest 실효값 false 또는 pageCache 실효값 false → 경고 로그 출력 후 no-op 반환.
    ///    (매니페스트 없이는 읽을 대상이 없고, 인터셉터(pageCache) 없이는 채운 캐시를 소비할 주체가 없음.)
    ///  · 세 조건 모두 실효값 true 일 때만 실제 파일을 산출합니다.
    ///
    /// === 호스트(슈퍼앱) 통합 계약 ===
    /// 1. 게임 진입 전(예: 목록 화면)에 숨김 WebView 로 <c>https://&lt;게임 오리진&gt;/ait-warm.html</c> 을 연다.
    ///    반드시 게임을 띄울 WebView 와 같은 프로파일/데이터스토어를 사용해야 CacheStorage 버킷이 공유된다.
    /// 2. 페이지는 <c>./ait-warm-manifest.json</c> 을 읽어 변경분만 내려받아 CacheStorage(설정된 cacheName)에
    ///    decode-free 로 저장한다. 이미 캐시된 자산(절대 URL 키 일치)은 네트워크를 타지 않는다(diff warm).
    ///    decode-free: <c>Content-Encoding</c> 없는 raw bytes 로 저장해 loader.js 가 Worker brotli 해제를 스킵(S2c).
    ///    CE:br 응답을 받으면 <c>arrayBuffer()</c> 재합성으로 CE 를 제거한다. CE 없는 응답은 <c>clone()</c> 그대로 저장.
    /// 3. 신호: <c>postMessage</c> type <c>ait:warm:progress|done|error</c> + <c>ReactNativeWebView.postMessage</c>
    ///    + <c>document.title</c> + <c>window.__aitWarmStats</c>.
    ///    <c>targetOrigin='*'</c>: 호스트 오리진을 빌드 시점에 알 수 없고 페이로드는 카운트뿐(비밀 없음).
    ///    호스트는 done 신호(또는 자체 타임아웃) 후 WebView 를 닫으면 된다.
    ///    warm 실패/미실행이어도 게임 로딩은 정상(캐시 miss 시 네트워크).
    /// 4. 쿼리 파라미터: <c>?timeout=&lt;ms&gt;</c>(기본 120000), <c>?concurrency=&lt;n&gt;</c>(기본 4).
    /// 5. 전제: pageCache 인터셉터와 동일한 cacheName, nameFilesAsHashes=true(기본).
    ///    멀티앱 주의: 같은 오리진의 여러 미니앱이 동일 cacheName 공유 시 stale 정리가 서로의 엔트리를
    ///    오삭제할 수 있음 — 앱별 고유 pageCacheName 을 권장한다.
    ///
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITWarmPageEmitter
    {
        internal const string FileName = "ait-warm.html";

        // HTML 템플릿 내 치환 마커 (ValidatePlaceholderSubstitution 의 %[A-Z0-9_]+% 정규식과 충돌하지 않음).
        private const string MarkerCacheName    = "__AIT_CACHE_NAME__";
        private const string MarkerManifestFile = "__AIT_MANIFEST_FILE__";

        /// <summary>
        /// <paramref name="destPath"/> 아래에 <c>ait-warm.html</c> 을 산출합니다.
        /// </summary>
        /// <param name="config">AIT Editor 설정 오브젝트. null 이면 no-op.</param>
        /// <param name="destPath">
        ///   Vite 정적 루트(publicPath). <c>ait-warm.html</c> 과 <c>ait-warm-manifest.json</c> 이 같은
        ///   웹 루트에 서빙되어 <c>./ait-warm-manifest.json</c> 상대 fetch 가 성립하고,
        ///   <c>Build/*</c> 도 같은 오리진에 위치합니다.
        /// </param>
        internal static void WritePage(AITEditorScriptObject config, string destPath)
        {
            string outputPath = Path.Combine(destPath, FileName);

            // 게이팅 1: config 가 null 이거나 warmPage 실효값이 false 이면 stale 파일 삭제 후 종료.
            // tri-state: -1=자동(GetDefaultWarmPage()), 0=비활성, 1=활성.
            if (config == null)
            {
                DeleteStale(outputPath);
                return;
            }

            bool warmPageEffective = config.warmPage < 0
                ? AITDefaultSettings.GetDefaultWarmPage()
                : config.warmPage == 1;

            if (!warmPageEffective)
            {
                DeleteStale(outputPath);
                return;
            }

            // 게이팅 2: warmPage 실효값 true 이지만 warmManifest 실효값 false 또는 pageCache 실효값 false → 미산출.
            // 매니페스트 없이는 warm 페이지가 읽을 대상이 없고,
            // 인터셉터(pageCache) 없이는 채운 캐시를 소비할 주체가 없음.
            bool warmManifestEffective = config.warmManifest < 0
                ? AITDefaultSettings.GetDefaultWarmManifest()
                : config.warmManifest == 1;
            bool pageCacheEffective    = config.pageCache < 0
                ? AITDefaultSettings.GetDefaultPageCache()
                : config.pageCache == 1;
            if (!warmManifestEffective || !pageCacheEffective)
            {
                Debug.LogWarning(
                    "[AIT] ait-warm.html 미산출: warmPage 실효값=true 이지만 " +
                    $"warmManifest 실효값={warmManifestEffective}, pageCache 실효값={pageCacheEffective} 입니다. " +
                    "warm 페이지는 매니페스트를 읽고 인터셉터 캐시 버킷에 기록합니다."
                );
                DeleteStale(outputPath);
                return;
            }

            // 캐시명: 비어 있으면 AITPageCacheEmitter 와 동일한 기본값으로 보정.
            string cacheName = string.IsNullOrEmpty(config.pageCacheName)
                ? AITPageCacheEmitter.DefaultCacheName
                : config.pageCacheName;

            // 마커 치환 (치환값 이스케이프: JS 단일따옴표 문자열용).
            string html = HtmlTemplate
                .Replace(MarkerCacheName,    EscapeJsSingleQuote(cacheName))
                .Replace(MarkerManifestFile, EscapeJsSingleQuote(AITWarmManifestEmitter.FileName));

            // 안전 검사 1: 마커 잔존 없음.
            if (html.Contains(MarkerCacheName) || html.Contains(MarkerManifestFile))
            {
                Debug.LogWarning("[AIT] ait-warm.html 내부 오류: 치환 마커가 산출물에 잔존합니다. 파일을 산출하지 않습니다.");
                return;
            }

            // 안전 검사 2: %[A-Z0-9_]+% 토큰 없음 (ValidatePlaceholderSubstitution 과 동일 기준).
            if (Regex.IsMatch(html, @"%[A-Z0-9_]+%"))
            {
                Debug.LogWarning("[AIT] ait-warm.html 내부 오류: 미치환 플레이스홀더 토큰이 감지되어 파일을 산출하지 않습니다.");
                return;
            }

            File.WriteAllText(outputPath, html, Encoding.UTF8);
            Debug.Log($"[AIT] ✓ ait-warm.html 산출 완료{(config.warmPage < 0 ? " (자동)" : "")}: {outputPath}");
        }

        // -----------------------------------------------------------------------
        // 유틸리티
        // -----------------------------------------------------------------------

        /// <summary>stale 파일이 있으면 예외 흡수하며 삭제합니다.</summary>
        private static void DeleteStale(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 삭제 실패는 무시 (읽기 전용 파일시스템 등).
            }
        }

        /// <summary>
        /// JS 단일따옴표 문자열에 안전하게 삽입할 수 있도록 이스케이프합니다.
        /// 실질적으로 cacheName 은 영숫자-하이픈이지만 방어적으로 처리합니다.
        /// </summary>
        private static string EscapeJsSingleQuote(string value)
        {
            if (value == null) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'");
        }

        // -----------------------------------------------------------------------
        // HTML 템플릿 (C# verbatim string — 내부 따옴표는 "" 이스케이프, JS 단일따옴표 위주)
        // 결정적 출력: 타임스탬프/난수/버전 문자열 0 — 같은 config 로 2회 호출 시 byte-identical.
        // -----------------------------------------------------------------------

        private const string HtmlTemplate = @"<!DOCTYPE html>
<html lang=""ko"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>ait-warm:idle</title>
</head>
<body>
  <noscript>JavaScript 가 필요합니다.</noscript>
  <p id=""status"">워밍 중...</p>
<script>
(function () {
  'use strict';

  // 빌드 시 주입된 상수.
  var BAKED_CACHE_NAME  = '__AIT_CACHE_NAME__';
  var MANIFEST_FILE     = '__AIT_MANIFEST_FILE__';
  var SCHEMA_VERSION    = 1;

  // 쿼리 파라미터 파싱 헬퍼: 정수 파라미터(기본값·최솟값·최댓값 포함).
  function qsInt(name, def, min, max) {
    try {
      var v = parseInt(new URLSearchParams(location.search).get(name), 10);
      if (isNaN(v)) { return def; }
      return Math.min(Math.max(v, min), max);
    } catch (e) { return def; }
  }

  var timeout     = qsInt('timeout',     120000, 1000, 600000);
  var concurrency = qsInt('concurrency', 4,      1,    16);

  // 절대 URL 생성: 인터셉터·매니페스트와 동일한 캐시 키 규약.
  function absUrl(path) {
    return new URL(path, location.href).href;
  }

  // 신호 발신: postMessage(부모·opener) + ReactNativeWebView + document.title + 전역 폴링용 변수.
  // targetOrigin='*': 호스트 오리진을 빌드 시점에 알 수 없고 페이로드는 카운트뿐(비밀 없음).
  function signal(msg) {
    window.__aitWarmStats = msg;
    var titleSuffix = (msg.type === 'ait:warm:done')
      ? ('done' + (msg.ok ? '' : ':error'))
      : msg.type.replace('ait:warm:', '');
    document.title = 'ait-warm:' + titleSuffix;
    var statusEl = document.getElementById('status');
    if (statusEl) { statusEl.textContent = document.title; }
    try { if (window.parent !== window) { window.parent.postMessage(msg, '*'); } } catch (e) {}
    try { if (window.opener) { window.opener.postMessage(msg, '*'); } } catch (e) {}
    try {
      if (window.ReactNativeWebView) {
        window.ReactNativeWebView.postMessage(JSON.stringify(msg));
      }
    } catch (e) {}
  }

  // decode-free 저장: Content-Encoding 이 있으면 body 를 arrayBuffer 로 읽어 CE 없는
  // 새 Response 로 재합성한다(S2c: loader.js 가 Unity 마커를 못 찾아 Worker brotli 해제 스킵).
  // CE 부재 시에는 clone 그대로 cache.put(zero-copy, S2c 또는 S2b 강등).
  // 반환값: 저장 바이트 수(buf.byteLength 또는 매니페스트 wireBytes/rawBytes 폴백, 실패 시 -1).
  function storeDecodeFree(cache, url, resp, wireBytes, rawBytes) {
    var ce = resp.headers.get('content-encoding');
    if (ce) {
      // 브라우저가 이미 자동 해제한 body 를 읽어 CE 없는 합성 Response 로 저장.
      return resp.arrayBuffer().then(function (buf) {
        var headers = { 'content-type': resp.headers.get('content-type') || 'application/octet-stream' };
        var synthetic = new Response(buf, { status: 200, headers: headers });
        return cache.put(url, synthetic).then(function () { return buf.byteLength; });
      });
    } else {
      // CE 없음: clone 그대로 put(zero-copy).
      var clone = resp.clone();
      return cache.put(url, clone).then(function () {
        // 바이트 수 추정: rawBytes 있으면 우선, 아니면 wireBytes.
        return (typeof rawBytes === 'number') ? rawBytes : (typeof wireBytes === 'number' ? wireBytes : 0);
      });
    }
  }

  // 동시성 풀: todo 배열을 workerCount 개의 루프가 소비.
  // 각 루프는 배열에서 splice(0,1) 로 하나씩 꺼내 처리한다.
  function runPool(workerCount, todo, deadline, cache, onProgress) {
    return new Promise(function (resolve) {
      var stored = 0, skipped = 0, failed = 0, bytes = 0;
      var stopReason = null;
      // 종료 카운터 = 워커 루프 수(아이템 수 아님). 각 루프는 종료 분기에서 정확히 1회 감소시키므로
      // 아이템 수로 초기화하면 todo.length > workerCount 일 때 0 에 도달하지 못해 resolve 가 누락된다.
      var workers = Math.min(workerCount, todo.length);
      var remaining = workers;

      if (todo.length === 0) { resolve({ stored: stored, skipped: skipped, failed: failed, bytes: bytes, reason: null }); return; }

      function next() {
        if (todo.length === 0 || stopReason) {
          remaining--;
          if (remaining <= 0) {
            resolve({ stored: stored, skipped: skipped, failed: failed, bytes: bytes, reason: stopReason });
          }
          return;
        }
        if (Date.now() > deadline) {
          stopReason = 'timeout';
          remaining--;
          if (remaining <= 0) {
            resolve({ stored: stored, skipped: skipped, failed: failed, bytes: bytes, reason: stopReason });
          }
          return;
        }
        var asset = todo.splice(0, 1)[0];
        fetch(asset.url, { cache: 'no-store' }).then(function (resp) {
          if (!resp.ok) {
            failed++;
            onProgress({ stored: stored, skipped: skipped, failed: failed, bytes: bytes });
            next();
            return;
          }
          storeDecodeFree(cache, asset.url, resp, asset.wireBytes, asset.rawBytes).then(function (b) {
            stored++;
            if (b > 0) { bytes += b; }
            onProgress({ stored: stored, skipped: skipped, failed: failed, bytes: bytes });
            next();
          }).catch(function (e) {
            // QuotaExceededError 시 전체 중단(쿼터 소진 후 계속은 무의미).
            var msg = (e && e.name) || '';
            if (msg === 'QuotaExceededError' || (e && e.message && e.message.indexOf('quota') >= 0)) {
              failed++;
              stopReason = 'quota';
              remaining--;
              if (remaining <= 0) {
                resolve({ stored: stored, skipped: skipped, failed: failed, bytes: bytes, reason: stopReason });
              }
              return;
            }
            failed++;
            onProgress({ stored: stored, skipped: skipped, failed: failed, bytes: bytes });
            next();
          });
        }).catch(function () {
          failed++;
          onProgress({ stored: stored, skipped: skipped, failed: failed, bytes: bytes });
          next();
        });
      }

      for (var i = 0; i < workers; i++) { next(); }
    });
  }

  async function main() {
    // 미지원/비보안 환경: no-op 강등 (인터셉터의 secure-context 가드와 동일 철학).
    if (!window.isSecureContext || !('caches' in window)) {
      signal({ type: 'ait:warm:done', ok: true, total: 0, stored: 0, skipped: 0, failed: 0, bytes: 0, elapsedMs: 0, reason: 'no-cache-api' });
      return;
    }

    var startMs  = Date.now();
    var deadline = startMs + timeout;

    // 매니페스트 취득.
    var manifest;
    try {
      var mResp = await fetch(MANIFEST_FILE, { cache: 'no-store' });
      if (!mResp.ok) { throw new Error('HTTP ' + mResp.status); }
      manifest = await mResp.json();
    } catch (e) {
      signal({ type: 'ait:warm:error', message: String(e) });
      signal({ type: 'ait:warm:done', ok: false, total: 0, stored: 0, skipped: 0, failed: 0, bytes: 0, elapsedMs: Date.now() - startMs, reason: 'manifest-fetch-failed' });
      return;
    }

    // 스키마 버전 체크.
    if (manifest.schemaVersion !== SCHEMA_VERSION) {
      signal({ type: 'ait:warm:done', ok: false, total: 0, stored: 0, skipped: 0, failed: 0, bytes: 0, elapsedMs: Date.now() - startMs, reason: 'schema-mismatch' });
      return;
    }

    // cacheName: 매니페스트 우선(빌드 skew 시 안전), 없으면 빌드 시 박힌 값.
    var cacheName = manifest.cacheName || BAKED_CACHE_NAME;
    var cache = await caches.open(cacheName);

    // 기캐시 URL 집합 구성 (절대 URL 키 규약).
    var existingKeys = await cache.keys();
    var existing = {};
    for (var i = 0; i < existingKeys.length; i++) { existing[existingKeys[i].url] = true; }

    // 자산 목록 절대화 + diff 계산.
    var assets = (manifest.assets || []);
    var targets = assets.map(function (a) {
      return { url: absUrl(a.path), wireBytes: a.wireBytes, rawBytes: a.rawBytes, path: a.path };
    });
    var manifestUrlSet = {};
    targets.forEach(function (t) { manifestUrlSet[t.url] = true; });

    var todo    = targets.filter(function (t) { return !existing[t.url]; });
    var skipped = targets.length - todo.length;
    var total   = targets.length;

    // 진행 신호 발신 헬퍼.
    function onProgress(counts) {
      signal({ type: 'ait:warm:progress', total: total, stored: counts.stored, skipped: skipped, failed: counts.failed, bytes: counts.bytes });
    }

    // 초기 진행 신호(skip 만 있는 경우 포함).
    signal({ type: 'ait:warm:progress', total: total, stored: 0, skipped: skipped, failed: 0, bytes: 0 });

    // 동시성 풀로 다운로드·저장.
    var result = await runPool(concurrency, todo, deadline, cache, onProgress);

    // stale 정리: 동일 오리진 && /Build/ 포함 && 매니페스트에 없는 엔트리 삭제.
    // 인터셉터 부팅 sweep 과 동일 원천(매니페스트=빌드 파일 목록)이라 일관적.
    // 비-Build 키는 보수적으로 불간섭.
    try {
      var staleKeys = await cache.keys();
      for (var j = 0; j < staleKeys.length; j++) {
        var k = staleKeys[j].url;
        try {
          var u = new URL(k);
          if (u.origin === location.origin && u.pathname.indexOf('/Build/') >= 0 && !manifestUrlSet[k]) {
            cache.delete(staleKeys[j]).catch(function () {});
          }
        } catch (e) {}
      }
    } catch (e) {}

    var elapsedMs = Date.now() - startMs;
    signal({
      type: 'ait:warm:done',
      ok: (result.failed === 0 && !result.reason),
      total: total,
      stored: result.stored,
      skipped: skipped,
      failed: result.failed,
      bytes: result.bytes,
      elapsedMs: elapsedMs,
      reason: result.reason || null
    });
  }

  // 전역 에러 안전망.
  window.addEventListener('error', function (e) {
    signal({ type: 'ait:warm:error', message: (e && e.message) || 'unknown error' });
  });
  window.addEventListener('unhandledrejection', function (e) {
    signal({ type: 'ait:warm:error', message: (e && e.reason && String(e.reason)) || 'unhandled rejection' });
  });

  main();
})();
</script>
</body>
</html>
";
    }
}
