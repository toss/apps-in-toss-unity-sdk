using System;
using System.Threading;
using AppsInToss.Editor.ErrorTracker;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// pnpm install 실행 (동기/비동기/백그라운드).
    /// PnpmStoreManager의 InstallStages 정책을 기반으로 frozen → lockfile 갱신 → lockfile 폐기 → clean 재시도 순으로 진행.
    /// 실패 시 NodeModulesValidator.CleanNodeModules로 복구 후 재시도.
    ///
    /// Sentry 캡처 정책: 중간/최종 단계 LogError는 모두 Console 진단 전용(sentryCapture:false)이다.
    /// 빌드 실패의 단일 구조화 Sentry 이벤트는 상위 진입점(ShowBuildFailedDialog /
    /// AITSettingsHelper → CaptureBuildError)이 errorCode 기반 fingerprint로 캡처한다.
    /// — runner 레벨 cascade(SDK-R5) 및 중간 단계 false positive(SDK-B8/SDK-HB) 동시 방지.
    /// </summary>
    internal static class PnpmRunner
    {
        /// <summary>
        /// 마지막 단계 실패 시 한 번만 출력하는 최종 에러 메시지.
        /// PnpmRunner의 sync/async/background 경로에서 일관되게 사용된다.
        /// </summary>
        internal const string FinalFailureMessage = "[AIT] pnpm install 실패 (모든 재시도 후에도 실패)";

        /// <summary>
        /// ThreadPool에서 pnpm install을 백그라운드 OS 프로세스로 실행합니다.
        /// WebGL 빌드가 메인 스레드를 블로킹하는 동안 독립적으로 실행됩니다.
        /// EditorUtility 등 메인 스레드 전용 API를 사용하지 않습니다.
        /// </summary>
        internal static void StartPnpmInstallInBackground(AITPackageBuilder.EarlyPackageContext earlyCtx)
        {
            Debug.Log("[AIT] [병렬] pnpm install을 백그라운드에서 시작합니다...");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var result = RunPnpmInstallInThread(earlyCtx);

                    if (result == AITConvertCore.AITExportError.SUCCEED)
                        Debug.Log("[AIT] [병렬] ✓ 백그라운드 pnpm install 완료");
                    else
                        Debug.Log($"[AIT] [병렬] 백그라운드 pnpm install 결과: {result}");

                    // Result 설정이 곧 완료 시그널 (PnpmInstallCompleted는 이 값으로 판단)
                    earlyCtx.PnpmInstallResult = result;
                }
                catch (Exception e)
                {
                    // 백그라운드 install 예외는 메인 빌드 흐름이 결과(FAIL_NPM_BUILD)를 인지해
                    // 재시도/터미널 처리하므로 cascade다. Sentry 단일 이벤트는 상위 CaptureBuildError에 위임.
                    AITLog.Error($"[AIT] [병렬] 백그라운드 pnpm install 예외: {e}", sentryCapture: false);
                    earlyCtx.PnpmInstallResult = AITConvertCore.AITExportError.FAIL_NPM_BUILD;
                }
            });
        }

        /// <summary>
        /// 백그라운드 스레드에서 pnpm install 3단계 재시도를 실행합니다.
        /// 메인 스레드 전용 API(EditorUtility 등)를 사용하지 않으며,
        /// Process.HasExited 폴링 + CancellationToken으로 취소를 지원합니다.
        /// </summary>
        private static AITConvertCore.AITExportError RunPnpmInstallInThread(AITPackageBuilder.EarlyPackageContext earlyCtx)
        {
            var ct = earlyCtx.PnpmCancellationToken;
            var additionalPaths = AITNpmRunner.BuildAdditionalPaths(earlyCtx.PnpmPath);

            // 중간 단계의 실행 오류/시간 초과 LogError는 정상 fallback의 일부라 Sentry로 보내지 않는다.
            // 최종 실패 LogError도 sentryCapture:false이며, 빌드 실패 캡처는 상위 CaptureBuildError가 담당한다.
            AITEditorErrorTracker.BeginSuppressLogCapture();
            try
            {
                foreach (var (args, label, cleanFirst, deleteLockfileFirst) in PnpmStoreManager.InstallStages)
                {
                    if (ct.IsCancellationRequested)
                        return AITConvertCore.AITExportError.CANCELLED;

                    if (cleanFirst) NodeModulesValidator.CleanNodeModules(earlyCtx.BuildProjectPath);
                    if (deleteLockfileFirst) DeleteLockfileIfExists(earlyCtx.BuildProjectPath);

                    string fullArguments = AITNpmRunner.BuildFullArguments(args, earlyCtx.LocalCachePath);
                    string command = $"\"{earlyCtx.PnpmPath}\" {fullArguments}";

                    Debug.Log($"[AIT] [병렬] pnpm {label} 실행 중...");
                    Debug.Log($"[AIT] [병렬] 명령: {command}");

                    try
                    {
                        var processInfo = AITPlatformHelper.CreateProcessStartInfo(
                            command, earlyCtx.BuildProjectPath, additionalPaths.ToArray());

                        using (var process = new System.Diagnostics.Process { StartInfo = processInfo })
                        {
                            int pid = -1;
                            try
                            {
                                var outputBuilder = new System.Text.StringBuilder();
                                var errorBuilder = new System.Text.StringBuilder();

                                process.OutputDataReceived += (sender, e) =>
                                {
                                    if (e.Data != null)
                                        outputBuilder.AppendLine(AITPlatformHelper.StripAnsiCodes(e.Data));
                                };
                                process.ErrorDataReceived += (sender, e) =>
                                {
                                    if (e.Data != null)
                                        errorBuilder.AppendLine(AITPlatformHelper.StripAnsiCodes(e.Data));
                                };

                                process.Start();
                                pid = process.Id;
                                AITBuildSession.RecordPid(pid);
                                process.BeginOutputReadLine();
                                process.BeginErrorReadLine();

                                // HasExited 폴링 + CancellationToken 체크
                                int maxWaitMs = 300000; // 5분
                                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                                bool timedOut = false;

                                while (!process.HasExited)
                                {
                                    if (ct.IsCancellationRequested)
                                    {
                                        Debug.Log($"[AIT] [병렬] pnpm install 취소 요청. 프로세스를 종료합니다.");
                                        process.Kill();
                                        process.WaitForExit(5000);
                                        return AITConvertCore.AITExportError.CANCELLED;
                                    }

                                    if (stopwatch.ElapsedMilliseconds > maxWaitMs)
                                    {
                                        process.Kill();
                                        process.WaitForExit(5000);
                                        // 5분 타임아웃은 환경(네트워크/registry) 원인이며 다음 폴백
                                        // 단계로 진행됨. Sentry로 fingerprint를 흘리지 않음.
                                        AITLog.Error($"[AIT] [병렬] pnpm {label} 시간 초과 ({maxWaitMs / 1000}초)", sentryCapture: false);
                                        // 모든 단계가 타임아웃으로 끝나도 상위 캡처에 진단이 남도록 기록 (§5).
                                        AITBuildDiagnostics.RecordFailure(
                                            $"pnpm {label} (timeout {maxWaitMs / 1000}s)", -1,
                                            errorBuilder.ToString(), outputBuilder.ToString());
                                        timedOut = true;
                                        break; // 다음 단계로
                                    }

                                    Thread.Sleep(200);
                                }

                                // 타임아웃으로 Kill한 경우 process.HasExited가 true가 되어 아래 exit-code 경로로
                                // 떨어지면 위 (timeout) 진단을 일반 라벨로 덮어쓴다. timedOut이면 곧장 다음 단계로 (§5).
                                if (timedOut || !process.HasExited) continue;

                                process.WaitForExit(5000); // 출력 버퍼 플러시

                                if (process.ExitCode == 0)
                                {
                                    // 성공 시 직전 폴백 단계의 실패 진단을 비운다 (§5).
                                    AITBuildDiagnostics.ClearOnSuccess();
                                    Debug.Log($"[AIT] [병렬] ✓ pnpm {label} 성공");
                                    return AITConvertCore.AITExportError.SUCCEED;
                                }

                                string output = outputBuilder.ToString();
                                string error = errorBuilder.ToString();
                                Debug.Log($"[AIT] [병렬] pnpm {label} 실패 (Exit Code: {process.ExitCode})");
                                if (!string.IsNullOrEmpty(output))
                                    Debug.Log($"[AIT] [병렬] 출력:\n{output.Trim()}");
                                if (!string.IsNullOrEmpty(error))
                                    Debug.Log($"[AIT] [병렬] 오류:\n{error.Trim()}");
                                // 상위 단일 빌드 실패 캡처(CaptureBuildError)에 exit/stderr 진단을 실어줄 수 있도록 기록 (§5).
                                AITBuildDiagnostics.RecordFailure($"pnpm {label}", process.ExitCode, error, output);
                            }
                            finally
                            {
                                if (pid > 0) AITBuildSession.ClearPid(pid);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // 프로세스 시작/IO 예외 — 다음 폴백 단계로 진행. 환경 원인 cascade이므로 Sentry 차단.
                        AITLog.Error($"[AIT] [병렬] pnpm {label} 실행 오류: {e}", sentryCapture: false);
                        // 모든 단계가 시작 단계 예외로 끝나도 상위 캡처가 원인을 받도록 진단 기록 (§5).
                        AITBuildDiagnostics.RecordFailure($"pnpm {label} (process-start exception)", -1, e.Message);
                    }

                    Debug.Log($"[AIT] [병렬] pnpm install ({label}) 실패, 다음 단계로...");
                }
            }
            finally
            {
                AITEditorErrorTracker.EndSuppressLogCapture();
            }

            // 백그라운드 install 최종 실패 — 메인 빌드 흐름이 재설치/터미널 실패로 인지한다.
            // Sentry 단일 이벤트는 상위(ShowBuildFailedDialog → CaptureBuildError)에 위임 (SDK-R5 cascade 방지).
            AITLog.Error("[AIT] [병렬] pnpm install 실패 (모든 재시도 후에도 실패)", sentryCapture: false);
            return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
        }

        /// <summary>
        /// pnpm install 3단계 재시도 (동기)
        /// </summary>
        internal static AITConvertCore.AITExportError RunPnpmInstallSync(AITPackageBuilder.PackageContext ctx)
        {
            // 중간 단계 실패는 정상 fallback이라 Sentry로 보내지 않는다.
            // 최종 실패 LogError도 sentryCapture:false이며, 빌드 실패 캡처는 상위 CaptureBuildError가 담당한다.
            AITEditorErrorTracker.BeginSuppressLogCapture();
            try
            {
                foreach (var (args, label, cleanFirst, deleteLockfileFirst) in PnpmStoreManager.InstallStages)
                {
                    if (cleanFirst) NodeModulesValidator.CleanNodeModules(ctx.BuildProjectPath);
                    if (deleteLockfileFirst) DeleteLockfileIfExists(ctx.BuildProjectPath);
                    var result = AITNpmRunner.RunNpmCommandWithCache(
                        ctx.BuildProjectPath, ctx.PnpmPath, args, ctx.LocalCachePath,
                        $"pnpm {label}...");
                    if (result == AITConvertCore.AITExportError.SUCCEED) return result;
                    if (result == AITConvertCore.AITExportError.CANCELLED) return result;
                    Debug.Log($"[AIT] pnpm install ({label}) 실패, 다음 단계로...");
                }
            }
            finally
            {
                AITEditorErrorTracker.EndSuppressLogCapture();
            }

            // 동기 install 최종 실패 — Sentry 단일 이벤트는 상위 CaptureBuildError에 위임 (SDK-R5 cascade 방지).
            AITLog.Error(FinalFailureMessage, sentryCapture: false);
            return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
        }

        /// <summary>
        /// pnpm install 비동기 실행 (PnpmStoreManager.InstallStages 배열 기반 재귀 재시도)
        /// </summary>
        internal static void RunPnpmInstallAsync(
            string buildProjectPath,
            string pnpmPath,
            string localCachePath,
            CancellationToken ct,
            Action<string> onOutput,
            Action<AITConvertCore.AITExportError> onComplete,
            int stageIndex = 0)
        {
            if (stageIndex >= PnpmStoreManager.InstallStages.Count)
            {
                // 비동기 install 최종 실패 — Sentry 단일 이벤트는 상위 CaptureBuildError에 위임 (SDK-R5 cascade 방지).
                AITLog.Error(FinalFailureMessage, sentryCapture: false);
                onComplete?.Invoke(AITConvertCore.AITExportError.FAIL_NPM_BUILD);
                return;
            }

            var (args, label, cleanFirst, deleteLockfileFirst) = PnpmStoreManager.InstallStages[stageIndex];
            if (cleanFirst) NodeModulesValidator.CleanNodeModules(buildProjectPath);
            if (deleteLockfileFirst) DeleteLockfileIfExists(buildProjectPath);

            Debug.Log($"[AIT] pnpm {label} 실행 중...");

            // 중간 단계 실패는 정상 fallback이라 Sentry로 보내지 않는다.
            // 비동기 콜백 경계를 넘기 위해 Begin/End를 직접 호출한다.
            AITEditorErrorTracker.BeginSuppressLogCapture();
            bool suppressReleased = false;
            void ReleaseSuppress()
            {
                if (suppressReleased) return;
                suppressReleased = true;
                AITEditorErrorTracker.EndSuppressLogCapture();
            }

            try
            {
                AITNpmRunner.RunNpmCommandWithCacheAsync(
                    buildProjectPath, pnpmPath, args, localCachePath,
                    onComplete: (result) =>
                    {
                        ReleaseSuppress();

                        if (result == AITConvertCore.AITExportError.SUCCEED)
                        {
                            onComplete?.Invoke(result);
                            return;
                        }

                        if (ct.IsCancellationRequested)
                        {
                            onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                            return;
                        }

                        Debug.Log($"[AIT] pnpm install ({label}) 실패, 다음 단계로...");
                        RunPnpmInstallAsync(buildProjectPath, pnpmPath, localCachePath, ct, onOutput, onComplete, stageIndex + 1);
                    },
                    onOutputReceived: onOutput
                );
            }
            catch
            {
                // RunNpmCommandWithCacheAsync 자체가 동기적으로 throw할 경우 suppress 누수를 막는다.
                ReleaseSuppress();
                throw;
            }
        }

        /// <summary>
        /// ait-build/pnpm-lock.yaml 을 안전하게 삭제한다. 파일이 없으면 no-op.
        /// 파일 잠금 등 IO 예외는 로그만 남기고 무시 — pnpm install이 어차피 lockfile을 재생성한다.
        /// internal: EditMode 테스트가 임시 디렉토리에서 IO 동작을 검증하기 위해 접근.
        /// </summary>
        internal static void DeleteLockfileIfExists(string buildProjectPath)
        {
            string lockfilePath = System.IO.Path.Combine(buildProjectPath, "pnpm-lock.yaml");
            if (!System.IO.File.Exists(lockfilePath)) return;

            try
            {
                System.IO.File.Delete(lockfilePath);
                Debug.Log("[AIT] pnpm-lock.yaml 삭제됨 (lockfile 폐기 후 재시도 단계)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AIT] pnpm-lock.yaml 삭제 실패 (무시하고 진행): {e.Message}");
            }
        }
    }
}
