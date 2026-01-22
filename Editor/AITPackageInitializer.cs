using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Apps in Toss SDK 패키지 초기화
    /// Unity Editor 시작 시 자동으로 pnpm & Node.js 환경을 설정합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITPackageInitializer
    {
        private const string PREFS_KEY_LAST_CHECK = "AIT_LastPackageManagerCheck";
        private const string PREFS_KEY_INSTALLING = "AIT_PackageManagerInstalling";
        private const string PREFS_KEY_INSTALL_START_TIME = "AIT_InstallStartTime";
        private const double CHECK_INTERVAL_HOURS = 24.0; // 24시간마다 체크

        /// <summary>
        /// 패키지 매니저 설치가 진행 중인지 확인
        /// </summary>
        public static bool IsInstalling
        {
            get => EditorPrefs.GetBool(PREFS_KEY_INSTALLING, false);
            private set => EditorPrefs.SetBool(PREFS_KEY_INSTALLING, value);
        }

        /// <summary>
        /// 설치 시작 시간
        /// </summary>
        private static DateTime? InstallStartTime
        {
            get
            {
                string timeStr = EditorPrefs.GetString(PREFS_KEY_INSTALL_START_TIME, string.Empty);
                if (DateTime.TryParse(timeStr, out DateTime time))
                {
                    return time;
                }
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    EditorPrefs.SetString(PREFS_KEY_INSTALL_START_TIME, value.Value.ToString("O"));
                }
                else
                {
                    EditorPrefs.DeleteKey(PREFS_KEY_INSTALL_START_TIME);
                }
            }
        }

        static AITPackageInitializer()
        {
            // Editor가 Play Mode로 진입할 때는 실행하지 않음
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // 백그라운드에서 비동기적으로 초기화 작업 수행
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // 로딩 화면 템플릿 자동 생성 (항상 체크)
                    EnsureLoadingScreenTemplate();

                    // 패키지 매니저 체크 (24시간마다)
                    string lastCheckStr = EditorPrefs.GetString(PREFS_KEY_LAST_CHECK, string.Empty);
                    bool shouldCheckPackageManager = true;

                    if (!string.IsNullOrEmpty(lastCheckStr))
                    {
                        if (DateTime.TryParse(lastCheckStr, out DateTime lastCheck))
                        {
                            double hoursSinceLastCheck = (DateTime.Now - lastCheck).TotalHours;
                            if (hoursSinceLastCheck < CHECK_INTERVAL_HOURS)
                            {
                                shouldCheckPackageManager = false;
                            }
                        }
                    }

                    if (shouldCheckPackageManager)
                    {
                        CheckAndSetupPackageManager();
                        EditorPrefs.SetString(PREFS_KEY_LAST_CHECK, DateTime.Now.ToString("O"));
                    }
                }
                catch (Exception e)
                {
                    // 초기화 실패해도 Unity는 정상 작동해야 함
                    Debug.LogWarning($"[AIT] SDK 초기화 중 예외 발생 (무시됨): {e.Message}");
                }
            };
        }

        /// <summary>
        /// SDK 로딩 화면 템플릿 경로 반환
        /// 여러 후보 경로 중 존재하는 첫 번째 경로를 반환합니다.
        /// </summary>
        /// <returns>존재하는 템플릿 경로, 또는 찾지 못한 경우 null</returns>
        public static string GetSDKLoadingTemplatePath()
        {
            string[] sdkLoadingPaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/loading.html"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/loading.html"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITPackageInitializer).Assembly.Location)), "WebGLTemplates/AITTemplate/loading.html")
            };

            foreach (string sdkPath in sdkLoadingPaths)
            {
                if (File.Exists(sdkPath))
                {
                    return sdkPath;
                }
            }

            return null;
        }

        /// <summary>
        /// 프로젝트의 로딩 화면 경로 반환
        /// </summary>
        public static string GetProjectLoadingPath()
        {
            return Path.Combine(Application.dataPath, "AppsInToss", "loading.html");
        }

        /// <summary>
        /// 로딩 화면 템플릿 자동 생성
        /// Assets/AppsInToss/loading.html이 없으면 SDK 기본 템플릿을 복사합니다.
        /// </summary>
        private static void EnsureLoadingScreenTemplate()
        {
            string projectLoadingPath = GetProjectLoadingPath();

            // 이미 존재하면 스킵
            if (File.Exists(projectLoadingPath))
            {
                return;
            }

            string sdkTemplatePath = GetSDKLoadingTemplatePath();
            if (sdkTemplatePath != null)
            {
                // AppsInToss 폴더가 없으면 생성
                string appsInTossDir = Path.GetDirectoryName(projectLoadingPath);
                if (!Directory.Exists(appsInTossDir))
                {
                    Directory.CreateDirectory(appsInTossDir);
                }

                // SDK 기본 템플릿을 프로젝트에 복사
                File.Copy(sdkTemplatePath, projectLoadingPath);
                AssetDatabase.Refresh();
                Debug.Log("[AIT] ✓ 로딩 화면 템플릿 자동 생성: Assets/AppsInToss/loading.html");
                Debug.Log("[AIT]   이 파일을 수정하면 커스텀 로딩 화면이 적용됩니다.");
                Debug.Log("[AIT]   기본 템플릿으로 초기화하려면: AIT > Reset Loading Screen");
            }
            else
            {
                // SDK 템플릿을 찾지 못함 (경고만 출력, 빌드 시 다시 시도됨)
                Debug.LogWarning("[AIT] SDK 로딩 화면 템플릿을 찾을 수 없습니다. 첫 빌드 시 다시 시도됩니다.");
            }
        }

        /// <summary>
        /// 패키지 매니저 체크 및 설정
        /// </summary>
        private static void CheckAndSetupPackageManager()
        {
            Debug.Log("[AIT] 패키지 매니저 환경 체크 시작...");

            // AITPackageManagerHelper를 사용한 통합 체크
            string buildPath = GetBuildPath();

            // 1. 패키지 매니저 찾기 (node → npm → pnpm 순서로 체크)
            string packageManagerPath = AITPackageManagerHelper.FindPackageManager(buildPath, verbose: true);
            if (string.IsNullOrEmpty(packageManagerPath))
            {
                Debug.Log("[AIT] Node.js가 설치되어 있지 않습니다. 첫 빌드 시 자동으로 다운로드됩니다.");
                return;
            }

            // 패키지 매니저가 존재하면 성공 메시지 출력
            string pmName = Path.GetFileName(packageManagerPath);
            Debug.Log($"[AIT] ✓ 패키지 매니저 사용 가능: {pmName} ({packageManagerPath})");

            // 2. pnpm 글로벌 설치 확인 (내장 Node.js에 pnpm이 설치되어 있는지)
            // npm이 반환되었다면 pnpm이 아직 설치되지 않은 것
            if (pmName == "npm")
            {
                Debug.Log("[AIT] 내장 Node.js에 pnpm이 설치되어 있지 않습니다.");
                Debug.Log($"[AIT] 백그라운드에서 pnpm@{AITPackageManagerHelper.PNPM_VERSION}을 설치합니다...");

                string npmPath = packageManagerPath;
                EditorApplication.delayCall += () =>
                {
                    InstallPnpmGlobal(npmPath);
                };
            }

            // 3. 의존성 동기화 (pnpm install)
            // package.json 변경에 대비하여 항상 실행 (이미 설치된 경우 빠르게 완료됨)
            string packageJsonSource = FindPackageJsonTemplate();
            if (!string.IsNullOrEmpty(packageJsonSource))
            {
                Debug.Log("[AIT] 백그라운드에서 의존성을 동기화합니다...");

                EditorApplication.delayCall += () =>
                {
                    AITPackageManagerHelper.RunPackageCommand(
                        "install",
                        buildPath,
                        packageJsonSource,
                        async: true,
                        verbose: true,
                        showProgressBar: false
                    );
                };
            }
            else
            {
                Debug.LogWarning("[AIT] package.json 템플릿을 찾을 수 없습니다. 첫 빌드 시 자동으로 설치됩니다.");
            }
        }

        /// <summary>
        /// ait-build 경로 가져오기
        /// </summary>
        private static string GetBuildPath()
        {
            return AITPackageManagerHelper.GetBuildPath();
        }

        /// <summary>
        /// 설치 시작 표시
        /// </summary>
        public static void MarkInstallationStarted()
        {
            IsInstalling = true;
            InstallStartTime = DateTime.Now;
            Debug.Log("[AIT] 패키지 매니저 설치 시작");
        }

        /// <summary>
        /// 설치 완료 표시
        /// </summary>
        public static void MarkInstallationCompleted()
        {
            IsInstalling = false;
            InstallStartTime = null;
            Debug.Log("[AIT] 패키지 매니저 설치 완료");
        }

        /// <summary>
        /// 설치 중이면 대기 (최대 5분)
        /// </summary>
        /// <returns>설치 완료 여부 (timeout 시 false)</returns>
        public static bool WaitForInstallation()
        {
            if (!IsInstalling)
            {
                return true;
            }

            const int maxWaitSeconds = 300; // 5분
            const int checkIntervalMs = 500; // 0.5초마다 체크

            DateTime? startTime = InstallStartTime;
            if (startTime.HasValue)
            {
                // 설치 시작 후 5분 이상 경과했으면 타임아웃으로 간주
                double elapsedMinutes = (DateTime.Now - startTime.Value).TotalMinutes;
                if (elapsedMinutes > 5)
                {
                    Debug.LogWarning("[AIT] 패키지 매니저 설치 타임아웃 (5분 경과). 설치 상태 초기화.");
                    MarkInstallationCompleted();
                    return false;
                }
            }

            Debug.Log("[AIT] 백그라운드 패키지 매니저 설치 진행 중... 대기합니다.");

            int waitedMs = 0;
            while (IsInstalling && waitedMs < (maxWaitSeconds * 1000))
            {
                System.Threading.Thread.Sleep(checkIntervalMs);
                waitedMs += checkIntervalMs;

                // 10초마다 진행 상태 로깅
                if (waitedMs % 10000 == 0)
                {
                    Debug.Log($"[AIT] 설치 대기 중... ({waitedMs / 1000}초 경과)");
                }
            }

            if (IsInstalling)
            {
                Debug.LogWarning("[AIT] 패키지 매니저 설치 대기 타임아웃. 강제 진행합니다.");
                MarkInstallationCompleted();
                return false;
            }

            Debug.Log("[AIT] 패키지 매니저 설치 완료. 빌드를 계속합니다.");
            return true;
        }

        /// <summary>
        /// 수동으로 패키지 매니저 체크 강제 실행 (디버그용)
        /// </summary>
        [MenuItem("AIT/Debug/Force Package Manager Check")]
        public static void ForcePackageManagerCheck()
        {
            EditorPrefs.DeleteKey(PREFS_KEY_LAST_CHECK);
            CheckAndSetupPackageManager();
            Debug.Log("[AIT] 패키지 매니저 체크 완료");
        }

        /// <summary>
        /// 설치 상태 초기화 (디버그용)
        /// </summary>
        [MenuItem("AIT/Debug/Reset Installation State")]
        public static void ResetInstallationState()
        {
            MarkInstallationCompleted();
            Debug.Log("[AIT] 설치 상태 초기화 완료");
        }

        /// <summary>
        /// package.json 템플릿 경로 찾기
        /// </summary>
        private static string FindPackageJsonTemplate()
        {
            // SDK 패키지 경로 찾기
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/im.toss.apps-in-toss-unity-sdk");
            string packagePath;

            if (packageInfo != null)
            {
                packagePath = packageInfo.resolvedPath;
            }
            else
            {
                // Fallback: Assets/AppsInToss 경로
                packagePath = Path.Combine(Application.dataPath, "AppsInToss");
            }

            // WebGLTemplates/AITTemplate/BuildConfig~/package.json (Unity는 ~ 접미사 폴더 무시)
            string packageJsonPath = Path.Combine(packagePath, "WebGLTemplates", "AITTemplate", "BuildConfig~", "package.json");

            if (File.Exists(packageJsonPath))
            {
                return packageJsonPath;
            }

            return null;
        }

        /// <summary>
        /// 내장 Node.js에 pnpm 글로벌 설치 (백그라운드)
        /// </summary>
        /// <param name="npmPath">npm 실행 파일 경로</param>
        private static void InstallPnpmGlobal(string npmPath)
        {
            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);
                string pnpmVersion = AITPackageManagerHelper.PNPM_VERSION;

                Debug.Log($"[AIT] pnpm@{pnpmVersion} 글로벌 설치 시작...");

                // npm install -g pnpm@버전 실행
                string command = $"\"{npmPath}\" install -g pnpm@{pnpmVersion}";
                var result = AITPlatformHelper.ExecuteCommand(command, npmDir, new[] { npmDir }, verbose: true);

                if (result.Success)
                {
                    Debug.Log($"[AIT] ✓ pnpm@{pnpmVersion} 글로벌 설치 완료");
                }
                else
                {
                    Debug.LogWarning($"[AIT] pnpm 글로벌 설치 실패 (exit code: {result.ExitCode}). 첫 빌드 시 다시 시도됩니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] pnpm 글로벌 설치 중 예외 발생: {e.Message}");
            }
        }
    }
}
