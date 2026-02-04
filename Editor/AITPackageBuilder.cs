using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// ait-build 패키징 담당 클래스
    /// </summary>
    internal static class AITPackageBuilder
    {
        // SDK BuildConfig 경로 캐시 (빌드 중 반복 검색 방지)
        private static string _cachedSdkBuildConfigPath = null;
        private static string _cachedSdkRuntimePath = null;

        /// <summary>
        /// 캐시된 SDK 경로를 초기화합니다.
        /// Domain reload 시 자동으로 초기화됨
        /// </summary>
        internal static void ClearPathCache()
        {
            _cachedSdkBuildConfigPath = null;
            _cachedSdkRuntimePath = null;
        }

        /// <summary>
        /// WebGL 빌드를 ait-build로 패키징
        /// </summary>
        internal static AITConvertCore.AITExportError PackageWebGLBuild(string projectPath, string webglPath, AITBuildProfile profile = null)
        {
            // 백그라운드 Node.js/pnpm 설치가 진행 중이면 완료될 때까지 대기
            if (AITPackageInitializer.IsInstalling)
            {
                Debug.Log("[AIT] Node.js/pnpm 설치가 진행 중입니다. 완료될 때까지 대기합니다...");
                if (!AITPackageInitializer.WaitForInstallation())
                {
                    Debug.LogError("[AIT] 설치 대기 타임아웃. 빌드를 중단합니다.");
                    return AITConvertCore.AITExportError.NODE_NOT_FOUND;
                }
            }

            Debug.Log("[AIT] Vite 기반 빌드 패키징 시작...");

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            string buildProjectPath = Path.Combine(projectPath, "ait-build");

            // ait-build 폴더가 없으면 생성
            if (!Directory.Exists(buildProjectPath))
            {
                Directory.CreateDirectory(buildProjectPath);
                Debug.Log("[AIT] ait-build 폴더 생성");
            }
            else
            {
                Debug.Log("[AIT] 기존 빌드 결과물 정리 중... (node_modules와 설정 파일은 유지)");

                // 유지할 항목들
                string[] itemsToKeep = new string[]
                {
                    "node_modules",
                    ".npm-cache",
                    "package.json",
                    "package-lock.json",
                    "pnpm-lock.yaml",
                    "granite.config.ts",
                    "vite.config.ts",
                    "tsconfig.json"
                };

                // 모든 파일과 폴더를 순회하면서 유지 목록에 없는 것들 삭제
                foreach (string item in Directory.GetFileSystemEntries(buildProjectPath))
                {
                    string itemName = Path.GetFileName(item);

                    // 유지 목록에 있으면 스킵
                    bool shouldKeep = false;
                    foreach (string keepItem in itemsToKeep)
                    {
                        if (itemName == keepItem)
                        {
                            shouldKeep = true;
                            break;
                        }
                    }

                    if (shouldKeep)
                    {
                        continue;
                    }

                    // 삭제
                    try
                    {
                        if (Directory.Exists(item))
                        {
                            AITFileUtils.DeleteDirectory(item);
                            Debug.Log($"[AIT] 삭제됨: {itemName}/");
                        }
                        else if (File.Exists(item))
                        {
                            File.Delete(item);
                            Debug.Log($"[AIT] 삭제됨: {itemName}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AIT] 삭제 실패: {itemName} - {e.Message}");
                    }
                }
            }

            // npm 경로 찾기
            string npmPath = AITNpmRunner.FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("[AIT] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                return AITConvertCore.AITExportError.NODE_NOT_FOUND;
            }

            // 1. Vite 프로젝트 구조 생성 (템플릿에서 복사)
            Debug.Log("[AIT] Step 1/3: Vite 프로젝트 구조 생성 중...");
            CopyBuildConfigFromTemplate(buildProjectPath);

            // 2. Unity WebGL 빌드를 public 폴더로 복사
            Debug.Log("[AIT] Step 2/3: Unity WebGL 빌드 복사 중...");
            var copyResult = CopyWebGLToPublic(webglPath, buildProjectPath, profile);
            if (copyResult != AITConvertCore.AITExportError.SUCCEED)
            {
                return copyResult;
            }

            // 3. npm install 및 build 실행
            Debug.Log("[AIT] Step 3/3: pnpm install & build 실행 중...");
            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");

            // pnpm install 실행 (의존성 동기화 - 이미 설치된 경우 빠르게 완료됨)
            Debug.Log("[AIT] pnpm install 실행 중...");

            // pnpm 경로 찾기 (없으면 자동 설치)
            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPath))
            {
                Debug.LogError("[AIT] pnpm 설치에 실패했습니다. Unity Console에서 에러를 확인해주세요.");
                string nodejsPath = AITPlatformHelper.IsWindows
                    ? "%LOCALAPPDATA%\\ait-unity-sdk\\nodejs\\"
                    : "~/.ait-unity-sdk/nodejs/";
                AITPlatformHelper.ShowInfoDialog(
                    "빌드 실패",
                    "pnpm을 찾을 수 없습니다.\n\n" +
                    "해결 방법:\n" +
                    "1. 네트워크 연결을 확인하세요.\n" +
                    $"2. {nodejsPath} 폴더를 삭제 후 Unity를 재시작하세요.\n" +
                    "3. 방화벽/프록시가 nodejs.org를 차단하고 있는지 확인하세요.",
                    "확인");
                return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
            }

            // node_modules 무결성 검증 (web-framework 버전 일치 확인)
            if (!ValidateNodeModulesIntegrity(buildProjectPath))
            {
                Debug.Log("[AIT] node_modules 무결성 검증 실패. 정리 후 재설치합니다.");
                CleanNodeModules(buildProjectPath);
            }

            // 먼저 --frozen-lockfile로 시도, 실패하면 lockfile 없이 재시도
            // (사용자가 package.json에 새 패키지 추가 시 lockfile이 outdated 될 수 있음)
            var installResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install --frozen-lockfile", localCachePath, "pnpm install 실행 중...");

            if (installResult != AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.LogWarning("[AIT] --frozen-lockfile 설치 실패, lockfile 갱신 모드로 재시도...");
                Debug.LogWarning("[AIT] (사용자가 package.json에 새 패키지를 추가한 경우 정상 동작입니다)");

                // lockfile 없이 재시도 (CI 환경에서도 lockfile 갱신 허용)
                installResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install --no-frozen-lockfile", localCachePath, "pnpm install (lockfile 갱신)...");

                if (installResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    // 최종 재시도: node_modules 정리 후 clean install
                    Debug.LogWarning("[AIT] pnpm install 재시도 실패. node_modules 정리 후 한 번 더 시도합니다...");
                    CleanNodeModules(buildProjectPath);

                    installResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install --no-frozen-lockfile", localCachePath, "pnpm install (clean 재시도)...");
                    if (installResult != AITConvertCore.AITExportError.SUCCEED)
                    {
                        Debug.LogError("[AIT] pnpm install 실패 (clean 재시도 후에도 실패)");
                        return installResult;
                    }
                }

                Debug.Log("[AIT] ✓ 새 패키지 설치 및 lockfile 갱신 완료");
            }

            // granite build 실행 (web 폴더를 dist로 복사)
            Debug.Log("[AIT] granite build 실행 중...");

            var buildResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "run build", localCachePath, "granite build 실행 중...");

            if (buildResult != AITConvertCore.AITExportError.SUCCEED)
            {
                // MODULE_NOT_FOUND 등 런타임 에러 대응: node_modules 정리 후 install부터 재시도
                Debug.LogWarning("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
                CleanNodeModules(buildProjectPath);

                // install부터 다시 실행
                var retryInstallResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install --no-frozen-lockfile", localCachePath, "pnpm install (빌드 실패 후 재시도)...");
                if (retryInstallResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.LogError("[AIT] granite build 실패 후 pnpm install 재시도도 실패");
                    return retryInstallResult;
                }

                // build 재시도
                buildResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "run build", localCachePath, "granite build (재시도)...");
                if (buildResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    Debug.LogError("[AIT] granite build 재시도도 실패");
                    return buildResult;
                }

                Debug.Log("[AIT] ✓ granite build 재시도 성공");
            }

            string distPath = Path.Combine(buildProjectPath, "dist");

            // 빌드 완료 리포트 출력
            AITBuildValidator.PrintBuildReport(buildProjectPath, distPath);

            Debug.Log($"[AIT] ✓ 패키징 완료: {distPath}");

            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// package.json의 dependencies를 머지합니다.
        /// </summary>
        internal static void MergePackageJson(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "package.json");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "package.json");
            string destFile = Path.Combine(destPath, "package.json");

            // 프로젝트 파일 없으면 SDK 복사
            if (!File.Exists(projectFile))
            {
                File.Copy(sdkFile, destFile, true);
                Debug.Log("[AIT]   ✓ package.json (SDK에서 복사)");
                return;
            }

            try
            {
                string projectContent = File.ReadAllText(projectFile);
                string sdkContent = File.ReadAllText(sdkFile);

                // 간단한 JSON 머지 (dependencies와 devDependencies)
                var projectJson = MiniJson.Deserialize(projectContent) as Dictionary<string, object>;
                var sdkJson = MiniJson.Deserialize(sdkContent) as Dictionary<string, object>;

                if (projectJson == null || sdkJson == null)
                {
                    Debug.LogWarning("[AIT] package.json 파싱 실패, SDK 버전 사용");
                    File.Copy(sdkFile, destFile, true);
                    return;
                }

                // SDK의 기본 구조를 사용하고 dependencies만 머지
                var result = new Dictionary<string, object>(sdkJson);

                // dependencies 머지
                result["dependencies"] = MergeDependencies(
                    projectJson.ContainsKey("dependencies") ? projectJson["dependencies"] as Dictionary<string, object> : null,
                    sdkJson.ContainsKey("dependencies") ? sdkJson["dependencies"] as Dictionary<string, object> : null
                );

                // devDependencies 머지
                result["devDependencies"] = MergeDependencies(
                    projectJson.ContainsKey("devDependencies") ? projectJson["devDependencies"] as Dictionary<string, object> : null,
                    sdkJson.ContainsKey("devDependencies") ? sdkJson["devDependencies"] as Dictionary<string, object> : null
                );

                string mergedJson = MiniJson.Serialize(result);
                File.WriteAllText(destFile, mergedJson, new System.Text.UTF8Encoding(false));
                Debug.Log("[AIT]   ✓ package.json (dependencies 머지됨)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] package.json 머지 실패: {e.Message}, SDK 버전 사용");
                File.Copy(sdkFile, destFile, true);
            }
        }

        /// <summary>
        /// dependencies 딕셔너리를 머지합니다. SDK 패키지가 우선됩니다.
        /// </summary>
        internal static Dictionary<string, object> MergeDependencies(Dictionary<string, object> project, Dictionary<string, object> sdk)
        {
            var result = new Dictionary<string, object>();

            // 프로젝트 dependencies 먼저 추가
            if (project != null)
            {
                foreach (var kvp in project)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // SDK dependencies로 덮어쓰기 (SDK가 우선)
            if (sdk != null)
            {
                foreach (var kvp in sdk)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// tsconfig.json을 머지합니다.
        /// SDK의 필수 옵션을 유지하면서 사용자 옵션을 추가합니다.
        /// </summary>
        internal static void MergeTsConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "tsconfig.json");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "tsconfig.json");
            string destFile = Path.Combine(destPath, "tsconfig.json");

            // 프로젝트 파일 없으면 SDK 복사
            if (!File.Exists(projectFile))
            {
                File.Copy(sdkFile, destFile, true);
                Debug.Log("[AIT]   ✓ tsconfig.json (SDK에서 복사)");
                return;
            }

            try
            {
                string projectContent = File.ReadAllText(projectFile);
                string sdkContent = File.ReadAllText(sdkFile);

                var projectJson = MiniJson.Deserialize(projectContent) as Dictionary<string, object>;
                var sdkJson = MiniJson.Deserialize(sdkContent) as Dictionary<string, object>;

                if (projectJson == null || sdkJson == null)
                {
                    Debug.LogWarning("[AIT] tsconfig.json 파싱 실패, SDK 버전 사용");
                    File.Copy(sdkFile, destFile, true);
                    return;
                }

                // SDK의 기본 구조를 사용
                var result = new Dictionary<string, object>(sdkJson);

                // compilerOptions 머지
                var sdkCompilerOptions = sdkJson.ContainsKey("compilerOptions")
                    ? sdkJson["compilerOptions"] as Dictionary<string, object>
                    : new Dictionary<string, object>();
                var projectCompilerOptions = projectJson.ContainsKey("compilerOptions")
                    ? projectJson["compilerOptions"] as Dictionary<string, object>
                    : new Dictionary<string, object>();

                // SDK 필수 옵션 정의 (이 옵션들은 SDK 값으로 강제)
                var sdkRequiredOptions = new HashSet<string>
                {
                    "moduleResolution",  // bundler 필수
                    "esModuleInterop",   // 호환성 필수
                };

                // 머지된 compilerOptions 생성
                var mergedCompilerOptions = new Dictionary<string, object>();

                // 1. SDK 옵션 먼저 추가 (기본값)
                if (sdkCompilerOptions != null)
                {
                    foreach (var kvp in sdkCompilerOptions)
                    {
                        mergedCompilerOptions[kvp.Key] = kvp.Value;
                    }
                }

                // 2. 프로젝트 옵션으로 덮어쓰기 (SDK 필수 옵션 제외)
                if (projectCompilerOptions != null)
                {
                    foreach (var kvp in projectCompilerOptions)
                    {
                        if (!sdkRequiredOptions.Contains(kvp.Key))
                        {
                            mergedCompilerOptions[kvp.Key] = kvp.Value;
                        }
                    }
                }

                result["compilerOptions"] = mergedCompilerOptions;

                // include 배열 (프로젝트에 있으면 프로젝트 우선)
                if (projectJson.ContainsKey("include"))
                {
                    result["include"] = projectJson["include"];
                }

                // exclude 배열 (프로젝트에 있으면 사용)
                if (projectJson.ContainsKey("exclude"))
                {
                    result["exclude"] = projectJson["exclude"];
                }

                string mergedJson = MiniJson.Serialize(result);
                File.WriteAllText(destFile, mergedJson, new System.Text.UTF8Encoding(false));
                Debug.Log("[AIT]   ✓ tsconfig.json (compilerOptions 머지됨)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] tsconfig.json 머지 실패: {e.Message}, SDK 버전 사용");
                File.Copy(sdkFile, destFile, true);
            }
        }

        /// <summary>
        /// vite.config.ts를 마커 기반으로 업데이트합니다.
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// import 문, SDK_PLUGINS, SDK_GENERATED는 항상 SDK 최신 버전으로 갱신됩니다.
        /// </summary>
        internal static void UpdateViteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "vite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "vite.config.ts");
            string destFile = Path.Combine(destPath, "vite.config.ts");

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환
            string finalContent = sdkTemplate
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString());

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                // 프로젝트의 USER_CONFIG 영역 추출
                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    // SDK 템플릿의 USER_CONFIG를 프로젝트의 USER_CONFIG로 교체
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ vite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ vite.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ vite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// granite.config.ts를 마커 기반으로 업데이트합니다.
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// import 문, SDK_GENERATED는 항상 SDK 최신 버전으로 갱신됩니다.
        /// </summary>
        internal static void UpdateGraniteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "granite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "granite.config.ts");
            string destFile = Path.Combine(destPath, "granite.config.ts");

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환
            Debug.Log("[AIT] granite.config.ts placeholder 치환 중...");
            string finalContent = sdkTemplate
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ICON_URL%", config.iconUrl)
                .Replace("%AIT_BRIDGE_COLOR_MODE%", config.GetBridgeColorModeString())
                .Replace("%AIT_WEBVIEW_TYPE%", config.GetWebViewTypeString())
                .Replace("%AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%", config.allowsInlineMediaPlayback.ToString().ToLower())
                .Replace("%AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%", config.mediaPlaybackRequiresUserAction.ToString().ToLower())
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString())
                .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson())
                .Replace("%AIT_OUTDIR%", config.outdir);

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                // 프로젝트의 USER_CONFIG 영역 추출
                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    // SDK 템플릿의 USER_CONFIG를 프로젝트의 USER_CONFIG로 교체
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ granite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ granite.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ granite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// 프로젝트 BuildConfig의 추가 파일들을 재귀적으로 복사합니다.
        /// </summary>
        internal static void CopyAdditionalUserFiles(string projectBuildConfigPath, string destPath)
        {
            if (!Directory.Exists(projectBuildConfigPath)) return;

            // 루트 레벨에서 제외할 파일들
            var excludeRootFiles = new HashSet<string>
            {
                "package.json", "pnpm-lock.yaml", "vite.config.ts",
                "tsconfig.json", "unity-bridge.ts", "granite.config.ts"
            };

            // 제외할 폴더들
            var excludeFolders = new HashSet<string>
            {
                "node_modules",
                ".npm-cache",
                "dist"
            };

            CopyUserFilesRecursive(projectBuildConfigPath, destPath, excludeRootFiles, excludeFolders, isRoot: true);
        }

        /// <summary>
        /// 재귀적으로 사용자 파일을 복사합니다.
        /// </summary>
        private static void CopyUserFilesRecursive(
            string sourceDir,
            string destDir,
            HashSet<string> excludeRootFiles,
            HashSet<string> excludeFolders,
            bool isRoot)
        {
            // 대상 폴더 생성
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // 파일 복사
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);

                // 루트 레벨에서만 특정 파일 제외
                if (isRoot && excludeRootFiles.Contains(fileName))
                {
                    continue;
                }

                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);

                // 의미 있는 파일만 로그 출력
                if (fileName.EndsWith(".ts") || fileName.EndsWith(".tsx") ||
                    fileName.EndsWith(".js") || fileName.EndsWith(".jsx") ||
                    fileName.EndsWith(".css") || fileName.EndsWith(".scss"))
                {
                    Debug.Log($"[AIT]   ✓ {fileName} (사용자 추가 파일)");
                }
            }

            // 하위 폴더 재귀 복사
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);

                // 제외 폴더 스킵
                if (excludeFolders.Contains(dirName))
                {
                    continue;
                }

                string destSubDir = Path.Combine(destDir, dirName);
                CopyUserFilesRecursive(dir, destSubDir, excludeRootFiles, excludeFolders, isRoot: false);

                // 폴더 복사 완료 로그
                Debug.Log($"[AIT]   ✓ {dirName}/ (사용자 추가 폴더)");
            }
        }

        /// <summary>
        /// BuildConfig 템플릿에서 빌드 설정 파일들을 복사합니다.
        /// </summary>
        internal static void CopyBuildConfigFromTemplate(string buildProjectPath)
        {
            // 프로젝트 BuildConfig 경로 (사용자 커스터마이징 가능)
            string projectBuildConfigPath = Path.Combine(Application.dataPath, "WebGLTemplates/AITTemplate/BuildConfig~");

            // SDK의 BuildConfig 템플릿 경로 찾기 (캐싱 사용)
            string sdkBuildConfigPath = GetSdkBuildConfigPath();

            if (sdkBuildConfigPath == null)
            {
                Debug.LogError("[AIT] SDK BuildConfig 폴더를 찾을 수 없습니다.");
                Debug.LogError("[AIT] SDK가 올바르게 설치되었는지 확인하세요.");
                return;
            }

            Debug.Log($"[AIT] ✓ SDK BuildConfig 템플릿: {sdkBuildConfigPath}");

            // 프로젝트 BuildConfig 존재 여부 확인
            bool hasProjectBuildConfig = Directory.Exists(projectBuildConfigPath);
            if (hasProjectBuildConfig)
            {
                Debug.Log($"[AIT] ✓ 프로젝트 BuildConfig 발견: {projectBuildConfigPath}");
            }
            else
            {
                Debug.Log("[AIT] 프로젝트 BuildConfig 없음, SDK 버전 사용");
            }

            var config = UnityUtil.GetEditorConf();

            Debug.Log("[AIT] BuildConfig 파일 처리 중...");

            // 1. package.json - dependencies 머지
            MergePackageJson(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath);

            // 2. pnpm-lock.yaml - 프로젝트 우선, 없으면 SDK
            string pnpmLockProject = Path.Combine(projectBuildConfigPath, "pnpm-lock.yaml");
            string pnpmLockSdk = Path.Combine(sdkBuildConfigPath, "pnpm-lock.yaml");
            string pnpmLockDst = Path.Combine(buildProjectPath, "pnpm-lock.yaml");
            if (File.Exists(pnpmLockProject))
            {
                File.Copy(pnpmLockProject, pnpmLockDst, true);
                Debug.Log("[AIT]   ✓ pnpm-lock.yaml (프로젝트에서 복사)");
            }
            else if (File.Exists(pnpmLockSdk))
            {
                File.Copy(pnpmLockSdk, pnpmLockDst, true);
                Debug.Log("[AIT]   ✓ pnpm-lock.yaml (SDK에서 복사)");
            }

            // 3. vite.config.ts - 마커 기반 업데이트
            UpdateViteConfig(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath, config);

            // 4. granite.config.ts - 마커 기반 업데이트
            UpdateGraniteConfig(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath, config);

            // 5. tsconfig.json - 머지 (프로젝트 옵션 + SDK 필수 옵션)
            MergeTsConfig(projectBuildConfigPath, sdkBuildConfigPath, buildProjectPath);

            // 6. unity-bridge.ts - 프로젝트 우선, 없으면 SDK
            string unityBridgeProject = Path.Combine(projectBuildConfigPath, "unity-bridge.ts");
            string unityBridgeSdk = Path.Combine(sdkBuildConfigPath, "unity-bridge.ts");
            string unityBridgeDst = Path.Combine(buildProjectPath, "unity-bridge.ts");
            if (File.Exists(unityBridgeProject))
            {
                File.Copy(unityBridgeProject, unityBridgeDst, true);
                Debug.Log("[AIT]   ✓ unity-bridge.ts (프로젝트에서 복사)");
            }
            else if (File.Exists(unityBridgeSdk))
            {
                File.Copy(unityBridgeSdk, unityBridgeDst, true);
                Debug.Log("[AIT]   ✓ unity-bridge.ts (SDK에서 복사)");
            }

            // 7. 사용자 추가 파일 복사
            CopyAdditionalUserFiles(projectBuildConfigPath, buildProjectPath);

            Debug.Log("[AIT] ✓ 빌드 설정 파일 처리 완료");
        }

        /// <summary>
        /// SDK BuildConfig 템플릿 경로를 반환합니다. (캐싱 사용)
        /// </summary>
        private static string GetSdkBuildConfigPath()
        {
            // 캐시된 경로가 유효한지 확인
            if (_cachedSdkBuildConfigPath != null && Directory.Exists(_cachedSdkBuildConfigPath))
            {
                return _cachedSdkBuildConfigPath;
            }

            // 경로 검색
            string[] possiblePaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/BuildConfig~"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/BuildConfig~"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate/BuildConfig~")
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    _cachedSdkBuildConfigPath = path;
                    Debug.Log($"[AIT] SDK BuildConfig 경로 캐싱: {path}");
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// SDK 템플릿의 Runtime 폴더 경로를 반환합니다. (캐싱 사용)
        /// Package Only 실행 시 webgl/ 폴더에 Runtime이 없을 경우 폴백으로 사용됩니다.
        /// </summary>
        private static string FindSdkRuntimePath()
        {
            // 캐시된 경로가 유효한지 확인
            if (_cachedSdkRuntimePath != null && Directory.Exists(_cachedSdkRuntimePath))
            {
                return _cachedSdkRuntimePath;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] possiblePaths = new string[]
            {
                Path.Combine(projectRoot, "Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/Runtime"),
                Path.Combine(projectRoot, "Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/Runtime"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate/Runtime")
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    _cachedSdkRuntimePath = path;
                    Debug.Log($"[AIT] SDK Runtime 경로 캐싱: {path}");
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Unity WebGL 빌드를 public 폴더로 복사합니다.
        /// </summary>
        /// <returns>성공 시 SUCCEED, 실패 시 해당 에러 코드</returns>
        internal static AITConvertCore.AITExportError CopyWebGLToPublic(string webglPath, string buildProjectPath, AITBuildProfile profile = null)
        {
            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            var config = UnityUtil.GetEditorConf();

            // Unity WebGL 빌드를 Vite 프로젝트에 복사
            // - index.html: 프로젝트 루트 (Vite 요구사항)
            // - Build, TemplateData, Runtime: public 폴더 (정적 자산)
            string publicPath = Path.Combine(buildProjectPath, "public");

            // public 폴더 생성
            if (!Directory.Exists(publicPath))
            {
                Directory.CreateDirectory(publicPath);
            }

            // Build 폴더 → public/Build
            string buildSrc = Path.Combine(webglPath, "Build");
            string buildDest = Path.Combine(publicPath, "Build");
            if (Directory.Exists(buildSrc))
            {
                UnityUtil.CopyDirectory(buildSrc, buildDest);
            }

            // TemplateData 폴더 → public/TemplateData
            string templateDataSrc = Path.Combine(webglPath, "TemplateData");
            string templateDataDest = Path.Combine(publicPath, "TemplateData");
            if (Directory.Exists(templateDataSrc))
            {
                UnityUtil.CopyDirectory(templateDataSrc, templateDataDest);
            }

            // Runtime 폴더 → public/Runtime
            // 1순위: webgl/ 폴더에 Runtime이 있으면 사용 (AITTemplate 빌드)
            // 2순위: webgl/ 폴더에 Runtime이 없으면 SDK 템플릿에서 복사 (Package Only 지원)
            string runtimeSrc = Path.Combine(webglPath, "Runtime");
            string runtimeDest = Path.Combine(publicPath, "Runtime");
            if (Directory.Exists(runtimeSrc))
            {
                UnityUtil.CopyDirectory(runtimeSrc, runtimeDest);
            }
            else
            {
                // SDK 템플릿에서 Runtime 폴더 복사 (수동 WebGL 빌드 시 AITTemplate 미사용 대응)
                Debug.LogWarning("[AIT] WebGL 빌드에 Runtime 폴더가 없습니다. SDK 템플릿에서 복사합니다.");
                Debug.LogWarning("[AIT]    ⚠️ AITTemplate이 아닌 다른 템플릿으로 빌드되었을 수 있습니다.");
                string sdkRuntimePath = FindSdkRuntimePath();
                if (!string.IsNullOrEmpty(sdkRuntimePath) && Directory.Exists(sdkRuntimePath))
                {
                    UnityUtil.CopyDirectory(sdkRuntimePath, runtimeDest);
                    Debug.Log("[AIT] ✓ Runtime 폴더: SDK 템플릿에서 복사 완료");
                }
                else
                {
                    Debug.LogError("[AIT] Runtime 폴더를 찾을 수 없습니다. 'Build And Package'를 사용하세요.");
                }
            }

            // StreamingAssets 폴더 → public/StreamingAssets (있는 경우)
            string streamingAssetsSrc = Path.Combine(webglPath, "StreamingAssets");
            string streamingAssetsDest = Path.Combine(publicPath, "StreamingAssets");
            if (Directory.Exists(streamingAssetsSrc))
            {
                UnityUtil.CopyDirectory(streamingAssetsSrc, streamingAssetsDest);
            }

            // index.html → 프로젝트 루트 (Vite가 루트에서 index.html을 찾음)
            string indexSrc = Path.Combine(webglPath, "index.html");
            string indexDest = Path.Combine(buildProjectPath, "index.html");

            // index.html 필수 검증
            if (!File.Exists(indexSrc))
            {
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] ✗ 치명적: index.html을 찾을 수 없습니다!");
                Debug.LogError("[AIT] ========================================");
                Debug.LogError($"[AIT] 검색 경로: {indexSrc}");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 가능한 원인:");
                Debug.LogError("[AIT]   1. Unity WebGL 빌드가 완료되지 않았습니다.");
                Debug.LogError("[AIT]   2. WebGL 템플릿이 올바르게 설정되지 않았습니다.");
                Debug.LogError("[AIT]   3. 이전 빌드가 손상되었습니다.");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 해결 방법:");
                Debug.LogError("[AIT]   1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.");
                Debug.LogError("[AIT]   2. AIT > Clean 메뉴로 빌드 폴더를 삭제 후 재빌드하세요.");
                Debug.LogError("[AIT]   3. AIT > Regenerate WebGL Templates 실행 후 재빌드하세요.");
                Debug.LogError("[AIT] ========================================");
                return AITConvertCore.AITExportError.WEBGL_BUILD_INCOMPLETE;
            }

            {
                string indexContent = File.ReadAllText(indexSrc);

                // Build 폴더에서 실제 파일 이름 찾기
                // Unity 압축 설정에 따라 .unityweb, .gz, .br 확장자가 붙을 수 있음
                Debug.Log("[AIT] WebGL 빌드 파일 검색 중...");

                // 필수 파일들 (isRequired = true)
                string loaderFile = AITBuildValidator.FindFileInBuild(buildSrc, "*.loader.js", isRequired: true);
                string dataFile = AITBuildValidator.FindFileInBuild(buildSrc, "*.data*", isRequired: true);
                string frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, "*.framework.js*", isRequired: true);
                string wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, "*.wasm*", isRequired: true);

                // 선택적 파일 (isRequired = false)
                string symbolsFile = AITBuildValidator.FindFileInBuild(buildSrc, "*.symbols.json*", isRequired: false);

                // 필수 파일 검증 경고
                var missingFiles = new List<string>();
                if (string.IsNullOrEmpty(loaderFile)) missingFiles.Add("*.loader.js");
                if (string.IsNullOrEmpty(dataFile)) missingFiles.Add("*.data");
                if (string.IsNullOrEmpty(frameworkFile)) missingFiles.Add("*.framework.js");
                if (string.IsNullOrEmpty(wasmFile)) missingFiles.Add("*.wasm");

                if (missingFiles.Count > 0)
                {
                    Debug.LogError("[AIT] ========================================");
                    Debug.LogError("[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락!");
                    Debug.LogError("[AIT] ========================================");
                    Debug.LogError($"[AIT] 누락된 필수 파일: {string.Join(", ", missingFiles)}");
                    Debug.LogError("[AIT] ");
                    Debug.LogError("[AIT] 가능한 원인:");
                    Debug.LogError("[AIT]   1. Unity WebGL 빌드가 완료되지 않았습니다.");
                    Debug.LogError("[AIT]   2. WebGL 빌드가 실패했지만 부분 결과물만 남아있습니다.");
                    Debug.LogError("[AIT]   3. 빌드 설정(압축 방식 등)이 예상과 다릅니다.");
                    Debug.LogError("[AIT] ");
                    Debug.LogError("[AIT] 해결 방법:");
                    Debug.LogError("[AIT]   1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.");
                    Debug.LogError("[AIT]   2. Unity Console에서 빌드 에러를 확인하세요.");
                    Debug.LogError("[AIT] ========================================");
                    return AITConvertCore.AITExportError.WEBGL_BUILD_INCOMPLETE;
                }

                // 프로필 기반 설정값 (Mock 브릿지가 비활성화되면 프로덕션 모드로 간주)
                string isProduction = profile.enableMockBridge ? "false" : "true";
                string enableDebugConsole = profile.enableDebugConsole ? "true" : "false";

                // 프로젝트의 index.html에서 사용자 커스텀 섹션 추출 (있는 경우)
                string projectIndexPath = Path.Combine(Application.dataPath, "WebGLTemplates", "AITTemplate", "index.html");
                if (File.Exists(projectIndexPath))
                {
                    string projectIndexContent = File.ReadAllText(projectIndexPath);

                    // USER_HEAD 섹션 추출 및 교체
                    string userHeadSection = AITTemplateManager.ExtractHtmlUserSection(projectIndexContent, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END);
                    if (userHeadSection != null)
                    {
                        indexContent = AITTemplateManager.ReplaceHtmlUserSection(indexContent, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END, userHeadSection);
                        Debug.Log("[AIT] index.html USER_HEAD 섹션 머지됨");
                    }

                    // USER_BODY_END 섹션 추출 및 교체
                    string userBodyEndSection = AITTemplateManager.ExtractHtmlUserSection(projectIndexContent, AITTemplateManager.HTML_USER_BODY_END_START, AITTemplateManager.HTML_USER_BODY_END_END);
                    if (userBodyEndSection != null)
                    {
                        indexContent = AITTemplateManager.ReplaceHtmlUserSection(indexContent, AITTemplateManager.HTML_USER_BODY_END_START, AITTemplateManager.HTML_USER_BODY_END_END, userBodyEndSection);
                        Debug.Log("[AIT] index.html USER_BODY_END 섹션 머지됨");
                    }
                }

                // Unity 플레이스홀더 치환
                indexContent = indexContent
                    .Replace("%UNITY_WEB_NAME%", PlayerSettings.productName)
                    .Replace("%UNITY_WIDTH%", PlayerSettings.defaultWebScreenWidth.ToString())
                    .Replace("%UNITY_HEIGHT%", PlayerSettings.defaultWebScreenHeight.ToString())
                    .Replace("%UNITY_COMPANY_NAME%", PlayerSettings.companyName)
                    .Replace("%UNITY_PRODUCT_NAME%", PlayerSettings.productName)
                    .Replace("%UNITY_PRODUCT_VERSION%", PlayerSettings.bundleVersion)
                    // Unity 표준 URL 형식 (Unity가 치환하지 않은 경우 SDK가 처리)
                    .Replace("%UNITY_WEBGL_LOADER_URL%", $"Build/{loaderFile}")
                    .Replace("%UNITY_WEBGL_DATA_URL%", $"Build/{dataFile}")
                    .Replace("%UNITY_WEBGL_FRAMEWORK_URL%", $"Build/{frameworkFile}")
                    .Replace("%UNITY_WEBGL_CODE_URL%", $"Build/{wasmFile}")
                    .Replace("%UNITY_WEBGL_SYMBOLS_URL%", !string.IsNullOrEmpty(symbolsFile) ? $"Build/{symbolsFile}" : "")
                    // 하위 호환성을 위한 FILENAME 형식 (레거시)
                    .Replace("%UNITY_WEBGL_LOADER_FILENAME%", loaderFile)
                    .Replace("%UNITY_WEBGL_DATA_FILENAME%", dataFile)
                    .Replace("%UNITY_WEBGL_FRAMEWORK_FILENAME%", frameworkFile)
                    .Replace("%UNITY_WEBGL_CODE_FILENAME%", wasmFile)
                    .Replace("%UNITY_WEBGL_SYMBOLS_FILENAME%", symbolsFile)
                    // AIT 커스텀 플레이스홀더
                    .Replace("%AIT_IS_PRODUCTION%", isProduction)
                    .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", enableDebugConsole)
                    .Replace("%AIT_DEVICE_PIXEL_RATIO%", config.devicePixelRatio.ToString())
                    .Replace("%AIT_ICON_URL%", config.iconUrl ?? "")
                    .Replace("%AIT_DISPLAY_NAME%", config.displayName ?? "")
                    .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor ?? "#3182f6")
                    // 메트릭 전송 간격 (초 -> 밀리초 변환, 범위 제한 적용)
                    .Replace("%AIT_WEB_METRICS_INTERVAL_MS%", (Math.Max(10, Math.Min(60, config.webMetricsIntervalSec)) * 1000).ToString())
                    .Replace("%AIT_UNITY_METRICS_INTERVAL_MS%", (Math.Max(10, Math.Min(60, config.unityMetricsIntervalSec)) * 1000).ToString())
                    // Unity 엔진 버전 (상세 정보: major.minor.patch.commit, 예: 6000.2.14f1)
                    .Replace("%AIT_UNITY_VERSION%", Application.unityVersion)
                    // HTML5 Preload 태그 (로딩 성능 개선)
                    .Replace("%AIT_PRELOAD_TAGS%", GeneratePreloadTags(dataFile, wasmFile, frameworkFile));

                // 로딩 화면 삽입 (%AIT_LOADING_SCREEN% 플레이스홀더)
                string loadingContent = "";
                string projectLoadingPath = AITPackageInitializer.GetProjectLoadingPath();

                // 프로젝트의 loading.html 사용 (SDK 초기화 시 자동 생성됨)
                if (File.Exists(projectLoadingPath))
                {
                    loadingContent = File.ReadAllText(projectLoadingPath);
                    Debug.Log("[AIT] ✓ 로딩 화면 적용: " + projectLoadingPath);
                }
                else
                {
                    // 폴백: SDK 기본 템플릿 직접 사용 (초기화가 실행되지 않은 경우)
                    string sdkTemplatePath = AITPackageInitializer.GetSDKLoadingTemplatePath();
                    if (sdkTemplatePath != null)
                    {
                        loadingContent = File.ReadAllText(sdkTemplatePath);
                        Debug.Log("[AIT] ✓ SDK 기본 로딩 화면 적용");
                    }
                    else
                    {
                        Debug.LogWarning("[AIT] 로딩 화면 파일을 찾을 수 없습니다. 빈 로딩 화면이 사용됩니다.");
                    }
                }

                // %AIT_LOADING_SCREEN% 플레이스홀더 치환
                indexContent = indexContent.Replace("%AIT_LOADING_SCREEN%", loadingContent);

                File.WriteAllText(indexDest, indexContent, System.Text.Encoding.UTF8);
                Debug.Log("[AIT] index.html → 프로젝트 루트에 생성");

                // 플레이스홀더 치환 결과 검증
                if (!AITBuildValidator.ValidatePlaceholderSubstitution(indexContent, indexDest))
                {
                    return AITConvertCore.AITExportError.WEBGL_BUILD_INCOMPLETE;
                }
            }

            // Runtime/appsintoss-unity-bridge.js 파일도 치환
            string bridgeSrc = Path.Combine(publicPath, "Runtime", "appsintoss-unity-bridge.js");
            if (File.Exists(bridgeSrc))
            {
                // 프로필 기반 설정값 (Mock 브릿지가 비활성화되면 프로덕션 모드로 간주)
                string isProduction = profile.enableMockBridge ? "false" : "true";

                string bridgeContent = File.ReadAllText(bridgeSrc);
                bridgeContent = bridgeContent.Replace("%AIT_IS_PRODUCTION%", isProduction);
                File.WriteAllText(bridgeSrc, bridgeContent, System.Text.Encoding.UTF8);
                Debug.Log($"[AIT] appsintoss-unity-bridge.js Mock 브릿지 모드: {(profile.enableMockBridge ? "활성화" : "비활성화")}");
            }

            Debug.Log("[AIT] Unity WebGL 빌드 복사 완료");
            Debug.Log("[AIT]   - index.html → 프로젝트 루트");
            Debug.Log("[AIT]   - Build, TemplateData, Runtime → public/");

            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// Unity WebGL 리소스에 대한 preload 태그를 생성합니다.
        /// HTML5 preload를 사용하면 HTML 파싱과 동시에 리소스 다운로드가 시작되어 로딩 성능이 개선됩니다.
        /// </summary>
        /// <param name="dataFile">data 파일명 (예: Build.data.br)</param>
        /// <param name="wasmFile">wasm 파일명 (예: Build.wasm.br)</param>
        /// <param name="frameworkFile">framework 파일명 (예: Build.framework.js.br)</param>
        /// <returns>preload 태그 문자열 (줄바꿈 포함)</returns>
        private static string GeneratePreloadTags(string dataFile, string wasmFile, string frameworkFile)
        {
            var sb = new System.Text.StringBuilder();

            // 우선순위: data > wasm > framework (일반적으로 크기 순)
            // as="fetch" + crossorigin="anonymous": Unity가 fetch() API로 로드하므로 동일한 캐시 키 사용
            if (!string.IsNullOrEmpty(dataFile))
            {
                sb.AppendLine($"    <link rel=\"preload\" href=\"Build/{dataFile}\" as=\"fetch\" crossorigin=\"anonymous\">");
            }

            if (!string.IsNullOrEmpty(wasmFile))
            {
                sb.AppendLine($"    <link rel=\"preload\" href=\"Build/{wasmFile}\" as=\"fetch\" crossorigin=\"anonymous\">");
            }

            // framework.js는 preload하지 않음
            // Unity 로더가 framework.js를 <script> 태그로 로드하는 경우 as="fetch" preload와 캐시 키가 불일치하여
            // 이중 다운로드가 발생할 수 있음. 이는 메모리 압박을 증가시켜 간헐적 초기화 실패(ASM_CONSTS 오류)의
            // 확률을 높일 수 있으므로 framework.js preload를 제거함.

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// WebGL 빌드를 ait-build로 비동기 패키징 (non-blocking)
        /// Unity Editor를 차단하지 않고 pnpm install/build를 실행합니다.
        /// </summary>
        /// <param name="projectPath">Unity 프로젝트 경로</param>
        /// <param name="webglPath">WebGL 빌드 경로</param>
        /// <param name="profile">빌드 프로필</param>
        /// <param name="onComplete">완료 콜백</param>
        /// <param name="onProgress">진행 상황 콜백 (phase, progress, status)</param>
        /// <param name="cancellationToken">취소 토큰</param>
        internal static void PackageWebGLBuildAsync(
            string projectPath,
            string webglPath,
            AITBuildProfile profile,
            Action<AITConvertCore.AITExportError> onComplete,
            Action<AITConvertCore.BuildPhase, float, string> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            // 백그라운드 Node.js/pnpm 설치가 진행 중이면 완료될 때까지 대기
            if (AITPackageInitializer.IsInstalling)
            {
                onProgress?.Invoke(AITConvertCore.BuildPhase.Preparing, 0.01f, "Node.js/pnpm 설치 대기 중...");
                Debug.Log("[AIT] Node.js/pnpm 설치가 진행 중입니다. 완료될 때까지 대기합니다...");
                if (!AITPackageInitializer.WaitForInstallation())
                {
                    Debug.LogError("[AIT] 설치 대기 타임아웃. 빌드를 중단합니다.");
                    onComplete?.Invoke(AITConvertCore.AITExportError.NODE_NOT_FOUND);
                    return;
                }
            }

            // 취소 확인
            if (cancellationToken.IsCancellationRequested)
            {
                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                return;
            }

            Debug.Log("[AIT] Vite 기반 비동기 패키징 시작...");
            onProgress?.Invoke(AITConvertCore.BuildPhase.CopyingFiles, 0.05f, "패키징 준비 중...");

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            string buildProjectPath = Path.Combine(projectPath, "ait-build");

            // ait-build 폴더 정리
            PrepareAitBuildFolder(buildProjectPath);

            // npm 경로 찾기
            string npmPath = AITNpmRunner.FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("[AIT] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                onComplete?.Invoke(AITConvertCore.AITExportError.NODE_NOT_FOUND);
                return;
            }

            // 취소 확인
            if (cancellationToken.IsCancellationRequested)
            {
                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                return;
            }

            // Step 1: 파일 복사 (동기, 빠름)
            onProgress?.Invoke(AITConvertCore.BuildPhase.CopyingFiles, 0.1f, "빌드 설정 파일 복사 중...");
            Debug.Log("[AIT] Step 1/3: Vite 프로젝트 구조 생성 중...");
            CopyBuildConfigFromTemplate(buildProjectPath);

            // Step 2: Unity WebGL 빌드 복사
            onProgress?.Invoke(AITConvertCore.BuildPhase.CopyingFiles, 0.15f, "WebGL 빌드 복사 중...");
            Debug.Log("[AIT] Step 2/3: Unity WebGL 빌드 복사 중...");
            var copyResult = CopyWebGLToPublic(webglPath, buildProjectPath, profile);
            if (copyResult != AITConvertCore.AITExportError.SUCCEED)
            {
                onComplete?.Invoke(copyResult);
                return;
            }

            // 취소 확인
            if (cancellationToken.IsCancellationRequested)
            {
                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                return;
            }

            // pnpm 경로 찾기
            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPath))
            {
                Debug.LogError("[AIT] pnpm 설치에 실패했습니다. Unity Console에서 에러를 확인해주세요.");
                string nodejsPath = AITPlatformHelper.IsWindows
                    ? "%LOCALAPPDATA%\\ait-unity-sdk\\nodejs\\"
                    : "~/.ait-unity-sdk/nodejs/";
                AITPlatformHelper.ShowInfoDialog(
                    "빌드 실패",
                    "pnpm을 찾을 수 없습니다.\n\n" +
                    "해결 방법:\n" +
                    "1. 네트워크 연결을 확인하세요.\n" +
                    $"2. {nodejsPath} 폴더를 삭제 후 Unity를 재시작하세요.\n" +
                    "3. 방화벽/프록시가 nodejs.org를 차단하고 있는지 확인하세요.",
                    "확인");
                onComplete?.Invoke(AITConvertCore.AITExportError.FAIL_NPM_BUILD);
                return;
            }

            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");
            var finalProfile = profile; // 클로저용

            // node_modules 무결성 검증 (web-framework 버전 일치 확인)
            if (!ValidateNodeModulesIntegrity(buildProjectPath))
            {
                Debug.Log("[AIT] node_modules 무결성 검증 실패. 정리 후 재설치합니다.");
                CleanNodeModules(buildProjectPath);
            }

            // Step 3: pnpm install (비동기)
            onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.2f, "pnpm install 실행 중...");
            Debug.Log("[AIT] Step 3/3: pnpm install & build 실행 중...");

            RunPnpmInstallAsync(buildProjectPath, pnpmPath, localCachePath, cancellationToken,
                onOutputReceived: (line) =>
                {
                    onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.35f, line);
                },
                onComplete: (installResult) =>
                {
                    if (installResult != AITConvertCore.AITExportError.SUCCEED)
                    {
                        onComplete?.Invoke(installResult);
                        return;
                    }

                    // 취소 확인
                    if (cancellationToken.IsCancellationRequested)
                    {
                        onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                        return;
                    }

                    // Step 4: granite build (비동기)
                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.5f, "granite build 실행 중...");
                    Debug.Log("[AIT] granite build 실행 중...");

                    AITNpmRunner.RunNpmCommandWithCacheAsync(
                        buildProjectPath,
                        pnpmPath,
                        "run build",
                        localCachePath,
                        onComplete: (buildResult) =>
                        {
                            if (buildResult == AITConvertCore.AITExportError.SUCCEED)
                            {
                                string distPath = Path.Combine(buildProjectPath, "dist");
                                AITBuildValidator.PrintBuildReport(buildProjectPath, distPath);
                                onProgress?.Invoke(AITConvertCore.BuildPhase.Complete, 1f, "패키징 완료!");
                                Debug.Log($"[AIT] ✓ 비동기 패키징 완료: {distPath}");
                                onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                                return;
                            }

                            // 취소 확인
                            if (cancellationToken.IsCancellationRequested)
                            {
                                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                                return;
                            }

                            // MODULE_NOT_FOUND 등 런타임 에러 대응: node_modules 정리 후 install부터 재시도
                            Debug.LogWarning("[AIT] granite build 실패. node_modules 정리 후 install부터 재시도합니다...");
                            CleanNodeModules(buildProjectPath);

                            onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.5f, "빌드 실패 후 재설치 중...");

                            // install 재시도
                            AITNpmRunner.RunNpmCommandWithCacheAsync(
                                buildProjectPath,
                                pnpmPath,
                                "install --no-frozen-lockfile",
                                localCachePath,
                                onComplete: (retryInstallResult) =>
                                {
                                    if (retryInstallResult != AITConvertCore.AITExportError.SUCCEED)
                                    {
                                        Debug.LogError("[AIT] granite build 실패 후 pnpm install 재시도도 실패");
                                        onComplete?.Invoke(retryInstallResult);
                                        return;
                                    }

                                    // build 재시도
                                    onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.7f, "granite build 재시도 중...");

                                    AITNpmRunner.RunNpmCommandWithCacheAsync(
                                        buildProjectPath,
                                        pnpmPath,
                                        "run build",
                                        localCachePath,
                                        onComplete: (retryBuildResult) =>
                                        {
                                            if (retryBuildResult == AITConvertCore.AITExportError.SUCCEED)
                                            {
                                                string distPath2 = Path.Combine(buildProjectPath, "dist");
                                                AITBuildValidator.PrintBuildReport(buildProjectPath, distPath2);
                                                onProgress?.Invoke(AITConvertCore.BuildPhase.Complete, 1f, "패키징 완료!");
                                                Debug.Log($"[AIT] ✓ 비동기 패키징 완료 (재시도 성공): {distPath2}");
                                            }
                                            else
                                            {
                                                Debug.LogError("[AIT] granite build 재시도도 실패");
                                            }
                                            onComplete?.Invoke(retryBuildResult);
                                        },
                                        onOutputReceived: (line2) =>
                                        {
                                            onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.85f, line2);
                                        }
                                    );
                                },
                                onOutputReceived: (line2) =>
                                {
                                    onProgress?.Invoke(AITConvertCore.BuildPhase.PnpmInstall, 0.55f, line2);
                                }
                            );
                        },
                        onOutputReceived: (line) =>
                        {
                            onProgress?.Invoke(AITConvertCore.BuildPhase.GraniteBuild, 0.75f, line);
                        }
                    );
                }
            );
        }

        /// <summary>
        /// node_modules 무결성 검증
        /// package.json의 @apps-in-toss/web-framework 버전과 node_modules 내 설치 버전이 일치하는지 확인합니다.
        /// pnpm의 node_modules/.pnpm/@apps-in-toss+web-framework@{version} 디렉토리 존재 여부로 판단합니다.
        /// </summary>
        /// <param name="buildProjectPath">빌드 프로젝트 경로</param>
        /// <returns>true: 무결성 확인됨 또는 node_modules 없음, false: 버전 불일치로 정리 필요</returns>
        private static bool ValidateNodeModulesIntegrity(string buildProjectPath)
        {
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                // node_modules가 없으면 검증 불필요 (install 시 새로 생성됨)
                return true;
            }

            // package.json에서 web-framework 버전 읽기
            string packageJsonPath = Path.Combine(buildProjectPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                return true;
            }

            try
            {
                string packageJsonContent = File.ReadAllText(packageJsonPath);
                var packageJson = MiniJson.Deserialize(packageJsonContent) as Dictionary<string, object>;
                if (packageJson == null) return true;

                var dependencies = packageJson.ContainsKey("dependencies")
                    ? packageJson["dependencies"] as Dictionary<string, object>
                    : null;
                if (dependencies == null) return true;

                if (!dependencies.ContainsKey("@apps-in-toss/web-framework")) return true;

                string expectedVersion = dependencies["@apps-in-toss/web-framework"] as string;
                if (string.IsNullOrEmpty(expectedVersion)) return true;

                // ^ 또는 ~ 접두사 제거 (정확한 버전만 비교)
                expectedVersion = expectedVersion.TrimStart('^', '~');

                // pnpm의 node_modules/.pnpm/@apps-in-toss+web-framework@{version} 확인
                string pnpmDir = Path.Combine(nodeModulesPath, ".pnpm");
                if (!Directory.Exists(pnpmDir))
                {
                    // .pnpm 디렉토리가 없으면 오염된 상태
                    Debug.LogWarning("[AIT] node_modules/.pnpm 디렉토리가 없습니다. node_modules를 정리합니다.");
                    return false;
                }

                // @apps-in-toss+web-framework@{version}으로 시작하는 디렉토리 검색
                string expectedDirPrefix = $"@apps-in-toss+web-framework@{expectedVersion}";
                string[] matchingDirs = Directory.GetDirectories(pnpmDir, $"{expectedDirPrefix}*");

                if (matchingDirs.Length > 0)
                {
                    Debug.Log($"[AIT] ✓ node_modules 무결성 확인: web-framework@{expectedVersion}");
                    return true;
                }

                // 현재 설치된 버전 찾기 (로그용)
                string[] installedDirs = Directory.GetDirectories(pnpmDir, "@apps-in-toss+web-framework@*");
                if (installedDirs.Length > 0)
                {
                    string installedDirName = Path.GetFileName(installedDirs[0]);
                    Debug.LogWarning($"[AIT] web-framework 버전 불일치 감지!");
                    Debug.LogWarning($"[AIT]   기대 버전: {expectedVersion}");
                    Debug.LogWarning($"[AIT]   설치된 버전: {installedDirName}");
                    Debug.LogWarning($"[AIT]   node_modules를 정리하고 재설치합니다.");
                }
                else
                {
                    Debug.LogWarning($"[AIT] web-framework가 node_modules에 없습니다. node_modules를 정리합니다.");
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] node_modules 무결성 검증 중 오류 (무시됨): {e.Message}");
                return true; // 검증 실패 시 기존 동작 유지
            }
        }

        /// <summary>
        /// node_modules 및 캐시 정리
        /// </summary>
        /// <param name="buildProjectPath">빌드 프로젝트 경로</param>
        private static void CleanNodeModules(string buildProjectPath)
        {
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
            string npmCachePath = Path.Combine(buildProjectPath, ".npm-cache");

            if (Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[AIT] node_modules 삭제 중...");
                try
                {
                    AITFileUtils.DeleteDirectory(nodeModulesPath);
                    Debug.Log("[AIT] ✓ node_modules 삭제 완료");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT] node_modules 삭제 실패: {e.Message}");
                }
            }

            if (Directory.Exists(npmCachePath))
            {
                Debug.Log("[AIT] .npm-cache 삭제 중...");
                try
                {
                    AITFileUtils.DeleteDirectory(npmCachePath);
                    Debug.Log("[AIT] ✓ .npm-cache 삭제 완료");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT] .npm-cache 삭제 실패: {e.Message}");
                }
            }
        }

        /// <summary>
        /// ait-build 폴더 준비 (기존 결과물 정리)
        /// </summary>
        private static void PrepareAitBuildFolder(string buildProjectPath)
        {
            if (!Directory.Exists(buildProjectPath))
            {
                Directory.CreateDirectory(buildProjectPath);
                Debug.Log("[AIT] ait-build 폴더 생성");
            }
            else
            {
                Debug.Log("[AIT] 기존 빌드 결과물 정리 중... (node_modules와 설정 파일은 유지)");

                string[] itemsToKeep = new string[]
                {
                    "node_modules",
                    ".npm-cache",
                    "package.json",
                    "package-lock.json",
                    "pnpm-lock.yaml",
                    "granite.config.ts",
                    "vite.config.ts",
                    "tsconfig.json"
                };

                foreach (string item in Directory.GetFileSystemEntries(buildProjectPath))
                {
                    string itemName = Path.GetFileName(item);

                    bool shouldKeep = false;
                    foreach (string keepItem in itemsToKeep)
                    {
                        if (itemName == keepItem)
                        {
                            shouldKeep = true;
                            break;
                        }
                    }

                    if (shouldKeep) continue;

                    try
                    {
                        if (Directory.Exists(item))
                        {
                            AITFileUtils.DeleteDirectory(item);
                        }
                        else if (File.Exists(item))
                        {
                            File.Delete(item);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AIT] 삭제 실패: {itemName} - {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// pnpm install 비동기 실행 (frozen-lockfile 실패 시 재시도)
        /// </summary>
        private static void RunPnpmInstallAsync(
            string buildProjectPath,
            string pnpmPath,
            string localCachePath,
            CancellationToken cancellationToken,
            Action<string> onOutputReceived,
            Action<AITConvertCore.AITExportError> onComplete)
        {
            Debug.Log("[AIT] pnpm install --frozen-lockfile 실행 중...");

            AITNpmRunner.RunNpmCommandWithCacheAsync(
                buildProjectPath,
                pnpmPath,
                "install --frozen-lockfile",
                localCachePath,
                onComplete: (result) =>
                {
                    if (result == AITConvertCore.AITExportError.SUCCEED)
                    {
                        onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                        return;
                    }

                    // 취소 확인
                    if (cancellationToken.IsCancellationRequested)
                    {
                        onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                        return;
                    }

                    // frozen-lockfile 실패 시 재시도
                    Debug.LogWarning("[AIT] --frozen-lockfile 설치 실패, lockfile 갱신 모드로 재시도...");

                    AITNpmRunner.RunNpmCommandWithCacheAsync(
                        buildProjectPath,
                        pnpmPath,
                        "install --no-frozen-lockfile",
                        localCachePath,
                        onComplete: (retryResult) =>
                        {
                            if (retryResult == AITConvertCore.AITExportError.SUCCEED)
                            {
                                Debug.Log("[AIT] ✓ 새 패키지 설치 및 lockfile 갱신 완료");
                                onComplete?.Invoke(retryResult);
                                return;
                            }

                            // 취소 확인
                            if (cancellationToken.IsCancellationRequested)
                            {
                                onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                                return;
                            }

                            // 최종 재시도: node_modules 정리 후 clean install
                            Debug.LogWarning("[AIT] pnpm install 재시도 실패. node_modules 정리 후 한 번 더 시도합니다...");
                            CleanNodeModules(buildProjectPath);

                            AITNpmRunner.RunNpmCommandWithCacheAsync(
                                buildProjectPath,
                                pnpmPath,
                                "install --no-frozen-lockfile",
                                localCachePath,
                                onComplete: (cleanRetryResult) =>
                                {
                                    if (cleanRetryResult == AITConvertCore.AITExportError.SUCCEED)
                                    {
                                        Debug.Log("[AIT] ✓ clean install 성공");
                                    }
                                    else
                                    {
                                        Debug.LogError("[AIT] pnpm install 실패 (clean 재시도 후에도 실패)");
                                    }
                                    onComplete?.Invoke(cleanRetryResult);
                                },
                                onOutputReceived: onOutputReceived
                            );
                        },
                        onOutputReceived: onOutputReceived
                    );
                },
                onOutputReceived: onOutputReceived
            );
        }
    }
}
