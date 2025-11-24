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
    /// Apps in Toss 빌드 & 배포 도구 (통합 버전)
    /// </summary>
    public class AppsInTossBuildWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private AITEditorScriptObject config;
        private static Process devServerProcess;
        private static bool isDevServerRunning = false;
        private string buildLog = "";
        private bool isBuildingStep1 = false;
        private bool isBuildingStep2 = false;
        private bool isBuildingStep3 = false;

        // Settings 섹션 접기/펴기
        private bool showSettings = true;

        // 빌드 시간 측정
        private System.Diagnostics.Stopwatch buildStopwatch = new System.Diagnostics.Stopwatch();

        [MenuItem("Apps in Toss/Build & Deploy Window", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<AppsInTossBuildWindow>("Apps in Toss Build & Deploy");
            window.minSize = new Vector2(500, 800);
            window.Show();
        }

        private void OnEnable()
        {
            config = UnityUtil.GetEditorConf();
            EditorApplication.update += CheckDevServerStatus;
        }

        private void OnDisable()
        {
            EditorApplication.update -= CheckDevServerStatus;
            SaveSettings();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);
            DrawHeader();
            GUILayout.Space(10);
            DrawSettings(); // Settings 통합
            GUILayout.Space(10);
            DrawBuildInfo();
            GUILayout.Space(10);
            DrawActionButtons();
            GUILayout.Space(10);
            DrawBuildLog();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Label("Apps in Toss Build & Deploy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Unity 게임을 Apps in Toss 미니앱으로 빌드하고 배포할 수 있습니다.",
                MessageType.Info
            );
        }

        private void DrawSettings()
        {
            // Settings 섹션 (접기/펴기 가능)
            showSettings = EditorGUILayout.Foldout(showSettings, "⚙️ 설정", true, EditorStyles.foldoutHeader);

            if (showSettings)
            {
                EditorGUILayout.BeginVertical("box");

                // 앱 기본 정보
                EditorGUILayout.LabelField("앱 기본 정보", EditorStyles.boldLabel);

                // 앱 ID (검증 포함)
                config.appName = EditorGUILayout.TextField("앱 ID", config.appName);
                if (!string.IsNullOrWhiteSpace(config.appName) && !config.IsAppNameValid())
                {
                    EditorGUILayout.HelpBox("앱 ID는 영문, 숫자, 하이픈(-)만 사용할 수 있습니다.", MessageType.Warning);
                }

                config.displayName = EditorGUILayout.TextField("표시 이름", config.displayName);

                // 버전 (검증 포함)
                config.version = EditorGUILayout.TextField("버전", config.version);
                if (!string.IsNullOrWhiteSpace(config.version) && !config.IsVersionValid())
                {
                    EditorGUILayout.HelpBox("버전은 x.y.z 형식이어야 합니다. (예: 1.0.0)", MessageType.Warning);
                }

                config.description = EditorGUILayout.TextArea(config.description, GUILayout.Height(60));

                GUILayout.Space(10);

                // 브랜드 설정
                EditorGUILayout.LabelField("브랜드 설정", EditorStyles.boldLabel);
                config.primaryColor = EditorGUILayout.TextField("기본 색상", config.primaryColor);
                config.iconUrl = EditorGUILayout.TextField("아이콘 URL (필수)", config.iconUrl);

                // 아이콘 URL 검증
                if (string.IsNullOrWhiteSpace(config.iconUrl))
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ 아이콘 URL을 입력해주세요. 빌드 시 필수입니다.\n예: https://your-domain.com/icon.png",
                        MessageType.Warning
                    );
                }
                else if (!config.IsIconUrlValid())
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ 아이콘 URL은 http:// 또는 https://로 시작해야 합니다.",
                        MessageType.Error
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox("✓ 아이콘 URL이 올바른 형식입니다.", MessageType.Info);
                }

                GUILayout.Space(10);

                // 개발 서버 설정
                EditorGUILayout.LabelField("개발 서버 설정", EditorStyles.boldLabel);
                config.localPort = EditorGUILayout.IntField("로컬 포트", config.localPort);

                GUILayout.Space(10);

                // 빌드 설정
                EditorGUILayout.LabelField("빌드 설정", EditorStyles.boldLabel);
                config.isProduction = EditorGUILayout.Toggle("프로덕션 모드", config.isProduction);
                config.enableOptimization = EditorGUILayout.Toggle("최적화 활성화", config.enableOptimization);

                EditorGUILayout.HelpBox(
                    "Compression Format은 자동으로 Disabled로 설정됩니다 (Apps in Toss 권장)",
                    MessageType.Info
                );

                GUILayout.Space(10);

                // 광고 설정
                EditorGUILayout.LabelField("광고 설정 (선택)", EditorStyles.boldLabel);
                config.enableAdvertisement = EditorGUILayout.Toggle("광고 활성화", config.enableAdvertisement);
                if (config.enableAdvertisement)
                {
                    EditorGUI.indentLevel++;
                    config.interstitialAdGroupId = EditorGUILayout.TextField("전면 광고 ID", config.interstitialAdGroupId);
                    config.rewardedAdGroupId = EditorGUILayout.TextField("보상형 광고 ID", config.rewardedAdGroupId);
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(10);

                // 배포 설정
                EditorGUILayout.LabelField("배포 설정", EditorStyles.boldLabel);
                config.deploymentKey = EditorGUILayout.PasswordField("배포 키 (API Key)", config.deploymentKey);

                if (string.IsNullOrWhiteSpace(config.deploymentKey))
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ 배포 키를 입력해주세요. 배포 시 필수입니다.",
                        MessageType.Warning
                    );
                }

                EditorGUILayout.EndVertical();

                if (GUI.changed)
                {
                    SaveSettings();
                }
            }
        }

        private void SaveSettings()
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawBuildInfo()
        {
            EditorGUILayout.LabelField("📊 프로젝트 정보", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("프로젝트 이름:", PlayerSettings.productName);
            EditorGUILayout.LabelField("Unity 버전:", Application.unityVersion);
            EditorGUILayout.LabelField("앱 이름:", config.appName);
            EditorGUILayout.LabelField("버전:", config.version);

            string buildPath = GetBuildTemplatePath();
            bool hasBuild = Directory.Exists(buildPath);
            EditorGUILayout.LabelField("빌드 상태:", hasBuild ? "빌드 완료" : "빌드 필요");

            GUILayout.Space(5);

            // 설정 검증 상태 요약
            bool readyForBuild = config.IsIconUrlValid() && config.IsAppNameValid() && config.IsVersionValid();
            bool readyForDeploy = config.IsReadyForDeploy();

            if (readyForBuild)
            {
                EditorGUILayout.HelpBox("✓ 빌드 준비 완료", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ 설정을 완료해주세요 (아이콘 URL, 앱 ID, 버전)", MessageType.Warning);
            }

            if (hasBuild && readyForDeploy)
            {
                EditorGUILayout.HelpBox("✓ 배포 준비 완료", MessageType.Info);
            }
            else if (hasBuild && !readyForDeploy)
            {
                EditorGUILayout.HelpBox("⚠ 배포 키를 입력해주세요", MessageType.Warning);
            }

            GUILayout.Space(5);

            // 빌드 통계
            var stats = AITBuildHistory.GetStatistics();
            if (stats.totalBuilds > 0)
            {
                EditorGUILayout.LabelField("빌드 통계:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  총 빌드: {stats.totalBuilds}회");
                EditorGUILayout.LabelField($"  성공률: {stats.SuccessRate:F1}% ({stats.successfulBuilds}성공/{stats.failedBuilds}실패)");
                EditorGUILayout.LabelField($"  평균 시간: {stats.averageBuildTime:F1}초");

                if (GUILayout.Button("빌드 히스토리 초기화", GUILayout.Height(20)))
                {
                    if (EditorUtility.DisplayDialog("히스토리 초기화", "모든 빌드 히스토리를 삭제하시겠습니까?", "삭제", "취소"))
                    {
                        AITBuildHistory.ClearHistory();
                        AppendLog("빌드 히스토리가 초기화되었습니다.");
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("🚀 작업", EditorStyles.boldLabel);

            // 빌드 중일 때 취소 버튼 표시
            bool isAnyBuildRunning = isBuildingStep1 || isBuildingStep2 || isBuildingStep3;
            if (isAnyBuildRunning)
            {
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("⛔ Cancel Build", GUILayout.Height(50)))
                {
                    AITConvertCore.CancelBuild();
                    AppendLog("빌드 취소 요청됨...");
                }
                GUI.backgroundColor = Color.white;
                GUILayout.Space(10);
            }

            EditorGUI.BeginDisabledGroup(isBuildingStep1 || isBuildingStep2 || isBuildingStep3);

            // WebGL 빌드만
            if (GUILayout.Button("🔨 WebGL Build Only", GUILayout.Height(40)))
            {
                ExecuteWebGLBuildOnly();
            }

            // 패키징만
            bool hasWebGLBuild = CheckWebGLBuildExists();
            EditorGUI.BeginDisabledGroup(!hasWebGLBuild);
            if (GUILayout.Button("📦 Package Only", GUILayout.Height(40)))
            {
                ExecutePackageOnly();
            }
            EditorGUI.EndDisabledGroup();

            // Build & Package (통합)
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("🚀 Build & Package", GUILayout.Height(50)))
            {
                ExecuteBuildAndPackage();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // 배포
            EditorGUILayout.LabelField("🚀 배포", EditorStyles.boldLabel);

            bool hasBuildOutput = Directory.Exists(Path.Combine(GetBuildTemplatePath(), "dist"));
            bool hasDeploymentKey = !string.IsNullOrWhiteSpace(config.deploymentKey);

            EditorGUI.BeginDisabledGroup(!hasBuildOutput || !hasDeploymentKey);
            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.2f);
            if (GUILayout.Button("📤 Deploy to Apps in Toss", GUILayout.Height(50)))
            {
                ExecuteDeploy();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            if (!hasBuildOutput)
            {
                EditorGUILayout.HelpBox("먼저 빌드를 완료해주세요.", MessageType.Warning);
            }
            else if (!hasDeploymentKey)
            {
                EditorGUILayout.HelpBox("배포 키를 입력해주세요 (설정 섹션)", MessageType.Warning);
            }

            GUILayout.Space(10);

            // 개발 서버
            EditorGUILayout.LabelField("💻 개발 서버", EditorStyles.boldLabel);

            bool hasBuildFolder = Directory.Exists(GetBuildTemplatePath());
            EditorGUI.BeginDisabledGroup(!hasBuildFolder);

            if (!isDevServerRunning)
            {
                GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
                if (GUILayout.Button("▶️ Start Dev Server", GUILayout.Height(40)))
                {
                    StartDevServer();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // 브라우저에서 열기 버튼
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);
                if (GUILayout.Button("🌐 브라우저에서 열기", GUILayout.Height(40)))
                {
                    OpenInBrowser();
                }
                GUI.backgroundColor = Color.white;

                GUILayout.Space(5);

                // 서버 중지 버튼
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("⏹️ Stop Dev Server", GUILayout.Height(40)))
                {
                    StopDevServer();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    $"개발 서버 실행 중: http://localhost:{config.localPort}/index.html",
                    MessageType.Info
                );
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            // 빌드 폴더 열기
            EditorGUI.BeginDisabledGroup(!hasBuildFolder);
            if (GUILayout.Button("📂 Open Build Folder", GUILayout.Height(35)))
            {
                OpenBuildFolder();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawBuildLog()
        {
            EditorGUILayout.LabelField("📝 빌드 로그", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.TextArea(buildLog, GUILayout.Height(200));
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("로그 지우기"))
            {
                buildLog = "";
            }
        }

        private void AppendLog(string message)
        {
            buildLog += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            Repaint();
        }

        // ============================================
        // 빌드 실행 메서드들
        // ============================================

        private void ExecuteWebGLBuildOnly()
        {
            if (!ValidateSettings()) return;

            // 빌드 전 검증
            var validationErrors = AITBuildValidator.ValidateBeforeBuild();
            if (validationErrors.Count > 0)
            {
                string errorMessage = AITBuildValidator.FormatValidationErrors(validationErrors);
                AppendLog("✗ 빌드 전 검증 실패:");
                foreach (var error in validationErrors)
                {
                    AppendLog($"  - {error}");
                }
                EditorUtility.DisplayDialog("빌드 전 검증 실패", errorMessage, "확인");
                return;
            }

            AppendLog("WebGL 빌드 시작...");
            isBuildingStep1 = true;
            buildStopwatch.Restart();

            // 빌드 히스토리 항목 생성
            var historyEntry = new BuildHistoryEntry
            {
                buildType = "WebGL",
                appVersion = config.version
            };

            try
            {
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: false);
                buildStopwatch.Stop();
                isBuildingStep1 = false;

                historyEntry.success = (result == AITConvertCore.AITExportError.SUCCEED);
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    AppendLog($"✓ WebGL 빌드 완료! (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"WebGL 빌드가 완료되었습니다!\n\n소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    historyEntry.errorMessage = result.ToString();
                    AppendLog($"✗ WebGL 빌드 실패: {result}");
                    EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
                }

                // 빌드 히스토리 저장
                AITBuildHistory.AddHistory(historyEntry);
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                isBuildingStep1 = false;
                historyEntry.success = false;
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;
                historyEntry.errorMessage = e.Message;
                AITBuildHistory.AddHistory(historyEntry);

                AppendLog($"✗ 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private void ExecutePackageOnly()
        {
            if (!ValidateSettings()) return;

            AppendLog("패키징 시작...");
            isBuildingStep2 = true;
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
                isBuildingStep2 = false;

                historyEntry.success = (result == AITConvertCore.AITExportError.SUCCEED);
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    AppendLog($"✓ 패키징 완료! (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"패키징이 완료되었습니다!\n\n소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    historyEntry.errorMessage = result.ToString();
                    AppendLog($"✗ 패키징 실패: {result}");
                    EditorUtility.DisplayDialog("패키징 실패", errorMessage, "확인");
                }

                // 빌드 히스토리 저장
                AITBuildHistory.AddHistory(historyEntry);
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                isBuildingStep2 = false;
                historyEntry.success = false;
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;
                historyEntry.errorMessage = e.Message;
                AITBuildHistory.AddHistory(historyEntry);

                AppendLog($"✗ 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private void ExecuteBuildAndPackage()
        {
            if (!ValidateSettings()) return;

            // 빌드 전 검증
            var validationErrors = AITBuildValidator.ValidateBeforeBuild();
            if (validationErrors.Count > 0)
            {
                string errorMessage = AITBuildValidator.FormatValidationErrors(validationErrors);
                AppendLog("✗ 빌드 전 검증 실패:");
                foreach (var error in validationErrors)
                {
                    AppendLog($"  - {error}");
                }
                EditorUtility.DisplayDialog("빌드 전 검증 실패", errorMessage, "확인");
                return;
            }

            AppendLog("전체 빌드 & 패키징 시작...");
            isBuildingStep1 = true;
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
                isBuildingStep1 = false;

                historyEntry.success = (result == AITConvertCore.AITExportError.SUCCEED);
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;

                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    AppendLog($"✓ 전체 프로세스 완료! (총 소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                    EditorUtility.DisplayDialog("성공", $"빌드 & 패키징이 완료되었습니다!\n\n총 소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                }
                else
                {
                    string errorMessage = AITConvertCore.GetErrorMessage(result);
                    historyEntry.errorMessage = result.ToString();
                    AppendLog($"✗ 빌드 실패: {result}");
                    EditorUtility.DisplayDialog("빌드 실패", errorMessage, "확인");
                }

                // 빌드 히스토리 저장
                AITBuildHistory.AddHistory(historyEntry);
            }
            catch (Exception e)
            {
                buildStopwatch.Stop();
                isBuildingStep1 = false;
                historyEntry.success = false;
                historyEntry.buildTimeSeconds = (float)buildStopwatch.Elapsed.TotalSeconds;
                historyEntry.errorMessage = e.Message;
                AITBuildHistory.AddHistory(historyEntry);

                AppendLog($"✗ 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", e.Message, "확인");
            }
        }

        private void ExecuteDeploy()
        {
            if (!ValidateSettings()) return;

            // 배포 전 검증
            var validationErrors = AITBuildValidator.ValidateBeforeDeploy();
            if (validationErrors.Count > 0)
            {
                string errorMessage = AITBuildValidator.FormatValidationErrors(validationErrors);
                AppendLog("✗ 배포 전 검증 실패:");
                foreach (var error in validationErrors)
                {
                    AppendLog($"  - {error}");
                }
                EditorUtility.DisplayDialog("배포 전 검증 실패", errorMessage, "확인");
                return;
            }

            string buildPath = GetBuildTemplatePath();
            string distPath = Path.Combine(buildPath, "dist");

            // npm 경로 찾기
            string npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                AppendLog("✗ npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
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

            AppendLog($"Apps in Toss 배포 시작...");
            isBuildingStep3 = true;

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);
                string npxPath = Path.Combine(npmDir, "npx");
                string pathEnv = $"{npmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{npxPath}' ait deploy --api-key '{config.deploymentKey}'\"",
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
                        AppendLog($"[Deploy] {args.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        AppendLog($"[Deploy] {args.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 타임아웃 설정 (5분)
                bool finished = process.WaitForExit(300000);

                isBuildingStep3 = false;

                if (!finished)
                {
                    process.Kill();
                    AppendLog("✗ 배포 타임아웃 (5분 초과)");
                    EditorUtility.DisplayDialog("타임아웃", "배포 시간이 초과되었습니다.", "확인");
                }
                else if (process.ExitCode == 0)
                {
                    AppendLog("✓ 배포 완료!");
                    EditorUtility.DisplayDialog("성공", "Apps in Toss에 배포되었습니다!", "확인");
                }
                else
                {
                    AppendLog($"✗ 배포 실패 (Exit Code: {process.ExitCode})");
                    EditorUtility.DisplayDialog("실패", "배포에 실패했습니다.\n\n로그를 확인하세요.", "확인");
                }
            }
            catch (Exception e)
            {
                isBuildingStep3 = false;
                AppendLog($"✗ 배포 오류: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"배포 오류:\n{e.Message}", "확인");
            }
        }

        private void OpenBuildFolder()
        {
            string buildPath = GetBuildTemplatePath();
            if (Directory.Exists(buildPath))
            {
                EditorUtility.RevealInFinder(buildPath);
                AppendLog($"빌드 폴더 열기: {buildPath}");
            }
        }

        private void StartDevServer()
        {
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
                AppendLog("✗ npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                EditorUtility.DisplayDialog("오류", "npm을 찾을 수 없습니다.\n\nNode.js가 설치되어 있는지 확인하세요.", "확인");
                return;
            }

            // 포트가 이미 사용 중인지 확인하고 종료
            AppendLog($"포트 {config.localPort} 확인 중...");
            KillProcessOnPort(config.localPort);

            // 프로세스 종료 대기
            System.Threading.Thread.Sleep(500);

            AppendLog($"Vite 개발 서버 시작 중... ({buildPath})");

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);
                string npxPath = Path.Combine(npmDir, "npx");
                string pathEnv = $"{npmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";

                // Vite 개발 서버로 변경 (public/ 폴더를 루트로 서빙)
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{npxPath}' vite --port {config.localPort} --host\"",
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
                        AppendLog($"[Dev Server] {args.Data}");
                    }
                };

                devServerProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        AppendLog($"[Dev Server] {args.Data}");
                    }
                };

                devServerProcess.Start();
                devServerProcess.BeginOutputReadLine();
                devServerProcess.BeginErrorReadLine();

                isDevServerRunning = true;
                AppendLog($"✓ Vite 개발 서버가 시작되었습니다: http://localhost:{config.localPort}");
                AppendLog($"  브라우저에서 http://localhost:{config.localPort} 로 접속하세요");
                AppendLog($"  (Vite는 public/ 폴더의 파일을 루트로 서빙합니다)");
            }
            catch (Exception e)
            {
                AppendLog($"✗ 개발 서버 시작 실패: {e.Message}");
                EditorUtility.DisplayDialog("오류", $"개발 서버 시작 실패:\n{e.Message}\n\nnpx vite가 설치되어 있는지 확인하세요.", "확인");
            }
        }

        private void StopDevServer()
        {
            try
            {
                // 1. 프로세스 종료
                if (devServerProcess != null && !devServerProcess.HasExited)
                {
                    devServerProcess.Kill();
                    devServerProcess.WaitForExit(1000);
                }

                // 2. 포트를 점유하는 프로세스 강제 종료 (확실하게)
                KillProcessOnPort(config.localPort);

                devServerProcess = null;
                isDevServerRunning = false;
                AppendLog("✓ 개발 서버가 중지되었습니다.");
            }
            catch (Exception e)
            {
                AppendLog($"✗ 개발 서버 중지 실패: {e.Message}");
            }
        }

        private void KillProcessOnPort(int port)
        {
            try
            {
                // lsof로 포트 사용 중인 프로세스 찾기
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

        private void OpenInBrowser()
        {
            string url = $"http://localhost:{config.localPort}/index.html";
            Application.OpenURL(url);
            AppendLog($"브라우저 열기: {url}");
        }

        // ============================================
        // 유틸리티 메서드들
        // ============================================

        private bool ValidateSettings()
        {
            if (config == null)
            {
                EditorUtility.DisplayDialog("오류", "설정을 찾을 수 없습니다.", "확인");
                return false;
            }

            return true;
        }

        private bool CheckWebGLBuildExists()
        {
            string projectPath = UnityUtil.GetProjectPath();
            string webglPath = Path.Combine(projectPath, "webgl");
            string buildPath = Path.Combine(webglPath, "Build");
            return Directory.Exists(buildPath);
        }

        private string GetBuildTemplatePath()
        {
            string projectPath = UnityUtil.GetProjectPath();
            return Path.Combine(projectPath, "ait-build");
        }

        private void CheckDevServerStatus()
        {
            if (isDevServerRunning && (devServerProcess == null || devServerProcess.HasExited))
            {
                isDevServerRunning = false;
                Repaint();
            }
        }

        private void OnDestroy()
        {
            if (devServerProcess != null && !devServerProcess.HasExited)
            {
                devServerProcess.Kill();
                devServerProcess = null;
                isDevServerRunning = false;
            }
        }

        private string FindNpmPath()
        {
            // 1. 시스템 설치 npm 우선 사용
            string systemNpm = FindSystemNpm();
            if (!string.IsNullOrEmpty(systemNpm))
            {
                AppendLog($"✓ 시스템 npm 사용: {systemNpm}");
                return systemNpm;
            }

            // 2. Embedded portable Node.js 사용 (자동 다운로드)
            string embeddedNpm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
            if (!string.IsNullOrEmpty(embeddedNpm))
            {
                AppendLog($"✓ Embedded npm 사용: {embeddedNpm}");
                return embeddedNpm;
            }

            return null;
        }

        private string FindSystemNpm()
        {
            // 1. 일반적인 npm 설치 경로 확인
            string[] possiblePaths = new string[]
            {
                "/usr/local/bin/npm",
                "/opt/homebrew/bin/npm",
                "/usr/bin/npm"
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // 2. which npm 명령으로 찾기
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-l -c \"which npm\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    return output;
                }
            }
            catch
            {
                // 무시
            }

            return null;
        }
    }
}
