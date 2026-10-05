// -----------------------------------------------------------------------
// AITLoaderPatcher.cs - 빌드 후 Unity loader(*.loader.js) 텍스트 패치 (소비한 data 버퍼 해제용 훅)
//
// 호출 지점: WebGLBuildCopier.ApplyBuildPatches (AITFrameworkPatcher 다음).
//
// === 무엇을 패치하나 (releaseConsumedData 가 켜졌을 때만) ===
//  data-release  로더가 .data 본문을 받는 버퍼 할당 자리와, 파일 루프의 FS_createDataFile 호출 자리에
//                Runtime/ait-datarelease.js 의 훅(window.__AIT_DATAREL.alloc / .created)을 건다.
//                훅은 .data 를 크기 조절 가능 ArrayBuffer 로 받게 하고, IL2CPP 가 한 번 읽고 끝나는 global-metadata.dat 를
//                첫 프레임 뒤에 해제한다(상세는 ait-datarelease.js 머리 주석).
//    alloc    `,u=new Uint8Array(d),c=[],`  →  `,u=(window.__AIT_DATAREL&&window.__AIT_DATAREL.alloc(d,<resp>))||new Uint8Array(d),c=[],`
//    created  `g.FS_createDataFile(p,null,r.subarray(a,a+b),!0,!0,!0)`
//               →  `(window.__AIT_DATAREL&&window.__AIT_DATAREL.created||function(n){return n})(<원문 호출>,p)`
//             인자는 한 번만 평가되고 반환값은 그대로다. 변수 이름은 Unity 버전마다 달라서(2021.3 / 6000.0 / 6000.3 모두 확인) 정규식으로 잡는다.
//
// === 안전 계약 (AITFrameworkPatcher 와 같다) ===
//  - 두 편집은 한 그룹이다. 각 앵커가 정확히 1회 일치할 때만 둘 다 적용하고, 아니면 로더를 그대로 둔다(경고만 남긴다).
//  - 멱등: 이미 훅 이름이 있으면 아무것도 하지 않는다.
//  - 결과가 `node --check` 를 통과할 때만 채택한다. Node 가 없으면 패치하지 않는다. 어떤 실패도 예외를 밖으로 던지지 않는다(fail-open).
//  - releaseConsumedData 가 꺼져 있거나(자동 = 꺼짐), exactDataBody 가 꺼져 있으면(.data 크기가 정확하지 않으면 훅이 아무것도 못 한다) 건드리지 않는다.
//  - Decompression Fallback(.unityweb)은 호출부가 거르고, 여기서도 .unityweb 가 있으면 건너뛴다.
//  - 패치한 파일은 AITPatchedFileNaming 의 ".aitpN-<hash>" 이름으로 옮기고 옛 이름 → 새 이름을 renames 에 기록한다.
//  - Unity 원본 소스 텍스트를 저장소에 두지 않는다(공개 저장소). 앵커는 매칭에 필요한 짧은 패턴만 쓴다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AppsInToss.Editor.Package;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    internal static class AITLoaderPatcher
    {
        /// <summary>패치 그룹 이름(로그·테스트에서 쓴다).</summary>
        internal const string GroupDataRelease = "data-release";

        /// <summary>패치 마커 겸 런타임 훅 전역 이름. 이 문자열이 이미 있으면 재패치하지 않는다.</summary>
        internal const string HookGlobal = "window.__AIT_DATAREL";

        // 패치 내용(앵커·치환 모양)이 바뀌면 올린다. 파일명 해시 입력이라 낡은 캐시가 서빙되지 않는다.
        internal const string PatchRevision = "data-release/1";

        private const int NodeTimeoutMs = 60000;

        // readBodyWithProgress=function(resp,onProgress,streaming){ ... — 첫 인자가 Response 다.
        private static readonly Regex ReadBodyRegex = new Regex(
            @"\breadBodyWithProgress=function\((\w+),\w+,\w+\)\{", RegexOptions.CultureInvariant);

        // 본문 버퍼 할당: ,u=new Uint8Array(d),c=[],  (readBodyWithProgress 함수 안, 시작에서 이 거리 안에 정확히 1회)
        private static readonly Regex AllocRegex = new Regex(
            @",(\w+)=new Uint8Array\((\w+)\),(\w+)=\[\],", RegexOptions.CultureInvariant);

        private const int AllocSearchWindow = 2500;

        // 파일 생성: g.FS_createDataFile(path,null,view.subarray(off,off+len),!0,!0,!0)
        private static readonly Regex CreateRegex = new Regex(
            @"(\w+)\.FS_createDataFile\((\w+),null,(\w+)\.subarray\((\w+),(\w+)\+(\w+)\),!0,!0,!0\)",
            RegexOptions.CultureInvariant);

        /// <summary>패치 결과. Source 는 적용하지 못했으면 입력 그대로다.</summary>
        internal sealed class PatchOutcome
        {
            internal string Source;
            internal bool Applied;
            internal bool AlreadyPatched;

            /// <summary>적용하지 못한 이유(Applied 가 false 이고 AlreadyPatched 가 아닐 때).</summary>
            internal string Reason;
        }

        private sealed class Edit
        {
            internal int Index;
            internal int Length;
            internal string Replacement;
        }

        /// <summary>
        /// loader 텍스트에 data-release 그룹을 적용한다(순수 로직, Node 불필요).
        /// 앵커가 정확히 1회가 아니면 아무것도 적용하지 않고 이유를 Reason 에 남긴다.
        /// </summary>
        internal static PatchOutcome PatchText(string source)
        {
            var outcome = new PatchOutcome { Source = source };
            if (string.IsNullOrEmpty(source))
            {
                outcome.Reason = "빈 텍스트";
                return outcome;
            }

            if (source.IndexOf(HookGlobal, StringComparison.Ordinal) >= 0)
            {
                outcome.AlreadyPatched = true;
                return outcome;
            }

            MatchCollection readBody = ReadBodyRegex.Matches(source);
            if (readBody.Count != 1)
            {
                outcome.Reason = "readBodyWithProgress 앵커 일치 " + readBody.Count + "회(기대 1회)";
                return outcome;
            }

            string respVar = readBody[0].Groups[1].Value;
            int windowStart = readBody[0].Index;
            int windowLength = Math.Min(AllocSearchWindow, source.Length - windowStart);
            MatchCollection allocs = AllocRegex.Matches(source.Substring(windowStart, windowLength));
            if (allocs.Count != 1)
            {
                outcome.Reason = "버퍼 할당 앵커 일치 " + allocs.Count + "회(기대 1회)";
                return outcome;
            }

            MatchCollection creates = CreateRegex.Matches(source);
            if (creates.Count != 1)
            {
                outcome.Reason = "FS_createDataFile 앵커 일치 " + creates.Count + "회(기대 1회)";
                return outcome;
            }

            Match a = allocs[0];
            Match c = creates[0];
            string bufVar = a.Groups[1].Value;
            string sizeVar = a.Groups[2].Value;
            string chunksVar = a.Groups[3].Value;

            var edits = new List<Edit>
            {
                new Edit
                {
                    Index = windowStart + a.Index,
                    Length = a.Length,
                    Replacement = "," + bufVar + "=(" + HookGlobal + "&&" + HookGlobal + ".alloc(" + sizeVar + "," + respVar + "))||new Uint8Array(" + sizeVar + ")," + chunksVar + "=[],",
                },
                new Edit
                {
                    Index = c.Index,
                    Length = c.Length,
                    Replacement = "(" + HookGlobal + "&&" + HookGlobal + ".created||function(n){return n})(" + c.Value + "," + c.Groups[2].Value + ")",
                },
            };

            // 두 편집이 겹치면(있을 수 없지만) 적용하지 않는다.
            Edit first = edits[0].Index <= edits[1].Index ? edits[0] : edits[1];
            Edit second = ReferenceEquals(first, edits[0]) ? edits[1] : edits[0];
            if (first.Index + first.Length > second.Index)
            {
                outcome.Reason = "편집 구간이 겹침";
                return outcome;
            }

            // 뒤에서부터 적용해 앞 구간 오프셋을 보존한다. '$' 패턴 해석이 없도록 문자열 이어붙이기만 쓴다.
            string text = source;
            text = text.Substring(0, second.Index) + second.Replacement + text.Substring(second.Index + second.Length);
            text = text.Substring(0, first.Index) + first.Replacement + text.Substring(first.Index + first.Length);

            outcome.Source = text;
            outcome.Applied = true;
            return outcome;
        }

        /// <summary>
        /// loader 파일에 패치를 적용한다.
        /// </summary>
        /// <param name="buildDir">Unity Build/ 폴더 경로.</param>
        /// <param name="config">현재 빌드 설정.</param>
        /// <param name="renames">rename 한 파일의 옛 이름 → 새 이름을 기록할 맵(null 허용).</param>
        /// <returns>패치를 적용한 파일 수.</returns>
        internal static int Apply(string buildDir, AITEditorScriptObject config, IDictionary<string, string> renames = null)
        {
            try
            {
                return ApplyCore(buildDir, config, renames);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-DataRelease] loader 패치 예외 — 패치 없이 계속합니다: {e.GetType().Name}: {e.Message}");
                return 0;
            }
        }

        private static int ApplyCore(string buildDir, AITEditorScriptObject config, IDictionary<string, string> renames)
        {
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                return 0;
            }

            // 기본(자동)은 꺼짐: 로더를 건드리지 않는다.
            if (!AITPerfFlags.EffectiveReleaseConsumedData(config))
            {
                return 0;
            }

            if (!AITPerfFlags.EffectiveExactDataBody(config))
            {
                Debug.LogWarning("[AIT-DataRelease] releaseConsumedData 는 exactDataBody(data 크기 정확 재포장)가 켜져 있어야 동작합니다 — loader 패치를 건너뜁니다.");
                return 0;
            }

            var targets = new List<string>();
            foreach (string path in Directory.GetFiles(buildDir))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[AIT-DataRelease] .unityweb 산출물이 있어 loader 패치를 건너뜁니다.");
                    return 0;
                }

                if (IsLoaderFile(name) && !AITPatchedFileNaming.IsPatched(name))
                {
                    targets.Add(path);
                }
            }

            if (targets.Count == 0)
            {
                return 0;
            }

            if (!AITBrotliCompressor.TryResolveNode(out string node))
            {
                Debug.LogWarning("[AIT-DataRelease] 내장 Node 미가용 — loader 패치를 건너뜁니다(stock).");
                return 0;
            }

            int patched = 0;
            foreach (string path in targets)
            {
                try
                {
                    if (PatchFile(buildDir, path, node, renames))
                    {
                        patched++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-DataRelease] {Path.GetFileName(path)} 패치 실패(원본 유지): {e.GetType().Name}: {e.Message}");
                }
            }

            return patched;
        }

        private static bool PatchFile(string buildDir, string path, string node, IDictionary<string, string> renames)
        {
            string name = Path.GetFileName(path);
            string source = File.ReadAllText(path, new UTF8Encoding(false));
            PatchOutcome outcome = PatchText(source);

            if (outcome.AlreadyPatched)
            {
                Debug.Log($"[AIT-DataRelease] {name}: 이미 패치됨 — 건너뜁니다.");
                return false;
            }

            if (!outcome.Applied)
            {
                // Unity 가 앵커 모양을 바꿨다. stock 그대로 두면 .data 는 종전처럼 상주한다(회귀 없음).
                Debug.LogWarning($"[AIT-DataRelease] {name}: {GroupDataRelease} 그룹을 적용하지 못해 stock 그대로 둡니다 ({outcome.Reason})");
                return false;
            }

            string work = Path.Combine(Path.GetTempPath(), "ait-loaderpatch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(work);
            try
            {
                string patchedJs = Path.Combine(work, "patched.js");
                File.WriteAllText(patchedJs, outcome.Source, new UTF8Encoding(false));

                if (!RunNodeCheck(node, patchedJs, out string checkErr))
                {
                    Debug.LogWarning($"[AIT-DataRelease] {name}: 패치 결과가 node --check 를 통과하지 못해 원본을 유지합니다: {Truncate(checkErr)}");
                    return false;
                }

                // 새 바이트를 patch-set 이름으로 먼저 쓰고 나서 원본을 지운다 — 중간에 실패해도 원본은 남는다.
                string configHash = AITPatchedFileNaming.ComputeConfigHash(PatchRevision, GroupDataRelease);
                string newName = AITPatchedFileNaming.GetPatchedName(name, AITPatchedFileNaming.PatchSetVersion, configHash);
                File.Copy(patchedJs, Path.Combine(buildDir, newName), true);
                if (!string.Equals(newName, name, StringComparison.Ordinal))
                {
                    File.Delete(path);
                }

                var local = new Dictionary<string, string>(StringComparer.Ordinal) { { name, newName } };
                if (renames != null)
                {
                    renames[name] = newName;
                }

                RewriteTextReferences(buildDir, local);

                long delta = outcome.Source.Length - source.Length;
                Debug.Log($"[AIT-DataRelease] loader 패치: {name} → {newName} (적용 {GroupDataRelease}, +{delta}B)");
                return true;
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { /* 임시 폴더 정리 실패 무시 */ }
            }
        }

        // loader 파일 이름을 가리키는 작은 텍스트(빌드 폴더의 html/json)가 있으면 새 이름으로 고친다.
        private static void RewriteTextReferences(string buildDir, IDictionary<string, string> renames)
        {
            foreach (string path in Directory.GetFiles(buildDir))
            {
                string name = Path.GetFileName(path);
                bool text = name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    || (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        && name.IndexOf(".symbols.", StringComparison.OrdinalIgnoreCase) < 0);
                if (!text)
                {
                    continue;
                }

                try
                {
                    if (new FileInfo(path).Length <= 2 * 1024 * 1024)
                    {
                        AITPatchedFileNaming.RewriteReferencesInFile(path, renames);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-DataRelease] 참조 갱신 실패(무시): {name}: {e.Message}");
                }
            }
        }

        /// <summary>파일명이 loader 산출물(무압축 *.loader.js)인지.</summary>
        internal static bool IsLoaderFile(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && fileName.EndsWith(".loader.js", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RunNodeCheck(string node, string jsPath, out string stderr)
        {
            stderr = null;
            var psi = new ProcessStartInfo
            {
                FileName = node,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            psi.Arguments = "--check \"" + jsPath + "\"";

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();

                // 두 스트림 모두 비동기로 모아 버퍼가 차서 교착하는 일을 막는다.
                var errSb = new StringBuilder();
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) { errSb.AppendLine(ev.Data); } };
                p.OutputDataReceived += (_, ev) => { };
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();

                if (!p.WaitForExit(NodeTimeoutMs))
                {
                    try { p.Kill(); } catch { /* 이미 종료됨 */ }
                    stderr = errSb.ToString();
                    return false;
                }

                p.WaitForExit();
                stderr = errSb.ToString();
                return p.ExitCode == 0;
            }
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "(출력 없음)";
            }

            s = s.Trim();
            return s.Length <= 300 ? s : s.Substring(0, 300) + "…";
        }
    }
}
