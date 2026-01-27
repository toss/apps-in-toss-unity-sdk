using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// ait-build 패키징 담당 클래스
    /// </summary>
    internal static class AITPackageBuilder
    {
        /// <summary>
        /// WebGL 빌드를 ait-build로 패키징
        /// </summary>
        internal static AITConvertCore.AITExportError PackageWebGLBuild(string projectPath, string webglPath, AITBuildProfile profile = null)
        {
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
                return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
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
                    Debug.LogError("[AIT] pnpm install 실패");
                    return installResult;
                }

                Debug.Log("[AIT] ✓ 새 패키지 설치 및 lockfile 갱신 완료");
            }

            // granite build 실행 (web 폴더를 dist로 복사)
            Debug.Log("[AIT] granite build 실행 중...");

            var buildResult = AITNpmRunner.RunNpmCommandWithCache(buildProjectPath, pnpmPath, "run build", localCachePath, "granite build 실행 중...");

            if (buildResult != AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.LogError("[AIT] granite build 실패");
                return buildResult;
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

            // SDK의 BuildConfig 템플릿 경로 찾기
            Debug.Log("[AIT] SDK BuildConfig 템플릿 경로 검색 중...");
            string[] possibleSdkPaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/BuildConfig~"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/BuildConfig~"), // 레거시 호환성
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate/BuildConfig~")
            };

            string sdkBuildConfigPath = null;
            for (int i = 0; i < possibleSdkPaths.Length; i++)
            {
                string path = possibleSdkPaths[i];
                bool exists = Directory.Exists(path);
                Debug.Log($"[AIT]   경로 {i + 1}/{possibleSdkPaths.Length}: {(exists ? "✓ 발견" : "✗ 없음")} - {path}");

                if (exists && sdkBuildConfigPath == null)
                {
                    sdkBuildConfigPath = path;
                }
            }

            if (sdkBuildConfigPath == null)
            {
                Debug.LogError("[AIT] SDK BuildConfig 폴더를 찾을 수 없습니다.");
                Debug.LogError("[AIT] 위의 경로들을 확인해주세요. SDK가 올바르게 설치되었는지 확인하세요.");
                return;
            }

            Debug.Log($"[AIT] ✓ SDK BuildConfig 템플릿 발견: {sdkBuildConfigPath}");

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
        /// SDK 템플릿의 Runtime 폴더 경로를 찾습니다.
        /// Package Only 실행 시 webgl/ 폴더에 Runtime이 없을 경우 폴백으로 사용됩니다.
        /// </summary>
        private static string FindSdkRuntimePath()
        {
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
                    .Replace("%AIT_UNITY_VERSION%", Application.unityVersion);

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
    }
}
