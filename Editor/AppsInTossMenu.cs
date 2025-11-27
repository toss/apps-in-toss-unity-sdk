using System;
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
    public class AppsInTossMenu
    {
        private static Process devServerProcess;
        private static Process prodServerProcess;
        private static int devServerPort = 0;
        private static int prodServerPort = 0;
        private static Stopwatch buildStopwatch = new Stopwatch();

        /// <summary>
        /// Dev 서버가 실행 중인지 확인
        /// </summary>
        private static bool IsDevServerRunning
        {
            get
            {
                if (devServerProcess == null) return false;
                try
                {
                    return !devServerProcess.HasExited;
                }
                catch
                {
                    devServerProcess = null;
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
                if (prodServerProcess == null) return false;
                try
                {
                    return !prodServerProcess.HasExited;
                }
                catch
                {
                    prodServerProcess = null;
                    return false;
                }
            }
        }

        // ==================== Dev Server ====================
        [MenuItem("AIT/Dev Server/Start", false, 1)]
        public static void StartDev()
        {
            Debug.Log("AIT: Dev 서버 시작...");
            StartDevServer();
        }

        [MenuItem("AIT/Dev Server/Start", true)]
        public static bool ValidateStartDev()
        {
            return !IsDevServerRunning;
        }

        [MenuItem("AIT/Dev Server/Stop", false, 2)]
        public static void StopDev()
        {
            Debug.Log("AIT: Dev 서버 중지...");
            StopDevServer();
        }

        [MenuItem("AIT/Dev Server/Stop", true)]
        public static bool ValidateStopDev()
        {
            return IsDevServerRunning;
        }

        // ==================== Production Server ====================
        [MenuItem("AIT/Production Server/Start", false, 11)]
        public static void StartProduction()
        {
            Debug.Log("AIT: Production 서버 시작...");
            StartProdServer();
        }

        [MenuItem("AIT/Production Server/Start", true)]
        public static bool ValidateStartProduction()
        {
            return !IsProdServerRunning;
        }

        [MenuItem("AIT/Production Server/Stop", false, 12)]
        public static void StopProduction()
        {
            Debug.Log("AIT: Production 서버 중지...");
            StopProdServer();
        }

        [MenuItem("AIT/Production Server/Stop", true)]
        public static bool ValidateStopProduction()
        {
            return IsProdServerRunning;
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
                EditorUtility.RevealInFinder(buildPath);
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
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: false);
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
                var result = AITConvertCore.DoExport(buildWebGL: false, doPackaging: true);
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
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: true);
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

                // pnpm만 사용 (내장 Node.js + pnpm)
                string pnpxName = AITPlatformHelper.IsWindows ? "pnpx.cmd" : "pnpx";
                string pnpxPath = Path.Combine(npmDir, pnpxName);

                // 크로스 플랫폼 명령 실행
                string command = $"\"{pnpxPath}\" ait deploy --api-key \"{config.deploymentKey}\"";
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
        /// </summary>
        private static void StartDevServer()
        {
            if (IsDevServerRunning)
            {
                Debug.LogWarning("AIT: Dev 서버가 이미 실행 중입니다.");
                return;
            }

            string buildPath = GetBuildTemplatePath();

            if (!ValidateBuildPath(buildPath)) return;

            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath)) return;

            if (!EnsureNodeModules(buildPath, npmPath)) return;

            Debug.Log($"AIT: Dev 서버 시작 중 (granite dev)... ({buildPath})");

            try
            {
                devServerProcess = StartServerProcessWithPortDetection(
                    buildPath, npmPath, "run dev", "Dev Server",
                    (port) =>
                    {
                        devServerPort = port;
                        Debug.Log($"AIT: Dev 서버가 시작되었습니다: http://localhost:{port}");
                        Application.OpenURL($"http://localhost:{port}/index.html");
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Dev 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Dev 서버 시작 실패:\n{e.Message}", "확인");
            }
        }

        /// <summary>
        /// Dev 서버 중지
        /// </summary>
        private static void StopDevServer()
        {
            if (devServerProcess != null && !devServerProcess.HasExited)
            {
                try
                {
                    devServerProcess.Kill();
                    devServerProcess.WaitForExit(2000);
                }
                catch { }
            }
            devServerProcess = null;

            // 감지된 포트에서 실행 중인 프로세스도 종료
            if (devServerPort > 0)
            {
                KillProcessOnPort(devServerPort);
            }

            devServerPort = 0;
            Debug.Log("AIT: Dev 서버가 중지되었습니다.");
        }

        /// <summary>
        /// Production 서버 시작 (vite preview 사용)
        /// </summary>
        private static void StartProdServer()
        {
            if (IsProdServerRunning)
            {
                Debug.LogWarning("AIT: Production 서버가 이미 실행 중입니다.");
                return;
            }

            string buildPath = GetBuildTemplatePath();
            string distPath = Path.Combine(buildPath, "dist");

            if (!ValidateBuildPath(buildPath)) return;

            // dist 폴더 확인 (프로덕션 빌드 결과물)
            if (!Directory.Exists(distPath))
            {
                EditorUtility.DisplayDialog("오류", "dist 폴더를 찾을 수 없습니다.\n\n먼저 'Build & Package'를 실행하세요.", "확인");
                return;
            }

            string npmPath = FindNpmPath();
            if (!ValidateNpmPath(npmPath)) return;

            if (!EnsureNodeModules(buildPath, npmPath)) return;

            Debug.Log($"AIT: Production 서버 시작 중 (vite preview)... ({buildPath})");

            try
            {
                prodServerProcess = StartServerProcessWithPortDetection(
                    buildPath, npmPath, "run start", "Prod Server",
                    (port) =>
                    {
                        prodServerPort = port;
                        Debug.Log($"AIT: Production 서버가 시작되었습니다: http://localhost:{port}");
                        Application.OpenURL($"http://localhost:{port}/");
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: Production 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"Production 서버 시작 실패:\n{e.Message}", "확인");
            }
        }

        /// <summary>
        /// Production 서버 중지
        /// </summary>
        private static void StopProdServer()
        {
            if (prodServerProcess != null && !prodServerProcess.HasExited)
            {
                try
                {
                    prodServerProcess.Kill();
                    prodServerProcess.WaitForExit(2000);
                }
                catch { }
            }
            prodServerProcess = null;

            // 감지된 포트에서 실행 중인 프로세스도 종료
            if (prodServerPort > 0)
            {
                KillProcessOnPort(prodServerPort);
            }

            prodServerPort = 0;
            Debug.Log("AIT: Production 서버가 중지되었습니다.");
        }

        /// <summary>
        /// 서버 프로세스 시작 (동적 포트 감지 포함) - 크로스 플랫폼
        /// </summary>
        /// <param name="onPortDetected">포트가 감지되면 호출되는 콜백 (메인 스레드에서 실행)</param>
        private static Process StartServerProcessWithPortDetection(
            string buildPath,
            string npmPath,
            string npmCommand,
            string logPrefix,
            Action<int> onPortDetected)
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
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["PATH"] = pathEnv;
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{npmPath}' {npmCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            var process = new Process { StartInfo = startInfo };
            bool portDetected = false;

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

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
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
