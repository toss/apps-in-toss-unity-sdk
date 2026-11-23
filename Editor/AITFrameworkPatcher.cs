// -----------------------------------------------------------------------
// AITFrameworkPatcher.cs - 빌드 후 Unity framework(*.framework.js[.br|.gz]) 텍스트 패치
//
// 호출 지점: WebGLBuildCopier.CopyWebGLToPublic — 필수 파일 검증 직후, Build/ 선별 복사보다 앞.
//
// === 무엇을 패치하나 ===
//  context-resume-offset  AudioContext 가 멈출 때 stock 은 채널을 pause 하며 멈춘 위치를 offset 으로 쌓고, 다시 돌 때
//                         channel.resume(offset+멈춘 시간)을 부른다. resume 은 멈춘 위치에 offset 을 또 더하므로 위치가
//                         매번 두 배로 튄다(앱 복귀 뒤 루프 BGM 이 엉뚱한 곳에서 이어지고 같이 맞춘 트랙끼리 어긋남).
//                         offset 을 0 으로 둬 멈춘 위치 + 멈춘 시간에서 잇게 한다.
//
// === 안전 계약 ===
//  - 앵커가 정확히 1회 일치할 때만 적용한다. 0회(이미 패치됐거나 Unity 가 문자열을 바꿈)나 2회 이상이면 건너뛰고 stock 그대로 둔다.
//    그래서 이미 패치된 파일에 다시 돌려도 아무 일도 하지 않는다(멱등).
//  - 어떤 실패도 빌드를 막지 않는다(fail-open). 예외는 밖으로 던지지 않고, 원본 파일은 새 바이트를 다 쓴 뒤에만 교체한다.
//  - Decompression Fallback(.unityweb)은 JS 디컴프레서가 감지 마커로 쓰는 포맷이라 건드리지 않는다.
//  - .br 는 Editor mono 에 디코더가 없어 내장 Node 로 풀고 다시 누른다(압축 라운드트립 검증 포함). Node 를 못 구하면 건너뛴다.
//  - 파일명은 바꾸지 않는다. Unity 가 빌드마다 Build/ 를 새로 쓰고, 패치 대상 문자열이 없어진 파일은 건너뛰므로 같은 파일을 두 번 패치하지 않는다.
//  - Unity 원본 소스 텍스트를 저장소에 두지 않는다(공개 저장소). 앵커는 매칭에 필요한 짧은 스니펫만 쓴다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    internal static class AITFrameworkPatcher
    {
        /// <summary>그룹 이름(로그·테스트에서 쓴다).</summary>
        internal const string GroupResumeOffset = "context-resume-offset";

        // 2022.3(stopDelay 없음)·6000.0(stopDelay:-1 있음) 두 형태 모두 이 꼬리로 정확히 1회 일치한다.
        internal const string ResumeOffsetFrom = "offset:audioInstance.source.playbackPausedAtPosition})";

        internal const string ResumeOffsetTo = "offset:0})";

        private const int NodeTimeoutMs = 120000;

        /// <summary>텍스트 패치 결과.</summary>
        internal sealed class PatchOutcome
        {
            /// <summary>패치된 텍스트. 적용된 그룹이 없으면 입력 그대로.</summary>
            public string Source;

            public readonly List<string> Applied = new List<string>();

            /// <summary>건너뛴 그룹과 이유.</summary>
            public readonly List<string> Skipped = new List<string>();
        }

        /// <summary>겹치지 않는 ordinal 일치 횟수.</summary>
        internal static int CountOccurrences(string text, string anchor)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(anchor))
            {
                return 0;
            }

            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(anchor, i, StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += anchor.Length;
            }

            return count;
        }

        /// <summary>
        /// framework 텍스트에 패치를 적용한다. 앵커가 정확히 1회가 아니면 건너뛰고 이유를 Skipped 에 남긴다.
        /// </summary>
        internal static PatchOutcome PatchText(string source)
        {
            var outcome = new PatchOutcome { Source = source };
            if (string.IsNullOrEmpty(source))
            {
                outcome.Skipped.Add(GroupResumeOffset + ": 빈 텍스트");
                return outcome;
            }

            int n = CountOccurrences(source, ResumeOffsetFrom);
            if (n != 1)
            {
                outcome.Skipped.Add(GroupResumeOffset + ": 앵커 일치 " + n + "회(기대 1회)");
                return outcome;
            }

            // '$' 패턴 해석이 없도록 문자열 이어붙이기로 치환한다.
            int idx = source.IndexOf(ResumeOffsetFrom, StringComparison.Ordinal);
            outcome.Source = source.Substring(0, idx) + ResumeOffsetTo + source.Substring(idx + ResumeOffsetFrom.Length);
            outcome.Applied.Add(GroupResumeOffset);
            return outcome;
        }

        /// <summary>파일명이 framework 산출물이면 압축 종류("br"/"gz"/"none"), 아니면 null.</summary>
        internal static string GetFrameworkKind(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            if (fileName.EndsWith(".framework.js.br", StringComparison.OrdinalIgnoreCase)) return "br";
            if (fileName.EndsWith(".framework.js.gz", StringComparison.OrdinalIgnoreCase)) return "gz";
            if (fileName.EndsWith(".framework.js", StringComparison.OrdinalIgnoreCase)) return "none";
            return null;
        }

        /// <summary>
        /// Build/ 폴더의 framework 파일을 제자리에서 패치한다.
        /// </summary>
        /// <param name="buildDir">Unity Build/ 폴더 경로.</param>
        /// <returns>패치를 적용한 파일 수. 어떤 실패도 예외로 던지지 않는다.</returns>
        internal static int Apply(string buildDir)
        {
            try
            {
                return ApplyCore(buildDir);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-Audio] framework 패치 예외 — 패치 없이 계속합니다: {e.GetType().Name}: {e.Message}");
                return 0;
            }
        }

        private static int ApplyCore(string buildDir)
        {
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                return 0;
            }

            var targets = new List<string>();
            foreach (string path in Directory.GetFiles(buildDir))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[AIT-Audio] .unityweb 산출물이 있어 framework 패치를 건너뜁니다.");
                    return 0;
                }

                if (GetFrameworkKind(name) != null)
                {
                    targets.Add(path);
                }
            }

            int patched = 0;
            foreach (string path in targets)
            {
                try
                {
                    if (PatchFile(path))
                    {
                        patched++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-Audio] {Path.GetFileName(path)} 패치 실패(원본 유지): {e.GetType().Name}: {e.Message}");
                }
            }

            return patched;
        }

        private static bool PatchFile(string path)
        {
            string name = Path.GetFileName(path);
            string kind = GetFrameworkKind(name);
            var utf8 = new UTF8Encoding(false);

            string work = Path.Combine(Path.GetTempPath(), "ait-fwpatch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(work);
            try
            {
                string node = null;
                string runner = null;
                if (kind == "br")
                {
                    if (!TryResolveNode(out node))
                    {
                        Debug.Log($"[AIT-Audio] {name}: 내장 Node 를 구하지 못해 brotli framework 패치를 건너뜁니다(stock).");
                        return false;
                    }

                    runner = Path.Combine(work, "runner.js");
                    File.WriteAllText(runner, RunnerJs, utf8);
                }

                // 1) 풀기
                string source;
                if (kind == "none")
                {
                    source = File.ReadAllText(path, utf8);
                }
                else if (kind == "gz")
                {
                    using (var fs = File.OpenRead(path))
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gz, utf8))
                    {
                        source = reader.ReadToEnd();
                    }
                }
                else
                {
                    string decoded = Path.Combine(work, "decoded.js");
                    if (!RunRunner(node, runner, "decode", path, decoded, out string decodeErr))
                    {
                        Debug.LogWarning($"[AIT-Audio] {name} 해제 실패(원본 유지): {decodeErr}");
                        return false;
                    }

                    source = File.ReadAllText(decoded, utf8);
                }

                // 2) 패치
                PatchOutcome outcome = PatchText(source);
                if (outcome.Applied.Count == 0)
                {
                    // 이미 패치됐거나 Unity 가 앵커 문자열을 바꾼 경우, 오디오 모듈이 빠진 경우 — stock 그대로 둔다.
                    Debug.Log($"[AIT-Audio] {name}: 적용할 패치가 없어 그대로 둡니다 ({string.Join("; ", outcome.Skipped)})");
                    return false;
                }

                // 3) 다시 쓰기: 같은 폴더의 임시 파일에 먼저 완성한 뒤 교체한다(중간에 실패해도 원본은 남는다).
                string tmp = path + ".aitpatch.tmp";
                try
                {
                    if (kind == "none")
                    {
                        File.WriteAllText(tmp, outcome.Source, utf8);
                    }
                    else if (kind == "gz")
                    {
                        using (var fs = File.Create(tmp))
                        using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
                        {
                            byte[] bytes = utf8.GetBytes(outcome.Source);
                            gz.Write(bytes, 0, bytes.Length);
                        }
                    }
                    else
                    {
                        string patchedJs = Path.Combine(work, "patched.js");
                        File.WriteAllText(patchedJs, outcome.Source, utf8);
                        if (!RunRunner(node, runner, "encode", patchedJs, tmp, out string encodeErr))
                        {
                            Debug.LogWarning($"[AIT-Audio] {name} 재압축 실패(원본 유지): {encodeErr}");
                            return false;
                        }
                    }

                    File.Copy(tmp, path, true);
                }
                finally
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 임시 파일 정리 실패 무시 */ }
                }

                Debug.Log($"[AIT-Audio] framework 패치: {name} (적용 {string.Join(",", outcome.Applied)}, {outcome.Source.Length - source.Length:+#;-#;0}B)");
                return true;
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { /* 임시 폴더 정리 실패 무시 */ }
            }
        }

        // ─────────────────────────── Node (brotli 전용) ───────────────────────────

        // argv: op(decode|encode) src dst.
        //  decode: .br 를 풀어 dst 에 쓴다.
        //  encode: src 를 br(q11, LGWIN 24 — 표준 윈도우 상한이라 브라우저 디코더 호환)로 눌러 dst 에 쓰고,
        //          다시 풀어 원본과 바이트가 같은지 확인한다. 불일치면 실패(exit 1).
        private const string RunnerJs =
            "'use strict';\n" +
            "const zlib=require('zlib'),fs=require('fs');\n" +
            "try{\n" +
            "  const op=process.argv[2],src=process.argv[3],dst=process.argv[4];\n" +
            "  const input=fs.readFileSync(src);\n" +
            "  let out;\n" +
            "  if(op==='decode'){out=zlib.brotliDecompressSync(input);}\n" +
            "  else if(op==='encode'){\n" +
            "    out=zlib.brotliCompressSync(input,{params:{[zlib.constants.BROTLI_PARAM_QUALITY]:11,[zlib.constants.BROTLI_PARAM_LGWIN]:24,[zlib.constants.BROTLI_PARAM_SIZE_HINT]:input.length}});\n" +
            "    const back=zlib.brotliDecompressSync(out);\n" +
            "    if(back.length!==input.length||Buffer.compare(back,input)!==0)throw new Error('roundtrip mismatch');\n" +
            "  }else{throw new Error('unknown op');}\n" +
            "  fs.writeFileSync(dst,out);\n" +
            "}catch(e){process.stderr.write(String((e&&e.message)||e));process.exitCode=1;}\n";

        private static bool TryResolveNode(out string nodeExe)
        {
            nodeExe = null;
            try
            {
                string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
                if (string.IsNullOrEmpty(npm))
                {
                    return false;
                }

                string node = Path.Combine(Path.GetDirectoryName(npm), AITPlatformHelper.IsWindows ? "node.exe" : "node");
                if (!File.Exists(node))
                {
                    return false;
                }

                nodeExe = node;
                return true;
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT-Audio] 내장 Node 해석 예외: {e.Message}");
                return false;
            }
        }

        private static bool RunRunner(string node, string runner, string op, string src, string dst, out string error)
        {
            error = null;
            var psi = new ProcessStartInfo
            {
                FileName = node,
                Arguments = $"\"{runner}\" {op} \"{src}\" \"{dst}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };

            AITProcessExecutor.Result r = AITProcessExecutor.Run(psi, NodeTimeoutMs);
            if (r.TimedOut)
            {
                error = "시간 초과";
                return false;
            }

            if (r.ExitCode == 0 && File.Exists(dst))
            {
                return true;
            }

            string detail = (r.StdErr ?? string.Empty).Trim();
            error = detail.Length == 0 ? "exit " + r.ExitCode : (detail.Length <= 300 ? detail : detail.Substring(0, 300) + "…");
            return false;
        }
    }
}
