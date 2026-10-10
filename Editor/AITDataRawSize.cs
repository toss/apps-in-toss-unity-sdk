// -----------------------------------------------------------------------
// AITDataRawSize.cs - .data 파일의 압축 해제 후 크기 측정
//
// Unity 로더는 data 응답의 Content-Length 로 버퍼를 잡는다. .data.br/.data.gz 를 Content-Encoding 으로
// 서빙하면 Content-Length 는 압축 크기라 버퍼가 어긋나 복사가 생긴다. 이 측정값(압축 해제 후 바이트 수)을
// AITPerfFlags 가 window.__AIT_PERF.dataRawSize 로 주입하고, Runtime/ait-databuf.js 가 data Response 를
// Content-Length=RAW 로 다시 감싸 로더가 버퍼를 한 번만 잡게 한다.
//
// 측정 방식(파일명 접미사로 판정 — Unity 가 압축 형식에 따라 .br/.gz 를 붙인다):
//  · .unityweb  → -1  (Decompression Fallback 산출물. brotli 감지 마커가 박혀 있어 이 경로는 쓰지 않는다)
//  · .br        → 내장 Node 의 zlib 스트리밍 디코드로 바이트 수만 센다(메모리에 올리지 않음). Node 미가용이면 -1.
//                 Unity Editor mono BCL 에는 BrotliStream 이 없을 수 있어(AITBrotliCompressor 주석 참고) Node 에 위임한다.
//  · .gz        → System.IO.Compression.GZipStream 스트리밍 카운트
//  · 그 외      → 파일 길이(무압축 .data)
// 어떤 실패도 예외로 번지지 않고 -1 을 돌려준다(런타임 훅은 종전 동작).
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    internal static class AITDataRawSize
    {
        /// <summary>내장 Node 가 큰 .br 을 스트리밍으로 푸는 시간 상한(ms).</summary>
        private const int NodeTimeoutMs = 300000;

        /// <summary>Node 러너가 stdout 에 쓰는 결과 접두사. "RAW &lt;바이트 수&gt;".</summary>
        private const string RunnerOutputPrefix = "RAW ";

        /// <summary>파일명에서 판정한 data 파일 인코딩.</summary>
        internal enum DataEncoding
        {
            /// <summary>무압축(.data).</summary>
            None,
            /// <summary>.br</summary>
            Brotli,
            /// <summary>.gz</summary>
            Gzip,
            /// <summary>.unityweb (Decompression Fallback). 측정 대상이 아니다.</summary>
            Unityweb,
        }

        // 압축 해제 크기만 스트리밍으로 센다. 입력은 argv[2](.br 경로). 외부 npm 패키지 불필요(zlib 내장).
        // 성공: stdout "RAW <n>" + 종료 코드 0. 입력 손상/잘림/읽기 실패: stderr 에 사유 + 종료 코드 3~4.
        private const string RunnerJs =
            "'use strict';\n" +
            "const zlib=require('zlib'),fs=require('fs');\n" +
            "const src=process.argv[2];\n" +
            "let n=0;\n" +
            "const rs=fs.createReadStream(src);\n" +
            "const d=zlib.createBrotliDecompress();\n" +
            "rs.on('error',(e)=>{process.stderr.write('READ_ERR '+String((e&&e.message)||e));process.exit(3);});\n" +
            "d.on('error',(e)=>{process.stderr.write('DECODE_ERR '+String((e&&e.message)||e));process.exit(4);});\n" +
            "d.on('data',(c)=>{n+=c.length;});\n" +
            "d.on('end',()=>{process.stdout.write('RAW '+n);});\n" +
            "rs.pipe(d);\n";

        /// <summary>
        /// .data 파일(.br/.gz/무압축)의 압축 해제 후 바이트 수를 잰다.
        /// </summary>
        /// <param name="dataPath">.data 파일의 전체 경로.</param>
        /// <returns>압축 해제 크기(바이트). 측정할 수 없으면 -1(그러면 런타임 훅은 종전 동작으로 돌아간다). 예외를 던지지 않는다.</returns>
        internal static long Measure(string dataPath)
        {
            try
            {
                if (string.IsNullOrEmpty(dataPath) || !File.Exists(dataPath))
                {
                    Debug.LogWarning($"[AIT-DataRawSize] data 파일이 없어 측정을 건너뜁니다: {dataPath}");
                    return -1;
                }

                string name = Path.GetFileName(dataPath);
                var sw = Stopwatch.StartNew();
                long raw;
                DataEncoding encoding = Classify(name);
                switch (encoding)
                {
                    case DataEncoding.Unityweb:
                        // Decompression Fallback(.unityweb) 는 로더가 직접 풀므로 이 최적화 대상이 아니다.
                        return -1;
                    case DataEncoding.Brotli:
                        raw = MeasureBrotli(dataPath);
                        break;
                    case DataEncoding.Gzip:
                        raw = MeasureGzip(dataPath);
                        break;
                    default:
                        raw = new FileInfo(dataPath).Length;
                        break;
                }

                if (raw > 0)
                {
                    Debug.Log($"[AIT-DataRawSize] {name}: 압축 해제 크기 {raw}B ({encoding}, {sw.ElapsedMilliseconds}ms) — data 응답을 Content-Length={raw} 로 재포장합니다.");
                    return raw;
                }

                // 0 이하는 측정 실패와 같이 취급한다(빈 data 는 있을 수 없다).
                Debug.LogWarning($"[AIT-DataRawSize] {name}: 압축 해제 크기를 측정하지 못했습니다({encoding}) — data 재포장 없이 진행합니다.");
                return -1;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-DataRawSize] 측정 예외 — data 재포장 없이 진행합니다: {e.GetType().Name}: {e.Message}");
                return -1;
            }
        }

        /// <summary>파일명 접미사로 data 파일의 인코딩을 판정한다(대소문자 무시). 위치 구분 없이 끝 접미사만 본다.</summary>
        internal static DataEncoding Classify(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return DataEncoding.None;
            }

            if (fileName.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
            {
                return DataEncoding.Unityweb;
            }

            if (fileName.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            {
                return DataEncoding.Brotli;
            }

            if (fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                return DataEncoding.Gzip;
            }

            return DataEncoding.None;
        }

        /// <summary>Node 러너 stdout("RAW &lt;n&gt;")을 파싱한다. 형식이 다르거나 음수면 false.</summary>
        internal static bool TryParseRunnerOutput(string stdout, out long raw)
        {
            raw = -1;
            if (string.IsNullOrEmpty(stdout))
            {
                return false;
            }

            string s = stdout.Trim();
            if (!s.StartsWith(RunnerOutputPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (!long.TryParse(s.Substring(RunnerOutputPrefix.Length).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long n) || n < 0)
            {
                return false;
            }

            raw = n;
            return true;
        }

        private static long MeasureGzip(string path)
        {
            try
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                {
                    int read;
                    while ((read = gz.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                    }
                }

                return total;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-DataRawSize] gzip 해제 실패: {e.Message}");
                return -1;
            }
        }

        private static long MeasureBrotli(string path)
        {
            if (!AITBrotliCompressor.TryResolveNode(out string node))
            {
                Debug.LogWarning("[AIT-DataRawSize] 내장 Node 미가용 — brotli data 의 압축 해제 크기를 잴 수 없습니다.");
                return -1;
            }

            string runner = Path.Combine(Path.GetTempPath(), "ait-datarawsize-runner-" + Guid.NewGuid().ToString("N") + ".js");
            try
            {
                File.WriteAllText(runner, RunnerJs);

                var psi = new ProcessStartInfo
                {
                    FileName = node,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                psi.ArgumentList.Add(runner);
                psi.ArgumentList.Add(path);

                using (var p = new Process { StartInfo = psi })
                {
                    p.Start();

                    // stderr 는 비동기로 모아 stdout ReadToEnd 중 버퍼가 차서 교착하는 일을 막는다.
                    var errSb = new StringBuilder();
                    p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) { errSb.AppendLine(ev.Data); } };
                    p.BeginErrorReadLine();

                    string stdout = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(NodeTimeoutMs))
                    {
                        try { p.Kill(); } catch { /* 이미 종료됨 */ }
                        Debug.LogWarning("[AIT-DataRawSize] brotli 해제 측정 시간 초과.");
                        return -1;
                    }

                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        Debug.LogWarning($"[AIT-DataRawSize] brotli 해제 측정 실패(exit {p.ExitCode}): {errSb.ToString().Trim()}");
                        return -1;
                    }

                    return TryParseRunnerOutput(stdout, out long raw) ? raw : -1;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-DataRawSize] brotli 해제 측정 예외: {e.Message}");
                return -1;
            }
            finally
            {
                try { File.Delete(runner); } catch { /* 임시 파일 정리 실패 무시 */ }
            }
        }
    }
}
