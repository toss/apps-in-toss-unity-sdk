using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// ait-build 패키징 담당 클래스
    /// </summary>
    internal static class AITPackageBuilder
    {
        // SDK BuildConfig 경로 캐시 (빌드 중 반복 검색 방지)
        private static string _cachedSdkBuildConfigPath = null;
        private static string _cachedSdkRuntimePath = null;

        /// <summary>
        /// 캐시된 SDK 경로를 초기화합니다.
        /// Domain reload 시 자동으로 초기화됨
        /// </summary>
        internal static void ClearPathCache()
        {
            _cachedSdkBuildConfigPath = null;
            _cachedSdkRuntimePath = null;
        }

        #region Packaging Common

        /// <summary>
        /// 동기/비동기 패키징 공통 준비 컨텍스트
        /// </summary>
        private class PackageContext
        {
            public string BuildProjectPath;
            public string PnpmPath;
            public string LocalCachePath;
            public Dictionary<string, string> UnityMetadataEnv;
        }

        /// <summary>
        /// 병렬 pnpm install 상태를 추적하는 컨텍스트.
        /// WebGL 빌드 전에 pnpm install을 백그라운드에서 시작하고,
        /// WebGL 빌드 완료 후 결과를 확인하는 데 사용합니다.
        /// </summary>
        internal class EarlyPackageContext
        {
            public string BuildProjectPath;
            public string PnpmPath;
            public string LocalCachePath;
            public Dictionary<string, string> UnityMetadataEnv;
            // PnpmInstallResult에 값이 설정되면 완료로 간주 (단일 volatile 필드로 원자적 시그널링)
            // volatile int: -1 = 미완료, 0+ = AITExportError 값
            private volatile int _pnpmInstallResultCode = -1;
            public bool PnpmInstallCompleted => _pnpmInstallResultCode >= 0;
            public AITConvertCore.AITExportError? PnpmInstallResult
            {
                get { return _pnpmInstallResultCode < 0 ? (AITConvertCore.AITExportError?)null : (AITConvertCore.AITExportError)_pnpmInstallResultCode; }
                set { _pnpmInstallResultCode = value.HasValue ? (int)value.Value : -1; }
            }
            private CancellationTokenSource _pnpmCancellation = new CancellationTokenSource();
            private volatile bool _pnpmCancellationDisposed;

            public CancellationToken PnpmCancellationToken => _pnpmCancellation.Token;

            /// <summary>
            /// pnpm install을 안전하게 취소하고 CTS를 정리합니다.
            /// 여러 번 호출해도 안전합니다 (double-dispose 방지).
            /// </summary>
            public void CancelAndDisposePnpm()
            {
                if (_pnpmCancellationDisposed) return;
                _pnpmCancellationDisposed = true;
                try { _pnpmCancellation.Cancel(); } catch (ObjectDisposedException) { }
                try { _pnpmCancellation.Dispose(); } catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// pnpm install 재시도 정책 (args, label, cleanFirst)
        /// </summary>
        private static readonly (string args, string label, bool cleanFirst)[] PnpmInstallStages =
        {
            ("install --frozen-lockfile", "frozen-lockfile", false),
            ("install --no-frozen-lockfile", "lockfile 갱신", false),
            ("install --no-frozen-lockfile", "clean 재시도", true),
        };

        /// <summary>
        /// 동기/비동기 경로의 공통 준비 로직을 수행합니다.
        /// Node.js 대기, 프로필 폴백, 폴더 정리, 파일 복사, pnpm 경로 확인, node_modules 검증.
        /// </summary>
        private static (PackageContext ctx, AITConvertCore.AITExportError error) PreparePackaging(
            string projectPath, string webglPath, AITBuildProfile profile)
        {
            // 백그라운드 Node.js/pnpm 설치 대기 (빌드 경로이므로 blocking으로 대기)
            if (AITPackageInitializer.IsInstalling)
            {
                Debug.Log("[AIT] Node.js/pnpm 설치가 진행 중입니다. 완료될 때까지 대기합니다...");
                if (!AITPackageInitializer.WaitForInstallation(blocking: true))
                {
                    Debug.LogError("[AIT] 설치 대기 타임아웃. 빌드를 중단합니다.");
                    return (null, AITConvertCore.AITExportError.NODE_NOT_FOUND);
                }
            }

            if (profile == null) profile = AITBuildProfile.CreateProductionProfile();

            string buildProjectPath = Path.Combine(projectPath, "ait-build");
            PrepareAitBuildFolder(buildProjectPath);

            // npm 경로 확인
            string npmPath = AITNpmRunner.FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("[AIT] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                return (null, AITConvertCore.AITExportError.NODE_NOT_FOUND);
            }

            // Step 1-2: 파일 복사
            Debug.Log("[AIT] Step 1/3: Vite 프로젝트 구조 생성 중...");
            CopyBuildConfigFromTemplate(buildProjectPath);

            Debug.Log("[AIT] Step 2/3: Unity WebGL 빌드 복사 중...");
            var copyResult = CopyWebGLToPublic(webglPath, buildProjectPath, profile);
            if (copyResult != AITConvertCore.AITExportError.SUCCEED)
                return (null, copyResult);

            // pnpm 경로 확인
            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPath))
            {
                Debug.LogError("[AIT] pnpm 설치에 실패했습니다. Unity Console에서 에러를 확인해주세요.");
                AITPackageManagerHelper.ShowInstallationFailureDialog();
                return (null, AITConvertCore.AITExportError.FAIL_NPM_BUILD);
            }

            // node_modules 무결성 검증
            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");
            if (!ValidateNodeModulesIntegrity(buildProjectPath))
            {
                Debug.Log("[AIT] node_modules 무결성 검증 실패. 정리 후 재설치합니다.");
                CleanNodeModules(buildProjectPath);
            }

            return (new PackageContext
            {
                BuildProjectPath = buildProjectPath,
                PnpmPath = pnpmPath,
                LocalCachePath = localCachePath,
                UnityMetadataEnv = AITUnityMetadata.BuildEnvironmentVariables()
            }, AITConvertCore.AITExportError.SUCCEED);
        }

        #endregion

        #region Parallel pnpm install (WebGL 빌드와 병렬 실행)

        /// <summary>
        /// WebGL 빌드 전에 실행 가능한 사전 준비를 수행합니다.
        /// BuildConfig 복사와 node_modules 무결성 검증까지 완료하여
        /// pnpm install을 즉시 시작할 수 있는 상태를 만듭니다.
        /// </summary>
        internal static (EarlyPackageContext ctx, AITConvertCore.AITExportError error) PrepareEarlyPackaging(
            string projectPath, AITBuildProfile profile)
        {
            // 백그라운드 Node.js/pnpm 설치 대기
            if (AITPackageInitializer.IsInstalling)
            {
                Debug.Log("[AIT] [병렬] Node.js/pnpm 설치가 진행 중입니다. 완료될 때까지 대기합니다...");
                if (!AITPackageInitializer.WaitForInstallation(blocking: true))
                {
                    Debug.LogError("[AIT] [병렬] 설치 대기 타임아웃. 빌드를 중단합니다.");
                    return (null, AITConvertCore.AITExportError.NODE_NOT_FOUND);
                }
            }

            if (profile == null) profile = AITBuildProfile.CreateProductionProfile();

            string buildProjectPath = Path.Combine(projectPath, "ait-build");
            PrepareAitBuildFolder(buildProjectPath);

            // BuildConfig 복사 (WebGL 출력 불필요 — package.json, lockfile 등만)
            Debug.Log("[AIT] [병렬] BuildConfig 파일 복사 중...");
            CopyBuildConfigFromTemplate(buildProjectPath);

            // pnpm 경로 확인
            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPath))
            {
                Debug.LogError("[AIT] [병렬] pnpm 설치에 실패했습니다.");
                AITPackageManagerHelper.ShowInstallationFailureDialog();
                return (null, AITConvertCore.AITExportError.FAIL_NPM_BUILD);
            }

            // node_modules 무결성 검증
            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");
            if (!ValidateNodeModulesIntegrity(buildProjectPath))
            {
                Debug.Log("[AIT] [병렬] node_modules 무결성 검증 실패. 정리 후 재설치합니다.");
                CleanNodeModules(buildProjectPath);
            }

            return (new EarlyPackageContext
            {
                BuildProjectPath = buildProjectPath,
                PnpmPath = pnpmPath,
                LocalCachePath = localCachePath,
                UnityMetadataEnv = AITUnityMetadata.BuildEnvironmentVariables(),
                PnpmInstallResult = null,
            }, AITConvertCore.AITExportError.SUCCEED);
        }

        /// <summary>
        /// ThreadPool에서 pnpm install을 백그라운드 OS 프로세스로 실행합니다.
        /// WebGL 빌드가 메인 스레드를 블로킹하는 동안 독립적으로 실행됩니다.
        /// EditorUtility 등 메인 스레드 전용 API를 사용하지 않습니다.
        /// </summary>
        internal static void StartPnpmInstallInBackground(EarlyPackageContext earlyCtx)
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
                        Debug.LogWarning($"[AIT] [병렬] 백그라운드 pnpm install 결과: {result}");

                    // Result 설정이 곧 완료 시그널 (PnpmInstallCompleted는 이 값으로 판단)
                    earlyCtx.PnpmInstallResult = result;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AIT] [병렬] 백그라운드 pnpm install 예외: {e.Message}");
                    earlyCtx.PnpmInstallResult = AITConvertCore.AITExportError.FAIL_NPM_BUILD;
                }
            });
        }

        /// <summary>
        /// 백그라운드 스레드에서 pnpm install 3단계 재시도를 실행합니다.
        /// 메인 스레드 전용 API(EditorUtility 등)를 사용하지 않으며,
        /// Process.HasExited 폴링 + CancellationToken으로 취소를 지원합니다.
        /// </summary>
        private static AITConvertCore.AITExportError RunPnpmInstallInThread(EarlyPackageContext earlyCtx)
        {
            var ct = earlyCtx.PnpmCancellationToken;
            var additionalPaths = AITNpmRunner.BuildAdditionalPaths(earlyCtx.PnpmPath);

            foreach (var (args, label, cleanFirst) in PnpmInstallStages)
            {
                if (ct.IsCancellationRequested)
                    return AITConvertCore.AITExportError.CANCELLED;

                if (cleanFirst) CleanNodeModules(earlyCtx.BuildProjectPath);

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
                        Debug.LogWarning($"[AIT] [병렬] pnpm {label} 실패 (Exit Code: {process.ExitCode})");
                        if (!string.IsNullOrEmpty(output))
                            Debug.LogWarning($"[AIT] [병렬] 출력:\n{output.Trim()}");
                        if (!string.IsNullOrEmpty(error))
                            Debug.LogWarning($"[AIT] [병렬] 오류:\n{error.Trim()}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AIT] [병렬] pnpm {label} 실행 오류: {e.Message}");
                }

                Debug.LogWarning($"[AIT] [병렬] pnpm install ({label}) 실패, 다음 단계로...");
            }

            Debug.LogError("[AIT] [병렬] pnpm install 실패 (모든 재시도 후에도 실패)");
            return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
        }

        /// <summary>
        /// WebGL 빌드 완료 후 나머지 패키징을 수행합니다.
        /// 1. WebGL 출력을 public/ 폴더로 복사
        /// 2. 백그라운드 pnpm install 완료 대기
        /// 3. granite build 실행
        /// </summary>
        internal static void CompletePackagingAfterWebGLBuild(
            EarlyPackageContext earlyCtx,
            string webglPath,
            AITBuildProfile profile,
            Editor.AITPlayerSettingsBackup settingsBackup,
            Action<AITConvertCore.AITExportError> onComplete,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            bool skipGraniteBuild = false)
        {
            // Step 1: WebGL 출력을 public/ 폴더로 복사
            onProgress?.Invoke(AITConvertCore.BuildPhase.CopyingFiles, 0.15f, "WebGL 빌드 파일 복사 중...");
            Debug.Log("[AIT] [병렬] WebGL 빌드를 ait-build/public으로 복사 중...");

            var copyResult = CopyWebGLToPublic(webglPath, earlyCtx.BuildProjectPath, profile);
            if (copyResult != AITConvertCore.AITExportError.SUCCEED)
            {
                earlyCtx.CancelAndDisposePnpm();
                settingsBackup.Restore();
                onComplete?.Invoke(copyResult);
                return;
            }

            // Step 2: 백그라운드 pnpm install 완료 확인
            if (earlyCtx.PnpmInstallCompleted)
            {
                // 이미 완료됨 — 즉시 진행
                Debug.Log("[AIT] [병렬] ✓ pnpm install 이미 완료 (WebGL 빌드 시간에 숨겨짐)");
                OnPnpmInstallReady(earlyCtx, settingsBackup, onComplete, onProgress, skipGraniteBuild);
            }
            else
            {
                // 아직 진행 중 — EditorApplication.update로 폴링
                Debug.Log("[AIT] [병렬] pnpm install 진행 중. 완료를 대기합니다...");
                onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.3f, "pnpm install 완료 대기 중...");

                void PollPnpmCompletion()
                {
                    if (AITConvertCore.IsCancelled())
                    {
                        EditorApplication.update -= PollPnpmCompletion;
                        earlyCtx.CancelAndDisposePnpm();
                        settingsBackup.Restore();
                        onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                        return;
                    }

                    if (!earlyCtx.PnpmInstallCompleted) return;

                    EditorApplication.update -= PollPnpmCompletion;
                    Debug.Log("[AIT] [병렬] ✓ pnpm install 완료 확인");
                    OnPnpmInstallReady(earlyCtx, settingsBackup, onComplete, onProgress, skipGraniteBuild);
                }

                EditorApplication.update += PollPnpmCompletion;
            }
        }

        /// <summary>
        /// pnpm install이 완료된 후 결과를 확인하고 granite build를 실행합니다.
        /// </summary>
        private static void OnPnpmInstallReady(
            EarlyPackageContext earlyCtx,
            Editor.AITPlayerSettingsBackup settingsBackup,
            Action<AITConvertCore.AITExportError> onComplete,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            bool skipGraniteBuild = false)
        {
            var installResult = earlyCtx.PnpmInstallResult ?? AITConvertCore.AITExportError.FAIL_NPM_BUILD;
            if (installResult != AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.LogError($"[AIT] [병렬] pnpm install 실패: {installResult}");
                earlyCtx.CancelAndDisposePnpm();
                settingsBackup.Restore();
                onComplete?.Invoke(installResult);
                return;
            }

            if (AITConvertCore.IsCancelled())
            {
                earlyCtx.CancelAndDisposePnpm();
                settingsBackup.Restore();
                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                return;
            }

            if (skipGraniteBuild)
            {
                Debug.Log("[AIT] [병렬] granite build 스킵 (Dev Server 모드)");
                earlyCtx.CancelAndDisposePnpm();
                AITConvertCore.SetCurrentAsyncTask(null);
                settingsBackup.Restore();
                onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                return;
            }

            // PackageContext로 변환하여 기존 granite build 로직 재사용
            var ctx = new PackageContext
            {
                BuildProjectPath = earlyCtx.BuildProjectPath,
                PnpmPath = earlyCtx.PnpmPath,
                LocalCachePath = earlyCtx.LocalCachePath,
                UnityMetadataEnv = earlyCtx.UnityMetadataEnv,
            };

            // granite build 실행 (비동기)
            var cancellationToken = earlyCtx.PnpmCancellationToken;
            RunGraniteBuildAsync(ctx, cancellationToken, onProgress,
                (buildResult) =>
                {
                    earlyCtx.CancelAndDisposePnpm();
                    AITConvertCore.SetCurrentAsyncTask(null);
                    settingsBackup.Restore();

                    if (buildResult == AITConvertCore.AITExportError.SUCCEED)
                    {
                        Debug.Log("[AIT] [병렬] ✓ 비동기 미니앱 생성 완료!");
                    }

                    onComplete?.Invoke(buildResult);
                });
        }

        #endregion

        #region Sync Packaging

        /// <summary>
        /// WebGL 빌드를 ait-build로 패키징
        /// </summary>
        internal static AITConvertCore.AITExportError PackageWebGLBuild(string projectPath, string webglPath, AITBuildProfile profile = null, bool skipGraniteBuild = false)
        {
            Debug.Log("[AIT] Vite 기반 빌드 패키징 시작...");

            var (ctx, prepError) = PreparePackaging(projectPath, webglPath, profile);
            if (ctx == null) return prepError;

            // Step 3: pnpm install & build
            Debug.Log("[AIT] Step 3/3: pnpm install & build 실행 중...");

            var installResult = RunPnpmInstallSync(ctx);
            if (installResult != AITConvertCore.AITExportError.SUCCEED) return installResult;

            if (skipGraniteBuild)
            {
                Debug.Log("[AIT] granite build 스킵 (Dev Server 모드)");
                return AITConvertCore.AITExportError.SUCCEED;
            }

            var buildResult = RunGraniteBuildSync(ctx);
            if (buildResult != AITConvertCore.AITExportError.SUCCEED) return buildResult;

            string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
            AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);
            Debug.Log($"[AIT] ✓ 패키징 완료: {distPath}");

            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// pnpm install 3단계 재시도 (동기)
        /// </summary>
        private static AITConvertCore.AITExportError RunPnpmInstallSync(PackageContext ctx)
        {
            foreach (var (args, label, cleanFirst) in PnpmInstallStages)
            {
                if (cleanFirst) CleanNodeModules(ctx.BuildProjectPath);
                var result = AITNpmRunner.RunNpmCommandWithCache(
                    ctx.BuildProjectPath, ctx.PnpmPath, args, ctx.LocalCachePath,
                    $"pnpm {label}...");
                if (result == AITConvertCore.AITExportError.SUCCEED) return result;
                if (result == AITConvertCore.AITExportError.CANCELLED) return result;
                Debug.LogWarning($"[AIT] pnpm install ({label}) 실패, 다음 단계로...");
            }
            Debug.LogError("[AIT] pnpm install 실패 (모든 재시도 후에도 실패)");
            return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
        }

        /// <summary>
        /// granite build 실행 + 실패 시 1회 재시도 (동기)
        /// </summary>
        private static AITConvertCore.AITExportError RunGraniteBuildSync(PackageContext ctx)
        {
            Debug.Log("[AIT] granite build 실행 중...");
            Debug.Log($"[AIT] UNITY_METADATA: {ctx.UnityMetadataEnv["UNITY_METADATA"]}");

            var result = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "run build", ctx.LocalCachePath,
                "granite build...", additionalEnvVars: ctx.UnityMetadataEnv);
            if (result == AITConvertCore.AITExportError.SUCCEED) return result;
            if (result == AITConvertCore.AITExportError.CANCELLED) return result;

            // 재시도: clean → install → build
            Debug.LogWarning("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
            CleanNodeModules(ctx.BuildProjectPath);

            var installResult = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "install --no-frozen-lockfile",
                ctx.LocalCachePath, "pnpm install (빌드 실패 후)...");
            if (installResult != AITConvertCore.AITExportError.SUCCEED) return installResult;

            result = AITNpmRunner.RunNpmCommandWithCache(
                ctx.BuildProjectPath, ctx.PnpmPath, "run build", ctx.LocalCachePath,
                "granite build (재시도)...", additionalEnvVars: ctx.UnityMetadataEnv);
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

        #endregion

        /// <summary>
        /// package.json의 dependencies를 머지합니다.
        /// </summary>
        internal static void MergePackageJson(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "package.json");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "package.json");
            string destFile = Path.Combine(destPath, "package.json");

            // 프로젝트 파일 없으면 SDK 복사
            if (!File.Exists(projectFile))
            {
                File.Copy(sdkFile, destFile, true);
                Debug.Log("[AIT]   ✓ package.json (SDK에서 복사)");
                return;
            }

            try
            {
                string projectContent = File.ReadAllText(projectFile);
                string sdkContent = File.ReadAllText(sdkFile);

                // 간단한 JSON 머지 (dependencies와 devDependencies)
                var projectJson = MiniJson.Deserialize(projectContent) as Dictionary<string, object>;
                var sdkJson = MiniJson.Deserialize(sdkContent) as Dictionary<string, object>;

                if (projectJson == null || sdkJson == null)
                {
                    Debug.LogWarning("[AIT] package.json 파싱 실패, SDK 버전 사용");
                    File.Copy(sdkFile, destFile, true);
                    return;
                }

                // SDK의 기본 구조를 사용하고 dependencies만 머지
                var result = new Dictionary<string, object>(sdkJson);

                // dependencies 머지
                result["dependencies"] = MergeDependencies(
                    projectJson.ContainsKey("dependencies") ? projectJson["dependencies"] as Dictionary<string, object> : null,
                    sdkJson.ContainsKey("dependencies") ? sdkJson["dependencies"] as Dictionary<string, object> : null
                );

                // devDependencies 머지
                result["devDependencies"] = MergeDependencies(
                    projectJson.ContainsKey("devDependencies") ? projectJson["devDependencies"] as Dictionary<string, object> : null,
                    sdkJson.ContainsKey("devDependencies") ? sdkJson["devDependencies"] as Dictionary<string, object> : null
                );

                string mergedJson = MiniJson.Serialize(result);
                File.WriteAllText(destFile, mergedJson, new System.Text.UTF8Encoding(false));
                Debug.Log("[AIT]   ✓ package.json (dependencies 머지됨)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] package.json 머지 실패: {e.Message}, SDK 버전 사용");
                File.Copy(sdkFile, destFile, true);
            }
        }

        /// <summary>
        /// dependencies 딕셔너리를 머지합니다. SDK 패키지가 우선됩니다.
        /// </summary>
        internal static Dictionary<string, object> MergeDependencies(Dictionary<string, object> project, Dictionary<string, object> sdk)
        {
            var result = new Dictionary<string, object>();

            // 프로젝트 dependencies 먼저 추가
            if (project != null)
            {
                foreach (var kvp in project)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // SDK dependencies로 덮어쓰기 (SDK가 우선)
            if (sdk != null)
            {
                foreach (var kvp in sdk)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// tsconfig.json을 머지합니다.
        /// SDK의 필수 옵션을 유지하면서 사용자 옵션을 추가합니다.
        /// </summary>
        internal static void MergeTsConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "tsconfig.json");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "tsconfig.json");
            string destFile = Path.Combine(destPath, "tsconfig.json");

            // 프로젝트 파일 없으면 SDK 복사
            if (!File.Exists(projectFile))
            {
                File.Copy(sdkFile, destFile, true);
                Debug.Log("[AIT]   ✓ tsconfig.json (SDK에서 복사)");
                return;
            }

            try
            {
                string projectContent = File.ReadAllText(projectFile);
                string sdkContent = File.ReadAllText(sdkFile);

                var projectJson = MiniJson.Deserialize(projectContent) as Dictionary<string, object>;
                var sdkJson = MiniJson.Deserialize(sdkContent) as Dictionary<string, object>;

                if (projectJson == null || sdkJson == null)
                {
                    Debug.LogWarning("[AIT] tsconfig.json 파싱 실패, SDK 버전 사용");
                    File.Copy(sdkFile, destFile, true);
                    return;
                }

                // SDK의 기본 구조를 사용
                var result = new Dictionary<string, object>(sdkJson);

                // compilerOptions 머지
                var sdkCompilerOptions = sdkJson.ContainsKey("compilerOptions")
                    ? sdkJson["compilerOptions"] as Dictionary<string, object>
                    : new Dictionary<string, object>();
                var projectCompilerOptions = projectJson.ContainsKey("compilerOptions")
                    ? projectJson["compilerOptions"] as Dictionary<string, object>
                    : new Dictionary<string, object>();

                // SDK 필수 옵션 정의 (이 옵션들은 SDK 값으로 강제)
                var sdkRequiredOptions = new HashSet<string>
                {
                    "moduleResolution",  // bundler 필수
                    "esModuleInterop",   // 호환성 필수
                };

                // 머지된 compilerOptions 생성
                var mergedCompilerOptions = new Dictionary<string, object>();

                // 1. SDK 옵션 먼저 추가 (기본값)
                if (sdkCompilerOptions != null)
                {
                    foreach (var kvp in sdkCompilerOptions)
                    {
                        mergedCompilerOptions[kvp.Key] = kvp.Value;
                    }
                }

                // 2. 프로젝트 옵션으로 덮어쓰기 (SDK 필수 옵션 제외)
                if (projectCompilerOptions != null)
                {
                    foreach (var kvp in projectCompilerOptions)
                    {
                        if (!sdkRequiredOptions.Contains(kvp.Key))
                        {
                            mergedCompilerOptions[kvp.Key] = kvp.Value;
                        }
                    }
                }

                result["compilerOptions"] = mergedCompilerOptions;

                // include 배열 (프로젝트에 있으면 프로젝트 우선)
                if (projectJson.ContainsKey("include"))
                {
                    result["include"] = projectJson["include"];
                }

                // exclude 배열 (프로젝트에 있으면 사용)
                if (projectJson.ContainsKey("exclude"))
                {
                    result["exclude"] = projectJson["exclude"];
                }

                string mergedJson = MiniJson.Serialize(result);
                File.WriteAllText(destFile, mergedJson, new System.Text.UTF8Encoding(false));
                Debug.Log("[AIT]   ✓ tsconfig.json (compilerOptions 머지됨)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] tsconfig.json 머지 실패: {e.Message}, SDK 버전 사용");
                File.Copy(sdkFile, destFile, true);
            }
        }

        /// <summary>
        /// vite.config.ts를 마커 기반으로 업데이트합니다.
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// import 문, SDK_PLUGINS, SDK_GENERATED는 항상 SDK 최신 버전으로 갱신됩니다.
        /// </summary>
        internal static void UpdateViteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "vite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "vite.config.ts");
            string destFile = Path.Combine(destPath, "vite.config.ts");

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환
            string finalContent = sdkTemplate
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString());

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                // 프로젝트의 USER_CONFIG 영역 추출
                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    // SDK 템플릿의 USER_CONFIG를 프로젝트의 USER_CONFIG로 교체
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ vite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ vite.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ vite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// granite.config.ts를 마커 기반으로 업데이트합니다.
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// import 문, SDK_GENERATED는 항상 SDK 최신 버전으로 갱신됩니다.
        /// </summary>
        internal static void UpdateGraniteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "granite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "granite.config.ts");
            string destFile = Path.Combine(destPath, "granite.config.ts");

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환
            Debug.Log("[AIT] granite.config.ts placeholder 치환 중...");
            string finalContent = sdkTemplate
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ICON_URL%", config.iconUrl)
                .Replace("%AIT_BRIDGE_COLOR_MODE%", config.GetBridgeColorModeString())
                .Replace("%AIT_WEBVIEW_TYPE%", config.GetWebViewTypeString())
                .Replace("%AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%", config.allowsInlineMediaPlayback.ToString().ToLower())
                .Replace("%AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%", config.mediaPlaybackRequiresUserAction.ToString().ToLower())
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString())
                .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson())
                .Replace("%AIT_OUTDIR%", config.outdir);

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                // 프로젝트의 USER_CONFIG 영역 추출
                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    // SDK 템플릿의 USER_CONFIG를 프로젝트의 USER_CONFIG로 교체
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ granite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ granite.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ granite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// 프로젝트 BuildConfig의 추가 파일들을 재귀적으로 복사합니다.
        /// </summary>
        internal static void CopyAdditionalUserFiles(string projectBuildConfigPath, string destPath)
        {
            if (!Directory.Exists(projectBuildConfigPath)) return;

            // 루트 레벨에서 제외할 파일들
            var excludeRootFiles = new HashSet<string>
            {
                "package.json", "pnpm-lock.yaml", "vite.config.ts",
                "tsconfig.json", "unity-bridge.ts", "granite.config.ts"
            };

            // 제외할 폴더들
            var excludeFolders = new HashSet<string>
            {
                "node_modules",
                ".npm-cache",
                "dist"
            };

            CopyUserFilesRecursive(projectBuildConfigPath, destPath, excludeRootFiles, excludeFolders, isRoot: true);
        }

        /// <summary>
        /// 재귀적으로 사용자 파일을 복사합니다.
        /// </summary>
        private static void CopyUserFilesRecursive(
            string sourceDir,
            string destDir,
            HashSet<string> excludeRootFiles,
            HashSet<string> excludeFolders,
            bool isRoot)
        {
            // 대상 폴더 생성
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // 파일 복사
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);

                // 루트 레벨에서만 특정 파일 제외
                if (isRoot && excludeRootFiles.Contains(fileName))
                {
                    continue;
                }

                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);

                // 의미 있는 파일만 로그 출력
                if (fileName.EndsWith(".ts") || fileName.EndsWith(".tsx") ||
                    fileName.EndsWith(".js") || fileName.EndsWith(".jsx") ||
                    fileName.EndsWith(".css") || fileName.EndsWith(".scss"))
                {
                    Debug.Log($"[AIT]   ✓ {fileName} (사용자 추가 파일)");
                }
            }

            // 하위 폴더 재귀 복사
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);

                // 제외 폴더 스킵
                if (excludeFolders.Contains(dirName))
                {
                    continue;
                }

                string destSubDir = Path.Combine(destDir, dirName);
                CopyUserFilesRecursive(dir, destSubDir, excludeRootFiles, excludeFolders, isRoot: false);

                // 폴더 복사 완료 로그
                Debug.Log($"[AIT]   ✓ {dirName}/ (사용자 추가 폴더)");
            }
        }

        /// <summary>
        /// BuildConfig 템플릿에서 빌드 설정 파일들을 복사합니다.
        /// </summary>
        internal static void CopyBuildConfigFromTemplate(string buildProjectPath)
        {
            // 프로젝트 BuildConfig 경로 (사용자 커스터마이징 가능)
            string projectBuildConfigPath = Path.Combine(Application.dataPath, "WebGLTemplates/AITTemplate/BuildConfig~");

            // SDK의 BuildConfig 템플릿 경로 찾기 (캐싱 사용)
            string sdkBuildConfigPath = GetSdkBuildConfigPath();

            if (sdkBuildConfigPath == null)
            {
                Debug.LogError("[AIT] SDK BuildConfig 폴더를 찾을 수 없습니다.");
                Debug.LogError("[AIT] SDK가 올바르게 설치되었는지 확인하세요.");
                return;
            }

            Debug.Log($"[AIT] ✓ SDK BuildConfig 템플릿: {sdkBuildConfigPath}");

            // 프로젝트 BuildConfig 존재 여부 확인
            bool hasProjectBuildConfig = Directory.Exists(projectBuildConfigPath);
            if (hasProjectBuildConfig)
            {
                Debug.Log($"[AIT] ✓ 프로젝트 BuildConfig 발견: {projectBuildConfigPath}");
            }
            else
            {
                Debug.Log("[AIT] 프로젝트 BuildConfig 없음, SDK 버전 사용");
            }

            var config = UnityUtil.GetEditorConf();

            Debug.Log("[AIT] BuildConfig 파일 처리 중...");

            // 1. package.json - dependencies 머지
            MergePackageJson(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath);

            // 2. pnpm-lock.yaml - 프로젝트 우선, 없으면 SDK
            string pnpmLockProject = Path.Combine(projectBuildConfigPath, "pnpm-lock.yaml");
            string pnpmLockSdk = Path.Combine(sdkBuildConfigPath, "pnpm-lock.yaml");
            string pnpmLockDst = Path.Combine(buildProjectPath, "pnpm-lock.yaml");
            if (File.Exists(pnpmLockProject))
            {
                File.Copy(pnpmLockProject, pnpmLockDst, true);
                Debug.Log("[AIT]   ✓ pnpm-lock.yaml (프로젝트에서 복사)");
            }
            else if (File.Exists(pnpmLockSdk))
            {
                File.Copy(pnpmLockSdk, pnpmLockDst, true);
                Debug.Log("[AIT]   ✓ pnpm-lock.yaml (SDK에서 복사)");
            }

            // 3. vite.config.ts - 마커 기반 업데이트
            UpdateViteConfig(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath, config);

            // 4. granite.config.ts - 마커 기반 업데이트
            UpdateGraniteConfig(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath, config);

            // 5. tsconfig.json - 머지 (프로젝트 옵션 + SDK 필수 옵션)
            MergeTsConfig(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath);

            // 6. unity-bridge.ts - 프로젝트 우선, 없으면 SDK
            string unityBridgeProject = Path.Combine(projectBuildConfigPath, "unity-bridge.ts");
            string unityBridgeSdk = Path.Combine(sdkBuildConfigPath, "unity-bridge.ts");
            string unityBridgeDst = Path.Combine(buildProjectPath, "unity-bridge.ts");
            if (File.Exists(unityBridgeProject))
            {
                File.Copy(unityBridgeProject, unityBridgeDst, true);
                Debug.Log("[AIT]   ✓ unity-bridge.ts (프로젝트에서 복사)");
            }
            else if (File.Exists(unityBridgeSdk))
            {
                File.Copy(unityBridgeSdk, unityBridgeDst, true);
                Debug.Log("[AIT]   ✓ unity-bridge.ts (SDK에서 복사)");
            }

            // 7. 사용자 추가 파일 복사
            CopyAdditionalUserFiles(projectBuildConfigPath, buildProjectPath);

            Debug.Log("[AIT] ✓ 빌드 설정 파일 처리 완료");
        }

        /// <summary>
        /// SDK BuildConfig 템플릿 경로를 반환합니다. (캐싱 사용)
        /// </summary>
        private static string GetSdkBuildConfigPath()
        {
            // 캐시된 경로가 유효한지 확인
            if (_cachedSdkBuildConfigPath != null && Directory.Exists(_cachedSdkBuildConfigPath))
            {
                return _cachedSdkBuildConfigPath;
            }

            // 경로 검색
            string[] possiblePaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/BuildConfig~"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/BuildConfig~"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate/BuildConfig~")
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    _cachedSdkBuildConfigPath = path;
                    Debug.Log($"[AIT] SDK BuildConfig 경로 캐싱: {path}");
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// SDK 템플릿의 Runtime 폴더 경로를 반환합니다. (캐싱 사용)
        /// webgl/ 폴더에 Runtime이 없을 경우 폴백으로 사용됩니다.
        /// </summary>
        private static string FindSdkRuntimePath()
        {
            // 캐시된 경로가 유효한지 확인
            if (_cachedSdkRuntimePath != null && Directory.Exists(_cachedSdkRuntimePath))
            {
                return _cachedSdkRuntimePath;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] possiblePaths = new string[]
            {
                Path.Combine(projectRoot, "Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/Runtime"),
                Path.Combine(projectRoot, "Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/Runtime"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate/Runtime")
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    _cachedSdkRuntimePath = path;
                    Debug.Log($"[AIT] SDK Runtime 경로 캐싱: {path}");
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Unity WebGL 빌드를 public 폴더로 복사합니다.
        /// </summary>
        /// <returns>성공 시 SUCCEED, 실패 시 해당 에러 코드</returns>
        internal static AITConvertCore.AITExportError CopyWebGLToPublic(string webglPath, string buildProjectPath, AITBuildProfile profile = null)
        {
            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            var config = UnityUtil.GetEditorConf();

            // Unity WebGL 빌드를 Vite 프로젝트에 복사
            // - index.html: 프로젝트 루트 (Vite 요구사항)
            // - Build, TemplateData, Runtime: public 폴더 (정적 자산)
            string publicPath = Path.Combine(buildProjectPath, "public");

            // public 폴더 생성
            if (!Directory.Exists(publicPath))
            {
                Directory.CreateDirectory(publicPath);
            }

            // Build 폴더 → public/Build (필수 파일만 선별 복사)
            string buildSrc = Path.Combine(webglPath, "Build");
            string buildDest = Path.Combine(publicPath, "Build");

            if (!Directory.Exists(buildSrc))
            {
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] ✗ 치명적: Build 폴더를 찾을 수 없습니다!");
                Debug.LogError("[AIT] ========================================");
                Debug.LogError($"[AIT] 검색 경로: {buildSrc}");
                return AITConvertCore.AITExportError.BUILD_FOLDER_MISSING;
            }

            // Build 폴더에서 실제 파일 이름 찾기
            // 빌드 마커에서 압축 포맷 정보를 읽어 정확한 확장자로 탐지
            Debug.Log("[AIT] WebGL 빌드 파일 검색 중...");

            var buildInfo = AITConvertCore.ReadBuildMarker(webglPath);
            int compressionFormat = buildInfo?.compressionFormat ?? -1;
            bool decompressionFallback = buildInfo?.decompressionFallback ?? false;
            var patterns = AITBuildValidator.GetFilePatterns(compressionFormat, decompressionFallback);

            // 폴백 경로 존재 여부: 정확한 패턴(압축 포맷 또는 .unityweb)이 있을 때만 와일드카드 폴백 가능
            bool hasFallbackPath = compressionFormat >= 0 || decompressionFallback;

            if (buildInfo != null)
            {
                string[] formatNames = { "Disabled", "Gzip", "Brotli" };
                string formatName = compressionFormat >= 0 && compressionFormat < formatNames.Length ? formatNames[compressionFormat] : "Unknown";
                Debug.Log($"[AIT] 빌드 마커 감지: 압축 포맷 = {formatName} ({compressionFormat}), Decompression Fallback = {decompressionFallback}");
            }

            // 정확한 패턴으로 시도
            // 폴백 경로가 있으면 isRequired: false (와일드카드에서 에러 보고)
            // 폴백 경로가 없으면 isRequired: true (여기서 바로 에러 보고)
            string loaderFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["loader"], isRequired: true);
            string dataFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["data"], isRequired: !hasFallbackPath);
            string frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["framework"], isRequired: !hasFallbackPath);
            string wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["wasm"], isRequired: !hasFallbackPath);

            // 선택적 파일
            string symbolsFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["symbols"]);

            // 정확한 패턴으로 못 찾으면 와일드카드로 폴백 (loader는 압축 무관하므로 제외)
            if (hasFallbackPath)
            {
                var fallback = AITBuildValidator.GetFilePatterns(-1);
                if (string.IsNullOrEmpty(dataFile)) dataFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["data"], isRequired: true);
                if (string.IsNullOrEmpty(frameworkFile)) frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["framework"], isRequired: true);
                if (string.IsNullOrEmpty(wasmFile)) wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["wasm"], isRequired: true);
                if (string.IsNullOrEmpty(symbolsFile)) symbolsFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["symbols"]);
            }

            // 필수 파일 검증
            var missingFiles = new List<string>();
            if (string.IsNullOrEmpty(loaderFile)) missingFiles.Add("*.loader.js");
            if (string.IsNullOrEmpty(dataFile)) missingFiles.Add("*.data");
            if (string.IsNullOrEmpty(frameworkFile)) missingFiles.Add("*.framework.js");
            if (string.IsNullOrEmpty(wasmFile)) missingFiles.Add("*.wasm");

            if (missingFiles.Count > 0)
            {
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락!");
                Debug.LogError("[AIT] ========================================");
                Debug.LogError($"[AIT] 누락된 필수 파일: {string.Join(", ", missingFiles)}");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 가능한 원인:");
                Debug.LogError("[AIT]   1. Unity WebGL 빌드가 완료되지 않았습니다.");
                Debug.LogError("[AIT]   2. WebGL 빌드가 실패했지만 부분 결과물만 남아있습니다.");
                Debug.LogError("[AIT]   3. 빌드 설정(압축 방식 등)이 예상과 다릅니다.");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 해결 방법:");
                Debug.LogError("[AIT]   1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.");
                Debug.LogError("[AIT]   2. Unity Console에서 빌드 에러를 확인하세요.");
                Debug.LogError("[AIT] ========================================");
                return AITConvertCore.AITExportError.REQUIRED_FILE_MISSING;
            }

            // Build 대상 폴더 정리 후 재생성
            if (Directory.Exists(buildDest))
            {
                AITFileUtils.DeleteDirectory(buildDest);
            }
            Directory.CreateDirectory(buildDest);

            // 필수 파일만 선별 복사
            var filesToCopy = new List<string> { loaderFile, dataFile, frameworkFile, wasmFile };
            if (!string.IsNullOrEmpty(symbolsFile))
            {
                filesToCopy.Add(symbolsFile);
            }

            long totalBytes = 0;
            foreach (var fileName in filesToCopy)
            {
                string src = Path.Combine(buildSrc, fileName);
                string dest = Path.Combine(buildDest, fileName);
                File.Copy(src, dest, true);
                UnityUtil.EnsureFileReadable(dest);
                totalBytes += new FileInfo(src).Length;
            }

            Debug.Log($"[AIT] ✓ Build 파일 {filesToCopy.Count}개 선별 복사 완료 ({totalBytes / 1024.0 / 1024.0:0.#}MB)");

            // 안전장치: Build/ 폴더에 인식되지 않은 파일이 있으면 경고
            var allBuildFiles = Directory.GetFiles(buildSrc);
            var copiedFileNames = new HashSet<string>(filesToCopy);
            foreach (var file in allBuildFiles)
            {
                string name = Path.GetFileName(file);
                if (!copiedFileNames.Contains(name))
                {
                    Debug.LogWarning($"[AIT] ⚠️ Build 폴더에 복사되지 않은 파일: {name}");
                }
            }

            // TemplateData 폴더 → public/TemplateData
            string templateDataSrc = Path.Combine(webglPath, "TemplateData");
            string templateDataDest = Path.Combine(publicPath, "TemplateData");
            if (Directory.Exists(templateDataSrc))
            {
                UnityUtil.CopyDirectory(templateDataSrc, templateDataDest);
            }

            // Runtime 폴더 → public/Runtime
            // 1순위: webgl/ 폴더에 Runtime이 있으면 사용 (AITTemplate 빌드)
            // 2순위: webgl/ 폴더에 Runtime이 없으면 SDK 템플릿에서 복사
            string runtimeSrc = Path.Combine(webglPath, "Runtime");
            string runtimeDest = Path.Combine(publicPath, "Runtime");
            if (Directory.Exists(runtimeSrc))
            {
                UnityUtil.CopyDirectory(runtimeSrc, runtimeDest);
            }
            else
            {
                // SDK 템플릿에서 Runtime 폴더 복사 (수동 WebGL 빌드 시 AITTemplate 미사용 대응)
                Debug.LogWarning("[AIT] WebGL 빌드에 Runtime 폴더가 없습니다. SDK 템플릿에서 복사합니다.");
                Debug.LogWarning("[AIT]    ⚠️ AITTemplate이 아닌 다른 템플릿으로 빌드되었을 수 있습니다.");
                string sdkRuntimePath = FindSdkRuntimePath();
                if (!string.IsNullOrEmpty(sdkRuntimePath) && Directory.Exists(sdkRuntimePath))
                {
                    UnityUtil.CopyDirectory(sdkRuntimePath, runtimeDest);
                    Debug.Log("[AIT] ✓ Runtime 폴더: SDK 템플릿에서 복사 완료");
                }
                else
                {
                    Debug.LogError("[AIT] Runtime 폴더를 찾을 수 없습니다. 'Build And Package'를 사용하세요.");
                }
            }

            // StreamingAssets 폴더 → public/StreamingAssets (있는 경우)
            string streamingAssetsSrc = Path.Combine(webglPath, "StreamingAssets");
            string streamingAssetsDest = Path.Combine(publicPath, "StreamingAssets");
            if (Directory.Exists(streamingAssetsSrc))
            {
                UnityUtil.CopyDirectory(streamingAssetsSrc, streamingAssetsDest);
            }

            // index.html → 프로젝트 루트 (Vite가 루트에서 index.html을 찾음)
            string indexSrc = Path.Combine(webglPath, "index.html");
            string indexDest = Path.Combine(buildProjectPath, "index.html");

            // index.html 필수 검증
            if (!File.Exists(indexSrc))
            {
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] ✗ 치명적: index.html을 찾을 수 없습니다!");
                Debug.LogError("[AIT] ========================================");
                Debug.LogError($"[AIT] 검색 경로: {indexSrc}");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 가능한 원인:");
                Debug.LogError("[AIT]   1. Unity WebGL 빌드가 완료되지 않았습니다.");
                Debug.LogError("[AIT]   2. WebGL 템플릿이 올바르게 설정되지 않았습니다.");
                Debug.LogError("[AIT]   3. 이전 빌드가 손상되었습니다.");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 해결 방법:");
                Debug.LogError("[AIT]   1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.");
                Debug.LogError("[AIT]   2. AIT > Clean 메뉴로 빌드 폴더를 삭제 후 재빌드하세요.");
                Debug.LogError("[AIT] ========================================");
                return AITConvertCore.AITExportError.INDEX_HTML_MISSING;
            }

            string indexContent = File.ReadAllText(indexSrc);

            // 프로필 기반 설정값 (Mock 브릿지가 비활성화되면 프로덕션 모드로 간주)
            string isProduction = profile.enableMockBridge ? "false" : "true";
            string enableDebugConsole = profile.enableDebugConsole ? "true" : "false";

            // 프로젝트의 index.html에서 사용자 커스텀 섹션 추출 (있는 경우)
            string projectIndexPath = Path.Combine(Application.dataPath, "WebGLTemplates", "AITTemplate", "index.html");
            if (File.Exists(projectIndexPath))
            {
                string projectIndexContent = File.ReadAllText(projectIndexPath);

                // USER_HEAD 섹션 추출 및 교체
                string userHeadSection = AITTemplateManager.ExtractHtmlUserSection(projectIndexContent, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END);
                if (userHeadSection != null)
                {
                    indexContent = AITTemplateManager.ReplaceHtmlUserSection(indexContent, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END, userHeadSection);
                    Debug.Log("[AIT] index.html USER_HEAD 섹션 머지됨");
                }

                // USER_BODY_END 섹션 추출 및 교체
                string userBodyEndSection = AITTemplateManager.ExtractHtmlUserSection(projectIndexContent, AITTemplateManager.HTML_USER_BODY_END_START, AITTemplateManager.HTML_USER_BODY_END_END);
                if (userBodyEndSection != null)
                {
                    indexContent = AITTemplateManager.ReplaceHtmlUserSection(indexContent, AITTemplateManager.HTML_USER_BODY_END_START, AITTemplateManager.HTML_USER_BODY_END_END, userBodyEndSection);
                    Debug.Log("[AIT] index.html USER_BODY_END 섹션 머지됨");
                }
            }

            // Unity 플레이스홀더 치환
            indexContent = indexContent
                .Replace("%UNITY_WEB_NAME%", PlayerSettings.productName)
                .Replace("%UNITY_WIDTH%", PlayerSettings.defaultWebScreenWidth.ToString())
                .Replace("%UNITY_HEIGHT%", PlayerSettings.defaultWebScreenHeight.ToString())
                .Replace("%UNITY_COMPANY_NAME%", PlayerSettings.companyName)
                .Replace("%UNITY_PRODUCT_NAME%", PlayerSettings.productName)
                .Replace("%UNITY_PRODUCT_VERSION%", PlayerSettings.bundleVersion)
                // Unity 표준 URL 형식 (Unity가 치환하지 않은 경우 SDK가 처리)
                .Replace("%UNITY_WEBGL_LOADER_URL%", $"Build/{loaderFile}")
                .Replace("%UNITY_WEBGL_DATA_URL%", $"Build/{dataFile}")
                .Replace("%UNITY_WEBGL_FRAMEWORK_URL%", $"Build/{frameworkFile}")
                .Replace("%UNITY_WEBGL_CODE_URL%", $"Build/{wasmFile}")
                .Replace("%UNITY_WEBGL_SYMBOLS_URL%", !string.IsNullOrEmpty(symbolsFile) ? $"Build/{symbolsFile}" : "")
                // 하위 호환성을 위한 FILENAME 형식 (레거시)
                .Replace("%UNITY_WEBGL_LOADER_FILENAME%", loaderFile)
                .Replace("%UNITY_WEBGL_DATA_FILENAME%", dataFile)
                .Replace("%UNITY_WEBGL_FRAMEWORK_FILENAME%", frameworkFile)
                .Replace("%UNITY_WEBGL_CODE_FILENAME%", wasmFile)
                .Replace("%UNITY_WEBGL_SYMBOLS_FILENAME%", symbolsFile)
                // AIT 커스텀 플레이스홀더
                .Replace("%AIT_IS_PRODUCTION%", isProduction)
                .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", enableDebugConsole)
                .Replace("%AIT_DEVICE_PIXEL_RATIO%", config.devicePixelRatio.ToString())
                .Replace("%AIT_ICON_URL%", config.iconUrl ?? "")
                .Replace("%AIT_DISPLAY_NAME%", config.displayName ?? "")
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor ?? "#3182f6")
                // HTML5 Preload 태그 (로딩 성능 개선)
                .Replace("%AIT_PRELOAD_TAGS%", GeneratePreloadTags(dataFile, wasmFile, frameworkFile));

            // 로딩 화면 삽입 (%AIT_LOADING_SCREEN% 플레이스홀더)
            string loadingContent = "";
            string projectLoadingPath = AITPackageInitializer.GetProjectLoadingPath();

            // 프로젝트의 loading.html 사용 (SDK 초기화 시 자동 생성됨)
            if (File.Exists(projectLoadingPath))
            {
                loadingContent = File.ReadAllText(projectLoadingPath);
                Debug.Log("[AIT] ✓ 로딩 화면 적용: " + projectLoadingPath);
            }
            else
            {
                // 폴백: SDK 기본 템플릿 직접 사용 (초기화가 실행되지 않은 경우)
                string sdkTemplatePath = AITPackageInitializer.GetSDKLoadingTemplatePath();
                if (sdkTemplatePath != null)
                {
                    loadingContent = File.ReadAllText(sdkTemplatePath);
                    Debug.Log("[AIT] ✓ SDK 기본 로딩 화면 적용");
                }
                else
                {
                    Debug.LogWarning("[AIT] 로딩 화면 파일을 찾을 수 없습니다. 빈 로딩 화면이 사용됩니다.");
                }
            }

            // %AIT_LOADING_SCREEN% 플레이스홀더 치환
            indexContent = indexContent.Replace("%AIT_LOADING_SCREEN%", loadingContent);

            File.WriteAllText(indexDest, indexContent, System.Text.Encoding.UTF8);
            Debug.Log("[AIT] index.html → 프로젝트 루트에 생성");

            // 플레이스홀더 치환 결과 검증
            if (!AITBuildValidator.ValidatePlaceholderSubstitution(indexContent, indexDest))
            {
                return AITConvertCore.AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED;
            }

            // Runtime/appsintoss-unity-bridge.js 파일도 치환
            string bridgeSrc = Path.Combine(publicPath, "Runtime", "appsintoss-unity-bridge.js");
            if (File.Exists(bridgeSrc))
            {
                string bridgeContent = File.ReadAllText(bridgeSrc);
                bridgeContent = bridgeContent.Replace("%AIT_IS_PRODUCTION%", isProduction);
                File.WriteAllText(bridgeSrc, bridgeContent, System.Text.Encoding.UTF8);
                Debug.Log($"[AIT] appsintoss-unity-bridge.js Mock 브릿지 모드: {(profile.enableMockBridge ? "활성화" : "비활성화")}");
            }

            Debug.Log("[AIT] Unity WebGL 빌드 복사 완료");
            Debug.Log("[AIT]   - index.html → 프로젝트 루트");
            Debug.Log("[AIT]   - Build, TemplateData, Runtime → public/");

            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// Unity WebGL 리소스에 대한 preload 태그를 생성합니다.
        /// HTML5 preload를 사용하면 HTML 파싱과 동시에 리소스 다운로드가 시작되어 로딩 성능이 개선됩니다.
        /// </summary>
        /// <param name="dataFile">data 파일명 (예: Build.data.br)</param>
        /// <param name="wasmFile">wasm 파일명 (예: Build.wasm.br)</param>
        /// <param name="frameworkFile">framework 파일명 (예: Build.framework.js.br)</param>
        /// <returns>preload 태그 문자열 (줄바꿈 포함)</returns>
        private static string GeneratePreloadTags(string dataFile, string wasmFile, string frameworkFile)
        {
            var sb = new System.Text.StringBuilder();

            // 우선순위: data > wasm > framework (일반적으로 크기 순)
            // as="fetch": same-origin 리소스이므로 crossorigin 불필요 (Unity loader와 동일한 credentials mode 유지)
            if (!string.IsNullOrEmpty(dataFile))
            {
                sb.AppendLine($"    <link rel=\"preload\" href=\"Build/{dataFile}\" as=\"fetch\">");
            }

            if (!string.IsNullOrEmpty(wasmFile))
            {
                sb.AppendLine($"    <link rel=\"preload\" href=\"Build/{wasmFile}\" as=\"fetch\">");
            }

            // framework.js는 preload하지 않음
            // Unity 로더가 framework.js를 <script> 태그로 로드하는 경우 as="fetch" preload와 캐시 키가 불일치하여
            // 이중 다운로드가 발생할 수 있음. 이는 메모리 압박을 증가시켜 간헐적 초기화 실패(ASM_CONSTS 오류)의
            // 확률을 높일 수 있으므로 framework.js preload를 제거함.

            return sb.ToString().TrimEnd();
        }

        #region Async Packaging

        /// <summary>
        /// WebGL 빌드를 ait-build로 비동기 패키징 (non-blocking)
        /// Unity Editor를 차단하지 않고 pnpm install/build를 실행합니다.
        /// </summary>
        internal static void PackageWebGLBuildAsync(
            string projectPath,
            string webglPath,
            AITBuildProfile profile,
            Action<AITConvertCore.AITExportError> onComplete,
            Action<AITConvertCore.BuildPhase, float, string> onProgress = null,
            CancellationToken cancellationToken = default,
            bool skipGraniteBuild = false)
        {
            onProgress?.Invoke(AITConvertCore.BuildPhase.Preparing, 0.01f, "패키징 준비 중...");

            if (cancellationToken.IsCancellationRequested)
            {
                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                return;
            }

            Debug.Log("[AIT] Vite 기반 비동기 패키징 시작...");
            onProgress?.Invoke(AITConvertCore.BuildPhase.CopyingFiles, 0.05f, "빌드 설정 파일 복사 중...");

            var (ctx, prepError) = PreparePackaging(projectPath, webglPath, profile);
            if (ctx == null)
            {
                onComplete?.Invoke(prepError);
                return;
            }

            onProgress?.Invoke(AITConvertCore.BuildPhase.CopyingFiles, 0.15f, "파일 복사 완료");

            if (cancellationToken.IsCancellationRequested)
            {
                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                return;
            }

            // Step 3: pnpm install (비동기)
            onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.2f, "pnpm install 실행 중...");
            Debug.Log("[AIT] Step 3/3: pnpm install & build 실행 중...");

            RunPnpmInstallAsync(ctx.BuildProjectPath, ctx.PnpmPath, ctx.LocalCachePath, cancellationToken,
                onOutput: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.35f, line);
                },
                onComplete: (installResult) =>
                {
                    if (installResult != AITConvertCore.AITExportError.SUCCEED)
                    {
                        onComplete?.Invoke(installResult);
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                        return;
                    }

                    // Step 4: granite build (비동기)
                    if (skipGraniteBuild)
                    {
                        Debug.Log("[AIT] granite build 스킵 (Dev Server 모드)");
                        onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                        return;
                    }
                    RunGraniteBuildAsync(ctx, cancellationToken, onProgress, onComplete);
                }
            );
        }

        /// <summary>
        /// granite build 비동기 실행 + 실패 시 1회 재시도
        /// </summary>
        private static void RunGraniteBuildAsync(
            PackageContext ctx,
            CancellationToken ct,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.5f, "granite build 실행 중...");
            Debug.Log("[AIT] granite build 실행 중...");
            Debug.Log($"[AIT] UNITY_METADATA: {ctx.UnityMetadataEnv["UNITY_METADATA"]}");

            AITNpmRunner.RunNpmCommandWithCacheAsync(
                ctx.BuildProjectPath, ctx.PnpmPath, "run build", ctx.LocalCachePath,
                onComplete: (buildResult) =>
                {
                    if (buildResult == AITConvertCore.AITExportError.SUCCEED)
                    {
                        string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
                        AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);
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
                },
                onOutputReceived: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.75f, line);
                },
                additionalEnvVars: ctx.UnityMetadataEnv
            );
        }

        /// <summary>
        /// granite build 실패 후 clean install → build 재시도 (비동기)
        /// </summary>
        private static void RetryGraniteBuildAsync(
            PackageContext ctx,
            CancellationToken ct,
            Action<AITConvertCore.BuildPhase, float, string> onProgress,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            Debug.LogWarning("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
            CleanNodeModules(ctx.BuildProjectPath);
            onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.5f, "빌드 실패 후 재설치 중...");

            AITNpmRunner.RunNpmCommandWithCacheAsync(
                ctx.BuildProjectPath, ctx.PnpmPath, "install --no-frozen-lockfile", ctx.LocalCachePath,
                onComplete: (retryInstallResult) =>
                {
                    if (retryInstallResult != AITConvertCore.AITExportError.SUCCEED)
                    {
                        Debug.LogError("[AIT] granite build 실패 후 pnpm install 재시도도 실패");
                        onComplete?.Invoke(retryInstallResult);
                        return;
                    }

                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.7f, "granite build 재시도 중...");

                    AITNpmRunner.RunNpmCommandWithCacheAsync(
                        ctx.BuildProjectPath, ctx.PnpmPath, "run build", ctx.LocalCachePath,
                        onComplete: (retryBuildResult) =>
                        {
                            if (retryBuildResult == AITConvertCore.AITExportError.SUCCEED)
                            {
                                string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
                                AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);
                                onProgress?.Invoke(AITConvertCore.BuildPhase.Complete, 1f, "패키징 완료!");
                                Debug.Log($"[AIT] ✓ 비동기 패키징 완료 (재시도 성공): {distPath}");
                            }
                            else
                            {
                                Debug.LogError("[AIT] granite build 재시도도 실패");
                            }
                            onComplete?.Invoke(retryBuildResult);
                        },
                        onOutputReceived: (line) =>
                        {
                            onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.85f, line);
                        },
                        additionalEnvVars: ctx.UnityMetadataEnv
                    );
                },
                onOutputReceived: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.55f, line);
                }
            );
        }

        #endregion

        /// <summary>
        /// node_modules 무결성 검증
        /// package.json의 @apps-in-toss/web-framework 버전과 node_modules 내 설치 버전이 일치하는지 확인합니다.
        /// pnpm의 node_modules/.pnpm/@apps-in-toss+web-framework@{version} 디렉토리 존재 여부로 판단합니다.
        /// </summary>
        /// <param name="buildProjectPath">빌드 프로젝트 경로</param>
        /// <returns>true: 무결성 확인됨 또는 node_modules 없음, false: 버전 불일치로 정리 필요</returns>
        private static bool ValidateNodeModulesIntegrity(string buildProjectPath)
        {
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                // node_modules가 없으면 검증 불필요 (install 시 새로 생성됨)
                return true;
            }

            // package.json에서 web-framework 버전 읽기
            string packageJsonPath = Path.Combine(buildProjectPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                return true;
            }

            try
            {
                string packageJsonContent = File.ReadAllText(packageJsonPath);
                var packageJson = MiniJson.Deserialize(packageJsonContent) as Dictionary<string, object>;
                if (packageJson == null) return true;

                var dependencies = packageJson.ContainsKey("dependencies")
                    ? packageJson["dependencies"] as Dictionary<string, object>
                    : null;
                if (dependencies == null) return true;

                if (!dependencies.ContainsKey("@apps-in-toss/web-framework")) return true;

                string expectedVersion = dependencies["@apps-in-toss/web-framework"] as string;
                if (string.IsNullOrEmpty(expectedVersion)) return true;

                // ^ 또는 ~ 접두사 제거 (정확한 버전만 비교)
                expectedVersion = expectedVersion.TrimStart('^', '~');

                // pnpm의 node_modules/.pnpm/@apps-in-toss+web-framework@{version} 확인
                string pnpmDir = Path.Combine(nodeModulesPath, ".pnpm");
                if (!Directory.Exists(pnpmDir))
                {
                    // .pnpm 디렉토리가 없으면 오염된 상태
                    Debug.LogWarning("[AIT] node_modules/.pnpm 디렉토리가 없습니다. node_modules를 정리합니다.");
                    return false;
                }

                // @apps-in-toss+web-framework@{version}으로 시작하는 디렉토리 검색
                string expectedDirPrefix = $"@apps-in-toss+web-framework@{expectedVersion}";
                string[] matchingDirs = Directory.GetDirectories(pnpmDir, $"{expectedDirPrefix}*");

                if (matchingDirs.Length > 0)
                {
                    Debug.Log($"[AIT] ✓ node_modules 무결성 확인: web-framework@{expectedVersion}");
                    return true;
                }

                // 현재 설치된 버전 찾기 (로그용)
                string[] installedDirs = Directory.GetDirectories(pnpmDir, "@apps-in-toss+web-framework@*");
                if (installedDirs.Length > 0)
                {
                    string installedDirName = Path.GetFileName(installedDirs[0]);
                    Debug.LogWarning($"[AIT] web-framework 버전 불일치 감지!");
                    Debug.LogWarning($"[AIT]   기대 버전: {expectedVersion}");
                    Debug.LogWarning($"[AIT]   설치된 버전: {installedDirName}");
                    Debug.LogWarning($"[AIT]   node_modules를 정리하고 재설치합니다.");
                }
                else
                {
                    Debug.LogWarning($"[AIT] web-framework가 node_modules에 없습니다. node_modules를 정리합니다.");
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] node_modules 무결성 검증 중 오류 (무시됨): {e.Message}");
                return true; // 검증 실패 시 기존 동작 유지
            }
        }

        /// <summary>
        /// node_modules 및 캐시 정리
        /// </summary>
        /// <param name="buildProjectPath">빌드 프로젝트 경로</param>
        private static void CleanNodeModules(string buildProjectPath)
        {
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
            string npmCachePath = Path.Combine(buildProjectPath, ".npm-cache");

            if (Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[AIT] node_modules 삭제 중...");
                try
                {
                    AITFileUtils.DeleteDirectory(nodeModulesPath);
                    Debug.Log("[AIT] ✓ node_modules 삭제 완료");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT] node_modules 삭제 실패: {e.Message}");
                }
            }

            if (Directory.Exists(npmCachePath))
            {
                Debug.Log("[AIT] .npm-cache 삭제 중...");
                try
                {
                    AITFileUtils.DeleteDirectory(npmCachePath);
                    Debug.Log("[AIT] ✓ .npm-cache 삭제 완료");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT] .npm-cache 삭제 실패: {e.Message}");
                }
            }
        }

        /// <summary>
        /// ait-build 폴더 준비 (기존 결과물 정리)
        /// </summary>
        private static void PrepareAitBuildFolder(string buildProjectPath)
        {
            if (!Directory.Exists(buildProjectPath))
            {
                Directory.CreateDirectory(buildProjectPath);
                Debug.Log("[AIT] ait-build 폴더 생성");
            }
            else
            {
                Debug.Log("[AIT] 기존 빌드 결과물 정리 중... (node_modules와 설정 파일은 유지)");

                string[] itemsToKeep = new string[]
                {
                    "node_modules",
                    ".npm-cache",
                    "package.json",
                    "package-lock.json",
                    "pnpm-lock.yaml",
                    "granite.config.ts",
                    "vite.config.ts",
                    "tsconfig.json"
                };

                foreach (string item in Directory.GetFileSystemEntries(buildProjectPath))
                {
                    string itemName = Path.GetFileName(item);

                    bool shouldKeep = false;
                    foreach (string keepItem in itemsToKeep)
                    {
                        if (itemName == keepItem)
                        {
                            shouldKeep = true;
                            break;
                        }
                    }

                    if (shouldKeep) continue;

                    try
                    {
                        if (Directory.Exists(item))
                        {
                            AITFileUtils.DeleteDirectory(item);
                        }
                        else if (File.Exists(item))
                        {
                            File.Delete(item);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AIT] 삭제 실패: {itemName} - {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// pnpm install 비동기 실행 (PnpmInstallStages 배열 기반 재귀 재시도)
        /// </summary>
        private static void RunPnpmInstallAsync(
            string buildProjectPath,
            string pnpmPath,
            string localCachePath,
            CancellationToken ct,
            Action<string> onOutput,
            Action<AITConvertCore.AITExportError> onComplete,
            int stageIndex = 0)
        {
            if (stageIndex >= PnpmInstallStages.Length)
            {
                Debug.LogError("[AIT] pnpm install 실패 (모든 재시도 후에도 실패)");
                onComplete?.Invoke(AITConvertCore.AITExportError.FAIL_NPM_BUILD);
                return;
            }

            var (args, label, cleanFirst) = PnpmInstallStages[stageIndex];
            if (cleanFirst) CleanNodeModules(buildProjectPath);

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

                    Debug.LogWarning($"[AIT] pnpm install ({label}) 실패, 다음 단계로...");
                    RunPnpmInstallAsync(buildProjectPath, pnpmPath, localCachePath, ct, onOutput, onComplete, stageIndex + 1);
                },
                onOutputReceived: onOutput
            );
        }
    }
}
