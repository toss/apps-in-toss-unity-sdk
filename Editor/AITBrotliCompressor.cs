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

        /// <summary>.br q11 재인코딩 파일 1건의 결과. error 가 비어 있고 verified=true 일 때만 신뢰.</summary>
        [Serializable]
        internal class RecompressResult
        {
            public int idx;
            public long origBr;   // 원본 .br 크기(bytes)
            public long decoded;  // .br 을 디코드한 원본 바이트 크기
            public long reBr;     // q11 재인코딩 .br 크기(bytes)
            public bool verified; // 재인코딩본을 재디코드한 바이트가 원본 디코드 바이트와 동일한지
            public bool wrote;    // 러너가 dst(temp)에 재인코딩본을 실제로 썼는지(verified && reBr<origBr)
            public long ms;       // 이 파일의 디코드+재인코딩+검증 소요(ms)
            public string error;  // 비어 있으면 디코드/재인코딩 성공
        }

        [Serializable]
        private class RecompressBatch
        {
            public RecompressResult[] results;
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

        /// <summary>
        /// dir 내 확장자 .br 파일을 q11 로 in-place 재인코딩한다(스파이크 훅). Unity 내장 brotli(~q5)를
        /// 외부 q11 로 다시 눌러 data/wasm 을 더 줄이는 것이 목적이며, 파일명은 그대로 유지한다.
        ///
        /// 대상 선정: dir 최상위의 .br 파일만. .unityweb(Unity decompressionFallback 산출물 —
        /// brotli 메타데이터에 Unity 감지 마커가 박혀 있어 재인코딩하면 로더가 못 읽음)은 절대 건드리지 않는다.
        ///
        /// 안전 가드(하나라도 걸리면 원본을 그대로 둔다):
        ///  (a) 재인코딩 결과가 원본 .br 이상(reBr &gt;= origBr)이면 채택하지 않음.
        ///  (b) 디코드 실패(.br 이름인데 실제 brotli 가 아님)면 경고 로그 후 원본 유지.
        ///  (c) 재인코딩본을 재디코드한 바이트가 원본 디코드 바이트와 다르면(verified=false) 경고 후 원본 유지.
        /// 채택 시에도 러너는 temp(dst) 에만 쓰고, C# 이 File.Replace 로 원본을 원자적으로 교체한다.
        /// 표준 윈도우만 사용한다(BROTLI_PARAM_LGWIN 미지정=기본 22, LARGE_WINDOW 미사용 — 브라우저 CE 디코더 호환).
        /// Node 미가용 시 no-op(원본 유지). 파일별 원본→재인코딩 크기와 소요 시간을 Debug.Log 로 남긴다.
        /// </summary>
        internal static void RecompressBrFilesInPlace(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            var targets = new List<string>();
            foreach (var path in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(path);
                // .unityweb 는 절대 재인코딩 금지(감지 마커 파손 → 로더 실패). 확장자가 .br 이 아니면 무시.
                if (name.IndexOf(".unityweb", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (!name.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                targets.Add(path);
            }

            if (targets.Count == 0)
            {
                return;
            }

            if (!TryResolveNode(out string node))
            {
                Debug.LogWarning("[AIT-Brotli] 내장 Node 미가용 — .br q11 재인코딩을 건너뜁니다(원본 유지).");
                return;
            }

            string runner = null;
            var temps = new List<string>();
            try
            {
                runner = WriteRecompressRunner();

                var input = new StringBuilder();
                input.Append("{\"quality\":").Append(Quality).Append(",\"files\":[");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0)
                    {
                        input.Append(',');
                    }

                    string tmp = targets[i] + ".q11tmp";
                    temps.Add(tmp);
                    input.Append("{\"idx\":").Append(i)
                         .Append(",\"src\":").Append(JsonStr(targets[i]))
                         .Append(",\"dst\":").Append(JsonStr(tmp)).Append('}');
                }

                input.Append("]}");

                if (!RunNode(node, runner, input.ToString(), out string stdout, out string stderr))
                {
                    Debug.LogWarning($"[AIT-Brotli] q11 재인코딩 실행 실패 — 원본 유지: {Truncate(stderr ?? stdout)}");
                    return;
                }

                var batch = UnityEngine.JsonUtility.FromJson<RecompressBatch>(stdout);
                if (batch?.results == null)
                {
                    Debug.LogWarning("[AIT-Brotli] q11 재인코딩 결과 파싱 실패 — 원본 유지.");
                    return;
                }

                int adopted = 0;
                long totalBefore = 0, totalAfter = 0;
                foreach (var r in batch.results)
                {
                    if (r == null || r.idx < 0 || r.idx >= targets.Count)
                    {
                        continue;
                    }

                    string src = targets[r.idx];
                    string tmp = src + ".q11tmp";
                    string name = Path.GetFileName(src);

                    // 가드 (b): 디코드 실패(.br 이름인데 brotli 아님) → 원본 유지 + 경고.
                    if (!string.IsNullOrEmpty(r.error))
                    {
                        Debug.LogWarning($"[AIT-Brotli] {name}: 재인코딩 실패(원본 유지) — {r.error}");
                        continue;
                    }

                    // 가드 (c): 재디코드 검증 실패 → 원본 유지 + 경고.
                    if (!r.verified)
                    {
                        Debug.LogWarning($"[AIT-Brotli] {name}: 재디코드 검증 실패(원본 유지) — 재인코딩 바이트가 원본 디코드와 불일치.");
                        continue;
                    }

                    // 가드 (a): 재인코딩이 원본 이상이면 이득 없음 → 원본 유지.
                    if (!r.wrote || r.reBr <= 0 || r.reBr >= r.origBr)
                    {
                        Debug.Log($"[AIT-Brotli] {name}: 재인코딩 이득 없음(원본 {r.origBr}B ≤ 재인코딩 {r.reBr}B, {r.ms}ms) — 원본 유지.");
                        continue;
                    }

                    // 채택: temp → 원본 원자적 교체(File.Replace). 실패 시 Delete+Move 폴백.
                    if (!File.Exists(tmp))
                    {
                        Debug.LogWarning($"[AIT-Brotli] {name}: 재인코딩 temp 파일 부재(원본 유지).");
                        continue;
                    }

                    try
                    {
                        AtomicReplace(tmp, src);
                        adopted++;
                        totalBefore += r.origBr;
                        totalAfter += r.reBr;
                        double pct = r.origBr > 0 ? (r.origBr - r.reBr) * 100.0 / r.origBr : 0;
                        Debug.Log($"[AIT-Brotli] {name}: q11 재인코딩 채택 {r.origBr}B → {r.reBr}B (−{r.origBr - r.reBr}B, {pct:0.#}%, {r.ms}ms)");
                    }
                    catch (Exception e)
                    {
                        // AtomicReplace 는 실패 시 원본을 복원하거나(성공) 백업 경로에 보존한 채로(실패) 예외를
                        // 던진다 — 메시지 자체가 실제 결과를 담고 있으므로 여기서 "(원본 유지)"를 단정하지 않는다.
                        Debug.LogWarning($"[AIT-Brotli] {name}: 원자적 교체 실패 — {e.Message}");
                    }
                }

                Debug.Log($"[AIT-Brotli] q11 재인코딩 완료: {adopted}/{targets.Count}개 채택, {totalBefore}B → {totalAfter}B (−{totalBefore - totalAfter}B).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-Brotli] q11 재인코딩 예외 — 원본 유지: {e.Message}");
            }
            finally
            {
                if (runner != null)
                {
                    try { File.Delete(runner); } catch { /* 임시 파일 정리 실패 무시 */ }
                }

                foreach (var t in temps)
                {
                    try { if (File.Exists(t)) { File.Delete(t); } } catch { /* 채택 시 이미 rename 됨 / 미채택 잔여 정리 */ }
                }
            }
        }

        // temp(신규 내용) → dst(원본) 원자적 교체. File.Replace 는 동일 볼륨에서 원자적이며 dst 를 새 내용으로
        // 갈아끼운다(백업 없음). File.Replace 는 플랫폼에 따라(예: 일부 비-Windows 런타임) 예외를 던질 수
        // 있는데, 이때 절대 dst 를 먼저 지우지 않는다 — Delete 후 Move 가 실패하면(디스크 풀, 권한, 프로세스
        // 강제 종료 등) 원본 .br 빌드 산출물이 복구 불가능하게 사라진다. 대신 dst 를 백업 경로로 rename 해
        // 놓은 뒤에만 temp 를 dst 로 옮기고, 그 이동이 실패하면 백업에서 dst 를 복원한다(rename 은 동일
        // 디렉토리·동일 볼륨이라 실패 확률이 삭제+이동 조합보다 훨씬 낮고, 실패해도 백업이 원본을 보존한다).
        private static void AtomicReplace(string temp, string dst)
        {
            try
            {
                File.Replace(temp, dst, null);
                return;
            }
            catch
            {
                // 폴백 진행(아래).
            }

            string backup = dst + ".ait-bak";
            try
            {
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
            }
            catch
            {
                // 이전 잔여 백업 정리 실패는 무시 — File.Move(dst, backup) 자체가 실패하면 아래에서 잡힌다.
            }

            bool renamed = false;
            try
            {
                File.Move(dst, backup);
                renamed = true;

                File.Move(temp, dst);

                // 교체 완료 — 더 이상 필요 없는 백업 정리(실패해도 dst 는 이미 안전).
                try { File.Delete(backup); } catch { /* 백업 정리 실패 무시 */ }
            }
            catch (Exception moveEx)
            {
                // dst 가 백업으로 이미 옮겨진 상태에서 temp→dst 이동이 실패했다면, dst 를 잃기 전에
                // 백업에서 즉시 복원을 시도한다. 원본을 절대 무조건 삭제하지 않으므로, 복원이 성공하면
                // 원본이 실제로 보존되고, 복원까지 실패해도 백업 파일이 디스크에 남아 수동 복구가 가능하다.
                if (renamed && !File.Exists(dst) && File.Exists(backup))
                {
                    try
                    {
                        File.Move(backup, dst);
                    }
                    catch (Exception restoreEx)
                    {
                        throw new IOException(
                            $"임시 파일 교체 실패 및 백업 복원도 실패 — 원본이 '{backup}' 에 보존되어 있으니 수동 복구 필요: {restoreEx.Message}",
                            moveEx);
                    }

                    throw new IOException($"임시 파일 교체 실패, 백업에서 원본 복원 완료(원본 유지): {moveEx.Message}", moveEx);
                }

                throw;
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

        // stdin 으로 {quality, files:[{idx,src,dst}]} 를 받아, 각 src(.br)를 brotliDecompressSync 로 풀고
        // q11 로 다시 눌러(표준 윈도우 최대 LGWIN=24 명시, LARGE_WINDOW 미사용) 검증까지 마친 뒤,
        // ── LGWIN=24 인 이유: Node 기본(22)은 Unity 내장 인코더의 윈도우보다 작아 wasm 등
        //    대형 파일에서 재인코딩이 오히려 커질 수 있다(샘플 실측: wasm 8,021,924B → w22 8,047,001B
        //    vs w24 7,838,394B). 24는 RFC 7932 표준 윈도우 상한이라 모든 브라우저 CE 디코더가 지원한다.
        // 이득이 있을 때만(verified && reBr<origBr) dst(temp)에 쓴다. stdout 으로
        // {results:[{idx,origBr,decoded,reBr,verified,wrote,ms,error?}]} 를 돌려준다.
        //  · verified: 재인코딩본을 재디코드한 바이트가 원본 디코드 바이트와 동일한지(라운드트립 검증).
        //  · 디코드 실패(.br 이름인데 실제 brotli 아님)는 error 에 사유를 담고 원본을 건드리지 않는다.
        //  · 원본 교체는 하지 않는다 — 원자적 교체는 C#(File.Replace) 몫.
        private const string RecompressRunnerJs =
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
            "    const t0=Date.now();\n" +
            "    let origBr=0;\n" +
            "    try{\n" +
            "      const input=fs.readFileSync(f.src);\n" +
            "      origBr=input.length;\n" +
            "      let decoded;\n" +
            "      try{decoded=zlib.brotliDecompressSync(input);}\n" +
            "      catch(e){out.push({idx:f.idx,origBr:origBr,decoded:0,reBr:0,verified:false,wrote:false,ms:Date.now()-t0,error:'decode:'+String((e&&e.message)||e)});continue;}\n" +
            "      const re=zlib.brotliCompressSync(decoded,{params:{[zlib.constants.BROTLI_PARAM_QUALITY]:q,[zlib.constants.BROTLI_PARAM_LGWIN]:24,[zlib.constants.BROTLI_PARAM_SIZE_HINT]:decoded.length}});\n" +
            "      let verified=false;\n" +
            "      try{const chk=zlib.brotliDecompressSync(re);verified=(chk.length===decoded.length&&Buffer.compare(chk,decoded)===0);}catch(e){verified=false;}\n" +
            "      let wrote=false;\n" +
            "      if(verified&&re.length<origBr){fs.writeFileSync(f.dst,re);wrote=true;}\n" +
            "      out.push({idx:f.idx,origBr:origBr,decoded:decoded.length,reBr:re.length,verified:verified,wrote:wrote,ms:Date.now()-t0});\n" +
            "    }catch(e){out.push({idx:f.idx,origBr:origBr,decoded:0,reBr:0,verified:false,wrote:false,ms:Date.now()-t0,error:String((e&&e.message)||e)});}\n" +
            "  }\n" +
            "  process.stdout.write(JSON.stringify({results:out}));\n" +
            "});\n";

        private static string WriteRecompressRunner()
        {
            // 병렬 Editor 인스턴스 간 충돌을 피하기 위해 프로세스별 고유 파일명 사용.
            string path = Path.Combine(Path.GetTempPath(), $"ait-brotli-recompress-runner-{Process.GetCurrentProcess().Id}.js");
            File.WriteAllText(path, RecompressRunnerJs);
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
