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
            bool templatesChanged = AITTemplateManager.EnsureWebGLTemplatesExist();
            Debug.Log($"[AIT] 빌드 초기화: 템플릿 변경={templatesChanged}");

            // 템플릿이 변경된 경우에만 Unity가 인식하도록 리프레시
            // Domain Reload 방지: 빌드 중 Assembly 리로드를 잠금하여
            // 비-스크립트 파일 변경으로 인한 불필요한 Domain Reload를 차단
            if (templatesChanged)
            {
                Debug.Log("[AIT] AssetDatabase.Refresh 시작 (LockReloadAssemblies 적용)");
                EditorApplication.LockReloadAssemblies();
                try
                {
                    AssetDatabase.Refresh();
                }
                finally
                {
                    EditorApplication.UnlockReloadAssemblies();
                }
                Debug.Log("[AIT] AssetDatabase.Refresh 완료");
            }

            var editorConfig = UnityUtil.GetEditorConf();

            // Unity 버전 정보
            Debug.Log($"[AIT] 현재 Unity 버전: {Application.unityVersion} ({AITDefaultSettings.GetUnityVersionGroup()})");

            // ===== 기본 설정 (모든 버전 공통) =====
            PlayerSettings.WebGL.template = "PROJECT:AITTemplate";
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.defaultCursor = null;
            PlayerSettings.cursorHotspot = Vector2.zero;

            // ===== Graphics API: WebGL 2.0 전용 =====
            // WebGL 1 + WebGL 2 동시 설정 시, Emscripten이 WebGL 1 context를 먼저 생성한 후
            // WebGL 2를 시도하면 "Canvas has an existing context of a different type" 크래시 발생.
            // Apps in Toss는 Toss 앱 WebView(Android Chrome, iOS Safari)에서만 실행되므로
            // WebGL 2.0만 지원하면 충분함.
            var currentAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            bool needsGraphicsAPIUpdate = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL)
                || currentAPIs.Length != 1
                || currentAPIs[0] != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3;

            if (needsGraphicsAPIUpdate)
            {
                var previousAPIs = string.Join(", ", currentAPIs);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL,
                    new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
                Debug.Log($"[AIT] Graphics API를 WebGL 2.0 전용으로 변경했습니다. (이전: {previousAPIs})");
            }

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
            // 실제 적용은 아래 ApplySentryFriendlyWebGLSettings에서 수행 (stack trace와 함께 관리)
            WebGLExceptionSupport exceptionSupport = editorConfig.exceptionSupport >= 0
                ? (WebGLExceptionSupport)editorConfig.exceptionSupport
                : AITDefaultSettings.GetDefaultExceptionSupport();

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
                Debug.Log("[AIT] Unity 2021.x에서는 '파일명 해싱' 옵션이 빌드 오류를 유발하여 자동으로 비활성화됩니다. Unity 2022.3 이상으로 업그레이드를 권장합니다.");
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

            // ===== Sentry 친화 설정 (WebGL exception support + stack trace) =====
            // Capture() 스냅샷이 반드시 이보다 먼저 찍혀야 Restore()가 의미 있음 (DoExport 참조).
            ApplySentryFriendlyWebGLSettings(exceptionSupport);

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

            // E2E CI 한정 오버라이드: IL2CPP 컴파일러 옵티마이저 레벨을 줄여 Link_WebGL_wasm 단축.
            // developmentBuild 플래그는 Player 측 옵션이며 IL2CPP 옵티마이저와 별개라 별도 변수 필요.
            string il2cppConfigEnv = System.Environment.GetEnvironmentVariable("AIT_IL2CPP_CONFIGURATION");
            if (!string.IsNullOrEmpty(il2cppConfigEnv))
            {
                if (System.Enum.TryParse<Il2CppCompilerConfiguration>(il2cppConfigEnv, ignoreCase: true, out var parsed))
                {
                    il2cppConfig = parsed;
                    Debug.Log($"[AIT] 환경 변수 오버라이드: AIT_IL2CPP_CONFIGURATION={parsed}");
                }
                else
                {
                    Debug.LogWarning($"[AIT] AIT_IL2CPP_CONFIGURATION 환경 변수 값이 올바르지 않습니다: '{il2cppConfigEnv}' (Debug/Release/Master 필요)");
                }
            }

#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, il2cppConfig);
#else
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, il2cppConfig);
#endif

            // ===== IL2CPP Code Generation (Meta 로드타임 스택: OptimizeSize) =====
            // 제네릭 인스턴스화 공유로 wasm 코드 크기 축소. Unity 6+ 전용 API.
#if UNITY_6000_0_OR_NEWER
            Il2CppCodeGeneration il2cppCodeGen = editorConfig.il2cppCodeGeneration >= 0
                ? (Il2CppCodeGeneration)editorConfig.il2cppCodeGeneration
                : AITDefaultSettings.GetDefaultIl2CppCodeGeneration();
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, il2cppCodeGen);
#endif

            // ===== WebAssembly 2023 (Meta 로드타임 스택: native exception/SIMD/BigInt/Table) =====
            // 코드 크기·다운로드·시작 시간 단축. 미지원 브라우저에서는 로드 실패하므로
            // Apps in Toss WebView min-spec(Chrome≥91 / Safari≥16.4) 충족이 전제.
#if UNITY_6000_0_OR_NEWER
            bool wasm2023 = editorConfig.wasm2023 >= 0
                ? editorConfig.wasm2023 == 1
                : AITDefaultSettings.GetDefaultWasm2023();
            PlayerSettings.WebGL.wasm2023 = wasm2023;
#endif

            // ===== WebGL Code Optimization (Meta 로드타임 스택의 실제 "Disk Size with LTO" 레버) =====
            // IL2CPP 컴파일러 config(Master)는 WebGL emscripten 최적화/LTO에 영향을 주지 않으므로(no-op),
            // 실제 LTO는 emscripten code optimization으로 켠다. 버전별 API 차이(2022.3/6: UserBuildSettings,
            // 구버전: PlayerSettings.WebGL) + WebGL 모듈 어셈블리 참조 보장 부재 때문에
            // AITWebGLCodeOptimization이 reflection으로 적용한다(멤버 없는 버전은 fail-safe로 건너뜀).
            // editorConfig.webGLCodeOptimization: -1(자동)/1 → DiskSizeLTO 적용, 0 → 미적용(Unity 설정 유지).
            bool applyWebGLCodeOpt = editorConfig.webGLCodeOptimization != 0;
            string webGLCodeOptTarget = AITDefaultSettings.GetDefaultWebGLCodeOptimization();
            bool webGLCodeOptApplied = false;
            if (applyWebGLCodeOpt)
            {
                webGLCodeOptApplied = AITWebGLCodeOptimization.TrySetByName(webGLCodeOptTarget);
                if (!webGLCodeOptApplied)
                {
                    Debug.LogWarning(
                        $"[AIT] WebGL Code Optimization({webGLCodeOptTarget}) 미적용 — 이 Unity 버전에서 API/멤버 부재 (빌드는 계속)");
                }
            }

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

            // ===== 콘텐츠 축소 (빌드 산출물 .data 실감축, 빌드 후 스냅샷 복원) =====
            // 두 레버 모두 프로젝트 전역 PlayerSettings이므로 PlayerSettingsSnapshot이 캡처/복원한다
            // (영구 오염 방지). 설정 출력은 바꾸지 않고(미사용 데이터만 제거) 산출물 크기만 줄인다.
            bool mipStripping = editorConfig.mipStripping >= 0
                ? editorConfig.mipStripping == 1
                : AITDefaultSettings.GetDefaultMipStripping();
            PlayerSettings.mipStripping = mipStripping;

            bool stripUnusedMeshComponents = editorConfig.stripUnusedMeshComponents >= 0
                ? editorConfig.stripUnusedMeshComponents == 1
                : AITDefaultSettings.GetDefaultStripUnusedMeshComponents();
            PlayerSettings.stripUnusedMeshComponents = stripUnusedMeshComponents;

            // 설정 요약 로그
            Debug.Log($"[AIT] Unity {AITDefaultSettings.GetUnityVersionGroup()} 최적화 설정 적용:");
            Debug.Log($"[AIT]   - WebGL Template: {PlayerSettings.WebGL.template}");
            Debug.Log($"[AIT]   - Graphics API: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL))}");
            Debug.Log($"[AIT]   - 메모리: {memorySize}MB{(editorConfig.memorySize <= 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 압축: {compressionFormat}{(profile?.compressionFormat < 0 || profile == null ? " (자동)" : " (프로필)")}");
            Debug.Log($"[AIT]   - 스레딩: {threadsSupport}{(editorConfig.threadsSupport < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 데이터 캐싱: {dataCaching}{(editorConfig.dataCaching < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - 예외 처리: {exceptionSupport}{(editorConfig.exceptionSupport < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Stack Trace Log Type (Error/Assert/Warning/Log/Exception): {PlayerSettings.GetStackTraceLogType(LogType.Error)} (WebGL 자동)");
            Debug.Log($"[AIT]   - Stripping Level: {strippingLevel}{(profile?.managedStrippingLevel < 0 || profile == null ? " (자동)" : " (프로필)")}");
            Debug.Log($"[AIT]   - IL2CPP 설정: {il2cppConfig}{(!string.IsNullOrEmpty(il2cppConfigEnv) ? " (환경 변수)" : editorConfig.il2cppConfiguration < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Run In Background: {runInBackground}{(editorConfig.runInBackground < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Decompression Fallback: {decompressionFallback}{(editorConfig.decompressionFallback < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Mip Stripping: {mipStripping}{(editorConfig.mipStripping < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Optimize Mesh Data: {stripUnusedMeshComponents}{(editorConfig.stripUnusedMeshComponents < 0 ? " (자동)" : "")}");
            string webGLCodeOptLog = !applyWebGLCodeOpt ? "미적용 (off)"
                : webGLCodeOptApplied ? $"{webGLCodeOptTarget}{(editorConfig.webGLCodeOptimization < 0 ? " (자동)" : "")}"
                : "미적용 (API 부재)";
            Debug.Log($"[AIT]   - WebGL Code Optimization: {webGLCodeOptLog}");
#if UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - IL2CPP Code Generation: {il2cppCodeGen}{(editorConfig.il2cppCodeGeneration < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - WebAssembly 2023: {wasm2023}{(editorConfig.wasm2023 < 0 ? " (자동)" : "")}");
#endif
#if UNITY_2023_3_OR_NEWER
            Debug.Log($"[AIT]   - Power Preference: {powerPreference}{(editorConfig.powerPreference < 0 ? " (자동)" : "")}");
#if !UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - WASM Streaming: {editorConfig.wasmStreaming}");
#endif
#endif
        }

        /// <summary>
        /// Sentry/에러 추적 SDK가 요구하는 WebGL 설정을 적용한다.
        /// 호출 위치: <see cref="Init"/> 내 IL2CPP 설정 직후 (Init이 이 메서드에 위임).
        /// - WebGL exceptionSupport를 지정 값으로 설정 (기본 FullWithStacktrace — stack trace 캡처 가능)
        /// - Stack Trace Log Type은 WebGL에서 지원되는 ScriptOnly로 고정 (Full은 IL2CPP/WebGL 조합 미지원)
        ///
        /// 주의: PlayerSettings.SetStackTraceLogType은 플랫폼별이 아닌 프로젝트 전역 설정이다.
        /// 사용자 PlayerSettings가 영구 변경되지 않도록, 호출 직전에 PlayerSettingsSnapshot.Capture()가
        /// 실행되어 있어야 한다 (AITConvertCore.DoExport 참조). 이 메서드는 Init() 외부에서도
        /// 테스트가 부수 효과(AssetDatabase.Refresh 등) 없이 설정만 검증할 수 있도록 분리되었다.
        /// </summary>
        internal static void ApplySentryFriendlyWebGLSettings(WebGLExceptionSupport exceptionSupport)
        {
            PlayerSettings.WebGL.exceptionSupport = exceptionSupport;

            // 경고 방지: "The 'Method Name, File Name, and Line Number' option for IL2CPP stack traces is not supported on WebGL."
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
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

            // 환경 변수 읽기
            string debugConsoleEnv = System.Environment.GetEnvironmentVariable("AIT_DEBUG_CONSOLE");
            string compressionFormatEnv = System.Environment.GetEnvironmentVariable("AIT_COMPRESSION_FORMAT");
            string developmentBuildEnv = System.Environment.GetEnvironmentVariable("AIT_DEVELOPMENT_BUILD");

            // 오버라이드할 항목이 없으면 원본 반환
            if (string.IsNullOrEmpty(debugConsoleEnv) && string.IsNullOrEmpty(compressionFormatEnv) && string.IsNullOrEmpty(developmentBuildEnv))
                return profile;

            // 복사본 생성 (새 필드 추가 시 누락 방지)
            var overriddenProfile = profile.Clone();

            // AIT_DEBUG_CONSOLE 오버라이드
            if (!string.IsNullOrEmpty(debugConsoleEnv))
            {
                if (bool.TryParse(debugConsoleEnv, out bool debugConsole))
                {
                    overriddenProfile.enableDebugConsole = debugConsole;
                    Debug.Log($"[AIT] 환경 변수 오버라이드: AIT_DEBUG_CONSOLE={debugConsole}");
                }
                else
                {
                    Debug.LogWarning($"[AIT] AIT_DEBUG_CONSOLE 환경 변수 값이 올바르지 않습니다: '{debugConsoleEnv}' (true/false 필요)");
                }
            }

            // AIT_COMPRESSION_FORMAT 오버라이드
            // 값: -1 = 자동, 0 = Disabled, 1 = Gzip, 2 = Brotli
            if (!string.IsNullOrEmpty(compressionFormatEnv))
            {
                if (int.TryParse(compressionFormatEnv, out int compressionFormat) && compressionFormat >= -1 && compressionFormat <= 2)
                {
                    overriddenProfile.compressionFormat = compressionFormat;
                    Debug.Log($"[AIT] 환경 변수 오버라이드: AIT_COMPRESSION_FORMAT={compressionFormat}");
                }
                else
                {
                    Debug.LogWarning($"[AIT] AIT_COMPRESSION_FORMAT 환경 변수 값이 올바르지 않습니다: '{compressionFormatEnv}' (-1/0/1/2 필요)");
                }
            }

            // AIT_DEVELOPMENT_BUILD 오버라이드
            // E2E CI에서 Emscripten 옵티마이저 단축으로 Link_WebGL_wasm 단계 시간 절감 목적.
            if (!string.IsNullOrEmpty(developmentBuildEnv))
            {
                if (bool.TryParse(developmentBuildEnv, out bool developmentBuild))
                {
                    overriddenProfile.developmentBuild = developmentBuild;
                    Debug.Log($"[AIT] 환경 변수 오버라이드: AIT_DEVELOPMENT_BUILD={developmentBuild}");
                }
                else
                {
                    Debug.LogWarning($"[AIT] AIT_DEVELOPMENT_BUILD 환경 변수 값이 올바르지 않습니다: '{developmentBuildEnv}' (true/false 필요)");
                }
            }

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
