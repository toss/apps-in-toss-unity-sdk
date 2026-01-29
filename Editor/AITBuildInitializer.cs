using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Unity PlayerSettings 초기화 및 빌드 프로필 적용
    /// </summary>
    internal static class AITBuildInitializer
    {
        /// <summary>
        /// Unity WebGL 빌드 설정 초기화
        /// </summary>
        internal static void Init(AITBuildProfile profile = null)
        {
            // WebGL 템플릿 복사 (필요한 경우)
            AITTemplateManager.EnsureWebGLTemplatesExist();

            // 템플릿이 복사되었을 경우 Unity가 인식하도록 강제 리프레시
            AssetDatabase.Refresh();

            var editorConfig = UnityUtil.GetEditorConf();

            // Unity 버전 정보
            Debug.Log($"[AIT] 현재 Unity 버전: {Application.unityVersion} ({AITDefaultSettings.GetUnityVersionGroup()})");

            // ===== 기본 설정 (모든 버전 공통) =====
            PlayerSettings.WebGL.template = "PROJECT:AITTemplate";
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
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
            WebGLCompressionFormat compressionFormat = profile != null
                ? ConvertToCompressionFormat(profile.compressionFormat)
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

            // ===== Unity 로고 표시: 사용자의 PlayerSettings 설정을 그대로 유지 =====

            // ===== 디버그 심볼 (빌드 프로필에서 설정 - ApplyBuildProfileSettings 참조) =====
            // 프로필 기반 설정은 DoExport()에서 ApplyBuildProfileSettings()를 통해 적용됨

            // ===== Decompression Fallback (사용자 지정 또는 자동) =====
            // 출처: StartupOptimization.md:93
            bool decompressionFallback = editorConfig.decompressionFallback >= 0
                ? editorConfig.decompressionFallback == 1
                : AITDefaultSettings.GetDefaultDecompressionFallback();
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;

            // ===== Show Diagnostics (Unity 2022.2+, 기본 활성화) =====
#if UNITY_2022_2_OR_NEWER
            bool showDiagnostics = editorConfig.showDiagnostics >= 0
                ? editorConfig.showDiagnostics == 1
                : true; // 기본값: 활성화 (Unity 메트릭 수집을 위해)
            PlayerSettings.WebGL.showDiagnostics = showDiagnostics;
#endif

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
            Debug.Log($"[AIT]   - Run In Background: {runInBackground}{(editorConfig.runInBackground < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Decompression Fallback: {decompressionFallback}{(editorConfig.decompressionFallback < 0 ? " (자동)" : "")}");
#if UNITY_2023_3_OR_NEWER
            Debug.Log($"[AIT]   - Power Preference: {powerPreference}{(editorConfig.powerPreference < 0 ? " (자동)" : "")}");
#if !UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - WASM Streaming: {editorConfig.wasmStreaming}");
#endif
#endif
#if UNITY_2022_2_OR_NEWER
            Debug.Log($"[AIT]   - Show Diagnostics: {showDiagnostics}{(editorConfig.showDiagnostics < 0 ? " (자동)" : "")}");
#endif
        }

        /// <summary>
        /// 프로필 저장값을 WebGLCompressionFormat enum으로 변환
        /// 저장값: -1=자동, 0=Disabled, 1=Gzip, 2=Brotli
        /// enum값: 0=Brotli, 1=Gzip, 2=Disabled
        /// </summary>
        private static WebGLCompressionFormat ConvertToCompressionFormat(int storedValue)
        {
            return storedValue switch
            {
                0 => WebGLCompressionFormat.Disabled,
                1 => WebGLCompressionFormat.Gzip,
                2 => WebGLCompressionFormat.Brotli,
                _ => AITDefaultSettings.GetDefaultCompressionFormat()
            };
        }

        /// <summary>
        /// 빌드 프로필 정보를 로그로 출력
        /// </summary>
        internal static void LogBuildProfile(AITBuildProfile profile, string profileName)
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
        internal static AITBuildProfile ApplyEnvironmentVariableOverrides(AITBuildProfile profile)
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
        internal static void ApplyBuildProfileSettings(AITBuildProfile profile)
        {
            // 디버그 심볼 설정 (Unity 2022.3+)
#if UNITY_2022_3_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = profile.debugSymbolsExternal
                ? WebGLDebugSymbolMode.External
                : WebGLDebugSymbolMode.Embedded;
            Debug.Log($"[AIT] 디버그 심볼 모드 설정: {PlayerSettings.WebGL.debugSymbolMode}");
#endif
        }
    }
}
