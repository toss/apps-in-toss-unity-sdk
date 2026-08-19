using System;
using System.Collections.Generic;
using System.Globalization;
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

        // 빌드 취소 상태(플래그/비동기 작업 핸들/조기 패키지 컨텍스트)는
        // AITBuildCancellation으로 분리(#36). 아래는 기존 공개 API 호환을 위한 위임 래퍼이며
        // 동작은 변경되지 않는다.

        static AITConvertCore() { }

        /// <summary>
        /// 빌드 취소 요청
        /// </summary>
        public static void CancelBuild() => AITBuildCancellation.CancelBuild();

        /// <summary>
        /// 빌드 취소 플래그 리셋
        /// </summary>
        public static void ResetCancellation() => AITBuildCancellation.ResetCancellation();

        /// <summary>
        /// 빌드가 취소되었는지 확인
        /// </summary>
        public static bool IsCancelled() => AITBuildCancellation.IsCancelled();

        /// <summary>
        /// 비동기 빌드 작업이 진행 중인지 확인
        /// </summary>
        public static bool HasRunningAsyncTask() => AITBuildCancellation.HasRunningAsyncTask();

        /// <summary>
        /// 현재 비동기 작업 설정 (취소용)
        /// </summary>
        internal static void SetCurrentAsyncTask(Editor.AITAsyncCommandRunner.CommandTask task)
            => AITBuildCancellation.SetCurrentAsyncTask(task);

        #endregion

        #region Public API - Initialization

        /// <summary>
        /// Unity WebGL 빌드 설정 초기화
        /// </summary>
        public static void Init(AITBuildProfile profile = null, bool fastBuild = false)
        {
            AITBuildInitializer.Init(profile, fastBuild);
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

        // 에러 메시지 변환 구현은 AITExportErrorCatalog로 분리(#36의 AITBuildCancellation과 동일 패턴).
        // 아래 3개 메서드는 기존 공개 API 호환을 위한 위임이며 동작은 변경되지 않는다.

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
            DIST_FOLDER_MISSING = 14,
            AIT_FILE_MISSING = 15,
        }

        /// <summary>
        /// 에러 코드를 사용자 친화적 메시지로 변환
        /// </summary>
        public static string GetErrorMessage(AITExportError error) => Editor.AITExportErrorCatalog.GetErrorMessage(error);

        /// <summary>
        /// 다이얼로그 제목에 붙일 짧은 사유 라벨을 반환합니다.
        /// 예: "빌드 실패 ({shortReason})" 형태로 조합됩니다.
        /// </summary>
        public static string GetErrorShortReason(AITExportError error) => Editor.AITExportErrorCatalog.GetErrorShortReason(error);

        /// <summary>
        /// 다이얼로그 본문에 사용할 원인 한 단락을 반환합니다. 해결 방법은 포함하지 않습니다.
        /// </summary>
        public static string GetErrorCause(AITExportError error) => Editor.AITExportErrorCatalog.GetErrorCause(error);

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
                Debug.LogWarning($"[AIT] 빌드 마커 읽기 실패: {e}");
                return null;
            }
        }

        /// <summary>
        /// 빌드 마커 파일을 지정된 디렉토리에 생성합니다.
        /// 부모 디렉토리가 없으면 자동 생성합니다 (Sentry SDK-DC 대응).
        /// </summary>
        internal static void WriteBuildMarker(string webglPath, AITBuildInfo buildInfo)
        {
            string markerPath = Path.Combine(webglPath, BUILD_MARKER_FILENAME);
            string markerDir = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrEmpty(markerDir) && !Directory.Exists(markerDir))
            {
                Directory.CreateDirectory(markerDir);
                Debug.Log($"[AIT] 빌드 마커 디렉토리 생성: {markerDir}");
            }
            File.WriteAllText(markerPath, JsonUtility.ToJson(buildInfo, true));
            Debug.Log($"[AIT] 빌드 마커 생성: {markerPath}");
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
        private static AITEditorScriptObject PrepareExport(ref AITBuildProfile profile, ref string profileName, bool fastBuild = false)
        {
            var editorConfig = UnityUtil.GetEditorConf();
            if (profile == null)
            {
                profile = editorConfig.productionProfile;
                profileName = profileName ?? "Production";
            }

            profile = AITBuildInitializer.ApplyEnvironmentVariableOverrides(profile);
            Init(profile, fastBuild);
            AITBuildInitializer.LogBuildProfile(profile, profileName);
            AITBuildInitializer.ApplyBuildProfileSettings(profile);

            return editorConfig;
        }

        /// <summary>
        /// 빌드 전 에셋 최적화 검사 (배치 모드 또는 빠른 빌드에서는 스킵)
        /// 빠른 빌드(Dev Server·Deploy (Test)) 경로는 반복 루프 속도가 우선이며, 검사는
        /// Deploy (Production)/Build & Package에서 계속 수행됨.
        /// </summary>
        /// <returns>true = 빌드 진행, false = 빌드 취소</returns>
        private static bool RunPreBuildOptimizationCheck(AITEditorScriptObject editorConfig, bool fastBuild = false)
        {
            if (Application.isBatchMode) return true;
            if (fastBuild)
            {
                Debug.Log("[AIT] 빠른 빌드: 에셋 최적화 검사를 건너뜁니다 (Deploy (Production)/Build & Package에서는 계속 수행됨).");
                return true;
            }
            if (editorConfig == null) return true;
            if (!editorConfig.enableBuildOptimizationCheck) return true;

            var issues = AITBuildOptimizationScanner.Scan();

            bool hasIssues = false;
            foreach (var issue in issues)
            {
                if (issue.status == OptimizationStatus.Issue)
                {
                    hasIssues = true;
                    break;
                }
            }
            if (!hasIssues) return true;

            return AITBuildOptimizationWindow.ShowAndWait(issues, editorConfig);
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
        /// <param name="fastBuild">
        /// 빠른 반복 빌드(Dev Server·Deploy (Test)) 경로 여부. true면 IL2CPP 컴파일러 구성을 Debug로,
        /// IL2CPP Code Generation을 OptimizeSize로 전환하고 에셋 최적화 검사를 건너뛴다
        /// (반복 루프 속도 우선). 기본값 false는 기존 호출부의 동작을 보존한다.
        /// </param>
        /// <returns>변환 결과</returns>
        public static AITExportError DoExport(bool buildWebGL = true, bool doPackaging = true, bool cleanBuild = false, AITBuildProfile profile = null, string profileName = null, bool skipGraniteBuild = false, bool fastBuild = false)
        {
            // play mode 중에는 BuildPipeline.BuildPlayer가 내부적으로 Addressables 빌드를
            // PreprocessBuild에서 트리거하며 "This cannot be used during play mode." 에러를 낸다.
            // SDK 버그가 아니라 사용자 조작 오류이므로 빌드 진입 자체를 막는다 (SDK-QJ/QH/QG/E1).
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                AITLog.Error("[AIT] play mode 중에는 빌드를 실행할 수 없습니다. Play 모드를 종료한 후 다시 시도하세요.", sentryCapture: false);
                return AITExportError.CANCELLED;
            }

            // 빌드 시작 전 취소 플래그 리셋
            ResetCancellation();

            // 빌드 세션 시작 + PlayerSettings 스냅샷 (리로드/강종 대비)
            AITBuildSession.BeginBuild(profileName ?? "Build");
            var snapshot = PlayerSettingsSnapshot.Capture();
            AITBuildSession.RecordPlayerSettingsSnapshot(snapshot);

            // 에러 트래커 초기화 (DSN이 설정된 경우에만)
            Editor.ErrorTracker.AITBuildTransaction transaction = null;
            if (Editor.ErrorTracker.AITEditorErrorTracker.IsDsnConfigured)
            {
                transaction = new Editor.ErrorTracker.AITBuildTransaction(profileName ?? "Build");
                transaction.SetTag("clean_build", cleanBuild.ToString());
                transaction.SetTag("build_webgl", buildWebGL.ToString());
                transaction.SetTag("do_packaging", doPackaging.ToString());
            }

            try
            {
                var editorConfig = PrepareExport(ref profile, ref profileName, fastBuild);

                Debug.Log($"Apps in Toss 미니앱 변환을 시작합니다... (cleanBuild: {cleanBuild})");

                if (editorConfig == null)
                {
                    AITLog.Error("Apps in Toss 설정을 찾을 수 없습니다.", sentryCapture: false);
                    transaction?.Finish("internal_error");
                    return AITExportError.INVALID_APP_CONFIG;
                }

                // 빌드 전 에셋 최적화 검사
                if (buildWebGL && !RunPreBuildOptimizationCheck(editorConfig, fastBuild))
                {
                    return AITExportError.CANCELLED;
                }

                if (buildWebGL)
                {
                    // 취소 확인
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        transaction?.Finish("cancelled");
                        return AITExportError.CANCELLED;
                    }

                    if (transaction != null)
                        Editor.ErrorTracker.AITEditorErrorTracker.AddBreadcrumb("build", "WebGL 빌드 시작");
                    var webglSpan = transaction?.StartSpan("webgl.build", "Unity WebGL Build");
                    AITBuildSession.SetStage(BuildStage.WebGLBuild);
                    var webglResult = Editor.AITWebGLBuilder.BuildWebGL(cleanBuild, profile);
                    webglSpan?.Finish(webglResult == AITExportError.SUCCEED ? "ok" : "internal_error");

                    if (webglResult != AITExportError.SUCCEED)
                    {
                        transaction?.Finish("internal_error");
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
                        transaction?.Finish("cancelled");
                        return AITExportError.CANCELLED;
                    }

                    if (transaction != null)
                        Editor.ErrorTracker.AITEditorErrorTracker.AddBreadcrumb("build", "패키징 시작");
                    var packageSpan = transaction?.StartSpan("packaging", "Generate MiniApp Package");
                    AITBuildSession.SetStage(BuildStage.Packaging);
                    var exportResult = GenerateMiniAppPackage(profile, skipGraniteBuild, fastBuild);
                    packageSpan?.Finish(exportResult == AITExportError.SUCCEED ? "ok" : "internal_error");

                    if (exportResult != AITExportError.SUCCEED)
                    {
                        transaction?.Finish("internal_error");
                        return exportResult;
                    }
                }

                AITBuildSession.SetStage(BuildStage.Completing);
                Debug.Log("Apps in Toss 미니앱 변환이 완료되었습니다!");
                transaction?.Finish("ok");
                return AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                Debug.LogError($"변환 중 오류가 발생했습니다: {e}");
                transaction?.Finish("internal_error");
                return AITExportError.BUILD_WEBGL_FAILED;
            }
            finally
            {
                // 빌드 완료 후 PlayerSettings 복원 (성공/실패 무관)
                try { snapshot.Restore(); }
                finally { AITBuildSession.EndBuild(); }
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
        /// <param name="fastBuild">
        /// 빠른 반복 빌드(Dev Server·Deploy (Test)) 경로 여부. DoExport와 동일한 의미 — StartServer와
        /// AITDeployManager.RunDeploy(DeployKind.Test)가 이 값을 전달한다. 기본값 false는 Production
        /// 계열 호출부의 동작을 보존한다.
        /// </param>
        /// <remarks>
        /// onProgress/onComplete는 내부적으로 <see cref="PhaseTimingTracker"/>로 감싸여 BuildPhase 전환
        /// 시점 기준 단계별 소요 시간을 계측한다 — Deploy (Test)/(Production)/Build &amp; Package/Dev Server 등
        /// 이 메서드를 쓰는 모든 인터랙티브 경로에 공통 적용된다. 완료 시 "AIT: 단계별 소요 — ..." 요약
        /// 1줄만 로그로 남기며(실패 시 실패 단계까지의 부분 요약 1줄), Sentry로는 전송하지 않는다.
        /// </remarks>
        public static void DoExportAsync(
            bool buildWebGL,
            bool doPackaging,
            bool cleanBuild,
            AITBuildProfile profile,
            string profileName,
            Action<AITExportError> onComplete,
            Action<BuildPhase, float, string> onProgress = null,
            bool skipGraniteBuild = false,
            bool fastBuild = false)
        {
            // play mode 중 빌드 진입 차단 (SDK-QJ/QH/QG/E1). DoExport와 동일한 가드 — 메뉴/UI 모든
            // 진입점이 DoExport/DoExportAsync로 수렴하므로 두 곳만 막으면 전체 경로가 커버된다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                AITLog.Error("[AIT] play mode 중에는 빌드를 실행할 수 없습니다. Play 모드를 종료한 후 다시 시도하세요.", sentryCapture: false);
                onComplete?.Invoke(AITExportError.CANCELLED);
                return;
            }

            // 배치 모드에서는 동기 실행
            if (Application.isBatchMode)
            {
                var result = DoExport(buildWebGL, doPackaging, cleanBuild, profile, profileName, skipGraniteBuild, fastBuild);
                onComplete?.Invoke(result);
                return;
            }

            // 취소 플래그 리셋
            ResetCancellation();

            // 빌드 세션 시작 + PlayerSettings 스냅샷 (리로드/강종 대비)
            AITBuildSession.BeginBuild(profileName ?? "Build");
            var snapshot = PlayerSettingsSnapshot.Capture();
            AITBuildSession.RecordPlayerSettingsSnapshot(snapshot);

            // 단계별 시간 계측 — onProgress의 BuildPhase 전환 시점을 가로채 누적하고,
            // onComplete 시점에 요약 1줄을 로그로 남긴다. 아래 두 래퍼를 거치지 않는 진입점
            // (위 play mode/배치 모드의 조기 return)은 실제 빌드를 시작하지 않으므로 계측 대상이 아니다.
            var phaseTiming = new PhaseTimingTracker();
            Action<BuildPhase, float, string> trackedOnProgress = (phase, progress, status) =>
            {
                phaseTiming.EnterPhase(phase);
                onProgress?.Invoke(phase, progress, status);
            };
            Action<AITExportError> trackedOnComplete = (result) =>
            {
                phaseTiming.LogSummary(result);
                onComplete?.Invoke(result);
            };

            try
            {
                var editorConfig = PrepareExport(ref profile, ref profileName, fastBuild);

                trackedOnProgress(BuildPhase.Preparing, 0.01f, "빌드 준비 중...");
                Debug.Log($"[AIT] 비동기 미니앱 변환 시작... (cleanBuild: {cleanBuild})");

                if (editorConfig == null)
                {
                    AITLog.Error("Apps in Toss 설정을 찾을 수 없습니다.", sentryCapture: false);
                    try { snapshot.Restore(); }
                    finally { AITBuildSession.EndBuild(); }
                    trackedOnComplete(AITExportError.INVALID_APP_CONFIG);
                    return;
                }

                // 빌드 전 에셋 최적화 검사
                if (buildWebGL && !RunPreBuildOptimizationCheck(editorConfig, fastBuild))
                {
                    try { snapshot.Restore(); }
                    finally { AITBuildSession.EndBuild(); }
                    trackedOnComplete(AITExportError.CANCELLED);
                    return;
                }

                // 병렬 경로: WebGL 빌드 + pnpm install 동시 실행
                if (buildWebGL && doPackaging)
                {
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(AITExportError.CANCELLED);
                        return;
                    }

                    // Phase 0: BuildConfig 복사 + pnpm install 백그라운드 시작
                    trackedOnProgress(BuildPhase.Preparing, 0.02f, "빌드 설정 파일 복사 및 pnpm install 준비 중...");
                    string projectPath = UnityUtil.GetProjectPath();

                    var (earlyCtx, earlyError) = Editor.AITPackageBuilder.PrepareEarlyPackaging(projectPath, profile);
                    if (earlyCtx == null)
                    {
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(earlyError);
                        return;
                    }

                    AITBuildCancellation.SetCurrentEarlyContext(earlyCtx);
                    Editor.AITPackageBuilder.StartPnpmInstallInBackground(earlyCtx);

                    // Phase 1: WebGL Build (BLOCKING - 메인 스레드)
                    // 백그라운드 pnpm install은 독립 OS 프로세스이므로 이 동안 병렬 실행됨
                    trackedOnProgress(BuildPhase.WebGLBuild, 0.05f, "WebGL 빌드 중... (pnpm install 병렬 실행 중)");

                    AITBuildSession.SetStage(BuildStage.WebGLBuild);
                    var webglResult = Editor.AITWebGLBuilder.BuildWebGL(cleanBuild, profile);
                    if (webglResult != AITExportError.SUCCEED)
                    {
                        earlyCtx.CancelAndDisposePnpm();
                        AITBuildCancellation.SetCurrentEarlyContext(null);
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(webglResult);
                        return;
                    }

                    if (IsCancelled())
                    {
                        earlyCtx.CancelAndDisposePnpm();
                        AITBuildCancellation.SetCurrentEarlyContext(null);
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(AITExportError.CANCELLED);
                        return;
                    }

                    // Phase 2: WebGL 출력 복사 + pnpm install 완료 대기 + granite build
                    string webglPath = Path.Combine(projectPath, webglDir);
                    AITBuildCancellation.SetCurrentEarlyContext(null);
                    AITBuildSession.SetStage(BuildStage.Packaging);
                    Editor.AITPackageBuilder.CompletePackagingAfterWebGLBuild(
                        earlyCtx, webglPath, profile, snapshot, trackedOnComplete, trackedOnProgress, skipGraniteBuild, fastBuild);
                }
                else if (buildWebGL)
                {
                    // WebGL 빌드만 (패키징 없음)
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(AITExportError.CANCELLED);
                        return;
                    }

                    trackedOnProgress(BuildPhase.WebGLBuild, 0.05f, "WebGL 빌드 중... (Unity 제한으로 에디터가 일시 정지됩니다)");

                    AITBuildSession.SetStage(BuildStage.WebGLBuild);
                    var webglResult = Editor.AITWebGLBuilder.BuildWebGL(cleanBuild, profile);
                    if (webglResult != AITExportError.SUCCEED)
                    {
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(webglResult);
                        return;
                    }

                    AITBuildSession.SetStage(BuildStage.Completing);
                    Debug.Log("[AIT] 비동기 미니앱 변환이 완료되었습니다!");
                    try { snapshot.Restore(); }
                    finally { AITBuildSession.EndBuild(); }
                    trackedOnComplete(AITExportError.SUCCEED);
                }
                else if (doPackaging)
                {
                    // 패키징만 (buildWebGL: false) — 기존 순차 경로 사용
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        try { snapshot.Restore(); }
                        finally { AITBuildSession.EndBuild(); }
                        trackedOnComplete(AITExportError.CANCELLED);
                        return;
                    }

                    AITBuildSession.SetStage(BuildStage.Packaging);
                    GenerateMiniAppPackageAsync(profile, snapshot, trackedOnComplete, trackedOnProgress, skipGraniteBuild, fastBuild);
                }
                else
                {
                    AITBuildSession.SetStage(BuildStage.Completing);
                    Debug.Log("[AIT] 비동기 미니앱 변환이 완료되었습니다!");
                    try { snapshot.Restore(); }
                    finally { AITBuildSession.EndBuild(); }
                    trackedOnComplete(AITExportError.SUCCEED);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT] 변환 중 오류가 발생했습니다: {e}");
                try { snapshot.Restore(); }
                finally { AITBuildSession.EndBuild(); }
                trackedOnComplete(AITExportError.BUILD_WEBGL_FAILED);
            }
        }

        /// <summary>
        /// 미니앱 패키지 비동기 생성
        /// </summary>
        private static void GenerateMiniAppPackageAsync(
            AITBuildProfile profile,
            PlayerSettingsSnapshot snapshot,
            Action<AITExportError> onComplete,
            Action<BuildPhase, float, string> onProgress,
            bool skipGraniteBuild = false,
            bool fastBuild = false)
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
                AITLog.Error("[AIT] WebGL 빌드 결과를 찾을 수 없습니다. WebGL 빌드를 먼저 실행하세요.", sentryCapture: false);
                try { snapshot.Restore(); }
                finally { AITBuildSession.EndBuild(); }
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
                    AITBuildCancellation.SetCurrentAsyncTask(null);
                    try { snapshot.Restore(); }
                    finally { AITBuildSession.EndBuild(); }

                    if (result == AITExportError.SUCCEED)
                    {
                        Debug.Log("[AIT] 비동기 미니앱이 생성되었습니다!");
                    }

                    onComplete?.Invoke(result);
                },
                onProgress: onProgress,
                skipGraniteBuild: skipGraniteBuild,
                fastBuild: fastBuild
            );
        }

        #endregion

        #region Phase Timing (Deploy 가속 계측)

        /// <summary>
        /// DoExportAsync 진행 중 BuildPhase 전환 시점을 기준으로 각 단계의 소요 시간을 누적한다.
        /// 동일 단계에 여러 번 재진입해도(GraniteBuildRunner의 pnpm 재설치 → granite 재시도 경로 등)
        /// 시간을 합산한다. Preparing/Complete/Failed/Cancelled/None은 순간적인 마커라 별도 버킷을
        /// 만들지 않는다(그 구간에 흐른 시간은 요약에서 빠지지만, 총 소요 시간은 overall 스톱워치가
        /// 별도로 잡으므로 정보 손실이 실질적으로 없다 — 준비 단계는 보통 1초 미만).
        /// </summary>
        private sealed class PhaseTimingTracker
        {
            private static readonly HashSet<BuildPhase> TrackedPhases = new HashSet<BuildPhase>
            {
                BuildPhase.WebGLBuild,
                BuildPhase.CopyingFiles,
                BuildPhase.PnpmInstall,
                BuildPhase.GraniteBuild,
            };

            private readonly System.Diagnostics.Stopwatch _overallStopwatch = System.Diagnostics.Stopwatch.StartNew();
            private readonly System.Diagnostics.Stopwatch _phaseStopwatch = new System.Diagnostics.Stopwatch();
            private readonly List<BuildPhase> _phaseOrder = new List<BuildPhase>();
            private readonly Dictionary<BuildPhase, double> _phaseSeconds = new Dictionary<BuildPhase, double>();
            private BuildPhase? _currentPhase;
            private bool _summaryLogged;

            /// <summary>
            /// onProgress가 새 phase를 보고할 때마다 호출한다. phase가 실제로 바뀐 경우에만
            /// 직전 phase의 경과 시간을 버킷에 반영하고 스톱워치를 재시작한다 — 같은 phase가
            /// 여러 번(진행률 갱신마다) 불려도 시간이 끊기지 않는다.
            /// </summary>
            public void EnterPhase(BuildPhase phase)
            {
                if (_currentPhase.HasValue && _currentPhase.Value != phase)
                {
                    FlushCurrentPhase();
                }
                if (!_currentPhase.HasValue || _currentPhase.Value != phase)
                {
                    _currentPhase = phase;
                    _phaseStopwatch.Restart();
                }
            }

            private void FlushCurrentPhase()
            {
                if (!_currentPhase.HasValue) return;
                double seconds = _phaseStopwatch.Elapsed.TotalSeconds;
                var phase = _currentPhase.Value;
                if (!TrackedPhases.Contains(phase)) return;

                if (!_phaseSeconds.ContainsKey(phase))
                {
                    _phaseSeconds[phase] = 0;
                    _phaseOrder.Add(phase);
                }
                _phaseSeconds[phase] += seconds;
            }

            /// <summary>
            /// 빌드 완료(성공/실패/취소) 시 1회 호출. 요약 로그 1줄을 남긴다. 중복 호출은 무시한다
            /// (일부 실패 경로가 콜백을 여러 겹으로 감쌀 가능성에 대비한 방어).
            /// </summary>
            public void LogSummary(AITExportError result)
            {
                if (_summaryLogged) return;
                _summaryLogged = true;

                FlushCurrentPhase();
                _overallStopwatch.Stop();

                // 단계 진입 전 즉시 종료(예: 설정 오류로 즉시 실패)된 경우 요약할 내용이 없다.
                if (_phaseOrder.Count == 0) return;

                var phaseTimings = new List<(string label, double seconds)>(_phaseOrder.Count);
                foreach (var phase in _phaseOrder)
                {
                    phaseTimings.Add((PhaseTimingLabel(phase), _phaseSeconds[phase]));
                }

                double totalSeconds = _overallStopwatch.Elapsed.TotalSeconds;
                string failedAtLabel = result == AITExportError.SUCCEED || !_currentPhase.HasValue
                    ? null
                    : PhaseTimingLabel(_currentPhase.Value);

                Debug.Log(FormatPhaseTimings(phaseTimings, totalSeconds, failedAtLabel));
            }
        }

        /// <summary>
        /// 단계별 시간 요약 로그에 쓰는 한국어 라벨. AITDeployManager.GetPhaseText(진행률 다이얼로그용)와는
        /// 별개 — GraniteBuild는 여기서 "vite/ait build"로 표기해 실제 실행되는 CLI를 드러낸다.
        /// </summary>
        private static string PhaseTimingLabel(BuildPhase phase)
        {
            switch (phase)
            {
                case BuildPhase.WebGLBuild: return "WebGL 빌드";
                case BuildPhase.CopyingFiles: return "파일 복사";
                case BuildPhase.PnpmInstall: return "pnpm install";
                case BuildPhase.GraniteBuild: return "vite/ait build";
                default: return phase.ToString();
            }
        }

        /// <summary>
        /// 단계별 소요 시간 요약을 "AIT: " 로그 컨벤션에 맞는 한 줄 문자열로 조립한다.
        /// Stopwatch/시각 의존이 없는 순수 함수 — 유닛 테스트로 포맷을 고정할 수 있다.
        /// 예: "AIT: 단계별 소요 — WebGL 빌드 87.3s · 파일 복사 2.1s · pnpm install 0.4s · vite/ait build 41.2s (총 131.0s)"
        /// failedAtLabel이 주어지면(성공하지 못한 경우) 실패 시점까지의 부분 요약으로 표기한다.
        /// </summary>
        internal static string FormatPhaseTimings(IReadOnlyList<(string label, double seconds)> phaseTimings, double totalSeconds, string failedAtLabel = null)
        {
            var segments = new List<string>(phaseTimings?.Count ?? 0);
            if (phaseTimings != null)
            {
                foreach (var (label, seconds) in phaseTimings)
                {
                    segments.Add($"{label} {seconds.ToString("F1", CultureInfo.InvariantCulture)}s");
                }
            }
            string body = string.Join(" · ", segments);
            string totalStr = totalSeconds.ToString("F1", CultureInfo.InvariantCulture);

            return string.IsNullOrEmpty(failedAtLabel)
                ? $"AIT: 단계별 소요 — {body} (총 {totalStr}s)"
                : $"AIT: 단계별 소요(실패: {failedAtLabel}) — {body} (실패까지 {totalStr}s)";
        }

        #endregion

        #region WebGL Build

        // WebGL 빌드 실행부(ShouldForceCleanBuild/BuildWebGL/버전 정보 JSON 기록·정리)는
        // AITWebGLBuilder로 분리(#36의 AITBuildCancellation과 동일 패턴).
        // 빌드 파이프라인은 Editor.AITWebGLBuilder.BuildWebGL을 호출하며 동작은 변경되지 않는다.

        #endregion

        #region Package Generation

        private static AITExportError GenerateMiniAppPackage(AITBuildProfile profile = null, bool skipGraniteBuild = false, bool fastBuild = false)
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
                AITLog.Error("WebGL 빌드 결과를 찾을 수 없습니다. WebGL 빌드를 먼저 실행하세요.", sentryCapture: false);
                return AITExportError.BUILD_WEBGL_FAILED;
            }

            // AITPackageBuilder에 패키징 위임
            var packageResult = AITPackageBuilder.PackageWebGLBuild(projectPath, webglPath, profile, skipGraniteBuild, fastBuild);
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
