using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Unity SDK 버전 정보를 제공하는 정적 클래스
    /// </summary>
    [Preserve]
    public static class AITVersion
    {
        /// <summary>
        /// SDK 버전 (예: "1.8.0")
        /// </summary>
        public static string Version { get; private set; } = "unknown";

        /// <summary>
        /// 릴리즈 일시 (예: "20260126_1803"), 없으면 null
        /// </summary>
        public static string ReleaseDateTime { get; private set; } = null;

        /// <summary>
        /// 전체 버전 문자열 (예: "1.8.0 (20260126_1803)")
        /// </summary>
        public static string FullVersion =>
            string.IsNullOrEmpty(ReleaseDateTime)
                ? Version
                : $"{Version} ({ReleaseDateTime})";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LoadVersionInfo();
            Debug.Log($"[AIT] Apps in Toss Unity SDK v{FullVersion}");
        }

        private static void LoadVersionInfo()
        {
#if UNITY_EDITOR
            LoadVersionInfoEditor();
#else
            LoadVersionInfoRuntime();
#endif
        }

#if UNITY_EDITOR
        private static void LoadVersionInfoEditor()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/im.toss.apps-in-toss-unity-sdk"
            );
            if (packageInfo != null)
            {
                Version = packageInfo.version;
                var match = Regex.Match(
                    packageInfo.description,
                    @"\(Released:\s*(\d{8}_\d{4})\)"
                );
                ReleaseDateTime = match.Success ? match.Groups[1].Value : null;
            }
            else
            {
                // 로컬 개발 환경에서 패키지를 찾지 못한 경우 상수 사용
                Version = AITVersionConstants.Version ?? "unknown";
                ReleaseDateTime = AITVersionConstants.ReleaseDateTime;
            }
        }
#endif

        private static void LoadVersionInfoRuntime()
        {
            Version = AITVersionConstants.Version ?? "unknown";
            ReleaseDateTime = AITVersionConstants.ReleaseDateTime;
        }
    }
}
