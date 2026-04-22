using System;
using System.Threading;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// pnpm install 실행 (동기/비동기/백그라운드).
    /// PnpmStoreManager의 InstallStages 정책을 기반으로 frozen → lockfile 갱신 → clean 재시도 순으로 진행.
    /// 실패 시 NodeModulesValidator.CleanNodeModules로 복구 후 재시도.
    /// </summary>
    internal static class PnpmRunner
    {
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
                    Debug.LogError($"[AIT] [병렬] 백그라운드 pnpm install 예외: {e}");
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

            foreach (var (args, label, cleanFirst) in PnpmStoreManager.InstallStages)
            {
                if (ct.IsCancellationRequested)
                    return AITConvertCore.AITExportError.CANCELLED;

                if (cleanFirst) NodeModulesValidator.CleanNodeModules(earlyCtx.BuildProjectPath);

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
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // HasExited 폴링 + CancellationToken 체크
                        int maxWaitMs = 300000; // 5분
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
                                Debug.LogError($"[AIT] [병렬] pnpm {label} 시간 초과 ({maxWaitMs / 1000}초)");
                                break; // 다음 단계로
                            }

                            Thread.Sleep(200);
                        }

                        // 아직 종료 안 된 경우 (타임아웃으로 빠져나온 경우) → 다음 단계
                        if (!process.HasExited) continue;

                        process.WaitForExit(5000); // 출력 버퍼 플러시

                        if (process.ExitCode == 0)
                        {
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
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AIT] [병렬] pnpm {label} 실행 오류: {e}");
                }

                Debug.Log($"[AIT] [병렬] pnpm install ({label}) 실패, 다음 단계로...");
            }

            Debug.LogError("[AIT] [병렬] pnpm install 실패 (모든 재시도 후에도 실패)");
            return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
        }

        /// <summary>
        /// pnpm install 3단계 재시도 (동기)
        /// </summary>
        internal static AITConvertCore.AITExportError RunPnpmInstallSync(AITPackageBuilder.PackageContext ctx)
        {
            foreach (var (args, label, cleanFirst) in PnpmStoreManager.InstallStages)
            {
                if (cleanFirst) NodeModulesValidator.CleanNodeModules(ctx.BuildProjectPath);
                var result = AITNpmRunner.RunNpmCommandWithCache(
                    ctx.BuildProjectPath, ctx.PnpmPath, args, ctx.LocalCachePath,
                    $"pnpm {label}...");
                if (result == AITConvertCore.AITExportError.SUCCEED) return result;
                if (result == AITConvertCore.AITExportError.CANCELLED) return result;
                Debug.Log($"[AIT] pnpm install ({label}) 실패, 다음 단계로...");
            }
            Debug.LogError("[AIT] pnpm install 실패 (모든 재시도 후에도 실패)");
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
                Debug.LogError("[AIT] pnpm install 실패 (모든 재시도 후에도 실패)");
                onComplete?.Invoke(AITConvertCore.AITExportError.FAIL_NPM_BUILD);
                return;
            }

            var (args, label, cleanFirst) = PnpmStoreManager.InstallStages[stageIndex];
            if (cleanFirst) NodeModulesValidator.CleanNodeModules(buildProjectPath);

            Debug.Log($"[AIT] pnpm {label} 실행 중...");

            AITNpmRunner.RunNpmCommandWithCacheAsync(
                buildProjectPath, pnpmPath, args, localCachePath,
                onComplete: (result) =>
                {
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
    }
}
