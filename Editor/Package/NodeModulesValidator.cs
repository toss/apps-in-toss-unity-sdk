using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// 빌드 프로젝트의 node_modules 디렉토리 무결성 검증 및 정리.
    /// pnpm이 설치한 web-framework 버전이 package.json 선언 버전과 일치하는지 확인한다.
    /// 검증은 best-effort: 판별이 불가능한 모든 케이스(파일 없음, 파싱 실패, 예외 등)는
    /// true를 반환해 불필요한 재설치를 피한다. false는 "정리가 확실히 필요하다"고 판단될 때만 반환된다.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class NodeModulesValidator
    {
        /// <summary>
        /// node_modules 무결성 검증.
        /// package.json의 @apps-in-toss/web-framework 버전과 node_modules 내 설치 버전이 일치하는지 확인한다.
        /// pnpm의 node_modules/.pnpm/@apps-in-toss+web-framework@{version} 디렉토리 존재 여부로 판단.
        /// </summary>
        /// <param name="buildProjectPath">빌드 프로젝트 경로</param>
        /// <returns>true: 무결성 확인됨 또는 node_modules 없음, false: 버전 불일치로 정리 필요</returns>
        internal static bool ValidateIntegrity(string buildProjectPath)
        {
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                return true;
            }

            string packageJsonPath = Path.Combine(buildProjectPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                return true;
            }

            try
            {
                string packageJsonContent = File.ReadAllText(packageJsonPath);
                var packageJson = MiniJson.Deserialize(packageJsonContent) as Dictionary<string, object>;
                if (packageJson == null) return true;

                var dependencies = packageJson.ContainsKey("dependencies")
                    ? packageJson["dependencies"] as Dictionary<string, object>
                    : null;
                if (dependencies == null) return true;

                if (!dependencies.ContainsKey("@apps-in-toss/web-framework")) return true;

                string expectedVersion = dependencies["@apps-in-toss/web-framework"] as string;
                if (string.IsNullOrEmpty(expectedVersion)) return true;

                expectedVersion = expectedVersion.TrimStart('^', '~');

                string pnpmDir = Path.Combine(nodeModulesPath, ".pnpm");
                if (!Directory.Exists(pnpmDir))
                {
                    Debug.Log("[AIT] node_modules/.pnpm 디렉토리가 없습니다. node_modules를 정리합니다.");
                    return false;
                }

                string expectedDirPrefix = $"@apps-in-toss+web-framework@{expectedVersion}";
                string[] matchingDirs = Directory.GetDirectories(pnpmDir, $"{expectedDirPrefix}*");

                if (matchingDirs.Length > 0)
                {
                    Debug.Log($"[AIT] ✓ node_modules 무결성 확인: web-framework@{expectedVersion}");
                    return true;
                }

                string[] installedDirs = Directory.GetDirectories(pnpmDir, "@apps-in-toss+web-framework@*");
                if (installedDirs.Length > 0)
                {
                    string installedDirName = Path.GetFileName(installedDirs[0]);
                    Debug.Log($"[AIT] web-framework 버전 불일치 감지!");
                    Debug.Log($"[AIT]   기대 버전: {expectedVersion}");
                    Debug.Log($"[AIT]   설치된 버전: {installedDirName}");
                    Debug.Log($"[AIT]   node_modules를 정리하고 재설치합니다.");
                }
                else
                {
                    Debug.Log($"[AIT] web-framework가 node_modules에 없습니다. node_modules를 정리합니다.");
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] node_modules 무결성 검증 중 오류 (무시됨): {e}");
                return true;
            }
        }

        /// <summary>
        /// node_modules 및 레거시 .npm-cache 디렉토리를 삭제한다.
        /// </summary>
        internal static void CleanNodeModules(string buildProjectPath)
        {
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
            string npmCachePath = Path.Combine(buildProjectPath, ".npm-cache");

            if (Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[AIT] node_modules 삭제 중...");
                if (AITFileUtils.DeleteDirectory(nodeModulesPath))
                {
                    Debug.Log("[AIT] ✓ node_modules 삭제 완료");
                }
            }

            if (Directory.Exists(npmCachePath))
            {
                Debug.Log("[AIT] 레거시 .npm-cache 삭제 중...");
                if (AITFileUtils.DeleteDirectory(npmCachePath))
                {
                    Debug.Log("[AIT] ✓ 레거시 .npm-cache 삭제 완료");
                }
            }
        }
    }
}
