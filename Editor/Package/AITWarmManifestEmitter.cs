using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// 빌드 시 <c>ait-warm-manifest.json</c> 을 산출합니다.
    ///
    /// 목적: 호스트(슈퍼앱)가 미니앱 목록 화면에서 Build 자산을 선다운로드(warm)할 때
    /// diff 기준으로 쓸 기계가독 계약 파일입니다.
    /// <see cref="AITPageCacheEmitter"/> 인터셉터의 allowlist 와 '같은 빌드의 같은 입력 변수'에서
    /// 생성되므로 두 파일 사이에 drift 가 불가능합니다.
    ///
    /// === 게이팅 규칙 (tri-state) ===
    ///  · <c>config == null</c> → stale 파일 삭제 후 no-op 반환.
    ///  · warmManifest 실효값 = (warmManifest == -1) ? GetDefaultWarmManifest() : (warmManifest == 1)
    ///  · 실효값 false → stale 파일 삭제 후 no-op 반환.
    ///  · pageCache 실효값 = (pageCache == -1) ? GetDefaultPageCache() : (pageCache == 1)
    ///  · warmManifest 실효값 true + pageCache 실효값 false → 경고 로그 출력 후 no-op 반환.
    ///    (manifest 단독으로는 의미가 없습니다. 인터셉터 없이 배포하면 호스트가 참조할 캐시 버킷이 존재하지 않음.)
    ///  · 둘 다 실효값 true 일 때만 실제 파일을 산출합니다.
    ///
    /// === 호스트(슈퍼앱) 연동 계약 (코드 주석으로 명문화) ===
    ///  · cacheName: 호스트 백그라운드 pre-fill 페이지와 '동일한 캐시명'을 사용해야 같은 버킷을 공유합니다.
    ///    <c>config.pageCacheName</c> 이 비어 있으면 <see cref="AITPageCacheEmitter.ResolveCacheName"/> 이
    ///    appName 파생 슬러그(ait-page-cache-{slug})로 보정합니다(pageCache 인터셉터와 ★동일★ 버킷명).
    ///  · assets: data/framework/wasm 3종만 포함합니다. loader 와 symbols 는 excluded 에만 기재합니다.
    ///  · wireBytes: 실제 디스크 상의 (압축 포함) 파일 크기입니다.
    ///  · rawBytes: gzip/brotli 해제 후 바이트 수입니다. 탐지 불가 시 필드를 생략합니다.
    ///  · encoding: "gzip" | "br" | "none".
    ///
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITWarmManifestEmitter
    {
        internal const string FileName = "ait-warm-manifest.json";

        /// <summary>
        /// <paramref name="destPath"/> 아래에 <c>ait-warm-manifest.json</c> 을 산출합니다.
        /// </summary>
        /// <param name="config">AIT Editor 설정 오브젝트. null 이면 no-op.</param>
        /// <param name="destPath">web 루트 경로 (index.html 이 놓이는 디렉토리).</param>
        /// <param name="loaderFile">Build/ 내 loader 파일명 (excluded 항목).</param>
        /// <param name="dataFile">Build/ 내 data 파일명 (assets 항목).</param>
        /// <param name="frameworkFile">Build/ 내 framework 파일명 (assets 항목).</param>
        /// <param name="wasmFile">Build/ 내 wasm 파일명 (assets 항목).</param>
        /// <param name="symbolsFile">Build/ 내 symbols 파일명 (excluded 항목, 비어 있으면 생략).</param>
        /// <returns>
        /// 성공 시 .ait 메타데이터 <c>prefetch</c> 키에 인라인으로 실을 compact v2 카탈로그 JSON(single-line).
        /// 게이팅으로 미산출되거나 안전 검사 실패 시 <c>null</c>.
        /// (파일 <c>ait-warm-manifest.json</c> 은 warm 페이지가 소비하므로 별개로 계속 산출합니다.)
        /// </returns>
        internal static string WriteManifest(
            AITEditorScriptObject config,
            string destPath,
            string loaderFile,
            string dataFile,
            string frameworkFile,
            string wasmFile,
            string symbolsFile)
        {
            string outputPath = Path.Combine(destPath, FileName);

            // 게이팅 1: config 가 null 이거나 warmManifest 실효값이 false 이면 stale 파일 삭제 후 종료.
            // tri-state: -1=자동(GetDefaultWarmManifest()), 0=비활성, 1=활성.
            if (config == null)
            {
                DeleteStale(outputPath);
                return null;
            }

            bool warmManifestEffective = config.warmManifest < 0
                ? AITDefaultSettings.GetDefaultWarmManifest()
                : config.warmManifest == 1;

            if (!warmManifestEffective)
            {
                DeleteStale(outputPath);
                return null;
            }

            // 게이팅 2: warmManifest 실효값 true 이지만 pageCache 실효값 false 이면 무의미 — 미산출 + 경고.
            // 호스트 pre-fill 없이 manifest 만 내보내면 참조할 캐시 버킷 자체가 없어 활용 불가.
            // pageCache tri-state: -1=자동(GetDefaultPageCache()), 0=비활성, 1=활성.
            bool pageCacheEffective = config.pageCache < 0
                ? AITDefaultSettings.GetDefaultPageCache()
                : config.pageCache == 1;

            if (!pageCacheEffective)
            {
                Debug.LogWarning(
                    "[AIT] ait-warm-manifest.json 미산출: warmManifest 실효값=true 이지만 pageCache 실효값=false 입니다. " +
                    "manifest 는 페이지 캐시 인터셉터(pageCache) 없이는 호스트가 참조할 캐시 버킷이 존재하지 않으므로 무의미합니다."
                );
                DeleteStale(outputPath);
                return null;
            }

            // 캐시명: pageCache 인터셉터와 ★완전히 동일한★ 해석을 사용해야 같은 캐시 버킷을 공유한다.
            // 인라인 DefaultCacheName 폴백은 appName 파생(ait-page-cache-{slug})을 건너뛰므로
            // 인터셉터와 버킷이 어긋나 warm 재방문이 전부 캐시 미스가 된다 → ResolveCacheName 으로 통일.
            string cacheName = AITPageCacheEmitter.ResolveCacheName(config);

            // 버전 문자열: package.json 에서 추출. 실패 시 버전 없이 식별자만.
            string sdkVersion = ReadSdkVersion();
            string generator = string.IsNullOrEmpty(sdkVersion)
                ? "apps-in-toss.unity"
                : "apps-in-toss.unity@" + sdkVersion;

            // assets 및 totals 계산.
            long totalWire = 0;
            long totalRaw = 0;
            bool allRawKnown = true;

            var assets = new System.Collections.Generic.List<AssetEntry>();

            foreach (var tuple in new[]
            {
                (file: dataFile,      role: "data",      mime: "application/octet-stream"),
                (file: frameworkFile, role: "framework",  mime: "text/javascript"),
                (file: wasmFile,      role: "wasm",       mime: "application/wasm"),
            })
            {
                if (string.IsNullOrEmpty(tuple.file)) continue;

                string fullPath = Path.Combine(destPath, "Build", tuple.file);
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"[AIT] ait-warm-manifest.json: Build/{tuple.file} 파일을 찾을 수 없어 해당 항목을 건너뜁니다.");
                    allRawKnown = false;
                    continue;
                }

                long wireBytes = new FileInfo(fullPath).Length;
                string encoding;
                long rawBytes;
                bool rawKnown;
                DetectEncoding(fullPath, tuple.file, wireBytes, out encoding, out rawBytes, out rawKnown);

                totalWire += wireBytes;
                if (rawKnown)
                {
                    totalRaw += rawBytes;
                }
                else
                {
                    allRawKnown = false;
                }

                assets.Add(new AssetEntry
                {
                    path = "Build/" + tuple.file,
                    role = tuple.role,
                    mime = tuple.mime,
                    wireBytes = wireBytes,
                    rawBytes = rawBytes,
                    rawKnown = rawKnown,
                    encoding = encoding,
                });
            }

            // excluded 목록 구성.
            var excluded = new System.Collections.Generic.List<ExcludedEntry>();
            if (!string.IsNullOrEmpty(loaderFile))
            {
                excluded.Add(new ExcludedEntry
                {
                    path = "Build/" + loaderFile,
                    role = "loader",
                    reason = "script-src 로드 — page fetch 비경유, 인터셉터 서빙 불가",
                });
            }
            if (!string.IsNullOrEmpty(symbolsFile))
            {
                excluded.Add(new ExcludedEntry
                {
                    path = "Build/" + symbolsFile,
                    role = "symbols",
                    reason = "부팅 임계경로 밖",
                });
            }

            // JSON 직렬화 (StringBuilder 수작업, 2-space pretty, 키 순서 고정).
            string json = BuildJson(generator, cacheName, assets, excluded, totalWire, allRawKnown ? (long?)totalRaw : null);

            // 안전 검사: %대문자_언더스코어% 토큰이 산출물에 없어야 한다.
            if (Regex.IsMatch(json, @"%[A-Z0-9_]+%"))
            {
                Debug.LogError("[AIT] ait-warm-manifest.json 내부 오류: 미치환 플레이스홀더 토큰이 감지되어 파일을 산출하지 않습니다.");
                return null;
            }

            File.WriteAllText(outputPath, json, Encoding.UTF8);
            Debug.Log($"[AIT] ✓ ait-warm-manifest.json 산출 완료: {outputPath}");

            // .ait 메타데이터 prefetch 에 인라인으로 실을 compact v2 카탈로그를 산출.
            // nativeSource 실효값(tri-state: -1=자동, 0=비활성, 1=활성)을 카탈로그에 동봉.
            bool nativeSourceEffective = config.nativeAssetSource < 0
                ? AITDefaultSettings.GetDefaultNativeAssetSource()
                : config.nativeAssetSource == 1;
            string compactCatalog = BuildCompactCatalogJson(
                cacheName, nativeSourceEffective, assets, totalWire, allRawKnown ? (long?)totalRaw : null);

            // 안전 검사: compact 카탈로그에도 미치환 플레이스홀더가 없어야 한다.
            if (Regex.IsMatch(compactCatalog, @"%[A-Z0-9_]+%"))
            {
                Debug.LogError("[AIT] 프리페치 인라인 카탈로그 내부 오류: 미치환 플레이스홀더 토큰이 감지되어 prefetch 를 방출하지 않습니다.");
                return null;
            }

            return compactCatalog;
        }

        // -----------------------------------------------------------------------
        // 내부 자료형
        // -----------------------------------------------------------------------

        private struct AssetEntry
        {
            public string path;
            public string role;
            public string mime;
            public long wireBytes;
            public long rawBytes;
            public bool rawKnown;
            public string encoding;
        }

        private struct ExcludedEntry
        {
            public string path;
            public string role;
            public string reason;
        }

        // -----------------------------------------------------------------------
        // JSON 빌더 (외부 라이브러리 의존 없음, 키 순서 고정, 2-space indent)
        // -----------------------------------------------------------------------

        private static string BuildJson(
            string generator,
            string cacheName,
            System.Collections.Generic.List<AssetEntry> assets,
            System.Collections.Generic.List<ExcludedEntry> excluded,
            long totalWire,
            long? totalRaw)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"schemaVersion\": 1,");
            sb.AppendLine("  \"generator\": " + JsonString(generator) + ",");
            sb.AppendLine("  \"cacheName\": " + JsonString(cacheName) + ",");

            // assets 배열
            sb.AppendLine("  \"assets\": [");
            for (int i = 0; i < assets.Count; i++)
            {
                var a = assets[i];
                bool last = (i == assets.Count - 1);
                sb.AppendLine("    {");
                sb.AppendLine("      \"path\": " + JsonString(a.path) + ",");
                sb.AppendLine("      \"role\": " + JsonString(a.role) + ",");
                sb.AppendLine("      \"mime\": " + JsonString(a.mime) + ",");
                sb.AppendLine("      \"wireBytes\": " + a.wireBytes + ",");
                if (a.rawKnown)
                {
                    sb.AppendLine("      \"rawBytes\": " + a.rawBytes + ",");
                }
                sb.AppendLine("      \"encoding\": " + JsonString(a.encoding));
                sb.Append("    }");
                sb.AppendLine(last ? "" : ",");
            }
            sb.AppendLine("  ],");

            // excluded 배열
            sb.AppendLine("  \"excluded\": [");
            for (int i = 0; i < excluded.Count; i++)
            {
                var e = excluded[i];
                bool last = (i == excluded.Count - 1);
                sb.AppendLine("    {");
                sb.AppendLine("      \"path\": " + JsonString(e.path) + ",");
                sb.AppendLine("      \"role\": " + JsonString(e.role) + ",");
                sb.AppendLine("      \"reason\": " + JsonString(e.reason));
                sb.Append("    }");
                sb.AppendLine(last ? "" : ",");
            }
            sb.AppendLine("  ],");

            // totals 오브젝트
            sb.AppendLine("  \"totals\": {");
            sb.Append("    \"wireBytes\": " + totalWire);
            if (totalRaw.HasValue)
            {
                sb.AppendLine(",");
                sb.AppendLine("    \"rawBytes\": " + totalRaw.Value);
            }
            else
            {
                sb.AppendLine();
            }
            sb.AppendLine("  }");

            sb.Append("}");
            return sb.ToString();
        }

        // -----------------------------------------------------------------------
        // 인라인 카탈로그 빌더 (compact v2, single-line — .ait 메타데이터 prefetch 용)
        // -----------------------------------------------------------------------

        /// <summary>
        /// .ait 메타데이터 <c>prefetch</c> 키에 인라인으로 실을 compact v2 카탈로그 JSON 을 생성합니다.
        ///
        /// 파일(<c>ait-warm-manifest.json</c>, schemaVersion 1) 과 같은 입력에서 만들어지지만,
        /// 환경변수로 전달되므로 줄바꿈/들여쓰기 없는 single-line 이며 호스트(RN/콘솔)가
        /// 별도 파일 fetch 없이 카탈로그만으로 사전 다운로드할 수 있도록
        /// per-asset <c>mime/encoding/wireBytes/rawBytes</c> 를 모두 인라인합니다.
        ///
        /// v1 파일과의 차이: <c>generator</c>·<c>excluded</c> 생략(부팅 임계경로 밖 자산은 카탈로그 비대상),
        /// <c>nativeSource</c> 추가(인터셉터 native 우선 신호와 정합).
        /// </summary>
        private static string BuildCompactCatalogJson(
            string cacheName,
            bool nativeSource,
            System.Collections.Generic.List<AssetEntry> assets,
            long totalWire,
            long? totalRaw)
        {
            var sb = new StringBuilder();
            sb.Append("{\"schemaVersion\":2");
            sb.Append(",\"cacheName\":").Append(JsonString(cacheName));
            sb.Append(",\"nativeSource\":").Append(nativeSource ? "true" : "false");
            sb.Append(",\"assets\":[");
            for (int i = 0; i < assets.Count; i++)
            {
                var a = assets[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"path\":").Append(JsonString(a.path));
                sb.Append(",\"role\":").Append(JsonString(a.role));
                sb.Append(",\"mime\":").Append(JsonString(a.mime));
                sb.Append(",\"encoding\":").Append(JsonString(a.encoding));
                sb.Append(",\"wireBytes\":").Append(a.wireBytes);
                if (a.rawKnown) sb.Append(",\"rawBytes\":").Append(a.rawBytes);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append(",\"totals\":{\"wireBytes\":").Append(totalWire);
            if (totalRaw.HasValue) sb.Append(",\"rawBytes\":").Append(totalRaw.Value);
            sb.Append("}}");
            return sb.ToString();
        }

        // -----------------------------------------------------------------------
        // 인코딩/압축 탐지
        // -----------------------------------------------------------------------

        /// <summary>
        /// 파일의 압축 인코딩과 해제 바이트 수를 탐지합니다.
        /// </summary>
        /// <param name="fullPath">파일의 전체 경로.</param>
        /// <param name="fileName">파일명 (확장자 기반 brotli 추론용).</param>
        /// <param name="wireBytes">알려진 파일 크기 (rawBytes 폴백 시 사용).</param>
        /// <param name="encoding">탐지된 인코딩: "gzip" | "br" | "none".</param>
        /// <param name="rawBytes">해제 후 바이트 수 (탐지 불가 시 wireBytes 와 같거나 무의미).</param>
        /// <param name="rawKnown">rawBytes 를 신뢰할 수 있으면 true.</param>
        private static void DetectEncoding(
            string fullPath,
            string fileName,
            long wireBytes,
            out string encoding,
            out long rawBytes,
            out bool rawKnown)
        {
            encoding = "none";
            rawBytes = wireBytes;
            rawKnown = false;

            try
            {
                // 첫 2바이트로 gzip 마커 확인.
                byte[] header = new byte[2];
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int read = fs.Read(header, 0, 2);
                    if (read < 2)
                    {
                        // 파일이 너무 작으면 그냥 "none".
                        rawKnown = true;
                        return;
                    }
                }

                // gzip: 0x1f 0x8b
                if (header[0] == 0x1f && header[1] == 0x8b)
                {
                    encoding = "gzip";
                    long counted = CountDecompressedBytes_GZip(fullPath);
                    if (counted >= 0)
                    {
                        rawBytes = counted;
                        rawKnown = true;
                    }
                    return;
                }

                // brotli: 직접 타입 참조 금지(Unity mono BCL 에 System.IO.Compression.BrotliStream 이 없을 수 있음).
                // 리플렉션으로 BrotliStream 타입을 탐색한 뒤 있으면 스트리밍 디코드 시도.
                // (Unity mono BCL 은 .NET Standard 2.0 기반으로 BrotliStream 이 미포함인 버전이 많음.)
                long brotliBytes = TryCountDecompressedBytes_Brotli(fullPath);
                if (brotliBytes >= 0)
                {
                    encoding = "br";
                    rawBytes = brotliBytes;
                    rawKnown = true;
                    return;
                }

                // 타입 부재 또는 디코드 예외 폴백: .unityweb 확장자이면 brotli 로 간주하되 rawBytes 생략.
                if (fileName.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
                {
                    encoding = "br";
                    rawKnown = false;
                    return;
                }

                // 그 외: none, rawBytes = wireBytes.
                encoding = "none";
                rawBytes = wireBytes;
                rawKnown = true;
            }
            catch
            {
                // 탐지 과정 예외 — encoding "none", rawKnown=false 로 안전 처리.
                encoding = "none";
                rawKnown = false;
            }
        }

        /// <summary>
        /// GZipStream 을 64KB 버퍼로 스트리밍하여 해제 바이트 수를 카운트합니다.
        /// 메모리에 전체를 적재하지 않습니다.
        /// 실패 시 -1 반환.
        /// </summary>
        private static long CountDecompressedBytes_GZip(string fullPath)
        {
            try
            {
                const int bufferSize = 64 * 1024;
                byte[] buffer = new byte[bufferSize];
                long total = 0;
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                {
                    int read;
                    while ((read = gz.Read(buffer, 0, bufferSize)) > 0)
                    {
                        total += read;
                    }
                }
                return total;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 리플렉션으로 BrotliStream 타입을 탐색해 스트리밍 카운트를 시도합니다.
        /// Unity mono BCL 에 BrotliStream 이 없을 수 있으므로 직접 타입 참조 대신 리플렉션을 사용합니다.
        /// (Unity 2021.3 LTS 의 mono BCL 은 .NET Standard 2.0 기반 — BrotliStream 은 .NET Standard 2.1 추가.)
        /// 성공 시 해제 바이트 수, 실패/타입 부재 시 -1 반환.
        /// </summary>
        private static long TryCountDecompressedBytes_Brotli(string fullPath)
        {
            // AppDomain 내 로드된 어셈블리에서 BrotliStream 타입 탐색.
            Type brotliStreamType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType("System.IO.Compression.BrotliStream");
                    if (t != null)
                    {
                        brotliStreamType = t;
                        break;
                    }
                }
                catch
                {
                    // 어셈블리 열거 중 예외(리플렉션 권한 등) — 무시하고 계속.
                }
            }

            if (brotliStreamType == null)
            {
                return -1;
            }

            try
            {
                const int bufferSize = 64 * 1024;
                byte[] buffer = new byte[bufferSize];
                long total = 0;

                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Activator.CreateInstance(BrotliStream, Stream, CompressionMode.Decompress)
                    object brotliStream = Activator.CreateInstance(
                        brotliStreamType,
                        new object[] { fs, CompressionMode.Decompress }
                    );

                    using (var stream = (Stream)brotliStream)
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, bufferSize)) > 0)
                        {
                            total += read;
                        }
                    }
                }
                return total;
            }
            catch
            {
                return -1;
            }
        }

        // -----------------------------------------------------------------------
        // SDK 버전 읽기
        // -----------------------------------------------------------------------

        /// <summary>
        /// SDK package.json 에서 "version" 값을 읽어 반환합니다.
        /// 파일을 찾지 못하거나 파싱 실패 시 null 반환.
        /// </summary>
        private static string ReadSdkVersion()
        {
            try
            {
                // AITPackagePathResolver 를 이용해 package.json 의 부모 디렉토리(패키지 루트)를 탐색.
                // SdkPathResolver.GetSdkBuildConfigPath() → "WebGLTemplates/AITTemplate/BuildConfig~" 경로를 반환.
                // 패키지 루트는 그 위 3단계.
                string buildConfigPath = SdkPathResolver.GetSdkBuildConfigPath();
                if (!string.IsNullOrEmpty(buildConfigPath))
                {
                    // BuildConfig~ → AITTemplate → WebGLTemplates → 패키지루트
                    string packageRoot = Path.GetFullPath(Path.Combine(buildConfigPath, "..", "..", ".."));
                    string packageJsonPath = Path.Combine(packageRoot, "package.json");
                    if (File.Exists(packageJsonPath))
                    {
                        string text = File.ReadAllText(packageJsonPath, Encoding.UTF8);
                        var match = Regex.Match(text, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            catch
            {
                // 버전 탐지 실패는 빌드를 멈추지 않음.
            }

            return null;
        }

        // -----------------------------------------------------------------------
        // 유틸리티
        // -----------------------------------------------------------------------

        /// <summary>
        /// stale 파일이 있으면 예외 흡수하며 삭제합니다.
        /// </summary>
        private static void DeleteStale(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 삭제 실패는 무시 (읽기 전용 파일시스템 등).
            }
        }

        /// <summary>
        /// 문자열을 안전한 JSON 문자열 리터럴로 인코딩합니다(따옴표/역슬래시/제어문자 이스케이프).
        /// </summary>
        private static string JsonString(string value)
        {
            if (value == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20)
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
