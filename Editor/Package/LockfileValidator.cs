using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// 사용자 프로젝트 BuildConfig~ 의 package.json과 pnpm-lock.yaml 사이
    /// dep specifier 정합성을 검증한다. mismatch가 있으면 SDK 출하 lockfile로
    /// 폴백할 수 있도록 호출자에게 정보를 제공한다.
    ///
    /// 외부 YAML 라이브러리에 의존하지 않기 위해 lockfile은 정규식 기반으로
    /// 'importers."."' 섹션의 specifier 값만 추출한다. lockfileVersion이
    /// 검증되지 않은 형식이면 false를 반환하여 안전 폴백을 유도한다.
    /// </summary>
    internal static class LockfileValidator
    {
        // 지원하는 lockfileVersion. pnpm 9, 10이 동일한 v9.0 형식을 사용.
        // 미래 메이저 변경 시 의도적으로 false를 반환해 SDK 폴백 경로로 보낸다.
        private static readonly HashSet<string> SupportedLockfileVersions = new HashSet<string> { "9.0", "9" };

        /// <summary>
        /// package.json의 dependencies/devDependencies 모든 항목의 specifier가
        /// lockfile importers."."의 specifier와 정확히 일치하면 true.
        /// 그렇지 않으면 false 와 함께 mismatchSummary 에 사람이 읽을 수 있는
        /// 한 줄 요약을 채운다.
        /// </summary>
        internal static bool IsLockfileInSync(string packageJsonPath, string lockfilePath, out string mismatchSummary)
        {
            mismatchSummary = "";

            if (!File.Exists(packageJsonPath))
            {
                mismatchSummary = $"package.json 없음: {packageJsonPath}";
                return false;
            }
            if (!File.Exists(lockfilePath))
            {
                mismatchSummary = $"pnpm-lock.yaml 없음: {lockfilePath}";
                return false;
            }

            // package.json 파싱 (MiniJson 재사용)
            var manifest = ParseManifestSpecifiers(packageJsonPath);
            if (manifest == null)
            {
                mismatchSummary = "package.json 파싱 실패";
                return false;
            }

            // lockfile 파싱 (lockfileVersion 가드 포함)
            string lockfileText = File.ReadAllText(lockfilePath);
            string version = ParseLockfileVersion(lockfileText);
            if (version == null || !SupportedLockfileVersions.Contains(version))
            {
                mismatchSummary = $"지원하지 않는 lockfileVersion: '{version}'";
                return false;
            }

            var lockfileSpecs = ParseLockfileImporterSpecifiers(lockfileText);

            // 정합성 비교: 양방향 검증 (manifest의 모든 dep이 lockfile에 있고 specifier 일치)
            var diffs = new List<string>();
            foreach (var kvp in manifest)
            {
                if (!lockfileSpecs.TryGetValue(kvp.Key, out string lockSpec))
                {
                    diffs.Add($"{kvp.Key} (manifest={kvp.Value}, lockfile=없음)");
                }
                else if (lockSpec != kvp.Value)
                {
                    diffs.Add($"{kvp.Key} (lockfile={lockSpec}, manifest={kvp.Value})");
                }
            }
            // lockfile에만 있는 dep은 stale 잔재로 간주
            foreach (var kvp in lockfileSpecs)
            {
                if (!manifest.ContainsKey(kvp.Key))
                {
                    diffs.Add($"{kvp.Key} (lockfile={kvp.Value}, manifest=없음)");
                }
            }

            if (diffs.Count == 0) return true;

            mismatchSummary = string.Join(", ", diffs);
            return false;
        }

        private static Dictionary<string, string> ParseManifestSpecifiers(string packageJsonPath)
        {
            try
            {
                string json = File.ReadAllText(packageJsonPath);
                var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
                if (root == null) return null;

                var result = new Dictionary<string, string>();
                CollectStringMap(root, "dependencies", result);
                CollectStringMap(root, "devDependencies", result);
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static void CollectStringMap(Dictionary<string, object> root, string key, Dictionary<string, string> dest)
        {
            if (!root.ContainsKey(key)) return;
            if (!(root[key] is Dictionary<string, object> map)) return;
            foreach (var kvp in map)
            {
                if (kvp.Value is string s) dest[kvp.Key] = s;
            }
        }

        // 'lockfileVersion: '9.0'' 또는 'lockfileVersion: 9.0' 첫 줄에서 값 추출.
        private static readonly Regex LockfileVersionRegex =
            new Regex(@"^lockfileVersion:\s*'?([0-9.]+)'?\s*$", RegexOptions.Multiline);

        private static string ParseLockfileVersion(string lockfileText)
        {
            var m = LockfileVersionRegex.Match(lockfileText);
            return m.Success ? m.Groups[1].Value : null;
        }

        // importers."." 블록의 dependencies / devDependencies 섹션에서
        // <name>: \n  specifier: <value> 패턴을 추출한다.
        // pnpm-lock.yaml v9.0 형식에 맞춤.
        internal static Dictionary<string, string> ParseLockfileImporterSpecifiers(string lockfileText)
        {
            var result = new Dictionary<string, string>();

            // 1) importers 섹션 진입 (단일 importer 'root' = '.:').
            var importersMatch = Regex.Match(lockfileText, @"^importers:\s*$", RegexOptions.Multiline);
            if (!importersMatch.Success) return result;

            // 2) importers 섹션 내 '.:' 블록 추출.
            //    pnpm v9.0은 '.:' 다음 들여쓰기 4칸으로 dependencies/devDependencies가 옴.
            //    다음 importer가 없으면 packages: 섹션까지가 끝.
            int importersStart = importersMatch.Index + importersMatch.Length;
            string afterImporters = lockfileText.Substring(importersStart);

            var rootMatch = Regex.Match(afterImporters, @"^\s*\.:\s*$", RegexOptions.Multiline);
            if (!rootMatch.Success) return result;

            int rootStart = rootMatch.Index + rootMatch.Length;
            string afterRoot = afterImporters.Substring(rootStart);

            // 3) 다음 top-level 섹션('packages:', 'snapshots:' 등) 또는 다른 importer 시작 전까지
            //    동일 들여쓰기 레벨('  ' 다음 들여쓰기 안 들어간 라인)에서 끝남.
            var endMatch = Regex.Match(afterRoot, @"^[a-zA-Z]", RegexOptions.Multiline);
            string rootBlock = endMatch.Success ? afterRoot.Substring(0, endMatch.Index) : afterRoot;

            // 4) dependencies / devDependencies 안에서 <name>: + specifier: <value> 추출.
            //    name 형식: 인용된 '@scope/pkg' 또는 일반 식별자.
            var specifierRegex = new Regex(
                @"^\s{6}'?(?<name>[^:\s']+)'?:\s*\n\s+specifier:\s*(?<spec>.+?)\s*$",
                RegexOptions.Multiline);

            foreach (Match m in specifierRegex.Matches(rootBlock))
            {
                string name = m.Groups["name"].Value;
                string spec = m.Groups["spec"].Value.Trim().Trim('\'', '"');
                result[name] = spec;
            }

            return result;
        }
    }
}
