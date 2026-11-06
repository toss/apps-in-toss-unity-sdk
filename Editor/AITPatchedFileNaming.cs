// -----------------------------------------------------------------------
// AITPatchedFileNaming.cs - 빌드 후 패치한 Unity 산출물(framework/loader)의 파일명 규약
//
// 배경: nameFilesAsHashes(기본 true)일 때 페이지 캐시와 warm manifest 는 해시 파일명을 캐시 키로 쓴다.
// 빌드 뒤에 framework/loader 를 패치하면서 이름을 그대로 두면 바이트만 바뀌어, SDK 버전이 달라져
// 패치 내용이 바뀐 재배포에서도 같은 이름의 낡은 캐시가 서빙된다. 그래서 패치한 파일은 이름에
// patch-set 버전 접미사를 붙여 rename 하고, 그 이름을 참조하는 곳(index.html, 로더 config)을 다시 쓴다.
//
// 규약: "<stem>.aitpN-<8hex>.<role 확장자들>"   N = PatchSetVersion(양의 정수), 8hex = 패치 입력의 짧은 해시
//   abc123.framework.js.br  ->  abc123.aitp1-1a2b3c4d.framework.js.br
//   abc123.loader.js        ->  abc123.aitp1-9f8e7d6c.loader.js
// 8hex 는 설정에 따라 패치 바이트가 달라지는 입력(강제 압축 여부·최소 길이·payload·적용 그룹)에서 뽑는다.
// 같은 SDK 버전이라도 설정이 바뀌면 이름이 달라져 낡은 캐시가 서빙되지 않는다.
// 해시 없는 옛 형식(".aitpN")도 파싱·제거는 그대로 된다(하위 호환).
// 접미사는 역할 확장자(.framework.js/.loader.js/.data/.wasm/.symbols.json) 앞에 끼워 넣는다.
// 그래서 AITBuildValidator.GetFilePatterns 의 "*.framework.js.br" 같은 패턴이 그대로 맞고,
// 압축 확장자(.br/.gz/.unityweb) 판별도 영향받지 않는다.
//
// 이 규약을 쓰는 패처(AITFrameworkPatcher / AITLoaderPatcher)는 page cache·warm manifest 산출보다 먼저,
// brotli 재압축 전에 실행돼야 한다(WebGLBuildCopier.ApplyBuildPatches 참조).
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AppsInToss.Editor
{
    internal static class AITPatchedFileNaming
    {
        /// <summary>
        /// 현재 patch-set 버전. 패처가 적용하는 패치 내용이 바뀌면(새 그룹 추가·앵커 수정 등) 올린다.
        /// 올리면 파일명이 바뀌어 낡은 캐시가 자연히 무효화된다.
        /// </summary>
        internal const int PatchSetVersion = 1;

        /// <summary>접미사 접두 문자열. ".aitp" + 버전 숫자.</summary>
        internal const string MarkerPrefix = ".aitp";

        // Unity WebGL 빌드 산출물의 역할 확장자. 접미사는 스템 뒤, 이 중 하나의 앞에 들어간다.
        private static readonly string[] RoleMarkers =
        {
            ".framework.js", ".loader.js", ".symbols.json", ".data", ".wasm",
        };

        // 이미 붙은 접미사 탐지: ".aitp" + 숫자 + 선택적 "-8hex", 뒤가 '.' 이거나 끝.
        private static readonly Regex MarkerRegex = new Regex(@"\.aitp(\d+)(?:-([0-9a-f]{8}))?(?=\.|$)", RegexOptions.CultureInvariant);

        /// <summary>
        /// 패치 입력 문자열들에서 안정적인 8자리 hex 해시(SHA1 앞 4바이트)를 만든다.
        /// 각 입력에 길이를 붙여 경계가 모호해지지 않게 하고, 입력 순서가 같으면 항상 같은 값이다.
        /// </summary>
        internal static string ComputeConfigHash(params string[] parts)
        {
            var sb = new StringBuilder();
            if (parts != null)
            {
                foreach (string part in parts)
                {
                    string value = part ?? string.Empty;
                    sb.Append(value.Length).Append(':').Append(value).Append('\n');
                }
            }

            using (SHA1 sha = SHA1.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(8);
                for (int i = 0; i < 4; i++)
                {
                    hex.Append(digest[i].ToString("x2"));
                }

                return hex.ToString();
            }
        }

        /// <summary>
        /// 원본 이름에 patch-set 접미사를 붙인 이름을 돌려준다. 디렉터리/URL 접두("Build/")는 보존한다.
        /// 이미 접미사가 있으면(어떤 버전이든) 지우고 <paramref name="version"/> 으로 다시 붙이므로 멱등이다.
        /// </summary>
        /// <param name="configHash">패치 입력 해시(<see cref="ComputeConfigHash"/>, 8자리 소문자 hex). null/빈 값이면 옛 형식 ".aitpN".</param>
        internal static string GetPatchedName(string fileName, int version = PatchSetVersion, string configHash = null)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("fileName 이 비어 있습니다.", nameof(fileName));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version), "patch-set 버전은 1 이상이어야 합니다.");

            if (!string.IsNullOrEmpty(configHash) && !Regex.IsMatch(configHash, "^[0-9a-f]{8}$"))
            {
                throw new ArgumentException("configHash 는 8자리 소문자 hex 여야 합니다: " + configHash, nameof(configHash));
            }

            SplitDirectory(fileName, out string prefix, out string name);
            name = MarkerRegex.Replace(name, string.Empty);
            if (name.Length == 0) throw new ArgumentException("파일명 부분이 비어 있습니다: " + fileName, nameof(fileName));

            int insertAt = FindInsertIndex(name);
            return prefix + name.Substring(0, insertAt) + MarkerPrefix + version
                + (string.IsNullOrEmpty(configHash) ? string.Empty : "-" + configHash)
                + name.Substring(insertAt);
        }

        /// <summary>patch-set 접미사가 붙어 있으면 지운 원본 이름을 돌려준다(없으면 그대로).</summary>
        internal static string GetOriginalName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;
            SplitDirectory(fileName, out string prefix, out string name);
            return prefix + MarkerRegex.Replace(name, string.Empty);
        }

        /// <summary>이름에 patch-set 접미사가 있는지, 있으면 그 버전을 돌려준다.</summary>
        internal static bool TryParsePatchVersion(string fileName, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(fileName)) return false;
            SplitDirectory(fileName, out _, out string name);
            Match m = MarkerRegex.Match(name);
            return m.Success && int.TryParse(m.Groups[1].Value, out version) && version > 0;
        }

        /// <summary>이름에 설정 해시(".aitpN-8hex")가 있으면 그 해시를 돌려준다. 옛 ".aitpN" 이면 false.</summary>
        internal static bool TryParseConfigHash(string fileName, out string configHash)
        {
            configHash = null;
            if (string.IsNullOrEmpty(fileName)) return false;
            SplitDirectory(fileName, out _, out string name);
            Match m = MarkerRegex.Match(name);
            if (!m.Success || !m.Groups[2].Success) return false;
            configHash = m.Groups[2].Value;
            return true;
        }

        internal static bool IsPatched(string fileName)
        {
            return TryParsePatchVersion(fileName, out _);
        }

        /// <summary>
        /// <paramref name="dir"/> 안의 <paramref name="fileName"/> 을 패치 이름으로 옮긴다(같은 이름이 있으면 덮어씀).
        /// 패처가 새 바이트를 원본 이름으로 쓴 뒤 호출하는 용도다.
        /// </summary>
        /// <param name="newName">옮긴 뒤 이름(실패하거나 이미 그 이름이면 fileName 그대로).</param>
        /// <returns>실제로 rename 했으면 true. 원본이 없거나 이미 같은 이름이면 false.</returns>
        internal static bool RenameInDirectory(string dir, string fileName, out string newName, int version = PatchSetVersion, string configHash = null)
        {
            newName = fileName;
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName)) return false;

            string target = GetPatchedName(fileName, version, configHash);
            if (string.Equals(target, fileName, StringComparison.Ordinal)) return false;

            string src = Path.Combine(dir, fileName);
            if (!File.Exists(src)) return false;

            string dest = Path.Combine(dir, target);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(src, dest);
            newName = target;
            return true;
        }

        /// <summary>
        /// 텍스트(index.html, 로더 config 등) 안의 옛 파일명 참조를 새 이름으로 바꾼다.
        /// 이름 경계를 지켜서(앞뒤가 파일명 문자가 아닐 때만) 다른 이름의 일부는 건드리지 않고,
        /// 긴 이름부터 치환해 한 이름이 다른 이름의 접두인 경우도 안전하다.
        /// </summary>
        /// <param name="renames">옛 이름 → 새 이름. 디렉터리 접두("Build/")는 텍스트에 있는 그대로 유지된다.</param>
        internal static string RewriteReferences(string text, IDictionary<string, string> renames)
        {
            if (string.IsNullOrEmpty(text) || renames == null || renames.Count == 0) return text;

            string result = text;
            foreach (KeyValuePair<string, string> kv in renames.OrderByDescending(p => p.Key.Length))
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Key == kv.Value) continue;

                // 앞: 파일명 문자(영숫자 _ - .)가 아니어야 함. 뒤: 파일명 문자가 아니고, '.'+영숫자(더 긴 이름)도 아니어야 함.
                var pattern = new Regex(
                    @"(?<![A-Za-z0-9_\-.])" + Regex.Escape(kv.Key) + @"(?![A-Za-z0-9_\-]|\.[A-Za-z0-9])",
                    RegexOptions.CultureInvariant);
                string replacement = kv.Value;
                result = pattern.Replace(result, _ => replacement);
            }
            return result;
        }

        /// <summary>파일을 읽어 <see cref="RewriteReferences"/> 를 적용하고, 바뀐 경우에만 다시 쓴다.</summary>
        /// <returns>내용이 바뀌어 썼으면 true.</returns>
        internal static bool RewriteReferencesInFile(string path, IDictionary<string, string> renames)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            string before = File.ReadAllText(path);
            string after = RewriteReferences(before, renames);
            if (string.Equals(before, after, StringComparison.Ordinal)) return false;
            File.WriteAllText(path, after);
            return true;
        }

        // 마지막 '/' 또는 '\' 까지를 접두로 분리한다.
        private static void SplitDirectory(string path, out string prefix, out string name)
        {
            int sep = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            prefix = sep >= 0 ? path.Substring(0, sep + 1) : string.Empty;
            name = sep >= 0 ? path.Substring(sep + 1) : path;
        }

        // 접미사를 끼울 위치: 역할 확장자 중 가장 뒤에 있는(마커 뒤가 '.' 또는 끝인) 것의 시작.
        // 없으면 첫 '.'(맨 앞 제외), 그것도 없으면 끝.
        private static int FindInsertIndex(string name)
        {
            int best = -1;
            foreach (string marker in RoleMarkers)
            {
                int from = name.Length - 1;
                while (from >= 0)
                {
                    int idx = name.LastIndexOf(marker, from, StringComparison.Ordinal);
                    if (idx < 0) break;
                    int end = idx + marker.Length;
                    if (end == name.Length || name[end] == '.')
                    {
                        if (idx > best) best = idx;
                        break;
                    }
                    from = idx - 1;
                }
            }
            if (best > 0) return best;

            int dot = name.Length > 1 ? name.IndexOf('.', 1) : -1;
            return dot > 0 ? dot : name.Length;
        }
    }
}
