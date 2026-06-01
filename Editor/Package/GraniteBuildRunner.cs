using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// Granite / AIT 빌드 실행 (동기 & 비동기) + 실패 시 재시도.
    /// granite build 실패 시 node_modules 정리 후 pnpm install 재시도 경로로 위임.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class GraniteBuildRunner
    {
        /// <summary>
        /// granite build 실행 + 실패 시 1회 재시도 (동기)
        /// ait build → granite build 순으로 시도합니다.
        /// </summary>
        internal static AITConvertCore.AITExportError RunGraniteBuildSync(AITPackageBuilder.PackageContext ctx)
        {
            Debug.Log("[AIT] granite build 실행 중...");
            Debug.Log($"[AIT] UNITY_METADATA: {ctx.UnityMetadataEnv["UNITY_METADATA"]}");

            var result = RunAitOrGraniteBuildSync(ctx, "ait build");
            if (result == AITConvertCore.AITExportError.SUCCEED) return result;
            if (result == AITConvertCore.AITExportError.CANCELLED) return result;

            // 재시도: clean → install → build
            Debug.Log("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
            NodeModulesValidator.CleanNodeModules(ctx.BuildProjectPath);

            var installResult = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "install --no-frozen-lockfile",
                ctx.LocalCachePath, "pnpm install (빌드 실패 후)...");
            if (installResult != AITConvertCore.AITExportError.SUCCEED) return installResult;

            result = RunAitOrGraniteBuildSync(ctx, "ait build (재시도)");
            if (result == AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.Log("[AIT] ✓ granite build 재시도 성공");
            }
            else
            {
                Debug.LogError("[AIT] granite build 재시도도 실패");
            }
            return result;
        }

        /// <summary>
        /// ait build를 먼저 시도하고, 실패 시 granite build로 폴백합니다 (동기).
        /// package.json scripts에 의존하지 않고 CLI를 직접 호출합니다.
        /// </summary>
        internal static AITConvertCore.AITExportError RunAitOrGraniteBuildSync(AITPackageBuilder.PackageContext ctx, string label)
        {
            Debug.Log($"[AIT] {label}: ait build 시도 중...");
            var result = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "exec ait build", ctx.LocalCachePath,
                $"{label} (ait build)...", additionalEnvVars: ctx.UnityMetadataEnv);
            if (result == AITConvertCore.AITExportError.SUCCEED) return result;
            if (result == AITConvertCore.AITExportError.CANCELLED) return result;

            Debug.Log($"[AIT] {label}: ait build 실패, granite build로 폴백합니다...");
            return AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "exec granite build", ctx.LocalCachePath,
                $"{label} (granite build)...", additionalEnvVars: ctx.UnityMetadataEnv);
        }

        /// <summary>
        /// granite build 비동기 실행 + 실패 시 1회 재시도
        /// ait build → granite build 순으로 시도합니다.
        /// </summary>
        internal static void RunGraniteBuildAsync(
            AITPackageBuilder.PackageContext ctx,
            CancellationToken ct,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.5f, "granite build 실행 중...");
            Debug.Log("[AIT] granite build 실행 중...");
            Debug.Log($"[AIT] UNITY_METADATA: {ctx.UnityMetadataEnv["UNITY_METADATA"]}");

            RunAitOrGraniteBuildAsync(ctx, onProgress,
                onComplete: (buildResult) =>
                {
                    if (buildResult == AITConvertCore.AITExportError.SUCCEED)
                    {
                        string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
                        AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);

                        var distValidation = AITBuildValidator.ValidateDistOutput(ctx.BuildProjectPath);
                        if (distValidation != AITConvertCore.AITExportError.SUCCEED)
                        {
                            Debug.LogError("[AIT] granite build가 exit code 0으로 완료되었으나 출력물 검증 실패. 재시도합니다...");
                            RetryGraniteBuildAsync(ctx, ct, onProgress, onComplete);
                            return;
                        }

                        onProgress?.Invoke(AITConvertCore.BuildPhase.Complete, 1f, "패키징 완료!");
                        Debug.Log($"[AIT] ✓ 비동기 패키징 완료: {distPath}");
                        onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                        return;
                    }

                    if (ct.IsCancellationRequested)
                    {
                        onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                        return;
                    }

                    // 재시도: clean → install → build
                    RetryGraniteBuildAsync(ctx, ct, onProgress, onComplete);
                }
            );
        }

        /// <summary>
        /// ait build를 먼저 시도하고, 실패 시 granite build로 폴백합니다 (비동기).
        /// package.json scripts에 의존하지 않고 CLI를 직접 호출합니다.
        /// </summary>
        internal static void RunAitOrGraniteBuildAsync(
            AITPackageBuilder.PackageContext ctx,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            Debug.Log("[AIT] ait build 시도 중...");
            AITNpmRunner.RunNpmCommandWithCacheAsync(
                ctx.BuildProjectPath, ctx.PnpmPath, "exec ait build", ctx.LocalCachePath,
                onComplete: (aitResult) =>
                {
                    if (aitResult == AITConvertCore.AITExportError.SUCCEED)
                    {
                        Debug.Log("[AIT] ✓ ait build 성공");
                        onComplete?.Invoke(aitResult);
                        return;
                    }

                    if (aitResult == AITConvertCore.AITExportError.CANCELLED)
                    {
                        onComplete?.Invoke(aitResult);
                        return;
                    }

                    // ait build 실패 → granite build 폴백
                    Debug.Log("[AIT] ait build 실패, granite build로 폴백합니다...");
                    AITNpmRunner.RunNpmCommandWithCacheAsync(
                        ctx.BuildProjectPath, ctx.PnpmPath, "exec granite build", ctx.LocalCachePath,
                        onComplete: (graniteResult) =>
                        {
                            if (graniteResult == AITConvertCore.AITExportError.SUCCEED)
                                Debug.Log("[AIT] ✓ granite build 폴백 성공");
                            onComplete?.Invoke(graniteResult);
                        },
                        onOutputReceived: (line) =>
                        {
                            onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.75f, line);
                        },
                        additionalEnvVars: ctx.UnityMetadataEnv
                    );
                },
                onOutputReceived: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.6f, line);
                },
                additionalEnvVars: ctx.UnityMetadataEnv
            );
        }

        /// <summary>
        /// granite build 실패 후 clean install → build 재시도 (비동기)
        /// </summary>
        internal static void RetryGraniteBuildAsync(
            AITPackageBuilder.PackageContext ctx,
            CancellationToken ct,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            Debug.Log("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
            NodeModulesValidator.CleanNodeModules(ctx.BuildProjectPath);
            onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.5f, "빌드 실패 후 재설치 중...");

            AITNpmRunner.RunNpmCommandWithCacheAsync(
                ctx.BuildProjectPath, ctx.PnpmPath, "install --no-frozen-lockfile", ctx.LocalCachePath,
                onComplete: (retryInstallResult) =>
                {
                    if (retryInstallResult != AITConvertCore.AITExportError.SUCCEED)
                    {
                        // 실제 터미널 실패 경로 — 캡처 유지. 단일 이슈(Sentry SDK-VH) 안에서 원인 구분이
                        // 가능하도록 실패 카테고리(AITExportError)를 함께 남긴다.
                        Debug.LogError($"[AIT] granite build 실패 후 pnpm install 재시도도 실패 (결과: {retryInstallResult})");
                        onComplete?.Invoke(retryInstallResult);
                        return;
                    }

                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.7f, "granite build 재시도 중...");

                    RunAitOrGraniteBuildAsync(ctx, onProgress,
                        onComplete: (retryBuildResult) =>
                        {
                            if (retryBuildResult == AITConvertCore.AITExportError.SUCCEED)
                            {
                                string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
                                AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);

                                var distValidation = AITBuildValidator.ValidateDistOutput(ctx.BuildProjectPath);
                                if (distValidation != AITConvertCore.AITExportError.SUCCEED)
                                {
                                    Debug.LogError($"[AIT] granite build 재시도도 출력물 검증 실패 (결과: {distValidation})");
                                    onComplete?.Invoke(distValidation);
                                    return;
                                }

                                onProgress?.Invoke(AITConvertCore.BuildPhase.Complete, 1f, "패키징 완료!");
                                Debug.Log($"[AIT] ✓ 비동기 패키징 완료 (재시도 성공): {distPath}");
                            }
                            else
                            {
                                Debug.LogError($"[AIT] granite build 재시도도 실패 (결과: {retryBuildResult})");
                            }
                            onComplete?.Invoke(retryBuildResult);
                        }
                    );
                },
                onOutputReceived: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.55f, line);
                }
            );
        }
    }
}
