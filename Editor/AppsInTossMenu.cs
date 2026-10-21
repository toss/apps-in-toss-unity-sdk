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
    public partial class AppsInTossMenu
    {
        // 서버 상태 관리자 (실제 상태 기반 판단)
        private static AITServerStateManager devServerState;
        private static Stopwatch buildStopwatch = new Stopwatch();

        // 재진입 가드 — 비동기 빌드/패키징 진행 중 Start/Restart 메뉴 재클릭 차단.
        // AITDeployManager._buildEntryInProgress와 같은 정적 bool 플래그 방식이지만, 그쪽은
        // DoExportAsync 진입 직후(WaitAsync 대기 구간만) 해제하는 반면 여기서는 실제 빌드
        // 완료(onComplete 콜백)까지 유지한다. Dev Server 메뉴의 validate 함수는 서버 상태
        // (NotRunning)만 보고 활성화되므로, WebGL 빌드 이후 pnpm install/granite build가
        // 비동기로 진행되는 동안 열리는 재클릭 창을 이 플래그로 직접 막아야 한다.
        private static bool _devServerBuildInProgress;

        /// <summary>
        /// 도메인 리로드 시 기존 서버 프로세스 복원 및 종료 이벤트 등록
        /// </summary>
        static AppsInTossMenu()
        {
            // 구 Production Server EditorPrefs 키가 남아있으면 1회성 정리 (절대 throw하지 않음)
            AITServerStateManager.MigrateLegacyProdServerState();

            // 상태 관리자 초기화
            devServerState = new AITServerStateManager(ServerType.Dev);

            // 즉시 실제 상태 검증 (domain reload 후 복원)
            devServerState.ValidateState();
        }

        /// <summary>
        /// Unity Editor 종료 시 모든 서버 프로세스 정리
        /// MainThreadDispatcher의 EditorApplication.quitting 구독을 통해 호출됨.
        /// </summary>
        internal static void HandleEditorQuitting()
        {
            var devState = devServerState?.GetCachedState() ?? ServerState.NotRunning;

            if (devState != ServerState.NotRunning)
            {
                Debug.Log("[AIT] Editor 종료 - Dev 서버 프로세스 정리 중...");
                StopServer(ServerType.Dev);
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

                    if (devState != ServerState.NotRunning)
                    {
                        StopServer(ServerType.Dev);
                    }

                    break;
                }
            }
        }

        // ==================== Local Debug ====================
        // 메뉴 경로는 "Local Debug"지만 내부 ServerType/GetProfileName("Dev Server")·로그 prefix는
        // Sentry 노이즈 필터(AITEditorErrorTracker.ServerLogPrefixes)와 결합되어 있어 그대로 둔다.

        [MenuItem("AIT/Local Debug/Start Server", false, 1)]
        public static void MenuStartDevServer()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            StartServer(ServerType.Dev);
        }

        [MenuItem("AIT/Local Debug/Start Server", true)]
        public static bool ValidateMenuStartDevServer()
        {
            if (_devServerBuildInProgress) return false;
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.NotRunning;
        }

        [MenuItem("AIT/Local Debug/Stop Server", false, 2)]
        public static void MenuStopDevServer()
        {
            Debug.Log("AIT: Dev 서버 중지...");
            StopServer(ServerType.Dev);
        }

        [MenuItem("AIT/Local Debug/Stop Server", true)]
        public static bool ValidateMenuStopDevServer()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        [MenuItem("AIT/Local Debug/Restart Server", false, 3)]
        public static void MenuRestartDevServer()
        {
            Debug.Log("AIT: Dev 서버 재시작...");
            RestartDevServer();
        }

        [MenuItem("AIT/Local Debug/Restart Server", true)]
        public static bool ValidateMenuRestartDevServer()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        [MenuItem("AIT/Local Debug/Restart Server (server-only)", false, 4)]
        public static void MenuRestartDevServerOnly()
        {
            Debug.Log("AIT: Dev 서버 재시작 중 (서버만)...");
            RestartDevServerOnly();
        }

        [MenuItem("AIT/Local Debug/Restart Server (server-only)", true)]
        public static bool ValidateMenuRestartDevServerOnly()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running;
        }

        // RestartServer/RestartDevServer/RestartDevServerOnly는 Editor/AppsInTossMenu.DevServer.cs로 이동했습니다.

        // ==================== Deploy for Online Test / Deploy Release Candidate ====================
        // ait deploy CLI는 플래그와 무관하게 항상 콘솔 QR 테스트 환경에 배포한다(실제 출시는
        // 콘솔 심사/출시 신청으로만 가능). 두 메뉴는 빌드 방식(증분/클린)과 memo 접두사,
        // 성공 창의 콘솔 안내 노출 여부만 다르다 — Editor/Menu/AITDeployManager.cs 참조.

        [MenuItem("AIT/Deploy for Online Test", false, 31)]
        public static void DeployTest()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            AITDeployManager.RunDeploy(DeployKind.Test);
        }

        [MenuItem("AIT/Deploy Release Candidate", false, 32)]
        public static void DeployProduction()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            AITDeployManager.RunDeploy(DeployKind.Production);
        }

        // ==================== Advanced ====================
        [MenuItem("AIT/Advanced/Build & Package", false, 51)]
        public static void BuildAndPackage()
        {
            if (AITDeprecationChecker.BlockIfDeprecated()) return;
            Debug.Log("AIT: Build & Package 시작...");
            AITDeployManager.RunBuildAndPackage();
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

        // 서버 타입별 헬퍼(GetServerState~StartServerProcessWithPortDetection)는 Editor/AppsInTossMenu.DevServer.cs로 이동했습니다.

    }
}
