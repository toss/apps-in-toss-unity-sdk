using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using AppsInToss.Editor;
using AppsInToss.Editor.IssueReport;
using AppsInToss.Editor.Menu;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss 메뉴 시스템
    /// </summary>
    [InitializeOnLoad]
    public class AppsInTossMenu
    {
        // 서버 상태 관리자 (실제 상태 기반 판단)
        private static AITServerStateManager devServerState;
        private static AITServerStateManager prodServerState;
        private static Stopwatch buildStopwatch = new Stopwatch();

        /// <summary>
        /// 도메인 리로드 시 기존 서버 프로세스 복원 및 종료 이벤트 등록
        /// </summary>
        static AppsInTossMenu()
        {
            // 상태 관리자 초기화
            devServerState = new AITServerStateManager(ServerType.Dev);
            prodServerState = new AITServerStateManager(ServerType.Prod);

            // 즉시 실제 상태 검증 (domain reload 후 복원)
            devServerState.ValidateState();
            prodServerState.ValidateState();
        }

        /// <summary>
        /// Unity Editor 종료 시 모든 서버 프로세스 정리
        /// MainThreadDispatcher의 EditorApplication.quitting 구독을 통해 호출됨.
        /// </summary>
        internal static void HandleEditorQuitting()
        {
            var devState = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            var prodState = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            bool hadRunningServers = devState != ServerState.NotRunning || prodState != ServerState.NotRunning;

            if (devState != ServerState.NotRunning)
            {
                Debug.Log("[AIT] Editor 종료 - Dev 서버 프로세스 정리 중...");
                StopServer(ServerType.Dev);
            }

            if (prodState != ServerState.NotRunning)
            {
                Debug.Log("[AIT] Editor 종료 - Prod 서버 프로세스 정리 중...");
                StopServer(ServerType.Prod);
            }

            if (hadRunningServers)
            {
                Debug.Log("[AIT] 모든 서버 프로세스가 정리되었습니다.");
            }
        }

        /// <summary>
        /// 패키지 등록 변경 시 SDK 패키지 제거 감지
        /// MainThreadDispatcher의 PackageManager.Events.registeredPackages 구독을 통해 호출됨.
        /// </summary>
        internal static void HandlePackagesChanged(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            foreach (var package in args.removed)
            {
                // 이 SDK 패키지가 제거되었는지 확인
                if (package.name == AITVersion.PackageName ||
                    package.name == AITVersion.LegacyPackageName)
                {
                    Debug.Log("[AIT] SDK 패키지가 제거됨 - 서버 프로세스 정리 중...");

                    var devState = devServerState?.GetCachedState() ?? ServerState.NotRunning;
                    var prodState = prodServerState?.GetCachedState() ?? ServerState.NotRunning;

                    if (devState != ServerState.NotRunning)
                    {
                        StopServer(ServerType.Dev);
                    }

                    if (prodState != ServerState.NotRunning)
                    {
                        StopServer(ServerType.Prod);
                    }

                    break;
                }
            }
        }

        // ==================== Dev Server ====================

        [MenuItem("AIT/Dev Server/Start Server", false, 1)]
        public static void MenuStartDevServer()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            StartServer(ServerType.Dev);
        }

        [MenuItem("AIT/Dev Server/Start Server", true)]
        public static bool ValidateMenuStartDevServer()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.NotRunning;
        }

        [MenuItem("AIT/Dev Server/Stop Server", false, 2)]
        public static void MenuStopDevServer()
        {
            Debug.Log("AIT: Dev 서버 중지...");
            StopServer(ServerType.Dev);
        }

        [MenuItem("AIT/Dev Server/Stop Server", true)]
        public static bool ValidateMenuStopDevServer()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        [MenuItem("AIT/Dev Server/Restart Server", false, 3)]
        public static void MenuRestartDevServer()
        {
            Debug.Log("AIT: Dev 서버 재시작...");
            RestartDevServer();
        }

        [MenuItem("AIT/Dev Server/Restart Server", true)]
        public static bool ValidateMenuRestartDevServer()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        [MenuItem("AIT/Dev Server/Restart Server (server-only)", false, 4)]
        public static void MenuRestartDevServerOnly()
        {
            Debug.Log("AIT: Dev 서버 재시작 중 (서버만)...");
            RestartDevServerOnly();
        }

        [MenuItem("AIT/Dev Server/Restart Server (server-only)", true)]
        public static bool ValidateMenuRestartDevServerOnly()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        // ==================== Production Server ====================

        [MenuItem("AIT/Production Server/Start Server", false, 11)]
        public static void MenuStartProdServer()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            StartServer(ServerType.Prod);
        }

        [MenuItem("AIT/Production Server/Start Server", true)]
        public static bool ValidateMenuStartProdServer()
        {
            var state = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.NotRunning;
        }

        [MenuItem("AIT/Production Server/Stop Server", false, 12)]
        public static void MenuStopProdServer()
        {
            Debug.Log("AIT: Production 서버 중지...");
            StopServer(ServerType.Prod);
        }

        [MenuItem("AIT/Production Server/Stop Server", true)]
        public static bool ValidateMenuStopProdServer()
        {
            var state = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        [MenuItem("AIT/Production Server/Restart Server", false, 13)]
        public static void MenuRestartProdServer()
        {
            Debug.Log("AIT: Production 서버 재시작...");
            RestartProdServer();
        }

        [MenuItem("AIT/Production Server/Restart Server", true)]
        public static bool ValidateMenuRestartProdServer()
        {
            var state = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        [MenuItem("AIT/Production Server/Restart Server (server-only)", false, 14)]
        public static void MenuRestartProdServerOnly()
        {
            Debug.Log("AIT: Production 서버 재시작 중 (서버만)...");
            RestartProdServerOnly();
        }

        [MenuItem("AIT/Production Server/Restart Server (server-only)", true)]
        public static bool ValidateMenuRestartProdServerOnly()
        {
            var state = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        // ==================== Helper Methods ====================

        private static void RestartServer(ServerType type, bool serverOnly)
        {
            StopServer(type);
            // 프로세스 종료 대기 후 서버 재시작 (메인 스레드 블로킹 방지)
            double startTime = EditorApplication.timeSinceStartup;
            void WaitAndRestart()
            {
                if (EditorApplication.timeSinceStartup - startTime < 0.5) return;
                EditorApplication.update -= WaitAndRestart;
                if (serverOnly)
                    StartServerOnly(type);
                else
                    StartServer(type);
            }
            EditorApplication.update += WaitAndRestart;
        }

        private static void RestartDevServer() => RestartServer(ServerType.Dev, serverOnly: false);
        private static void RestartProdServer() => RestartServer(ServerType.Prod, serverOnly: false);
        private static void RestartDevServerOnly() => RestartServer(ServerType.Dev, serverOnly: true);
        private static void RestartProdServerOnly() => RestartServer(ServerType.Prod, serverOnly: true);

        // ==================== Build & Package ====================
        [MenuItem("AIT/Build & Package", false, 23)]
        public static void BuildAndPackage()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            Debug.Log("AIT: Build & Package 시작...");
            AITDeployManager.RunBuildAndPackage();
        }

        // ==================== Publish ====================

        [MenuItem("AIT/Publish", false, 31)]
        public static void Publish()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            AITDeployManager.RunPublish();
        }

        // ==================== Clean ====================
        [MenuItem("AIT/Clean", false, 101)]
        public static void Clean()
        {
            string projectPath = UnityUtil.GetProjectPath();
            string webglPath = Path.Combine(projectPath, "webgl");
            string aitBuildPath = Path.Combine(projectPath, "ait-build");

            bool webglExists = Directory.Exists(webglPath);
            bool aitBuildExists = Directory.Exists(aitBuildPath);

            if (!webglExists && !aitBuildExists)
            {
                AITPlatformHelper.ShowInfoDialog("정보", "삭제할 빌드 폴더가 없습니다.", "확인");
                return;
            }

            // 삭제할 폴더 목록 구성
            var foldersToDelete = new List<string>();
            if (webglExists) foldersToDelete.Add("webgl/");
            if (aitBuildExists) foldersToDelete.Add("ait-build/");

            bool confirmed = AITPlatformHelper.ShowConfirmDialog(
                "빌드 Clean",
                $"다음 폴더를 삭제하시겠습니까?\n\n• {string.Join("\n• ", foldersToDelete)}\n\n이 작업은 되돌릴 수 없습니다.",
                "삭제",
                "취소",
                autoApprove: true
            );

            if (!confirmed) return;

            Debug.Log("AIT: 빌드 폴더 삭제 시작...");

            int deletedCount = 0;

            if (webglExists)
            {
                // ait-build/node_modules 내 pnpm 의존성처럼 read-only 속성이 깔린 파일이
                // 섞여 있을 수 있어 Directory.Delete 직호출은 UnauthorizedAccessException을
                // 발생시킨다 (Sentry: APPS-IN-TOSS-UNITY-SDK-CA). 헬퍼는 ReadOnly를 선제 해제하고
                // Windows 일시 잠금에 대해 지수 백오프로 재시도한다.
                if (AITFileUtils.DeleteDirectory(webglPath))
                {
                    Debug.Log($"AIT: ✓ webgl/ 폴더 삭제 완료");
                    deletedCount++;
                }
                else
                {
                    // 헬퍼가 이미 [AIT] 디렉토리 삭제 실패 경고를 출력했음. 추가 LogError는 사용자 가시성용이며
                    // Sentry로 캐스케이드하지 않도록 sentryCapture:false로 명시.
                    AITLog.Error($"AIT: webgl/ 폴더 삭제 실패: {webglPath}", sentryCapture: false);
                }
            }

            if (aitBuildExists)
            {
                if (AITFileUtils.DeleteDirectory(aitBuildPath))
                {
                    Debug.Log($"AIT: ✓ ait-build/ 폴더 삭제 완료");
                    deletedCount++;
                }
                else
                {
                    AITLog.Error($"AIT: ait-build/ 폴더 삭제 실패: {aitBuildPath}", sentryCapture: false);
                }
            }

            if (deletedCount > 0)
            {
                Debug.Log($"AIT: Clean 완료! ({deletedCount}개 폴더 삭제됨)");
                AITPlatformHelper.ShowInfoDialog("완료", $"빌드 폴더 {deletedCount}개가 삭제되었습니다.", "확인");
            }
        }

        // ==================== Open Build Output ====================
        [MenuItem("AIT/Open Build Output", false, 102)]
        public static void OpenBuildOutput()
        {
            string buildPath = PathValidator.GetBuildTemplatePath();
            if (Directory.Exists(buildPath))
            {
                // EditorUtility.RevealInFinder는 폴더를 "선택"하므로 부모 폴더가 열림
                // 폴더 자체를 열려면 플랫폼별 명령 사용
#if UNITY_EDITOR_OSX
                System.Diagnostics.Process.Start("open", buildPath);
#elif UNITY_EDITOR_WIN
                System.Diagnostics.Process.Start("explorer.exe", buildPath);
#else
                EditorUtility.RevealInFinder(buildPath);
#endif
                Debug.Log($"AIT: 빌드 폴더 열기: {buildPath}");
            }
            else
            {
                AITPlatformHelper.ShowInfoDialog("오류", "빌드 폴더를 찾을 수 없습니다. 먼저 빌드를 실행하세요.", "확인");
            }
        }

        // ==================== Reset Loading Screen ====================
        [MenuItem("AIT/Reset Loading Screen", false, 104)]
        public static void ResetLoadingScreen()
        {
            string projectLoadingPath = AITPackageInitializer.GetProjectLoadingPath();
            string sdkLoadingPath = AITPackageInitializer.GetSDKLoadingTemplatePath();

            if (sdkLoadingPath == null)
            {
                AITPlatformHelper.ShowInfoDialog("오류", "SDK 기본 로딩 화면 템플릿을 찾을 수 없습니다.", "확인");
                return;
            }

            // 확인 다이얼로그
            bool confirm = UnityEditor.EditorUtility.DisplayDialog(
                "로딩 화면 초기화",
                "로딩 화면을 기본 템플릿으로 초기화하시겠습니까?\n\n기존 커스터마이징 내용이 삭제됩니다.",
                "초기화",
                "취소"
            );

            if (!confirm) return;

            try
            {
                // AppsInToss 폴더가 없으면 생성
                string appsInTossDir = Path.GetDirectoryName(projectLoadingPath);
                if (!Directory.Exists(appsInTossDir))
                {
                    Directory.CreateDirectory(appsInTossDir);
                }

                // SDK 기본 템플릿으로 덮어쓰기
                File.Copy(sdkLoadingPath, projectLoadingPath, true);
                // .html 파일만 변경되므로 개별 임포트 (Domain Reload 방지)
                string assetPath = "Assets/AppsInToss/loading.html";
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                Debug.Log("[AIT] ✓ 로딩 화면 초기화 완료: " + projectLoadingPath);
                AITPlatformHelper.ShowInfoDialog(
                    "AIT",
                    "로딩 화면이 기본 템플릿으로 초기화되었습니다.\n\n파일 위치: Assets/AppsInToss/loading.html",
                    "확인"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT] 로딩 화면 초기화 실패: {e}");
                AITPlatformHelper.ShowInfoDialog("오류", $"로딩 화면 초기화 실패: {e.Message}", "확인");
            }
        }

        // ==================== Configuration ====================
        [MenuItem("AIT/Configuration", false, 201)]
        public static void ShowConfiguration()
        {
            AITConfigurationWindow.ShowWindow();
        }

        // ==================== 이슈 제보 ====================

        // priority 300: Configuration/Sentry 블록에서 11 이상 떨어져 있어 Unity 가 자동으로 구분선을 넣어준다.
        [MenuItem("AIT/이슈 제보하기", false, 300)]
        public static void OpenIssueReport()
        {
            AITIssueReportWindow.Open(AITIssueReportContext.Manual);
        }

        // ==================== Sentry ====================
        [MenuItem("AIT/Install Sentry SDK", false, 211)]
        public static void InstallSentry()
        {
            UnityEditor.PackageManager.Client.Add("https://github.com/getsentry/unity.git#4.1.0");
            Debug.Log("[AIT] Sentry Unity SDK 설치를 시작합니다...");
        }

        [MenuItem("AIT/Install Sentry SDK", true)]
        public static bool InstallSentryValidate()
        {
            // io.sentry.unity가 이미 설치되어 있으면 비활성화
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/io.sentry.unity");
            return info == null;
        }

        // ==================== Debug ====================
        [MenuItem("AIT/Debug/Reset All SDK State", false)]
        public static void ResetAllSDKState()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "SDK 상태 초기화",
                "모든 SDK 내부 상태를 초기화합니다.\n\n" +
                "• .gitignore 체크 상태\n" +
                "• 업데이트 체크 상태\n" +
                "• 패키지 매니저 설치 상태\n\n" +
                "다음 Editor 시작 시 모든 초기화가 다시 실행됩니다.",
                "초기화",
                "취소"
            );
            if (!confirm) return;

            // SessionState (세션 범위)
            AITGitGuard.ResetSessionState();

            // EditorPrefs (영구) + SessionState
            AITAutoUpdater.ResetDailyCheck();
            AITPackageInitializer.ResetInstallationState();

            Debug.Log("[AIT] ✓ 모든 SDK 상태가 초기화되었습니다.");
            EditorUtility.DisplayDialog("완료", "SDK 상태가 초기화되었습니다.\nEditor를 재시작하면 모든 초기화가 다시 실행됩니다.", "확인");
        }

        [MenuItem("AIT/Debug/Force Update WebGL Template", false)]
        public static void ForceUpdateWebGLTemplate()
        {
            bool changed = AITTemplateManager.EnsureWebGLTemplatesExist();
            if (changed)
            {
                AssetDatabase.Refresh();
                Debug.Log("[AIT] ✓ WebGL 템플릿이 최신 SDK 버전으로 갱신되었습니다.");
                EditorUtility.DisplayDialog("완료", "WebGL 템플릿이 최신 SDK 버전으로 갱신되었습니다.", "확인");
            }
            else
            {
                Debug.Log("[AIT] WebGL 템플릿이 이미 최신 상태입니다.");
                EditorUtility.DisplayDialog("확인", "WebGL 템플릿이 이미 최신 상태입니다.", "확인");
            }
        }

        // ==================== 서버 타입별 헬퍼 ====================

        private static ServerType OppositeType(ServerType type) =>
            type == ServerType.Dev ? ServerType.Prod : ServerType.Dev;

        private static AITServerStateManager GetServerState(ServerType type) =>
            type == ServerType.Dev ? devServerState : prodServerState;

        private static string GetServerLabel(ServerType type) =>
            type == ServerType.Dev ? "Dev" : "Production";

        private static AITBuildProfile GetBuildProfile(AITEditorScriptObject config, ServerType type) =>
            type == ServerType.Dev ? config.devServerProfile : config.productionProfile;

        private static string GetProfileName(ServerType type) =>
            type == ServerType.Dev ? "Dev Server" : "Production Server";

        /// <summary>
        /// 서버 시작 전 공통 검증: 이미 실행 중인지 확인하고, 반대편 서버 전환을 처리
        /// </summary>
        /// <returns>검증 통과 시 config, 실패 시 null</returns>
        private static AITEditorScriptObject ValidateAndSwitchServer(ServerType type)
        {
            var stateManager = GetServerState(type);
            string label = GetServerLabel(type);

            // 실제 상태 검증
            var currentState = stateManager.ValidateState();
            if (currentState == ServerState.Running)
            {
                Debug.LogWarning($"AIT: {label} 서버가 이미 실행 중입니다.");
                return null;
            }

            // 반대편 서버가 실행 중이면 확인 후 전환
            var oppositeType = OppositeType(type);
            var otherState = GetServerState(oppositeType).ValidateState();
            if (otherState == ServerState.Running)
            {
                string oppositeLabel = GetServerLabel(oppositeType);
                if (AITPlatformHelper.ShowConfirmDialog(
                    "서버 전환",
                    $"{oppositeLabel} 서버가 실행 중입니다.\n{label} 서버로 전환하시겠습니까?",
                    "예", "아니오",
                    autoApprove: true))
                {
                    StopServer(oppositeType);
                }
                else
                {
                    return null;
                }
            }

            var config = UnityUtil.GetEditorConf();
            if (!PathValidator.ValidateSettings(config))
            {
                return null;
            }

            return config;
        }

        // ==================== 통합 서버 메서드 ====================

        /// <summary>
        /// 서버 시작 (빌드 & 패키징 수행 후 granite dev 실행)
        /// </summary>
        private static void StartServer(ServerType type)
        {
            var config = ValidateAndSwitchServer(type);
            if (config == null) return;

            string profileName = GetProfileName(type);
            var profile = GetBuildProfile(config, type);

            // Dev Server는 granite build(production build)를 스킵하여 시작 속도 개선
            bool skipGraniteBuild = (type == ServerType.Dev);

            // 빌드 & 패키징 수행 (증분 빌드로 빠른 반복)
            Debug.Log($"AIT: 빌드 & 패키징 수행 중 (증분 빌드, {profileName} 프로필{(skipGraniteBuild ? ", granite build 스킵" : "")})...");
            buildStopwatch.Restart();

            var result = AITConvertCore.DoExport(
                buildWebGL: true,
                doPackaging: true,
                cleanBuild: false,
                profile: profile,
                profileName: profileName,
                skipGraniteBuild: skipGraniteBuild
            );
            buildStopwatch.Stop();

            if (result != AITConvertCore.AITExportError.SUCCEED)
            {
                AITDeployManager.ShowBuildFailedDialog(result, profileName);
                return;
            }

            Debug.Log($"AIT: 빌드 & 패키징 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");

            string buildPath = PathValidator.GetBuildTemplatePath();
            string npmPath = PathValidator.FindNpmPath();
            if (!PathValidator.ValidateNpmPath(npmPath))
            {
                return;
            }

            // Note: EnsureNodeModules 호출 제거 - PackageWebGLBuild에서 이미 pnpm install 실행됨

            LaunchServerProcess(type, config, buildPath, npmPath, openBrowser: true);
        }

        /// <summary>
        /// 서버 시작 (서버만, 빌드 없음)
        /// 기존 빌드 결과물을 사용하여 granite dev 서버만 재시작
        /// </summary>
        private static void StartServerOnly(ServerType type)
        {
            var config = ValidateAndSwitchServer(type);
            if (config == null) return;

            string buildPath = PathValidator.GetBuildTemplatePath();

            // 빌드 결과물이 있는지 확인
            if (!Directory.Exists(buildPath))
            {
                AITLog.Error($"AIT: 빌드 결과물이 없습니다. 먼저 Build & Package를 실행하세요. ({buildPath})", sentryCapture: false);
                AITPlatformHelper.ShowInfoDialog("빌드 필요", "빌드 결과물이 없습니다.\n먼저 Build & Package를 실행하세요.", "확인");
                return;
            }

            string npmPath = PathValidator.FindNpmPath();
            if (!PathValidator.ValidateNpmPath(npmPath))
            {
                return;
            }

            if (!PathValidator.EnsureNodeModules(buildPath, npmPath))
            {
                return;
            }

            LaunchServerProcess(type, config, buildPath, npmPath, openBrowser: false);
        }

        /// <summary>
        /// 서버 프로세스 실행 공통 로직 (포트 해석 → 프로세스 시작)
        /// </summary>
        private static void LaunchServerProcess(
            ServerType type, AITEditorScriptObject config,
            string buildPath, string npmPath, bool openBrowser)
        {
            var stateManager = GetServerState(type);
            string label = GetServerLabel(type);
            string logPrefix = GetProfileName(type);
            string suffix = openBrowser ? "" : " (서버만)";

            // 서버 포트 해석 및 충돌 검사
            if (!PortResolver.TryResolveServerPorts(config,
                out string graniteHost, out int granitePort,
                out string viteHost, out int vitePort))
            {
                return;
            }

            Debug.Log($"AIT: {label} 서버 시작 중{suffix} (granite dev)... ({buildPath})");
            Debug.Log($"AIT:   Granite: {graniteHost}:{granitePort}");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");

            // 캡처용 로컬 변수
            int finalVitePort = vitePort;
            int finalGranitePort = granitePort;

            try
            {
                // 환경 변수로 Vite 설정 전달 (granite.config.ts, vite.config.ts에서 사용)
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_GRANITE_HOST", graniteHost },
                    { "AIT_GRANITE_PORT", finalGranitePort.ToString() },
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", finalVitePort.ToString() }
                };

                // granite dev 명령어에 --host, --port 인자로 granite 서버 설정 전달
                string graniteCommand = "exec -- granite dev";

                var processManager = new AITProcessTreeManager();

                // 포트와 프로세스 관리자 저장 (상태는 변경하지 않음)
                stateManager.SetExpectedPortAndProcess(processManager, finalGranitePort);

                StartServerProcessWithPortDetection(
                    processManager,
                    buildPath, npmPath, graniteCommand, logPrefix, envVars, finalGranitePort,
                    onServerStarted: (detectedPort) =>
                    {
                        // 감지된 포트(Granite)를 저장하여 ValidateState에서 올바르게 확인할 수 있도록 함
                        stateManager.OnServerStarted(detectedPort);
                        Debug.Log($"AIT: {label} 서버가 시작되었습니다{suffix}");
                        Debug.Log($"AIT:   Granite (Metro): http://{graniteHost}:{finalGranitePort}");
                        Debug.Log($"AIT:   Vite: http://{viteHost}:{finalVitePort}");
                        if (openBrowser)
                            AITBrowserLauncher.OpenBrowser(finalVitePort, type);
                    },
                    onServerFailed: (reason) =>
                    {
                        Debug.LogError($"AIT: {label} 서버 시작 실패 - {reason}");
                        AITPlatformHelper.ShowInfoDialog($"{label} 서버 시작 실패", reason, "확인");
                        stateManager.OnServerFailed();
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: {label} 서버 시작 실패: {e}");
                AITPlatformHelper.ShowInfoDialog("오류", $"{label} 서버 시작 실패:\n{e.Message}", "확인");
                stateManager.OnServerFailed();
            }
        }

        /// <summary>
        /// 서버 중지
        /// </summary>
        private static void StopServer(ServerType type)
        {
            var stateManager = GetServerState(type);
            string label = GetServerLabel(type);

            // 백업: 포트에서 실행 중인 프로세스도 종료 (혹시 남아있는 경우)
            int port = stateManager?.Port ?? 0;
            if (port > 0)
            {
                PortResolver.KillProcessOnPort(port);
            }

            // 상태 관리자에 중지 알림
            stateManager?.OnServerStopped();

            Debug.Log($"AIT: {label} 서버가 중지되었습니다.");
        }

        // 서버 시작 타임아웃 (30초)
        private const double SERVER_START_TIMEOUT_SECONDS = 30.0;

        // 포트 직접 확인 폴백 시작 시간 (5초 후부터)
        // stdout 파싱이 실패해도 포트가 열려있으면 성공으로 처리
        private const double PORT_FALLBACK_CHECK_START_SECONDS = 5.0;

        /// <summary>
        /// 서버 프로세스 시작 (동적 포트 감지 포함) - 크로스 플랫폼
        /// AITProcessTreeManager를 사용하여 프로세스 트리 전체를 관리
        /// </summary>
        /// <param name="manager">프로세스 트리 관리자</param>
        /// <param name="envVars">환경 변수 (AIT_GRANITE_HOST, AIT_GRANITE_PORT, AIT_VITE_PORT 등)</param>
        /// <param name="expectedPort">예상 포트 (타임아웃 시 확인용)</param>
        /// <param name="onServerStarted">서버가 성공적으로 시작되면 호출되는 콜백 (메인 스레드에서 실행)</param>
        /// <param name="onServerFailed">서버 시작에 실패하면 호출되는 콜백 (메인 스레드에서 실행)</param>
        private static void StartServerProcessWithPortDetection(
            AITProcessTreeManager manager,
            string buildPath,
            string npmPath,
            string npmCommand,
            string logPrefix,
            Dictionary<string, string> envVars,
            int expectedPort,
            Action<int> onServerStarted,
            Action<string> onServerFailed = null)
        {
            string npmDir = Path.GetDirectoryName(npmPath);
            string pathEnv = AITPlatformHelper.BuildPathEnv(npmDir);

            ProcessStartInfo startInfo;

            if (AITPlatformHelper.IsWindows)
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{npmPath}\" {npmCommand}\"",
                    WorkingDirectory = buildPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                startInfo.EnvironmentVariables["PATH"] = pathEnv;

                // Windows: 환경 변수 직접 설정
                if (envVars != null)
                {
                    foreach (var kv in envVars)
                    {
                        startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                    }
                }
            }
            else
            {
                string escapedPathEnv = AITPlatformHelper.EscapeForBashDoubleQuotes(pathEnv);
                string escapedBuildPath = AITPlatformHelper.EscapeForBashDoubleQuotes(buildPath);
                string escapedNpmPath = AITPlatformHelper.EscapeForBashDoubleQuotes(npmPath);
                startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH=\\\"{escapedPathEnv}\\\" && cd \\\"{escapedBuildPath}\\\" && \\\"{escapedNpmPath}\\\" {npmCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                // 환경변수는 ProcessStartInfo.EnvironmentVariables로 설정
                // bash -c "..." 안에서 export 할당 시 JSON 등의 큰따옴표가
                // 바깥 큰따옴표와 충돌하므로, 셸 명령에서는 export하지 않음
                if (envVars != null)
                {
                    foreach (var kv in envVars)
                    {
                        startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                    }
                }
            }

            // pnpm이 비대화형(no TTY) 환경임을 인식하도록 CI=true 설정
            // (없으면 node_modules purge 확인 프롬프트에서 ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY 발생)
            // AITPlatformHelper.CreateProcessStartInfo / AITAsyncCommandRunner와 동일한 컨벤션
            startInfo.EnvironmentVariables["CI"] = "true";

            // 스레드 안전한 상태 플래그 (Interlocked 사용)
            // OutputDataReceived는 ThreadPool 스레드에서 호출되므로 원자적 연산 필요
            int serverStartedFlag = 0;  // 0 = false, 1 = true
            int serverFailedFlag = 0;   // 0 = false, 1 = true
            object failureReasonLock = new object();
            string failureReason = null;
            double startTime = EditorApplication.timeSinceStartup;

            // AITProcessTreeManager를 통해 프로세스 시작 (프로세스 그룹 관리)
            var process = manager.StartProcess(startInfo);

            // 타임아웃 체크 및 포트 폴백 확인을 위한 EditorApplication.update 콜백
            EditorApplication.CallbackFunction timeoutCheck = null;
            timeoutCheck = () =>
            {
                // 이미 성공 또는 실패한 경우 콜백 제거 (원자적 읽기)
                if (Interlocked.CompareExchange(ref serverStartedFlag, 0, 0) == 1 ||
                    Interlocked.CompareExchange(ref serverFailedFlag, 0, 0) == 1)
                {
                    EditorApplication.update -= timeoutCheck;
                    return;
                }

                double elapsed = EditorApplication.timeSinceStartup - startTime;

                // 폴백 체크: stdout 파싱이 실패해도 포트가 열려있으면 성공으로 처리
                // (일부 환경에서 stdout 버퍼링으로 인해 포트 정보가 지연될 수 있음)
                if (elapsed > PORT_FALLBACK_CHECK_START_SECONDS && expectedPort > 0)
                {
                    if (!PortResolver.IsPortAvailable(expectedPort))
                    {
                        // 포트가 사용 중 = 서버가 시작됨
                        if (Interlocked.CompareExchange(ref serverStartedFlag, 1, 0) == 0)
                        {
                            EditorApplication.update -= timeoutCheck;
                            Debug.Log($"[{logPrefix}] 포트 {expectedPort} 감지됨 (stdout 폴백)");
                            onServerStarted?.Invoke(expectedPort);
                            return;
                        }
                    }
                }

                // 타임아웃 체크
                if (elapsed > SERVER_START_TIMEOUT_SECONDS)
                {
                    // 원자적으로 실패 플래그 설정 (중복 호출 방지)
                    if (Interlocked.CompareExchange(ref serverFailedFlag, 1, 0) == 0)
                    {
                        EditorApplication.update -= timeoutCheck;

                        // 프로세스 종료 시도
                        try
                        {
                            manager.KillProcessTree();
                        }
                        catch
                        {
                            // 무시
                        }

                        string reason = $"서버 시작 타임아웃 ({SERVER_START_TIMEOUT_SECONDS}초)";
                        Debug.LogError($"[{logPrefix}] {reason}");
                        onServerFailed?.Invoke(reason);
                    }
                }
            };
            EditorApplication.update += timeoutCheck;

            // 프로세스 종료 감지
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                // 서버가 아직 시작되지 않았는데 프로세스가 종료된 경우 = 실패
                // 원자적으로 플래그 확인 및 설정
                if (Interlocked.CompareExchange(ref serverStartedFlag, 0, 0) == 0 &&
                    Interlocked.CompareExchange(ref serverFailedFlag, 1, 0) == 0)
                {
                    int exitCode = process.ExitCode;
                    string reason;
                    lock (failureReasonLock)
                    {
                        reason = failureReason ?? $"프로세스가 비정상 종료되었습니다 (Exit Code: {exitCode})";
                    }

                    // 스레드 안전한 메인 스레드 큐를 통해 콜백 실행
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        EditorApplication.update -= timeoutCheck;
                        Debug.LogError($"[{logPrefix}] 서버 시작 실패: {reason}");
                        onServerFailed?.Invoke(reason);
                    });
                }
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");

                    // 에러 패턴 감지 (stdout에도 에러가 출력될 수 있음)
                    if (PathValidator.IsErrorOutput(cleanOutput))
                    {
                        Debug.LogError($"[{logPrefix}] {cleanOutput}");

                        // 포트 충돌 에러 감지
                        if (PortResolver.IsPortConflictError(cleanOutput))
                        {
                            lock (failureReasonLock)
                            {
                                failureReason = "포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요.";
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[{logPrefix}] {args.Data}");
                    }

                    // 서버 시작 성공 감지 (포트 감지)
                    // IPv4: localhost:PORT, 0.0.0.0:PORT, 127.0.0.1:PORT
                    // IPv6: [::1]:PORT, [::]:PORT
                    // 원자적으로 플래그 확인 (ThreadPool 스레드에서 호출됨)
                    if (Interlocked.CompareExchange(ref serverStartedFlag, 0, 0) == 0 &&
                        Interlocked.CompareExchange(ref serverFailedFlag, 0, 0) == 0)
                    {
                        var portMatch = Regex.Match(cleanOutput, @"(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)");
                        if (portMatch.Success)
                        {
                            int port = int.Parse(portMatch.Groups[1].Value);

                            // 원자적으로 성공 플래그 설정 (중복 호출 방지)
                            if (Interlocked.CompareExchange(ref serverStartedFlag, 1, 0) == 0)
                            {
                                // 스레드 안전한 메인 스레드 큐를 통해 콜백 실행
                                MainThreadDispatcher.Enqueue(() =>
                                {
                                    EditorApplication.update -= timeoutCheck;
                                    onServerStarted?.Invoke(port);
                                });
                            }
                        }
                    }
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");

                    // stderr 출력을 실제 에러와 경고로 분류
                    if (PathValidator.IsErrorOutput(cleanOutput))
                        Debug.LogError($"[{logPrefix}] {cleanOutput}");
                    else
                        Debug.LogWarning($"[{logPrefix}] {cleanOutput}");

                    // 포트 충돌 에러 감지
                    if (PortResolver.IsPortConflictError(cleanOutput))
                    {
                        lock (failureReasonLock)
                        {
                            failureReason = "포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요.";
                        }
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

    }
}
