// -----------------------------------------------------------------------
// AITTextureStreamRecompressor.cs - 외부화된 스트리밍 텍스처 PNG 사본의 빌드타임 무손실 재압축
//
// 대상: AITLargeTextureExternalizer 가 StreamingAssets 에 만든 PNG 사본(프로젝트 원본 아님).
//   다운스케일(3-1) '이후', brotli(3-2) '이전'에 실행 — 다운스케일이 EncodeToPNG 로 다시 쓴
//   무최적화 deflate 산출물(실측 −32~34%)과 원본 소스 PNG(실측 −7~16%)를 함께 누른다.
//
// 무손실: oxipng(WASM) 는 픽셀 데이터 불변, 필터/deflate 재탐색만 수행 —
//   런타임 Texture2D.LoadImage 결과가 동일하므로 청취/시각 검증 게이트가 불필요하다.
//   따라서 auto(-1) 기본 활성(GetDefaultTextureStreamRecompress() == true).
//
// 실패 정책: AITAudioStreamTranscoder 와 동일 — 파일 단위 격리, 어떤 실패에서도 원본 사본 유지.
// 도구: FontSubset/AudioTranscode 패턴의 on-demand npm install (~/.ait-unity-sdk/png-recompress).
//   @jsquash/oxipng 2.3.0 (Apache-2.0, 순수 WASM) — 빌드 도구 전용, .ait 에 포함되지 않음.
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
    /// 외부화된 스트리밍 텍스처 PNG 사본의 빌드타임 무손실 재압축기.
    /// <see cref="AITLargeTextureExternalizer"/> 가 다운스케일 직후 호출한다.
    /// </summary>
    internal static class AITTextureStreamRecompressor
    {
        /// <summary>도구 소스 디렉토리명(SDK 패키지 동봉, '~' 접미사라 Unity 미임포트).</summary>
        private const string ToolDirName = "PngRecompress~";
        private const string RunnerName = "png-recompress-runner.mjs";
        private const int TimeoutMs = 600000;
        private const string TmpSuffix = ".aitpngtmp";

        /// <summary>oxipng 최적화 레벨. 2: 레벨 4 대비 이득 ~2%p 차이에 시간 절반(실측) — 빌드 시간 균형점.</summary>
        internal const int Level = 2;

        [Serializable]
        internal class Result
        {
            public int idx;
            public long raw;
            public long @out;
            public string error;

            public bool Ok => string.IsNullOrEmpty(error) && raw > 0 && @out > 0;
        }

        [Serializable]
        private class Batch
        {
            public Result[] results;
        }

        // ─────────────────────────── 판정 (순수 함수, Level 0 테스트 대상) ───────────────────────────

        /// <summary>tri-state 해석. 무손실이라 auto(-1) 기본 활성 — off(0) 만 명시 비활성.</summary>
        internal static bool IsEnabled(AITEditorScriptObject config)
        {
            if (config == null)
            {
                return false;
            }

            return config.textureStreamRecompress >= 0
                ? config.textureStreamRecompress == 1
                : AITDefaultSettings.GetDefaultTextureStreamRecompress();
        }

        /// <summary>채택 판정: 무손실이므로 1바이트라도 작으면 교체(품질 트레이드오프 없음).</summary>
        internal static bool ShouldAdopt(long rawBytes, long outBytes)
        {
            return rawBytes > 0 && outBytes > 0 && outBytes < rawBytes;
        }

        // ─────────────────────────── 실행 ───────────────────────────

        /// <summary>
        /// 스트림 PNG 사본들을 일괄 무손실 재압축해 작아진 산출물로 제자리 교체한다
        /// (파일명/매니페스트/런타임 경로 불변). 실패는 파일 단위로 격리되며 어떤 실패에서도
        /// 원본 사본이 유지된다. 반환: 교체된 파일 수.
        /// </summary>
        internal static int RecompressInPlace(AITEditorScriptObject config, IReadOnlyList<string> absPaths)
        {
            if (!IsEnabled(config) || absPaths == null || absPaths.Count == 0)
            {
                return 0;
            }

            var targets = new List<string>();
            foreach (var p in absPaths)
            {
                if (!string.IsNullOrEmpty(p)
                    && string.Equals(Path.GetExtension(p), ".png", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(p))
                {
                    targets.Add(p);
                }
            }

            if (targets.Count == 0)
            {
                return 0;
            }

            if (!EnsureTool(out string node, out string runner))
            {
                Debug.LogWarning("[AIT-PngRecompress] 도구 준비 실패 — 스트림 사본을 원본 그대로 유지합니다.");
                return 0;
            }

            var results = RunBatch(node, runner, targets);
            int adopted = 0;
            long savedBytes = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                string src = targets[i];
                string tmp = src + TmpSuffix;
                try
                {
                    Result r = results != null && results.TryGetValue(i, out var rr) ? rr : null;
                    if (r == null || !r.Ok || !File.Exists(tmp))
                    {
                        string why = r == null ? "결과 없음" : (string.IsNullOrEmpty(r.error) ? "산출물 없음" : r.error);
                        Debug.LogWarning($"[AIT-PngRecompress]   실패(원본 유지) {Path.GetFileName(src)}: {why}");
                        continue;
                    }

                    if (!ShouldAdopt(r.raw, r.@out))
                    {
                        continue; // 이득 없음(이미 최적) — 무손실이라 경고 불필요, 조용히 원본 유지.
                    }

                    File.Delete(src);
                    File.Move(tmp, src);
                    adopted++;
                    savedBytes += r.raw - r.@out;
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

            Debug.Log($"[AIT-PngRecompress] ✓ 스트림 PNG {adopted}/{targets.Count}개 무손실 재압축(oxipng L{Level}), {savedBytes / 1048576f:0.0}MB 절감");
            return adopted;
        }

        // ─────────────────────────── 도구 준비 (AudioTranscode 패턴) ───────────────────────────

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
                    Debug.LogWarning($"[AIT-PngRecompress] 러너 소스 디렉토리를 찾지 못했습니다: '{srcDir}'");
                    return false;
                }

                string homeTool = GetHomeToolDir();
                Directory.CreateDirectory(homeTool);

                // 러너/매니페스트는 항상 최신본으로 동기화(SDK 업데이트 반영).
                File.Copy(Path.Combine(srcDir, RunnerName), Path.Combine(homeTool, RunnerName), true);
                File.Copy(Path.Combine(srcDir, "package.json"), Path.Combine(homeTool, "package.json"), true);

                // 의존성 미설치 시 1회 설치(on-demand).
                string installed = Path.Combine(homeTool, "node_modules", "@jsquash", "oxipng", "package.json");
                if (!File.Exists(installed))
                {
                    Debug.Log("[AIT-PngRecompress] 재압축기 최초 설치 중(내장 npm)...");
                    string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
                    if (string.IsNullOrEmpty(npm) || !RunNpmInstall(npm, Path.GetDirectoryName(npm), homeTool)
                        || !File.Exists(installed))
                    {
                        Debug.LogWarning("[AIT-PngRecompress] 재압축기 설치 실패.");
                        return false;
                    }
                }

                nodeExe = node;
                runnerPath = Path.Combine(homeTool, RunnerName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-PngRecompress] 도구 준비 예외: {e.Message}");
                return false;
            }
        }

        private static string GetHomeToolDir()
        {
            string basePath = AITPlatformHelper.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(basePath, ".ait-unity-sdk", "png-recompress");
        }

        /// <summary>SDK 패키지에 동봉된 러너 소스 디렉토리. UPM/embedded 설치 모두 해석.</summary>
        private static string ResolveToolSourceDir()
        {
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AITTextureStreamRecompressor).Assembly);
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
                Debug.LogWarning($"[AIT-PngRecompress] npm install 예외: {e.Message}");
                return false;
            }
        }

        // ─────────────────────────── 러너 실행 (Brotli 패턴) ───────────────────────────

        /// <summary>대상 목록을 러너에 일괄 위임. 반환: 대상 인덱스 → Result (실패 시 빈 딕셔너리).</summary>
        private static Dictionary<int, Result> RunBatch(string node, string runner, IReadOnlyList<string> targets)
        {
            var map = new Dictionary<int, Result>();
            try
            {
                var input = new StringBuilder();
                input.Append("{\"level\":").Append(Level).Append(",\"files\":[");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0)
                    {
                        input.Append(',');
                    }

                    input.Append("{\"idx\":").Append(i)
                         .Append(",\"src\":").Append(JsonStr(targets[i]))
                         .Append(",\"dst\":").Append(JsonStr(targets[i] + TmpSuffix)).Append('}');
                }

                input.Append("]}");

                if (!RunNode(node, runner, input.ToString(), out string stdout, out string stderr))
                {
                    Debug.LogWarning($"[AIT-PngRecompress] 일괄 재압축 실행 실패 — 원본 유지: {Truncate(stderr ?? stdout)}");
                    return map;
                }

                var batch = UnityEngine.JsonUtility.FromJson<Batch>(stdout);
                if (batch?.results == null)
                {
                    Debug.LogWarning("[AIT-PngRecompress] 재압축 결과 파싱 실패 — 원본 유지.");
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
                Debug.LogWarning($"[AIT-PngRecompress] 재압축 예외 — 원본 유지: {e.Message}");
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
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
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
            => string.IsNullOrEmpty(s) ? "(빈 출력)" : (s.Length <= 400 ? s : s.Substring(0, 400) + "…");
    }
}
