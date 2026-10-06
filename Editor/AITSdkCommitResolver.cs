using System;
using System.Diagnostics;
using System.IO;
using AppsInToss;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 릴리즈 태그(package.json description) / UPM git.hash / packages-lock 어디에도 커밋 해시가
    /// 없는 로컬·embedded 설치를 위한 최후 폴백. SDK 패키지 자신의 git 저장소에서 직접 조회한다.
    ///
    /// <para><see cref="AITConvertCore"/>의 GetGitCommitHash()와 절대 혼동하지 말 것 — 그 함수는
    /// 명시적으로 "프로젝트(게임)" 커밋을 얻는 함수이며 이 클래스와 무관하다. 이 클래스는 SDK 패키지
    /// 경로가 게임 프로젝트와 "별도의" git 저장소일 때만 값을 반환하고, 같은 저장소이면(즉 SDK가
    /// 프로젝트에 파일로 커밋되어 있어 구분 불가능하면) null을 반환해 프로젝트 커밋을 SDK 커밋인 양
    /// 넣는 실수를 방지한다.</para>
    /// </summary>
    internal static class AITSdkCommitResolver
    {
        /// <summary>
        /// SDK 패키지 경로의 git 저장소에서 short 커밋 해시를 조회한다.
        /// 조회 불가(비-git, git 미설치/실패, 게임 프로젝트와 동일 저장소)면 <c>null</c>.
        /// 계약: 절대 빈 문자열을 반환하지 않는다 (null 또는 비어있지 않은 hex).
        /// </summary>
        internal static string TryResolveLocalGitCommitHash()
        {
            try
            {
                string sdkPath = AITPackagePathResolver.GetSDKResolvedPath();
                if (string.IsNullOrEmpty(sdkPath) || !Directory.Exists(sdkPath))
                    return null;

                string sdkToplevel = RunGit(sdkPath, "rev-parse --show-toplevel");
                if (string.IsNullOrEmpty(sdkToplevel))
                    return null; // SDK 경로가 git 저장소가 아님 (tarball/registry 추출본 등)

                string projectToplevel = RunGit(UnityUtil.GetProjectPath(), "rev-parse --show-toplevel");
                if (!string.IsNullOrEmpty(projectToplevel)
                    && string.Equals(Normalize(sdkToplevel), Normalize(projectToplevel),
                        StringComparison.OrdinalIgnoreCase))
                {
                    // SDK가 게임 프로젝트와 동일 git 저장소 안에 있음 → git만으로는 SDK 고유 커밋을
                    // 프로젝트 커밋과 구분할 수 없다. 틀린 값을 넣느니 비워 둔다.
                    return null;
                }

                string hash = RunGit(sdkToplevel, "rev-parse --short=7 HEAD");
                return string.IsNullOrEmpty(hash) ? null : hash;
            }
            catch (Exception e)
            {
                // 로컬/embedded 설치에서 git 부재·실패는 흔한 정상 케이스 → Sentry 노이즈 억제.
                AITLog.Warning($"[AIT] SDK 커밋 해시 로컬 조회 실패 (무시됨): {e.Message}", sentryCapture: false);
                return null;
            }
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path).TrimEnd('/', '\\');
        }

        /// <summary>
        /// 지정 디렉토리에서 git 명령을 실행하고 표준출력(trim)을 반환한다.
        /// 5초 내 종료하지 않으면 프로세스를 종료하고 <c>null</c>. exit code가 0이 아니어도 <c>null</c>.
        /// stderr는 리다이렉트하지 않는다(파이프 미소비로 인한 교착 방지 — <see cref="AITConvertCore"/>
        /// 의 git 호출과 동일한 방식).
        /// </summary>
        private static string RunGit(string workingDirectory, string arguments)
        {
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                bool exited = process.WaitForExit(5000);
                if (!exited)
                {
                    try { process.Kill(); } catch { /* 이미 종료됐을 수 있음 — 무시 */ }
                    return null;
                }
                return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
            }
        }
    }
}
