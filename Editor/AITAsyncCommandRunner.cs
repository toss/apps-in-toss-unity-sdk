using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 비동기 명령 실행기
    /// EditorApplication.update 폴링 패턴을 사용하여 Unity Editor를 차단하지 않고 외부 명령을 실행합니다.
    /// </summary>
    public static class AITAsyncCommandRunner
    {
        /// <summary>
        /// 명령 실행 상태
        /// </summary>
        public enum CommandState
        {
            Pending,
            Running,
            Completed,
            Failed,
            Cancelled
        }

        /// <summary>
        /// 비동기 명령 작업
        /// </summary>
        public class CommandTask
        {
            public string Id { get; }
            public CommandState State { get; internal set; }
            public float Progress { get; internal set; }
            public string CurrentOutput { get; internal set; }
            public CancellationTokenSource CancellationSource { get; }
            public StringBuilder OutputLog { get; }
            public Process Process { get; internal set; }

            internal Action<AITPlatformHelper.CommandResult> OnComplete;
            internal Action<string> OnOutputReceived;

            public CommandTask(string id)
            {
                Id = id;
                State = CommandState.Pending;
                Progress = 0f;
                CurrentOutput = "";
                CancellationSource = new CancellationTokenSource();
                OutputLog = new StringBuilder();
            }
        }

        // 메인 스레드 콜백 큐
        private static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
        private static int isQueueProcessorRegistered = 0;
        private static int taskIdCounter = 0;

        /// <summary>
        /// 비동기 명령 실행 (즉시 반환, 콜백으로 결과 전달)
        /// </summary>
        /// <param name="command">실행할 명령</param>
        /// <param name="workingDirectory">작업 디렉토리</param>
        /// <param name="additionalPaths">PATH에 추가할 경로들</param>
        /// <param name="onComplete">완료 콜백 (메인 스레드에서 호출)</param>
        /// <param name="onOutputReceived">출력 수신 콜백 (메인 스레드에서 호출)</param>
        /// <param name="timeoutMs">타임아웃 (밀리초, 기본 5분)</param>
        /// <returns>명령 작업 객체</returns>
        public static CommandTask RunAsync(
            string command,
            string workingDirectory,
            string[] additionalPaths,
            Action<AITPlatformHelper.CommandResult> onComplete,
            Action<string> onOutputReceived = null,
            int timeoutMs = 300000,
            Dictionary<string, string> additionalEnvVars = null)
        {
            EnsureQueueProcessorRegistered();

            var task = new CommandTask($"cmd_{Interlocked.Increment(ref taskIdCounter)}")
            {
                OnComplete = onComplete,
                OnOutputReceived = onOutputReceived
            };

            // 백그라운드 스레드에서 명령 실행
            ThreadPool.QueueUserWorkItem(_ => ExecuteCommandAsync(task, command, workingDirectory, additionalPaths, timeoutMs, additionalEnvVars));

            return task;
        }

        /// <summary>
        /// 작업 취소
        /// </summary>
        public static void CancelTask(CommandTask task)
        {
            if (task == null || task.State == CommandState.Completed || task.State == CommandState.Failed || task.State == CommandState.Cancelled)
            {
                return;
            }

            task.CancellationSource.Cancel();
            task.State = CommandState.Cancelled;

            try
            {
                if (task.Process != null && !task.Process.HasExited)
                {
                    task.Process.Kill();
                    Debug.Log($"[AIT Async] 프로세스 종료: {task.Id}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT Async] 프로세스 종료 실패: {e}");
            }
        }

        /// <summary>
        /// 백그라운드 스레드에서 명령 실행
        /// </summary>
        private static void ExecuteCommandAsync(
            CommandTask task,
            string command,
            string workingDirectory,
            string[] additionalPaths,
            int timeoutMs,
            Dictionary<string, string> additionalEnvVars = null)
        {
            task.State = CommandState.Running;
            var result = new AITPlatformHelper.CommandResult();
            int pid = -1;

            try
            {
                string shell, shellArgs;
                string pathEnv = AITPlatformHelper.BuildPathEnv(additionalPaths ?? new string[0]);

                if (AITPlatformHelper.IsWindows)
                {
                    shell = "powershell.exe";
                    string escapedCommand = EscapeForPowerShell(command);
                    string escapedPathEnv = pathEnv.Replace("'", "''");
                    string envSetup = $"$env:CI = 'true'; $env:PATH = '{escapedPathEnv}';";
                    if (additionalEnvVars != null)
                    {
                        foreach (var kvp in additionalEnvVars)
                        {
                            string escapedValue = kvp.Value.Replace("'", "''");
                            envSetup += $" $env:{kvp.Key} = '{escapedValue}';";
                        }
                    }
                    shellArgs = $"-ExecutionPolicy Bypass -NoProfile -NoLogo -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {envSetup} {escapedCommand}\"";
                }
                else
                {
                    shell = "/bin/bash";
                    // 환경변수는 아래 ProcessStartInfo.EnvironmentVariables 블록에서 설정
                    // bash -c "..." 안에서 export 할당 시 JSON 등의 큰따옴표가
                    // 바깥 큰따옴표와 충돌하므로, 셸 명령에서는 CI만 설정
                    string envExports = "export CI=true";
                    string escapedCommand = AITPlatformHelper.EscapeForBashDoubleQuotes(command);
                    string escapedPathEnv = AITPlatformHelper.EscapeForBashDoubleQuotes(pathEnv);
                    shellArgs = $"-l -c \"{envExports} && export PATH=\\\"{escapedPathEnv}\\\" && {escapedCommand}\"";
                }

                EnqueueMainThread(() => Debug.Log($"[AIT Async] 명령 시작: {command}"));

                var processInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                if (!string.IsNullOrEmpty(workingDirectory) && System.IO.Directory.Exists(workingDirectory))
                {
                    processInfo.WorkingDirectory = workingDirectory;
                }

                if (additionalPaths != null && additionalPaths.Length > 0)
                {
                    processInfo.EnvironmentVariables["PATH"] = pathEnv;
                }
                processInfo.EnvironmentVariables["CI"] = "true";

                // 추가 환경변수 설정
                if (additionalEnvVars != null)
                {
                    foreach (var kvp in additionalEnvVars)
                    {
                        processInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                }

                using (var process = new Process { StartInfo = processInfo })
                {
                    task.Process = process;

                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    // 실시간 출력 수신
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            string line = AITPlatformHelper.StripAnsiCodes(e.Data);
                            outputBuilder.AppendLine(line);
                            task.OutputLog.AppendLine(line);
                            task.CurrentOutput = line;

                            if (task.OnOutputReceived != null)
                            {
                                EnqueueMainThread(() => task.OnOutputReceived?.Invoke(line));
                            }
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            string line = AITPlatformHelper.StripAnsiCodes(e.Data);
                            errorBuilder.AppendLine(line);
                            task.OutputLog.AppendLine($"[stderr] {line}");
                        }
                    };

                    process.Start();
                    pid = process.Id;
                    AITBuildSession.RecordPid(pid);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 취소 가능 + 타임아웃 대기. timeoutMs(기본 5분) 초과 시 프로세스를 강제 종료한다.
                    // 이 루프가 timeoutMs를 무시하면 granite/pnpm 등 하위 프로세스가 hang 했을 때
                    // 무한 대기에 빠져 빌드 세션이 영구 정지된다 (SDK-VH 근본 원인).
                    var timeoutStopwatch = Stopwatch.StartNew();
                    while (!process.HasExited)
                    {
                        if (task.CancellationSource.Token.IsCancellationRequested)
                        {
                            process.Kill();
                            result.Success = false;
                            result.Error = "작업이 취소되었습니다.";
                            result.ExitCode = -1;
                            task.State = CommandState.Cancelled;

                            EnqueueMainThread(() =>
                            {
                                Debug.Log($"[AIT Async] 명령 취소됨: {task.Id}");
                                task.OnComplete?.Invoke(result);
                            });
                            return;
                        }

                        if (timeoutMs > 0 && timeoutStopwatch.ElapsedMilliseconds > timeoutMs)
                        {
                            try { process.Kill(); } catch { /* 이미 종료된 프로세스는 무시 */ }
                            // WaitForExit(ms)는 비동기 출력 핸들러 플러시를 보장하지 않는다. 종료가 확인되면
                            // 무파라미터 오버로드를 한 번 더 호출해 outputBuilder/errorBuilder를 마저 채운다(.NET 권장, §5).
                            if (process.WaitForExit(5000))
                                process.WaitForExit();
                            result.Success = false;
                            // 타임아웃 직전까지 누적된 stderr/stdout을 결과에 실어 상위 빌드 실패 캡처의
                            // 진단(extra)에 hang 원인이 남도록 한다 — 동기 경로와 대칭, 빈 result.Output/Error
                            // 회귀 방지 (§5). "명령 시간 초과" 문구를 접두로 둬 메시지 가독성을 유지한다(매칭 계약 아님).
                            result.Output = outputBuilder.ToString();
                            string timeoutStderr = errorBuilder.ToString();
                            result.Error = string.IsNullOrEmpty(timeoutStderr)
                                ? $"명령 시간 초과 ({timeoutMs / 1000}초)"
                                : $"명령 시간 초과 ({timeoutMs / 1000}초)\n{timeoutStderr}";
                            result.ExitCode = -1;
                            task.State = CommandState.Failed;

                            EnqueueMainThread(() =>
                            {
                                // 타임아웃은 환경(네트워크/registry/hang) 원인. 상위 빌드 흐름이 FAIL 결과로
                                // 인지해 단일 CaptureBuildError로 캡처하므로 runner 레벨은 진단만 남긴다.
                                AppsInToss.Editor.AITLog.Error(
                                    $"[AIT Async] ✗ 명령 시간 초과 ({timeoutMs / 1000}초): {task.Id}",
                                    sentryCapture: false);
                                task.OnComplete?.Invoke(result);
                            });
                            return;
                        }

                        Thread.Sleep(100);
                    }

                    process.WaitForExit(); // 출력 버퍼 플러시 대기

                    // 외부에서 취소된 경우 (CancelTask 호출로 프로세스가 Kill된 경우)
                    if (task.CancellationSource.Token.IsCancellationRequested || task.State == CommandState.Cancelled)
                    {
                        result.Success = false;
                        result.Error = "작업이 취소되었습니다.";
                        result.ExitCode = -1;
                        task.State = CommandState.Cancelled;

                        EnqueueMainThread(() =>
                        {
                            Debug.Log($"[AIT Async] 명령 취소됨: {task.Id}");
                            task.OnComplete?.Invoke(result);
                        });
                        return;
                    }

                    result.Output = outputBuilder.ToString();
                    result.Error = errorBuilder.ToString();
                    result.ExitCode = process.ExitCode;
                    result.Success = process.ExitCode == 0;

                    task.State = result.Success ? CommandState.Completed : CommandState.Failed;
                    task.Progress = 1f;

                    EnqueueMainThread(() =>
                    {
                        if (result.Success)
                        {
                            Debug.Log($"[AIT Async] ✓ 명령 성공: {task.Id}");
                        }
                        else
                        {
                            // task.Id(cmd_N)는 fingerprint 가치가 없고, 빌드 실패의 단일 캡처는 상위
                            // (CaptureBuildError)가 담당하므로 runner 레벨 명령 실패는 진단만 남긴다 (SDK-VG/VD/VB).
                            AppsInToss.Editor.AITLog.Error(
                                $"[AIT Async] ✗ 명령 실패 (Exit: {result.ExitCode}): {task.Id}",
                                sentryCapture: false);
                            if (!string.IsNullOrEmpty(result.Error))
                            {
                                AppsInToss.Editor.AITLog.Error(
                                    $"[AIT Async] stderr:\n{result.Error.Trim()}",
                                    sentryCapture: false);
                            }
                        }
                        task.OnComplete?.Invoke(result);
                    });
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Error = e.Message;
                result.ExitCode = -1;
                task.State = CommandState.Failed;

                EnqueueMainThread(() =>
                {
                    // 프로세스 시작/IO 예외 — 환경 원인 cascade이며 호출자(빌드: CaptureBuildError /
                    // pnpm 글로벌 설치: LogWarning)가 결과를 인지해 처리한다. runner 레벨은 Sentry 차단 (SDK-VB).
                    AppsInToss.Editor.AITLog.Error($"[AIT Async] 명령 실행 예외: {e}", sentryCapture: false);
                    task.OnComplete?.Invoke(result);
                });
            }
            finally
            {
                if (pid > 0) AITBuildSession.ClearPid(pid);
            }
        }

        /// <summary>
        /// PowerShell 명령용 문자열 이스케이프
        /// </summary>
        private static string EscapeForPowerShell(string command)
        {
            return command
                .Replace("`", "``")
                .Replace("$", "`$");
        }

        /// <summary>
        /// 메인 스레드에서 실행할 작업을 큐에 추가
        /// </summary>
        private static void EnqueueMainThread(Action action)
        {
            if (action == null) return;
            mainThreadQueue.Enqueue(action);
        }

        /// <summary>
        /// 큐 프로세서가 등록되어 있는지 확인하고 등록
        /// </summary>
        private static void EnsureQueueProcessorRegistered()
        {
            if (Interlocked.CompareExchange(ref isQueueProcessorRegistered, 1, 0) == 0)
            {
                EditorApplication.update += ProcessMainThreadQueue;
            }
        }

        /// <summary>
        /// 메인 스레드 큐 처리 (EditorApplication.update에서 호출)
        /// </summary>
        private static void ProcessMainThreadQueue()
        {
            // 한 프레임에 최대 20개 작업 처리
            int processedCount = 0;
            while (processedCount < 20 && mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                processedCount++;
            }
        }
    }
}
