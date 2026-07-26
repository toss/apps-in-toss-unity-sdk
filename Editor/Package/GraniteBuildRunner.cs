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

            var result = RunVersionedBuildSync(ctx, "ait build");
            if (result == AITConvertCore.AITExportError.SUCCEED) return result;
            if (result == AITConvertCore.AITExportError.CANCELLED) return result;

            // 재시도: clean → install → build
            Debug.Log("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
            NodeModulesValidator.CleanNodeModules(ctx.BuildProjectPath);

            var installResult = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "install --no-frozen-lockfile",
                ctx.LocalCachePath, "pnpm install (빌드 실패 후)...");
            if (installResult != AITConvertCore.AITExportError.SUCCEED) return installResult;
            PnpmInstallStateMarker.WriteMarkerAfterSuccessfulInstall(ctx.BuildProjectPath);

            result = RunVersionedBuildSync(ctx, "ait build (재시도)");
            if (result == AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.Log("[AIT] ✓ granite build 재시도 성공");
            }
            else
            {
                // 동기 재시도 실패 — Console 진단만. Sentry 단일 이벤트는 상위
                // (ShowBuildFailedDialog → CaptureBuildError)에 위임 (cascade 중복 방지, SDK-10Q).
                AITLog.Error("[AIT] granite build 재시도도 실패", sentryCapture: false);
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
        /// web-framework 3.x: vite build → dist/web 으로 빌드. (3.x ait build는 vite를 실행하지 않음)
        /// </summary>
        internal const string ViteOutDir = "dist/web";

        /// <summary>
        /// 설치될 @apps-in-toss/web-framework의 메이저 버전을 ait-build/package.json에서 파싱합니다.
        /// 파싱 불가능한 모든 케이스(파일 없음, 파싱 실패, 비숫자 등)는 2를 반환해
        /// 검증된 2.x(granite) 경로로 폴백합니다 (보수적 기본값 — stable 무회귀).
        /// </summary>
        internal static int GetWebFrameworkMajor(string buildProjectPath)
        {
            try
            {
                string pkgPath = Path.Combine(buildProjectPath, "package.json");
                if (!File.Exists(pkgPath)) return 2;

                var pkg = MiniJson.Deserialize(File.ReadAllText(pkgPath)) as System.Collections.Generic.Dictionary<string, object>;
                var deps = pkg != null && pkg.ContainsKey("dependencies")
                    ? pkg["dependencies"] as System.Collections.Generic.Dictionary<string, object>
                    : null;
                if (deps == null || !deps.ContainsKey("@apps-in-toss/web-framework")) return 2;

                string ver = (deps["@apps-in-toss/web-framework"] as string);
                if (string.IsNullOrEmpty(ver)) return 2;
                ver = ver.TrimStart('^', '~', '>', '=', 'v', ' ');

                // 선행 숫자만 추출 (예: "3.0.0-beta.9d42c0b" → "3", "2.6.1" → "2")
                var sb = new System.Text.StringBuilder();
                foreach (char c in ver)
                {
                    if (char.IsDigit(c)) sb.Append(c);
                    else break;
                }
                if (sb.Length == 0) return 2;
                return int.Parse(sb.ToString());
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] web-framework 버전 파싱 실패 (2.x 경로로 폴백): {e.Message}");
                return 2;
            }
        }

        /// <summary>
        /// web-framework 버전에 따라 빌드 전략을 선택합니다 (동기).
        /// - 3.x: vite build(→dist/web) → ait build (CLI가 vite를 실행하지 않으므로 2단계)
        /// - 2.x: ait build → granite build 폴백 (기존 동작)
        /// </summary>
        internal static AITConvertCore.AITExportError RunVersionedBuildSync(AITPackageBuilder.PackageContext ctx, string label)
        {
            int major = GetWebFrameworkMajor(ctx.BuildProjectPath);
            if (major >= 3)
            {
                return Run3xBuildSync(ctx, label);
            }
            return RunAitOrGraniteBuildSync(ctx, label);
        }

        /// <summary>
        /// web-framework 3.x 빌드 (동기): vite build(→dist/web) 후 ait build 패키징.
        /// 3.x ait build는 webBundleDir(=dist/web)만 패키징하고 vite를 실행하지 않으므로
        /// vite를 먼저 직접 호출해야 한다. granite 폴백 없음(3.x엔 granite bin이 없음).
        /// </summary>
        internal static AITConvertCore.AITExportError Run3xBuildSync(AITPackageBuilder.PackageContext ctx, string label)
        {
            Debug.Log($"[AIT] {label}: web-framework 3.x 감지 — vite build → ait build (2단계)");

            var viteResult = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, $"exec vite build --outDir {ViteOutDir}", ctx.LocalCachePath,
                $"{label} (vite build)...", additionalEnvVars: ctx.UnityMetadataEnv);
            if (viteResult != AITConvertCore.AITExportError.SUCCEED) return viteResult;

            Debug.Log("[AIT] ✓ vite build 완료 (dist/web). ait build로 패키징합니다...");

            // ait build 직전: cli.js를 패치해 setMetadata가 runtimeVersion을 emit하도록 만든다
            // (3.x deploy 게이트 통과용). CANCELLED만 전파, 그 외 실패는 경고 후 계속.
            var patchResult = RunRuntimeVersionPatchSync(ctx, label);
            if (patchResult == AITConvertCore.AITExportError.CANCELLED) return patchResult;

            return AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "exec ait build", ctx.LocalCachePath,
                $"{label} (ait build)...", additionalEnvVars: ctx.UnityMetadataEnv);
        }

        /// <summary>
        /// ait build 직전에 web-framework cli.js를 패치해 setMetadata가 runtimeVersion을
        /// 직접 emit하도록 만든다 (3.x deploy 게이트 "runtimeVersion이 없습니다" 통과용).
        /// 빌드된 .ait 자체는 손대지 않으므로 봉인이 깨지지 않는다.
        ///
        /// ait-patch-cli.mjs는 항상 exit 0이며 (2.x cli.js엔 setMetadata 없음/구조 변경/
        /// 이미 설정됨 → graceful no-op), 패치 자체는 빌드를 막지 않는다. 따라서 사용자
        /// 취소(CANCELLED)만 상위로 전파하고, 그 외 비정상 종료는 경고만 남기고 ait build를
        /// 계속 진행한다 (패치 실패가 빌드 실패로 번지지 않게 — stable 무회귀).
        /// </summary>
        internal static AITConvertCore.AITExportError RunRuntimeVersionPatchSync(AITPackageBuilder.PackageContext ctx, string label)
        {
            var result = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "exec node ait-patch-cli.mjs", ctx.LocalCachePath,
                $"{label} (runtimeVersion 패치)...", additionalEnvVars: ctx.UnityMetadataEnv);
            if (result == AITConvertCore.AITExportError.CANCELLED) return result;
            if (result != AITConvertCore.AITExportError.SUCCEED)
                Debug.LogWarning($"[AIT] runtimeVersion 패치 스크립트 비정상 종료 (결과: {result}) — 무시하고 ait build를 계속합니다.");
            return AITConvertCore.AITExportError.SUCCEED;
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

            RunVersionedBuildAsync(ctx, onProgress,
                onComplete: (buildResult) =>
                {
                    if (buildResult == AITConvertCore.AITExportError.SUCCEED)
                    {
                        string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
                        AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);

                        var distValidation = AITBuildValidator.ValidateDistOutput(ctx.BuildProjectPath);
                        if (distValidation != AITConvertCore.AITExportError.SUCCEED)
                        {
                            // 출력물 검증 실패 → 재시도 진입(중간 단계). Sentry 캡처 불필요 (상위 단일 캡처에 위임).
                            AITLog.Error("[AIT] granite build가 exit code 0으로 완료되었으나 출력물 검증 실패. 재시도합니다...", sentryCapture: false);
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
        /// web-framework 버전에 따라 빌드 전략을 선택합니다 (비동기).
        /// - 3.x: vite build(→dist/web) → ait build
        /// - 2.x: ait build → granite build 폴백 (기존 동작)
        /// </summary>
        internal static void RunVersionedBuildAsync(
            AITPackageBuilder.PackageContext ctx,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            int major = GetWebFrameworkMajor(ctx.BuildProjectPath);
            if (major >= 3)
            {
                Run3xBuildAsync(ctx, onProgress, onComplete);
                return;
            }
            RunAitOrGraniteBuildAsync(ctx, onProgress, onComplete);
        }

        /// <summary>
        /// web-framework 3.x 빌드 (비동기): vite build(→dist/web) 후 ait build 패키징.
        /// granite 폴백 없음(3.x엔 granite bin이 없음).
        /// </summary>
        internal static void Run3xBuildAsync(
            AITPackageBuilder.PackageContext ctx,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            Debug.Log("[AIT] web-framework 3.x 감지 — vite build → ait build (2단계)");
            AITNpmRunner.RunNpmCommandWithCacheAsync(
                ctx.BuildProjectPath, ctx.PnpmPath, $"exec vite build --outDir {ViteOutDir}", ctx.LocalCachePath,
                onComplete: (viteResult) =>
                {
                    if (viteResult != AITConvertCore.AITExportError.SUCCEED)
                    {
                        if (viteResult != AITConvertCore.AITExportError.CANCELLED)
                            Debug.LogError("[AIT] vite build 실패 (3.x)");
                        onComplete?.Invoke(viteResult);
                        return;
                    }

                    Debug.Log("[AIT] ✓ vite build 완료 (dist/web). ait build로 패키징합니다...");

                    // ait build 직전: cli.js 패치(runtimeVersion 주입) → 그 다음 ait build.
                    // 패치 자체 실패는 빌드를 막지 않고, 사용자 취소(CANCELLED)만 전파한다.
                    RunRuntimeVersionPatchAsync(ctx, (patchResult) =>
                    {
                        if (patchResult == AITConvertCore.AITExportError.CANCELLED)
                        {
                            onComplete?.Invoke(patchResult);
                            return;
                        }
                        AITNpmRunner.RunNpmCommandWithCacheAsync(
                            ctx.BuildProjectPath, ctx.PnpmPath, "exec ait build", ctx.LocalCachePath,
                            onComplete: (aitResult) =>
                            {
                                if (aitResult == AITConvertCore.AITExportError.SUCCEED)
                                    Debug.Log("[AIT] ✓ ait build 패키징 성공 (3.x)");
                                onComplete?.Invoke(aitResult);
                            },
                            onOutputReceived: (line) =>
                            {
                                onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.85f, line);
                            },
                            additionalEnvVars: ctx.UnityMetadataEnv
                        );
                    });
                },
                onOutputReceived: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.7f, line);
                },
                additionalEnvVars: ctx.UnityMetadataEnv
            );
        }

        /// <summary>
        /// <see cref="RunRuntimeVersionPatchSync"/>의 비동기 버전.
        /// CANCELLED만 onComplete로 전파하고, 그 외 비정상 종료는 경고 후 SUCCEED로 흘려
        /// 호출부가 ait build를 계속 진행하게 한다.
        /// </summary>
        internal static void RunRuntimeVersionPatchAsync(
            AITPackageBuilder.PackageContext ctx,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            Debug.Log("[AIT] runtimeVersion 패치: cli.js setMetadata에 runtimeVersion 주입 시도...");
            AITNpmRunner.RunNpmCommandWithCacheAsync(
                ctx.BuildProjectPath, ctx.PnpmPath, "exec node ait-patch-cli.mjs", ctx.LocalCachePath,
                onComplete: (result) =>
                {
                    if (result == AITConvertCore.AITExportError.CANCELLED)
                    {
                        onComplete?.Invoke(result);
                        return;
                    }
                    if (result != AITConvertCore.AITExportError.SUCCEED)
                        Debug.LogWarning($"[AIT] runtimeVersion 패치 스크립트 비정상 종료 (결과: {result}) — 무시하고 ait build를 계속합니다.");
                    onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                },
                onOutputReceived: (line) =>
                {
                    Debug.Log($"[AIT] [patch] {line}");
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
                        // granite build 실패 후 재설치까지 실패한 경로. Console 진단용으로 실패
                        // 카테고리(AITExportError)를 남기되, Sentry 단일 이벤트는 상위
                        // (ShowBuildFailedDialog → CaptureBuildError)에 위임한다 (SDK-VH cascade 중복 방지).
                        AITLog.Error($"[AIT] granite build 실패 후 pnpm install 재시도도 실패 (결과: {retryInstallResult})", sentryCapture: false);
                        onComplete?.Invoke(retryInstallResult);
                        return;
                    }

                    PnpmInstallStateMarker.WriteMarkerAfterSuccessfulInstall(ctx.BuildProjectPath);
                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.7f, "granite build 재시도 중...");

                    RunVersionedBuildAsync(ctx, onProgress,
                        onComplete: (retryBuildResult) =>
                        {
                            if (retryBuildResult == AITConvertCore.AITExportError.SUCCEED)
                            {
                                string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
                                AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);

                                var distValidation = AITBuildValidator.ValidateDistOutput(ctx.BuildProjectPath);
                                if (distValidation != AITConvertCore.AITExportError.SUCCEED)
                                {
                                    // 재시도 출력물 검증 실패 — Console 진단만, Sentry는 상위 단일 캡처에 위임.
                                    AITLog.Error($"[AIT] granite build 재시도도 출력물 검증 실패 (결과: {distValidation})", sentryCapture: false);
                                    onComplete?.Invoke(distValidation);
                                    return;
                                }

                                onProgress?.Invoke(AITConvertCore.BuildPhase.Complete, 1f, "패키징 완료!");
                                Debug.Log($"[AIT] ✓ 비동기 패키징 완료 (재시도 성공): {distPath}");
                            }
                            else
                            {
                                // 비동기 재시도 실패 — Console 진단만, Sentry는 상위 단일 캡처에 위임 (SDK-10Q).
                                AITLog.Error($"[AIT] granite build 재시도도 실패 (결과: {retryBuildResult})", sentryCapture: false);
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
