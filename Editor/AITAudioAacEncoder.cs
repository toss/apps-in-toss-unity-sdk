// -----------------------------------------------------------------------
// AITAudioAacEncoder.cs - 스트림 오디오 사본의 PCM WAV → AAC-LC(.m4a) 빌드타임 인코더 (P0-6)
//
// 왜 필요한가:
//   외부화 오디오 중 압축 재생 경로(streamAudio=false, compressed=true)로 가는 PCM WAV 는 원본 바이트 그대로
//   wasm 힙에 남고(FMOD 사본) media element 용 Blob 사본이 따로 생긴다(60초 스테레오 WAV = 10.6MB x 2).
//   AAC-LC 160kbps 로 바꾸면 60초 기준 약 1.2MB 라 두 사본 모두 10분의 1 로 줄어든다.
//
// 왜 Node 러너가 아니라 시스템 인코더인가:
//   AudioTranscode~ 러너는 순수 WASM(mpg123 디코더 + LAME 인코더)이라 MP3 전용이다. AAC 인코더를 순수 WASM 으로 쓰려면
//   ffmpeg.wasm(수십 MB npm 의존성) 같은 무거운 의존성이 필요해서, 빌드 머신에 이미 있는 ffmpeg(전 플랫폼) 또는
//   afconvert(macOS 기본 탑재)를 외부 프로세스로 부른다. 둘 다 없으면 호출부가 경고만 남기고 WAV 사본을 그대로 둔다.
//
// 이 파일은 도구 탐색·인자 구성·WAV 헤더 판정(순수 함수, EditMode 테스트 대상)과 프로세스 실행만 맡는다.
// 대상 선정·채택 판정·매니페스트 반영은 AITAudioStreamTranscoder / AITAudioStreamingProcessor 몫이다.
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
    /// 시스템 ffmpeg/afconvert 로 PCM WAV 를 AAC-LC(.m4a) 로 인코딩한다. 실패는 예외 대신 false 로 보고한다(원본 유지).
    /// </summary>
    internal static class AITAudioAacEncoder
    {
        internal enum Tool
        {
            None,
            Ffmpeg,
            Afconvert,
        }

        /// <summary>ffmpeg 경로를 직접 지정하는 환경 변수(빌드 머신 PATH 에 없을 때).</summary>
        internal const string FfmpegPathEnvVar = "AIT_FFMPEG_PATH";

        /// <summary>인코더 1건 타임아웃(ms). 60초 스테레오 클립이 수 초라 넉넉한 값.</summary>
        private const int TimeoutMs = 180000;

        /// <summary>WAV fmt 청크가 들어 있는 앞부분만 읽는다(헤더 판정용).</summary>
        internal const int HeaderProbeBytes = 4096;

        /// <summary>WAV 헤더 판정 결과.</summary>
        internal struct WavInfo
        {
            /// <summary>유효 포맷 태그. 1 = 정수 PCM, 3 = float PCM(WAVE_FORMAT_EXTENSIBLE 은 서브포맷으로 풀어 담는다).</summary>
            public int FormatTag;

            public int Channels;
            public int SampleRate;

            public bool IsPcm => FormatTag == 1 || FormatTag == 3;
        }

        // ─────────────────────────── 순수 함수 (테스트 대상) ───────────────────────────

        /// <summary>
        /// RIFF/WAVE 앞부분 바이트에서 fmt 청크를 찾아 포맷을 해석한다. 범위를 모두 검사하며 못 찾으면 false.
        /// ADPCM(2)·A-law(6)·mu-law(7) 같은 이미 압축된 WAV 는 FormatTag 가 1/3 이 아니라 IsPcm 이 false 다.
        /// </summary>
        internal static bool TryParseWavHeader(byte[] head, out WavInfo info)
        {
            info = default;
            if (head == null || head.Length < 20
                || !Tag(head, 0, "RIFF") || !Tag(head, 8, "WAVE"))
            {
                return false;
            }

            int pos = 12;
            while (pos + 8 <= head.Length)
            {
                uint size = ReadU32(head, pos + 4);
                if (Tag(head, pos, "fmt "))
                {
                    int body = pos + 8;
                    if (size < 16 || body + 16 > head.Length)
                    {
                        return false;
                    }

                    int tag = ReadU16(head, body);
                    info.Channels = ReadU16(head, body + 2);
                    info.SampleRate = (int)ReadU32(head, body + 4);
                    if (tag == 0xFFFE)
                    {
                        // WAVE_FORMAT_EXTENSIBLE: fmt 본문 24 바이트째부터 SubFormat GUID, 앞 2 바이트가 실제 포맷 태그.
                        if (size < 40 || body + 26 > head.Length)
                        {
                            return false;
                        }

                        tag = ReadU16(head, body + 24);
                    }

                    info.FormatTag = tag;
                    return info.Channels > 0 && info.SampleRate > 0;
                }

                long next = (long)pos + 8 + size + (size & 1);
                if (next > head.Length)
                {
                    return false;
                }

                pos = (int)next;
            }

            return false;
        }

        /// <summary>파일 앞부분만 읽어 WAV 헤더를 해석한다. 읽기 실패는 false.</summary>
        internal static bool TryReadWavInfo(string path, out WavInfo info)
        {
            info = default;
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var buf = new byte[HeaderProbeBytes];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n < buf.Length)
                    {
                        Array.Resize(ref buf, n);
                    }

                    return TryParseWavHeader(buf, out info);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 채널 수에 맞는 AAC 비트레이트(kbps). 스테레오는 요청값 그대로, 모노는 같은 음질에 필요한 양이 더 작아
        /// 5/8(하한 64)로 줄인다. 요청값이 비정상이면 160.
        /// </summary>
        internal static int AacBitrateKbps(int requestedKbps, int channels)
        {
            int target = requestedKbps > 0 ? requestedKbps : 160;
            return channels <= 1 ? Math.Max(64, target * 5 / 8) : target;
        }

        /// <summary>ISO BMFF("ftyp" 박스)로 보이는지. 인코더가 에러 텍스트나 빈 파일을 남긴 경우를 거른다.</summary>
        internal static bool LooksLikeMp4(byte[] head)
        {
            return head != null && head.Length >= 12 && Tag(head, 4, "ftyp");
        }

        /// <summary>ffmpeg 인자. -f mp4 를 명시해 임시 확장자(.aittranscodetmp)에서도 컨테이너를 고정한다. 태그는 버린다.</summary>
        internal static List<string> BuildFfmpegArgs(string src, string dst, int kbps)
        {
            return new List<string>
            {
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-i", src,
                "-vn", "-map_metadata", "-1",
                "-c:a", "aac", "-b:a", kbps + "k",
                "-movflags", "+faststart",
                "-f", "mp4",
                dst,
            };
        }

        /// <summary>afconvert 인자(macOS). m4af 컨테이너 + AAC-LC.</summary>
        internal static List<string> BuildAfconvertArgs(string src, string dst, int kbps)
        {
            return new List<string>
            {
                "-f", "m4af", "-d", "aac", "-b", (kbps * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture),
                src, dst,
            };
        }

        /// <summary>
        /// ffmpeg 후보 경로를 우선순위대로 나열한다(존재 여부는 호출부가 확인). 에디터가 Finder/Hub 에서 떠서 PATH 가
        /// 최소일 수 있으므로 흔한 설치 위치를 함께 본다.
        /// </summary>
        internal static List<string> EnumerateFfmpegCandidates(string envOverride, string pathEnv, bool isWindows)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(envOverride))
            {
                list.Add(envOverride);
            }

            string exe = isWindows ? "ffmpeg.exe" : "ffmpeg";
            char sep = isWindows ? ';' : ':';
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (string dir in pathEnv.Split(sep))
                {
                    string d = dir.Trim().Trim('"');
                    if (d.Length > 0)
                    {
                        list.Add(Path.Combine(d, exe));
                    }
                }
            }

            if (!isWindows)
            {
                list.Add("/opt/homebrew/bin/ffmpeg");
                list.Add("/usr/local/bin/ffmpeg");
                list.Add("/usr/bin/ffmpeg");
            }

            return list;
        }

        // ─────────────────────────── 도구 탐색·실행 ───────────────────────────

        /// <summary>사용 가능한 AAC 인코더를 찾는다. ffmpeg 우선, 없으면 macOS 의 afconvert. 없으면 false.</summary>
        internal static bool TryFindTool(out Tool tool, out string toolPath)
        {
            tool = Tool.None;
            toolPath = null;
            try
            {
                foreach (string c in EnumerateFfmpegCandidates(
                             Environment.GetEnvironmentVariable(FfmpegPathEnvVar),
                             Environment.GetEnvironmentVariable("PATH"),
                             AITPlatformHelper.IsWindows))
                {
                    if (File.Exists(c))
                    {
                        tool = Tool.Ffmpeg;
                        toolPath = c;
                        return true;
                    }
                }

                const string afconvert = "/usr/bin/afconvert";
                if (!AITPlatformHelper.IsWindows && File.Exists(afconvert))
                {
                    tool = Tool.Afconvert;
                    toolPath = afconvert;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] AAC 인코더 탐색 예외: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// src(PCM WAV)를 dst(AAC-LC mp4)로 인코딩한다. 성공은 종료 코드 0 + 산출물이 ISO BMFF 로 보일 때만이다.
        /// 실패하면 dst 를 지우고 false 를 돌려주며 사유는 error 에 담는다.
        /// </summary>
        internal static bool TryEncode(Tool tool, string toolPath, string src, string dst, int kbps, out string error)
        {
            error = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = toolPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                List<string> args = tool == Tool.Ffmpeg
                    ? BuildFfmpegArgs(src, dst, kbps)
                    : BuildAfconvertArgs(src, dst, kbps);
                foreach (string a in args)
                {
                    psi.ArgumentList.Add(a);
                }

                var err = new StringBuilder();
                using (var p = new Process { StartInfo = psi })
                {
                    p.Start();
                    p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) { err.AppendLine(ev.Data); } };
                    p.OutputDataReceived += (_, ev) => { };
                    p.BeginErrorReadLine();
                    p.BeginOutputReadLine();
                    if (!p.WaitForExit(TimeoutMs))
                    {
                        try { p.Kill(); } catch { /* 이미 종료됨 */ }
                        error = "시간 초과";
                        TryDelete(dst);
                        return false;
                    }

                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        error = "종료 코드 " + p.ExitCode + ": " + Truncate(err.ToString());
                        TryDelete(dst);
                        return false;
                    }
                }

                if (!File.Exists(dst) || new FileInfo(dst).Length <= 0)
                {
                    error = "산출물 없음";
                    return false;
                }

                var head = new byte[12];
                using (var fs = File.OpenRead(dst))
                {
                    fs.Read(head, 0, head.Length);
                }

                if (!LooksLikeMp4(head))
                {
                    error = "산출물이 mp4 컨테이너가 아님";
                    TryDelete(dst);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                TryDelete(dst);
                return false;
            }
        }

        // ─────────────────────────── 내부 ───────────────────────────

        private static bool Tag(byte[] b, int off, string tag)
        {
            if (off + 4 > b.Length)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (b[off + i] != (byte)tag[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int ReadU16(byte[] b, int off) => b[off] | (b[off + 1] << 8);

        private static uint ReadU32(byte[] b, int off) =>
            (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 임시 산출물 정리 실패는 무시(다음 빌드에서 streamroot 째로 제거됨).
            }
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "(출력 없음)";
            }

            s = s.Trim();
            return s.Length <= 300 ? s : s.Substring(0, 300) + "...";
        }
    }
}
