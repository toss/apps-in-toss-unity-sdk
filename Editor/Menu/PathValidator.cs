using System.IO;
using UnityEngine;
using AppsInToss.Editor;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// 빌드 경로, npm/pnpm 경로, node_modules 무결성, 설정값 검증 유틸리티
    /// </summary>
    internal static class PathValidator
    {
        /// <summary>
        /// 출력이 에러인지 판단
        /// </summary>
        internal static bool IsErrorOutput(string output)
        {
            if (string.IsNullOrEmpty(output)) return false;

            string lower = output.ToLowerInvariant();

            // 명확한 에러 패턴
            if (lower.Contains("error:") ||
                lower.Contains("fatal:") ||
                lower.Contains("eaddrinuse") ||
                lower.Contains("port is already in use") ||
                lower.Contains("address already in use") ||
                lower.Contains("cannot find module") ||
                lower.Contains("command not found") ||
                lower.Contains("permission denied") ||
                lower.Contains("build failed") ||
                lower.Contains("deploy failed") ||
                lower.Contains("install failed"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 빌드 경로 유효성 검사
        /// </summary>
        internal static bool ValidateBuildPath(string buildPath)
        {
            if (!Directory.Exists(buildPath))
            {
                AITPlatformHelper.ShowInfoDialog("오류", "빌드 폴더를 찾을 수 없습니다.\n\n먼저 빌드를 실행하세요.", "확인");
                return false;
            }

            string indexPath = Path.Combine(buildPath, "index.html");
            if (!File.Exists(indexPath))
            {
                AITPlatformHelper.ShowInfoDialog("오류", "index.html을 찾을 수 없습니다.\n\n먼저 빌드를 실행하세요.", "확인");
                return false;
            }

            return true;
        }

        /// <summary>
        /// npm 경로 유효성 검사
        /// </summary>
        internal static bool ValidateNpmPath(string npmPath)
        {
            if (string.IsNullOrEmpty(npmPath))
            {
                AITLog.Error("AIT: npm을 찾을 수 없습니다.", sentryCapture: false);
                AITPlatformHelper.ShowInfoDialog("오류", "npm을 찾을 수 없습니다.\n\nNode.js가 설치되어 있는지 확인하세요.", "확인");
                return false;
            }
            return true;
        }

        /// <summary>
        /// node_modules 확인 및 설치
        /// </summary>
        internal static bool EnsureNodeModules(string buildPath, string npmPath)
        {
            string nodeModulesPath = Path.Combine(buildPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                Debug.Log("AIT: node_modules가 없습니다. pnpm install을 실행합니다...");

                string localCachePath = AITPackageBuilder.GetSharedPnpmStorePath();
                var installResult = AITNpmRunner.RunNpmCommandWithCache(
                    buildPath, npmPath, "install", localCachePath, "pnpm install 실행 중..."
                );

                if (installResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    AITLog.Error("AIT: pnpm install 실패", sentryCapture: false);
                    AITPlatformHelper.ShowInfoDialog("오류", "pnpm install 실패\n\nConsole 로그를 확인하세요.", "확인");
                    return false;
                }

                Debug.Log("AIT: pnpm install 완료");
            }
            return true;
        }

        internal static bool ValidateSettings(AITEditorScriptObject config)
        {
            if (config == null)
            {
                AITPlatformHelper.ShowInfoDialog("오류", "설정을 찾을 수 없습니다.", "확인");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 패키징/배포용 설정 검증 (appName 필수)
        /// </summary>
        internal static bool ValidateSettingsForPackage(AITEditorScriptObject config)
        {
            if (!ValidateSettings(config))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.appName))
            {
                AITLog.Error("AIT: App Name이 설정되지 않았습니다.", sentryCapture: false);
                AITPlatformHelper.ShowInfoDialog("오류", "App Name이 설정되지 않았습니다.\n\nAIT > Configuration에서 App Name을 입력해주세요.", "확인");
                return false;
            }

            return true;
        }

        internal static string GetBuildTemplatePath()
        {
            string projectPath = UnityUtil.GetProjectPath();
            return Path.Combine(projectPath, "ait-build");
        }

        internal static string FindNpmPath()
        {
            // AITConvertCore의 FindNpm 사용 (pnpm 우선, 로컬 설치 지원)
            return AITConvertCore.FindNpm();
        }
    }
}
