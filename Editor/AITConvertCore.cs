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
    [System.Serializable]
    internal class AITBuildInfo
    {
        public string sdkVersion;
        public string buildTime;
        public int compressionFormat;
        public bool decompressionFallback;
        public string profileName;
        public string unityVersion;
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
        private static Editor.AITPackageBuilder.EarlyPackageContext currentEarlyContext = null;

        static AITConvertCore() { }

        /// <summary>
        /// 빌드 취소 요청
        /// </summary>
        public static void CancelBuild()
        {
            isCancelled = true;
            Debug.Log("[AIT] 빌드 취소 요청됨");

            // 병렬 pnpm install 취소
            if (currentEarlyContext != null)
            {
                currentEarlyContext.CancelAndDisposePnpm();
                currentEarlyContext = null;
            }

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
            currentEarlyContext = null;
        }

        /// <summary>
        /// 빌드가 취소되었는지 확인
        /// </summary>
        public static bool IsCancelled()
        {
            return isCancelled;
        }

        /// <summary>
        /// 비동기 빌드 작업이 진행 중인지 확인
        /// </summary>
        public static bool HasRunningAsyncTask()
        {
            return currentAsyncTask != null;
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
        /// <returns>파일이 실제로 변경된 경우 true</returns>
        public static bool EnsureWebGLTemplatesExist()
        {
            return AITTemplateManager.EnsureWebGLTemplatesExist();
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
            // 7은 이전 WEBGL_BUILD_INCOMPLETE (아래 세분화 코드로 대체됨)

            // 세분화된 에러 코드
            BUILD_FOLDER_MISSING = 10,
            REQUIRED_FILE_MISSING = 11,
            INDEX_HTML_MISSING = 12,
            PLACEHOLDER_SUBSTITUTION_FAILED = 13,
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

                case AITExportError.BUILD_FOLDER_MISSING:
                    return "WebGL 빌드의 Build 폴더를 찾을 수 없습니다.\n\n" +
                           "WebGL 빌드가 실행되지 않았거나 빌드 결과물이 삭제되었습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 'Build & Package' 메뉴로 전체 빌드를 실행하세요.\n" +
                           "2. webgl/ 폴더가 존재하는지 확인하세요.";

                case AITExportError.REQUIRED_FILE_MISSING:
                    return "WebGL 빌드 필수 파일이 누락되었습니다.\n\n" +
                           "loader.js, data, framework.js, wasm 파일 중 일부가 없습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n" +
                           "2. Unity Console에서 빌드 에러를 확인하세요.";

                case AITExportError.INDEX_HTML_MISSING:
                    return "WebGL 빌드의 index.html을 찾을 수 없습니다.\n\n" +
                           "WebGL 템플릿이 올바르게 설정되지 않았을 수 있습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. AIT > Clean 메뉴로 빌드 폴더 삭제 후 재빌드\n" +
                           "2. 'Clean Build' 옵션 활성화 후 재빌드";

                case AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED:
                    return "index.html의 필수 플레이스홀더가 치환되지 않았습니다.\n\n" +
                           "이 상태로 배포하면 'createUnityInstance is not defined' 에러가 발생합니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n" +
                           "2. AIT > Clean 메뉴로 빌드 폴더 삭제 후 재빌드하세요.";

                default:
                    return $"알 수 없는 오류가 발생했습니다. (코드: {error})";
            }
        }

        #endregion

        #region Build Marker

        public const string BUILD_MARKER_FILENAME = ".ait-build-info.json";

        /// <summary>
        /// 빌드 마커를 읽어 AITBuildInfo를 반환합니다.
        /// 파일이 없거나 파싱 실패 시 null을 반환합니다.
        /// </summary>
        internal static AITBuildInfo ReadBuildMarker(string webglPath)
        {
            string markerPath = Path.Combine(webglPath, BUILD_MARKER_FILENAME);
            if (!File.Exists(markerPath)) return null;
            try
            {
                string json = File.ReadAllText(markerPath);
                return JsonUtility.FromJson<AITBuildInfo>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 빌드 마커 읽기 실패: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Public Static Fields

        public static AITEditorScriptObject config => UnityUtil.GetEditorConf();

        public static string defaultTemplateDir => "appsintoss-default";
        public static string webglDir = "webgl";

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

        #region Export Setup

        /// <summary>
        /// DoExport/DoExportAsync 공통 셋업: 프로필 폴백, 환경 변수 오버라이드, Init, 로그, PlayerSettings 적용
        /// </summary>
        /// <returns>editorConfig (null이면 설정 오류)</returns>
        private static AITEditorScriptObject PrepareExport(ref AITBuildProfile profile, ref string profileName)
        {
            var editorConfig = UnityUtil.GetEditorConf();
            if (profile == null)
            {
                profile = editorConfig.productionProfile;
                profileName = profileName ?? "Production";
            }

            profile = AITBuildInitializer.ApplyEnvironmentVariableOverrides(profile);
            Init(profile);
            AITBuildInitializer.LogBuildProfile(profile, profileName);
            AITBuildInitializer.ApplyBuildProfileSettings(profile);

            return editorConfig;
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
        public static AITExportError DoExport(bool buildWebGL = true, bool doPackaging = true, bool cleanBuild = false, AITBuildProfile profile = null, string profileName = null, bool skipGraniteBuild = false)
        {
            // 빌드 시작 전 취소 플래그 리셋
            ResetCancellation();

            // 빌드 전 PlayerSettings 백업 (빌드 완료 후 복원)
            var settingsBackup = Editor.AITPlayerSettingsBackup.Capture();

            try
            {
                var editorConfig = PrepareExport(ref profile, ref profileName);

                Debug.Log($"Apps in Toss 미니앱 변환을 시작합니다... (cleanBuild: {cleanBuild})");

                if (editorConfig == null)
                {
                    Debug.LogError("Apps in Toss 설정을 찾을 수 없습니다.");
                    return AITExportError.INVALID_APP_CONFIG;
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

                    var exportResult = GenerateMiniAppPackage(profile, skipGraniteBuild);
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
            Action<BuildPhase, float, string> onProgress = null,
            bool skipGraniteBuild = false)
        {
            // 배치 모드에서는 동기 실행
            if (Application.isBatchMode)
            {
                var result = DoExport(buildWebGL, doPackaging, cleanBuild, profile, profileName, skipGraniteBuild);
                onComplete?.Invoke(result);
                return;
            }

            // 취소 플래그 리셋
            ResetCancellation();

            // PlayerSettings 백업
            var settingsBackup = Editor.AITPlayerSettingsBackup.Capture();

            try
            {
                var editorConfig = PrepareExport(ref profile, ref profileName);

                onProgress?.Invoke(BuildPhase.Preparing, 0.01f, "빌드 준비 중...");
                Debug.Log($"[AIT] 비동기 미니앱 변환 시작... (cleanBuild: {cleanBuild})");

                if (editorConfig == null)
                {
                    Debug.LogError("Apps in Toss 설정을 찾을 수 없습니다.");
                    settingsBackup.Restore();
                    onComplete?.Invoke(AITExportError.INVALID_APP_CONFIG);
                    return;
                }

                // 병렬 경로: WebGL 빌드 + pnpm install 동시 실행
                if (buildWebGL && doPackaging)
                {
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        settingsBackup.Restore();
                        onComplete?.Invoke(AITExportError.CANCELLED);
                        return;
                    }

                    // Phase 0: BuildConfig 복사 + pnpm install 백그라운드 시작
                    onProgress?.Invoke(BuildPhase.Preparing, 0.02f, "빌드 설정 파일 복사 및 pnpm install 준비 중...");
                    string projectPath = UnityUtil.GetProjectPath();

                    var (earlyCtx, earlyError) = Editor.AITPackageBuilder.PrepareEarlyPackaging(projectPath, profile);
                    if (earlyCtx == null)
                    {
                        settingsBackup.Restore();
                        onComplete?.Invoke(earlyError);
                        return;
                    }

                    currentEarlyContext = earlyCtx;
                    Editor.AITPackageBuilder.StartPnpmInstallInBackground(earlyCtx);

                    // Phase 1: WebGL Build (BLOCKING - 메인 스레드)
                    // 백그라운드 pnpm install은 독립 OS 프로세스이므로 이 동안 병렬 실행됨
                    onProgress?.Invoke(BuildPhase.WebGLBuild, 0.05f, "WebGL 빌드 중... (pnpm install 병렬 실행 중)");

                    var webglResult = BuildWebGL(cleanBuild, profile);
                    if (webglResult != AITExportError.SUCCEED)
                    {
                        earlyCtx.CancelAndDisposePnpm();
                        currentEarlyContext = null;
                        settingsBackup.Restore();
                        onComplete?.Invoke(webglResult);
                        return;
                    }

                    if (IsCancelled())
                    {
                        earlyCtx.CancelAndDisposePnpm();
                        currentEarlyContext = null;
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        settingsBackup.Restore();
                        onComplete?.Invoke(AITExportError.CANCELLED);
                        return;
                    }

                    // Phase 2: WebGL 출력 복사 + pnpm install 완료 대기 + granite build
                    string webglPath = Path.Combine(projectPath, webglDir);
                    currentEarlyContext = null;
                    Editor.AITPackageBuilder.CompletePackagingAfterWebGLBuild(
                        earlyCtx, webglPath, profile, settingsBackup, onComplete, onProgress, skipGraniteBuild);
                }
                else if (buildWebGL)
                {
                    // WebGL 빌드만 (패키징 없음)
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

                    Debug.Log("[AIT] 비동기 미니앱 변환이 완료되었습니다!");
                    settingsBackup.Restore();
                    onComplete?.Invoke(AITExportError.SUCCEED);
                }
                else if (doPackaging)
                {
                    // 패키징만 (buildWebGL: false) — 기존 순차 경로 사용
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        settingsBackup.Restore();
                        onComplete?.Invoke(AITExportError.CANCELLED);
                        return;
                    }

                    GenerateMiniAppPackageAsync(profile, settingsBackup, onComplete, onProgress, skipGraniteBuild);
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
            Action<BuildPhase, float, string> onProgress,
            bool skipGraniteBuild = false)
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
                    currentAsyncTask = null;
                    settingsBackup.Restore();

                    if (result == AITExportError.SUCCEED)
                    {
                        Debug.Log("[AIT] 비동기 미니앱이 생성되었습니다!");
                    }

                    onComplete?.Invoke(result);
                },
                onProgress: onProgress,
                skipGraniteBuild: skipGraniteBuild
            );
        }

        #endregion

        #region WebGL Build

        /// <summary>
        /// 기존 빌드 캐시가 유효한지 검증하여 clean build 필요 여부를 판단합니다.
        /// 빌드 마커 없음, Unity 버전 불일치, 필수 파일 누락 시 true를 반환합니다.
        /// </summary>
        private static bool ShouldForceCleanBuild(string outputPath, bool cleanBuild)
        {
            if (cleanBuild) return true;
            if (!Directory.Exists(outputPath)) return false;

            var buildInfo = ReadBuildMarker(outputPath);
            if (buildInfo == null)
            {
                Debug.LogWarning("[AIT] 빌드 마커가 없습니다. Clean build를 수행합니다.");
                return true;
            }
            if (buildInfo.unityVersion != Application.unityVersion)
            {
                Debug.LogWarning($"[AIT] Unity 버전 불일치: 빌드 {buildInfo.unityVersion} vs 현재 {Application.unityVersion}. Clean build를 수행합니다.");
                return true;
            }

            string buildDir = Path.Combine(outputPath, "Build");
            if (!Directory.Exists(buildDir) || Directory.GetFiles(buildDir, "*.loader.js").Length == 0)
            {
                Debug.LogWarning("[AIT] 빌드 필수 파일이 없습니다. Clean build를 수행합니다.");
                return true;
            }

            return false;
        }

        private static AITExportError BuildWebGL(bool cleanBuild = false, AITBuildProfile profile = null)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool confirm = AITPlatformHelper.ShowConfirmDialog(
                    "빌드 타겟 전환 필요",
                    $"현재 빌드 타겟이 {EditorUserBuildSettings.activeBuildTarget}입니다.\n" +
                    "WebGL 빌드를 위해 빌드 타겟을 WebGL로 전환해야 합니다.",
                    "전환",
                    "취소");

                if (!confirm)
                {
                    Debug.Log("[AIT] 사용자가 빌드 타겟 전환을 취소했습니다.");
                    return AITExportError.CANCELLED;
                }

                Debug.Log($"[AIT] 빌드 타겟을 {EditorUserBuildSettings.activeBuildTarget}에서 WebGL로 전환합니다...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                {
                    Debug.LogError("[AIT] WebGL 빌드 타겟으로 전환할 수 없습니다. WebGL Build Support 모듈이 설치되어 있는지 확인하세요.");
                    return AITExportError.BUILD_WEBGL_FAILED;
                }
            }

            string[] scenes = UnityUtil.GetBuildScenes();
            string outputPath = Path.Combine(UnityUtil.GetProjectPath(), webglDir);

            // 빌드 캐시 유효성 검증
            if (ShouldForceCleanBuild(outputPath, cleanBuild))
            {
                if (!cleanBuild)
                    Debug.Log("[AIT] 빌드 캐시 검증 실패. 자동으로 clean build를 수행합니다.");
                cleanBuild = true;
            }

            Debug.Log($"WebGL 빌드를 시작합니다... ({(cleanBuild ? "클린 빌드" : "증분 빌드")})");

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

            // AIT 빌드 마커 파일 생성 (빌드 정보 기록용)
            try
            {
                var buildInfo = new AITBuildInfo
                {
                    sdkVersion = AITVersion.Version,
                    buildTime = DateTime.UtcNow.ToString("o"),
                    compressionFormat = (int)PlayerSettings.WebGL.compressionFormat,
                    decompressionFallback = PlayerSettings.WebGL.decompressionFallback,
                    profileName = profile?.developmentBuild == true ? "Development" : "Production",
                    unityVersion = Application.unityVersion
                };
                string markerPath = Path.Combine(outputPath, BUILD_MARKER_FILENAME);
                File.WriteAllText(markerPath, JsonUtility.ToJson(buildInfo, true));
                Debug.Log($"[AIT] 빌드 마커 생성: {markerPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 빌드 마커 생성 실패 (무시됨): {e.Message}");
            }

            return AITExportError.SUCCEED;
        }

        #endregion

        #region Package Generation

        private static AITExportError GenerateMiniAppPackage(AITBuildProfile profile = null, bool skipGraniteBuild = false)
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
            var packageResult = AITPackageBuilder.PackageWebGLBuild(projectPath, webglPath, profile, skipGraniteBuild);
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
