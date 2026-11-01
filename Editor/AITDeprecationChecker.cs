using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 최소 버전 미만 감지 시 빌드를 차단하고 최신 버전으로 업그레이드를 유도합니다.
    /// 최소 버전은 GitHub의 sdk-policy.json에서 동적으로 가져오며, 실패 시 하드코딩 폴백을 사용합니다.
    /// 최소 버전 이상이지만 recommendedVersion 미만이면 빌드는 막지 않고 하루 한 번 업데이트를 권장합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITDeprecationChecker
    {
        private static readonly Version FallbackDeprecationThreshold = new Version(2, 4, 0);
        private const string GIT_REPO_URL = "https://github.com/toss/apps-in-toss-unity-sdk.git";
        // SDK는 main 브랜치의 sdk-policy.json을 런타임에 fetch합니다.
        // 로컬 패키지에도 같은 파일이 포함되어 있으나, 정책의 정본(source of truth)은 main 브랜치입니다.
        private const string POLICY_URL = "https://raw.githubusercontent.com/toss/apps-in-toss-unity-sdk/main/sdk-policy.json";
        // git ls-remote 실패 시 사용할 폴백 태그.
        // 불변 조건: 이 태그의 버전은 반드시 FallbackDeprecationThreshold 이상이어야 합니다.
        private const string FALLBACK_UPGRADE_TAG = "release/v2.4.1";
        private const int GIT_TIMEOUT_MS = 10000;
        private const int SUPPORTED_SCHEMA_VERSION = 1;

        // 동적으로 가져온 minVersion이 이 값을 초과하면 무시합니다.
        // 손상된 정책 파일이 모든 사용자를 차단하는 것을 방지합니다.
        private static readonly Version MaxReasonableMinVersion = new Version(10, 0, 0);

        // 최신 태그 캐시 (세션 중 1회만 조회)
        private static string _cachedLatestTag;
        private static bool _tagLookupDone;

        // 동적 최소 버전 캐시 (세션 중 1회만 조회)
        private static Version _cachedMinVersion;
        private static bool _minVersionLookupDone;

        // 권장 버전 캐시. 최소 버전과 같은 정책 파일에서 함께 읽습니다.
        // 하드 차단이 아니므로 폴백이 없고, 조회 실패·필드 없음이면 null이라 권장하지 않습니다.
        private static Version _cachedRecommendedVersion;

        // 업그레이드 시작 후 다이얼로그 반복을 멈추기 위한 플래그.
        // PackageManager.Client.Add() 호출 후 리셋되지 않으며, 세션 동안 유지됩니다.
        // 업그레이드가 실패해도 다이얼로그가 다시 표시되려면 Unity를 재시작해야 합니다.
        private static bool _upgradeInProgress;

        // AITAutoUpdater가 세션 1회 검사 여부를 기록하는 키. 다이얼로그 중복 표시를 막을 때 씁니다.
        private const string AUTO_UPDATE_SESSION_KEY = "AIT_AutoUpdate_Checked_v1";
        // 권장 다이얼로그는 프로젝트별로 하루 한 번만 띄웁니다.
        private const string RECOMMENDATION_PROMPT_KEY_PREFIX = "AIT_UpdateRecommendation_LastPrompt_";

        /// <summary>
        /// WebClient에 timeout을 지정하기 위한 서브클래스.
        /// WebClient는 직접 timeout 프로퍼티를 노출하지 않으므로 GetWebRequest를 오버라이드합니다.
        /// </summary>
        private class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs = GIT_TIMEOUT_MS)
            {
                _timeoutMs = timeoutMs;
            }

            protected override WebRequest GetWebRequest(Uri uri)
            {
                var request = base.GetWebRequest(uri);
                request.Timeout = _timeoutMs;
                return request;
            }
        }

        static AITDeprecationChecker()
        {
            // delayCall에서 캐시를 미리 프라이밍하여 GUI repaint 중 네트워크 호출 방지
            EditorApplication.delayCall += () =>
            {
                GetMinVersionCached();
                OnEditorReady();
            };
        }

        /// <summary>
        /// 동적 최소 버전을 조회하고 캐싱합니다. 이미 조회한 경우 캐시를 반환합니다.
        /// </summary>
        internal static Version GetMinVersionCached()
        {
            if (_minVersionLookupDone) return _cachedMinVersion;
            _minVersionLookupDone = true;

            try
            {
                string json = FetchPolicyJson();
                if (json != null)
                {
                    _cachedMinVersion = ParseMinVersionFromJson(json);
                    _cachedRecommendedVersion = ParseRecommendedVersionFromJson(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 최소 버전 조회 실패, 폴백 사용: {e}");
                _cachedMinVersion = FallbackDeprecationThreshold;
            }

            if (_cachedMinVersion == null)
            {
                _cachedMinVersion = FallbackDeprecationThreshold;
            }

            Debug.Log($"[AIT] SDK 최소 버전 정책: v{_cachedMinVersion}");
            return _cachedMinVersion;
        }

        /// <summary>
        /// GitHub raw content에서 sdk-policy.json을 가져옵니다. 실패하면 null을 반환합니다.
        /// System.Net.WebClient를 사용하여 동기적으로 처리합니다 (UnityWebRequest는 메인 스레드에서
        /// busy-wait 시 이벤트 루프 의존성으로 인해 교착 상태가 발생할 수 있음).
        /// TimeoutWebClient로 GIT_TIMEOUT_MS(10초) timeout을 적용합니다.
        /// 참고: delayCall에서 호출되므로 메인 스레드를 최대 10초간 차단할 수 있습니다.
        /// 비동기 처리로 이를 완전히 해결할 수 있으나, 복잡도 대비 10초 worst-case가 수용 가능하다고 판단했습니다.
        /// </summary>
        private static string FetchPolicyJson()
        {
            try
            {
#pragma warning disable SYSLIB0014 // WebClient is obsolete in .NET 6+ but Unity still uses .NET Standard 2.1
                using (var client = new TimeoutWebClient())
                {
                    client.Headers.Add("User-Agent", "AppsInTossUnitySDK");
                    return client.DownloadString(POLICY_URL);
                }
#pragma warning restore SYSLIB0014
            }
            catch (Exception e)
            {
                // 네트워크/접근 실패 fallback — SDK 결함 아니므로 Sentry 전송 억제
                AITLog.Warning($"[AIT] sdk-policy.json fetch 실패: {e}", sentryCapture: false);
                return null;
            }
        }

        /// <summary>
        /// sdk-policy.json 문자열에서 minVersion을 파싱합니다.
        /// 2-part 버전(예: "2.4")은 3-part(예: "2.4.0")로 정규화합니다.
        /// </summary>
        internal static Version ParseMinVersionFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[AIT] sdk-policy.json이 비어 있습니다");
                return null;
            }

            // JsonUtility.FromJson은 malformed JSON에서 ArgumentException을 throw할 수 있습니다.
            SdkPolicy policy;
            try
            {
                policy = JsonUtility.FromJson<SdkPolicy>(json);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[AIT] sdk-policy.json 파싱 실패: {e.Message}");
                return null;
            }

            if (policy == null || string.IsNullOrEmpty(policy.minVersion))
            {
                Debug.LogWarning("[AIT] sdk-policy.json 파싱 실패: minVersion이 없습니다");
                return null;
            }

            if (policy.schemaVersion != SUPPORTED_SCHEMA_VERSION)
            {
                Debug.LogWarning($"[AIT] 지원하지 않는 sdk-policy.json schemaVersion: {policy.schemaVersion} (지원: {SUPPORTED_SCHEMA_VERSION})");
                return null;
            }

            try
            {
                var version = NormalizeVersion(new Version(policy.minVersion));

                // 비정상적으로 높은 minVersion은 정책 파일 손상으로 간주
                if (version > MaxReasonableMinVersion)
                {
                    Debug.LogWarning($"[AIT] minVersion {version}이 비정상적으로 높습니다. 폴백 사용.");
                    return null;
                }

                return version;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] minVersion 파싱 실패: {policy.minVersion} — {e}");
                return null;
            }
        }

        /// <summary>
        /// sdk-policy.json 문자열에서 recommendedVersion을 파싱합니다.
        /// 선택 필드라 없으면 조용히 null을 반환하고, 값이 잘못되어도 권장만 건너뜁니다.
        /// </summary>
        internal static Version ParseRecommendedVersionFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            SdkPolicy policy;
            try
            {
                policy = JsonUtility.FromJson<SdkPolicy>(json);
            }
            catch (ArgumentException)
            {
                // minVersion 파싱에서 이미 경고합니다.
                return null;
            }

            if (policy == null
                || policy.schemaVersion != SUPPORTED_SCHEMA_VERSION
                || string.IsNullOrEmpty(policy.recommendedVersion))
            {
                return null;
            }

            try
            {
                var version = NormalizeVersion(new Version(policy.recommendedVersion));
                if (version > MaxReasonableMinVersion)
                {
                    Debug.LogWarning($"[AIT] recommendedVersion {version}이 비정상적으로 높습니다. 무시합니다.");
                    return null;
                }

                return version;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] recommendedVersion 파싱 실패: {policy.recommendedVersion} — {e}");
                return null;
            }
        }

        /// <summary>
        /// 3-part로 정규화합니다 (System.Version에서 2.4 &lt; 2.4.0, 2.4.1 &lt; 2.4.1.0이므로).
        /// sdk-policy.json의 버전은 반드시 x.y.z 형식이어야 하지만, 방어적으로 정규화합니다.
        /// </summary>
        private static Version NormalizeVersion(Version version)
        {
            if (version.Build < 0)
            {
                return new Version(version.Major, version.Minor, 0);
            }

            if (version.Revision >= 0)
            {
                return new Version(version.Major, version.Minor, version.Build);
            }

            return version;
        }

        [Serializable]
        private class SdkPolicy
        {
            public int schemaVersion;
            public string minVersion;
            public string recommendedVersion;
        }

        /// <summary>
        /// 최신 태그를 조회하고 캐싱합니다. 이미 조회한 경우 캐시를 반환합니다.
        /// </summary>
        private static string GetLatestTagCached()
        {
            if (_tagLookupDone) return _cachedLatestTag;
            _tagLookupDone = true;

            EditorUtility.DisplayProgressBar("SDK 업그레이드", "최신 버전을 확인하는 중...", 0.3f);
            try
            {
                _cachedLatestTag = FindLatestTag();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return _cachedLatestTag;
        }

        /// <summary>
        /// 캐시된 태그에서 버전 문자열을 추출합니다 (예: "release/v2.4.1" → "2.4.1").
        /// </summary>
        private static string GetLatestVersionDisplay()
        {
            string tag = GetLatestTagCached() ?? FALLBACK_UPGRADE_TAG;
            var match = Regex.Match(tag, @"release/v(.+)$");
            return match.Success ? match.Groups[1].Value : "최신 버전";
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
                var minVersion = GetMinVersionCached();
                return current < minVersion;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 현재 SDK가 업데이트 권장 대상인지 확인합니다. 지원 종료(하드 차단) 대상은 권장이 아니라 차단 경로를 탑니다.
        /// </summary>
        public static bool IsUpdateRecommended()
        {
            string versionStr = AITVersion.Version;
            if (string.IsNullOrEmpty(versionStr) || versionStr == "unknown")
            {
                return false;
            }

            try
            {
                var minVersion = GetMinVersionCached();
                return ShouldRecommendUpdate(new Version(versionStr), minVersion, _cachedRecommendedVersion);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// current가 최소 버전 이상이고 권장 버전 미만이면 true를 반환합니다.
        /// </summary>
        internal static bool ShouldRecommendUpdate(Version current, Version minVersion, Version recommendedVersion)
        {
            return recommendedVersion != null && current >= minVersion && current < recommendedVersion;
        }

        /// <summary>
        /// 태그가 권장 버전 이상인 업그레이드 대상인지 확인합니다.
        /// 조회 실패 시 쓰는 FALLBACK_UPGRADE_TAG는 권장 버전보다 낮아 다운그레이드가 되므로 여기서 걸러집니다.
        /// </summary>
        internal static bool IsRecommendationTarget(string tag, Version recommendedVersion)
        {
            if (string.IsNullOrEmpty(tag) || recommendedVersion == null) return false;

            var match = Regex.Match(tag, @"^release/v(\d+\.\d+\.\d+)$");
            return match.Success && new Version(match.Groups[1].Value) >= recommendedVersion;
        }

        /// <summary>
        /// 업데이트 권장 다이얼로그 본문을 만듭니다.
        /// </summary>
        internal static string BuildRecommendationMessage(Version current, Version recommendedVersion)
        {
            string message =
                $"현재 사용 중인 Apps in Toss Unity SDK v{current}보다 새로운 " +
                $"v{recommendedVersion} 이상으로 업데이트를 권장합니다.\n\n" +
                "지금 버전으로도 빌드와 배포는 계속할 수 있습니다.";

            // 3.0.x만 서비스 origin이 달랐고 3.1.0에서 2.x origin으로 돌아왔습니다.
            bool leavesLegacyOrigin = current.Major == 3 && current.Minor == 0
                && recommendedVersion >= new Version(3, 1, 0);
            if (leavesLegacyOrigin)
            {
                message +=
                    "\n\nSDK 3.1.0부터 앱의 서비스 주소(origin)가 바뀝니다. " +
                    "3.0.x 주소에 저장된 데이터가 필요하면 업데이트 후 " +
                    "AIT.MigrationGetOriginStorage()로 직접 옮겨 주세요.";
            }

            return message;
        }

        /// <summary>
        /// deprecated인 경우 다이얼로그를 표시하고 true를 반환합니다.
        /// 호출부에서 true면 즉시 return하여 빌드/배포를 차단하세요.
        /// </summary>
        public static bool BlockIfDeprecated()
        {
            if (_upgradeInProgress || !IsDeprecated()) return false;
            ShowDeprecationDialog();
            return true;
        }

        /// <summary>
        /// SDK 지원 종료 안내 다이얼로그를 표시합니다.
        /// </summary>
        public static void ShowDeprecationDialog()
        {
            string latestVersion = GetLatestVersionDisplay();
            var minVersion = GetMinVersionCached();
            // Version(2,4,1).ToString() → "2.4.1" (3-part 보장: ParseMinVersionFromJson에서 정규화)
            bool shouldUpdate = EditorUtility.DisplayDialog(
                "SDK 지원 종료 안내",
                $"현재 사용 중인 Apps in Toss Unity SDK v{AITVersion.Version}은 " +
                "지원이 종료되었습니다.\n\n" +
                $"SDK v{minVersion} 미만 버전으로는 더 이상 빌드 및 배포가 불가능합니다.\n" +
                $"앱인토스 콘솔에서도 v{minVersion} 미만 번들은 거부됩니다.\n\n" +
                $"SDK {latestVersion}으로 업그레이드해 주세요.",
                $"{latestVersion}으로 업데이트",
                "닫기"
            );

            if (shouldUpdate)
            {
                UpgradeToLatest();
            }
        }

        /// <summary>
        /// 최신 태그를 찾아 UPM으로 업그레이드합니다.
        /// </summary>
        public static void UpgradeToLatest()
        {
            _upgradeInProgress = true;
            string tag = GetLatestTagCached();
            if (string.IsNullOrEmpty(tag))
            {
                tag = FALLBACK_UPGRADE_TAG;
                Debug.LogWarning($"[AIT] 최신 태그를 자동 감지하지 못했습니다. 기본값 사용: {tag}");
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
                $"빌드 및 배포가 차단됩니다. SDK {latestVersion}으로 업그레이드해 주세요.",
                MessageType.Error
            );

            if (GUILayout.Button($"{latestVersion}으로 업데이트"))
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
                SessionState.SetBool(AUTO_UPDATE_SESSION_KEY, true);
                ShowDeprecationDialogPersistent();
            }
            else if (IsUpdateRecommended())
            {
                ShowRecommendationDialogIfDue();
            }
        }

        /// <summary>
        /// 업데이트 권장 다이얼로그를 프로젝트별로 하루 한 번 표시합니다.
        /// 권장 버전 이상의 태그를 찾지 못하면 표시하지 않고, 다음 에디터 시작 때 다시 시도합니다.
        /// </summary>
        private static void ShowRecommendationDialogIfDue()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string key = $"{RECOMMENDATION_PROMPT_KEY_PREFIX}{Application.dataPath.GetHashCode():X8}";
            if (EditorPrefs.GetString(key, "") == today) return;

            if (!IsRecommendationTarget(GetLatestTagCached(), _cachedRecommendedVersion)) return;

            EditorPrefs.SetString(key, today);
            // AITAutoUpdater의 업데이트 다이얼로그와 중복 표시 방지
            SessionState.SetBool(AUTO_UPDATE_SESSION_KEY, true);

            string latestVersion = GetLatestVersionDisplay();
            bool shouldUpdate = EditorUtility.DisplayDialog(
                "SDK 업데이트 권장",
                BuildRecommendationMessage(new Version(AITVersion.Version), _cachedRecommendedVersion),
                $"{latestVersion}으로 업데이트",
                "나중에"
            );

            if (shouldUpdate)
            {
                UpgradeToLatest();
            }
        }

        /// <summary>
        /// "닫기"를 눌러도 다시 표시되는 다이얼로그.
        /// 업데이트를 선택할 때까지 반복합니다.
        /// </summary>
        private static void ShowDeprecationDialogPersistent()
        {
            if (_upgradeInProgress) return;
            ShowDeprecationDialog();
            // 사용자가 "닫기"를 눌렀고 업그레이드가 시작되지 않았으면 다음 프레임에 다시 표시
            if (!_upgradeInProgress)
            {
                EditorApplication.delayCall += ShowDeprecationDialogPersistent;
            }
        }

        /// <summary>
        /// git ls-remote로 최신 release/v* 태그 중 minVersion 이상인 최신 태그를 찾습니다.
        /// release/v* 전체를 조회하여 동적 minVersion에 따라 클라이언트 측에서 필터링합니다.
        /// 참고: 릴리즈 태그가 많아지면 네트워크 페이로드가 증가할 수 있습니다.
        /// git glob이 범위 필터를 지원하지 않아 서버 측 필터링이 불가능합니다.
        /// </summary>
        private static string FindLatestTag()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"ls-remote --tags \"{GIT_REPO_URL}\" \"refs/tags/release/v*\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using (var process = Process.Start(psi))
                {
                    process.BeginErrorReadLine();
                    // ReadToEnd()는 프로세스가 stdout을 닫을 때까지 블로킹됩니다.
                    // 따라서 아래 WaitForExit timeout은 ReadToEnd 완료 후에만 도달합니다.
                    // git ls-remote의 응답이 작으므로 실질적 문제는 없으나, 알려진 제한사항입니다.
                    string output = process.StandardOutput.ReadToEnd();

                    if (!process.WaitForExit(GIT_TIMEOUT_MS))
                    {
                        try { process.Kill(); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AIT] git ls-remote 프로세스 kill 실패: {ex.GetType().Name}: {ex.Message}");
                        }
                        return null;
                    }

                    if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                    {
                        return null;
                    }

                    return ParseLatestTag(output, GetMinVersionCached());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 최신 태그 조회 실패: {e}");
                return null;
            }
        }

        /// <summary>
        /// git ls-remote 출력에서 minVersion 이상인 가장 높은 버전의 태그를 추출합니다.
        /// minVersion 필터링은 업그레이드 대상으로 deprecated 버전을 제시하지 않기 위함입니다.
        /// </summary>
        internal static string ParseLatestTag(string output, Version minVersion)
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
                    if (version < minVersion) continue;
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

        /// <summary>
        /// 테스트용: 캐시를 초기화합니다.
        /// </summary>
        internal static void ResetCache()
        {
            _cachedLatestTag = null;
            _tagLookupDone = false;
            _cachedMinVersion = null;
            _minVersionLookupDone = false;
            _cachedRecommendedVersion = null;
            _upgradeInProgress = false;
        }

        /// <summary>
        /// 테스트용: 최소 버전 캐시를 직접 설정합니다.
        /// </summary>
        internal static void SetMinVersionForTesting(Version version)
        {
            _cachedMinVersion = version;
            _minVersionLookupDone = true;
        }
    }
}
