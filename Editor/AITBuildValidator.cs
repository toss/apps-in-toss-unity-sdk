using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 결과 검증 및 리포트
    /// </summary>
    internal static class AITBuildValidator
    {
        // 치명적 플레이스홀더 목록 (이 플레이스홀더들이 미치환되면 빌드 실패)
        private static readonly string[] CriticalPlaceholders = new[]
        {
            "%UNITY_WEBGL_LOADER_URL%",
            "%UNITY_WEBGL_DATA_URL%",
            "%UNITY_WEBGL_FRAMEWORK_URL%",
            "%UNITY_WEBGL_CODE_URL%",
            // perf 채널 번들 마킹(window.AITLoading.buildVariant) — 미치환이면 성능 귀속이
            // 침묵 유실되므로 빌드를 실패시켜 회귀를 조기에 드러낸다.
            "%AIT_BUILD_VARIANT%"
        };

        // 2.x→3.x 마이그레이션 시 USER_CONFIG로 흘러들 수 있는, 3.x에서 이동/이름변경된 키.
        // (displayName/icon → toss 개발자센터, webViewProps → webView, outdir → webBundleDir,
        //  bridgeColorMode → 콘솔/플랫폼 관리). USER_CONFIG에 남아 있으면 경고만 한다(빌드는 계속).
        private static readonly string[] DeprecatedUserConfigKeys = new[]
        {
            "displayName",
            "icon",
            "webViewProps",
            "outdir",
            "bridgeColorMode"
        };

        /// <summary>
        /// index.html에서 미치환된 플레이스홀더가 있는지 검증합니다.
        /// </summary>
        /// <returns>검증 성공 시 true, 치명적 오류 발견 시 false</returns>
        internal static bool ValidatePlaceholderSubstitution(string content, string filePath)
        {
            bool hasError = false;

            // %로 시작하고 %로 끝나는 패턴 검색 (예: %UNITY_WEBGL_LOADER_URL%)
            var regex = new System.Text.RegularExpressions.Regex(@"%[A-Z_]+%");
            var matches = regex.Matches(content);

            if (matches.Count > 0)
            {
                var uniquePlaceholders = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    uniquePlaceholders.Add(match.Value);
                }

                // 치명적 플레이스홀더 확인
                var foundCritical = new List<string>();
                foreach (var placeholder in uniquePlaceholders)
                {
                    foreach (var critical in CriticalPlaceholders)
                    {
                        if (placeholder == critical)
                        {
                            foundCritical.Add(placeholder);
                            break;
                        }
                    }
                }

                if (foundCritical.Count > 0)
                {
                    var placeholderList = string.Join("\n", foundCritical.ConvertAll(p => $"  - {p}"));
                    Debug.LogError(
                        "[AIT] ✗ 치명적: 필수 플레이스홀더 미치환!\n"
                        + placeholderList + "\n"
                        + $"파일: {filePath}\n"
                        + "이 플레이스홀더들이 치환되지 않으면 런타임에서 'createUnityInstance is not defined' 에러가 발생합니다.\n"
                        + "해결 방법:\n"
                        + "  1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n"
                        + "  2. AIT > Clean 메뉴로 빌드 폴더 삭제 후 재빌드하세요."
                    );
                    hasError = true;
                }
                else
                {
                    // 비치명적 플레이스홀더 (경고만)
                    Debug.LogWarning("[AIT] ========================================");
                    Debug.LogWarning("[AIT] ⚠ 미치환된 플레이스홀더 발견 (비치명적)");
                    Debug.LogWarning("[AIT] ========================================");
                    foreach (var placeholder in uniquePlaceholders)
                    {
                        Debug.LogWarning($"[AIT]   - {placeholder}");
                    }
                    Debug.LogWarning($"[AIT] 파일: {filePath}");
                    Debug.LogWarning("[AIT] ========================================");
                }
            }

            // 잘못된 경로 패턴 검증 (예: Build/ 뒤에 파일명이 없는 경우)
            if (content.Contains("src=\"Build/\"") || content.Contains("\"Build/\"") || content.Contains("Build/\","))
            {
                Debug.LogError(
                    "[AIT] ✗ 치명적: 빈 파일 경로 발견!\n"
                    + "index.html에 'Build/' 뒤에 파일명이 없는 경로가 있습니다.\n"
                    + "이로 인해 'createUnityInstance is not defined' 에러가 발생합니다.\n"
                    + "원인: WebGL 빌드의 loader.js 파일을 찾지 못했습니다.\n"
                    + "해결: 위의 빌드 파일 검색 로그를 확인하세요."
                );
                hasError = true;
            }

            return !hasError;
        }

        /// <summary>
        /// 생성된 빌드 설정 파일(apps-in-toss.config.ts / granite.config.ts)을 검증합니다.
        /// (① 마이그레이션 가드 — 잘못 포팅된 USER_CONFIG로 인한 bundle.json 손상을 빌드 단계에서 차단)
        ///  • SDK_GENERATED 영역에 미치환 %AIT_*%/%UNITY_*% 플레이스홀더가 있으면 치명적 → 빌드 중단
        ///    (실제 치환 실패. 이대로 배포하면 bundle.json에 플레이스홀더 문자열이 그대로 들어감)
        ///  • USER_CONFIG 영역의 SDK 플레이스홀더/2.x deprecated 키는 경고만 → 빌드 계속
        ///    (apps-in-toss.config.ts 병합에서 SDK 값이 우선하므로 결과는 정상, 정리만 권고)
        /// 검증기 자체 예외는 빌드를 막지 않는다(보수적으로 SUCCEED 반환).
        /// </summary>
        /// <returns>치명적 미치환 발견 시 PLACEHOLDER_SUBSTITUTION_FAILED, 그 외 SUCCEED</returns>
        internal static AITConvertCore.AITExportError ValidateGeneratedBuildConfigs(string buildProjectPath)
        {
            try
            {
                bool hardError = false;

                foreach (var fileName in new[] { "apps-in-toss.config.ts", "granite.config.ts" })
                {
                    string path = Path.Combine(buildProjectPath, fileName);
                    if (!File.Exists(path)) continue;

                    string content = File.ReadAllText(path);

                    // 하드 에러: SDK_GENERATED 영역의 미치환 플레이스홀더 (= 치환 실패)
                    string sdkSection = AITTemplateManager.ExtractSdkSection(content) ?? content;
                    var sdkPlaceholders = FindUnsubstitutedPlaceholders(sdkSection);
                    if (sdkPlaceholders.Count > 0)
                    {
                        AITLog.Error(
                            $"[AIT] ✗ 치명적: {fileName}의 SDK_GENERATED 영역에 미치환 플레이스홀더가 있습니다: "
                            + string.Join(", ", sdkPlaceholders) + "\n"
                            + "  이 상태로 배포하면 bundle.json에 플레이스홀더 문자열이 그대로 들어가 앱 설정이 깨집니다.\n"
                            + "  해결: 'Clean Build' 옵션을 켜고 다시 빌드하세요.");
                        hardError = true;
                    }

                    // 경고: USER_CONFIG 영역의 SDK 플레이스홀더/2.x deprecated 키
                    // (병합 시 SDK 값이 우선하므로 빌드 결과는 정상 — 사용자에게 정리만 권고)
                    // 3.x 파일(apps-in-toss.config.ts)에만 적용. granite.config.ts는 2.x 레거시라
                    // USER_CONFIG에 webViewProps 등 2.x 키가 정상적으로 존재할 수 있어 오탐을 만든다.
                    string userSection = fileName == "apps-in-toss.config.ts"
                        ? AITTemplateManager.ExtractMarkerSection(content, "USER_CONFIG")
                        : null;
                    if (userSection != null)
                    {
                        var userPlaceholders = FindUnsubstitutedPlaceholders(userSection);
                        var deprecatedKeys = FindDeprecatedUserConfigKeys(userSection);
                        if (userPlaceholders.Count > 0 || deprecatedKeys.Count > 0)
                        {
                            string msg =
                                $"[AIT]   ⚠ {fileName}의 USER_CONFIG에 SDK가 관리하는 설정이 남아 있습니다. "
                                + "SDK 값이 우선 적용되어 빌드 결과에는 문제가 없지만, USER_CONFIG에서 제거하는 것을 권장합니다.";
                            if (userPlaceholders.Count > 0)
                                msg += "\n     - 미치환 플레이스홀더: " + string.Join(", ", userPlaceholders);
                            if (deprecatedKeys.Count > 0)
                                msg += "\n     - 3.x에서 이동/이름변경된 키: " + string.Join(", ", deprecatedKeys)
                                     + " (표시이름·아이콘은 toss 개발자센터, 색상/권한 등은 AIT Configuration 창에서 설정)";

                            // 사용자 환경(잘못 포팅된 USER_CONFIG)에 기인하므로 Sentry 전송은 억제하고 콘솔 경고만 남긴다.
                            AITLog.Warning(msg, sentryCapture: false);
                        }
                    }
                }

                return hardError
                    ? AITConvertCore.AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED
                    : AITConvertCore.AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                // 검증기 자체 결함으로 정상 빌드를 막지 않는다.
                AITLog.Warning($"[AIT] 빌드 설정 검증 중 예외(빌드는 계속): {e.Message}", sentryCapture: false);
                return AITConvertCore.AITExportError.SUCCEED;
            }
        }

        /// <summary>
        /// 텍스트에서 미치환된 %AIT_*% / %UNITY_*% 플레이스홀더를 중복 없이 찾습니다.
        /// </summary>
        private static List<string> FindUnsubstitutedPlaceholders(string content)
        {
            var found = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(content, @"%(?:AIT|UNITY)_[A-Z0-9_]+%"))
            {
                found.Add(m.Value);
            }
            return new List<string>(found);
        }

        /// <summary>
        /// USER_CONFIG 섹션에서 3.x deprecated/이름변경 키가 프로퍼티 키로 사용되었는지 찾습니다.
        /// </summary>
        private static List<string> FindDeprecatedUserConfigKeys(string userSection)
        {
            var found = new List<string>();
            foreach (var key in DeprecatedUserConfigKeys)
            {
                // 식별자 경계 뒤에 `key:` 형태로 등장할 때만 키로 간주 (부분 문자열 오탐 방지)
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        userSection, $@"(^|[^A-Za-z0-9_$])({System.Text.RegularExpressions.Regex.Escape(key)})\s*:"))
                {
                    found.Add(key);
                }
            }
            return found;
        }

        /// <summary>
        /// 빌드 완료 후 결과 리포트를 출력합니다.
        /// </summary>
        internal static void PrintBuildReport(string buildProjectPath, string distPath)
        {
            Debug.Log("[AIT] ========================================");
            Debug.Log("[AIT] 빌드 완료 리포트");
            Debug.Log("[AIT] ========================================");

            // 1. 필수 파일 존재 확인
            string publicBuildPath = Path.Combine(buildProjectPath, "public", "Build");

            if (!Directory.Exists(publicBuildPath))
            {
                Debug.LogError($"[AIT] ✗ Build 폴더가 존재하지 않습니다: {publicBuildPath}");
                Debug.Log("[AIT] ========================================");
                return;
            }

            var requiredPatterns = new Dictionary<string, string>
            {
                { "*.loader.js", "Unity WebGL 로더 (필수)" },
                { "*.data*", "게임 데이터 (필수)" },
                { "*.framework.js*", "Unity 프레임워크 (필수)" },
                { "*.wasm*", "WebAssembly 바이너리 (필수)" }
            };

            var optionalPatterns = new Dictionary<string, string>
            {
                { "*.symbols.json*", "디버그 심볼 (선택)" }
            };

            bool hasErrors = false;

            Debug.Log("[AIT] ");
            Debug.Log("[AIT] WebGL 빌드 파일:");

            foreach (var kvp in requiredPatterns)
            {
                var files = Directory.GetFiles(publicBuildPath, kvp.Key);
                if (files.Length > 0)
                {
                    var fileInfo = new FileInfo(files[0]);
                    string size = FormatFileSize(fileInfo.Length);
                    Debug.Log($"[AIT]   ✓ {Path.GetFileName(files[0])} ({size}) - {kvp.Value}");
                }
                else
                {
                    Debug.LogError($"[AIT]   ✗ {kvp.Key} - {kvp.Value} [누락됨!]");
                    hasErrors = true;
                }
            }

            foreach (var kvp in optionalPatterns)
            {
                var files = Directory.GetFiles(publicBuildPath, kvp.Key);
                if (files.Length > 0)
                {
                    var fileInfo = new FileInfo(files[0]);
                    string size = FormatFileSize(fileInfo.Length);
                    Debug.Log($"[AIT]   ○ {Path.GetFileName(files[0])} ({size}) - {kvp.Value}");
                }
            }

            // 2. index.html 검증
            string indexPath = Path.Combine(buildProjectPath, "index.html");
            if (File.Exists(indexPath))
            {
                string indexContent = File.ReadAllText(indexPath);

                // 빈 경로 검사
                bool hasBadPath = indexContent.Contains("src=\"Build/\"") ||
                                  indexContent.Contains("\"Build/\"") ||
                                  indexContent.Contains("Build/\",");

                if (hasBadPath)
                {
                    Debug.LogError("[AIT]   ✗ index.html에 잘못된 빌드 경로 발견!");
                    hasErrors = true;
                }
                else
                {
                    Debug.Log("[AIT]   ✓ index.html 경로 검증 통과");
                }
            }

            // 3. 최종 결과
            Debug.Log("[AIT] ");
            if (hasErrors)
            {
                Debug.LogError("[AIT] ⚠ 빌드에 문제가 있습니다!");
                Debug.LogError("[AIT] 위의 에러를 확인하고 Clean Build를 실행하세요.");
            }
            else
            {
                Debug.Log("[AIT] ✅ 모든 필수 파일 확인 완료");
                Debug.Log($"[AIT] 배포 폴더: {distPath}");
            }
            Debug.Log("[AIT] ========================================");
        }

        /// <summary>
        /// 파일 크기를 읽기 쉬운 형식으로 변환합니다.
        /// </summary>
        internal static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {suffixes[order]}";
        }

        /// <summary>
        /// 압축 포맷에 따른 파일 검색 패턴을 반환합니다.
        /// </summary>
        /// <param name="compressionFormat">압축 포맷 (0=Disabled, 1=Gzip, 2=Brotli, -1=와일드카드 폴백)</param>
        /// <param name="decompressionFallback">Decompression Fallback 활성 시 .unityweb 확장자 사용</param>
        internal static Dictionary<string, string> GetFilePatterns(int compressionFormat, bool decompressionFallback = false)
        {
            // decompressionFallback이 활성화되면 모든 파일이 .unityweb 확장자를 가짐 (loader 제외)
            if (decompressionFallback)
            {
                return new Dictionary<string, string>
                {
                    { "loader", "*.loader.js" },
                    { "data", "*.data.unityweb" },
                    { "framework", "*.framework.js.unityweb" },
                    { "wasm", "*.wasm.unityweb" },
                    { "symbols", "*.symbols.json.unityweb" }
                };
            }

            string ext;
            switch (compressionFormat)
            {
                case 0: ext = ""; break;       // Disabled
                case 1: ext = ".gz"; break;    // Gzip
                case 2: ext = ".br"; break;    // Brotli
                default: ext = null; break;    // Unknown → 와일드카드 폴백
            }

            if (ext == null) // 폴백: 기존 와일드카드
            {
                return new Dictionary<string, string>
                {
                    { "loader", "*.loader.js" },
                    { "data", "*.data*" },
                    { "framework", "*.framework.js*" },
                    { "wasm", "*.wasm*" },
                    { "symbols", "*.symbols.json*" }
                };
            }

            return new Dictionary<string, string>
            {
                { "loader", "*.loader.js" },  // loader는 압축되지 않음
                { "data", $"*.data{ext}" },
                { "framework", $"*.framework.js{ext}" },
                { "wasm", $"*.wasm{ext}" },
                { "symbols", $"*.symbols.json{ext}" }
            };
        }

        /// <summary>
        /// Build 폴더에서 패턴에 맞는 파일을 찾습니다.
        /// </summary>
        /// <param name="buildPath">검색할 빌드 경로</param>
        /// <param name="pattern">파일 패턴 (예: *.loader.js)</param>
        /// <param name="isRequired">필수 파일 여부 (true면 못 찾을 경우 에러 로그)</param>
        /// <returns>찾은 파일명 또는 빈 문자열</returns>
        internal static string FindFileInBuild(string buildPath, string pattern, bool isRequired = false)
        {
            if (!Directory.Exists(buildPath))
            {
                if (isRequired)
                {
                    Debug.LogError($"[AIT] 빌드 경로가 존재하지 않습니다: {buildPath}");
                }
                return "";
            }

            // *.data* 같은 와일드카드 꼬리 패턴은 *.data.meta 도 매칭하므로 .meta 는 제외해야 한다.
            // 그렇지 않으면 LastWriteTime 정렬 결과 .meta 가 최신으로 선택되어 잘못된 파일명을 반환한다.
            var files = Array.FindAll(
                Directory.GetFiles(buildPath, pattern),
                p => !p.EndsWith(".meta", StringComparison.Ordinal));
            if (files.Length > 0)
            {
                // 중복 파일 감지 시 최신 파일만 남기고 오래된 잔여물 자동 삭제
                // (경고 로그만으로는 매 빌드마다 반복 발생 → Sentry 노이즈 누적)
                if (files.Length > 1)
                {
                    // LastWriteTime 동률 시 파일명 내림차순으로 고정(Array.Sort는 불안정)
                    Array.Sort(files, (a, b) =>
                    {
                        int byTime = File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
                        return byTime != 0 ? byTime : string.CompareOrdinal(b, a);
                    });

                    string kept = Path.GetFileName(files[0]);
                    var deleted = new List<string>();
                    var failed = new List<string>();
                    for (int i = 1; i < files.Length; i++)
                    {
                        string stalePath = files[i];
                        try
                        {
                            File.Delete(stalePath);
                            string metaPath = stalePath + ".meta";
                            if (File.Exists(metaPath))
                            {
                                File.Delete(metaPath);
                            }
                            deleted.Add(Path.GetFileName(stalePath));
                        }
                        catch (Exception ex) when (
                            ex is IOException ||
                            ex is UnauthorizedAccessException ||
                            ex is PathTooLongException ||
                            ex is NotSupportedException ||
                            ex is System.Security.SecurityException)
                        {
                            failed.Add(Path.GetFileName(stalePath));
                        }
                    }

                    if (failed.Count == 0)
                    {
                        Debug.Log($"[AIT] ✓ '{pattern}' 이전 빌드 잔여물 {deleted.Count}개 자동 정리: {string.Join(", ", deleted)} (남김: {kept})");
                    }
                    else
                    {
                        // 삭제 실패 시 Clean Build 유도 (삭제 성공한 파일은 이미 없으므로 실패 목록만 표시)
                        Debug.Log($"[AIT] ℹ️ '{pattern}' 이전 빌드 잔여물 {failed.Count}개 정리 실패: {string.Join(", ", failed)}");
                        Debug.Log("[AIT]    'Clean Build' 사용을 권장합니다.");
                    }
                }

                string fileName = Path.GetFileName(files[0]);
                if (isRequired)
                {
                    Debug.Log($"[AIT]   ✓ {pattern} → {fileName}");
                }
                return fileName;
            }

            if (isRequired)
            {
                // 사용자 환경의 빌드 산출물 누락 진단 로그 — Sentry로는 한 줄로 요약 전송하고
                // 상세 다단 로그는 sentryCapture:false로 콘솔에만 남긴다.
                // 첫 줄만 Sentry로 전송하면 fingerprint가 pattern 별로 안정적으로 묶임(SDK-KX 계열).
                // 후속 진단 줄들은 hash/path가 빌드마다 달라 fingerprint가 분산되므로(SDK-KY~M6 계열) Sentry 억제.
                AITLog.Error($"[AIT] ✗ 필수 파일을 찾을 수 없습니다: {pattern}");
                AITLog.Error($"[AIT]   검색 경로: {buildPath}", sentryCapture: false);
                AITLog.Error($"[AIT]   이 파일이 없으면 런타임에서 'createUnityInstance is not defined' 에러가 발생합니다.", sentryCapture: false);

                var existingFiles = Directory.GetFiles(buildPath);
                if (existingFiles.Length > 0)
                {
                    AITLog.Error($"[AIT]   Build 폴더의 실제 파일들:", sentryCapture: false);
                    foreach (var file in existingFiles)
                    {
                        AITLog.Error($"[AIT]     - {Path.GetFileName(file)}", sentryCapture: false);
                    }
                }
                else
                {
                    AITLog.Error($"[AIT]   Build 폴더가 비어 있습니다!", sentryCapture: false);
                }
            }

            return "";
        }

        /// <summary>
        /// granite build 결과물(.ait 파일) 존재를 검증합니다.
        /// .ait 파일은 빌드 루트 또는 dist/ 폴더에 생성될 수 있습니다.
        /// </summary>
        internal static AITConvertCore.AITExportError ValidateDistOutput(string buildProjectPath)
        {
            // .ait 파일은 ait build CLI 버전에 따라 빌드 루트 또는 dist/에 생성될 수 있음
            var aitFiles = Directory.GetFiles(buildProjectPath, "*.ait");
            string distPath = Path.Combine(buildProjectPath, "dist");
            if (aitFiles.Length == 0 && Directory.Exists(distPath))
            {
                aitFiles = Directory.GetFiles(distPath, "*.ait");
            }

            if (aitFiles.Length == 0)
            {
                var msg = $"[AIT] ✗ .ait 파일이 생성되지 않았습니다!\n빌드 경로: {buildProjectPath}";
                if (Directory.Exists(distPath))
                {
                    var distFiles = Directory.GetFiles(distPath);
                    if (distFiles.Length > 0)
                    {
                        msg += "\ndist/ 폴더의 실제 파일들:";
                        foreach (var file in distFiles)
                        {
                            msg += $"\n  - {Path.GetFileName(file)}";
                        }
                    }
                }
                else
                {
                    msg += "\ndist/ 폴더도 존재하지 않습니다.";
                }
                Debug.LogError(msg);
                return AITConvertCore.AITExportError.AIT_FILE_MISSING;
            }

            foreach (var aitFile in aitFiles)
            {
                Debug.Log($"[AIT] ✓ .ait 파일 발견: {Path.GetFileName(aitFile)}");
            }
            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// 빌드 캐시 통계 출력
        /// </summary>
        internal static void LogBuildCacheStats(string buildProjectPath)
        {
            try
            {
                var nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
                if (Directory.Exists(nodeModulesPath))
                {
                    long nodeModulesSize = AITFileUtils.GetDirectorySize(nodeModulesPath);
                    int packageCount = Directory.GetDirectories(nodeModulesPath).Length;

                    Debug.Log($"[AIT] ✓ 빌드 캐시 사용 중:");
                    Debug.Log($"[AIT]   - node_modules: {nodeModulesSize / 1024 / 1024}MB ({packageCount}개 패키지)");
                    Debug.Log($"[AIT]   - pnpm install 건너뜀 → 약 1-2분 절약!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AIT] 캐시 통계 출력 실패: {e}");
            }
        }
    }
}
