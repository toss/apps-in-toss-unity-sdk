using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss
{
    // JsonUtility를 위한 직렬화 가능한 설정 클래스들
    [System.Serializable]
    public class AppConfigWindow
    {
        public string navigationBarTitleText;
        public string backgroundColor;
    }

    [System.Serializable]
    public class AppConfigData
    {
        public string appId;
        public string appName;
        public string version;
        public string description;
        public string[] pages;
        public AppConfigWindow window;
        public string[] permissions;
        public string[] plugins;
    }

    [System.Serializable]
    public class PaymentConfigData
    {
        public string merchantId;
        public string clientKey;
        public string environment;
    }

    [System.Serializable]
    public class AdConfigData
    {
        public bool enabled;
        public string bannerId;
        public string interstitialId;
        public string rewardedId;
    }

    public class AITConvertCore
    {
        // 빌드 취소 플래그
        private static bool isCancelled = false;

        static AITConvertCore()
        {

        }

        /// <summary>
        /// 빌드 취소 요청
        /// </summary>
        public static void CancelBuild()
        {
            isCancelled = true;
            Debug.Log("[AIT] 빌드 취소 요청됨");
        }

        /// <summary>
        /// 빌드 취소 플래그 리셋
        /// </summary>
        public static void ResetCancellation()
        {
            isCancelled = false;
        }

        /// <summary>
        /// 빌드가 취소되었는지 확인
        /// </summary>
        public static bool IsCancelled()
        {
            return isCancelled;
        }

        public static void Init()
        {
            // WebGL 템플릿 복사 (필요한 경우)
            EnsureWebGLTemplatesExist();

            // 템플릿이 복사되었을 경우 Unity가 인식하도록 강제 리프레시
            AssetDatabase.Refresh();

            string templateHeader = "PROJECT:";
            var editorConfig = UnityUtil.GetEditorConf();

            // Unity 버전별 최적화 프리셋 자동 적용
            Debug.Log($"[AIT] 현재 Unity 버전: {AITBuildPresets.GetUnityVersionInfo()}");

            if (editorConfig.enableOptimization)
            {
                AITBuildPresets.ApplyOptimalSettings();
            }
            else
            {
                // 최소 설정만 적용
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            }

            // Apps in Toss 플랫폼에 맞는 WebGL 기본 설정
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.runInBackground = false;

#if UNITY_2022_3_OR_NEWER
            PlayerSettings.WebGL.template = $"{templateHeader}AITTemplate2022";
#elif UNITY_2020_1_OR_NEWER
            PlayerSettings.WebGL.template = $"{templateHeader}AITTemplate2020";
#else
            PlayerSettings.WebGL.template = $"{templateHeader}AITTemplate";
#endif

            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.dataCaching = false;

            // Apps in Toss 플랫폼 특화 설정
            PlayerSettings.companyName = "Apps in Toss";
            PlayerSettings.defaultCursor = null;
            PlayerSettings.cursorHotspot = Vector2.zero;

            // 설정 요약 로그
            Debug.Log($"[AIT] WebGL Template: {PlayerSettings.WebGL.template}");
            Debug.Log($"[AIT] 압축 포맷: {PlayerSettings.WebGL.compressionFormat}");
            Debug.Log($"[AIT] 메모리 크기: {PlayerSettings.WebGL.memorySize}MB");
        }

        private static void EnsureWebGLTemplatesExist()
        {
            // 프로젝트의 Assets/WebGLTemplates 경로
            string projectTemplatesPath = Path.Combine(Application.dataPath, "WebGLTemplates");
            string projectTemplate2022 = Path.Combine(projectTemplatesPath, "AITTemplate2022");

            // 이미 존재하고 index.html이 있으면 복사 불필요
            if (Directory.Exists(projectTemplate2022) && File.Exists(Path.Combine(projectTemplate2022, "index.html")))
            {
                return;
            }

            // SDK의 WebGLTemplates 경로 찾기 (여러 가능한 경로 시도)
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] possibleSdkPaths = new string[]
            {
                // Package로 설치된 경우 (Unity 프로젝트 루트 기준)
                Path.Combine(projectRoot, "Packages/com.appsintoss.miniapp/WebGLTemplates"),
                // Assembly 경로 기반
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates")
            };

            string sdkTemplatesPath = null;
            foreach (string path in possibleSdkPaths)
            {
                Debug.Log($"[AIT] SDK 경로 확인 중: {path}");
                if (Directory.Exists(path))
                {
                    sdkTemplatesPath = path;
                    Debug.Log($"[AIT] SDK WebGLTemplates 발견: {path}");
                    break;
                }
            }

            if (sdkTemplatesPath == null)
            {
                Debug.LogError($"[AIT] SDK WebGLTemplates 폴더를 찾을 수 없습니다.");
                return;
            }

            // AITTemplate2022 복사
            string sdkTemplate2022 = Path.Combine(sdkTemplatesPath, "AITTemplate2022");

            if (Directory.Exists(sdkTemplate2022))
            {
                Debug.Log("[AIT] WebGLTemplates를 프로젝트로 복사 중...");

                // 기존 폴더가 비어있으면 삭제
                if (Directory.Exists(projectTemplate2022))
                {
                    Directory.Delete(projectTemplate2022, true);
                }

                Directory.CreateDirectory(projectTemplatesPath);
                UnityUtil.CopyDirectory(sdkTemplate2022, projectTemplate2022);
                Debug.Log("[AIT] ✓ WebGLTemplates 복사 완료");
            }
            else
            {
                Debug.LogError($"[AIT] SDK 템플릿 폴더를 찾을 수 없습니다: {sdkTemplate2022}");
            }
        }

        public enum AITExportError
        {
            SUCCEED = 0,
            NODE_NOT_FOUND = 1,
            BUILD_WEBGL_FAILED = 2,
            INVALID_APP_CONFIG = 3,
            NETWORK_ERROR = 4,
            CANCELLED = 5,
        }

        /// <summary>
        /// 에러 코드를 사용자 친화적 메시지로 변환
        /// </summary>
        public static string GetErrorMessage(AITExportError error)
        {
            switch (error)
            {
                case AITExportError.SUCCEED:
                    return "성공";

                case AITExportError.NODE_NOT_FOUND:
                    return "Node.js를 찾을 수 없습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. https://nodejs.org 에서 Node.js 설치\n" +
                           "2. Unity Editor 재시작\n" +
                           "3. 터미널에서 'node --version' 확인";

                case AITExportError.BUILD_WEBGL_FAILED:
                    return "WebGL 빌드에 실패했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Unity Console 창에서 에러 메시지 확인\n" +
                           "2. WebGL Build Support가 설치되어 있는지 확인\n" +
                           "3. 프로젝트에 빌드 오류가 없는지 확인\n" +
                           "4. File > Build Settings > WebGL에서 직접 빌드 시도";

                case AITExportError.INVALID_APP_CONFIG:
                    return "앱 설정이 올바르지 않습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Apps in Toss > Build & Deploy Window 열기\n" +
                           "2. 설정 섹션에서 아이콘 URL 입력 (필수)\n" +
                           "3. 앱 ID, 버전 등 기본 정보 확인";

                case AITExportError.NETWORK_ERROR:
                    return "네트워크 오류가 발생했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 인터넷 연결 확인\n" +
                           "2. npm 레지스트리 접속 가능 여부 확인\n" +
                           "3. 방화벽 또는 프록시 설정 확인";

                case AITExportError.CANCELLED:
                    return "사용자에 의해 빌드가 취소되었습니다.";

                default:
                    return $"알 수 없는 오류가 발생했습니다. (코드: {error})";
            }
        }

        public static AITEditorScriptObject config => UnityUtil.GetEditorConf();

        public static string defaultTemplateDir => "appsintoss-default";
        public static string webglDir = "webgl";
        public static string miniGameDir = "miniapp";
        public static string audioDir = "Assets";
        public static string frameworkDir = "framework";
        public static string dataFileSize = string.Empty;
        public static string codeMd5 = string.Empty;
        public static string dataMd5 = string.Empty;
        public static string defaultImgSrc = "Assets/AppsInToss-SDK/Runtime/appsintoss-default/images/background.jpg";

        public static bool UseIL2CPP
        {
            get
            {
#pragma warning disable CS0618
                return PlayerSettings.GetScriptingBackend(BuildTargetGroup.WebGL) == ScriptingImplementation.IL2CPP;
#pragma warning restore CS0618
            }
        }

        /// <summary>
        /// Apps in Toss 미니앱으로 변환 실행
        /// </summary>
        /// <param name="buildWebGL">WebGL 빌드 실행 여부</param>
        /// <param name="doPackaging">패키징 실행 여부</param>
        /// <returns>변환 결과</returns>
        public static AITExportError DoExport(bool buildWebGL = true, bool doPackaging = true)
        {
            // 빌드 시작 전 취소 플래그 리셋
            ResetCancellation();

            Init();

            Debug.Log("Apps in Toss 미니앱 변환을 시작합니다...");

            var config = UnityUtil.GetEditorConf();
            if (config == null)
            {
                Debug.LogError("Apps in Toss 설정을 찾을 수 없습니다.");
                return AITExportError.INVALID_APP_CONFIG;
            }

            try
            {
                if (buildWebGL)
                {
                    // 취소 확인
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        return AITExportError.CANCELLED;
                    }

                    var webglResult = BuildWebGL();
                    if (webglResult != AITExportError.SUCCEED)
                    {
                        return webglResult;
                    }
                }

                // Apps in Toss 미니앱 패키지 생성
                if (doPackaging)
                {
                    // 취소 확인
                    if (IsCancelled())
                    {
                        Debug.LogWarning("[AIT] 빌드가 취소되었습니다.");
                        return AITExportError.CANCELLED;
                    }

                    var exportResult = GenerateMiniAppPackage();
                    if (exportResult != AITExportError.SUCCEED)
                    {
                        return exportResult;
                    }
                }

                Debug.Log("Apps in Toss 미니앱 변환이 완료되었습니다!");
                return AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                Debug.LogError($"변환 중 오류가 발생했습니다: {e.Message}");
                return AITExportError.BUILD_WEBGL_FAILED;
            }
        }

        private static AITExportError BuildWebGL()
        {
            Debug.Log("WebGL 빌드를 시작합니다...");

            string[] scenes = UnityUtil.GetBuildScenes();
            string outputPath = Path.Combine(UnityUtil.GetProjectPath(), webglDir);

            // 기존 WebGL 빌드 폴더 정리
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            var config = UnityUtil.GetEditorConf();

            // 빌드 옵션 설정
            BuildOptions buildOptions = BuildOptions.None;

            if (config.enableOptimization)
            {
                buildOptions |= BuildOptions.CompressWithLz4;  // LZ4 압축으로 빌드 속도 향상
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = buildOptions,
#if UNITY_2023_3_OR_NEWER
                // Unity 2023.3+ (Unity 6): 최신 기능 활성화 (문서 권장)
                extraScriptingDefines = new[]
                {
                    "APPSINTOS_OPTIMIZED",
                    "WEBGL_2_0",
                    "UNITY_6_FEATURES"
                }
#elif UNITY_2022_3_OR_NEWER
                // Unity 2022.3+: WebGL 2.0 최적화 (문서 권장)
                extraScriptingDefines = new[]
                {
                    "APPSINTOS_OPTIMIZED",
                    "WEBGL_2_0"
                }
#endif
            };

            var result = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("WebGL 빌드가 실패했습니다.");
                return AITExportError.BUILD_WEBGL_FAILED;
            }

            Debug.Log("WebGL 빌드가 완료되었습니다.");
            return AITExportError.SUCCEED;
        }

        private static AITExportError GenerateMiniAppPackage()
        {
            Debug.Log("Apps in Toss 미니앱 패키지를 생성합니다...");

            // 설정 검증
            var config = UnityUtil.GetEditorConf();
            if (string.IsNullOrWhiteSpace(config.iconUrl))
            {
                Debug.LogError("[AIT] 아이콘 URL이 설정되지 않았습니다. Apps in Toss Build Window에서 아이콘 URL을 설정해주세요.");
                EditorUtility.DisplayDialog(
                    "아이콘 URL 필요",
                    "앱 아이콘 URL이 설정되지 않았습니다.\n\n" +
                    "Apps in Toss > Build & Deploy Window를 열어서\n" +
                    "브랜드 설정 > 아이콘 URL을 입력해주세요.\n\n" +
                    "예: https://your-domain.com/icon.png",
                    "확인"
                );
                return AITExportError.INVALID_APP_CONFIG;
            }

            string projectPath = UnityUtil.GetProjectPath();
            string webglPath = Path.Combine(projectPath, webglDir);

            if (!Directory.Exists(webglPath))
            {
                Debug.LogError("WebGL 빌드 결과를 찾을 수 없습니다. WebGL 빌드를 먼저 실행하세요.");
                return AITExportError.BUILD_WEBGL_FAILED;
            }

            // WebGL 빌드 결과를 ait-build로 복사
            var packageResult = PackageWebGLBuild(projectPath, webglPath);
            if (packageResult != AITExportError.SUCCEED)
            {
                return packageResult;
            }

            Debug.Log("Apps in Toss 미니앱이 생성되었습니다!");
            return AITExportError.SUCCEED;
        }

        private static AITExportError PackageWebGLBuild(string projectPath, string webglPath)
        {
            Debug.Log("[AIT] Vite 기반 빌드 패키징 시작...");

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
                            DeleteDirectory(item);
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
            string npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("[AIT] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
                return AITExportError.NODE_NOT_FOUND;
            }

            // 1. Vite 프로젝트 구조 생성 (템플릿에서 복사)
            Debug.Log("[AIT] Step 1/3: Vite 프로젝트 구조 생성 중...");
            CopyBuildConfigFromTemplate(buildProjectPath);

            // 2. Unity WebGL 빌드를 public 폴더로 복사
            Debug.Log("[AIT] Step 2/3: Unity WebGL 빌드 복사 중...");
            CopyWebGLToPublic(webglPath, buildProjectPath);

            // 3. npm install 및 build 실행
            Debug.Log("[AIT] Step 3/3: npm install & build 실행 중...");
            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");

            // node_modules가 없는 경우에만 npm install 실행
            if (!Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[AIT] node_modules가 없습니다. npm install을 실행합니다 (1-2분 소요)...");
                var installResult = RunNpmCommandWithCache(buildProjectPath, npmPath, "install", localCachePath, "npm install 실행 중...");

                if (installResult != AITExportError.SUCCEED)
                {
                    Debug.LogError("[AIT] npm install 실패");
                    return installResult;
                }
            }
            else
            {
                // 캐시 통계 출력
                LogBuildCacheStats(buildProjectPath);
                Debug.Log("[AIT] node_modules가 존재합니다. npm install을 건너뜁니다.");
            }

            // granite build 실행 (web 폴더를 dist로 복사)
            Debug.Log("[AIT] granite build 실행 중...");
            var buildResult = RunNpmCommandWithCache(buildProjectPath, npmPath, "run build", localCachePath, "granite build 실행 중...");

            if (buildResult != AITExportError.SUCCEED)
            {
                Debug.LogError("[AIT] granite build 실패");
                return buildResult;
            }

            string distPath = Path.Combine(buildProjectPath, "dist");

            Debug.Log($"[AIT] ✓ 패키징 완료: {distPath}");

            // dist 폴더 열기
            if (Directory.Exists(distPath))
            {
                EditorUtility.RevealInFinder(distPath);
            }

            return AITExportError.SUCCEED;
        }

        private static void CopyBuildConfigFromTemplate(string buildProjectPath)
        {
            // SDK의 BuildConfig 템플릿 경로 찾기
            string[] possibleSdkPaths = new string[]
            {
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate2022/BuildConfig"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate2022/BuildConfig")
            };

            string sdkBuildConfigPath = null;
            foreach (string path in possibleSdkPaths)
            {
                if (Directory.Exists(path))
                {
                    sdkBuildConfigPath = path;
                    break;
                }
            }

            if (sdkBuildConfigPath == null)
            {
                Debug.LogError("[AIT] SDK BuildConfig 폴더를 찾을 수 없습니다.");
                return;
            }

            var config = UnityUtil.GetEditorConf();

            // package.json 복사 (치환 없음)
            File.Copy(
                Path.Combine(sdkBuildConfigPath, "package.json"),
                Path.Combine(buildProjectPath, "package.json"),
                true
            );

            // vite.config.ts 복사 (치환 없음)
            File.Copy(
                Path.Combine(sdkBuildConfigPath, "vite.config.ts"),
                Path.Combine(buildProjectPath, "vite.config.ts"),
                true
            );

            // tsconfig.json 복사 (치환 없음)
            File.Copy(
                Path.Combine(sdkBuildConfigPath, "tsconfig.json"),
                Path.Combine(buildProjectPath, "tsconfig.json"),
                true
            );

            // granite.config.ts 복사 및 플레이스홀더 치환
            string graniteConfigTemplate = File.ReadAllText(Path.Combine(sdkBuildConfigPath, "granite.config.ts"));
            string graniteConfig = graniteConfigTemplate
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ICON_URL%", config.iconUrl)
                .Replace("%AIT_LOCAL_PORT%", config.localPort.ToString());

            File.WriteAllText(
                Path.Combine(buildProjectPath, "granite.config.ts"),
                graniteConfig,
                new System.Text.UTF8Encoding(false)
            );

            Debug.Log("[AIT] 빌드 설정 파일 복사 및 치환 완료");
        }

        private static void CopyWebGLToPublic(string webglPath, string buildProjectPath)
        {
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

            // Runtime 폴더 → public/Runtime (있는 경우)
            string runtimeSrc = Path.Combine(webglPath, "Runtime");
            string runtimeDest = Path.Combine(publicPath, "Runtime");
            if (Directory.Exists(runtimeSrc))
            {
                UnityUtil.CopyDirectory(runtimeSrc, runtimeDest);
            }

            // index.html → 프로젝트 루트 (Vite가 루트에서 index.html을 찾음)
            string indexSrc = Path.Combine(webglPath, "index.html");
            string indexDest = Path.Combine(buildProjectPath, "index.html");
            if (File.Exists(indexSrc))
            {
                string indexContent = File.ReadAllText(indexSrc);

                // Build 폴더에서 실제 파일 이름 찾기
                string loaderFile = FindFileInBuild(buildSrc, "*.loader.js");
                string dataFile = FindFileInBuild(buildSrc, "*.data");
                string frameworkFile = FindFileInBuild(buildSrc, "*.framework.js");
                string wasmFile = FindFileInBuild(buildSrc, "*.wasm");
                string symbolsFile = FindFileInBuild(buildSrc, "*.symbols.json");

                // AIT Config에서 프로덕션 모드 가져오기
                var aitConfig = UnityUtil.GetEditorConf();
                string isProduction = aitConfig.isProduction ? "true" : "false";

                // Unity 플레이스홀더 치환
                indexContent = indexContent
                    .Replace("%UNITY_WEB_NAME%", PlayerSettings.productName)
                    .Replace("%UNITY_WIDTH%", PlayerSettings.defaultWebScreenWidth.ToString())
                    .Replace("%UNITY_HEIGHT%", PlayerSettings.defaultWebScreenHeight.ToString())
                    .Replace("%UNITY_COMPANY_NAME%", PlayerSettings.companyName)
                    .Replace("%UNITY_PRODUCT_NAME%", PlayerSettings.productName)
                    .Replace("%UNITY_PRODUCT_VERSION%", PlayerSettings.bundleVersion)
                    .Replace("%UNITY_WEBGL_LOADER_URL%", $"Build/{loaderFile}")
                    .Replace("%UNITY_WEBGL_LOADER_FILENAME%", loaderFile)
                    .Replace("%UNITY_WEBGL_DATA_FILENAME%", dataFile)
                    .Replace("%UNITY_WEBGL_FRAMEWORK_FILENAME%", frameworkFile)
                    .Replace("%UNITY_WEBGL_CODE_FILENAME%", wasmFile)
                    .Replace("%UNITY_WEBGL_SYMBOLS_FILENAME%", symbolsFile)
                    .Replace("%AIT_IS_PRODUCTION%", isProduction);

                File.WriteAllText(indexDest, indexContent, System.Text.Encoding.UTF8);
                Debug.Log("[AIT] index.html → 프로젝트 루트에 생성");
            }

            // Runtime/appsintoss-unity-bridge.js 파일도 치환
            string bridgeSrc = Path.Combine(publicPath, "Runtime", "appsintoss-unity-bridge.js");
            if (File.Exists(bridgeSrc))
            {
                var aitConfig = UnityUtil.GetEditorConf();
                string isProduction = aitConfig.isProduction ? "true" : "false";

                string bridgeContent = File.ReadAllText(bridgeSrc);
                bridgeContent = bridgeContent.Replace("%AIT_IS_PRODUCTION%", isProduction);
                File.WriteAllText(bridgeSrc, bridgeContent, System.Text.Encoding.UTF8);
                Debug.Log($"[AIT] appsintoss-unity-bridge.js 프로덕션 모드 치환: {isProduction}");
            }

            Debug.Log("[AIT] Unity WebGL 빌드 복사 완료");
            Debug.Log("[AIT]   - index.html → 프로젝트 루트");
            Debug.Log("[AIT]   - Build, TemplateData, Runtime → public/");
        }

        private static string FindFileInBuild(string buildPath, string pattern)
        {
            if (!Directory.Exists(buildPath))
                return "";

            var files = Directory.GetFiles(buildPath, pattern);
            if (files.Length > 0)
            {
                return Path.GetFileName(files[0]);
            }
            return "";
        }

        private static string FindNpmPath()
        {
            // 1. 시스템 설치 npm 우선 사용
            string systemNpm = FindSystemNpm();
            if (!string.IsNullOrEmpty(systemNpm))
            {
                Debug.Log($"[npm] 시스템 npm 사용: {systemNpm}");
                return systemNpm;
            }

            // 2. Embedded portable Node.js 사용 (자동 다운로드)
            string embeddedNpm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
            if (!string.IsNullOrEmpty(embeddedNpm))
            {
                Debug.Log($"[npm] Embedded npm 사용: {embeddedNpm}");
                return embeddedNpm;
            }

            // 3. 둘 다 없으면 에러
            Debug.LogError("[npm] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
            return null;
        }

        private static string FindSystemNpm()
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
                    Debug.Log($"[npm] 시스템 npm 발견: {path}");
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
                    Debug.Log($"[npm] which로 시스템 npm 발견: {output}");
                    return output;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[npm] which npm 실행 실패: {e.Message}");
            }

            return null;
        }

        private static AITExportError RunNpmCommandWithCache(string workingDirectory, string npmPath, string arguments, string cachePath, string progressTitle)
        {
            string npmDir = Path.GetDirectoryName(npmPath);
            string pathEnv = $"{npmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";

            string fullCommand = $"cd '{workingDirectory}' && '{npmPath}' {arguments} --cache '{cachePath}' --prefer-offline false";

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-l -c \"export PATH='{pathEnv}' && {fullCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();

                System.Text.StringBuilder output = new System.Text.StringBuilder();
                System.Text.StringBuilder error = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, args) => {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                        Debug.Log($"[npm] {args.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, args) => {
                    if (args.Data != null && !args.Data.Contains("npm WARN"))
                    {
                        error.AppendLine(args.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 프로세스 완료를 대기하되, 진행 상황을 업데이트
                int maxWaitSeconds = 300; // 5분
                int elapsedSeconds = 0;

                while (!process.HasExited && elapsedSeconds < maxWaitSeconds)
                {
                    float progress = (float)elapsedSeconds / maxWaitSeconds;
                    EditorUtility.DisplayProgressBar("Apps in Toss",
                        $"{progressTitle} ({elapsedSeconds}초 경과)",
                        progress);

                    System.Threading.Thread.Sleep(1000); // 1초 대기
                    elapsedSeconds++;
                }

                EditorUtility.ClearProgressBar();

                if (!process.HasExited)
                {
                    process.Kill();
                    Debug.LogError($"[npm] 명령 시간 초과: npm {arguments}");
                    return AITExportError.BUILD_WEBGL_FAILED;
                }

                // 프로세스가 종료될 때까지 대기
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[npm] 명령 실패 (Exit Code: {process.ExitCode}): npm {arguments}");
                    Debug.LogError($"[npm] 오류:\n{error}");
                    return AITExportError.BUILD_WEBGL_FAILED;
                }

                return AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[npm] 명령 실행 오류: {e.Message}");
                return AITExportError.NODE_NOT_FOUND;
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
                return;

            // 모든 파일의 읽기 전용 속성 제거
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch { }
            }

            // 모든 하위 폴더 삭제
            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(dir, false);
                }
                catch { }
            }

            // 최상위 폴더 삭제
            try
            {
                Directory.Delete(path, true);
            }
            catch { }
        }

        /// <summary>
        /// 빌드 캐시 통계 출력
        /// </summary>
        private static void LogBuildCacheStats(string buildProjectPath)
        {
            try
            {
                var nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");
                var npmCachePath = Path.Combine(buildProjectPath, ".npm-cache");

                if (Directory.Exists(nodeModulesPath))
                {
                    long nodeModulesSize = GetDirectorySize(nodeModulesPath);
                    int packageCount = Directory.GetDirectories(nodeModulesPath).Length;

                    Debug.Log($"[AIT] ✓ 빌드 캐시 사용 중:");
                    Debug.Log($"[AIT]   - node_modules: {nodeModulesSize / 1024 / 1024}MB ({packageCount}개 패키지)");
                    Debug.Log($"[AIT]   - npm install 건너뜀 → 약 1-2분 절약!");
                }

                if (Directory.Exists(npmCachePath))
                {
                    long npmCacheSize = GetDirectorySize(npmCachePath);
                    Debug.Log($"[AIT]   - npm 캐시: {npmCacheSize / 1024 / 1024}MB");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 캐시 통계 출력 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 디렉토리 크기 계산
        /// </summary>
        private static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            long size = 0;
            try
            {
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        size += fileInfo.Length;
                    }
                    catch { }
                }
            }
            catch { }

            return size;
        }
    }

    /// <summary>
    /// 유틸리티 클래스
    /// </summary>
    public static class UnityUtil
    {
        public static string GetProjectPath()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        public static string[] GetBuildScenes()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }
            return scenes.ToArray();
        }

        public static AITEditorScriptObject GetEditorConf()
        {
            string configPath = "Assets/AppsInToss/Editor/AITConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<AITEditorScriptObject>(configPath);

            if (config == null)
            {
                // 기본 설정 생성
                config = ScriptableObject.CreateInstance<AITEditorScriptObject>();

                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(config, configPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
            }
        }
    }

    /// <summary>
    /// Settings Helper Interface for Apps in Toss
    /// </summary>
    public static class AITSettingsHelperInterface
    {
        public static IAITSettingsHelper helper = new AITSettingsHelper();
    }

    public interface IAITSettingsHelper
    {
        void OnFocus();
        void OnLostFocus();
        void OnDisable();
        void OnSettingsGUI(EditorWindow window);
        void OnBuildButtonGUI(EditorWindow window);
    }

    public class AITSettingsHelper : IAITSettingsHelper
    {
        private Vector2 scrollPosition;
        private AITEditorScriptObject config;

        public void OnFocus()
        {
            config = UnityUtil.GetEditorConf();
        }

        public void OnLostFocus()
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }

        public void OnDisable()
        {
            OnLostFocus();
        }

        public void OnSettingsGUI(EditorWindow window)
        {
            if (config == null)
                config = UnityUtil.GetEditorConf();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);
            GUILayout.Label("Apps in Toss 미니앱 설정", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // 앱 기본 정보
            GUILayout.Label("앱 기본 정보", EditorStyles.boldLabel);
            config.appName = EditorGUILayout.TextField("앱 이름", config.appName);
            config.version = EditorGUILayout.TextField("버전", config.version);
            config.description = EditorGUILayout.TextArea(config.description, GUILayout.Height(60));

            GUILayout.Space(10);

            // 빌드 설정
            GUILayout.Label("빌드 설정", EditorStyles.boldLabel);
            config.isProduction = EditorGUILayout.Toggle("프로덕션 모드", config.isProduction);
            config.enableOptimization = EditorGUILayout.Toggle("최적화 활성화", config.enableOptimization);
            config.enableCompression = EditorGUILayout.Toggle("압축 활성화", config.enableCompression);

            GUILayout.Space(10);

            // 토스페이 설정
            GUILayout.Label("토스페이 설정", EditorStyles.boldLabel);
            config.tossPayMerchantId = EditorGUILayout.TextField("가맹점 ID", config.tossPayMerchantId);
            config.tossPayClientKey = EditorGUILayout.TextField("클라이언트 키", config.tossPayClientKey);

            GUILayout.Space(10);

            // 광고 설정
            GUILayout.Label("광고 설정", EditorStyles.boldLabel);
            config.enableAdvertisement = EditorGUILayout.Toggle("광고 활성화", config.enableAdvertisement);
            if (config.enableAdvertisement)
            {
                config.interstitialAdGroupId = EditorGUILayout.TextField("전면 광고 ID", config.interstitialAdGroupId);
                config.rewardedAdGroupId = EditorGUILayout.TextField("보상형 광고 ID", config.rewardedAdGroupId);
            }

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }

        public void OnBuildButtonGUI(EditorWindow window)
        {
            GUILayout.Space(20);
            GUILayout.Label("빌드", EditorStyles.boldLabel);

            if (GUILayout.Button("미니앱으로 변환", GUILayout.Height(40)))
            {
                var result = AITConvertCore.DoExport(true);
                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    EditorUtility.DisplayDialog("성공", "Apps in Toss 미니앱 변환이 완료되었습니다!", "확인");
                }
                else
                {
                    EditorUtility.DisplayDialog("실패", $"변환 중 오류가 발생했습니다: {result}", "확인");
                }
            }

            if (GUILayout.Button("WebGL 빌드만 실행"))
            {
                var result = AITConvertCore.DoExport(false);
                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    EditorUtility.DisplayDialog("성공", "WebGL 빌드가 완료되었습니다!", "확인");
                }
                else
                {
                    EditorUtility.DisplayDialog("실패", $"빌드 중 오류가 발생했습니다: {result}", "확인");
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("설정 초기화"))
            {
                if (EditorUtility.DisplayDialog("설정 초기화", "모든 설정을 초기화하시겠습니까?", "예", "아니오"))
                {
                    string configPath = "Assets/AppsInToss/Editor/AITConfig.asset";
                    AssetDatabase.DeleteAsset(configPath);
                    config = UnityUtil.GetEditorConf();
                }
            }
        }
    }
}
