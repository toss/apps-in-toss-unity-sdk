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
        private const string PREFS_KEY_INSTALLING = "AIT_PackageManagerInstalling";
        private const string PREFS_KEY_INSTALL_START_TIME = "AIT_InstallStartTime";

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
            Debug.Log($"[AIT] Domain Reload 감지 - AITPackageInitializer 초기화 " +
                      $"(isPlaying={EditorApplication.isPlayingOrWillChangePlaymode}, " +
                      $"isBuildingPlayer={BuildPipeline.isBuildingPlayer}, " +
                      $"hasAsyncTask={AITConvertCore.HasRunningAsyncTask()})");

            // Domain Reload 전후 로깅 (원인 추적용)
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                Debug.Log("[AIT] ⚡ Domain Reload 시작 (beforeAssemblyReload)");
                // 스택트레이스로 Reload 트리거 원인 추적
                Debug.Log($"[AIT] 트리거 스택: {System.Environment.StackTrace}");
            };
            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                Debug.Log("[AIT] ⚡ Domain Reload 완료 (afterAssemblyReload)");
            };

            // Editor가 Play Mode로 진입할 때는 실행하지 않음
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // 빌드 중이면 실행하지 않음 (Domain Reload로 인한 중복 실행 방지)
            if (BuildPipeline.isBuildingPlayer || AITConvertCore.HasRunningAsyncTask())
            {
                Debug.Log("[AIT] 빌드 중 Domain Reload 발생 - 초기화 스킵");
                return;
            }

            // 백그라운드에서 비동기적으로 초기화 작업 수행
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // 로딩 화면 템플릿 자동 생성 (항상 체크)
                    EnsureLoadingScreenTemplate();

                    // 패키지 매니저 체크 (매번 확인 - pnpm 체크 비용이 낮으므로 throttle 불필요)
                    CheckAndSetupPackageManager();
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
                // AssetDatabase.Refresh()를 사용하지 않음: .html은 스크립트가 아니므로
                // 전체 Refresh는 불필요하며, Domain Reload를 유발하여 Editor 시작이 반복됨.
                // 대신 해당 파일만 개별 임포트하여 Unity가 인식하도록 함.
                string assetPath = "Assets/AppsInToss/loading.html";
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
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

        // 재진입 방지 guard (에디터 시작 시 중복 호출 방지)
        private static bool _isStartupInitRunning = false;

        /// <summary>
        /// 패키지 매니저 체크 및 설정
        /// Node.js가 없으면 백그라운드에서 자동 다운로드를 시작합니다.
        /// </summary>
        private static void CheckAndSetupPackageManager()
        {
            if (_isStartupInitRunning) return;
            _isStartupInitRunning = true;

            try
            {
                Debug.Log("[AIT] 패키지 매니저 환경 체크 시작...");

                // AITPackageManagerHelper를 사용한 통합 체크
                string buildPath = GetBuildPath();

                // 1. 패키지 매니저 찾기 (내장 Node.js 자동 다운로드 포함)
                // autoDownload: true로 Node.js가 없으면 백그라운드에서 자동 다운로드
                string packageManagerPath = AITPackageManagerHelper.FindPackageManager(buildPath, verbose: false);
                if (string.IsNullOrEmpty(packageManagerPath))
                {
                    // FindPackageManager가 이미 다운로드를 시도했지만 실패한 경우
                    Debug.LogWarning("[AIT] Node.js/pnpm 설치에 실패했습니다. 네트워크 연결을 확인하거나 첫 빌드 시 다시 시도됩니다.");
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
                    Debug.Log("[AIT] 의존성 동기화는 pnpm 설치 완료 후 다음 Editor 시작 또는 빌드 시 실행됩니다.");

                    string npmPath = packageManagerPath;
                    EditorApplication.delayCall += () =>
                    {
                        InstallPnpmGlobal(npmPath);
                    };

                    // pnpm이 아직 없으므로 의존성 동기화는 스킵
                    // (이 시점에서 RunPackageCommand를 호출하면 내부에서 pnpm을 동기적으로
                    //  다시 설치하려 해서 메인 스레드 블로킹 + 이중 설치 발생)
                    return;
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
            catch (Exception e)
            {
                Debug.LogError($"[AIT] 패키지 매니저 체크 중 예외 발생: {e.Message}");
            }
            finally
            {
                _isStartupInitRunning = false;
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
        /// 설치 중인지 확인하고 선택적으로 대기합니다.
        /// blocking=false (기본): 설치 완료 시 true, 진행 중이면 false를 즉시 반환합니다.
        /// blocking=true: 프로그레스바를 표시하며 설치 완료까지 대기합니다 (빌드 경로용).
        /// 5분 이상 경과한 stale 상태는 자동 리셋됩니다.
        /// </summary>
        /// <param name="blocking">true이면 프로그레스바와 함께 설치 완료까지 대기</param>
        /// <returns>설치 완료 여부 (타임아웃 시 false)</returns>
        public static bool WaitForInstallation(bool blocking = false)
        {
            if (!IsInstalling)
            {
                return true;
            }

            // Stale 상태 체크 (5분 이상 경과 시 리셋)
            DateTime? startTime = InstallStartTime;
            if (startTime.HasValue && (DateTime.Now - startTime.Value).TotalMinutes > 5)
            {
                Debug.LogWarning("[AIT] 패키지 매니저 설치 타임아웃 (5분 경과). 설치 상태 초기화.");
                MarkInstallationCompleted();
                return false;
            }

            if (!blocking)
            {
                // non-blocking: 즉시 반환
                Debug.LogWarning("[AIT] 백그라운드 설치 진행 중. 완료 후 다시 시도하세요.");
                return false;
            }

            // blocking 모드: 프로그레스바와 함께 대기 (빌드 경로용)
            Debug.Log("[AIT] 백그라운드 패키지 매니저 설치 진행 중... 대기합니다.");
            const int maxWaitSeconds = 300; // 5분
            const int checkIntervalMs = 500;
            int waitedMs = 0;

            EditorUtility.DisplayProgressBar("Apps in Toss", "패키지 매니저 설치 대기 중...", 0f);

            while (IsInstalling && waitedMs < (maxWaitSeconds * 1000))
            {
                System.Threading.Thread.Sleep(checkIntervalMs);
                waitedMs += checkIntervalMs;

                float progress = Mathf.Clamp01((float)waitedMs / (maxWaitSeconds * 1000));
                EditorUtility.DisplayProgressBar("Apps in Toss", $"패키지 매니저 설치 대기 중... ({waitedMs / 1000}초)", progress);
            }

            EditorUtility.ClearProgressBar();

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
        public static void ForcePackageManagerCheck()
        {
            _isStartupInitRunning = false; // 수동 실행 시 guard 리셋
            CheckAndSetupPackageManager();
            Debug.Log("[AIT] 패키지 매니저 체크 완료");
        }

        /// <summary>
        /// 설치 상태 초기화 (디버그용)
        /// </summary>
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
        /// 내장 Node.js에 pnpm 글로벌 설치 (백그라운드, non-blocking)
        /// AITAsyncCommandRunner를 사용하여 메인 스레드를 차단하지 않습니다.
        /// </summary>
        /// <param name="npmPath">npm 실행 파일 경로</param>
        private static void InstallPnpmGlobal(string npmPath)
        {
            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);
                string pnpmVersion = AITPackageManagerHelper.PNPM_VERSION;
                string command = $"\"{npmPath}\" install -g pnpm@{pnpmVersion}";

                Debug.Log($"[AIT] pnpm@{pnpmVersion} 백그라운드 글로벌 설치 시작...");

                MarkInstallationStarted();

                AITAsyncCommandRunner.RunAsync(
                    command: command,
                    workingDirectory: npmDir,
                    additionalPaths: new[] { npmDir },
                    onComplete: (result) =>
                    {
                        MarkInstallationCompleted();

                        if (result.Success)
                        {
                            Debug.Log($"[AIT] ✓ pnpm@{pnpmVersion} 글로벌 설치 완료");
                        }
                        else
                        {
                            Debug.LogWarning($"[AIT] pnpm 글로벌 설치 실패 (exit code: {result.ExitCode}). 첫 빌드 시 다시 시도됩니다.");
                            if (!string.IsNullOrEmpty(result.Error))
                            {
                                Debug.LogWarning($"[AIT] 오류: {result.Error}");
                            }
                        }
                    },
                    timeoutMs: 120000 // 2분
                );
            }
            catch (Exception e)
            {
                MarkInstallationCompleted();
                Debug.LogWarning($"[AIT] pnpm 글로벌 설치 중 예외 발생: {e.Message}");
            }
        }
    }
}
