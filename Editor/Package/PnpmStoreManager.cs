using System;
using System.Collections.Generic;
using System.IO;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// pnpm 공유 store 관리 (경로 + install 재시도 정책).
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class PnpmStoreManager
    {
        /// <summary>
        /// 머신 공유 pnpm content-addressable store 경로.
        /// 여러 프로젝트/러너가 같은 store를 공유해 중복 다운로드를 방지한다 (하드링크 사용).
        /// macOS/Linux: ~/.ait-unity-sdk/pnpm-store/
        /// Windows: %LOCALAPPDATA%\ait-unity-sdk\pnpm-store\
        /// </summary>
        internal static string GetSharedPnpmStorePath()
        {
            string basePath;
            #if UNITY_EDITOR_WIN
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            #else
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            #endif
            return Path.Combine(basePath, ".ait-unity-sdk", "pnpm-store");
        }

        /// <summary>
        /// pnpm install 재시도 정책 (args, label, cleanFirst).
        /// 1단계 frozen → 2단계 lockfile 갱신 → 3단계 clean 재시도.
        /// </summary>
        internal static readonly IReadOnlyList<(string args, string label, bool cleanFirst)> InstallStages =
            new (string args, string label, bool cleanFirst)[]
            {
                ("install --frozen-lockfile", "frozen-lockfile", false),
                ("install --no-frozen-lockfile", "lockfile 갱신", false),
                ("install --no-frozen-lockfile", "clean 재시도", true),
            };
    }
}
