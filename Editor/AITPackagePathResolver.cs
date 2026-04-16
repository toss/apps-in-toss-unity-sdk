using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 패키지 경로를 안정적으로 찾기 위한 유틸리티.
    /// FindForAssetPath가 실패할 수 있는 Git UPM 패키지 환경에서 폴백을 제공합니다.
    /// 캐시는 도메인 리로드(스크립트 재컴파일) 시 자동으로 무효화됩니다.
    /// Unity Editor 메인 스레드에서만 호출되는 것을 전제로 합니다.
    /// </summary>
    internal static class AITPackagePathResolver
    {
        private const string PackageAssetPath = "Packages/im.toss.apps-in-toss-unity-sdk";
        private const string LegacyPackageAssetPath = "Packages/com.appsintoss.miniapp";

        private static PackageInfo _cachedInfo;
        private static bool _cacheInitialized;

        /// <summary>
        /// SDK의 PackageInfo를 찾습니다.
        /// FindForAssetPath 실패 시 FindForAssembly를 폴백으로 사용합니다.
        /// 결과는 도메인 리로드까지 캐싱됩니다.
        /// </summary>
        /// <remarks>
        /// 폴백 체인을 변경할 경우 AITVersion.LoadVersionInfoEditor()도 동기화할 것.
        /// </remarks>
        internal static PackageInfo FindSDKPackageInfo()
        {
            if (_cacheInitialized)
            {
                return _cachedInfo;
            }

            _cacheInitialized = true;
            _cachedInfo = PackageInfo.FindForAssetPath(PackageAssetPath)
                          ?? PackageInfo.FindForAssetPath(LegacyPackageAssetPath)
                          ?? PackageInfo.FindForAssembly(typeof(AITPackagePathResolver).Assembly);
            return _cachedInfo;
        }

        /// <summary>
        /// SDK 패키지의 파일시스템 경로를 반환합니다.
        /// PackageInfo를 찾지 못하면 null을 반환합니다.
        /// </summary>
        internal static string GetSDKResolvedPath()
        {
            return FindSDKPackageInfo()?.resolvedPath;
        }

        /// <summary>
        /// SDK 내부의 상대 경로에 대한 후보 경로 배열을 반환합니다.
        /// resolver가 성공하면 resolvedPath 기반 단일 경로를 반환하고,
        /// 실패 시 하드코딩된 경로들과 Assembly.Location 폴백을 반환합니다.
        /// </summary>
        /// <param name="relativePath">SDK 루트로부터의 상대 경로 (예: "WebGLTemplates/AITTemplate/Runtime")</param>
        /// <param name="assemblyAnchor">Assembly.Location 폴백에 사용할 타입 (resolver 실패 시에만 사용, null이면 생략)</param>
        internal static string[] GetCandidatePaths(string relativePath, System.Type assemblyAnchor = null)
        {
            string resolvedPath = GetSDKResolvedPath();
            if (resolvedPath != null)
            {
                return new string[] { Path.Combine(resolvedPath, relativePath) };
            }

            // 폴백: 하드코딩된 경로 사용
            var paths = new List<string>
            {
                Path.GetFullPath(Path.Combine(PackageAssetPath, relativePath)),
                Path.GetFullPath(Path.Combine(LegacyPackageAssetPath, relativePath))
            };

            if (assemblyAnchor != null)
            {
                string loc = assemblyAnchor.Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    paths.Add(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(loc)), relativePath));
                }
            }

            return paths.ToArray();
        }
    }
}
