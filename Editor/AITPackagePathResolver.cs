using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        private static PackageInfo _cachedInfo;
        private static bool _cacheInitialized;

        /// <summary>
        /// SDK의 PackageInfo를 찾습니다.
        /// FindForAssetPath 실패 시 FindForAssembly를 폴백으로 사용합니다.
        /// 성공한 결과만 도메인 리로드까지 캐싱됩니다.
        /// </summary>
        /// <remarks>
        /// 폴백 체인을 변경할 경우 AITVersion.LoadVersionInfoEditor()도 동기화할 것.
        /// 패키지 ID 상수는 AITVersion.PackageAssetPath / LegacyPackageAssetPath를 참조.
        /// </remarks>
        internal static PackageInfo FindSDKPackageInfo()
        {
            if (_cacheInitialized)
            {
                return _cachedInfo;
            }

            var info = PackageInfo.FindForAssetPath(AITVersion.PackageAssetPath)
                       ?? PackageInfo.FindForAssetPath(AITVersion.LegacyPackageAssetPath)
                       ?? PackageInfo.FindForAssembly(typeof(AITPackagePathResolver).Assembly);

            if (info != null)
            {
                _cachedInfo = info;
                _cacheInitialized = true;
            }
            else
            {
                Debug.LogWarning("[AIT] SDK 패키지를 찾을 수 없습니다. 패키지 설치 상태를 확인하세요.");
            }

            return info;
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
        /// 실패 시 프로젝트 루트 기준 경로들과 Assembly.Location 폴백을 반환합니다.
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

            // 폴백: 프로젝트 루트 기준 경로 사용
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var paths = new List<string>
            {
                Path.Combine(projectRoot, AITVersion.PackageAssetPath, relativePath),
                Path.Combine(projectRoot, AITVersion.LegacyPackageAssetPath, relativePath)
            };

            if (assemblyAnchor != null)
            {
                string loc = assemblyAnchor.Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    // Assembly DLL은 {package-root}/Editor/ 또는 유사 하위 폴더에 위치한다고 가정
                    paths.Add(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(loc)), relativePath));
                }
            }

            return paths.ToArray();
        }
    }
}
