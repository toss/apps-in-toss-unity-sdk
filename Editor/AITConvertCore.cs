using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEngine;
using AppsInToss.Editor;

namespace AppsInToss
{
    // JsonUtility를 위한 직렬화 가능한 설정 클래스들
    [System.Serializable]
    public class AppConfigWindow
    {
        public string navigationBarTitleText;
        public string backgroundColor;
    }

    [System.Serializable]
    public class AppConfigData
    {
        public string appId;
        public string appName;
        public string version;
        public string description;
        public string[] pages;
        public AppConfigWindow window;
        public string[] permissions;
        public string[] plugins;
    }

    /// <summary>
    /// Apps in Toss 미니앱 변환 핵심 클래스 (파사드)
    /// 빌드 파이프라인의 진입점으로서 내부 헬퍼 클래스들을 조율합니다.
    /// </summary>
    public class AITConvertCore
    {
        #region Build Cancellation

        private static bool isCancelled = false;
        private static Editor.AITAsyncCommandRunner.CommandTask currentAsyncTask = null;

        static AITConvertCore() { }

        /// <summary>
        /// 빌드 취소 요청
        /// </summary>
        public static void CancelBuild()
        {
            isCancelled = true;
            Debug.Log("[AIT] 빌드 취소 요청됨");

            // 현재 실행 중인 비동기 작업이 있으면 취소
            if (currentAsyncTask != null)
            {
                Editor.AITAsyncCommandRunner.CancelTask(currentAsyncTask);
                currentAsyncTask = null;
            }
        }

        /// <summary>
        /// 빌드 취소 플래그 리셋
        /// </summary>
        public static void ResetCancellation()
        {
            isCancelled = false;
            currentAsyncTask = null;
        }

        /// <summary>
        /// 빌드가 취소되었는지 확인
        /// </summary>
        public static bool IsCancelled()
        {
            return isCancelled;
        }

        /// <summary>
        /// 현재 비동기 작업 설정 (취소용)
        /// </summary>
        internal static void SetCurrentAsyncTask(Editor.AITAsyncCommandRunner.CommandTask task)
        {
            currentAsyncTask = task;
        }

        #endregion

        #region Public API - Initialization

        /// <summary>
        /// Unity WebGL 빌드 설정 초기화
        /// </summary>
        public static void Init(AITBuildProfile profile = null)
        {
            AITBuildInitializer.Init(profile);
        }

        /// <summary>
        /// WebGL 템플릿을 SDK에서 프로젝트로 복사합니다.
        /// 빌드 시마다 최신 SDK 템플릿으로 교체합니다.
        /// </summary>
        public static void EnsureWebGLTemplatesExist()
        {
            AITTemplateManager.EnsureWebGLTemplatesExist();
        }

        #endregion

        #region Build Phase

        /// <summary>
        /// 빌드 단계
        /// </summary>
        public enum BuildPhase
        {
            None,
            Preparing,
            WebGLBuild,
            CopyingFiles,
            PnpmInstall,
            GraniteBuild,
            Complete,
            Failed,
            Cancelled
        }

        #endregion

        #region Error Handling

        public enum AITExportError
        {
            SUCCEED = 0,
            NODE_NOT_FOUND = 1,
            BUILD_WEBGL_FAILED = 2,
            INVALID_APP_CONFIG = 3,
            NETWORK_ERROR = 4,
            CANCELLED = 5,
            FAIL_NPM_BUILD = 6,
            WEBGL_BUILD_INCOMPLETE = 7,
        }

        /// <summary>
        /// 에러 코드를 사용자 친화적 메시지로 변환
        /// </summary>
        public static string GetErrorMessage(AITExportError error)
        {
            switch (error)
            {
                case AITExportError.SUCCEED:
                    return "성공";

                case AITExportError.NODE_NOT_FOUND:
                    return "Node.js를 찾을 수 없습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. https://nodejs.org 에서 Node.js 설치\n" +
                           "2. Unity Editor 재시작\n" +
                           "3. 터미널에서 'node --version' 확인";

                case AITExportError.BUILD_WEBGL_FAILED:
                    return "WebGL 빌드에 실패했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Unity Console 창에서 에러 메시지 확인\n" +
                           "2. WebGL Build Support가 설치되어 있는지 확인\n" +
                           "3. 프로젝트에 빌드 오류가 없는지 확인\n" +
                           "4. File > Build Settings > WebGL에서 직접 빌드 시도";

                case AITExportError.INVALID_APP_CONFIG:
                    return "앱 설정이 올바르지 않습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Apps in Toss > Build & Deploy Window 열기\n" +
                           "2. 설정 섹션에서 아이콘 URL 입력 (필수)\n" +
                           "3. 앱 ID, 버전 등 기본 정보 확인";

                case AITExportError.NETWORK_ERROR:
                    return "네트워크 오류가 발생했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 인터넷 연결 확인\n" +
                           "2. npm 레지스트리 접속 가능 여부 확인\n" +
                           "3. 방화벽 또는 프록시 설정 확인";

                case AITExportError.CANCELLED:
                    return "사용자에 의해 빌드가 취소되었습니다.";

                case AITExportError.FAIL_NPM_BUILD:
                    return "pnpm 빌드에 실패했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Unity Console 창에서 에러 메시지 확인\n" +
                           "2. ait-build 폴더에서 직접 pnpm install 시도\n" +
                           "3. package.json 파일이 올바른지 확인\n" +
                           "4. ~/.ait-unity-sdk/nodejs 폴더를 삭제 후 다시 빌드 시도";

                case AITExportError.WEBGL_BUILD_INCOMPLETE:
                    return "WebGL 빌드 결과물이 불완전합니다.\n\n" +
                           "필수 파일(loader.js, data, framework.js, wasm 등)이 누락되었거나\n" +
                           "index.html의 플레이스홀더가 치환되지 않았습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. AIT > Clean 메뉴로 빌드 폴더 삭제\n" +
                           "2. 'Clean Build' 옵션 활성화 후 재빌드\n" +
                           "3. AIT > Regenerate WebGL Templates 실행";

                default:
                    return $"알 수 없는 오류가 발생했습니다. (코드: {error})";
            }
        }

        #endregion

        #region Public Static Fields

        public static AITEditorScriptObject config => UnityUtil.GetEditorConf();

        public static string defaultTemplateDir => "appsintoss-default";
        public static string webglDir = "webgl";
        public static string miniGameDir = "miniapp";
        public static string audioDir = "Assets";
        public static string frameworkDir = "framework";
        public static string dataFileSize = string.Empty;
        public static string codeMd5 = string.Empty;
        public static string dataMd5 = string.Empty;
        public static string defaultImgSrc = "Assets/AppsInToss-SDK/Runtime/appsintoss-default/images/background.jpg";

        public static bool UseIL2CPP
        {
            get
            {
#pragma warning disable CS0618
                return PlayerSettings.GetScriptingBackend(BuildTargetGroup.WebGL) == ScriptingImplementation.IL2CPP;
#pragma warning restore CS0618
            }
        }

        #endregion

        #region Public API - npm/pnpm

        /// <summary>
        /// FindNpm - 외부 접근용 public 메서드
        /// AppsInTossMenu.cs 등에서 사용
        /// </summary>
        public static string FindNpm()
        {
            return AITNpmRunner.FindNpmPath();
        }

        #endregion

        #region Main Export Pipeline

        /// <summary>
        /// Apps in Toss 미니앱으로 변환 실행
        /// </summary>
        /// <param name="buildWebGL">WebGL 빌드 실행 여부</param>
        /// <param name="doPackaging">패키징 실행 여부</param>
        /// <param name="cleanBuild">클린 빌드 여부 (false면 incremental build)</param>
        /// <param name="profile">적용할 빌드 프로필 (null이면 productionProfile 사용)</param>
        /// <param name="profileName">빌드 프로필 이름 (로그 출력용)</param>
        /// <returns>변환 결과</returns>
        public static AITExportError DoExport(bool buildWebGL = true, bool doPackaging = true, bool cleanBuild = false, AITBuildProfile profile = null, string profileName = null)
        {
            // 빌드 시작 전 취소 플래그 리셋
            ResetCancellation();

            // 빌드 전 PlayerSettings 백업 (빌드 완료 후 복원)
            var settingsBackup = Editor.AITPlayerSettingsBackup.Capture();

            try
            {
                // 프로필이 지정되지 않으면 기본 프로필 사용
                var editorConfig = UnityUtil.GetEditorConf();
                if (profile == null)
                {
                    profile = editorConfig.productionProfile;
                    profileName = profileName ?? "Production";
                }

                // 환경 변수로 프로필 오버라이드 적용 (Init 전에 적용하여 PlayerSettings에 반영)
                profile = AITBuildInitializer.ApplyEnvironmentVariableOverrides(profile);

                // Init()에 프로필 전달하여 프로필별 압축/스트리핑 설정 적용
                Init(profile);

                // 빌드 프로필 로그 출력
                AITBuildInitializer.LogBuildProfile(profile, profileName);

                // 프로필 기반으로 PlayerSettings 설정
                AITBuildInitializer.ApplyBuildProfileSettings(profile);

                Debug.Log($"Apps in Toss 미니앱 변환을 시작합니다... (cleanBuild: {cleanBuild})");

                if (editorConfig == null)
                {
                    Debug.LogError("Apps in Toss 설정을 찾을 수 없습니다.");
                    return AITExportError.INVALID_APP_CONFIG;
                }

                // 에셋 스트리밍 분석 (pre-build check)
                if (!Application.isBatchMode && !Editor.AssetStreaming.AITPreBuildAssetCheck.RunPreBuildCheck())
                {
                    return AITExportError.CANCELLED;
                }

                if (buildWebGL)
                {
                    // 취소 확인
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        return AITExportError.CANCELLED;
                    }

                    var webglResult = BuildWebGL(cleanBuild, profile);
                    if (webglResult != AITExportError.SUCCEED)
                    {
                        return webglResult;
                    }
                }

                // Apps in Toss 미니앱 패키지 생성
                if (doPackaging)
                {
                    // 취소 확인
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        return AITExportError.CANCELLED;
                    }

                    var exportResult = GenerateMiniAppPackage(profile);
                    if (exportResult != AITExportError.SUCCEED)
                    {
                        return exportResult;
                    }
                }

                Debug.Log("Apps in Toss 미니앱 변환이 완료되었습니다!");
                return AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                Debug.LogError($"변환 중 오류가 발생했습니다: {e.Message}");
                return AITExportError.BUILD_WEBGL_FAILED;
            }
            finally
            {
                // 빌드 완료 후 PlayerSettings 복원 (성공/실패 무관)
                settingsBackup.Restore();
            }
        }

        /// <summary>
        /// Apps in Toss 미니앱으로 비동기 변환 실행 (non-blocking)
        /// WebGL 빌드는 Unity API 제한으로 blocking이지만, npm/granite 명령은 non-blocking으로 실행됩니다.
        /// </summary>
        /// <param name="buildWebGL">WebGL 빌드 실행 여부</param>
        /// <param name="doPackaging">패키징 실행 여부</param>
        /// <param name="cleanBuild">클린 빌드 여부</param>
        /// <param name="profile">빌드 프로필</param>
        /// <param name="profileName">프로필 이름 (로그용)</param>
        /// <param name="onComplete">완료 콜백</param>
        /// <param name="onProgress">진행 상황 콜백 (phase, progress, status)</param>
        public static void DoExportAsync(
            bool buildWebGL,
            bool doPackaging,
            bool cleanBuild,
            AITBuildProfile profile,
            string profileName,
            Action<AITExportError> onComplete,
            Action<BuildPhase, float, string> onProgress = null)
        {
            // 배치 모드에서는 동기 실행
            if (Application.isBatchMode)
            {
                var result = DoExport(buildWebGL, doPackaging, cleanBuild, profile, profileName);
                onComplete?.Invoke(result);
                return;
            }

            // 취소 플래그 리셋
            ResetCancellation();

            // PlayerSettings 백업
            var settingsBackup = Editor.AITPlayerSettingsBackup.Capture();

            try
            {
                // 프로필 설정
                var editorConfig = UnityUtil.GetEditorConf();
                if (profile == null)
                {
                    profile = editorConfig.productionProfile;
                    profileName = profileName ?? "Production";
                }

                // 환경 변수 오버라이드 적용
                profile = AITBuildInitializer.ApplyEnvironmentVariableOverrides(profile);

                // 초기화
                Init(profile);
                AITBuildInitializer.LogBuildProfile(profile, profileName);
                AITBuildInitializer.ApplyBuildProfileSettings(profile);

                onProgress?.Invoke(BuildPhase.Preparing, 0.01f, "빌드 준비 중...");
                Debug.Log($"[AIT] 비동기 미니앱 변환 시작... (cleanBuild: {cleanBuild})");

                if (editorConfig == null)
                {
                    Debug.LogError("Apps in Toss 설정을 찾을 수 없습니다.");
                    settingsBackup.Restore();
                    onComplete?.Invoke(AITExportError.INVALID_APP_CONFIG);
                    return;
                }

                // 에셋 스트리밍 분석 (pre-build check)
                if (!Application.isBatchMode && !Editor.AssetStreaming.AITPreBuildAssetCheck.RunPreBuildCheck())
                {
                    settingsBackup.Restore();
                    onComplete?.Invoke(AITExportError.CANCELLED);
                    return;
                }

                // Phase 1: WebGL Build (BLOCKING - Unity 제한)
                if (buildWebGL)
                {
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        settingsBackup.Restore();
                        onComplete?.Invoke(AITExportError.CANCELLED);
                        return;
                    }

                    onProgress?.Invoke(BuildPhase.WebGLBuild, 0.05f, "WebGL 빌드 중... (Unity 제한으로 에디터가 일시 정지됩니다)");

                    var webglResult = BuildWebGL(cleanBuild, profile);
                    if (webglResult != AITExportError.SUCCEED)
                    {
                        settingsBackup.Restore();
                        onComplete?.Invoke(webglResult);
                        return;
                    }

                    onProgress?.Invoke(BuildPhase.CopyingFiles, 0.15f, "WebGL 빌드 완료, 패키징 준비 중...");
                }

                // Phase 2: Async 패키징
                if (doPackaging)
                {
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        settingsBackup.Restore();
                        onComplete?.Invoke(AITExportError.CANCELLED);
                        return;
                    }

                    GenerateMiniAppPackageAsync(profile, settingsBackup, onComplete, onProgress);
                }
                else
                {
                    Debug.Log("[AIT] 비동기 미니앱 변환이 완료되었습니다!");
                    settingsBackup.Restore();
                    onComplete?.Invoke(AITExportError.SUCCEED);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT] 변환 중 오류가 발생했습니다: {e.Message}");
                settingsBackup.Restore();
                onComplete?.Invoke(AITExportError.BUILD_WEBGL_FAILED);
            }
        }

        /// <summary>
        /// 미니앱 패키지 비동기 생성
        /// </summary>
        private static void GenerateMiniAppPackageAsync(
            AITBuildProfile profile,
            Editor.AITPlayerSettingsBackup settingsBackup,
            Action<AITExportError> onComplete,
            Action<BuildPhase, float, string> onProgress)
        {
            Debug.Log("[AIT] 비동기 미니앱 패키지 생성 시작...");

            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            string projectPath = UnityUtil.GetProjectPath();
            string webglPath = Path.Combine(projectPath, webglDir);

            if (!Directory.Exists(webglPath))
            {
                Debug.LogError("[AIT] WebGL 빌드 결과를 찾을 수 없습니다. WebGL 빌드를 먼저 실행하세요.");
                settingsBackup.Restore();
                onComplete?.Invoke(AITExportError.BUILD_WEBGL_FAILED);
                return;
            }

            // 비동기 패키징 실행
            AITPackageBuilder.PackageWebGLBuildAsync(
                projectPath,
                webglPath,
                profile,
                onComplete: (result) =>
                {
                    settingsBackup.Restore();

                    if (result == AITExportError.SUCCEED)
                    {
                        Debug.Log("[AIT] 비동기 미니앱이 생성되었습니다!");
                    }

                    onComplete?.Invoke(result);
                },
                onProgress: onProgress
            );
        }

        #endregion

        #region WebGL Build

        private static AITExportError BuildWebGL(bool cleanBuild = false, AITBuildProfile profile = null)
        {
            Debug.Log($"WebGL 빌드를 시작합니다... ({(cleanBuild ? "클린 빌드" : "증분 빌드")})");

            string[] scenes = UnityUtil.GetBuildScenes();
            string outputPath = Path.Combine(UnityUtil.GetProjectPath(), webglDir);

            // 클린 빌드 시에만 기존 빌드 폴더 삭제
            if (cleanBuild && Directory.Exists(outputPath))
            {
                Debug.Log("[AIT] 클린 빌드: 기존 WebGL 빌드 폴더 삭제 중...");
                Directory.Delete(outputPath, true);
            }

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            // 빌드 옵션 설정
            BuildOptions buildOptions = BuildOptions.None;

            // Development Build 옵션 (빌드 속도 향상, 디버깅 편의)
            if (profile.developmentBuild)
            {
                buildOptions |= BuildOptions.Development;
                Debug.Log("[AIT] Development Build 활성화");
            }

            // LZ4 압축으로 빌드 속도 향상
            if (profile.enableLZ4Compression)
            {
                buildOptions |= BuildOptions.CompressWithLz4;
            }

            // Unity 2021.3에서 Bee 빌드 캐시 문제로 인한 빌드 루프 방지
            // cleanBuild 시 CleanBuildCache 옵션 추가
            if (cleanBuild)
            {
                buildOptions |= BuildOptions.CleanBuildCache;
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = buildOptions,
#if UNITY_2023_3_OR_NEWER
                // Unity 2023.3+ (Unity 6): 최신 기능 활성화 (문서 권장)
                extraScriptingDefines = new[]
                {
                    "APPSINTOS_OPTIMIZED",
                    "WEBGL_2_0",
                    "UNITY_6_FEATURES"
                }
#elif UNITY_2022_3_OR_NEWER
                // Unity 2022.3+: WebGL 2.0 최적화 (문서 권장)
                extraScriptingDefines = new[]
                {
                    "APPSINTOS_OPTIMIZED",
                    "WEBGL_2_0"
                }
#endif
            };

            var result = BuildPipeline.BuildPlayer(buildPlayerOptions);

            // 빌드 리포트를 에러 리포터에 저장 (Issue 신고 시 사용)
            AITErrorReporter.SetBuildReport(result);

            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("WebGL 빌드가 실패했습니다.");
                return AITExportError.BUILD_WEBGL_FAILED;
            }

            Debug.Log("WebGL 빌드가 완료되었습니다.");
            return AITExportError.SUCCEED;
        }

        #endregion

        #region Package Generation

        private static AITExportError GenerateMiniAppPackage(AITBuildProfile profile = null)
        {
            Debug.Log("Apps in Toss 미니앱 패키지를 생성합니다...");

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            string projectPath = UnityUtil.GetProjectPath();
            string webglPath = Path.Combine(projectPath, webglDir);

            if (!Directory.Exists(webglPath))
            {
                Debug.LogError("WebGL 빌드 결과를 찾을 수 없습니다. WebGL 빌드를 먼저 실행하세요.");
                return AITExportError.BUILD_WEBGL_FAILED;
            }

            // AITPackageBuilder에 패키징 위임
            var packageResult = AITPackageBuilder.PackageWebGLBuild(projectPath, webglPath, profile);
            if (packageResult != AITExportError.SUCCEED)
            {
                return packageResult;
            }

            Debug.Log("Apps in Toss 미니앱이 생성되었습니다!");
            return AITExportError.SUCCEED;
        }

        #endregion
    }
}
