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
    /// === 게이팅 규칙 ===
    ///  · <c>config == null || !config.emitWarmManifest</c> → stale 파일 삭제 후 no-op 반환.
    ///  · <c>emitWarmManifest=true &amp;&amp; enablePageCache=false</c> → 경고 로그 출력 후 no-op 반환.
    ///    (manifest 단독으로는 의미가 없습니다. 인터셉터 없이 배포하면 호스트가 참조할 캐시 버킷이 존재하지 않음.)
    ///  · 둘 다 true 일 때만 실제 파일을 산출합니다.
    ///
    /// === 호스트(슈퍼앱) 연동 계약 (코드 주석으로 명문화) ===
    ///  · cacheName: 호스트 백그라운드 pre-fill 페이지와 '동일한 캐시명'을 사용해야 같은 버킷을 공유합니다.
    ///    <c>config.pageCacheName</c> 이 비어 있으면 <see cref="AITPageCacheEmitter.DefaultCacheName"/> 으로 보정합니다.
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
        internal static void WriteManifest(
            AITEditorScriptObject config,
            string destPath,
            string loaderFile,
            string dataFile,
            string frameworkFile,
            string wasmFile,
            string symbolsFile)
        {
            string outputPath = Path.Combine(destPath, FileName);

            // 게이팅 1: emitWarmManifest 가 비활성이면 stale 파일 삭제 후 종료.
            if (config == null || !config.emitWarmManifest)
            {
                DeleteStale(outputPath);
                return;
            }

            // 게이팅 2: emitWarmManifest=true 이지만 enablePageCache=false 이면 무의미 — 미산출.
            // 호스트 pre-fill 없이 manifest 만 내보내면 참조할 캐시 버킷 자체가 없어 활용 불가.
            if (!config.enablePageCache)
            {
                Debug.LogWarning(
                    "[AIT] ait-warm-manifest.json 미산출: emitWarmManifest=true 이지만 enablePageCache=false 입니다. " +
                    "manifest 는 페이지 캐시 인터셉터(enablePageCache) 없이는 호스트가 참조할 캐시 버킷이 존재하지 않으므로 무의미합니다."
                );
                DeleteStale(outputPath);
                return;
            }

            // 캐시명: 비어 있으면 AITPageCacheEmitter 와 동일한 기본값으로 보정.
            string cacheName = string.IsNullOrEmpty(config.pageCacheName)
                ? AITPageCacheEmitter.DefaultCacheName
                : config.pageCacheName;

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
                return;
            }

            File.WriteAllText(outputPath, json, Encoding.UTF8);
            Debug.Log($"[AIT] ✓ ait-warm-manifest.json 산출 완료: {outputPath}");
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
