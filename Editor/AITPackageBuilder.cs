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
        /// <summary>
        /// 캐시된 SDK 경로를 초기화합니다. (SdkPathResolver 위임)
        /// </summary>
        internal static void ClearPathCache() => Package.SdkPathResolver.ClearPathCache();

        /// <summary>
        /// 머신 공유 pnpm store 경로 (PnpmStoreManager에 위임).
        /// </summary>
        internal static string GetSharedPnpmStorePath() => Package.PnpmStoreManager.GetSharedPnpmStorePath();

        #region Packaging Common

        /// <summary>
        /// 동기/비동기 패키징 공통 준비 컨텍스트.
        /// internal: AppsInToss.Editor.Package.GraniteBuildRunner에서 참조하기 위한 승격.
        /// </summary>
        internal class PackageContext
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
            Package.WebGLBuildCopier.PrepareAitBuildFolder(buildProjectPath);

            // npm 경로 확인
            string npmPath = AITNpmRunner.FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                AITLog.Error("[AIT] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.", sentryCapture: false);
                return (null, AITConvertCore.AITExportError.NODE_NOT_FOUND);
            }

            // Step 1-2: 파일 복사
            Debug.Log("[AIT] Step 1/3: Vite 프로젝트 구조 생성 중...");
            CopyBuildConfigFromTemplate(buildProjectPath);

            Debug.Log("[AIT] Step 2/3: Unity WebGL 빌드 복사 중...");
            var copyResult = Package.WebGLBuildCopier.CopyWebGLToPublic(webglPath, buildProjectPath, profile);
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
            string storePath = GetSharedPnpmStorePath();
            if (!Package.NodeModulesValidator.ValidateIntegrity(buildProjectPath))
            {
                Debug.Log("[AIT] node_modules 무결성 검증 실패. 정리 후 재설치합니다.");
                Package.NodeModulesValidator.CleanNodeModules(buildProjectPath);
            }

            return (new PackageContext
            {
                BuildProjectPath = buildProjectPath,
                PnpmPath = pnpmPath,
                LocalCachePath = storePath,
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
            Package.WebGLBuildCopier.PrepareAitBuildFolder(buildProjectPath);

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
            string storePath = GetSharedPnpmStorePath();
            if (!Package.NodeModulesValidator.ValidateIntegrity(buildProjectPath))
            {
                Debug.Log("[AIT] [병렬] node_modules 무결성 검증 실패. 정리 후 재설치합니다.");
                Package.NodeModulesValidator.CleanNodeModules(buildProjectPath);
            }

            return (new EarlyPackageContext
            {
                BuildProjectPath = buildProjectPath,
                PnpmPath = pnpmPath,
                LocalCachePath = storePath,
                UnityMetadataEnv = AITUnityMetadata.BuildEnvironmentVariables(),
                PnpmInstallResult = null,
            }, AITConvertCore.AITExportError.SUCCEED);
        }

        /// <summary>
        /// pnpm install 백그라운드 실행 (PnpmRunner에 위임). AITConvertCore 외부 호출 호환용.
        /// </summary>
        internal static void StartPnpmInstallInBackground(EarlyPackageContext earlyCtx)
            => Package.PnpmRunner.StartPnpmInstallInBackground(earlyCtx);

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

            var copyResult = Package.WebGLBuildCopier.CopyWebGLToPublic(webglPath, earlyCtx.BuildProjectPath, profile);
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
            Package.GraniteBuildRunner.RunGraniteBuildAsync(ctx, cancellationToken, onProgress,
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

            var installResult = Package.PnpmRunner.RunPnpmInstallSync(ctx);
            if (installResult != AITConvertCore.AITExportError.SUCCEED) return installResult;

            if (skipGraniteBuild)
            {
                Debug.Log("[AIT] granite build 스킵 (Dev Server 모드)");
                return AITConvertCore.AITExportError.SUCCEED;
            }

            var buildResult = Package.GraniteBuildRunner.RunGraniteBuildSync(ctx);
            if (buildResult != AITConvertCore.AITExportError.SUCCEED) return buildResult;

            string distPath = Path.Combine(ctx.BuildProjectPath, "dist");
            AITBuildValidator.PrintBuildReport(ctx.BuildProjectPath, distPath);

            var distValidation = AITBuildValidator.ValidateDistOutput(ctx.BuildProjectPath);
            if (distValidation != AITConvertCore.AITExportError.SUCCEED) return distValidation;

            Debug.Log($"[AIT] ✓ 패키징 완료: {distPath}");

            return AITConvertCore.AITExportError.SUCCEED;
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
                    Debug.Log("[AIT] package.json 파싱 실패, SDK 버전 사용");
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
                Debug.Log($"[AIT] package.json 머지 실패: {e}, SDK 버전 사용");
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
                    Debug.Log("[AIT] tsconfig.json 파싱 실패, SDK 버전 사용");
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
                Debug.Log($"[AIT] tsconfig.json 머지 실패: {e}, SDK 버전 사용");
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
        /// BuildConfig 템플릿에서 빌드 설정 파일들을 복사합니다.
        /// </summary>
        internal static void CopyBuildConfigFromTemplate(string buildProjectPath)
        {
            // 프로젝트 BuildConfig 경로 (사용자 커스터마이징 가능)
            string projectBuildConfigPath = Path.Combine(Application.dataPath, "WebGLTemplates/AITTemplate/BuildConfig~");

            // SDK의 BuildConfig 템플릿 경로 찾기 (캐싱 사용)
            string sdkBuildConfigPath = Package.SdkPathResolver.GetSdkBuildConfigPath();

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
            Package.WebGLBuildCopier.CopyAdditionalUserFiles(projectBuildConfigPath, buildProjectPath);

            Debug.Log("[AIT] ✓ 빌드 설정 파일 처리 완료");
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

            Package.PnpmRunner.RunPnpmInstallAsync(ctx.BuildProjectPath, ctx.PnpmPath, ctx.LocalCachePath, cancellationToken,
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
                    Package.GraniteBuildRunner.RunGraniteBuildAsync(ctx, cancellationToken, onProgress, onComplete);
                }
            );
        }

        #endregion
    }
}
