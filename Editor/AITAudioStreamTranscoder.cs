// -----------------------------------------------------------------------
// <copyright file="AITAudioStreamTranscoder.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming audio transcoder (build-time)
// </copyright>
// -----------------------------------------------------------------------
//
// 오디오 스트리밍(AITAudioStreamingProcessor)이 StreamingAssets 로 외부화한 "사본"을
// 저비트레이트 MP3 로 재인코딩해 .ait 번들 크기를 줄인다. 외부화 스트림은 원본 소스
// 바이트 그대로라(고비트레이트 MP3 등) 번들 최대 단일 요소가 되기 쉽다.
//
// 설계 근거(2026-07 멀티에이전트 리서치 + 적대 검증):
//   - 컨테이너/코덱을 MP3 로 유지 → 런타임 복원 경로(UnityWebRequestMultimedia →
//     브라우저 미디어 엘리먼트) 불변, iOS WKWebView 호환성이 소스와 동일(재생 리스크 ≈ 0).
//     (Vorbis/Opus 는 iOS 18.4 미만 미지원/불안정, WebGL AssetBundle 은 AAC 강제
//     재인코딩 + 전체 메모리 로드라 기각.)
//   - 트랜스코딩 대상이 "외부화 사본"뿐이므로 프로젝트 원본은 구조적으로 비파괴.
//   - 인코딩은 순수 WASM(mpg123-decoder + wasm-media-encoders/LAME)을 SDK 내장
//     Node.js 로 실행 — AITBrotliCompressor(내장 Node)와 AITFontSubsetProcessor
//     (on-demand npm 설치) 의 기존 패턴 재사용. 도구 미가용 시 원본 사본 유지(기능
//     저하 없음 — 번들 크기만 종전과 동일).
//
// ⚠ 기본값 정책: cascaded lossy(320→160kbps 등)는 세대손실이 누적되고 루핑 BGM 의
//   LAME delay/padding 갭 리스크가 있어, 청취 검증 전까지 auto(-1)에서는 비활성이다
//   (AITDefaultSettings.GetDefaultAudioStreamTranscode() == false). 명시 활성(==1)
//   에서만 동작한다 — 다른 레버의 auto(opt-out) 철학과 다른 의도적 예외.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 외부화된 스트리밍 오디오 사본의 빌드타임 MP3 재인코더.
    /// <see cref="AITAudioStreamingProcessor"/> 가 외부화 직후 호출한다.
    /// </summary>
    internal static class AITAudioStreamTranscoder
    {
        /// <summary>도구 소스 디렉토리명(SDK 패키지 동봉, '~' 접미사라 Unity 미임포트).</summary>
        private const string ToolDirName = "AudioTranscode~";

        /// <summary>러너 파일명.</summary>
        private const string RunnerName = "audio-transcode-runner.mjs";

        /// <summary>일괄 트랜스코딩 타임아웃(ms). 실측 파일당 ~0.7초(디코드+인코드)라 여유 값.</summary>
        private const int TimeoutMs = 600000;

        /// <summary>임시 산출물 접미사(채택 판정 전 dst).</summary>
        private const string TmpSuffix = ".aittranscodetmp";

        /// <summary>
        /// 채택 임계(%): 재인코딩 산출물이 원본 사본보다 이 비율 이상 작을 때만 교체한다.
        /// 320→160kbps 는 실측 ~50% 절감이므로 정상 동작 시 여유 통과 — 미달은 이상 신호로
        /// 보고 원본을 유지한다(brotli 채택 게이트와 동일 사상, 임계는 lossy 라 더 높게).
        /// </summary>
        internal const int MinGainPercent = 25;

        /// <summary>트랜스코딩 대상 후보(외부화 사본 1건).</summary>
        internal struct Candidate
        {
            /// <summary>StreamingAssets 사본의 절대 경로.</summary>
            public string AbsPath;

            /// <summary>사본 바이트 수.</summary>
            public long Bytes;

            /// <summary>클립 실 길이(초). AudioClip.length 캡처값.</summary>
            public float Seconds;
        }

        /// <summary>파일 1건의 러너 결과. error 가 비어 있으면 성공.</summary>
        [Serializable]
        internal class Result
        {
            public int idx;
            public long raw;
            public long @out;
            public int srcKbps;
            public float durationSec;
            public string error;

            public bool Ok => string.IsNullOrEmpty(error) && raw > 0 && @out > 0;
        }

        [Serializable]
        private class Batch
        {
            public Result[] results;
        }

        // ─────────────────────────── 판정 (순수 함수, Level 0 테스트 대상) ───────────────────────────

        /// <summary>tri-state 해석. 명시 활성(==1)만 동작 — auto 는 청취 검증 전까지 비활성(헤더 주석 참조).</summary>
        internal static bool IsEnabled(AITEditorScriptObject config)
        {
            if (config == null)
            {
                return false;
            }

            return config.audioStreamTranscode >= 0
                ? config.audioStreamTranscode == 1
                : AITDefaultSettings.GetDefaultAudioStreamTranscode();
        }

        /// <summary>파일 크기/재생 길이에서 평균 비트레이트(kbps)를 추정한다. 길이가 무의미하면 0.</summary>
        internal static int EstimateKbps(long bytes, float seconds)
        {
            if (bytes <= 0 || seconds < 0.5f)
            {
                return 0;
            }

            return (int)Math.Round(bytes * 8.0 / seconds / 1000.0);
        }

        /// <summary>
        /// 재인코딩 대상 판정: 소스 평균 비트레이트가 minSourceKbps 이상이고 target 보다
        /// 실질적으로 높을 때만. (target 근처 소스를 재인코딩하면 세대손실만 남는다.)
        /// </summary>
        internal static bool ShouldTranscode(long bytes, float seconds, int minSourceKbps, int targetKbps)
        {
            int kbps = EstimateKbps(bytes, seconds);
            if (kbps <= 0)
            {
                return false;
            }

            // 하한 방어: minSourceKbps 가 target 이하로 잘못 설정돼도 target+32 미만 소스는 제외.
            int floor = Math.Max(minSourceKbps, targetKbps + 32);
            return kbps >= floor;
        }

        /// <summary>채택 판정: 산출물이 원본 대비 MinGainPercent 이상 작아야 교체.</summary>
        internal static bool ShouldAdopt(long rawBytes, long outBytes)
        {
            return AITBrotliCompressor.ShouldKeep(rawBytes, outBytes, MinGainPercent);
        }

        /// <summary>목표 비트레이트 해석(96~320 클램프, 비정상 값은 기본 160).</summary>
        internal static int ResolveTargetKbps(AITEditorScriptObject config)
        {
            int v = config != null ? config.audioStreamTranscodeBitrateKbps : 0;
            if (v <= 0)
            {
                return 160;
            }

            return Math.Max(96, Math.Min(320, v));
        }

        /// <summary>소스 최소 비트레이트 게이트 해석(비정상 값은 기본 256).</summary>
        internal static int ResolveMinSourceKbps(AITEditorScriptObject config)
        {
            int v = config != null ? config.audioStreamTranscodeMinSourceKbps : 0;
            return v > 0 ? v : 256;
        }

        // ─────────────────────────── 실행 ───────────────────────────

        /// <summary>
        /// 후보 사본들을 판정해 대상만 일괄 재인코딩하고, 채택 게이트를 통과한 산출물로
        /// 사본을 제자리 교체한다(파일명/매니페스트 불변). 실패는 파일 단위로 격리되며
        /// 어떤 실패에서도 원본 사본이 유지된다. 반환: 교체된 파일 수.
        /// </summary>
        internal static int TranscodeInPlace(AITEditorScriptObject config, IReadOnlyList<Candidate> candidates)
        {
            if (!IsEnabled(config) || candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            int targetKbps = ResolveTargetKbps(config);
            int minSourceKbps = ResolveMinSourceKbps(config);

            var targets = new List<Candidate>();
            foreach (var c in candidates)
            {
                if (!string.Equals(Path.GetExtension(c.AbsPath), ".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // v1 은 MP3 전용(런타임 경로 불변 보장). WAV/OGG 는 후속.
                }

                if (ShouldTranscode(c.Bytes, c.Seconds, minSourceKbps, targetKbps))
                {
                    targets.Add(c);
                }
            }

            if (targets.Count == 0)
            {
                Debug.Log($"[AIT-AudioTranscode] 재인코딩 대상 없음(≥{minSourceKbps}kbps MP3 스트림 없음) → no-op.");
                return 0;
            }

            if (!EnsureTool(out string node, out string runner))
            {
                Debug.LogWarning("[AIT-AudioTranscode] 도구 준비 실패 — 스트림 사본을 원본 그대로 유지합니다.");
                return 0;
            }

            var results = RunBatch(node, runner, targets, targetKbps);
            int adopted = 0;
            long savedBytes = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                string src = targets[i].AbsPath;
                string tmp = src + TmpSuffix;
                try
                {
                    Result r = results != null && results.TryGetValue(i, out var rr) ? rr : null;
                    if (r == null || !r.Ok || !File.Exists(tmp))
                    {
                        string why = r == null ? "결과 없음" : (string.IsNullOrEmpty(r.error) ? "산출물 없음" : r.error);
                        Debug.LogWarning($"[AIT-AudioTranscode]   실패(원본 유지) {Path.GetFileName(src)}: {why}");
                        continue;
                    }

                    if (!ShouldAdopt(r.raw, r.@out))
                    {
                        Debug.LogWarning($"[AIT-AudioTranscode]   절감 미달(원본 유지) {Path.GetFileName(src)}: {r.raw / 1048576f:0.00}→{r.@out / 1048576f:0.00}MB (<{MinGainPercent}%)");
                        continue;
                    }

                    File.Delete(src);
                    File.Move(tmp, src);
                    adopted++;
                    savedBytes += r.raw - r.@out;
                    Debug.Log($"[AIT-AudioTranscode]   재인코딩 {Path.GetFileName(src)}: {r.srcKbps}→{targetKbps}kbps, {r.raw / 1048576f:0.00}→{r.@out / 1048576f:0.00}MB");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch
                    {
                        // 임시 산출물 정리 실패는 무시(다음 빌드에서 streamroot 째로 제거됨).
                    }
                }
            }

            Debug.Log($"[AIT-AudioTranscode] ✓ 스트림 오디오 {adopted}/{targets.Count}개 재인코딩({targetKbps}kbps CBR), {savedBytes / 1048576f:0.0}MB 절감");
            return adopted;
        }

        // ─────────────────────────── 도구 준비 (FontSubset 패턴) ───────────────────────────

        /// <summary>내장 Node.js + 러너/의존성을 준비한다. 미설치 시 on-demand npm install.</summary>
        private static bool EnsureTool(out string nodeExe, out string runnerPath)
        {
            nodeExe = null;
            runnerPath = null;
            try
            {
                if (!AITBrotliCompressor.TryResolveNode(out string node))
                {
                    return false;
                }

                string srcDir = ResolveToolSourceDir();
                if (string.IsNullOrEmpty(srcDir) || !File.Exists(Path.Combine(srcDir, RunnerName)))
                {
                    Debug.LogWarning($"[AIT-AudioTranscode] 러너 소스 디렉토리를 찾지 못했습니다: '{srcDir}'");
                    return false;
                }

                string homeTool = GetHomeToolDir();
                Directory.CreateDirectory(homeTool);

                // 러너/매니페스트는 항상 최신본으로 동기화(SDK 업데이트 반영).
                File.Copy(Path.Combine(srcDir, RunnerName), Path.Combine(homeTool, RunnerName), true);
                File.Copy(Path.Combine(srcDir, "package.json"), Path.Combine(homeTool, "package.json"), true);

                // 의존성 미설치 시 1회 설치(on-demand, FontSubset 과 동일 철학).
                string installedDecoder = Path.Combine(homeTool, "node_modules", "mpg123-decoder", "package.json");
                string installedEncoder = Path.Combine(homeTool, "node_modules", "wasm-media-encoders", "package.json");
                if (!File.Exists(installedDecoder) || !File.Exists(installedEncoder))
                {
                    Debug.Log("[AIT-AudioTranscode] 트랜스코더 최초 설치 중(내장 npm)...");
                    string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
                    if (string.IsNullOrEmpty(npm) || !RunNpmInstall(npm, Path.GetDirectoryName(npm), homeTool)
                        || !File.Exists(installedDecoder) || !File.Exists(installedEncoder))
                    {
                        Debug.LogWarning("[AIT-AudioTranscode] 트랜스코더 설치 실패.");
                        return false;
                    }
                }

                nodeExe = node;
                runnerPath = Path.Combine(homeTool, RunnerName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] 도구 준비 예외: {e.Message}");
                return false;
            }
        }

        private static string GetHomeToolDir()
        {
            string basePath = AITPlatformHelper.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(basePath, ".ait-unity-sdk", "audio-transcode");
        }

        /// <summary>SDK 패키지에 동봉된 러너 소스 디렉토리. UPM/embedded 설치 모두 해석.</summary>
        private static string ResolveToolSourceDir()
        {
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AITAudioStreamTranscoder).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    return Path.Combine(pkg.resolvedPath, "Editor", ToolDirName);
                }
            }
            catch
            {
                // PackageInfo 미해석(Assets 내 임베드 개발) → 소스 파일 위치 폴백
            }

            string here = CallerDir();
            return string.IsNullOrEmpty(here) ? null : Path.Combine(here, ToolDirName);
        }

        private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

        private static bool RunNpmInstall(string npm, string nodeBin, string cwd)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                if (AITPlatformHelper.IsWindows)
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c \"\"{npm}\" install --no-audit --no-fund --loglevel=error\"";
                }
                else
                {
                    psi.FileName = npm;
                    psi.Arguments = "install --no-audit --no-fund --loglevel=error";
                }

                string sep = AITPlatformHelper.IsWindows ? ";" : ":";
                string existing = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                psi.EnvironmentVariables["PATH"] = nodeBin + sep + existing;

                using (var p = new Process { StartInfo = psi })
                {
                    p.Start();
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(180000))
                    {
                        try { p.Kill(); } catch { /* 이미 종료됨 */ }
                        return false;
                    }

                    return p.ExitCode == 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] npm install 예외: {e.Message}");
                return false;
            }
        }

        // ─────────────────────────── 러너 실행 (Brotli 패턴) ───────────────────────────

        /// <summary>대상 목록을 러너에 일괄 위임. 반환: 대상 인덱스 → Result (실패 시 빈 딕셔너리).</summary>
        private static Dictionary<int, Result> RunBatch(string node, string runner, IReadOnlyList<Candidate> targets, int targetKbps)
        {
            var map = new Dictionary<int, Result>();
            try
            {
                var input = new StringBuilder();
                input.Append("{\"targetKbps\":").Append(targetKbps).Append(",\"files\":[");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0)
                    {
                        input.Append(',');
                    }

                    input.Append("{\"idx\":").Append(i)
                         .Append(",\"src\":").Append(JsonStr(targets[i].AbsPath))
                         .Append(",\"dst\":").Append(JsonStr(targets[i].AbsPath + TmpSuffix)).Append('}');
                }

                input.Append("]}");

                if (!RunNode(node, runner, input.ToString(), out string stdout, out string stderr))
                {
                    Debug.LogWarning($"[AIT-AudioTranscode] 일괄 재인코딩 실행 실패 — 원본 유지: {Truncate(stderr ?? stdout)}");
                    return map;
                }

                var batch = UnityEngine.JsonUtility.FromJson<Batch>(stdout);
                if (batch?.results == null)
                {
                    Debug.LogWarning("[AIT-AudioTranscode] 재인코딩 결과 파싱 실패 — 원본 유지.");
                    return map;
                }

                foreach (var r in batch.results)
                {
                    if (r != null && r.idx >= 0 && r.idx < targets.Count)
                    {
                        map[r.idx] = r;
                    }
                }

                return map;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] 재인코딩 예외 — 원본 유지: {e.Message}");
                map.Clear();
                return map;
            }
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
                WorkingDirectory = Path.GetDirectoryName(runner),
            };
            psi.ArgumentList.Add(runner);

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();

                // stderr 는 이벤트로 비동기 수집 — stdout ReadToEnd 중 stderr 버퍼 교착 방지.
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
