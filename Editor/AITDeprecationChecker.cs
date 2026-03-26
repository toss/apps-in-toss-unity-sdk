using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 2.0.0 미만 버전 감지 시 빌드를 차단하고 최신 버전으로 업그레이드를 유도합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITDeprecationChecker
    {
        private static readonly Version DeprecationThreshold = new Version(2, 0, 0);
        private const string GIT_REPO_URL = "https://github.com/toss/apps-in-toss-unity-sdk.git";
        // git ls-remote 실패 시 사용할 폴백 태그. 새 2.x 릴리즈 시 갱신 필요.
        private const string FALLBACK_UPGRADE_TAG = "release/v2.0.5";
        private const string SESSION_KEY = "AIT_Deprecation_Checked_v1";
        private const int GIT_TIMEOUT_MS = 10000;

        static AITDeprecationChecker()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        /// <summary>
        /// 현재 SDK 버전이 지원 종료(deprecated)되었는지 확인합니다.
        /// </summary>
        public static bool IsDeprecated()
        {
            string versionStr = AITVersion.Version;
            if (string.IsNullOrEmpty(versionStr) || versionStr == "unknown")
            {
                return false;
            }

            try
            {
                var current = new Version(versionStr);
                return current < DeprecationThreshold;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// deprecated인 경우 다이얼로그를 표시하고 true를 반환합니다.
        /// 호출부에서 true면 즉시 return하여 빌드/배포를 차단하세요.
        /// </summary>
        public static bool BlockIfDeprecated()
        {
            if (!IsDeprecated()) return false;
            ShowDeprecationDialog();
            return true;
        }

        /// <summary>
        /// SDK 지원 종료 안내 다이얼로그를 표시합니다.
        /// </summary>
        public static void ShowDeprecationDialog()
        {
            bool shouldUpdate = EditorUtility.DisplayDialog(
                "SDK 지원 종료 안내",
                $"현재 사용 중인 Apps in Toss Unity SDK v{AITVersion.Version}은 " +
                "지원이 종료되었습니다.\n\n" +
                "SDK 1.x 버전으로는 더 이상 빌드 및 배포가 불가능합니다.\n" +
                "앱인토스 콘솔에서도 2.0.0 미만 번들은 거부됩니다.\n\n" +
                "최신 SDK 2.x로 업그레이드해 주세요.",
                "최신 SDK로 업데이트",
                "닫기"
            );

            if (shouldUpdate)
            {
                UpgradeToLatest();
            }
        }

        /// <summary>
        /// 최신 2.x 태그를 찾아 UPM으로 업그레이드합니다.
        /// </summary>
        public static void UpgradeToLatest()
        {
            EditorUtility.DisplayProgressBar("SDK 업그레이드", "최신 2.x 버전을 확인하는 중...", 0.3f);
            string tag;
            try
            {
                tag = FindLatestV2Tag();
                if (string.IsNullOrEmpty(tag))
                {
                    tag = FALLBACK_UPGRADE_TAG;
                    Debug.LogWarning($"[AIT] 최신 2.x 태그를 자동 감지하지 못했습니다. 기본값 사용: {tag}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string url = $"{GIT_REPO_URL}#{tag}";
            Debug.Log($"[AIT] SDK 업그레이드 중: {url}");
            UnityEditor.PackageManager.Client.Add(url);
        }

        /// <summary>
        /// Configuration 윈도우 상단에 deprecation 경고 배너를 그립니다.
        /// deprecated가 아니면 아무것도 그리지 않습니다.
        /// </summary>
        public static void DrawDeprecationBanner()
        {
            if (!IsDeprecated()) return;

            EditorGUILayout.HelpBox(
                $"이 SDK 버전(v{AITVersion.Version})은 지원이 종료되었습니다.\n" +
                "빌드 및 배포가 차단됩니다. 최신 SDK 2.x로 업그레이드해 주세요.",
                MessageType.Error
            );

            if (GUILayout.Button("최신 SDK로 업데이트"))
            {
                UpgradeToLatest();
            }

            GUILayout.Space(10);
        }

        private static void OnEditorReady()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SESSION_KEY, false)) return;

            SessionState.SetBool(SESSION_KEY, true);

            if (IsDeprecated())
            {
                // AITAutoUpdater의 업데이트 다이얼로그와 중복 표시 방지
                SessionState.SetBool("AIT_AutoUpdate_Checked_v1", true);
                ShowDeprecationDialog();
            }
        }

        /// <summary>
        /// git ls-remote로 최신 release/v2.* 태그를 찾습니다.
        /// </summary>
        private static string FindLatestV2Tag()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"ls-remote --tags \"{GIT_REPO_URL}\" \"refs/tags/release/v2.*\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using (var process = Process.Start(psi))
                {
                    process.BeginErrorReadLine();
                    string output = process.StandardOutput.ReadToEnd();

                    if (!process.WaitForExit(GIT_TIMEOUT_MS))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        return null;
                    }

                    if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                    {
                        return null;
                    }

                    return ParseLatestTag(output);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 최신 태그 조회 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// git ls-remote 출력에서 가장 높은 버전의 태그를 추출합니다.
        /// </summary>
        private static string ParseLatestTag(string output)
        {
            Version bestVersion = null;
            string bestTag = null;

            foreach (string line in output.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.EndsWith("^{}")) continue;

                // "해시\trefs/tags/release/v2.X.Y"
                int tabIndex = trimmed.IndexOf('\t');
                if (tabIndex < 0) continue;

                string refPath = trimmed.Substring(tabIndex + 1).Trim();
                var match = Regex.Match(refPath, @"refs/tags/(release/v(\d+\.\d+\.\d+))$");
                if (!match.Success) continue;

                string tag = match.Groups[1].Value;
                string versionStr = match.Groups[2].Value;

                try
                {
                    var version = new Version(versionStr);
                    if (bestVersion == null || version > bestVersion)
                    {
                        bestVersion = version;
                        bestTag = tag;
                    }
                }
                catch (Exception)
                {
                    // 파싱 실패한 태그는 무시
                }
            }

            return bestTag;
        }
    }
}
