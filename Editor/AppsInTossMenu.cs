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
        /// <summary>
        /// 서버 상태
        /// </summary>
        private enum ServerStatus
        {
            NotRunning,
            Starting,
            Running,
            Stopping
        }

        private static AITProcessTreeManager devServerManager;
        private static AITProcessTreeManager prodServerManager;
        private static int devServerPort = 0;
        private static int prodServerPort = 0;
        private static ServerStatus devServerStatus = ServerStatus.NotRunning;
        private static ServerStatus prodServerStatus = ServerStatus.NotRunning;
        private static Stopwatch buildStopwatch = new Stopwatch();

        private const string DEV_SERVER_PID_KEY = "AIT_DevServerPID";
        private const string DEV_SERVER_PORT_KEY = "AIT_DevServerPort";
        private const string PROD_SERVER_PID_KEY = "AIT_ProdServerPID";
        private const string PROD_SERVER_PORT_KEY = "AIT_ProdServerPort";

        /// <summary>
        /// 도메인 리로드 시 기존 서버 프로세스 복원 및 종료 이벤트 등록
        /// </summary>
        static AppsInTossMenu()
        {
            RestoreServerProcesses();
            EditorApplication.quitting += OnEditorQuitting;
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesChanged;
        }

        private static void RestoreServerProcesses()
        {
            // Dev 서버 복원
            int devPid = EditorPrefs.GetInt(DEV_SERVER_PID_KEY, 0);
            int savedDevPort = EditorPrefs.GetInt(DEV_SERVER_PORT_KEY, 0);
            if (devPid > 0)
            {
                devServerManager = new AITProcessTreeManager();
                if (devServerManager.RestoreFromPid(devPid))
                {
                    devServerPort = savedDevPort;
                    devServerStatus = ServerStatus.Running;
                    Debug.Log($"[AIT] Dev 서버 프로세스 복원됨 (PID: {devPid}, Port: {savedDevPort})");
                }
                else
                {
                    devServerManager = null;
                    ClearDevServerPrefs();
                    devServerStatus = ServerStatus.NotRunning;
                }
            }

            // Prod 서버 복원
            int prodPid = EditorPrefs.GetInt(PROD_SERVER_PID_KEY, 0);
            int savedProdPort = EditorPrefs.GetInt(PROD_SERVER_PORT_KEY, 0);
            if (prodPid > 0)
            {
                prodServerManager = new AITProcessTreeManager();
                if (prodServerManager.RestoreFromPid(prodPid))
                {
                    prodServerPort = savedProdPort;
                    prodServerStatus = ServerStatus.Running;
                    Debug.Log($"[AIT] Prod 서버 프로세스 복원됨 (PID: {prodPid}, Port: {savedProdPort})");
                }
                else
                {
                    prodServerManager = null;
                    ClearProdServerPrefs();
                    prodServerStatus = ServerStatus.NotRunning;
                }
            }
        }

        private static void SaveDevServerPrefs(int pid, int port)
        {
            EditorPrefs.SetInt(DEV_SERVER_PID_KEY, pid);
            EditorPrefs.SetInt(DEV_SERVER_PORT_KEY, port);
        }

        private static void ClearDevServerPrefs()
        {
            EditorPrefs.DeleteKey(DEV_SERVER_PID_KEY);
            EditorPrefs.DeleteKey(DEV_SERVER_PORT_KEY);
        }

        private static void SaveProdServerPrefs(int pid, int port)
        {
            EditorPrefs.SetInt(PROD_SERVER_PID_KEY, pid);
            EditorPrefs.SetInt(PROD_SERVER_PORT_KEY, port);
        }

        private static void ClearProdServerPrefs()
        {
            EditorPrefs.DeleteKey(PROD_SERVER_PID_KEY);
            EditorPrefs.DeleteKey(PROD_SERVER_PORT_KEY);
        }

        /// <summary>
        /// Unity Editor 종료 시 모든 서버 프로세스 정리
        /// </summary>
        private static void OnEditorQuitting()
        {
            bool hadRunningServers = IsDevServerRunning || IsProdServerRunning;

            if (IsDevServerRunning)
            {
                Debug.Log("[AIT] Editor 종료 - Dev 서버 프로세스 정리 중...");
                StopDevServer();
            }

            if (IsProdServerRunning)
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

                    if (IsDevServerRunning)
                    {
                        StopDevServer();
                    }

                    if (IsProdServerRunning)
                    {
                        StopProdServer();
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Dev 서버가 실행 중인지 확인
        /// </summary>
        private static bool IsDevServerRunning
        {
            get
            {
                if (devServerManager == null) return false;
                try
                {
                    return !devServerManager.HasExited;
                }
                catch
                {
                    devServerManager = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Production 서버가 실행 중인지 확인
        /// </summary>
        private static bool IsProdServerRunning
        {
            get
            {
                if (prodServerManager == null) return false;
                try
                {
                    return !prodServerManager.HasExited;
                }
                catch
                {
                    prodServerManager = null;
                    return false;
                }
            }
        }

        // ==================== Dev Server ====================

        [MenuItem("AIT/Dev Server/Start Server", false, 1)]
        public static void MenuStartDevServer()
        {
            Debug.Log("AIT: Dev 서버 시작...");
            StartDevServer();
        }

        [MenuItem("AIT/Dev Server/Start Server", true)]
        public static bool ValidateMenuStartDevServer()
        {
            return devServerStatus == ServerStatus.NotRunning;
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
            return devServerStatus == ServerStatus.Running || devServerStatus == ServerStatus.Starting;
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
            return devServerStatus == ServerStatus.Running;
        }

        // ==================== Production Server ====================

        [MenuItem("AIT/Production Server/Start Server", false, 11)]
        public static void MenuStartProdServer()
        {
            Debug.Log("AIT: Production 서버 시작...");
            StartProdServer();
        }

        [MenuItem("AIT/Production Server/Start Server", true)]
        public static bool ValidateMenuStartProdServer()
        {
            return prodServerStatus == ServerStatus.NotRunning;
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
            return prodServerStatus == ServerStatus.Running || prodServerStatus == ServerStatus.Starting;
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
            return prodServerStatus == ServerStatus.Running;
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
            Debug.Log("AIT: 배포 시작...");
            ExecuteDeploy();
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
                    EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
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
            if (!ValidateSettings(config)) return;

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
                    EditorUtility.DisplayDialog("패키징 실패", errorMessage, "확인");
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
            if (!ValidateSettings(config)) return;

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
                    EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
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
            if (!ValidateSettings(config)) return;

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

            bool confirmed = EditorUtility.DisplayDialog(
                "배포 확인",
                $"Apps in Toss에 배포하시겠습니까?\n\n프로젝트: {config.appName}\n버전: {config.version}",
                "배포",
                "취소"
            );

            if (!confirmed) return;

            Debug.Log("AIT: Apps in Toss 배포 시작...");

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);

                // pnpm run deploy를 사용하여 로컬 node_modules/.bin/ait 사용
                string pnpmName = AITPlatformHelper.IsWindows ? "pnpm.cmd" : "pnpm";
                string pnpmPath = Path.Combine(npmDir, pnpmName);

                // pnpm run deploy -- --api-key "KEY" 형태로 인자 전달 (로컬 node_modules 사용)
                string command = $"\"{pnpmPath}\" run deploy -- --api-key \"{deploymentKey}\"";
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
                        Debug.LogError($"AIT: 배포 실패 (Exit Code: {result.ExitCode})");
                        EditorUtility.DisplayDialog("실패", "배포에 실패했습니다.\n\nConsole 로그를 확인하세요.", "확인");
                    }
                }
                else
                {
                    Debug.Log("AIT: 배포 완료!");
                    EditorUtility.DisplayDialog("성공", "Apps in Toss에 배포되었습니다!", "확인");
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
            if (IsDevServerRunning)
            {
                Debug.LogWarning("AIT: Dev 서버가 이미 실행 중입니다.");
                return;
            }

            // Production 서버가 실행 중이면 확인 후 전환
            if (IsProdServerRunning)
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

            devServerStatus = ServerStatus.Starting;

            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config))
            {
                devServerStatus = ServerStatus.NotRunning;
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
                EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
                devServerStatus = ServerStatus.NotRunning;
                return;
            }

            Debug.Log($"AIT: 빌드 & 패키징 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");

            string buildPath = GetBuildTemplatePath();
            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath))
            {
                devServerStatus = ServerStatus.NotRunning;
                return;
            }

            if (!EnsureNodeModules(buildPath, npmPath))
            {
                devServerStatus = ServerStatus.NotRunning;
                return;
            }

            // 서버 설정
            string graniteHost = !string.IsNullOrEmpty(config.graniteHost) ? config.graniteHost : "localhost";
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
                string graniteCommand = $"exec granite dev --host {graniteHost} --port {granitePort}";

                devServerManager = new AITProcessTreeManager();
                StartServerProcessWithPortDetection(
                    devServerManager,
                    buildPath, npmPath, graniteCommand, "Dev Server", envVars,
                    (port) =>
                    {
                        devServerPort = port;
                        devServerStatus = ServerStatus.Running;
                        SaveDevServerPrefs(devServerManager.ProcessId, port);
                        Debug.Log($"AIT: Dev 서버가 시작되었습니다: http://localhost:{port}");
                        Application.OpenURL($"http://localhost:{port}/index.html");
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Dev 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Dev 서버 시작 실패:\n{e.Message}", "확인");
                devServerManager = null;
                devServerStatus = ServerStatus.NotRunning;
            }
        }

        /// <summary>
        /// Dev 서버 중지
        /// </summary>
        private static void StopDevServer()
        {
            devServerStatus = ServerStatus.Stopping;

            // AITProcessTreeManager로 프로세스 트리 전체 종료
            if (devServerManager != null)
            {
                try
                {
                    devServerManager.KillProcessTree();
                }
                catch { }
                devServerManager = null;
            }

            // 백업: 포트에서 실행 중인 프로세스도 종료 (혹시 남아있는 경우)
            if (devServerPort > 0)
            {
                KillProcessOnPort(devServerPort);
            }

            devServerPort = 0;
            ClearDevServerPrefs();
            devServerStatus = ServerStatus.NotRunning;
            Debug.Log("AIT: Dev 서버가 중지되었습니다.");
        }

        /// <summary>
        /// Production 서버 시작 (vite preview 사용)
        /// 자동으로 빌드 & 패키징 수행 후 서버 시작
        /// </summary>
        private static void StartProdServer()
        {
            if (IsProdServerRunning)
            {
                Debug.LogWarning("AIT: Production 서버가 이미 실행 중입니다.");
                return;
            }

            // Dev 서버가 실행 중이면 확인 후 전환
            if (IsDevServerRunning)
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

            prodServerStatus = ServerStatus.Starting;

            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config))
            {
                prodServerStatus = ServerStatus.NotRunning;
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
                EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
                prodServerStatus = ServerStatus.NotRunning;
                return;
            }

            Debug.Log($"AIT: 빌드 & 패키징 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");

            string buildPath = GetBuildTemplatePath();
            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath))
            {
                prodServerStatus = ServerStatus.NotRunning;
                return;
            }

            if (!EnsureNodeModules(buildPath, npmPath))
            {
                prodServerStatus = ServerStatus.NotRunning;
                return;
            }

            // 서버 설정 (Dev 서버와 동일한 Vite 포트 사용)
            string viteHost = !string.IsNullOrEmpty(config.viteHost) ? config.viteHost : "localhost";
            int vitePort = config.vitePort > 0 ? config.vitePort : 5173;

            Debug.Log($"AIT: Production 서버 시작 중 (vite preview)... ({buildPath})");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");

            try
            {
                // 환경 변수로 Vite 설정 전달
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", vitePort.ToString() }
                };

                prodServerManager = new AITProcessTreeManager();
                StartServerProcessWithPortDetection(
                    prodServerManager,
                    buildPath, npmPath, $"exec vite preview --outDir dist/web --host {viteHost} --port {vitePort}", "Prod Server", envVars,
                    (port) =>
                    {
                        prodServerPort = port;
                        prodServerStatus = ServerStatus.Running;
                        SaveProdServerPrefs(prodServerManager.ProcessId, port);
                        Debug.Log($"AIT: Production 서버가 시작되었습니다: http://localhost:{port}");
                        Application.OpenURL($"http://localhost:{port}/");
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Production 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Production 서버 시작 실패:\n{e.Message}", "확인");
                prodServerManager = null;
                prodServerStatus = ServerStatus.NotRunning;
            }
        }

        /// <summary>
        /// Production 서버 중지
        /// </summary>
        private static void StopProdServer()
        {
            prodServerStatus = ServerStatus.Stopping;

            // AITProcessTreeManager로 프로세스 트리 전체 종료
            if (prodServerManager != null)
            {
                try
                {
                    prodServerManager.KillProcessTree();
                }
                catch { }
                prodServerManager = null;
            }

            // 백업: 포트에서 실행 중인 프로세스도 종료 (혹시 남아있는 경우)
            if (prodServerPort > 0)
            {
                KillProcessOnPort(prodServerPort);
            }

            prodServerPort = 0;
            ClearProdServerPrefs();
            prodServerStatus = ServerStatus.NotRunning;
            Debug.Log("AIT: Production 서버가 중지되었습니다.");
        }

        /// <summary>
        /// 서버 프로세스 시작 (동적 포트 감지 포함) - 크로스 플랫폼
        /// AITProcessTreeManager를 사용하여 프로세스 트리 전체를 관리
        /// </summary>
        /// <param name="manager">프로세스 트리 관리자</param>
        /// <param name="envVars">환경 변수 (AIT_GRANITE_HOST, AIT_GRANITE_PORT, AIT_VITE_PORT 등)</param>
        /// <param name="onPortDetected">포트가 감지되면 호출되는 콜백 (메인 스레드에서 실행)</param>
        private static void StartServerProcessWithPortDetection(
            AITProcessTreeManager manager,
            string buildPath,
            string npmPath,
            string npmCommand,
            string logPrefix,
            Dictionary<string, string> envVars,
            Action<int> onPortDetected)
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

            bool portDetected = false;

            // AITProcessTreeManager를 통해 프로세스 시작 (프로세스 그룹 관리)
            var process = manager.StartProcess(startInfo);

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Debug.Log($"[{logPrefix}] {args.Data}");

                    // 포트 감지 (ANSI 코드 제거 후 파싱)
                    if (!portDetected)
                    {
                        string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");
                        var portMatch = Regex.Match(cleanOutput, @"localhost:(\d+)");
                        if (portMatch.Success)
                        {
                            int port = int.Parse(portMatch.Groups[1].Value);
                            portDetected = true;

                            // Unity 메인 스레드에서 콜백 실행
                            EditorApplication.delayCall += () => onPortDetected?.Invoke(port);
                        }
                    }
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Debug.Log($"[{logPrefix}] {args.Data}");
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
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
                var installResult = AITConvertCore.RunNpmCommandWithCache(
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

        private static bool ValidateSettings(AITEditorScriptObject config)
        {
            if (config == null)
            {
                EditorUtility.DisplayDialog("오류", "설정을 찾을 수 없습니다.", "확인");
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
}
