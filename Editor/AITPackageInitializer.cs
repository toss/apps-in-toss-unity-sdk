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

            // 마지막 체크 시간 확인 (너무 자주 체크하지 않도록)
            string lastCheckStr = EditorPrefs.GetString(PREFS_KEY_LAST_CHECK, string.Empty);
            if (!string.IsNullOrEmpty(lastCheckStr))
            {
                if (DateTime.TryParse(lastCheckStr, out DateTime lastCheck))
                {
                    double hoursSinceLastCheck = (DateTime.Now - lastCheck).TotalHours;
                    if (hoursSinceLastCheck < CHECK_INTERVAL_HOURS)
                    {
                        // 24시간 이내에 이미 체크했으면 스킵
                        return;
                    }
                }
            }

            // 백그라운드에서 비동기적으로 패키지 매니저 체크
            EditorApplication.delayCall += () =>
            {
                try
                {
                    CheckAndSetupPackageManager();

                    // 체크 시간 저장
                    EditorPrefs.SetString(PREFS_KEY_LAST_CHECK, DateTime.Now.ToString("O"));
                }
                catch (Exception e)
                {
                    // 초기화 실패해도 Unity는 정상 작동해야 함
                    Debug.LogWarning($"[AIT] 패키지 매니저 초기화 중 예외 발생 (무시됨): {e.Message}");
                }
            };
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

            // 2. node_modules 상태 확인
            string nodeModulesPath = Path.Combine(buildPath, "node_modules");
            bool hasNodeModules = Directory.Exists(nodeModulesPath);
            bool hasLocalPnpm = File.Exists(Path.Combine(buildPath, "node_modules", ".bin", "pnpm"));

            if (!hasNodeModules)
            {
                // node_modules가 없으면 pnpm 설치 + dependencies 설치
                Debug.Log("[AIT] ait-build/node_modules가 없습니다.");
                Debug.Log("[AIT] 백그라운드에서 pnpm 및 dependencies를 설치합니다...");

                string packageJsonSource = FindPackageJsonTemplate();
                if (!string.IsNullOrEmpty(packageJsonSource))
                {
                    // 백그라운드에서 통합 함수로 dependencies 설치
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
            else if (!hasLocalPnpm)
            {
                // node_modules는 있지만 pnpm이 없으면 pnpm만 설치
                Debug.Log("[AIT] node_modules는 있지만 로컬 pnpm이 없습니다.");
                Debug.Log("[AIT] 백그라운드에서 pnpm을 설치합니다...");

                EditorApplication.delayCall += () =>
                {
                    // pnpm 로컬 설치
                    AITPackageManagerHelper.RunPackageCommand(
                        "install pnpm",
                        buildPath,
                        packageJsonTemplatePath: null,
                        async: true,
                        verbose: true,
                        showProgressBar: false
                    );
                };
            }
            else
            {
                Debug.Log("[AIT] ✓ ait-build/node_modules 및 pnpm 존재. 준비 완료.");
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

            // WebGLTemplates/AITTemplate/BuildConfig/package.json
            string packageJsonPath = Path.Combine(packagePath, "WebGLTemplates", "AITTemplate", "BuildConfig", "package.json");

            if (File.Exists(packageJsonPath))
            {
                return packageJsonPath;
            }

            return null;
        }
    }
}
