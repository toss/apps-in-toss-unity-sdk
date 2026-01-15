using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using AppsInToss.Editor;

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

            EditorApplication.quitting += OnEditorQuitting;
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesChanged;
        }

        /// <summary>
        /// Unity Editor 종료 시 모든 서버 프로세스 정리
        /// </summary>
        private static void OnEditorQuitting()
        {
            var devState = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            var prodState = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            bool hadRunningServers = devState != ServerState.NotRunning || prodState != ServerState.NotRunning;

            if (devState != ServerState.NotRunning)
            {
                Debug.Log("[AIT] Editor 종료 - Dev 서버 프로세스 정리 중...");
                StopDevServer();
            }

            if (prodState != ServerState.NotRunning)
            {
                Debug.Log("[AIT] Editor 종료 - Prod 서버 프로세스 정리 중...");
                StopProdServer();
            }

            if (hadRunningServers)
            {
                Debug.Log("[AIT] 모든 서버 프로세스가 정리되었습니다.");
            }
        }

        /// <summary>
        /// 패키지 등록 변경 시 SDK 패키지 제거 감지
        /// </summary>
        private static void OnPackagesChanged(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            foreach (var package in args.removed)
            {
                // 이 SDK 패키지가 제거되었는지 확인
                if (package.name == "im.toss.apps-in-toss-unity-sdk")
                {
                    Debug.Log("[AIT] SDK 패키지가 제거됨 - 서버 프로세스 정리 중...");

                    var devState = devServerState?.GetCachedState() ?? ServerState.NotRunning;
                    var prodState = prodServerState?.GetCachedState() ?? ServerState.NotRunning;

                    if (devState != ServerState.NotRunning)
                    {
                        StopDevServer();
                    }

                    if (prodState != ServerState.NotRunning)
                    {
                        StopProdServer();
                    }

                    break;
                }
            }
        }

        // ==================== Dev Server ====================

        [MenuItem("AIT/Dev Server/Start Server", false, 1)]
        public static void MenuStartDevServer()
        {
            StartDevServer();
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
            StopDevServer();
        }

        [MenuItem("AIT/Dev Server/Stop Server", true)]
        public static bool ValidateMenuStopDevServer()
        {
            var state = devServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running || state == ServerState.Starting;
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
            StartProdServer();
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
            StopProdServer();
        }

        [MenuItem("AIT/Production Server/Stop Server", true)]
        public static bool ValidateMenuStopProdServer()
        {
            var state = prodServerState?.GetCachedState() ?? ServerState.NotRunning;
            return state == ServerState.Running || state == ServerState.Starting;
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

        private static void RestartDevServer()
        {
            StopDevServer();
            // 짧은 딜레이 후 시작 (프로세스 종료 대기)
            EditorApplication.delayCall += () =>
            {
                System.Threading.Thread.Sleep(500);
                StartDevServer();
            };
        }

        private static void RestartProdServer()
        {
            StopProdServer();
            // 짧은 딜레이 후 시작 (프로세스 종료 대기)
            EditorApplication.delayCall += () =>
            {
                System.Threading.Thread.Sleep(500);
                StartProdServer();
            };
        }

        private static void RestartDevServerOnly()
        {
            StopDevServer();
            // 짧은 딜레이 후 시작 (프로세스 종료 대기)
            EditorApplication.delayCall += () =>
            {
                System.Threading.Thread.Sleep(500);
                StartDevServerOnly();
            };
        }

        private static void RestartProdServerOnly()
        {
            StopProdServer();
            // 짧은 딜레이 후 시작 (프로세스 종료 대기)
            EditorApplication.delayCall += () =>
            {
                System.Threading.Thread.Sleep(500);
                StartProdServerOnly();
            };
        }

        // ==================== Build ====================
        [MenuItem("AIT/Build", false, 21)]
        public static void Build()
        {
            Debug.Log("AIT: WebGL 빌드 시작...");
            ExecuteWebGLBuildOnly();
        }

        // ==================== Package ====================
        [MenuItem("AIT/Package", false, 22)]
        public static void Package()
        {
            Debug.Log("AIT: 패키징 시작...");
            ExecutePackageOnly();
        }

        // ==================== Build & Package ====================
        [MenuItem("AIT/Build & Package", false, 23)]
        public static void BuildAndPackage()
        {
            Debug.Log("AIT: Build & Package 시작...");
            ExecuteBuildAndPackage();
        }

        // ==================== Publish ====================
        [MenuItem("AIT/Publish", false, 31)]
        public static void Publish()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettingsForPackage(config)) return;

            string projectPath = UnityUtil.GetProjectPath();
            string aitBuildPath = Path.Combine(projectPath, "ait-build");
            string distPath = Path.Combine(aitBuildPath, "dist");
            PublishMode publishMode = config.publishMode; 

            // 기존 빌드가 있는지 확인
            bool hasExistingBuild = Directory.Exists(distPath) &&
                                    Directory.GetFiles(distPath, "*", SearchOption.AllDirectories).Length > 0;

            bool shouldRebuild = publishMode == PublishMode.RebuildAndDeploy;

            if (hasExistingBuild && publishMode == PublishMode.Manual)
            {
                // 기존 빌드가 있으면 사용자에게 선택권 부여
                int choice = EditorUtility.DisplayDialogComplex(
                    "Publish",
                    "기존 빌드가 존재합니다.\n\n" +
                    "코드나 에셋을 변경했다면 다시 빌드하는 것을 권장합니다.",
                    "다시 빌드 후 배포",  // 0: Alt (권장)
                    "취소",               // 1: Cancel
                    "기존 빌드로 배포"    // 2: Other
                );

                if (choice == 1)
                {
                    Debug.Log("AIT: Publish 취소됨");
                    return;
                }

                shouldRebuild = (choice == 0);
            }

            if (shouldRebuild)
            {
                // 클린 빌드 후 배포 (Production 프로필 사용)
                Debug.Log("AIT: 클린 빌드 & 배포 시작...");
                buildStopwatch.Restart();

                var result = AITConvertCore.DoExport(
                    buildWebGL: true,
                    doPackaging: true,
                    cleanBuild: true,
                    profile: config.productionProfile,
                    profileName: "Publish"
                );
                buildStopwatch.Stop();

                if (result != AITConvertCore.AITExportError.SUCCEED)
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    Debug.LogError($"AIT: 빌드 실패: {result}");
                    int choice = EditorUtility.DisplayDialogComplex(
                        "빌드 실패",
                        errorMessage + "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.",
                        "확인",
                        "Issue 신고",
                        null
                    );
                    if (choice == 1)
                    {
                        AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(result, "Publish");
                    }
                    return;
                }

                Debug.Log($"AIT: 클린 빌드 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
            }
            else
            {
                Debug.Log("AIT: 기존 빌드를 사용하여 배포합니다.");
            }

            // 배포 실행
            ExecuteDeploy();
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
                EditorUtility.DisplayDialog("정보", "삭제할 빌드 폴더가 없습니다.", "확인");
                return;
            }

            // 삭제할 폴더 목록 구성
            var foldersToDelete = new List<string>();
            if (webglExists) foldersToDelete.Add("webgl/");
            if (aitBuildExists) foldersToDelete.Add("ait-build/");

            bool confirmed = EditorUtility.DisplayDialog(
                "빌드 Clean",
                $"다음 폴더를 삭제하시겠습니까?\n\n• {string.Join("\n• ", foldersToDelete)}\n\n이 작업은 되돌릴 수 없습니다.",
                "삭제",
                "취소"
            );

            if (!confirmed) return;

            Debug.Log("AIT: 빌드 폴더 삭제 시작...");

            int deletedCount = 0;

            if (webglExists)
            {
                try
                {
                    Directory.Delete(webglPath, true);
                    Debug.Log($"AIT: ✓ webgl/ 폴더 삭제 완료");
                    deletedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"AIT: webgl/ 폴더 삭제 실패: {e.Message}");
                }
            }

            if (aitBuildExists)
            {
                try
                {
                    Directory.Delete(aitBuildPath, true);
                    Debug.Log($"AIT: ✓ ait-build/ 폴더 삭제 완료");
                    deletedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"AIT: ait-build/ 폴더 삭제 실패: {e.Message}");
                }
            }

            if (deletedCount > 0)
            {
                Debug.Log($"AIT: Clean 완료! ({deletedCount}개 폴더 삭제됨)");
                EditorUtility.DisplayDialog("완료", $"빌드 폴더 {deletedCount}개가 삭제되었습니다.", "확인");
            }
        }

        // ==================== Open Build Output ====================
        [MenuItem("AIT/Open Build Output", false, 102)]
        public static void OpenBuildOutput()
        {
            string buildPath = GetBuildTemplatePath();
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
                EditorUtility.DisplayDialog("오류", "빌드 폴더를 찾을 수 없습니다. 먼저 빌드를 실행하세요.", "확인");
            }
        }

        // ==================== Regenerate WebGL Templates ====================
        [MenuItem("AIT/Regenerate WebGL Templates", false, 103)]
        public static void RegenerateWebGLTemplates()
        {
            string projectTemplatesPath = Path.Combine(Application.dataPath, "WebGLTemplates");
            string projectTemplate = Path.Combine(projectTemplatesPath, "AITTemplate");

            // 기존 템플릿 삭제
            if (Directory.Exists(projectTemplate))
            {
                try
                {
                    Directory.Delete(projectTemplate, true);
                    Debug.Log("[AIT] 기존 WebGL 템플릿 삭제됨");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AIT] 템플릿 삭제 실패: {e.Message}");
                    EditorUtility.DisplayDialog("오류", $"템플릿 삭제 실패: {e.Message}", "확인");
                    return;
                }
            }

            // 새 템플릿 복사
            AITConvertCore.EnsureWebGLTemplatesExist();
            AssetDatabase.Refresh();

            Debug.Log("[AIT] ✓ WebGL 템플릿 재생성 완료");
            EditorUtility.DisplayDialog(
                "AIT",
                "WebGL 템플릿이 재생성되었습니다.\n\n새 템플릿이 프로젝트에 적용되었습니다.",
                "확인"
            );
        }

        // ==================== Configuration ====================
        [MenuItem("AIT/Configuration", false, 201)]
        public static void ShowConfiguration()
        {
            AITConfigurationWindow.ShowWindow();
        }

        // ============================================
        // 빌드 실행 메서드들
        // ============================================

        private static void ExecuteWebGLBuildOnly()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config)) return;

            Debug.Log("AIT: WebGL 빌드 시작...");
            buildStopwatch.Restart();

            try
            {
                // Build 단독 실행은 productionProfile 사용
                var result = AITConvertCore.DoExport(
                    buildWebGL: true,
                    doPackaging: false,
                    cleanBuild: false,
                    profile: config.productionProfile,
                    profileName: "Build"
                );
                buildStopwatch.Stop();

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.Log($"AIT: WebGL 빌드 완료! (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"WebGL 빌드가 완료되었습니다!\n\n소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    Debug.LogError($"AIT: WebGL 빌드 실패: {result}");
                    int choice = EditorUtility.DisplayDialogComplex(
                        "빌드 실패",
                        errorMessage + "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.",
                        "확인",
                        "Issue 신고",
                        null
                    );
                    if (choice == 1)
                    {
                        AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(result, "Build");
                    }
                }
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                Debug.LogError($"AIT: 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private static void ExecutePackageOnly()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettingsForPackage(config)) return;

            Debug.Log("AIT: 패키징 시작...");
            buildStopwatch.Restart();

            try
            {
                // Package 단독 실행은 productionProfile 사용
                var result = AITConvertCore.DoExport(
                    buildWebGL: false,
                    doPackaging: true,
                    cleanBuild: false,
                    profile: config.productionProfile,
                    profileName: "Package"
                );
                buildStopwatch.Stop();

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.Log($"AIT: 패키징 완료! (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"패키징이 완료되었습니다!\n\n소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    Debug.LogError($"AIT: 패키징 실패: {result}");
                    int choice = EditorUtility.DisplayDialogComplex(
                        "패키징 실패",
                        errorMessage + "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.",
                        "확인",
                        "Issue 신고",
                        null
                    );
                    if (choice == 1)
                    {
                        AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(result, "Package");
                    }
                }
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                Debug.LogError($"AIT: 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private static void ExecuteBuildAndPackage()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettingsForPackage(config)) return;

            Debug.Log("AIT: 전체 빌드 & 패키징 시작...");
            buildStopwatch.Restart();

            try
            {
                // Build & Package 메뉴는 productionProfile 사용
                var result = AITConvertCore.DoExport(
                    buildWebGL: true,
                    doPackaging: true,
                    cleanBuild: false,
                    profile: config.productionProfile,
                    profileName: "Build & Package"
                );
                buildStopwatch.Stop();

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.Log($"AIT: 전체 프로세스 완료! (총 소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"빌드 & 패키징이 완료되었습니다!\n\n총 소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    Debug.LogError($"AIT: 빌드 실패: {result}");
                    int choice = EditorUtility.DisplayDialogComplex(
                        "빌드 실패",
                        errorMessage + "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.",
                        "확인",
                        "Issue 신고",
                        null
                    );
                    if (choice == 1)
                    {
                        AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(result, "Build & Package");
                    }
                }
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                Debug.LogError($"AIT: 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private static void ExecuteDeploy()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettingsForPackage(config)) return;
            PublishMode publishMode = config.publishMode; 


            // AITCredentials에서 배포 키 로드
            string deploymentKey = AITCredentialsUtil.GetDeploymentKey();
            if (string.IsNullOrWhiteSpace(deploymentKey))
            {
                Debug.LogError("AIT: 배포 키가 설정되지 않았습니다.");
                EditorUtility.DisplayDialog("오류", "배포 키가 설정되지 않았습니다.\n\nApps in Toss > Configuration에서 배포 키를 입력해주세요.", "확인");
                return;
            }

            string buildPath = GetBuildTemplatePath();
            string distPath = Path.Combine(buildPath, "dist");

            // npm 경로 찾기
            string npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("AIT: npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                EditorUtility.DisplayDialog("오류", "npm을 찾을 수 없습니다.\n\nNode.js가 설치되어 있는지 확인하세요.", "확인");
                return;
            }

            if (publishMode == PublishMode.Manual)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "배포 확인",
                    $"Apps in Toss에 배포하시겠습니까?\n\n프로젝트: {config.appName}\n버전: {config.version}",
                    "배포",
                    "취소"
                );

                if (!confirmed) return;
            }

            Debug.Log("AIT: Apps in Toss 배포 시작...");

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);

                // pnpm run deploy를 사용하여 로컬 node_modules/.bin/ait 사용
                string pnpmName = AITPlatformHelper.IsWindows ? "pnpm.cmd" : "pnpm";
                string pnpmPath = Path.Combine(npmDir, pnpmName);

                // pnpm exec ait deploy --api-key "KEY" 형태로 직접 실행
                string command = $"\"{pnpmPath}\" exec ait deploy --api-key \"{deploymentKey}\"";
                var result = AITPlatformHelper.ExecuteCommand(
                    command,
                    buildPath,
                    new[] { npmDir },
                    timeoutMs: 300000,
                    verbose: true
                );

                if (!result.Success)
                {
                    if (result.ExitCode == -1)
                    {
                        Debug.LogError("AIT: 배포 타임아웃 (5분 초과)");
                        EditorUtility.DisplayDialog("타임아웃", "배포 시간이 초과되었습니다.", "확인");
                    }
                    else
                    {
                        // 에러 메시지 추출 (stdout과 stderr에서)
                        string errorDetail = ExtractDeployErrorMessage(result.Output, result.Error);
                        Debug.LogError($"AIT: 배포 실패 (Exit Code: {result.ExitCode})");

                        string dialogMessage = "배포에 실패했습니다.";
                        if (!string.IsNullOrEmpty(errorDetail))
                        {
                            dialogMessage += $"\n\n{errorDetail}";
                        }
                        dialogMessage += "\n\n자세한 내용은 Console 로그를 확인하세요.";
                        dialogMessage += "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.";

                        int choice = EditorUtility.DisplayDialogComplex(
                            "배포 실패",
                            dialogMessage,
                            "확인",
                            "Issue 신고",
                            null
                        );
                        if (choice == 1)
                        {
                            AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(AITConvertCore.AITExportError.NETWORK_ERROR, "Deploy");
                        }
                    }
                }
                else
                {
                    Debug.Log("AIT: 배포 완료!");
                    string deployUrl = ExtractDeployUrl(result.Output);
                    if (!string.IsNullOrEmpty(deployUrl))
                    {
                        Debug.Log($"AIT: 배포 URL: {deployUrl}");
                        DeploySuccessWindow.Show(deployUrl);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("성공", "Apps in Toss에 배포되었습니다!", "확인");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: 배포 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"배포 오류:\n{e.Message}", "확인");
            }
        }

        /// <summary>
        /// Dev 서버 시작 (granite dev 사용)
        /// 자동으로 빌드 & 패키징 수행 후 서버 시작
        /// </summary>
        private static void StartDevServer()
        {
            // 실제 상태 검증
            var currentState = devServerState.ValidateState();
            if (currentState == ServerState.Running)
            {
                Debug.LogWarning("AIT: Dev 서버가 이미 실행 중입니다.");
                return;
            }

            // Production 서버가 실행 중이면 확인 후 전환
            var prodState = prodServerState.ValidateState();
            if (prodState == ServerState.Running || prodState == ServerState.Starting)
            {
                if (EditorUtility.DisplayDialog(
                    "서버 전환",
                    "Production 서버가 실행 중입니다.\nDev 서버로 전환하시겠습니까?",
                    "예", "아니오"))
                {
                    StopProdServer();
                }
                else
                {
                    return;
                }
            }

            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config))
            {
                return;
            }

            // 빌드 & 패키징 수행 (Dev Server: devServerProfile 사용, 증분 빌드로 빠른 반복)
            Debug.Log("AIT: 빌드 & 패키징 수행 중 (증분 빌드, Dev Server 프로필)...");
            buildStopwatch.Restart();

            var result = AITConvertCore.DoExport(
                buildWebGL: true,
                doPackaging: true,
                cleanBuild: false,
                profile: config.devServerProfile,
                profileName: "Dev Server"
            );
            buildStopwatch.Stop();

            if (result != AITConvertCore.AITExportError.SUCCEED)
            {
                string errorMessage = AITConvertCore.GetErrorMessage(result);
                Debug.LogError($"AIT: 빌드 실패: {result}");
                int choice = EditorUtility.DisplayDialogComplex(
                    "빌드 실패",
                    errorMessage + "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.",
                    "확인",
                    "Issue 신고",
                    null
                );
                if (choice == 1)
                {
                    AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(result, "Dev Server");
                }
                return;
            }

            Debug.Log($"AIT: 빌드 & 패키징 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");

            string buildPath = GetBuildTemplatePath();
            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath))
            {
                return;
            }

            if (!EnsureNodeModules(buildPath, npmPath))
            {
                return;
            }

            // 서버 설정
            string graniteHost = !string.IsNullOrEmpty(config.graniteHost) ? config.graniteHost : "0.0.0.0";
            int granitePort = config.granitePort > 0 ? config.granitePort : 8081;
            string viteHost = !string.IsNullOrEmpty(config.viteHost) ? config.viteHost : "localhost";
            int vitePort = config.vitePort > 0 ? config.vitePort : 5173;

            Debug.Log($"AIT: Dev 서버 시작 중 (granite dev)... ({buildPath})");
            Debug.Log($"AIT:   Granite: {graniteHost}:{granitePort}");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");

            try
            {
                // 환경 변수로 Vite 설정 전달 (granite.config.ts, vite.config.ts에서 사용)
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_GRANITE_HOST", graniteHost },
                    { "AIT_GRANITE_PORT", granitePort.ToString() },
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", vitePort.ToString() }
                };

                // granite dev 명령어에 --host, --port 인자로 granite 서버 설정 전달
                string graniteCommand = "exec -- granite dev";

                var processManager = new AITProcessTreeManager();

                // 상태 관리자에 Starting 상태 알림
                devServerState.OnServerStarting(processManager, vitePort);

                StartServerProcessWithPortDetection(
                    processManager,
                    buildPath, npmPath, graniteCommand, "Dev Server", envVars,
                    onServerStarted: (detectedPort) =>
                    {
                        devServerState.OnServerStarted(vitePort);
                        Debug.Log($"AIT: Dev 서버가 시작되었습니다");
                        Debug.Log($"AIT:   Granite (Metro): http://{graniteHost}:{granitePort}");
                        Debug.Log($"AIT:   Vite: http://{viteHost}:{vitePort}");
                        Application.OpenURL($"http://localhost:{vitePort}/index.html");
                    },
                    onServerFailed: (reason) =>
                    {
                        Debug.LogError($"AIT: Dev 서버 시작 실패 - {reason}");
                        EditorUtility.DisplayDialog("Dev 서버 시작 실패", reason, "확인");
                        devServerState.OnServerFailed();
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Dev 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Dev 서버 시작 실패:\n{e.Message}", "확인");
                devServerState.OnServerFailed();
            }
        }

        /// <summary>
        /// Dev 서버 시작 (서버만, 빌드 없음)
        /// 기존 빌드 결과물을 사용하여 granite dev 서버만 재시작
        /// </summary>
        private static void StartDevServerOnly()
        {
            // 실제 상태 검증
            var currentState = devServerState.ValidateState();
            if (currentState == ServerState.Running)
            {
                Debug.LogWarning("AIT: Dev 서버가 이미 실행 중입니다.");
                return;
            }

            // Production 서버가 실행 중이면 확인 후 전환
            var prodState = prodServerState.ValidateState();
            if (prodState == ServerState.Running || prodState == ServerState.Starting)
            {
                if (EditorUtility.DisplayDialog(
                    "서버 전환",
                    "Production 서버가 실행 중입니다.\nDev 서버로 전환하시겠습니까?",
                    "예", "아니오"))
                {
                    StopProdServer();
                }
                else
                {
                    return;
                }
            }

            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config))
            {
                return;
            }

            string buildPath = GetBuildTemplatePath();

            // 빌드 결과물이 있는지 확인
            if (!Directory.Exists(buildPath))
            {
                Debug.LogError($"AIT: 빌드 결과물이 없습니다. 먼저 Build & Package를 실행하세요. ({buildPath})");
                EditorUtility.DisplayDialog("빌드 필요", "빌드 결과물이 없습니다.\n먼저 Build & Package를 실행하세요.", "확인");
                return;
            }

            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath))
            {
                return;
            }

            if (!EnsureNodeModules(buildPath, npmPath))
            {
                return;
            }

            // 서버 설정
            string graniteHost = !string.IsNullOrEmpty(config.graniteHost) ? config.graniteHost : "0.0.0.0";
            int granitePort = config.granitePort > 0 ? config.granitePort : 8081;
            string viteHost = !string.IsNullOrEmpty(config.viteHost) ? config.viteHost : "localhost";
            int vitePort = config.vitePort > 0 ? config.vitePort : 5173;

            Debug.Log($"AIT: Dev 서버 시작 중 (서버만, granite dev)... ({buildPath})");
            Debug.Log($"AIT:   Granite: {graniteHost}:{granitePort}");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");

            try
            {
                // 환경 변수로 Vite 설정 전달 (granite.config.ts, vite.config.ts에서 사용)
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_GRANITE_HOST", graniteHost },
                    { "AIT_GRANITE_PORT", granitePort.ToString() },
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", vitePort.ToString() }
                };

                // granite dev 명령어에 --host, --port 인자로 granite 서버 설정 전달
                string graniteCommand = "exec -- granite dev";

                var processManager = new AITProcessTreeManager();

                // 상태 관리자에 Starting 상태 알림
                devServerState.OnServerStarting(processManager, vitePort);

                StartServerProcessWithPortDetection(
                    processManager,
                    buildPath, npmPath, graniteCommand, "Dev Server", envVars,
                    onServerStarted: (detectedPort) =>
                    {
                        devServerState.OnServerStarted(vitePort);
                        Debug.Log($"AIT: Dev 서버가 시작되었습니다 (서버만)");
                        Debug.Log($"AIT:   Granite (Metro): http://{graniteHost}:{granitePort}");
                        Debug.Log($"AIT:   Vite: http://{viteHost}:{vitePort}");
                        // 서버만 재시작이므로 브라우저는 열지 않음
                    },
                    onServerFailed: (reason) =>
                    {
                        Debug.LogError($"AIT: Dev 서버 시작 실패 - {reason}");
                        EditorUtility.DisplayDialog("Dev 서버 시작 실패", reason, "확인");
                        devServerState.OnServerFailed();
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Dev 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Dev 서버 시작 실패:\n{e.Message}", "확인");
                devServerState.OnServerFailed();
            }
        }

        /// <summary>
        /// Dev 서버 중지
        /// </summary>
        private static void StopDevServer()
        {
            // 백업: 포트에서 실행 중인 프로세스도 종료 (혹시 남아있는 경우)
            int port = devServerState?.Port ?? 0;
            if (port > 0)
            {
                KillProcessOnPort(port);
            }

            // 상태 관리자에 중지 알림
            devServerState?.OnServerStopped();

            Debug.Log("AIT: Dev 서버가 중지되었습니다.");
        }

        /// <summary>
        /// Production 서버 시작 (vite preview 사용)
        /// 자동으로 빌드 & 패키징 수행 후 서버 시작
        /// </summary>
        private static void StartProdServer()
        {
            // 실제 상태 검증
            var currentState = prodServerState.ValidateState();
            if (currentState == ServerState.Running)
            {
                Debug.LogWarning("AIT: Production 서버가 이미 실행 중입니다.");
                return;
            }

            // Dev 서버가 실행 중이면 확인 후 전환
            var devState = devServerState.ValidateState();
            if (devState == ServerState.Running || devState == ServerState.Starting)
            {
                if (EditorUtility.DisplayDialog(
                    "서버 전환",
                    "Dev 서버가 실행 중입니다.\nProduction 서버로 전환하시겠습니까?",
                    "예", "아니오"))
                {
                    StopDevServer();
                }
                else
                {
                    return;
                }
            }

            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config))
            {
                return;
            }

            // 빌드 & 패키징 수행 (Production Server: productionProfile 사용, 클린 빌드로 깨끗한 결과물 보장)
            Debug.Log("AIT: 빌드 & 패키징 수행 중 (클린 빌드, Production 프로필)...");
            buildStopwatch.Restart();

            var result = AITConvertCore.DoExport(
                buildWebGL: true,
                doPackaging: true,
                cleanBuild: true,
                profile: config.productionProfile,
                profileName: "Production Server"
            );
            buildStopwatch.Stop();

            if (result != AITConvertCore.AITExportError.SUCCEED)
            {
                string errorMessage = AITConvertCore.GetErrorMessage(result);
                Debug.LogError($"AIT: 빌드 실패: {result}");
                int choice = EditorUtility.DisplayDialogComplex(
                    "빌드 실패",
                    errorMessage + "\n\n문제가 지속되면 'Issue 신고'를 클릭하세요.",
                    "확인",
                    "Issue 신고",
                    null
                );
                if (choice == 1)
                {
                    AppsInToss.Editor.AITErrorReporter.OpenIssueInBrowser(result, "Production Server");
                }
                return;
            }

            Debug.Log($"AIT: 빌드 & 패키징 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");

            string buildPath = GetBuildTemplatePath();
            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath))
            {
                return;
            }

            if (!EnsureNodeModules(buildPath, npmPath))
            {
                return;
            }

            // 서버 설정 (Dev 서버와 동일)
            string graniteHost = !string.IsNullOrEmpty(config.graniteHost) ? config.graniteHost : "0.0.0.0";
            int granitePort = config.granitePort > 0 ? config.granitePort : 8081;
            string viteHost = !string.IsNullOrEmpty(config.viteHost) ? config.viteHost : "localhost";
            int vitePort = config.vitePort > 0 ? config.vitePort : 5173;

            Debug.Log($"AIT: Production 서버 시작 중 (granite dev)... ({buildPath})");
            Debug.Log($"AIT:   Granite: {graniteHost}:{granitePort}");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");

            try
            {
                // 환경 변수로 Vite 설정 전달 (granite.config.ts, vite.config.ts에서 사용)
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_GRANITE_HOST", graniteHost },
                    { "AIT_GRANITE_PORT", granitePort.ToString() },
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", vitePort.ToString() }
                };

                // granite dev 명령어에 --host, --port 인자로 granite 서버 설정 전달
                string graniteCommand = "exec -- granite dev";

                var processManager = new AITProcessTreeManager();

                // 상태 관리자에 Starting 상태 알림
                prodServerState.OnServerStarting(processManager, vitePort);

                StartServerProcessWithPortDetection(
                    processManager,
                    buildPath, npmPath, graniteCommand, "Prod Server", envVars,
                    onServerStarted: (detectedPort) =>
                    {
                        prodServerState.OnServerStarted(vitePort);
                        Debug.Log($"AIT: Production 서버가 시작되었습니다");
                        Debug.Log($"AIT:   Granite (Metro): http://{graniteHost}:{granitePort}");
                        Debug.Log($"AIT:   Vite: http://{viteHost}:{vitePort}");
                        Application.OpenURL($"http://localhost:{vitePort}/");
                    },
                    onServerFailed: (reason) =>
                    {
                        Debug.LogError($"AIT: Production 서버 시작 실패 - {reason}");
                        EditorUtility.DisplayDialog("Production 서버 시작 실패", reason, "확인");
                        prodServerState.OnServerFailed();
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Production 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Production 서버 시작 실패:\n{e.Message}", "확인");
                prodServerState.OnServerFailed();
            }
        }

        /// <summary>
        /// Production 서버 시작 (서버만, 빌드 없음)
        /// 기존 빌드 결과물을 사용하여 granite dev 서버만 재시작
        /// </summary>
        private static void StartProdServerOnly()
        {
            // 실제 상태 검증
            var currentState = prodServerState.ValidateState();
            if (currentState == ServerState.Running)
            {
                Debug.LogWarning("AIT: Production 서버가 이미 실행 중입니다.");
                return;
            }

            // Dev 서버가 실행 중이면 확인 후 전환
            var devState = devServerState.ValidateState();
            if (devState == ServerState.Running || devState == ServerState.Starting)
            {
                if (EditorUtility.DisplayDialog(
                    "서버 전환",
                    "Dev 서버가 실행 중입니다.\nProduction 서버로 전환하시겠습니까?",
                    "예", "아니오"))
                {
                    StopDevServer();
                }
                else
                {
                    return;
                }
            }

            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config))
            {
                return;
            }

            string buildPath = GetBuildTemplatePath();

            // 빌드 결과물이 있는지 확인
            if (!Directory.Exists(buildPath))
            {
                Debug.LogError($"AIT: 빌드 결과물이 없습니다. 먼저 Build & Package를 실행하세요. ({buildPath})");
                EditorUtility.DisplayDialog("빌드 필요", "빌드 결과물이 없습니다.\n먼저 Build & Package를 실행하세요.", "확인");
                return;
            }

            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath))
            {
                return;
            }

            if (!EnsureNodeModules(buildPath, npmPath))
            {
                return;
            }

            // 서버 설정 (Dev 서버와 동일)
            string graniteHost = !string.IsNullOrEmpty(config.graniteHost) ? config.graniteHost : "0.0.0.0";
            int granitePort = config.granitePort > 0 ? config.granitePort : 8081;
            string viteHost = !string.IsNullOrEmpty(config.viteHost) ? config.viteHost : "localhost";
            int vitePort = config.vitePort > 0 ? config.vitePort : 5173;

            Debug.Log($"AIT: Production 서버 시작 중 (서버만, granite dev)... ({buildPath})");
            Debug.Log($"AIT:   Granite: {graniteHost}:{granitePort}");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");

            try
            {
                // 환경 변수로 Vite 설정 전달 (granite.config.ts, vite.config.ts에서 사용)
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_GRANITE_HOST", graniteHost },
                    { "AIT_GRANITE_PORT", granitePort.ToString() },
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", vitePort.ToString() }
                };

                // granite dev 명령어에 --host, --port 인자로 granite 서버 설정 전달
                string graniteCommand = "exec -- granite dev";

                var processManager = new AITProcessTreeManager();

                // 상태 관리자에 Starting 상태 알림
                prodServerState.OnServerStarting(processManager, vitePort);

                StartServerProcessWithPortDetection(
                    processManager,
                    buildPath, npmPath, graniteCommand, "Prod Server", envVars,
                    onServerStarted: (detectedPort) =>
                    {
                        prodServerState.OnServerStarted(vitePort);
                        Debug.Log($"AIT: Production 서버가 시작되었습니다 (서버만)");
                        Debug.Log($"AIT:   Granite (Metro): http://{graniteHost}:{granitePort}");
                        Debug.Log($"AIT:   Vite: http://{viteHost}:{vitePort}");
                        // 서버만 재시작이므로 브라우저는 열지 않음
                    },
                    onServerFailed: (reason) =>
                    {
                        Debug.LogError($"AIT: Production 서버 시작 실패 - {reason}");
                        EditorUtility.DisplayDialog("Production 서버 시작 실패", reason, "확인");
                        prodServerState.OnServerFailed();
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Production 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Production 서버 시작 실패:\n{e.Message}", "확인");
                prodServerState.OnServerFailed();
            }
        }

        /// <summary>
        /// Production 서버 중지
        /// </summary>
        private static void StopProdServer()
        {
            // 백업: 포트에서 실행 중인 프로세스도 종료 (혹시 남아있는 경우)
            int port = prodServerState?.Port ?? 0;
            if (port > 0)
            {
                KillProcessOnPort(port);
            }

            // 상태 관리자에 중지 알림
            prodServerState?.OnServerStopped();

            Debug.Log("AIT: Production 서버가 중지되었습니다.");
        }

        /// <summary>
        /// 서버 프로세스 시작 (동적 포트 감지 포함) - 크로스 플랫폼
        /// AITProcessTreeManager를 사용하여 프로세스 트리 전체를 관리
        /// </summary>
        /// <param name="manager">프로세스 트리 관리자</param>
        /// <param name="envVars">환경 변수 (AIT_GRANITE_HOST, AIT_GRANITE_PORT, AIT_VITE_PORT 등)</param>
        /// <param name="onServerStarted">서버가 성공적으로 시작되면 호출되는 콜백 (메인 스레드에서 실행)</param>
        /// <param name="onServerFailed">서버 시작에 실패하면 호출되는 콜백 (메인 스레드에서 실행)</param>
        private static void StartServerProcessWithPortDetection(
            AITProcessTreeManager manager,
            string buildPath,
            string npmPath,
            string npmCommand,
            string logPrefix,
            Dictionary<string, string> envVars,
            Action<int> onServerStarted,
            Action<string> onServerFailed = null)
        {
            string npmDir = Path.GetDirectoryName(npmPath);
            string pathEnv = AITPlatformHelper.BuildPathEnv(npmDir);

            // 환경 변수 export 문자열 생성 (Unix용)
            string envExports = "";
            if (envVars != null && envVars.Count > 0)
            {
                var exports = new List<string>();
                foreach (var kv in envVars)
                {
                    exports.Add($"export {kv.Key}='{kv.Value}'");
                }
                envExports = string.Join(" && ", exports) + " && ";
            }

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
                startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"{envExports}export PATH='{pathEnv}' && cd '{buildPath}' && '{npmPath}' {npmCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
            }

            bool serverStarted = false;
            bool serverFailed = false;
            string failureReason = null;

            // AITProcessTreeManager를 통해 프로세스 시작 (프로세스 그룹 관리)
            var process = manager.StartProcess(startInfo);

            // 프로세스 종료 감지
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                // 서버가 아직 시작되지 않았는데 프로세스가 종료된 경우 = 실패
                if (!serverStarted && !serverFailed)
                {
                    serverFailed = true;
                    int exitCode = process.ExitCode;
                    string reason = failureReason ?? $"프로세스가 비정상 종료되었습니다 (Exit Code: {exitCode})";

                    EditorApplication.delayCall += () =>
                    {
                        Debug.LogError($"[{logPrefix}] 서버 시작 실패: {reason}");
                        onServerFailed?.Invoke(reason);
                    };
                }
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");

                    // 에러 패턴 감지 (stdout에도 에러가 출력될 수 있음)
                    if (IsErrorOutput(cleanOutput))
                    {
                        Debug.LogError($"[{logPrefix}] {cleanOutput}");

                        // 포트 충돌 에러 감지
                        if (IsPortConflictError(cleanOutput))
                        {
                            failureReason = "포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요.";
                        }
                    }
                    else
                    {
                        Debug.Log($"[{logPrefix}] {args.Data}");
                    }

                    // 서버 시작 성공 감지 (포트 감지)
                    if (!serverStarted && !serverFailed)
                    {
                        var portMatch = Regex.Match(cleanOutput, @"localhost:(\d+)");
                        if (portMatch.Success)
                        {
                            int port = int.Parse(portMatch.Groups[1].Value);
                            serverStarted = true;

                            // Unity 메인 스레드에서 콜백 실행
                            EditorApplication.delayCall += () => onServerStarted?.Invoke(port);
                        }
                    }
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");

                    // stderr는 기본적으로 에러로 처리
                    Debug.LogError($"[{logPrefix}] {cleanOutput}");

                    // 포트 충돌 에러 감지
                    if (IsPortConflictError(cleanOutput))
                    {
                        failureReason = "포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요.";
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        /// <summary>
        /// 출력이 에러인지 판단
        /// </summary>
        private static bool IsErrorOutput(string output)
        {
            if (string.IsNullOrEmpty(output)) return false;

            string lower = output.ToLowerInvariant();

            // 명확한 에러 패턴
            if (lower.Contains("error:") ||
                lower.Contains("failed") ||
                lower.Contains("eaddrinuse") ||
                lower.Contains("port is already in use") ||
                lower.Contains("address already in use") ||
                lower.Contains("cannot find module") ||
                lower.Contains("command not found") ||
                lower.Contains("permission denied"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 포트 충돌 에러인지 판단
        /// </summary>
        private static bool IsPortConflictError(string output)
        {
            if (string.IsNullOrEmpty(output)) return false;

            string lower = output.ToLowerInvariant();
            return lower.Contains("eaddrinuse") ||
                   lower.Contains("port is already in use") ||
                   lower.Contains("address already in use");
        }

        /// <summary>
        /// 빌드 경로 유효성 검사
        /// </summary>
        private static bool ValidateBuildPath(string buildPath)
        {
            if (!Directory.Exists(buildPath))
            {
                EditorUtility.DisplayDialog("오류", "빌드 폴더를 찾을 수 없습니다.\n\n먼저 빌드를 실행하세요.", "확인");
                return false;
            }

            string indexPath = Path.Combine(buildPath, "index.html");
            if (!File.Exists(indexPath))
            {
                EditorUtility.DisplayDialog("오류", "index.html을 찾을 수 없습니다.\n\n먼저 빌드를 실행하세요.", "확인");
                return false;
            }

            return true;
        }

        /// <summary>
        /// npm 경로 유효성 검사
        /// </summary>
        private static bool ValidateNpmPath(string npmPath)
        {
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("AIT: npm을 찾을 수 없습니다.");
                EditorUtility.DisplayDialog("오류", "npm을 찾을 수 없습니다.\n\nNode.js가 설치되어 있는지 확인하세요.", "확인");
                return false;
            }
            return true;
        }

        /// <summary>
        /// node_modules 확인 및 설치
        /// </summary>
        private static bool EnsureNodeModules(string buildPath, string npmPath)
        {
            string nodeModulesPath = Path.Combine(buildPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                Debug.Log("AIT: node_modules가 없습니다. npm install을 실행합니다...");

                string localCachePath = Path.Combine(buildPath, ".npm-cache");
                var installResult = AITNpmRunner.RunNpmCommandWithCache(
                    buildPath, npmPath, "install", localCachePath, "npm install 실행 중..."
                );

                if (installResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.LogError("AIT: npm install 실패");
                    EditorUtility.DisplayDialog("오류", "npm install 실패\n\nConsole 로그를 확인하세요.", "확인");
                    return false;
                }

                Debug.Log("AIT: npm install 완료");
            }
            return true;
        }

        private static void KillProcessOnPort(int port)
        {
            try
            {
                string command;

                if (AITPlatformHelper.IsWindows)
                {
                    // Windows: netstat + taskkill
                    command = $"for /f \"tokens=5\" %a in ('netstat -aon ^| findstr :{port}') do taskkill /PID %a /F 2>nul";
                }
                else
                {
                    // Unix: lsof + kill
                    command = $"lsof -ti :{port} | xargs kill -9 2>/dev/null";
                }

                AITPlatformHelper.ExecuteCommand(command, null, null, timeoutMs: 2000, verbose: false);
            }
            catch
            {
                // 무시
            }
        }

        /// <summary>
        /// 포트가 사용 가능한지 확인 (0.0.0.0과 127.0.0.1 모두 체크)
        /// </summary>
        private static bool IsPortAvailable(int port)
        {
            // granite/vite는 0.0.0.0에 바인딩하므로 Any로 체크해야 함
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                listener.Start();
                listener.Stop();
            }
            catch
            {
                return false;
            }

            // 추가로 Loopback도 체크 (다른 프로세스가 127.0.0.1에만 바인딩한 경우)
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 사용 가능한 포트 찾기 (시작 포트부터 최대 10개 시도)
        /// </summary>
        private static int FindAvailablePort(int startPort, int maxAttempts = 10)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                int port = startPort + i;
                if (IsPortAvailable(port))
                {
                    return port;
                }
                Debug.Log($"[AIT] 포트 {port}가 사용 중, 다음 포트 시도...");
            }
            return -1; // 사용 가능한 포트 없음
        }

        // ============================================
        // 유틸리티 메서드들
        // ============================================

        /// <summary>
        /// 배포 출력에서 intoss-private:// URL 추출
        /// </summary>
        private static string ExtractDeployUrl(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return null;
            }

            // intoss-private://... URL 패턴 찾기
            // (ANSI 코드는 AITPlatformHelper.ExecuteCommand에서 이미 제거됨)
            var match = Regex.Match(output, @"intoss-private://[^\s\│\│]+");
            if (match.Success)
            {
                return match.Value.TrimEnd('│', ' ', '\r', '\n');
            }

            return null;
        }

        /// <summary>
        /// 배포 에러 메시지에서 사용자에게 보여줄 핵심 내용 추출
        /// </summary>
        private static string ExtractDeployErrorMessage(string stdout, string stderr)
        {
            // stderr와 stdout 합치기
            string combined = $"{stdout}\n{stderr}".Trim();

            if (string.IsNullOrEmpty(combined))
            {
                return null;
            }

            // 일반적인 에러 패턴 감지
            var lines = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var errorLines = new List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                // (ANSI 코드는 AITPlatformHelper.ExecuteCommand에서 이미 제거됨)

                // 에러 관련 라인 수집
                if (trimmed.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("ERR!", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("ENOENT") ||
                    trimmed.Contains("EACCES") ||
                    trimmed.Contains("401") ||
                    trimmed.Contains("403") ||
                    trimmed.Contains("404") ||
                    trimmed.Contains("500") ||
                    trimmed.Contains("Unauthorized") ||
                    trimmed.Contains("Forbidden") ||
                    trimmed.Contains("Not Found") ||
                    trimmed.Contains("failed") && trimmed.Contains("deploy"))
                {
                    errorLines.Add(trimmed);
                }
            }

            if (errorLines.Count > 0)
            {
                // 최대 3줄까지만 표시
                int maxLines = Math.Min(errorLines.Count, 3);
                return string.Join("\n", errorLines.GetRange(0, maxLines));
            }

            // 에러 패턴을 못 찾았으면 마지막 몇 줄 반환
            if (lines.Length > 0)
            {
                int startIndex = Math.Max(0, lines.Length - 3);
                var lastLines = new List<string>();
                for (int i = startIndex; i < lines.Length; i++)
                {
                    string trimmed = Regex.Replace(lines[i].Trim(), @"\x1B\[[0-9;]*[mGKH]", "");
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        lastLines.Add(trimmed);
                    }
                }
                if (lastLines.Count > 0)
                {
                    return string.Join("\n", lastLines);
                }
            }

            return null;
        }

        private static bool ValidateSettings(AITEditorScriptObject config)
        {
            if (config == null)
            {
                EditorUtility.DisplayDialog("오류", "설정을 찾을 수 없습니다.", "확인");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 패키징/배포용 설정 검증 (appName 필수)
        /// </summary>
        private static bool ValidateSettingsForPackage(AITEditorScriptObject config)
        {
            if (!ValidateSettings(config))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.appName))
            {
                Debug.LogError("AIT: App Name이 설정되지 않았습니다.");
                EditorUtility.DisplayDialog("오류", "App Name이 설정되지 않았습니다.\n\nAIT > Configuration에서 App Name을 입력해주세요.", "확인");
                return false;
            }

            return true;
        }

        private static string GetBuildTemplatePath()
        {
            string projectPath = UnityUtil.GetProjectPath();
            return Path.Combine(projectPath, "ait-build");
        }

        private static string FindNpmPath()
        {
            // AITConvertCore의 FindNpm 사용 (pnpm 우선, 로컬 설치 지원)
            return AITConvertCore.FindNpm();
        }
    }

    /// <summary>
    /// 배포 성공 시 URL을 표시하고 복사할 수 있는 창
    /// </summary>
    public class DeploySuccessWindow : EditorWindow
    {
        private string deployUrl;
        private bool copied = false;
        private GUIStyle urlStyle;
        private GUIStyle buttonStyle;
        private GUIStyle copiedLabelStyle;

        public static void Show(string url)
        {
            var window = GetWindow<DeploySuccessWindow>(true, "배포 완료", true);
            window.deployUrl = url;
            window.copied = false;
            window.minSize = new Vector2(500, 160);
            window.maxSize = new Vector2(700, 200);
            window.ShowUtility();
            window.CenterOnMainWin();
        }

        private void CenterOnMainWin()
        {
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var pos = position;
            pos.x = mainWindow.x + (mainWindow.width - pos.width) / 2;
            pos.y = mainWindow.y + (mainWindow.height - pos.height) / 2;
            position = pos;
        }

        private void OnGUI()
        {
            // 스타일 초기화
            if (urlStyle == null)
            {
                urlStyle = new GUIStyle(EditorStyles.textField)
                {
                    fontSize = 12,
                    wordWrap = false,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 8, 8)
                };
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 32
                };
            }

            if (copiedLabelStyle == null)
            {
                copiedLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.2f, 0.7f, 0.3f) }
                };
            }

            GUILayout.Space(15);

            // 성공 메시지
            EditorGUILayout.LabelField("Apps in Toss에 배포되었습니다!", EditorStyles.boldLabel);

            GUILayout.Space(10);

            // URL 표시
            EditorGUILayout.LabelField("배포 URL:", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(deployUrl, urlStyle, GUILayout.Height(30));

            GUILayout.Space(10);

            // 버튼 영역
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("URL 복사", buttonStyle, GUILayout.Width(120)))
            {
                EditorGUIUtility.systemCopyBuffer = deployUrl;
                copied = true;
                Debug.Log($"AIT: 배포 URL이 클립보드에 복사되었습니다: {deployUrl}");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(80)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 복사 완료 표시
            if (copied)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("✓ 클립보드에 복사되었습니다", copiedLabelStyle);
            }

            GUILayout.Space(10);
        }
    }
}
