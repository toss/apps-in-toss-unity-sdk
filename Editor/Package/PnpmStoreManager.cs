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
        /// 빌드 프로젝트 위치를 고려한 store 경로.
        /// pnpm의 하드링크는 같은 볼륨 안에서만 동작한다 — Windows에서 프로젝트가
        /// 홈 드라이브(보통 C:)와 다른 드라이브에 있으면 store→node_modules가 매번
        /// 파일 복사로 폴백되어 재설치가 크게 느려진다. 이 경우 pnpm 자체의 관례처럼
        /// 프로젝트 드라이브 루트에 드라이브별 store(예: D:\.ait-unity-sdk\pnpm-store)를
        /// 사용해 하드링크를 복원한다. 드라이브 루트에 쓸 수 없으면 홈 store로 폴백
        /// (복사라서 느리지만 동작은 함).
        /// </summary>
        internal static string GetSharedPnpmStorePath(string buildProjectPath)
        {
            string homeStore = GetSharedPnpmStorePath();
            string resolved = ResolveStorePathForProject(homeStore, buildProjectPath);
            if (resolved == homeStore)
            {
                return homeStore;
            }

            try
            {
                Directory.CreateDirectory(resolved);
                // 디렉토리가 이미 있어도 쓰기 권한이 없을 수 있어 실제 쓰기로 검증
                string probe = Path.Combine(resolved, ".ait-write-probe-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return resolved;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"[AIT] 프로젝트 드라이브 pnpm store({resolved})에 쓸 수 없어 홈 store로 폴백합니다. " +
                    $"크로스 드라이브 하드링크가 불가능해 설치가 느려질 수 있습니다: {e.Message}");
                return homeStore;
            }
        }

        /// <summary>
        /// 순수 함수: 홈 store와 프로젝트가 다른 Windows 드라이브에 있으면
        /// 프로젝트 드라이브 루트의 store 경로를, 그 외에는 홈 store를 반환.
        /// 드라이브 문자 루트(C:\ 등)만 대상 — UNC/유닉스 경로는 항상 홈 store.
        /// </summary>
        internal static string ResolveStorePathForProject(string homeStorePath, string buildProjectPath)
        {
            string homeRoot = GetWindowsDriveRoot(homeStorePath);
            string projectRoot = GetWindowsDriveRoot(buildProjectPath);
            if (homeRoot == null || projectRoot == null || homeRoot == projectRoot)
            {
                return homeStorePath;
            }
            return projectRoot + @".ait-unity-sdk\pnpm-store";
        }

        /// <summary>
        /// 경로가 Windows 드라이브 문자로 시작하면 정규화된 루트("D:\")를, 아니면 null 반환.
        /// Path.GetPathRoot는 실행 OS의 규칙을 따르므로 플랫폼 무관 판정을 위해 직접 파싱.
        /// </summary>
        internal static string GetWindowsDriveRoot(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 2 || path[1] != ':')
            {
                return null;
            }
            char drive = path[0];
            bool isAsciiLetter = (drive >= 'A' && drive <= 'Z') || (drive >= 'a' && drive <= 'z');
            return isAsciiLetter ? char.ToUpperInvariant(drive) + @":\" : null;
        }

        /// <summary>
        /// pnpm install 재시도 정책 (args, label, cleanFirst, deleteLockfileFirst).
        /// 1단계: frozen-lockfile (lockfile 정합 시 통과)
        /// 2단계: --no-frozen-lockfile (lockfile 갱신 허용)
        /// 3단계: lockfile 삭제 후 --no-frozen-lockfile (손상된 lockfile 폐기 후 재생성)
        /// 4단계: node_modules clean + --no-frozen-lockfile (최종 폴백)
        /// </summary>
        internal static readonly IReadOnlyList<(string args, string label, bool cleanFirst, bool deleteLockfileFirst)> InstallStages =
            new (string args, string label, bool cleanFirst, bool deleteLockfileFirst)[]
            {
                ("install --frozen-lockfile",    "frozen-lockfile",         false, false),
                ("install --no-frozen-lockfile", "lockfile 갱신",            false, false),
                ("install --no-frozen-lockfile", "lockfile 폐기 후 재시도",  false, true),
                ("install --no-frozen-lockfile", "clean 재시도",            true,  false),
            };
    }
}
