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
        private const int GIT_TIMEOUT_MS = 10000;

        // 최신 태그 캐시 (세션 중 1회만 조회)
        private static string _cachedLatestTag;
        private static bool _tagLookupDone;

        static AITDeprecationChecker()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        /// <summary>
        /// 최신 2.x 태그를 조회하고 캐싱합니다. 이미 조회한 경우 캐시를 반환합니다.
        /// </summary>
        private static string GetLatestV2TagCached()
        {
            if (_tagLookupDone) return _cachedLatestTag;
            _tagLookupDone = true;

            EditorUtility.DisplayProgressBar("SDK 업그레이드", "최신 2.x 버전을 확인하는 중...", 0.3f);
            try
            {
                _cachedLatestTag = FindLatestV2Tag();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return _cachedLatestTag;
        }

        /// <summary>
        /// 캐시된 태그에서 버전 문자열을 추출합니다 (예: "release/v2.0.5" → "2.0.5").
        /// </summary>
        private static string GetLatestVersionDisplay()
        {
            string tag = GetLatestV2TagCached() ?? FALLBACK_UPGRADE_TAG;
            var match = Regex.Match(tag, @"release/v(.+)$");
            return match.Success ? match.Groups[1].Value : "2.x";
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
            string latestVersion = GetLatestVersionDisplay();
            bool shouldUpdate = EditorUtility.DisplayDialog(
                "SDK 지원 종료 안내",
                $"현재 사용 중인 Apps in Toss Unity SDK v{AITVersion.Version}은 " +
                "지원이 종료되었습니다.\n\n" +
                "SDK 1.x 버전으로는 더 이상 빌드 및 배포가 불가능합니다.\n" +
                "앱인토스 콘솔에서도 2.0.0 미만 번들은 거부됩니다.\n\n" +
                $"최신 SDK v{latestVersion}으로 업그레이드해 주세요.",
                $"v{latestVersion}으로 업데이트",
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
            string tag = GetLatestV2TagCached();
            if (string.IsNullOrEmpty(tag))
            {
                tag = FALLBACK_UPGRADE_TAG;
                Debug.LogWarning($"[AIT] 최신 2.x 태그를 자동 감지하지 못했습니다. 기본값 사용: {tag}");
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

            string latestVersion = GetLatestVersionDisplay();
            EditorGUILayout.HelpBox(
                $"이 SDK 버전(v{AITVersion.Version})은 지원이 종료되었습니다.\n" +
                $"빌드 및 배포가 차단됩니다. 최신 SDK v{latestVersion}으로 업그레이드해 주세요.",
                MessageType.Error
            );

            if (GUILayout.Button($"v{latestVersion}으로 업데이트"))
            {
                UpgradeToLatest();
            }

            GUILayout.Space(10);
        }

        private static void OnEditorReady()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (IsDeprecated())
            {
                // AITAutoUpdater의 업데이트 다이얼로그와 중복 표시 방지
                SessionState.SetBool("AIT_AutoUpdate_Checked_v1", true);
                ShowDeprecationDialogPersistent();
            }
        }

        /// <summary>
        /// "닫기"를 눌러도 다시 표시되는 다이얼로그.
        /// 업데이트를 선택할 때까지 반복합니다.
        /// </summary>
        private static void ShowDeprecationDialogPersistent()
        {
            ShowDeprecationDialog();
            // 사용자가 "닫기"를 눌렀고 아직 deprecated면 다음 프레임에 다시 표시
            if (IsDeprecated())
            {
                EditorApplication.delayCall += ShowDeprecationDialogPersistent;
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
