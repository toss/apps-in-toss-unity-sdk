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

        public static void Init(AITBuildProfile profile = null)
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

            // ===== 압축 설정 (프로필 → 자동) =====
            WebGLCompressionFormat compressionFormat = profile?.compressionFormat >= 0
                ? (WebGLCompressionFormat)profile.compressionFormat
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

            // ===== Managed Stripping Level (프로필 → 자동) =====
            ManagedStrippingLevel strippingLevel = profile?.managedStrippingLevel >= 0
                ? (ManagedStrippingLevel)profile.managedStrippingLevel
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
            Debug.Log($"[AIT]   - 압축: {compressionFormat}{(profile?.compressionFormat < 0 || profile == null ? " (자동)" : " (프로필)")}");
            Debug.Log($"[AIT]   - 스레딩: {threadsSupport}{(editorConfig.threadsSupport < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 데이터 캐싱: {dataCaching}{(editorConfig.dataCaching < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 예외 처리: {exceptionSupport}{(editorConfig.exceptionSupport < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Stripping Level: {strippingLevel}{(profile?.managedStrippingLevel < 0 || profile == null ? " (자동)" : " (프로필)")}");
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
            // 압축 포맷 문자열 생성
            string compressionStr = profile.compressionFormat switch
            {
                0 => "Disabled",
                1 => "Gzip",
                2 => "Brotli",
                _ => "자동"
            };

            // Stripping Level 문자열 생성
            string strippingStr = profile.managedStrippingLevel switch
            {
                0 => "Disabled",
                1 => "Minimal",
                2 => "Low",
                3 => "Medium",
                4 => "High",
                _ => "자동 (High)"
            };

            Debug.Log("[AIT] ========================================");
            Debug.Log($"[AIT] 빌드 프로필: {profileName}");
            Debug.Log("[AIT] ========================================");
            Debug.Log($"[AIT]   Mock 브릿지: {(profile.enableMockBridge ? "활성화" : "비활성화")}");
            Debug.Log($"[AIT]   디버그 콘솔: {(profile.enableDebugConsole ? "활성화" : "비활성화")}");
            Debug.Log($"[AIT]   Development Build: {(profile.developmentBuild ? "활성화" : "비활성화")}");
            Debug.Log($"[AIT]   LZ4 압축: {(profile.enableLZ4Compression ? "활성화" : "비활성화")}");
            Debug.Log($"[AIT]   압축 포맷: {compressionStr}");
            Debug.Log($"[AIT]   Stripping Level: {strippingStr}");
            Debug.Log($"[AIT]   디버그 심볼: {(profile.debugSymbolsExternal ? "External" : "Embedded")}");
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
            string projectIndexHtml = Path.Combine(projectTemplate, "index.html");

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
                if (Directory.Exists(path))
                {
                    sdkTemplatesPath = path;
                    break;
                }
            }

            if (sdkTemplatesPath == null)
            {
                Debug.LogError($"[AIT] SDK WebGLTemplates 폴더를 찾을 수 없습니다.");
                return;
            }

            string sdkTemplate = Path.Combine(sdkTemplatesPath, "AITTemplate");
            string sdkIndexHtml = Path.Combine(sdkTemplate, "index.html");

            if (!Directory.Exists(sdkTemplate))
            {
                Debug.LogError($"[AIT] SDK 템플릿 폴더를 찾을 수 없습니다: {sdkTemplate}");
                return;
            }

            // 프로젝트 템플릿이 없으면 전체 복사
            if (!Directory.Exists(projectTemplate) || !File.Exists(projectIndexHtml))
            {
                Debug.Log("[AIT] WebGLTemplates를 프로젝트로 복사 중...");

                if (Directory.Exists(projectTemplate))
                {
                    Directory.Delete(projectTemplate, true);
                }

                Directory.CreateDirectory(projectTemplatesPath);
                UnityUtil.CopyDirectory(sdkTemplate, projectTemplate);
                Debug.Log("[AIT] ✓ WebGLTemplates 복사 완료");
                return;
            }

            // 프로젝트 템플릿이 있으면 마커 기반으로 업데이트
            UpdateProjectTemplate(projectTemplate, sdkTemplate);
        }

        /// <summary>
        /// 기존 프로젝트 템플릿을 SDK 템플릿으로 마커 기반 업데이트
        /// 사용자 커스텀 영역(USER_* 마커)은 보존하고 SDK 영역만 업데이트
        /// </summary>
        private static void UpdateProjectTemplate(string projectTemplate, string sdkTemplate)
        {
            // index.html 마커 기반 업데이트
            string projectIndexHtml = Path.Combine(projectTemplate, "index.html");
            string sdkIndexHtml = Path.Combine(sdkTemplate, "index.html");

            if (File.Exists(sdkIndexHtml) && File.Exists(projectIndexHtml))
            {
                string projectContent = File.ReadAllText(projectIndexHtml);
                string sdkContent = File.ReadAllText(sdkIndexHtml);

                // 프로젝트에 마커가 없으면 (이전 버전) SDK 템플릿으로 교체하되 경고
                if (!projectContent.Contains(HTML_USER_HEAD_START))
                {
                    Debug.Log("[AIT] 템플릿 업데이트: 이전 버전 템플릿을 새 마커 기반 템플릿으로 교체합니다.");
                    Debug.LogWarning("[AIT] ⚠️ 기존 index.html에 커스텀 수정이 있었다면 수동으로 USER_* 마커 영역에 재적용하세요.");
                    File.WriteAllText(projectIndexHtml, sdkContent);
                }
                else
                {
                    // 마커가 있으면 사용자 영역 보존하고 SDK 영역만 업데이트
                    string updatedContent = MergeHtmlTemplates(projectContent, sdkContent);
                    if (updatedContent != projectContent)
                    {
                        File.WriteAllText(projectIndexHtml, updatedContent);
                        Debug.Log("[AIT] ✓ index.html 템플릿 업데이트 (사용자 커스텀 영역 보존)");
                    }
                }
            }

            // vite.config.ts, granite.config.ts 마커 기반 업데이트
            UpdateConfigFileWithMarkers(projectTemplate, sdkTemplate, "BuildConfig~/vite.config.ts");
            UpdateConfigFileWithMarkers(projectTemplate, sdkTemplate, "BuildConfig~/granite.config.ts");

            // Runtime 폴더는 항상 SDK 버전으로 덮어쓰기 (브릿지 코드)
            string projectRuntime = Path.Combine(projectTemplate, "Runtime");
            string sdkRuntime = Path.Combine(sdkTemplate, "Runtime");
            if (Directory.Exists(sdkRuntime))
            {
                if (Directory.Exists(projectRuntime))
                {
                    Directory.Delete(projectRuntime, true);
                }
                UnityUtil.CopyDirectory(sdkRuntime, projectRuntime);
            }

            // TemplateData는 항상 SDK 버전으로 덮어쓰기
            string projectTemplateData = Path.Combine(projectTemplate, "TemplateData");
            string sdkTemplateData = Path.Combine(sdkTemplate, "TemplateData");
            if (Directory.Exists(sdkTemplateData))
            {
                if (Directory.Exists(projectTemplateData))
                {
                    Directory.Delete(projectTemplateData, true);
                }
                UnityUtil.CopyDirectory(sdkTemplateData, projectTemplateData);
            }
        }

        /// <summary>
        /// HTML 템플릿 마커 기반 병합
        /// SDK 템플릿의 전체 구조를 사용하되, 프로젝트의 USER_* 마커 영역 내용을 보존
        /// </summary>
        private static string MergeHtmlTemplates(string projectContent, string sdkContent)
        {
            string result = sdkContent;

            // USER_HEAD 영역 보존
            string projectUserHead = ExtractHtmlUserSection(projectContent, HTML_USER_HEAD_START, HTML_USER_HEAD_END);
            if (!string.IsNullOrEmpty(projectUserHead))
            {
                result = ReplaceHtmlUserSection(result, HTML_USER_HEAD_START, HTML_USER_HEAD_END, projectUserHead);
            }

            // USER_BODY_END 영역 보존
            string projectUserBodyEnd = ExtractHtmlUserSection(projectContent, HTML_USER_BODY_END_START, HTML_USER_BODY_END_END);
            if (!string.IsNullOrEmpty(projectUserBodyEnd))
            {
                result = ReplaceHtmlUserSection(result, HTML_USER_BODY_END_START, HTML_USER_BODY_END_END, projectUserBodyEnd);
            }

            return result;
        }

        /// <summary>
        /// Config 파일 마커 기반 업데이트 (vite.config.ts, granite.config.ts)
        /// </summary>
        private static void UpdateConfigFileWithMarkers(string projectTemplate, string sdkTemplate, string relativePath)
        {
            string projectFile = Path.Combine(projectTemplate, relativePath);
            string sdkFile = Path.Combine(sdkTemplate, relativePath);

            if (!File.Exists(sdkFile)) return;

            // 프로젝트 파일이 없으면 SDK에서 복사
            if (!File.Exists(projectFile))
            {
                string dir = Path.GetDirectoryName(projectFile);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Copy(sdkFile, projectFile);
                return;
            }

            string projectContent = File.ReadAllText(projectFile);
            string sdkContent = File.ReadAllText(sdkFile);

            // 프로젝트에 마커가 없으면 (이전 버전) 그대로 유지
            if (!projectContent.Contains(SDK_MARKER_START))
            {
                return;
            }

            // SDK 영역 추출 및 교체
            string sdkSection = ExtractMarkerSection(sdkContent, "SDK_GENERATED");
            if (!string.IsNullOrEmpty(sdkSection))
            {
                string updatedContent = ReplaceMarkerSection(projectContent, "SDK_GENERATED", sdkSection);
                if (updatedContent != projectContent)
                {
                    File.WriteAllText(projectFile, updatedContent);
                }
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
                           "1. Unity Console 창에서 에러 메시지 확인\n" +
                           "2. ait-build 폴더에서 직접 pnpm install 시도\n" +
                           "3. package.json 파일이 올바른지 확인\n" +
                           "4. Tools~/NodeJS 폴더를 삭제 후 다시 빌드 시도";

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
        /// <param name="profile">적용할 빌드 프로필 (null이면 productionProfile 사용)</param>
        /// <param name="profileName">빌드 프로필 이름 (로그 출력용)</param>
        /// <returns>변환 결과</returns>
        public static AITExportError DoExport(bool buildWebGL = true, bool doPackaging = true, bool cleanBuild = false, AITBuildProfile profile = null, string profileName = null)
        {
            // 빌드 시작 전 취소 플래그 리셋
            ResetCancellation();

            // 프로필이 지정되지 않으면 기본 프로필 사용
            var config = UnityUtil.GetEditorConf();
            if (profile == null)
            {
                profile = config.productionProfile;
                profileName = profileName ?? "Production";
            }

            // Init()에 프로필 전달하여 프로필별 압축/스트리핑 설정 적용
            Init(profile);

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
                profile = AITBuildProfile.CreateProductionProfile();
            }

            // 빌드 옵션 설정
            BuildOptions buildOptions = BuildOptions.None;

            // Development Build 옵션 (빌드 속도 향상, 디버깅 편의)
            if (profile.developmentBuild)
            {
                buildOptions |= BuildOptions.Development;
                Debug.Log("[AIT] Development Build 활성화");
            }

            // LZ4 압축으로 빌드 속도 향상
            if (profile.enableLZ4Compression)
            {
                buildOptions |= BuildOptions.CompressWithLz4;
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

            // 빌드 리포트를 에러 리포터에 저장 (Issue 신고 시 사용)
            AITErrorReporter.SetBuildReport(result);

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
                profile = AITBuildProfile.CreateProductionProfile();
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
            Debug.Log("[AIT] Step 3/3: pnpm install & build 실행 중...");
            string localCachePath = Path.Combine(buildProjectPath, ".npm-cache");

            // pnpm install 실행 (의존성 동기화 - 이미 설치된 경우 빠르게 완료됨)
            Debug.Log("[AIT] pnpm install 실행 중...");

            // pnpm 경로 찾기 (없으면 자동 설치)
            string pnpmPath = FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPath))
            {
                Debug.LogError("[AIT] pnpm 설치에 실패했습니다. Unity Console에서 에러를 확인해주세요.");
                return AITExportError.FAIL_NPM_BUILD;
            }

            // 먼저 --frozen-lockfile로 시도, 실패하면 lockfile 없이 재시도
            // (사용자가 package.json에 새 패키지 추가 시 lockfile이 outdated 될 수 있음)
            var installResult = RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install --frozen-lockfile", localCachePath, "pnpm install 실행 중...");

            if (installResult != AITExportError.SUCCEED)
            {
                Debug.LogWarning("[AIT] --frozen-lockfile 설치 실패, lockfile 갱신 모드로 재시도...");
                Debug.LogWarning("[AIT] (사용자가 package.json에 새 패키지를 추가한 경우 정상 동작입니다)");

                // lockfile 없이 재시도 (CI 환경에서도 lockfile 갱신 허용)
                installResult = RunNpmCommandWithCache(buildProjectPath, pnpmPath, "install --no-frozen-lockfile", localCachePath, "pnpm install (lockfile 갱신)...");

                if (installResult != AITExportError.SUCCEED)
                {
                    Debug.LogError("[AIT] pnpm install 실패");
                    return installResult;
                }

                Debug.Log("[AIT] ✓ 새 패키지 설치 및 lockfile 갱신 완료");
            }

            // granite build 실행 (web 폴더를 dist로 복사)
            Debug.Log("[AIT] granite build 실행 중...");

            var buildResult = RunNpmCommandWithCache(buildProjectPath, pnpmPath, "run build", localCachePath, "granite build 실행 중...");

            if (buildResult != AITExportError.SUCCEED)
            {
                Debug.LogError("[AIT] granite build 실패");
                return buildResult;
            }

            string distPath = Path.Combine(buildProjectPath, "dist");

            // 빌드 완료 리포트 출력
            PrintBuildReport(buildProjectPath, distPath);

            Debug.Log($"[AIT] ✓ 패키징 완료: {distPath}");

            return AITExportError.SUCCEED;
        }

        // 마커 상수 (TypeScript 설정 파일용)
        private const string SDK_MARKER_START = "//// SDK_GENERATED_START";
        private const string SDK_MARKER_END = "//// SDK_GENERATED_END ////";

        // HTML 마커 상수 (index.html용)
        private const string HTML_USER_HEAD_START = "<!-- USER_HEAD_START";
        private const string HTML_USER_HEAD_END = "<!-- USER_HEAD_END -->";
        private const string HTML_USER_BODY_END_START = "<!-- USER_BODY_END_START";
        private const string HTML_USER_BODY_END_END = "<!-- USER_BODY_END_END -->";

        /// <summary>
        /// 마커 기반으로 SDK 섹션을 교체합니다.
        /// </summary>
        private static string ReplaceMarkerSection(string content, string newSdkSection)
        {
            int startIdx = content.IndexOf(SDK_MARKER_START);
            int endIdx = content.IndexOf(SDK_MARKER_END);

            if (startIdx == -1 || endIdx == -1)
            {
                Debug.LogWarning("[AIT] SDK 마커를 찾을 수 없습니다. 전체 파일을 SDK 버전으로 교체합니다.");
                return newSdkSection;
            }

            // 마커 포함하여 교체
            string before = content.Substring(0, startIdx);
            string after = content.Substring(endIdx + SDK_MARKER_END.Length);

            return before + newSdkSection + after;
        }

        /// <summary>
        /// SDK 마커 섹션을 추출합니다.
        /// </summary>
        private static string ExtractSdkSection(string content)
        {
            int startIdx = content.IndexOf(SDK_MARKER_START);
            int endIdx = content.IndexOf(SDK_MARKER_END);

            if (startIdx == -1 || endIdx == -1)
            {
                return null;
            }

            return content.Substring(startIdx, endIdx + SDK_MARKER_END.Length - startIdx);
        }

        /// <summary>
        /// 지정된 마커 이름으로 섹션을 추출합니다.
        /// </summary>
        private static string ExtractMarkerSection(string content, string markerName)
        {
            string startMarker = $"//// {markerName}_START";
            string endMarker = $"//// {markerName}_END ////";

            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                return null;
            }

            return content.Substring(startIdx, endIdx + endMarker.Length - startIdx);
        }

        /// <summary>
        /// 지정된 마커 이름으로 섹션을 교체합니다.
        /// </summary>
        private static string ReplaceMarkerSection(string content, string markerName, string newSection)
        {
            string startMarker = $"//// {markerName}_START";
            string endMarker = $"//// {markerName}_END ////";

            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                Debug.LogWarning($"[AIT] 마커를 찾을 수 없습니다: {markerName}");
                return content;
            }

            string before = content.Substring(0, startIdx);
            string after = content.Substring(endIdx + endMarker.Length);

            return before + newSection + after;
        }

        /// <summary>
        /// HTML 마커 섹션을 추출합니다.
        /// </summary>
        private static string ExtractHtmlUserSection(string content, string startMarker, string endMarker)
        {
            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                return null;
            }

            return content.Substring(startIdx, endIdx + endMarker.Length - startIdx);
        }

        /// <summary>
        /// HTML 마커 섹션을 교체합니다.
        /// </summary>
        private static string ReplaceHtmlUserSection(string content, string startMarker, string endMarker, string newSection)
        {
            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                Debug.LogWarning($"[AIT] HTML 마커를 찾을 수 없습니다: {startMarker}");
                return content;
            }

            string before = content.Substring(0, startIdx);
            string after = content.Substring(endIdx + endMarker.Length);

            return before + newSection + after;
        }

        /// <summary>
        /// package.json의 dependencies를 머지합니다.
        /// </summary>
        private static void MergePackageJson(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
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
        private static Dictionary<string, object> MergeDependencies(Dictionary<string, object> project, Dictionary<string, object> sdk)
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
        /// vite.config.ts를 마커 기반으로 업데이트합니다.
        /// </summary>
        private static void UpdateViteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "vite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "vite.config.ts");
            string destFile = Path.Combine(destPath, "vite.config.ts");

            // SDK 템플릿에서 SDK 섹션 생성
            string sdkTemplate = File.ReadAllText(sdkFile);
            string sdkSection = ExtractSdkSection(sdkTemplate);

            if (sdkSection == null)
            {
                Debug.LogError("[AIT] vite.config.ts에서 SDK 마커를 찾을 수 없습니다.");
                File.Copy(sdkFile, destFile, true);
                return;
            }

            // 플레이스홀더 치환
            sdkSection = sdkSection
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString());

            string finalContent;

            if (File.Exists(projectFile))
            {
                // 프로젝트 파일이 있으면 SDK 섹션만 교체
                string projectContent = File.ReadAllText(projectFile);

                // SDK 영역이 수정되었는지 확인
                string projectSdkSection = ExtractSdkSection(projectContent);
                if (projectSdkSection != null && projectSdkSection != ExtractSdkSection(sdkTemplate))
                {
                    Debug.LogWarning("[AIT] ⚠️ vite.config.ts의 SDK_GENERATED 영역이 수정되었습니다.");
                    Debug.LogWarning("[AIT]    SDK 설정으로 덮어쓰기됩니다. 커스텀 설정은 USER_CONFIG 영역에 추가하세요.");
                }

                finalContent = ReplaceMarkerSection(projectContent, sdkSection);
                Debug.Log("[AIT]   ✓ vite.config.ts (마커 기반 업데이트)");
            }
            else
            {
                // 프로젝트 파일이 없으면 SDK 템플릿 사용
                finalContent = sdkTemplate
                    .Replace("%AIT_VITE_HOST%", config.viteHost)
                    .Replace("%AIT_VITE_PORT%", config.vitePort.ToString());
                Debug.Log("[AIT]   ✓ vite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// granite.config.ts를 마커 기반으로 업데이트합니다.
        /// </summary>
        private static void UpdateGraniteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "granite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "granite.config.ts");
            string destFile = Path.Combine(destPath, "granite.config.ts");

            // SDK 템플릿에서 SDK 섹션 생성
            string sdkTemplate = File.ReadAllText(sdkFile);
            string sdkSection = ExtractSdkSection(sdkTemplate);

            if (sdkSection == null)
            {
                Debug.LogError("[AIT] granite.config.ts에서 SDK 마커를 찾을 수 없습니다.");
                return;
            }

            // 플레이스홀더 치환
            Debug.Log("[AIT] granite.config.ts placeholder 치환 중...");
            sdkSection = sdkSection
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ICON_URL%", config.iconUrl)
                .Replace("%AIT_BRIDGE_COLOR_MODE%", config.GetBridgeColorModeString())
                .Replace("%AIT_WEBVIEW_TYPE%", config.GetWebViewTypeString())
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString())
                .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson())
                .Replace("%AIT_OUTDIR%", config.outdir);

            string finalContent;

            if (File.Exists(projectFile))
            {
                // 프로젝트 파일이 있으면 SDK 섹션만 교체
                string projectContent = File.ReadAllText(projectFile);

                // SDK 영역이 수정되었는지 확인
                string projectSdkSection = ExtractSdkSection(projectContent);
                if (projectSdkSection != null && projectSdkSection != ExtractSdkSection(sdkTemplate))
                {
                    Debug.LogWarning("[AIT] ⚠️ granite.config.ts의 SDK_GENERATED 영역이 수정되었습니다.");
                    Debug.LogWarning("[AIT]    SDK 설정으로 덮어쓰기됩니다. 커스텀 설정은 USER_CONFIG 영역에 추가하세요.");
                }

                finalContent = ReplaceMarkerSection(projectContent, sdkSection);
                Debug.Log("[AIT]   ✓ granite.config.ts (마커 기반 업데이트)");
            }
            else
            {
                // 프로젝트 파일이 없으면 SDK 템플릿 사용
                finalContent = sdkTemplate
                    .Replace("%AIT_APP_NAME%", config.appName)
                    .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                    .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                    .Replace("%AIT_ICON_URL%", config.iconUrl)
                    .Replace("%AIT_BRIDGE_COLOR_MODE%", config.GetBridgeColorModeString())
                    .Replace("%AIT_WEBVIEW_TYPE%", config.GetWebViewTypeString())
                    .Replace("%AIT_VITE_HOST%", config.viteHost)
                    .Replace("%AIT_VITE_PORT%", config.vitePort.ToString())
                    .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson())
                    .Replace("%AIT_OUTDIR%", config.outdir);
                Debug.Log("[AIT]   ✓ granite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// 프로젝트 BuildConfig의 추가 파일들을 복사합니다.
        /// </summary>
        private static void CopyAdditionalUserFiles(string projectBuildConfigPath, string destPath)
        {
            if (!Directory.Exists(projectBuildConfigPath)) return;

            // 기본 파일 제외
            var excludeFiles = new HashSet<string>
            {
                "package.json", "pnpm-lock.yaml", "vite.config.ts",
                "tsconfig.json", "unity-bridge.ts", "granite.config.ts"
            };

            foreach (var file in Directory.GetFiles(projectBuildConfigPath))
            {
                string fileName = Path.GetFileName(file);
                if (excludeFiles.Contains(fileName)) continue;

                File.Copy(file, Path.Combine(destPath, fileName), true);
                Debug.Log($"[AIT]   ✓ {fileName} (사용자 추가 파일)");
            }
        }

        private static void CopyBuildConfigFromTemplate(string buildProjectPath)
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

            // 5. tsconfig.json - SDK 전용
            string tsconfigSrc = Path.Combine(sdkBuildConfigPath, "tsconfig.json");
            string tsconfigDst = Path.Combine(buildProjectPath, "tsconfig.json");
            File.Copy(tsconfigSrc, tsconfigDst, true);
            Debug.Log("[AIT]   ✓ tsconfig.json (SDK에서 복사)");

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

        private static void CopyWebGLToPublic(string webglPath, string buildProjectPath, AITBuildProfile profile = null)
        {
            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
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
                Debug.Log("[AIT] WebGL 빌드 파일 검색 중...");

                // 필수 파일들 (isRequired = true)
                string loaderFile = FindFileInBuild(buildSrc, "*.loader.js", isRequired: true);
                string dataFile = FindFileInBuild(buildSrc, "*.data*", isRequired: true);
                string frameworkFile = FindFileInBuild(buildSrc, "*.framework.js*", isRequired: true);
                string wasmFile = FindFileInBuild(buildSrc, "*.wasm*", isRequired: true);

                // 선택적 파일 (isRequired = false)
                string symbolsFile = FindFileInBuild(buildSrc, "*.symbols.json*", isRequired: false);

                // 필수 파일 검증 경고
                var missingFiles = new List<string>();
                if (string.IsNullOrEmpty(loaderFile)) missingFiles.Add("*.loader.js");
                if (string.IsNullOrEmpty(dataFile)) missingFiles.Add("*.data");
                if (string.IsNullOrEmpty(frameworkFile)) missingFiles.Add("*.framework.js");
                if (string.IsNullOrEmpty(wasmFile)) missingFiles.Add("*.wasm");

                if (missingFiles.Count > 0)
                {
                    Debug.LogError("[AIT] ========================================");
                    Debug.LogError("[AIT] ⚠ WebGL 빌드 파일 검증 경고!");
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
                    string userHeadSection = ExtractHtmlUserSection(projectIndexContent, HTML_USER_HEAD_START, HTML_USER_HEAD_END);
                    if (userHeadSection != null)
                    {
                        indexContent = ReplaceHtmlUserSection(indexContent, HTML_USER_HEAD_START, HTML_USER_HEAD_END, userHeadSection);
                        Debug.Log("[AIT] index.html USER_HEAD 섹션 머지됨");
                    }

                    // USER_BODY_END 섹션 추출 및 교체
                    string userBodyEndSection = ExtractHtmlUserSection(projectIndexContent, HTML_USER_BODY_END_START, HTML_USER_BODY_END_END);
                    if (userBodyEndSection != null)
                    {
                        indexContent = ReplaceHtmlUserSection(indexContent, HTML_USER_BODY_END_START, HTML_USER_BODY_END_END, userBodyEndSection);
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
                    .Replace("%UNITY_WEBGL_LOADER_URL%", $"Build/{loaderFile}")
                    .Replace("%UNITY_WEBGL_LOADER_FILENAME%", loaderFile)
                    .Replace("%UNITY_WEBGL_DATA_FILENAME%", dataFile)
                    .Replace("%UNITY_WEBGL_FRAMEWORK_FILENAME%", frameworkFile)
                    .Replace("%UNITY_WEBGL_CODE_FILENAME%", wasmFile)
                    .Replace("%UNITY_WEBGL_SYMBOLS_FILENAME%", symbolsFile)
                    .Replace("%AIT_IS_PRODUCTION%", isProduction)
                    .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", enableDebugConsole)
                    .Replace("%AIT_DEVICE_PIXEL_RATIO%", config.devicePixelRatio.ToString());

                File.WriteAllText(indexDest, indexContent, System.Text.Encoding.UTF8);
                Debug.Log("[AIT] index.html → 프로젝트 루트에 생성");

                // 플레이스홀더 치환 결과 검증
                ValidatePlaceholderSubstitution(indexContent, indexDest);
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

        /// <summary>
        /// index.html에서 미치환된 플레이스홀더가 있는지 검증합니다.
        /// </summary>
        private static void ValidatePlaceholderSubstitution(string content, string filePath)
        {
            // %로 시작하고 %로 끝나는 패턴 검색 (예: %UNITY_WEBGL_LOADER_URL%)
            var regex = new System.Text.RegularExpressions.Regex(@"%[A-Z_]+%");
            var matches = regex.Matches(content);

            if (matches.Count > 0)
            {
                Debug.LogWarning("[AIT] ========================================");
                Debug.LogWarning("[AIT] ⚠ 미치환된 플레이스홀더 발견!");
                Debug.LogWarning("[AIT] ========================================");

                var uniquePlaceholders = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    uniquePlaceholders.Add(match.Value);
                }

                foreach (var placeholder in uniquePlaceholders)
                {
                    Debug.LogWarning($"[AIT]   - {placeholder}");
                }

                Debug.LogWarning($"[AIT] 파일: {filePath}");
                Debug.LogWarning("[AIT] 이 플레이스홀더들이 치환되지 않으면 런타임 에러가 발생할 수 있습니다.");
                Debug.LogWarning("[AIT] ========================================");
            }

            // 잘못된 경로 패턴 검증 (예: Build/ 뒤에 파일명이 없는 경우)
            if (content.Contains("src=\"Build/\"") || content.Contains("\"Build/\"") || content.Contains("Build/\","))
            {
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] ✗ 치명적: 빈 파일 경로 발견!");
                Debug.LogError("[AIT] ========================================");
                Debug.LogError("[AIT] index.html에 'Build/' 뒤에 파일명이 없는 경로가 있습니다.");
                Debug.LogError("[AIT] 이로 인해 'createUnityInstance is not defined' 에러가 발생합니다.");
                Debug.LogError("[AIT] ");
                Debug.LogError("[AIT] 원인: WebGL 빌드의 loader.js 파일을 찾지 못했습니다.");
                Debug.LogError("[AIT] 해결: 위의 빌드 파일 검색 로그를 확인하세요.");
                Debug.LogError("[AIT] ========================================");
            }
        }

        /// <summary>
        /// 빌드 완료 후 결과 리포트를 출력합니다.
        /// </summary>
        private static void PrintBuildReport(string buildProjectPath, string distPath)
        {
            Debug.Log("[AIT] ========================================");
            Debug.Log("[AIT] 빌드 완료 리포트");
            Debug.Log("[AIT] ========================================");

            // 1. 필수 파일 존재 확인
            string publicBuildPath = Path.Combine(buildProjectPath, "public", "Build");

            if (!Directory.Exists(publicBuildPath))
            {
                Debug.LogError($"[AIT] ✗ Build 폴더가 존재하지 않습니다: {publicBuildPath}");
                Debug.Log("[AIT] ========================================");
                return;
            }

            var requiredPatterns = new Dictionary<string, string>
            {
                { "*.loader.js", "Unity WebGL 로더 (필수)" },
                { "*.data*", "게임 데이터 (필수)" },
                { "*.framework.js*", "Unity 프레임워크 (필수)" },
                { "*.wasm*", "WebAssembly 바이너리 (필수)" }
            };

            var optionalPatterns = new Dictionary<string, string>
            {
                { "*.symbols.json*", "디버그 심볼 (선택)" }
            };

            bool hasErrors = false;

            Debug.Log("[AIT] ");
            Debug.Log("[AIT] WebGL 빌드 파일:");

            foreach (var kvp in requiredPatterns)
            {
                var files = Directory.GetFiles(publicBuildPath, kvp.Key);
                if (files.Length > 0)
                {
                    var fileInfo = new FileInfo(files[0]);
                    string size = FormatFileSize(fileInfo.Length);
                    Debug.Log($"[AIT]   ✓ {Path.GetFileName(files[0])} ({size}) - {kvp.Value}");
                }
                else
                {
                    Debug.LogError($"[AIT]   ✗ {kvp.Key} - {kvp.Value} [누락됨!]");
                    hasErrors = true;
                }
            }

            foreach (var kvp in optionalPatterns)
            {
                var files = Directory.GetFiles(publicBuildPath, kvp.Key);
                if (files.Length > 0)
                {
                    var fileInfo = new FileInfo(files[0]);
                    string size = FormatFileSize(fileInfo.Length);
                    Debug.Log($"[AIT]   ○ {Path.GetFileName(files[0])} ({size}) - {kvp.Value}");
                }
            }

            // 2. index.html 검증
            string indexPath = Path.Combine(buildProjectPath, "index.html");
            if (File.Exists(indexPath))
            {
                string indexContent = File.ReadAllText(indexPath);

                // 빈 경로 검사
                bool hasBadPath = indexContent.Contains("src=\"Build/\"") ||
                                  indexContent.Contains("\"Build/\"") ||
                                  indexContent.Contains("Build/\",");

                if (hasBadPath)
                {
                    Debug.LogError("[AIT]   ✗ index.html에 잘못된 빌드 경로 발견!");
                    hasErrors = true;
                }
                else
                {
                    Debug.Log("[AIT]   ✓ index.html 경로 검증 통과");
                }
            }

            // 3. 최종 결과
            Debug.Log("[AIT] ");
            if (hasErrors)
            {
                Debug.LogError("[AIT] ⚠ 빌드에 문제가 있습니다!");
                Debug.LogError("[AIT] 위의 에러를 확인하고 Clean Build를 실행하세요.");
            }
            else
            {
                Debug.Log("[AIT] ✅ 모든 필수 파일 확인 완료");
                Debug.Log($"[AIT] 배포 폴더: {distPath}");
            }
            Debug.Log("[AIT] ========================================");
        }

        /// <summary>
        /// 파일 크기를 읽기 쉬운 형식으로 변환합니다.
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {suffixes[order]}";
        }

        /// <summary>
        /// Build 폴더에서 패턴에 맞는 파일을 찾습니다.
        /// </summary>
        /// <param name="buildPath">검색할 빌드 경로</param>
        /// <param name="pattern">파일 패턴 (예: *.loader.js)</param>
        /// <param name="isRequired">필수 파일 여부 (true면 못 찾을 경우 에러 로그)</param>
        /// <returns>찾은 파일명 또는 빈 문자열</returns>
        private static string FindFileInBuild(string buildPath, string pattern, bool isRequired = false)
        {
            if (!Directory.Exists(buildPath))
            {
                if (isRequired)
                {
                    Debug.LogError($"[AIT] 빌드 경로가 존재하지 않습니다: {buildPath}");
                }
                return "";
            }

            var files = Directory.GetFiles(buildPath, pattern);
            if (files.Length > 0)
            {
                string fileName = Path.GetFileName(files[0]);
                if (isRequired)
                {
                    Debug.Log($"[AIT]   ✓ {pattern} → {fileName}");
                }
                return fileName;
            }

            if (isRequired)
            {
                Debug.LogError($"[AIT] ✗ 필수 파일을 찾을 수 없습니다: {pattern}");
                Debug.LogError($"[AIT]   검색 경로: {buildPath}");
                Debug.LogError($"[AIT]   이 파일이 없으면 런타임에서 'createUnityInstance is not defined' 에러가 발생합니다.");

                // 실제로 어떤 파일들이 있는지 출력
                var existingFiles = Directory.GetFiles(buildPath);
                if (existingFiles.Length > 0)
                {
                    Debug.LogError($"[AIT]   Build 폴더의 실제 파일들:");
                    foreach (var file in existingFiles)
                    {
                        Debug.LogError($"[AIT]     - {Path.GetFileName(file)}");
                    }
                }
                else
                {
                    Debug.LogError($"[AIT]   Build 폴더가 비어 있습니다!");
                }
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
            string requiredVersion = AITPackageManagerHelper.PNPM_VERSION;

            // 1. 내장 Node.js bin 디렉토리에서 pnpm 찾기
            string embeddedNodeBinPath = AppsInToss.Editor.AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            if (!string.IsNullOrEmpty(embeddedNodeBinPath))
            {
                string pnpmInEmbedded = Path.Combine(embeddedNodeBinPath, AppsInToss.Editor.AITPlatformHelper.GetExecutableName("pnpm"));
                string npmPath = Path.Combine(embeddedNodeBinPath, AppsInToss.Editor.AITPlatformHelper.GetExecutableName("npm"));

                if (File.Exists(pnpmInEmbedded))
                {
                    // 버전 확인
                    string installedVersion = GetPnpmVersion(pnpmInEmbedded, embeddedNodeBinPath);
                    if (!string.IsNullOrEmpty(installedVersion))
                    {
                        if (installedVersion == requiredVersion)
                        {
                            Debug.Log($"[AIT] ✓ 내장 pnpm v{installedVersion} 발견 (요구 버전과 일치)");
                            return pnpmInEmbedded;
                        }
                        else
                        {
                            Debug.Log($"[AIT] 내장 pnpm v{installedVersion} 발견 (요구 버전: v{requiredVersion})");
                            Debug.Log($"[AIT] pnpm을 v{requiredVersion}으로 업데이트합니다...");

                            // 버전이 다르면 재설치
                            if (File.Exists(npmPath) && InstallPnpmWithNpm(npmPath, embeddedNodeBinPath, requiredVersion))
                            {
                                Debug.Log($"[AIT] ✓ pnpm v{requiredVersion} 설치 완료");
                                return pnpmInEmbedded;
                            }
                        }
                    }
                    else
                    {
                        // 버전 확인 실패 시 그냥 사용
                        Debug.Log($"[AIT] ✓ 내장 pnpm 발견 (버전 확인 불가): {pnpmInEmbedded}");
                        return pnpmInEmbedded;
                    }
                }

                // 2. pnpm이 없으면 npm으로 글로벌 설치
                Debug.Log($"[AIT] 내장 pnpm이 없습니다. npm install -g pnpm@{requiredVersion} 실행 중...");

                if (File.Exists(npmPath))
                {
                    if (InstallPnpmWithNpm(npmPath, embeddedNodeBinPath, requiredVersion))
                    {
                        Debug.Log("[AIT] ✓ pnpm 글로벌 설치 완료");

                        // 설치 후 pnpm 경로 다시 확인
                        if (File.Exists(pnpmInEmbedded))
                        {
                            return pnpmInEmbedded;
                        }
                    }
                }
            }

            // 3. 시스템 pnpm 검색 (fallback)
            return AppsInToss.Editor.AITPackageManagerHelper.FindExecutable("pnpm", verbose: true);
        }

        /// <summary>
        /// pnpm 버전 확인
        /// </summary>
        private static string GetPnpmVersion(string pnpmPath, string workingDir)
        {
            try
            {
                var result = AppsInToss.Editor.AITPlatformHelper.ExecuteCommand(
                    $"\"{pnpmPath}\" --version",
                    workingDir,
                    new[] { workingDir },
                    timeoutMs: 10000,
                    verbose: false
                );

                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    return result.Output.Trim();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AIT] pnpm 버전 확인 실패: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// npm을 사용해 pnpm 설치
        /// </summary>
        private static bool InstallPnpmWithNpm(string npmPath, string workingDir, string version)
        {
            string command = $"\"{npmPath}\" install -g pnpm@{version}";
            var result = AppsInToss.Editor.AITPlatformHelper.ExecuteCommand(
                command,
                workingDir,
                new[] { workingDir },
                timeoutMs: 120000, // 2분
                verbose: true
            );

            if (!result.Success)
            {
                Debug.LogError($"[AIT] pnpm 설치 실패: {result.Error}");
                return false;
            }
            return true;
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
            // install, install --frozen-lockfile 등 모든 install 명령어에 적용
            bool isInstallCommand = arguments.TrimStart().StartsWith("install");
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
                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        Debug.LogError($"[{pmName}] 출력:\n{result.Output}");
                    }
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Debug.LogError($"[{pmName}] 오류:\n{result.Error}");
                    }
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
    /// 간단한 JSON 파서/직렬화 유틸리티
    /// package.json의 dependencies 머지에 사용
    /// </summary>
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            int index = 0;
            return ParseValue(json, ref index);
        }

        public static string Serialize(object obj)
        {
            var sb = new System.Text.StringBuilder();
            SerializeValue(obj, sb, 0);
            return sb.ToString();
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            if (char.IsDigit(c) || c == '-') return ParseNumber(json, ref index);

            return null;
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var obj = new Dictionary<string, object>();
            index++; // skip '{'
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != '}')
            {
                SkipWhitespace(json, ref index);
                if (json[index] == '}') break;

                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                index++; // skip ':'
                SkipWhitespace(json, ref index);
                object value = ParseValue(json, ref index);
                obj[key] = value;

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }

            if (index < json.Length) index++; // skip '}'
            return obj;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var arr = new List<object>();
            index++; // skip '['
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != ']')
            {
                arr.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
                SkipWhitespace(json, ref index);
            }

            if (index < json.Length) index++; // skip ']'
            return arr;
        }

        private static string ParseString(string json, ref int index)
        {
            index++; // skip opening '"'
            var sb = new System.Text.StringBuilder();

            while (index < json.Length && json[index] != '"')
            {
                if (json[index] == '\\' && index + 1 < json.Length)
                {
                    index++;
                    char escaped = json[index];
                    if (escaped == 'n') sb.Append('\n');
                    else if (escaped == 't') sb.Append('\t');
                    else if (escaped == 'r') sb.Append('\r');
                    else sb.Append(escaped);
                }
                else
                {
                    sb.Append(json[index]);
                }
                index++;
            }

            if (index < json.Length) index++; // skip closing '"'
            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == '-' || json[index] == 'e' || json[index] == 'E' || json[index] == '+'))
            {
                index++;
            }
            string numStr = json.Substring(start, index - start);
            if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
            {
                double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d);
                return d;
            }
            long.TryParse(numStr, out long l);
            return l;
        }

        private static bool ParseBool(string json, ref int index)
        {
            if (json.Substring(index, 4) == "true") { index += 4; return true; }
            if (json.Substring(index, 5) == "false") { index += 5; return false; }
            return false;
        }

        private static object ParseNull(string json, ref int index)
        {
            index += 4; // "null"
            return null;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private static void SerializeValue(object value, System.Text.StringBuilder sb, int indent)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string str)
            {
                sb.Append('"');
                sb.Append(str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t"));
                sb.Append('"');
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is Dictionary<string, object> dict)
            {
                sb.AppendLine("{");
                int i = 0;
                foreach (var kvp in dict)
                {
                    sb.Append(new string(' ', (indent + 1) * 2));
                    sb.Append('"');
                    sb.Append(kvp.Key);
                    sb.Append("\": ");
                    SerializeValue(kvp.Value, sb, indent + 1);
                    if (i < dict.Count - 1) sb.Append(',');
                    sb.AppendLine();
                    i++;
                }
                sb.Append(new string(' ', indent * 2));
                sb.Append('}');
            }
            else if (value is List<object> list)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    SerializeValue(list[i], sb, indent);
                    if (i < list.Count - 1) sb.Append(", ");
                }
                sb.Append(']');
            }
            else if (value is long || value is int)
            {
                sb.Append(value.ToString());
            }
            else if (value is double || value is float)
            {
                sb.Append(((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append('"');
                sb.Append(value.ToString());
                sb.Append('"');
            }
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
