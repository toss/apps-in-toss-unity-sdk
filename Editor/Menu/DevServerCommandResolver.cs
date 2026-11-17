using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// Dev Server가 실행할 dev 서버 커맨드를 해석합니다 (web-framework 3.x — vite 단독).
    /// 반환된 커맨드는 <see cref="Resolve"/>의 out directExecutablePath가 null이면 pnpm 인자
    /// (pnpm exec 경유), non-null이면 그 경로(node)로 직접 실행할 인자입니다 (5b).
    ///
    /// 3.x에는 granite bin과 @granite-js/* 의존성이 없고 ait CLI에도 dev 서브커맨드가 없으므로
    /// vite 패키지의 bin 파일을 node로 직접 실행한다 (<see cref="GraniteBuildRunner"/>의 빌드가
    /// vite build를 직접 호출하는 것과 대칭).
    /// vite bin 직접 실행은 pnpm CLI 기동 자체를 생략해 매 시작마다의 기동 비용을 줄인다 —
    /// 해석 실패 시(package.json 구조 변경 등) 항상 기존 'pnpm exec -- vite' 명령으로 폴백한다.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class DevServerCommandResolver
    {
        /// <summary>vite 패키지의 (ait-build 기준) 상대 경로</summary>
        internal const string VitePackagePath = "node_modules/vite";

        /// <summary>
        /// dev 서버 기동용 커맨드를 해석합니다. 서버는 vite 포트만 열므로 포트 감지/중지도 vite 포트를 기준으로 합니다.
        /// directExecutablePath가 non-null이면 반환된 커맨드는 pnpm이 아니라 그 경로(node)로 직접
        /// 실행해야 합니다 (5b — pnpm exec 기동 비용 제거). null이면 기존처럼 pnpm 경유로 실행합니다.
        /// </summary>
        internal static string Resolve(string buildProjectPath, int vitePort, out string directExecutablePath)
        {
            return Resolve(buildProjectPath, vitePort, ResolveNodeExecutablePath(), out directExecutablePath);
        }

        /// <summary>
        /// node 실행 파일 경로를 주입받는 내부 오버로드 (테스트에서 임의의 node 경로를 넣기 위함).
        /// </summary>
        internal static string Resolve(string buildProjectPath, int vitePort, string nodeExecutablePath, out string directExecutablePath)
        {
            string legacyViteCommand = $"exec -- vite --host --port {vitePort}";

            bool nodeAvailable = !string.IsNullOrEmpty(nodeExecutablePath) && File.Exists(nodeExecutablePath);
            string viteBinRelPath = nodeAvailable ? ResolveViteBinRelPath(buildProjectPath) : null;
            if (viteBinRelPath == null)
            {
                Debug.Log("[AIT] vite bin 직접 실행 경로 해석 실패 — 기존 'pnpm exec -- vite' 명령으로 폴백합니다.");
                directExecutablePath = null;
                return legacyViteCommand;
            }

            directExecutablePath = nodeExecutablePath;
            return $"{viteBinRelPath} --host --port {vitePort}";
        }

        /// <summary>
        /// 내장 Node.js 실행 파일의 절대 경로. 캐시된 embedded node bin 경로를 사용하며,
        /// 아직 탐지되지 않았다면(다운로드 전) null을 반환합니다 — 호출부는 pnpm exec 경로로 폴백합니다.
        /// </summary>
        private static string ResolveNodeExecutablePath()
        {
            string nodeBinDir = AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            if (string.IsNullOrEmpty(nodeBinDir)) return null;
            return Path.Combine(nodeBinDir, AITPlatformHelper.GetExecutableName("node"));
        }

        /// <summary>
        /// vite 패키지의 package.json "bin" 필드에서 진입 스크립트의 (ait-build 기준) 상대 경로를 해석합니다.
        /// "bin"이 문자열("bin": "bin/vite.js")과 객체("bin": {"vite": "bin/vite.js"}) 두 형태를 모두 처리합니다.
        /// 해석 불가·검증 실패 시 null (호출부가 기존 'pnpm exec -- vite' 명령으로 폴백).
        /// </summary>
        internal static string ResolveViteBinRelPath(string buildProjectPath)
        {
            try
            {
                string pkgDir = Path.Combine(buildProjectPath, VitePackagePath);
                string pkgJsonPath = Path.Combine(pkgDir, "package.json");
                if (!File.Exists(pkgJsonPath)) return null;

                var pkg = MiniJson.Deserialize(File.ReadAllText(pkgJsonPath)) as Dictionary<string, object>;
                if (pkg == null || !pkg.ContainsKey("bin")) return null;

                string binFile = ExtractViteBinEntry(pkg["bin"]);
                if (string.IsNullOrEmpty(binFile)) return null;
                if (binFile.StartsWith("./")) binFile = binFile.Substring(2);

                // 셸 명령 문자열에 인용 없이 삽입되므로 허용 문자만 통과시킨다 (allowlist)
                if (!IsSafeBinRelPath(binFile))
                {
                    return null;
                }

                if (!File.Exists(Path.Combine(pkgDir, binFile))) return null;

                return VitePackagePath + "/" + binFile;
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] vite bin 경로 해석 중 오류 (기존 명령으로 폴백): {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// package.json "bin" 필드값에서 vite 진입 스크립트 경로를 추출합니다.
        /// 문자열 형태는 그대로 사용하고, 객체 형태는 "vite" 키를 우선 조회하되 엔트리가
        /// 하나뿐이면 키 이름과 무관하게 그 값을 사용합니다 (패키지명이 다른 스코프 배포 대비).
        /// </summary>
        private static string ExtractViteBinEntry(object binValue)
        {
            if (binValue is string binStr) return binStr;

            if (binValue is Dictionary<string, object> binMap)
            {
                if (binMap.TryGetValue("vite", out var viteEntry)) return viteEntry as string;
                if (binMap.Count == 1)
                {
                    foreach (var kv in binMap) return kv.Value as string;
                }
            }

            return null;
        }

        /// <summary>bin 상대 경로가 셸 명령에 무인용 삽입해도 안전한 문자로만 구성됐는지 검사합니다.</summary>
        private static bool IsSafeBinRelPath(string binFile)
        {
            if (string.IsNullOrEmpty(binFile) || binFile.Contains("..") || Path.IsPathRooted(binFile))
            {
                return false;
            }
            foreach (char c in binFile)
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                    || c == '.' || c == '_' || c == '-' || c == '/';
                if (!allowed)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
