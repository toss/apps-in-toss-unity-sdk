using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEngine;
using AppsInToss.Editor;

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

            var editorConfig = UnityUtil.GetEditorConf();

            // Unity 버전 정보
            Debug.Log($"[AIT] 현재 Unity 버전: {Application.unityVersion} ({AITDefaultSettings.GetUnityVersionGroup()})");

            // ===== 기본 설정 (모든 버전 공통) =====
            PlayerSettings.WebGL.template = "PROJECT:AITTemplate";
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.companyName = "Apps in Toss";
            PlayerSettings.defaultCursor = null;
            PlayerSettings.cursorHotspot = Vector2.zero;

            // ===== Run In Background (사용자 지정 또는 자동) =====
            bool runInBackground = editorConfig.runInBackground >= 0
                ? editorConfig.runInBackground == 1
                : AITDefaultSettings.GetDefaultRunInBackground();
            PlayerSettings.runInBackground = runInBackground;

            // ===== 메모리 설정 (버전별 자동 또는 사용자 지정) =====
            int memorySize = editorConfig.memorySize > 0
                ? editorConfig.memorySize
                : AITDefaultSettings.GetDefaultMemorySize();
            PlayerSettings.WebGL.memorySize = memorySize;

            // ===== 압축 설정 (버전별 자동 또는 사용자 지정) =====
            WebGLCompressionFormat compressionFormat = editorConfig.compressionFormat >= 0
                ? (WebGLCompressionFormat)editorConfig.compressionFormat
                : AITDefaultSettings.GetDefaultCompressionFormat();
            PlayerSettings.WebGL.compressionFormat = compressionFormat;

            // ===== 스레딩 설정 (버전별 자동 또는 사용자 지정) =====
            bool threadsSupport = editorConfig.threadsSupport >= 0
                ? editorConfig.threadsSupport == 1
                : AITDefaultSettings.GetDefaultThreadsSupport();
            PlayerSettings.WebGL.threadsSupport = threadsSupport;

            // ===== 데이터 캐싱 (버전별 자동 또는 사용자 지정) =====
            bool dataCaching = editorConfig.dataCaching >= 0
                ? editorConfig.dataCaching == 1
                : AITDefaultSettings.GetDefaultDataCaching();
            PlayerSettings.WebGL.dataCaching = dataCaching;

            // ===== 예외 처리 (사용자 지정 또는 자동) =====
            // 출처: UnityVersion.md:393, 431
            WebGLExceptionSupport exceptionSupport = editorConfig.exceptionSupport >= 0
                ? (WebGLExceptionSupport)editorConfig.exceptionSupport
                : AITDefaultSettings.GetDefaultExceptionSupport();
            PlayerSettings.WebGL.exceptionSupport = exceptionSupport;

            // ===== 파일 해싱 =====
            // Unity 2021.3에서 nameFilesAsHashes = true 시 Bee 빌드 루프 버그 발생
            // Unity 2022.3+ 에서는 정상 작동
#if UNITY_2022_1_OR_NEWER
            PlayerSettings.WebGL.nameFilesAsHashes = editorConfig.nameFilesAsHashes;
#else
            // Unity 2021.x: nameFilesAsHashes 비활성화 (빌드 루프 방지)
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            if (editorConfig.nameFilesAsHashes)
            {
                Debug.LogWarning("[AIT] Unity 2021.x에서는 '파일명 해싱' 옵션이 빌드 오류를 유발하여 자동으로 비활성화됩니다. Unity 2022.3 이상으로 업그레이드를 권장합니다.");
            }
#endif

            // ===== IL2CPP/Stripping 설정 =====
            // 출처: startup-speed.md:82-89
            // WebGL은 IL2CPP만 지원하지만 명시적으로 설정
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
#endif

            PlayerSettings.stripEngineCode = editorConfig.stripEngineCode;

            ManagedStrippingLevel strippingLevel = editorConfig.managedStrippingLevel >= 0
                ? (ManagedStrippingLevel)editorConfig.managedStrippingLevel
                : AITDefaultSettings.GetDefaultManagedStrippingLevel();
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, strippingLevel);
#else
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, strippingLevel);
#endif

            Il2CppCompilerConfiguration il2cppConfig = editorConfig.il2cppConfiguration >= 0
                ? (Il2CppCompilerConfiguration)editorConfig.il2cppConfiguration
                : AITDefaultSettings.GetDefaultIl2CppConfiguration();
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, il2cppConfig);
#else
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, il2cppConfig);
#endif

            // ===== Unity 6 (2023.3+) 전용 설정 =====
#if UNITY_2023_3_OR_NEWER
            // 출처: UnityVersion.md:394-402
            WebGLPowerPreference powerPreference = editorConfig.powerPreference >= 0
                ? (WebGLPowerPreference)editorConfig.powerPreference
                : AITDefaultSettings.GetDefaultPowerPreference();
            PlayerSettings.WebGL.powerPreference = powerPreference;

            // wasmStreaming은 Unity 6000에서 deprecated됨 (decompressionFallback에 의해 자동 결정)
#if !UNITY_6000_0_OR_NEWER
            PlayerSettings.WebGL.wasmStreaming = editorConfig.wasmStreaming;
#endif
#endif

            // ===== Unity 로고 표시 (사용자 지정 또는 자동, Unity Pro 라이선스 필요) =====
            bool showUnityLogo = editorConfig.showUnityLogo >= 0
                ? editorConfig.showUnityLogo == 1
                : AITDefaultSettings.GetDefaultShowUnityLogo();
            PlayerSettings.SplashScreen.showUnityLogo = showUnityLogo;

            // ===== 디버그 심볼 (빌드 프로필에서 설정 - ApplyBuildProfileSettings 참조) =====
            // 프로필 기반 설정은 DoExport()에서 ApplyBuildProfileSettings()를 통해 적용됨

            // ===== Decompression Fallback (사용자 지정 또는 자동) =====
            // 출처: StartupOptimization.md:93
            bool decompressionFallback = editorConfig.decompressionFallback >= 0
                ? editorConfig.decompressionFallback == 1
                : AITDefaultSettings.GetDefaultDecompressionFallback();
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;

            // 설정 요약 로그
            Debug.Log($"[AIT] Unity {AITDefaultSettings.GetUnityVersionGroup()} 최적화 설정 적용:");
            Debug.Log($"[AIT]   - WebGL Template: {PlayerSettings.WebGL.template}");
            Debug.Log($"[AIT]   - 메모리: {memorySize}MB{(editorConfig.memorySize <= 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 압축: {compressionFormat}{(editorConfig.compressionFormat < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 스레딩: {threadsSupport}{(editorConfig.threadsSupport < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 데이터 캐싱: {dataCaching}{(editorConfig.dataCaching < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 예외 처리: {exceptionSupport}{(editorConfig.exceptionSupport < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Stripping Level: {strippingLevel}{(editorConfig.managedStrippingLevel < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - IL2CPP 설정: {il2cppConfig}{(editorConfig.il2cppConfiguration < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Unity 로고: {showUnityLogo}{(editorConfig.showUnityLogo < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Run In Background: {runInBackground}{(editorConfig.runInBackground < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Decompression Fallback: {decompressionFallback}{(editorConfig.decompressionFallback < 0 ? " (자동)" : "")}");
#if UNITY_2023_3_OR_NEWER
            Debug.Log($"[AIT]   - Power Preference: {powerPreference}{(editorConfig.powerPreference < 0 ? " (자동)" : "")}");
#if !UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - WASM Streaming: {editorConfig.wasmStreaming}");
            Debug.Log($"[AIT]   - WASM 산술 예외: {wasmArithmeticExceptions}{(editorConfig.webAssemblyArithmeticExceptions < 0 ? " (자동)" : "")}");
#endif
#endif
        }

        /// <summary>
        /// 빌드 프로필 정보를 로그로 출력
        /// </summary>
        private static void LogBuildProfile(AITBuildProfile profile, string profileName)
        {
            Debug.Log("[AIT] ========================================");
            Debug.Log($"[AIT] 빌드 프로필: {profileName}");
            Debug.Log("[AIT] ========================================");
            Debug.Log($"[AIT]   Mock 브릿지: {(profile.enableMockBridge ? "활성화" : "비활성화")}");
            Debug.Log($"[AIT]   디버그 심볼: {(profile.debugSymbolsExternal ? "External" : "Embedded")}");
            Debug.Log($"[AIT]   디버그 콘솔: {(profile.enableDebugConsole ? "활성화" : "비활성화")}");
            Debug.Log($"[AIT]   LZ4 압축: {(profile.enableLZ4Compression ? "활성화" : "비활성화")}");
            Debug.Log("[AIT] ========================================");
        }

        /// <summary>
        /// 환경 변수로 빌드 프로필 설정 오버라이드
        /// </summary>
        private static AITBuildProfile ApplyEnvironmentVariableOverrides(AITBuildProfile profile)
        {
            if (profile == null) return null;

            string debugConsoleEnv = System.Environment.GetEnvironmentVariable("AIT_DEBUG_CONSOLE");
            if (string.IsNullOrEmpty(debugConsoleEnv))
            {
                return profile;
            }

            if (!bool.TryParse(debugConsoleEnv, out bool debugConsole))
            {
                Debug.LogWarning($"[AIT] AIT_DEBUG_CONSOLE 환경 변수 값이 올바르지 않습니다: '{debugConsoleEnv}' (true/false 필요)");
                return profile;
            }

            // 복사본 생성 (원본 프로필 보존)
            var overriddenProfile = new AITBuildProfile
            {
                enableMockBridge = profile.enableMockBridge,
                debugSymbolsExternal = profile.debugSymbolsExternal,
                enableDebugConsole = debugConsole,
                enableLZ4Compression = profile.enableLZ4Compression
            };

            Debug.Log($"[AIT] 환경 변수 오버라이드: AIT_DEBUG_CONSOLE={debugConsole}");
            return overriddenProfile;
        }

        /// <summary>
        /// 빌드 프로필 기반으로 PlayerSettings 적용
        /// </summary>
        private static void ApplyBuildProfileSettings(AITBuildProfile profile)
        {
            // 디버그 심볼 설정 (Unity 2022.3+)
#if UNITY_2022_3_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = profile.debugSymbolsExternal
                ? WebGLDebugSymbolMode.External
                : WebGLDebugSymbolMode.Embedded;
            Debug.Log($"[AIT] 디버그 심볼 모드 설정: {PlayerSettings.WebGL.debugSymbolMode}");
#endif
        }

        private static void EnsureWebGLTemplatesExist()
        {
            // 프로젝트의 Assets/WebGLTemplates 경로
            string projectTemplatesPath = Path.Combine(Application.dataPath, "WebGLTemplates");
            string projectTemplate = Path.Combine(projectTemplatesPath, "AITTemplate");

            // 이미 존재하고 index.html이 있으면 복사 불필요
            if (Directory.Exists(projectTemplate) && File.Exists(Path.Combine(projectTemplate, "index.html")))
            {
                return;
            }

            // SDK의 WebGLTemplates 경로 찾기 (여러 가능한 경로 시도)
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] possibleSdkPaths = new string[]
            {
                // Package로 설치된 경우 (Unity 프로젝트 루트 기준)
                Path.Combine(projectRoot, "Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates"),
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

            // AITTemplate 복사
            string sdkTemplate = Path.Combine(sdkTemplatesPath, "AITTemplate");

            if (Directory.Exists(sdkTemplate))
            {
                Debug.Log("[AIT] WebGLTemplates를 프로젝트로 복사 중...");

                // 기존 폴더가 비어있으면 삭제
                if (Directory.Exists(projectTemplate))
                {
                    Directory.Delete(projectTemplate, true);
                }

                Directory.CreateDirectory(projectTemplatesPath);
                UnityUtil.CopyDirectory(sdkTemplate, projectTemplate);
                Debug.Log("[AIT] ✓ WebGLTemplates 복사 완료");
            }
            else
            {
                Debug.LogError($"[AIT] SDK 템플릿 폴더를 찾을 수 없습니다: {sdkTemplate}");
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
            FAIL_NPM_BUILD = 6,
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

                case AITExportError.FAIL_NPM_BUILD:
                    return "pnpm 빌드에 실패했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. pnpm이 설치되어 있는지 확인: npm install -g pnpm\n" +
                           "2. Unity Console 창에서 에러 메시지 확인\n" +
                           "3. ait-build 폴더에서 직접 pnpm install 시도\n" +
                           "4. package.json 파일이 올바른지 확인";

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
        /// <param name="cleanBuild">클린 빌드 여부 (false면 incremental build)</param>
        /// <param name="profile">적용할 빌드 프로필 (null이면 buildPackageProfile 사용)</param>
        /// <param name="profileName">빌드 프로필 이름 (로그 출력용)</param>
        /// <returns>변환 결과</returns>
        public static AITExportError DoExport(bool buildWebGL = true, bool doPackaging = true, bool cleanBuild = false, AITBuildProfile profile = null, string profileName = null)
        {
            // 빌드 시작 전 취소 플래그 리셋
            ResetCancellation();

            Init();

            // 프로필이 지정되지 않으면 기본 프로필 사용
            var config = UnityUtil.GetEditorConf();
            if (profile == null)
            {
                profile = config.buildPackageProfile;
                profileName = profileName ?? "Build & Package";
            }

            // 환경 변수로 프로필 오버라이드 적용
            profile = ApplyEnvironmentVariableOverrides(profile);

            // 빌드 프로필 로그 출력
            LogBuildProfile(profile, profileName);

            // 프로필 기반으로 PlayerSettings 설정
            ApplyBuildProfileSettings(profile);

            Debug.Log($"Apps in Toss 미니앱 변환을 시작합니다... (cleanBuild: {cleanBuild})");

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

                    var webglResult = BuildWebGL(cleanBuild, profile);
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

                    var exportResult = GenerateMiniAppPackage(profile);
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

        private static AITExportError BuildWebGL(bool cleanBuild = false, AITBuildProfile profile = null)
        {
            Debug.Log($"WebGL 빌드를 시작합니다... ({(cleanBuild ? "클린 빌드" : "증분 빌드")})");

            string[] scenes = UnityUtil.GetBuildScenes();
            string outputPath = Path.Combine(UnityUtil.GetProjectPath(), webglDir);

            // 클린 빌드 시에만 기존 빌드 폴더 삭제
            if (cleanBuild && Directory.Exists(outputPath))
            {
                Debug.Log("[AIT] 클린 빌드: 기존 WebGL 빌드 폴더 삭제 중...");
                Directory.Delete(outputPath, true);
            }

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateBuildPackageProfile();
            }

            // 빌드 옵션 설정
            BuildOptions buildOptions = BuildOptions.None;

            if (profile.enableLZ4Compression)
            {
                buildOptions |= BuildOptions.CompressWithLz4;  // LZ4 압축으로 빌드 속도 향상
            }

            // Unity 2021.3에서 Bee 빌드 캐시 문제로 인한 빌드 루프 방지
            // cleanBuild 시 CleanBuildCache 옵션 추가
            if (cleanBuild)
            {
                buildOptions |= BuildOptions.CleanBuildCache;
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

        private static AITExportError GenerateMiniAppPackage(AITBuildProfile profile = null)
        {
            Debug.Log("Apps in Toss 미니앱 패키지를 생성합니다...");

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateBuildPackageProfile();
            }

            string projectPath = UnityUtil.GetProjectPath();
            string webglPath = Path.Combine(projectPath, webglDir);

            if (!Directory.Exists(webglPath))
            {
                Debug.LogError("WebGL 빌드 결과를 찾을 수 없습니다. WebGL 빌드를 먼저 실행하세요.");
                return AITExportError.BUILD_WEBGL_FAILED;
            }

            // WebGL 빌드 결과를 ait-build로 복사
            var packageResult = PackageWebGLBuild(projectPath, webglPath, profile);
            if (packageResult != AITExportError.SUCCEED)
            {
                return packageResult;
            }

            Debug.Log("Apps in Toss 미니앱이 생성되었습니다!");
            return AITExportError.SUCCEED;
        }

        private static AITExportError PackageWebGLBuild(string projectPath, string webglPath, AITBuildProfile profile = null)
        {
            Debug.Log("[AIT] Vite 기반 빌드 패키징 시작...");

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateBuildPackageProfile();
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
            CopyWebGLToPublic(webglPath, buildProjectPath, profile);

            // 3. npm install 및 build 실행
            Debug.Log("[AIT] Step 3/3: npm install & build 실행 중...");
            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");
            string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");

            // node_modules가 없는 경우에만 pnpm install 실행
            if (!Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[AIT] node_modules가 없습니다. pnpm install을 실행합니다 (1-2분 소요)...");

                // pnpm 경로 찾기
                string pnpmPath = FindPnpmPath();
                if (string.IsNullOrEmpty(pnpmPath))
                {
                    Debug.LogError("[AIT] pnpm을 찾을 수 없습니다. pnpm을 설치해주세요: npm install -g pnpm");
                    return AITExportError.FAIL_NPM_BUILD;
                }

                var installResult = RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install", localCachePath, "pnpm install 실행 중...");

                if (installResult != AITExportError.SUCCEED)
                {
                    Debug.LogError("[AIT] pnpm install 실패");
                    return installResult;
                }
            }
            else
            {
                // 캐시 통계 출력
                LogBuildCacheStats(buildProjectPath);
                Debug.Log("[AIT] node_modules가 존재합니다. pnpm install을 건너뜁니다.");
            }

            // granite build 실행 (web 폴더를 dist로 복사)
            Debug.Log("[AIT] granite build 실행 중...");

            // pnpm 경로 찾기
            string pnpmPathForBuild = FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPathForBuild))
            {
                Debug.LogError("[AIT] pnpm을 찾을 수 없습니다. pnpm을 설치해주세요: npm install -g pnpm");
                return AITExportError.FAIL_NPM_BUILD;
            }

            var buildResult = RunNpmCommandWithCache(buildProjectPath, pnpmPathForBuild, "run build", localCachePath, "granite build 실행 중...");

            if (buildResult != AITExportError.SUCCEED)
            {
                Debug.LogError("[AIT] granite build 실패");
                return buildResult;
            }

            string distPath = Path.Combine(buildProjectPath, "dist");

            Debug.Log($"[AIT] ✓ 패키징 완료: {distPath}");

            return AITExportError.SUCCEED;
        }

        private static void CopyBuildConfigFromTemplate(string buildProjectPath)
        {
            // SDK의 BuildConfig 템플릿 경로 찾기
            Debug.Log("[AIT] SDK BuildConfig 템플릿 경로 검색 중...");
            string[] possibleSdkPaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/BuildConfig"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/BuildConfig"), // 레거시 호환성
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates/AITTemplate/BuildConfig")
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

            var config = UnityUtil.GetEditorConf();

            Debug.Log("[AIT] BuildConfig 파일 복사 중...");

            // package.json 복사 (치환 없음)
            string packageJsonSrc = Path.Combine(sdkBuildConfigPath, "package.json");
            string packageJsonDst = Path.Combine(buildProjectPath, "package.json");
            File.Copy(packageJsonSrc, packageJsonDst, true);
            Debug.Log($"[AIT]   ✓ package.json 복사: {new FileInfo(packageJsonSrc).Length / 1024}KB");

            // vite.config.ts 복사 (치환 없음)
            string viteConfigSrc = Path.Combine(sdkBuildConfigPath, "vite.config.ts");
            string viteConfigDst = Path.Combine(buildProjectPath, "vite.config.ts");
            File.Copy(viteConfigSrc, viteConfigDst, true);
            Debug.Log($"[AIT]   ✓ vite.config.ts 복사: {new FileInfo(viteConfigSrc).Length / 1024}KB");

            // tsconfig.json 복사 (치환 없음)
            string tsconfigSrc = Path.Combine(sdkBuildConfigPath, "tsconfig.json");
            string tsconfigDst = Path.Combine(buildProjectPath, "tsconfig.json");
            File.Copy(tsconfigSrc, tsconfigDst, true);
            Debug.Log($"[AIT]   ✓ tsconfig.json 복사: {new FileInfo(tsconfigSrc).Length / 1024}KB");

            // unity-bridge.ts 복사 (치환 없음) - @apps-in-toss/web-framework 함수를 window.AppsInToss에 노출
            string unityBridgeSrc = Path.Combine(sdkBuildConfigPath, "unity-bridge.ts");
            string unityBridgeDst = Path.Combine(buildProjectPath, "unity-bridge.ts");
            if (File.Exists(unityBridgeSrc))
            {
                File.Copy(unityBridgeSrc, unityBridgeDst, true);
                Debug.Log($"[AIT]   ✓ unity-bridge.ts 복사: {new FileInfo(unityBridgeSrc).Length / 1024}KB");
            }

            // granite.config.ts 복사 및 플레이스홀더 치환
            Debug.Log("[AIT] granite.config.ts placeholder 치환 중...");
            Debug.Log($"[AIT]   %AIT_APP_NAME% → '{config.appName}'");
            Debug.Log($"[AIT]   %AIT_DISPLAY_NAME% → '{config.displayName}'");
            Debug.Log($"[AIT]   %AIT_PRIMARY_COLOR% → '{config.primaryColor}'");
            Debug.Log($"[AIT]   %AIT_ICON_URL% → '{config.iconUrl}'");
            Debug.Log($"[AIT]   %AIT_BRIDGE_COLOR_MODE% → '{config.GetBridgeColorModeString()}'");
            Debug.Log($"[AIT]   %AIT_WEBVIEW_TYPE% → '{config.GetWebViewTypeString()}'");
            Debug.Log($"[AIT]   %AIT_HOST% → '{config.devHost}'");
            Debug.Log($"[AIT]   %AIT_LOCAL_PORT% → '{config.localPort}'");
            Debug.Log($"[AIT]   %AIT_PERMISSIONS% → {config.GetPermissionsJson()}");
            Debug.Log($"[AIT]   %AIT_OUTDIR% → '{config.outdir}'");

            string graniteConfigTemplate = File.ReadAllText(Path.Combine(sdkBuildConfigPath, "granite.config.ts"));
            string graniteConfig = graniteConfigTemplate
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ICON_URL%", config.iconUrl)
                .Replace("%AIT_BRIDGE_COLOR_MODE%", config.GetBridgeColorModeString())
                .Replace("%AIT_WEBVIEW_TYPE%", config.GetWebViewTypeString())
                .Replace("%AIT_HOST%", config.devHost)
                .Replace("%AIT_LOCAL_PORT%", config.localPort.ToString())
                .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson())
                .Replace("%AIT_OUTDIR%", config.outdir);

            File.WriteAllText(
                Path.Combine(buildProjectPath, "granite.config.ts"),
                graniteConfig,
                new System.Text.UTF8Encoding(false)
            );

            Debug.Log("[AIT] ✓ 빌드 설정 파일 복사 및 치환 완료");
        }

        private static void CopyWebGLToPublic(string webglPath, string buildProjectPath, AITBuildProfile profile = null)
        {
            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateBuildPackageProfile();
            }

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
                // Unity 압축 설정에 따라 .unityweb, .gz, .br 확장자가 붙을 수 있음
                string loaderFile = FindFileInBuild(buildSrc, "*.loader.js");
                string dataFile = FindFileInBuild(buildSrc, "*.data*");
                string frameworkFile = FindFileInBuild(buildSrc, "*.framework.js*");
                string wasmFile = FindFileInBuild(buildSrc, "*.wasm*");
                string symbolsFile = FindFileInBuild(buildSrc, "*.symbols.json*");

                // 프로필 기반 설정값 (Mock 브릿지가 비활성화되면 프로덕션 모드로 간주)
                string isProduction = profile.enableMockBridge ? "false" : "true";
                string enableDebugConsole = profile.enableDebugConsole ? "true" : "false";

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
                    .Replace("%AIT_IS_PRODUCTION%", isProduction)
                    .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", enableDebugConsole);

                File.WriteAllText(indexDest, indexContent, System.Text.Encoding.UTF8);
                Debug.Log("[AIT] index.html → 프로젝트 루트에 생성");
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
            // AITPackageManagerHelper를 사용한 통합 패키지 매니저 검색
            string buildPath = AITPackageManagerHelper.GetBuildPath();
            return AITPackageManagerHelper.FindPackageManager(buildPath, verbose: true);
        }

        /// <summary>
        /// pnpm 경로를 찾는 함수 (내장 Node.js 사용 시 자동 설치 포함)
        /// </summary>
        private static string FindPnpmPath()
        {
            // 1. 내장 Node.js bin 디렉토리에서 pnpm 찾기
            string embeddedNodeBinPath = AppsInToss.Editor.AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            if (!string.IsNullOrEmpty(embeddedNodeBinPath))
            {
                string pnpmInEmbedded = Path.Combine(embeddedNodeBinPath, AppsInToss.Editor.AITPlatformHelper.GetExecutableName("pnpm"));
                if (File.Exists(pnpmInEmbedded))
                {
                    Debug.Log($"[AIT] ✓ 내장 pnpm 발견: {pnpmInEmbedded}");
                    return pnpmInEmbedded;
                }

                // 2. pnpm이 없으면 npm으로 글로벌 설치
                Debug.Log("[AIT] 내장 pnpm이 없습니다. npm install -g pnpm 실행 중...");
                string npmPath = Path.Combine(embeddedNodeBinPath, AppsInToss.Editor.AITPlatformHelper.GetExecutableName("npm"));

                if (File.Exists(npmPath))
                {
                    // npm install -g pnpm 실행
                    string command = $"\"{npmPath}\" install -g pnpm";
                    var result = AppsInToss.Editor.AITPlatformHelper.ExecuteCommand(
                        command,
                        embeddedNodeBinPath,
                        new[] { embeddedNodeBinPath },
                        timeoutMs: 120000, // 2분
                        verbose: true
                    );

                    if (result.Success)
                    {
                        Debug.Log("[AIT] ✓ pnpm 글로벌 설치 완료");

                        // 설치 후 pnpm 경로 다시 확인
                        if (File.Exists(pnpmInEmbedded))
                        {
                            return pnpmInEmbedded;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[AIT] pnpm 설치 실패: {result.Error}");
                    }
                }
            }

            // 3. 시스템 pnpm 검색 (fallback)
            return AppsInToss.Editor.AITPackageManagerHelper.FindExecutable("pnpm", verbose: true);
        }

        /// <summary>
        /// FindNpm - 외부 접근용 public 메서드
        /// AppsInTossMenu.cs 등에서 사용
        /// </summary>
        public static string FindNpm()
        {
            string buildPath = AITPackageManagerHelper.GetBuildPath();
            return AITPackageManagerHelper.FindPackageManager(buildPath, verbose: true);
        }

        internal static AITExportError RunNpmCommandWithCache(string workingDirectory, string npmPath, string arguments, string cachePath, string progressTitle)
        {
            string npmDir = Path.GetDirectoryName(npmPath);

            // node 실행 파일 경로 찾기 (pnpm이 node를 찾을 수 있도록)
            string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
            string nodeDir = "";

            if (!string.IsNullOrEmpty(nodePath))
            {
                nodeDir = Path.GetDirectoryName(nodePath);
            }
            else
            {
                // node를 찾지 못한 경우, npmPath가 embedded Node.js인지 확인
                string nodeExeName = AppsInToss.Editor.AITPlatformHelper.GetExecutableName("node");
                string possibleNodePath = Path.Combine(npmDir, nodeExeName);
                if (File.Exists(possibleNodePath))
                {
                    nodePath = possibleNodePath;
                    nodeDir = npmDir;
                    Debug.Log($"[Package Manager] Embedded node 발견: {nodePath}");
                }
            }

            // 패키지 매니저 이름 추출 (pnpm 또는 npm)
            string pmName = Path.GetFileNameWithoutExtension(npmPath);

            // --store-dir는 install 명령어에만 적용 (run build에는 적용하지 않음)
            bool isInstallCommand = arguments.Trim() == "install";
            string fullArguments = isInstallCommand
                ? $"{arguments} --store-dir \"{cachePath}\""
                : arguments;

            // 추가 PATH 경로 수집
            var additionalPaths = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(npmDir)) additionalPaths.Add(npmDir);
            if (!string.IsNullOrEmpty(nodeDir) && nodeDir != npmDir) additionalPaths.Add(nodeDir);

            Debug.Log($"[{pmName}] 명령 실행 준비:");
            Debug.Log($"[{pmName}]   작업 디렉토리: {workingDirectory}");
            Debug.Log($"[{pmName}]   {pmName} 경로: {npmPath}");
            Debug.Log($"[{pmName}]   node 경로: {nodePath ?? "찾을 수 없음"}");
            Debug.Log($"[{pmName}]   명령: {pmName} {arguments}");
            Debug.Log($"[{pmName}]   캐시 경로: {cachePath}");

            try
            {
                Debug.Log($"[{pmName}] 프로세스 시작...");

                // 크로스 플랫폼 명령 구성
                string command = $"\"{npmPath}\" {fullArguments}";

                // 프로세스 완료를 대기하되, 진행 상황을 업데이트
                int maxWaitSeconds = 300; // 5분

                // EditorUtility.DisplayProgressBar와 함께 명령 실행
                EditorUtility.DisplayProgressBar("Apps in Toss", $"{progressTitle} (시작 중...)", 0);

                var result = AppsInToss.Editor.AITPlatformHelper.ExecuteCommand(
                    command,
                    workingDirectory,
                    additionalPaths.ToArray(),
                    timeoutMs: maxWaitSeconds * 1000,
                    verbose: true
                );

                EditorUtility.ClearProgressBar();

                if (!result.Success)
                {
                    Debug.LogError($"[{pmName}] 명령 실패 (Exit Code: {result.ExitCode}): {pmName} {arguments}");
                    Debug.LogError($"[{pmName}] 오류:\n{result.Error}");
                    return AITExportError.BUILD_WEBGL_FAILED;
                }

                Debug.Log($"[{pmName}] ✓ 명령 성공 완료: {pmName} {arguments}");
                return AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[{pmName}] 명령 실행 오류: {e.Message}");
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

            // 빌드 설정 (빌드 프로필 UI는 AITConfigurationWindow에서 관리)
            GUILayout.Label("빌드 프로필", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("빌드 프로필 설정은 Apps in Toss > Configuration 메뉴에서 확인하세요.", MessageType.Info);

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
