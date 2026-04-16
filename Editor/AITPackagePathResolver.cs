using UnityEditor.PackageManager;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 패키지 경로를 안정적으로 찾기 위한 유틸리티.
    /// FindForAssetPath가 실패할 수 있는 Git UPM 패키지 환경에서 폴백을 제공합니다.
    /// </summary>
    internal static class AITPackagePathResolver
    {
        private const string PackageAssetPath = "Packages/im.toss.apps-in-toss-unity-sdk";

        /// <summary>
        /// SDK의 PackageInfo를 찾습니다.
        /// FindForAssetPath 실패 시 FindForAssembly를 폴백으로 사용합니다.
        /// </summary>
        internal static PackageInfo FindSDKPackageInfo()
        {
            var info = PackageInfo.FindForAssetPath(PackageAssetPath);
            if (info != null)
            {
                return info;
            }

            // 폴백: 어셈블리 기반 탐색 (Git UPM 패키지에서 경로가 다를 수 있음)
            return PackageInfo.FindForAssembly(typeof(AITPackagePathResolver).Assembly);
        }

        /// <summary>
        /// SDK 패키지의 파일시스템 경로를 반환합니다.
        /// PackageInfo를 찾지 못하면 null을 반환합니다.
        /// </summary>
        internal static string GetSDKResolvedPath()
        {
            return FindSDKPackageInfo()?.resolvedPath;
        }
    }
}
