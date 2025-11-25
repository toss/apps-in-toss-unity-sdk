using System;
using System.Diagnostics;
using System.IO;
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
        private static bool isDevServerRunning = false;
        private static Stopwatch buildStopwatch = new Stopwatch();

        // ==================== Dev ====================
        [MenuItem("AIT/Dev", false, 1)]
        public static void StartDev()
        {
            Debug.Log("AIT: Dev 서버 시작...");
            StartDevServer();
        }

        // ==================== Build ====================
        [MenuItem("AIT/Build", false, 2)]
        public static void Build()
        {
            Debug.Log("AIT: WebGL 빌드 시작...");
            ExecuteWebGLBuildOnly();
        }

        // ==================== Package ====================
        [MenuItem("AIT/Package", false, 3)]
        public static void Package()
        {
            Debug.Log("AIT: 패키징 시작...");
            ExecutePackageOnly();
        }

        // ==================== Build & Package ====================
        [MenuItem("AIT/Build & Package", false, 4)]
        public static void BuildAndPackage()
        {
            Debug.Log("AIT: Build & Package 시작...");
            ExecuteBuildAndPackage();
        }

        // ==================== Publish ====================
        [MenuItem("AIT/Publish", false, 5)]
        public static void Publish()
        {
            Debug.Log("AIT: 배포 시작...");
            ExecuteDeploy();
        }

        // ==================== Logs ====================
        [MenuItem("AIT/Logs", false, 101)]
        public static void ShowLogs()
        {
            AITLogsWindow.ShowWindow();
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

            // 빌드 전 검증
            var validationErrors = AITBuildValidator.ValidateBeforeBuild();
            if (validationErrors.Count > 0)
            {
                string errorMessage = AITBuildValidator.FormatValidationErrors(validationErrors);
                Debug.LogError("AIT: 빌드 전 검증 실패:\n" + errorMessage);
                EditorUtility.DisplayDialog("빌드 전 검증 실패", errorMessage, "확인");
                return;
            }

            Debug.Log("AIT: WebGL 빌드 시작...");
            buildStopwatch.Restart();

            var historyEntry = new BuildHistoryEntry
            {
                buildType = "WebGL",
                appVersion = config.version
            };

            try
            {
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: false);
                buildStopwatch.Stop();

                historyEntry.success = (result == AITConvertCore.AITExportError.SUCCEED);
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.Log($"AIT: WebGL 빌드 완료! (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"WebGL 빌드가 완료되었습니다!\n\n소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    historyEntry.errorMessage = result.ToString();
                    Debug.LogError($"AIT: WebGL 빌드 실패: {result}");
                    EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
                }

                AITBuildHistory.AddHistory(historyEntry);
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                historyEntry.success = false;
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;
                historyEntry.errorMessage = e.Message;
                AITBuildHistory.AddHistory(historyEntry);

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

            var historyEntry = new BuildHistoryEntry
            {
                buildType = "Package",
                appVersion = config.version
            };

            try
            {
                var result = AITConvertCore.DoExport(buildWebGL: false, doPackaging: true);
                buildStopwatch.Stop();

                historyEntry.success = (result == AITConvertCore.AITExportError.SUCCEED);
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.Log($"AIT: 패키징 완료! (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"패키징이 완료되었습니다!\n\n소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    historyEntry.errorMessage = result.ToString();
                    Debug.LogError($"AIT: 패키징 실패: {result}");
                    EditorUtility.DisplayDialog("패키징 실패", errorMessage, "확인");
                }

                AITBuildHistory.AddHistory(historyEntry);
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                historyEntry.success = false;
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;
                historyEntry.errorMessage = e.Message;
                AITBuildHistory.AddHistory(historyEntry);

                Debug.LogError($"AIT: 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private static void ExecuteBuildAndPackage()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config)) return;

            // 빌드 전 검증
            var validationErrors = AITBuildValidator.ValidateBeforeBuild();
            if (validationErrors.Count > 0)
            {
                string errorMessage = AITBuildValidator.FormatValidationErrors(validationErrors);
                Debug.LogError("AIT: 빌드 전 검증 실패:\n" + errorMessage);
                EditorUtility.DisplayDialog("빌드 전 검증 실패", errorMessage, "확인");
                return;
            }

            Debug.Log("AIT: 전체 빌드 & 패키징 시작...");
            buildStopwatch.Restart();

            var historyEntry = new BuildHistoryEntry
            {
                buildType = "Full",
                appVersion = config.version
            };

            try
            {
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: true);
                buildStopwatch.Stop();

                historyEntry.success = (result == AITConvertCore.AITExportError.SUCCEED);
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.Log($"AIT: 전체 프로세스 완료! (총 소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"빌드 & 패키징이 완료되었습니다!\n\n총 소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    historyEntry.errorMessage = result.ToString();
                    Debug.LogError($"AIT: 빌드 실패: {result}");
                    EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
                }

                AITBuildHistory.AddHistory(historyEntry);
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                historyEntry.success = false;
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;
                historyEntry.errorMessage = e.Message;
                AITBuildHistory.AddHistory(historyEntry);

                Debug.LogError($"AIT: 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private static void ExecuteDeploy()
        {
            var config = UnityUtil.GetEditorConf();
            if (!ValidateSettings(config)) return;

            // 배포 전 검증
            var validationErrors = AITBuildValidator.ValidateBeforeDeploy();
            if (validationErrors.Count > 0)
            {
                string errorMessage = AITBuildValidator.FormatValidationErrors(validationErrors);
                Debug.LogError("AIT: 배포 전 검증 실패:\n" + errorMessage);
                EditorUtility.DisplayDialog("배포 전 검증 실패", errorMessage, "확인");
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

                // pnpm만 사용 (내장 Node.js + pnpm)
                string pnpxPath = Path.Combine(npmDir, "pnpx");

                string pathEnv = $"{npmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{pnpxPath}' ait deploy --api-key '{config.deploymentKey}'\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = startInfo };

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        Debug.Log($"[Deploy] {args.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        Debug.Log($"[Deploy] {args.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 타임아웃 설정 (5분)
                bool finished = process.WaitForExit(300000);

                if (!finished)
                {
                    process.Kill();
                    Debug.LogError("AIT: 배포 타임아웃 (5분 초과)");
                    EditorUtility.DisplayDialog("타임아웃", "배포 시간이 초과되었습니다.", "확인");
                }
                else if (process.ExitCode == 0)
                {
                    Debug.Log("AIT: 배포 완료!");
                    EditorUtility.DisplayDialog("성공", "Apps in Toss에 배포되었습니다!", "확인");
                }
                else
                {
                    Debug.LogError($"AIT: 배포 실패 (Exit Code: {process.ExitCode})");
                    EditorUtility.DisplayDialog("실패", "배포에 실패했습니다.\n\nConsole 로그를 확인하세요.", "확인");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: 배포 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"배포 오류:\n{e.Message}", "확인");
            }
        }

        private static void StartDevServer()
        {
            var config = UnityUtil.GetEditorConf();
            string buildPath = GetBuildTemplatePath();

            if (!Directory.Exists(buildPath))
            {
                EditorUtility.DisplayDialog("오류", "빌드 폴더를 찾을 수 없습니다. 먼저 빌드를 실행하세요.", "확인");
                return;
            }

            // index.html이 있는지 확인
            string indexPath = Path.Combine(buildPath, "index.html");
            if (!File.Exists(indexPath))
            {
                EditorUtility.DisplayDialog("오류", "index.html을 찾을 수 없습니다. 먼저 빌드를 실행하세요.", "확인");
                return;
            }

            // npm 경로 찾기
            string npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("AIT: npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                EditorUtility.DisplayDialog("오류", "npm을 찾을 수 없습니다.\n\nNode.js가 설치되어 있는지 확인하세요.", "확인");
                return;
            }

            // node_modules가 없으면 npm install 실행
            string nodeModulesPath = Path.Combine(buildPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                Debug.Log("AIT: node_modules가 없습니다. npm install을 실행합니다...");

                string localCachePath = Path.Combine(buildPath, ".npm-cache");
                var installResult = AITConvertCore.RunNpmCommandWithCache(
                    buildPath,
                    npmPath,
                    "install",
                    localCachePath,
                    "npm install 실행 중..."
                );

                if (installResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.LogError("AIT: npm install 실패");
                    EditorUtility.DisplayDialog("오류", "npm install 실패\n\nConsole 로그를 확인하세요.", "확인");
                    return;
                }

                Debug.Log("AIT: npm install 완료");
            }

            // 포트가 이미 사용 중인지 확인하고 종료
            Debug.Log($"AIT: 포트 {config.localPort} 확인 중...");
            KillProcessOnPort(config.localPort);

            // 프로세스 종료 대기
            System.Threading.Thread.Sleep(500);

            Debug.Log($"AIT: Vite 개발 서버 시작 중... ({buildPath})");

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);

                // pnpm만 사용 (내장 Node.js + pnpm)
                string pnpxPath = Path.Combine(npmDir, "pnpx");

                string pathEnv = $"{npmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{pnpxPath}' vite --port {config.localPort} --host\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                devServerProcess = new Process { StartInfo = startInfo };

                devServerProcess.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        Debug.Log($"[Dev Server] {args.Data}");
                    }
                };

                devServerProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        Debug.Log($"[Dev Server] {args.Data}");
                    }
                };

                devServerProcess.Start();
                devServerProcess.BeginOutputReadLine();
                devServerProcess.BeginErrorReadLine();

                isDevServerRunning = true;
                Debug.Log($"AIT: Vite 개발 서버가 시작되었습니다: http://localhost:{config.localPort}");
                Debug.Log($"브라우저에서 http://localhost:{config.localPort} 로 접속하세요");

                // 브라우저에서 자동으로 열기
                Application.OpenURL($"http://localhost:{config.localPort}/index.html");
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: 개발 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"개발 서버 시작 실패:\n{e.Message}\n\nvite가 설치되어 있는지 확인하세요.", "확인");
            }
        }

        private static void KillProcessOnPort(int port)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"lsof -ti :{port} | xargs kill -9 2>/dev/null\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(2000);
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
