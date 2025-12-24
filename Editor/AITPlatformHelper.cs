using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 크로스 플랫폼 유틸리티 클래스
    /// Windows, macOS, Linux에서 동일하게 동작하는 헬퍼 메서드 제공
    /// </summary>
    public static class AITPlatformHelper
    {
        /// <summary>
        /// 현재 플랫폼 열거형
        /// </summary>
        public enum Platform
        {
            Windows,
            MacOS,
            Linux,
            Unknown
        }

        /// <summary>
        /// 현재 플랫폼 반환
        /// </summary>
        public static Platform CurrentPlatform
        {
            get
            {
#if UNITY_EDITOR_WIN
                return Platform.Windows;
#elif UNITY_EDITOR_OSX
                return Platform.MacOS;
#elif UNITY_EDITOR_LINUX
                return Platform.Linux;
#else
                return Platform.Unknown;
#endif
            }
        }

        /// <summary>
        /// Windows 플랫폼 여부
        /// </summary>
        public static bool IsWindows => CurrentPlatform == Platform.Windows;

        /// <summary>
        /// macOS 플랫폼 여부
        /// </summary>
        public static bool IsMacOS => CurrentPlatform == Platform.MacOS;

        /// <summary>
        /// Linux 플랫폼 여부
        /// </summary>
        public static bool IsLinux => CurrentPlatform == Platform.Linux;

        /// <summary>
        /// Unix 계열 (macOS 또는 Linux) 여부
        /// </summary>
        public static bool IsUnix => IsMacOS || IsLinux;

        /// <summary>
        /// 플랫폼별 실행 파일 확장자
        /// </summary>
        public static string ExecutableExtension => IsWindows ? ".exe" : "";

        /// <summary>
        /// 플랫폼별 배치/셸 스크립트 확장자
        /// </summary>
        public static string ScriptExtension => IsWindows ? ".cmd" : "";

        /// <summary>
        /// 플랫폼별 PATH 구분자
        /// </summary>
        public static char PathSeparator => IsWindows ? ';' : ':';

        /// <summary>
        /// 플랫폼별 표준 실행 파일 검색 경로
        /// </summary>
        public static string[] StandardBinPaths
        {
            get
            {
                if (IsWindows)
                {
                    return new string[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs"),
                        Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? "", "npm"),
                    };
                }
                else
                {
                    return new string[]
                    {
                        "/opt/homebrew/bin",     // Apple Silicon Mac (Homebrew)
                        "/usr/local/bin",        // Intel Mac (Homebrew) / Linux
                        "/usr/bin",              // System default
                        "/home/linuxbrew/.linuxbrew/bin", // Linux Homebrew
                    };
                }
            }
        }

        /// <summary>
        /// 플랫폼별 기본 PATH 환경변수 값
        /// </summary>
        public static string DefaultPathEnv
        {
            get
            {
                if (IsWindows)
                {
                    return Environment.GetEnvironmentVariable("PATH") ?? "";
                }
                else
                {
                    return "/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";
                }
            }
        }

        /// <summary>
        /// PATH 환경변수 문자열 생성
        /// </summary>
        /// <param name="paths">추가할 경로들</param>
        /// <returns>플랫폼에 맞는 PATH 문자열</returns>
        public static string BuildPathEnv(params string[] paths)
        {
            var validPaths = new System.Collections.Generic.List<string>();

            foreach (var path in paths)
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    validPaths.Add(path);
                }
            }

            // 기본 경로 추가
            if (IsWindows)
            {
                string systemPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!string.IsNullOrEmpty(systemPath))
                {
                    validPaths.Add(systemPath);
                }
            }
            else
            {
                validPaths.Add("/usr/local/bin");
                validPaths.Add("/usr/bin");
                validPaths.Add("/bin");
                validPaths.Add("/opt/homebrew/bin");
            }

            return string.Join(PathSeparator.ToString(), validPaths);
        }

        /// <summary>
        /// 실행 파일 전체 이름 반환 (플랫폼별 확장자 포함)
        /// </summary>
        /// <param name="name">실행 파일 기본 이름 (예: "node", "npm", "pnpm")</param>
        /// <returns>플랫폼별 실행 파일 이름 (예: Windows에서 "node.exe")</returns>
        public static string GetExecutableName(string name)
        {
            if (IsWindows)
            {
                // npm, pnpm은 .cmd 파일
                if (name == "npm" || name == "pnpm" || name == "npx")
                {
                    return name + ".cmd";
                }
                return name + ".exe";
            }
            return name;
        }

        /// <summary>
        /// 셸 명령 실행 (크로스 플랫폼)
        /// </summary>
        /// <param name="command">실행할 명령</param>
        /// <param name="workingDirectory">작업 디렉토리</param>
        /// <param name="additionalPaths">PATH에 추가할 경로들</param>
        /// <param name="timeoutMs">타임아웃 (밀리초)</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>명령 실행 결과</returns>
        public static CommandResult ExecuteCommand(
            string command,
            string workingDirectory = null,
            string[] additionalPaths = null,
            int timeoutMs = 300000,
            bool verbose = true)
        {
            var result = new CommandResult();

            try
            {
                string shell, shellArgs;
                string pathEnv = BuildPathEnv(additionalPaths ?? new string[0]);

                if (IsWindows)
                {
                    shell = "cmd.exe";
                    // chcp 65001: 코드 페이지를 UTF-8로 변경하여 한글 등 유니코드 문자가 깨지지 않도록 함
                    shellArgs = $"/c \"chcp 65001 >nul && {command}\"";
                }
                else
                {
                    shell = "/bin/bash";
                    // -l 옵션으로 로그인 셸로 실행하여 .bashrc, .bash_profile 등을 로드
                    // CI=true: pnpm이 비-TTY 환경에서 확인 프롬프트 없이 실행되도록 설정
                    shellArgs = $"-l -c \"export CI=true && export PATH='{pathEnv}' && {command}\"";
                }

                if (verbose)
                {
                    Debug.Log($"[Platform] 명령 실행: {command}");
                    Debug.Log($"[Platform] 셸: {shell} {shellArgs}");
                    if (!string.IsNullOrEmpty(workingDirectory))
                    {
                        Debug.Log($"[Platform] 작업 디렉토리: {workingDirectory}");
                    }
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
                {
                    processInfo.WorkingDirectory = workingDirectory;
                }

                // Windows에서는 환경변수로 PATH 및 CI 설정
                if (IsWindows)
                {
                    if (additionalPaths != null)
                    {
                        processInfo.EnvironmentVariables["PATH"] = pathEnv;
                    }
                    // CI=true: pnpm이 비-TTY 환경에서 확인 프롬프트 없이 실행되도록 설정
                    processInfo.EnvironmentVariables["CI"] = "true";
                }

                using (var process = new Process { StartInfo = processInfo })
                {
                    process.Start();

                    // 비동기로 출력 읽기 (데드락 방지)
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    bool completed = process.WaitForExit(timeoutMs);

                    if (!completed)
                    {
                        process.Kill();
                        result.Success = false;
                        result.Error = "명령 실행 시간 초과";
                        result.ExitCode = -1;

                        if (verbose)
                        {
                            Debug.LogError($"[Platform] 명령 시간 초과 ({timeoutMs}ms): {command}");
                        }

                        return result;
                    }

                    result.Output = StripAnsiCodes(outputTask.Result);
                    result.Error = StripAnsiCodes(errorTask.Result);
                    result.ExitCode = process.ExitCode;
                    result.Success = process.ExitCode == 0;

                    if (verbose)
                    {
                        if (result.Success)
                        {
                            Debug.Log($"[Platform] ✓ 명령 성공 (Exit Code: {result.ExitCode})");
                            if (!string.IsNullOrEmpty(result.Output))
                            {
                                Debug.Log($"[Platform] 출력:\n{result.Output.Trim()}");
                            }
                        }
                        else
                        {
                            Debug.LogError($"[Platform] ✗ 명령 실패 (Exit Code: {result.ExitCode})");
                            // 실패 시 stdout과 stderr 모두 출력 (에러 정보가 stdout에 있을 수 있음)
                            if (!string.IsNullOrEmpty(result.Output))
                            {
                                Debug.LogError($"[Platform] stdout:\n{result.Output.Trim()}");
                            }
                            if (!string.IsNullOrEmpty(result.Error))
                            {
                                Debug.LogError($"[Platform] stderr:\n{result.Error.Trim()}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Error = e.Message;
                result.ExitCode = -1;

                if (verbose)
                {
                    Debug.LogError($"[Platform] 명령 실행 예외: {e.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// 실행 파일 경로 찾기 (크로스 플랫폼)
        /// </summary>
        /// <param name="executableName">실행 파일 이름 (예: "node", "npm")</param>
        /// <param name="additionalPaths">추가 검색 경로</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>실행 파일 절대 경로 또는 null</returns>
        public static string FindExecutable(string executableName, string[] additionalPaths = null, bool verbose = true)
        {
            if (verbose)
            {
                Debug.Log($"[Platform] '{executableName}' 검색 중...");
            }

            // 1. 추가 경로에서 찾기
            if (additionalPaths != null)
            {
                foreach (var dir in additionalPaths)
                {
                    string path = FindExecutableInDirectory(dir, executableName);
                    if (path != null)
                    {
                        if (verbose) Debug.Log($"[Platform] ✓ 발견 (추가 경로): {path}");
                        return path;
                    }
                }
            }

            // 2. 표준 경로에서 찾기
            foreach (var dir in StandardBinPaths)
            {
                string path = FindExecutableInDirectory(dir, executableName);
                if (path != null)
                {
                    if (verbose) Debug.Log($"[Platform] ✓ 발견 (표준 경로): {path}");
                    return path;
                }
            }

            // 3. 시스템 명령으로 찾기 (where/which)
            string systemPath = FindExecutableViaSystem(executableName, verbose);
            if (systemPath != null)
            {
                if (verbose) Debug.Log($"[Platform] ✓ 발견 (시스템): {systemPath}");
                return systemPath;
            }

            if (verbose)
            {
                Debug.Log($"[Platform] ✗ '{executableName}' 찾을 수 없음");
            }

            return null;
        }

        /// <summary>
        /// 특정 디렉토리에서 실행 파일 찾기
        /// </summary>
        private static string FindExecutableInDirectory(string directory, string executableName)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            // 플랫폼별 실행 파일 이름 시도
            string[] possibleNames;

            if (IsWindows)
            {
                possibleNames = new string[]
                {
                    executableName + ".exe",
                    executableName + ".cmd",
                    executableName + ".bat",
                    executableName
                };
            }
            else
            {
                possibleNames = new string[] { executableName };
            }

            foreach (var name in possibleNames)
            {
                string fullPath = Path.Combine(directory, name);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// 시스템 명령으로 실행 파일 찾기 (where/which/type)
        /// </summary>
        private static string FindExecutableViaSystem(string executableName, bool verbose)
        {
            try
            {
                string shell;
                string shellArgs;

                if (IsWindows)
                {
                    shell = "cmd.exe";
                    shellArgs = $"/c where {executableName}";
                }
                else
                {
                    shell = "/bin/bash";
                    // type -P는 함수가 아닌 실제 실행 파일만 찾음
                    shellArgs = $"-l -c \"type -P {executableName}\"";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = shell,
                        Arguments = shellArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    // Windows의 where 명령은 여러 줄을 반환할 수 있음
                    string firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];

                    if (File.Exists(firstLine))
                    {
                        return firstLine;
                    }
                }
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Debug.LogWarning($"[Platform] 시스템 검색 실패: {e.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// 파일에 실행 권한 부여 (Unix 전용, Windows에서는 무시)
        /// </summary>
        /// <param name="filePath">파일 경로</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        public static void SetExecutablePermission(string filePath, bool verbose = true)
        {
            if (IsWindows)
            {
                // Windows에서는 실행 권한이 필요 없음
                return;
            }

            if (!File.Exists(filePath))
            {
                if (verbose)
                {
                    Debug.LogWarning($"[Platform] 파일이 존재하지 않음: {filePath}");
                }
                return;
            }

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();

                if (verbose)
                {
                    Debug.Log($"[Platform] 실행 권한 부여: {filePath}");
                }
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Debug.LogWarning($"[Platform] 실행 권한 부여 실패: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Finder/Explorer에서 폴더 열기
        /// </summary>
        /// <param name="path">열 경로</param>
        public static void RevealInFileManager(string path)
        {
            if (IsWindows)
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (IsMacOS)
            {
                Process.Start("open", $"-R \"{path}\"");
            }
            else if (IsLinux)
            {
                // xdg-open은 파일 선택을 지원하지 않으므로 디렉토리만 열기
                string directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                Process.Start("xdg-open", $"\"{directory}\"");
            }
        }

        /// <summary>
        /// ANSI 이스케이프 코드 제거
        /// </summary>
        /// <param name="input">입력 문자열</param>
        /// <returns>ANSI 코드가 제거된 문자열</returns>
        public static string StripAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // ANSI 이스케이프 시퀀스 패턴:
            // - \x1B\[..m : 표준 ANSI (ESC[...m)
            // - \x1B\]....\x07 : OSC 시퀀스
            // - \[..m : ESC 없이 시작하는 경우 (일부 터미널)
            return Regex.Replace(input, @"\x1B\[[0-9;]*[a-zA-Z]|\x1B\][^\x07]*\x07|\[[0-9;]*[a-zA-Z]", "");
        }

        /// <summary>
        /// 명령 실행 결과
        /// </summary>
        public class CommandResult
        {
            public bool Success { get; set; }
            public string Output { get; set; } = "";
            public string Error { get; set; } = "";
            public int ExitCode { get; set; }
        }
    }
}
