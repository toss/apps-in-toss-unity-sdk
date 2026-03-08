using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

[assembly: InternalsVisibleTo("AppsInTossSDKEditor")]

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Unity SDK 버전 정보를 제공하는 정적 클래스
    /// </summary>
    [Preserve]
    public static class AITVersion
    {
        private static bool _loaded;
        private static string _version = "unknown";
        private static string _releaseDateTime;
        private static string _commitHash;

        /// <summary>
        /// SDK 버전 (예: "1.8.0")
        /// </summary>
        public static string Version
        {
            get { EnsureLoaded(); return _version; }
            private set { _version = value; }
        }

        /// <summary>
        /// 릴리즈 일시 (예: "20260126_1803"), 없으면 null
        /// </summary>
        public static string ReleaseDateTime
        {
            get { EnsureLoaded(); return _releaseDateTime; }
            private set { _releaseDateTime = value; }
        }

        /// <summary>
        /// 릴리즈 커밋 해시 (예: "e89a387"), 없으면 null
        /// </summary>
        public static string CommitHash
        {
            get { EnsureLoaded(); return _commitHash; }
            private set { _commitHash = value; }
        }

        /// <summary>
        /// 전체 버전 문자열 (예: "1.8.0 (20260126_1803, e89a387)")
        /// </summary>
        public static string FullVersion =>
            string.IsNullOrEmpty(ReleaseDateTime)
                ? Version
                : $"{Version} ({ReleaseDateTime}" +
                  (string.IsNullOrEmpty(CommitHash) ? ")" : $", {CommitHash})");

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            LoadVersionInfo();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureLoaded();
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
                // description 형식: "... (Released: 20260126_1204, e89a387)"
                var match = Regex.Match(
                    packageInfo.description,
                    @"\(Released:\s*(\d{8}_\d{4})(?:,\s*([a-f0-9]+))?\)"
                );
                ReleaseDateTime = match.Success ? match.Groups[1].Value : null;
                CommitHash = match.Success && match.Groups[2].Success ? match.Groups[2].Value : null;
            }
            else
            {
                // 로컬 개발 환경에서 패키지를 찾지 못한 경우 상수 사용
                Version = AITVersionConstants.Version ?? "unknown";
                ReleaseDateTime = AITVersionConstants.ReleaseDateTime;
                CommitHash = AITVersionConstants.CommitHash;
            }

            // CommitHash가 비어있으면 UPM 패키지 정보에서 조회
            if (string.IsNullOrEmpty(CommitHash) && packageInfo != null)
            {
                CommitHash = GetPackageCommitHash(packageInfo);
            }
        }

        private static string GetPackageCommitHash(
            UnityEditor.PackageManager.PackageInfo packageInfo
        )
        {
#if UNITY_2022_1_OR_NEWER
            if (packageInfo.git != null && !string.IsNullOrEmpty(packageInfo.git.hash))
            {
                var hash = packageInfo.git.hash;
                return hash.Length > 7 ? hash.Substring(0, 7) : hash;
            }
#endif
            // Unity 2021.3 폴백: packages-lock.json에서 추출
            var lockHash = GetHashFromPackagesLock(packageInfo.name);
            if (!string.IsNullOrEmpty(lockHash))
            {
                return lockHash.Length > 7 ? lockHash.Substring(0, 7) : lockHash;
            }
            return null;
        }

        /// <summary>
        /// packages-lock.json에서 패키지의 hash 필드를 추출
        /// </summary>
        /// <remarks>
        /// AITAutoUpdater(Editor)에서도 사용하므로 internal로 공개.
        /// 간단한 문자열 파싱으로 hash를 추출 (JSON 라이브러리 의존성 없이).
        /// </remarks>
        internal static string GetHashFromPackagesLock(string packageName)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string lockFilePath = Path.Combine(projectRoot, "Packages", "packages-lock.json");

                if (!File.Exists(lockFilePath))
                {
                    return null;
                }

                string content = File.ReadAllText(lockFilePath);

                // "패키지명": { ... "hash": "abc123" ... }
                int pkgIndex = content.IndexOf($"\"{packageName}\"");
                if (pkgIndex < 0)
                {
                    return null;
                }

                // 패키지 블록 내에서 hash 필드 찾기
                int hashIndex = content.IndexOf("\"hash\"", pkgIndex);
                if (hashIndex < 0)
                {
                    return null;
                }

                // 다음 패키지 블록 시작 전인지 확인 (안전 범위 제한)
                int nextPkgIndex = content.IndexOf("\n    }", pkgIndex);
                if (nextPkgIndex >= 0 && hashIndex > nextPkgIndex)
                {
                    return null;
                }

                // "hash": "값" 에서 값 추출
                int colonIndex = content.IndexOf(':', hashIndex);
                if (colonIndex < 0)
                {
                    return null;
                }

                int quoteStart = content.IndexOf('"', colonIndex + 1);
                if (quoteStart < 0)
                {
                    return null;
                }

                int quoteEnd = content.IndexOf('"', quoteStart + 1);
                if (quoteEnd < 0)
                {
                    return null;
                }

                string hash = content.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                return string.IsNullOrEmpty(hash) ? null : hash;
            }
            catch (Exception)
            {
                return null;
            }
        }
#endif

        private static void LoadVersionInfoRuntime()
        {
            Version = AITVersionConstants.Version ?? "unknown";
            ReleaseDateTime = AITVersionConstants.ReleaseDateTime;
            CommitHash = AITVersionConstants.CommitHash;
        }
    }
}
