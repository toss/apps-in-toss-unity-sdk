// -----------------------------------------------------------------------
// <copyright file="AITBrotliCompressor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming asset brotli compressor (build-time)
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 스트리밍 외부화 산출물(ait-stream-*)을 SDK 내장 Node.js(zlib.brotliCompressSync)로
    /// 일괄 brotli 압축하는 빌드타임 유틸리티.
    ///
    /// Editor mono BCL에는 brotli "인코더"가 없다(디코더 가용성조차 프로파일 의존 —
    /// AITWarmManifestEmitter가 reflection으로 참조하는 이유). 인코딩은 빌드 파이프라인이
    /// 어차피 보장하는 내장 Node(AITNodeJSDownloader)로 위임한다. Node 미가용 시 압축을
    /// 생략하고 원본을 유지한다(기능 저하 없음 — 번들 크기만 종전과 동일).
    /// </summary>
    internal static class AITBrotliCompressor
    {
        /// <summary>brotli 품질(0~11). 빌드타임 1회 비용이므로 최고 압축률을 쓴다.</summary>
        private const int Quality = 11;

        /// <summary>node 일괄 압축 타임아웃(ms). q11로 수십 MB를 눌러도 여유 있는 값.</summary>
        private const int TimeoutMs = 600000;

        /// <summary>
        /// 기본 채택 임계(%). br 산출물이 원본보다 이 비율 이상 작을 때만 채택한다.
        /// 이미 엔트로피 코딩된 포맷(PNG/JPG/LZ4 번들)의 파일럿 실측 편차(0~22%)를 근거로,
        /// 다운로드 시 브라우저의 추가 gzip/br 협상 여지와 .br 파일 관리 비용을 상쇄할 최소선.
        /// </summary>
        internal const int DefaultMinGainPercent = 10;

        /// <summary>파일 1건의 압축 결과. error 가 비어 있으면 성공.</summary>
        [Serializable]
        internal class Result
        {
            public int idx;
            public long raw;
            public long br;
            public string error;

            public bool Ok => string.IsNullOrEmpty(error) && raw > 0 && br > 0;
        }

        [Serializable]
        private class Batch
        {
            public Result[] results;
        }

        /// <summary>
        /// 압축 채택 판정: br 산출물이 원본 대비 minGainPercent 이상 작아야 true.
        /// PNG/JPG처럼 이미 엔트로피 코딩이 끝난 포맷은 이득이 파일별로 0~수%에 그치는 경우가
        /// 많아(2026-07 파일럿 실측 0~18.5%), 미달 파일은 br을 버리고 원본을 유지한다.
        /// </summary>
        internal static bool ShouldKeep(long rawBytes, long brBytes, int minGainPercent)
        {
            if (rawBytes <= 0 || brBytes <= 0)
            {
                return false;
            }

            return brBytes * 100L <= rawBytes * (100L - minGainPercent);
        }

        /// <summary>내장 Node 실행 파일을 해석한다(미설치 시 on-demand 다운로드 포함). 실패 시 false.</summary>
        internal static bool TryResolveNode(out string nodeExe)
        {
            nodeExe = null;
            try
            {
                string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
                if (string.IsNullOrEmpty(npm))
                {
                    return false;
                }

                string nodeBin = Path.GetDirectoryName(npm);
                string node = Path.Combine(nodeBin, AITPlatformHelper.IsWindows ? "node.exe" : "node");
                if (!File.Exists(node))
                {
                    return false;
                }

                nodeExe = node;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-Brotli] 내장 Node 해석 예외: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// sources 각각에 대해 "&lt;원본&gt;.br" 사본을 같은 디렉토리에 만든다(원본은 건드리지 않음 —
        /// 채택/원본삭제 판단은 호출부 몫). 반환: 원본 절대경로 → Result. node 미가용/일괄 실행
        /// 실패 시 빈 딕셔너리를 반환하고, 호출부는 원본 유지 경로로 진행한다.
        /// </summary>
        internal static Dictionary<string, Result> Compress(IReadOnlyList<string> sources)
        {
            var map = new Dictionary<string, Result>();
            if (sources == null || sources.Count == 0)
            {
                return map;
            }

            if (!TryResolveNode(out string node))
            {
                Debug.LogWarning("[AIT-Brotli] 내장 Node 미가용 — 스트리밍 에셋 brotli 압축을 건너뜁니다(원본 유지).");
                return map;
            }

            string runner = null;
            try
            {
                runner = WriteRunner();

                var input = new StringBuilder();
                input.Append("{\"quality\":").Append(Quality).Append(",\"files\":[");
                for (int i = 0; i < sources.Count; i++)
                {
                    if (i > 0)
                    {
                        input.Append(',');
                    }

                    input.Append("{\"idx\":").Append(i)
                         .Append(",\"src\":").Append(JsonStr(sources[i]))
                         .Append(",\"dst\":").Append(JsonStr(sources[i] + ".br")).Append('}');
                }

                input.Append("]}");

                if (!RunNode(node, runner, input.ToString(), out string stdout, out string stderr))
                {
                    Debug.LogWarning($"[AIT-Brotli] 일괄 압축 실행 실패 — 원본 유지: {Truncate(stderr ?? stdout)}");
                    return map;
                }

                var batch = UnityEngine.JsonUtility.FromJson<Batch>(stdout);
                if (batch?.results == null)
                {
                    Debug.LogWarning("[AIT-Brotli] 압축 결과 파싱 실패 — 원본 유지.");
                    return map;
                }

                foreach (var r in batch.results)
                {
                    if (r != null && r.idx >= 0 && r.idx < sources.Count)
                    {
                        map[sources[r.idx]] = r;
                    }
                }

                return map;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-Brotli] 압축 예외 — 원본 유지: {e.Message}");
                map.Clear();
                return map;
            }
            finally
            {
                if (runner != null)
                {
                    try { File.Delete(runner); } catch { /* 임시 파일 정리 실패 무시 */ }
                }
            }
        }

        // ─────────────────────────── 내부 구현 ───────────────────────────

        // stdin 으로 {quality, files:[{idx,src,dst}]} 를 받아 각 파일을 brotli 압축해 dst 에 쓰고,
        // stdout 으로 {results:[{idx,raw,br,error?}]} 를 돌려주는 단일 파일 러너.
        // 외부 npm 패키지 불필요(zlib 내장) — AITFontSubsetProcessor 와 달리 설치 단계가 없다.
        private const string RunnerJs =
            "'use strict';\n" +
            "const zlib=require('zlib'),fs=require('fs');\n" +
            "let raw='';\n" +
            "process.stdin.on('data',(d)=>{raw+=d;});\n" +
            "process.stdin.on('end',()=>{\n" +
            "  let req;\n" +
            "  try{req=JSON.parse(raw);}catch(e){process.stdout.write('{\"results\":[]}');process.exit(2);return;}\n" +
            "  const q=(req.quality|0)||11;\n" +
            "  const out=[];\n" +
            "  for(const f of (req.files||[])){\n" +
            "    try{\n" +
            "      const buf=fs.readFileSync(f.src);\n" +
            "      const br=zlib.brotliCompressSync(buf,{params:{[zlib.constants.BROTLI_PARAM_QUALITY]:q,[zlib.constants.BROTLI_PARAM_SIZE_HINT]:buf.length}});\n" +
            "      fs.writeFileSync(f.dst,br);\n" +
            "      out.push({idx:f.idx,raw:buf.length,br:br.length});\n" +
            "    }catch(e){out.push({idx:f.idx,raw:0,br:0,error:String((e&&e.message)||e)});}\n" +
            "  }\n" +
            "  process.stdout.write(JSON.stringify({results:out}));\n" +
            "});\n";

        private static string WriteRunner()
        {
            // 병렬 Editor 인스턴스 간 충돌을 피하기 위해 프로세스별 고유 파일명 사용.
            string path = Path.Combine(Path.GetTempPath(), $"ait-brotli-runner-{Process.GetCurrentProcess().Id}.js");
            File.WriteAllText(path, RunnerJs);
            return path;
        }

        private static bool RunNode(string node, string runner, string stdinJson, out string stdout, out string stderr)
        {
            stdout = null;
            stderr = null;
            var psi = new ProcessStartInfo
            {
                FileName = node,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            psi.ArgumentList.Add(runner);

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();

                // stderr 는 이벤트로 비동기 수집 — stdout ReadToEnd 중 stderr 버퍼가 차서
                // 교착하는 고전적 데드락 방지(러너는 stderr 를 거의 안 쓰지만 방어).
                var errSb = new StringBuilder();
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) { errSb.AppendLine(ev.Data); } };
                p.BeginErrorReadLine();

                p.StandardInput.Write(stdinJson);
                p.StandardInput.Close();

                stdout = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(TimeoutMs))
                {
                    try { p.Kill(); } catch { /* 이미 종료됨 */ }
                    stderr = errSb.ToString();
                    return false;
                }

                // WaitForExit(timeout) 성공 후 인자 없는 WaitForExit 로 비동기 스트림 플러시 보장.
                p.WaitForExit();
                stderr = errSb.ToString();
                return p.ExitCode == 0;
            }
        }

        private static string JsonStr(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
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
