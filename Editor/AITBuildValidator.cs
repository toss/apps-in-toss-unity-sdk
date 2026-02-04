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
            "%UNITY_WEBGL_CODE_URL%"
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
                    Debug.LogError("[AIT] ========================================");
                    Debug.LogError("[AIT] ✗ 치명적: 필수 플레이스홀더 미치환!");
                    Debug.LogError("[AIT] ========================================");
                    foreach (var placeholder in foundCritical)
                    {
                        Debug.LogError($"[AIT]   - {placeholder}");
                    }
                    Debug.LogError($"[AIT] 파일: {filePath}");
                    Debug.LogError("[AIT] ");
                    Debug.LogError("[AIT] 이 플레이스홀더들이 치환되지 않으면 런타임에서");
                    Debug.LogError("[AIT] 'createUnityInstance is not defined' 에러가 발생합니다.");
                    Debug.LogError("[AIT] ");
                    Debug.LogError("[AIT] 해결 방법:");
                    Debug.LogError("[AIT]   1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.");
                    Debug.LogError("[AIT]   2. AIT > Clean 메뉴로 빌드 폴더 삭제 후 재빌드하세요.");
                    Debug.LogError("[AIT] ========================================");
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
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] ✗ 치명적: 빈 파일 경로 발견!");
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] index.html에 'Build/' 뒤에 파일명이 없는 경로가 있습니다.");
                Debug.LogError("[AIT] 이로 인해 'createUnityInstance is not defined' 에러가 발생합니다.");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 원인: WebGL 빌드의 loader.js 파일을 찾지 못했습니다.");
                Debug.LogError("[AIT] 해결: 위의 빌드 파일 검색 로그를 확인하세요.");
                Debug.LogError("[AIT] ========================================");
                hasError = true;
            }

            return !hasError;
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

            var files = Directory.GetFiles(buildPath, pattern);
            if (files.Length > 0)
            {
                string fileName = Path.GetFileName(files[0]);
                if (isRequired)
                {
                    Debug.Log($"[AIT]   ✓ {pattern} → {fileName}");
                }
                return fileName;
            }

            if (isRequired)
            {
                Debug.LogError($"[AIT] ✗ 필수 파일을 찾을 수 없습니다: {pattern}");
                Debug.LogError($"[AIT]   검색 경로: {buildPath}");
                Debug.LogError($"[AIT]   이 파일이 없으면 런타임에서 'createUnityInstance is not defined' 에러가 발생합니다.");

                // 실제로 어떤 파일들이 있는지 출력
                var existingFiles = Directory.GetFiles(buildPath);
                if (existingFiles.Length > 0)
                {
                    Debug.LogError($"[AIT]   Build 폴더의 실제 파일들:");
                    foreach (var file in existingFiles)
                    {
                        Debug.LogError($"[AIT]     - {Path.GetFileName(file)}");
                    }
                }
                else
                {
                    Debug.LogError($"[AIT]   Build 폴더가 비어 있습니다!");
                }
            }

            return "";
        }

        /// <summary>
        /// 빌드 캐시 통계 출력
        /// </summary>
        internal static void LogBuildCacheStats(string buildProjectPath)
        {
            try
            {
                var nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
                var npmCachePath = Path.Combine(buildProjectPath, ".npm-cache");

                if (Directory.Exists(nodeModulesPath))
                {
                    long nodeModulesSize = AITFileUtils.GetDirectorySize(nodeModulesPath);
                    int packageCount = Directory.GetDirectories(nodeModulesPath).Length;

                    Debug.Log($"[AIT] ✓ 빌드 캐시 사용 중:");
                    Debug.Log($"[AIT]   - node_modules: {nodeModulesSize / 1024 / 1024}MB ({packageCount}개 패키지)");
                    Debug.Log($"[AIT]   - pnpm install 건너뜀 → 약 1-2분 절약!");
                }

                if (Directory.Exists(npmCachePath))
                {
                    long npmCacheSize = AITFileUtils.GetDirectorySize(npmCachePath);
                    Debug.Log($"[AIT]   - npm 캐시: {npmCacheSize / 1024 / 1024}MB");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AIT] 캐시 통계 출력 실패: {e.Message}");
            }
        }
    }
}
