// -----------------------------------------------------------------------
// <copyright file="AITWasmSections.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - wasm section size reader (build-time diagnostics)
// </copyright>
// -----------------------------------------------------------------------
//
// wasm 바이너리의 섹션별 크기를 읽는다(P0-5 진단: LTO 효과를 "코드 섹션 바이트"로 비교하려는 용도).
// 압축 파일 크기는 압축기·품질에 따라 흔들리므로, 해제한 wasm 의 code 섹션(id 10) 페이로드 크기를 기준 지표로 쓴다.
// .br/.gz/.unityweb 는 내장 Node(zlib)로 임시 파일에 풀어서 읽는다 — Editor mono 에는 brotli 디코더가 없다(AITBrotliCompressor 와 같은 사유).
// 진단 전용이다: 어떤 실패도 예외로 번지지 않고 false 를 돌려주며, 빌드 결과에는 영향을 주지 않는다.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AppsInToss.Editor
{
    internal static class AITWasmSections
    {
        /// <summary>wasm 섹션 id: custom=0, type=1, import=2, function=3, table=4, memory=5, global=6, export=7, start=8, element=9, code=10, data=11, data count=12.</summary>
        internal const byte SectionCustom = 0;
        internal const byte SectionCode = 10;
        internal const byte SectionData = 11;

        private const int NodeTimeoutMs = 120000;

        // 압축 파일을 임시 파일에 푼다. gzip 매직(1f 8b)이면 gunzip, wasm 매직(00 61 73 6d)이면 그대로, 그 외는 brotli 로 본다.
        // process.argv 는 `node -e <script> <in> <out>` 에서 [execPath, <in>, <out>] 이다.
        private const string DecompressScript =
            "const fs=require('fs'),z=require('zlib');" +
            "const b=fs.readFileSync(process.argv[1]);let o;" +
            "if(b.length>=2&&b[0]===0x1f&&b[1]===0x8b)o=z.gunzipSync(b);" +
            "else if(b.length>=4&&b[0]===0&&b[1]===0x61&&b[2]===0x73&&b[3]===0x6d)o=b;" +
            "else o=z.brotliDecompressSync(b);" +
            "fs.writeFileSync(process.argv[2],o);";

        /// <summary>섹션 크기 요약. 크기는 섹션 페이로드 바이트(id 와 size 헤더 제외)의 합이다.</summary>
        internal struct Sizes
        {
            /// <summary>해제한 wasm 전체 바이트.</summary>
            public long totalBytes;

            /// <summary>code 섹션(id 10) 페이로드 바이트 — 함수 본문 전체.</summary>
            public long codeBytes;

            /// <summary>data 섹션(id 11) 페이로드 바이트.</summary>
            public long dataBytes;

            /// <summary>custom 섹션(id 0: name, producers 등) 페이로드 바이트 합.</summary>
            public long customBytes;

            public int sectionCount;
        }

        /// <summary>
        /// wasm 바이트를 훑어 섹션 크기를 모은다. 매직/버전이 틀리거나 섹션 길이가 파일 끝을 넘으면 false.
        /// 섹션 본문은 해석하지 않고 건너뛴다(id + LEB128 size 만 읽는다).
        /// </summary>
        internal static bool TryParse(byte[] wasm, out Sizes sizes)
        {
            sizes = default(Sizes);
            if (wasm == null || wasm.Length < 8) return false;
            if (wasm[0] != 0x00 || wasm[1] != 0x61 || wasm[2] != 0x73 || wasm[3] != 0x6D) return false;
            if (wasm[4] != 0x01 || wasm[5] != 0x00 || wasm[6] != 0x00 || wasm[7] != 0x00) return false;

            var result = new Sizes { totalBytes = wasm.Length };
            int pos = 8;
            while (pos < wasm.Length)
            {
                byte id = wasm[pos++];
                if (!TryReadU32(wasm, ref pos, out uint size)) return false;
                if ((long)size > wasm.Length - pos) return false;

                switch (id)
                {
                    case SectionCode: result.codeBytes += size; break;
                    case SectionData: result.dataBytes += size; break;
                    case SectionCustom: result.customBytes += size; break;
                }

                result.sectionCount++;
                pos += (int)size;
            }

            sizes = result;
            return true;
        }

        /// <summary>LEB128 부호 없는 32비트 정수(최대 5바이트). 끝을 넘거나 5바이트를 넘기면 false.</summary>
        internal static bool TryReadU32(byte[] data, ref int pos, out uint value)
        {
            value = 0;
            int shift = 0;
            for (int i = 0; i < 5; i++)
            {
                if (pos >= data.Length) return false;
                byte b = data[pos++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
            }

            return false;
        }

        /// <summary>
        /// 빌드 산출물의 wasm(.wasm / .wasm.br / .wasm.gz / .wasm.unityweb)을 읽어 섹션 크기를 구한다.
        /// 압축 파일은 내장 Node 로 임시 파일에 풀어 읽고 바로 지운다. 실패하면 false 와 사유(<paramref name="error"/>).
        /// </summary>
        internal static bool TryMeasureFile(string path, out Sizes sizes, out string error)
        {
            sizes = default(Sizes);
            error = null;
            string temp = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    error = "파일 없음";
                    return false;
                }

                string readPath = path;
                if (!path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
                {
                    temp = Path.Combine(Path.GetTempPath(), "ait-wasm-sections-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N") + ".wasm");
                    if (!TryDecompressWithNode(path, temp, out error)) return false;
                    readPath = temp;
                }

                if (!TryParse(File.ReadAllBytes(readPath), out sizes))
                {
                    error = "wasm 형식이 아님(매직/섹션 길이 불일치)";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
            finally
            {
                if (temp != null)
                {
                    try { File.Delete(temp); } catch (Exception) { /* 임시 파일 정리 실패는 무시 */ }
                }
            }
        }

        private static bool TryDecompressWithNode(string src, string dst, out string error)
        {
            error = null;
            if (!AITBrotliCompressor.TryResolveNode(out string node))
            {
                error = "내장 Node 를 찾을 수 없음";
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = node,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add(DecompressScript);
            psi.ArgumentList.Add(src);
            psi.ArgumentList.Add(dst);

            using (var p = new Process { StartInfo = psi })
            {
                var err = new StringBuilder();
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) err.AppendLine(ev.Data); };
                p.Start();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(NodeTimeoutMs))
                {
                    try { p.Kill(); } catch (Exception) { /* 이미 종료됨 */ }
                    error = "node 해제 시간 초과";
                    return false;
                }

                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    string msg = err.ToString().Trim();
                    error = "node 해제 실패(exit " + p.ExitCode + "): " + (msg.Length > 200 ? msg.Substring(0, 200) : msg);
                    return false;
                }
            }

            if (!File.Exists(dst))
            {
                error = "node 해제 산출물 없음";
                return false;
            }

            return true;
        }
    }
}
