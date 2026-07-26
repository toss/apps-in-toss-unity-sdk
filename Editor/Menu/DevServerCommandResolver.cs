using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AppsInToss.Editor.Package;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// Dev/Production Server가 실행할 dev 서버 커맨드(pnpm 인자)를 web-framework 버전에 맞춰 해석합니다.
    ///
    /// "granite"라는 bin 이름을 pnpm exec로 resolve하면 안 되는 이유:
    /// - 2.x: 전이 의존성 @granite-js/react-native도 같은 이름의 granite bin을 선언하므로,
    ///   설치 그래프에 따라 node_modules/.bin/granite가 RN Metro CLI로 링크될 수 있다.
    ///   그 CLI는 web용 granite.config.ts의 pluginHooks를 요구해 즉시 종료한다 (bin collision).
    /// - 3.x: granite bin과 @granite-js/* 의존성이 트리에서 제거됐고, ait CLI에도 dev
    ///   서브커맨드가 없다.
    /// 따라서 2.x는 web-framework 패키지 자신의 granite bin 파일을 node로 직접 실행하고
    /// (bin 이름 resolve 우회), 3.x는 vite dev 서버를 직접 실행한다
    /// (<see cref="GraniteBuildRunner"/>의 3.x 빌드가 vite build를 직접 호출하는 것과 대칭).
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class DevServerCommandResolver
    {
        /// <summary>ait-build 기준 web-framework 패키지 상대 경로 (node는 Windows에서도 / 구분자 허용)</summary>
        internal const string WebFrameworkPackagePath = "node_modules/@apps-in-toss/web-framework";

        /// <summary>bin 파일 해석 실패 시에만 쓰는 기존 명령 (bin 이름 resolve — collision에 노출)</summary>
        internal const string LegacyGraniteCommand = "exec -- granite dev";

        /// <summary>
        /// dev 서버가 vite 단독으로 동작하는지 (web-framework 3.x — granite/Metro 레이어 없음).
        /// 이 경우 granite 포트는 전혀 사용되지 않으므로, 포트 해석 단계에서 granite 포트
        /// 가용성 검사를 건너뛰어야 합니다 (점유 시 vite 단독 기동까지 차단되는 것을 방지).
        /// </summary>
        internal static bool IsViteOnly(string buildProjectPath)
        {
            return GraniteBuildRunner.GetWebFrameworkMajor(buildProjectPath) >= 3;
        }

        /// <summary>
        /// dev 서버 기동용 pnpm 인자 문자열을 반환합니다.
        /// viteOnly가 true면 서버는 vite 포트만 열므로 포트 감지/중지도 vite 포트를 기준으로 해야 합니다.
        /// </summary>
        internal static string Resolve(string buildProjectPath, int vitePort, out bool viteOnly)
        {
            if (IsViteOnly(buildProjectPath))
            {
                viteOnly = true;
                return $"exec -- vite --host --port {vitePort}";
            }

            viteOnly = false;
            string binRelPath = ResolveGraniteBinRelPath(buildProjectPath);
            if (binRelPath == null)
            {
                Debug.LogWarning("[AIT] web-framework granite bin 경로 해석 실패 — 기존 granite dev 명령으로 폴백합니다.");
                return LegacyGraniteCommand;
            }
            return $"exec -- node {binRelPath} dev";
        }

        /// <summary>
        /// web-framework package.json의 bin 맵에서 granite 진입 파일의 (ait-build 기준) 상대 경로를 해석합니다.
        /// 해석 불가·검증 실패 시 null (호출부가 기존 명령으로 폴백).
        /// </summary>
        internal static string ResolveGraniteBinRelPath(string buildProjectPath)
        {
            try
            {
                string pkgDir = Path.Combine(buildProjectPath, WebFrameworkPackagePath);
                string pkgJsonPath = Path.Combine(pkgDir, "package.json");
                if (!File.Exists(pkgJsonPath)) return null;

                var pkg = MiniJson.Deserialize(File.ReadAllText(pkgJsonPath)) as Dictionary<string, object>;
                var bin = pkg != null && pkg.ContainsKey("bin") ? pkg["bin"] as Dictionary<string, object> : null;
                if (bin == null || !bin.ContainsKey("granite")) return null;

                string binFile = bin["granite"] as string;
                if (string.IsNullOrEmpty(binFile)) return null;
                if (binFile.StartsWith("./")) binFile = binFile.Substring(2);

                // 셸 명령 문자열에 인용 없이 삽입되므로 허용 문자만 통과시킨다 (allowlist).
                // 영숫자·'.'·'_'·'-'·'/' 외의 문자(공백, 따옴표, ;&|<> 등 bash/cmd 메타문자 일체)가
                // 하나라도 있으면 거부. 추가로 경로 탈출('..')과 절대 경로도 차단.
                if (!IsSafeBinRelPath(binFile))
                {
                    return null;
                }

                if (!File.Exists(Path.Combine(pkgDir, binFile))) return null;

                return WebFrameworkPackagePath + "/" + binFile;
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] granite bin 경로 해석 중 오류 (기존 명령으로 폴백): {e.Message}");
                return null;
            }
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
