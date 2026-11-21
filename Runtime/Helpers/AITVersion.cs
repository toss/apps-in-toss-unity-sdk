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
        internal const string PackageName = "im.toss.apps-in-toss-unity-sdk";
        internal const string LegacyPackageName = "com.appsintoss.miniapp";
        internal const string PackageAssetPath = "Packages/" + PackageName;
        internal const string LegacyPackageAssetPath = "Packages/" + LegacyPackageName;

        /// <summary>
        /// 플레이어 빌드에 포함되는 버전 정보 JSON 리소스 이름 (확장자 제외).
        /// Editor가 빌드 직전 Assets/Resources/AITVersionInfo.json 에 기록하고,
        /// 런타임이 Resources.Load<TextAsset>로 읽음. .cs가 아니므로 쓰기 시
        /// 도메인 리로드가 발생하지 않음.
        /// </summary>
        internal const string VersionInfoResourceName = "AITVersionInfo";

        [Serializable]
        internal sealed class VersionInfoPayload
        {
            public string version;
            public string releaseDateTime;
            public string commitHash;
            // 필드가 없는 이전 JSON은 JsonUtility가 0으로 채운다 (0 = 리비전 없음)
            public int revision;
        }

        /// <summary>
        /// package.json description의 릴리스 스탬프 정규식.
        /// "(Released: 20260126_1204, e89a387, +2)" 형식이고 해시와 리비전은 생략될 수 있다.
        /// </summary>
        internal const string ReleaseStampPattern =
            @"\(Released:\s*(\d{8}_\d{4})(?:,\s*([a-f0-9]+))?(?:,\s*\+([1-9]\d*))?\)";

        private static bool _loaded;
        private static string _version = "unknown";
        private static string _releaseDateTime;
        private static string _commitHash;
        private static int _revision;

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
        /// 같은 Version 안에서의 릴리스 리비전 (1부터). 알 수 없거나 리비전 없는 릴리스는 0.
        /// </summary>
        public static int Revision
        {
            get { EnsureLoaded(); return _revision; }
            private set { _revision = value; }
        }

        /// <summary>
        /// 릴리스 버전 (예: "1.8.0+2"). Revision이 0이면 Version과 같다.
        /// </summary>
        public static string ReleaseVersion =>
            Revision > 0 ? $"{Version}+{Revision}" : Version;

        /// <summary>
        /// 전체 버전 문자열 (예: "1.8.0+2 (20260126_1803, e89a387)")
        /// </summary>
        public static string FullVersion =>
            string.IsNullOrEmpty(ReleaseDateTime)
                ? ReleaseVersion
                : $"{ReleaseVersion} ({ReleaseDateTime}" +
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

        /// <summary>
        /// 릴리스 스탬프에서 일시·해시·리비전을 읽는다. 스탬프가 없거나 형식이 다르면 모두 비운다.
        /// </summary>
        internal static void ParseReleaseStamp(
            string description, out string releaseDateTime, out string commitHash, out int revision)
        {
            releaseDateTime = null;
            commitHash = null;
            revision = 0;
            if (string.IsNullOrEmpty(description)) return;

            var match = Regex.Match(description, ReleaseStampPattern);
            if (!match.Success) return;

            releaseDateTime = match.Groups[1].Value;
            commitHash = match.Groups[2].Success ? match.Groups[2].Value : null;
            // 패턴이 [1-9]\d*만 받으므로 int 범위를 넘는 경우만 TryParse가 걸러낸다
            if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var parsed))
            {
                revision = parsed;
            }
        }

        private static void ApplyReleaseStamp(string description)
        {
            ParseReleaseStamp(description, out var dateTime, out var hash, out var revision);
            ReleaseDateTime = dateTime;
            CommitHash = hash;
            Revision = revision;
        }

#if UNITY_EDITOR
        private static void LoadVersionInfoEditor()
        {
            // AITPackagePathResolver.FindSDKPackageInfo()의 null-coalescing 체인과 동일한 로직.
            // Runtime asmdef에서 Editor assembly를 참조할 수 없으므로 인라인 if-null 형태로 구현.
            // 폴백 체인: PackageAssetPath → LegacyPackageAssetPath → FindForAssembly
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                PackageAssetPath
            );
            // 레거시 패키지 ID 폴백
            if (packageInfo == null)
            {
                packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    LegacyPackageAssetPath
                );
            }
            // 어셈블리 기반 폴백 (Git UPM 패키지에서 경로가 다를 수 있음)
            if (packageInfo == null)
            {
                packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(AITVersion).Assembly
                );
            }
            if (packageInfo != null)
            {
                Version = packageInfo.version;
                // description 형식: "... (Released: 20260126_1204, e89a387, +2)" (해시·리비전은 선택)
                ApplyReleaseStamp(packageInfo.description);
            }
            else
            {
                // PackageInfo를 못 찾은 경우 Resources JSON 폴백 (플레이어 런타임과 동일 경로)
                LoadVersionInfoRuntime();
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
            // Resources/AITVersionInfo.json (Editor가 빌드 직전에 생성) 을 읽어 버전 정보 설정.
            // JSON이 없으면 (Editor를 한 번도 거치지 않은 raw clone 등) Version="unknown" 폴백.
            var asset = Resources.Load<TextAsset>(VersionInfoResourceName);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<VersionInfoPayload>(asset.text);
                if (payload == null) return;

                if (!string.IsNullOrEmpty(payload.version))
                {
                    Version = payload.version;
                }
                ReleaseDateTime = string.IsNullOrEmpty(payload.releaseDateTime) ? null : payload.releaseDateTime;
                CommitHash = string.IsNullOrEmpty(payload.commitHash) ? null : payload.commitHash;
                Revision = payload.revision > 0 ? payload.revision : 0;
            }
            catch (Exception)
            {
                // 파싱 실패는 무시하고 "unknown" 폴백 유지
            }
        }
    }
}
