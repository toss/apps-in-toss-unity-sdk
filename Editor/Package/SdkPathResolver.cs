using System.IO;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// SDK 내부 BuildConfig 템플릿 및 Runtime 폴더 경로 해석 + 캐시.
    /// 빌드 중 반복 검색을 방지하기 위해 내부 static 필드로 캐싱한다.
    /// </summary>
    internal static class SdkPathResolver
    {
        private static string _cachedSdkBuildConfigPath = null;
        private static string _cachedSdkRuntimePath = null;

        /// <summary>
        /// 캐시된 SDK 경로를 초기화합니다.
        /// Domain reload 시 static 필드가 자동으로 null이 되므로 별도 호출이 필수는 아니지만,
        /// 외부에서 명시적으로 초기화해야 할 때 사용합니다.
        /// </summary>
        internal static void ClearPathCache()
        {
            _cachedSdkBuildConfigPath = null;
            _cachedSdkRuntimePath = null;
        }

        /// <summary>
        /// SDK BuildConfig 템플릿 경로를 반환합니다. (캐싱 사용)
        /// </summary>
        internal static string GetSdkBuildConfigPath()
        {
            if (_cachedSdkBuildConfigPath != null && Directory.Exists(_cachedSdkBuildConfigPath))
            {
                return _cachedSdkBuildConfigPath;
            }

            if (AITPackagePathResolver.TryResolveDirectory(
                "WebGLTemplates/AITTemplate/BuildConfig~", out string found, typeof(AITConvertCore)))
            {
                _cachedSdkBuildConfigPath = found;
                Debug.Log($"[AIT] SDK BuildConfig 경로 캐싱: {found}");
                return found;
            }

            return null;
        }

        /// <summary>
        /// SDK 템플릿의 Runtime 폴더 경로를 반환합니다. (캐싱 사용)
        /// webgl/ 폴더에 Runtime이 없을 경우 폴백으로 사용됩니다.
        /// </summary>
        internal static string FindSdkRuntimePath()
        {
            if (_cachedSdkRuntimePath != null && Directory.Exists(_cachedSdkRuntimePath))
            {
                return _cachedSdkRuntimePath;
            }

            if (AITPackagePathResolver.TryResolveDirectory(
                "WebGLTemplates/AITTemplate/Runtime", out string found, typeof(AITConvertCore)))
            {
                _cachedSdkRuntimePath = found;
                Debug.Log($"[AIT] SDK Runtime 경로 캐싱: {found}");
                return found;
            }

            return null;
        }
    }
}
