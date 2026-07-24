using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

            // Unity WebGL 템플릿 전처리기 크래시 방지 (SDK-137):
            // BuildConfig~ 하위에 잔존하는 비-소스 산출물(node_modules 등)을 BuildPlayer 직전에 제거.
            ScrubTemplatePreprocessorHazards();

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

            // ===== WebGL Code Optimization (Meta 로드타임 스택의 실제 "Disk Size with LTO" 레버) =====
            // IL2CPP 컴파일러 config(Master)는 WebGL emscripten 최적화/LTO에 영향을 주지 않으므로(no-op),
            // 실제 LTO는 emscripten code optimization으로 켠다. 버전별 API 차이(2022.3/6: UserBuildSettings,
            // 구버전: PlayerSettings.WebGL) + WebGL 모듈 어셈블리 참조 보장 부재 때문에
            // AITWebGLCodeOptimization이 reflection으로 적용한다(멤버 없는 버전은 fail-safe로 건너뜀).
            // editorConfig.webGLCodeOptimization: -1(자동)/1 → DiskSizeLTO 적용, 0 → 미적용(Unity 설정 유지).
            bool applyWebGLCodeOpt = editorConfig.webGLCodeOptimization != 0;
            string webGLCodeOptTarget = AITDefaultSettings.GetDefaultWebGLCodeOptimization();
            bool webGLCodeOptApplied = false;
            // Unity 6000.0의 emscripten 툴체인은 대형 프로젝트의 whole-program LTO 링크에서
            // wasm-ld가 빌드 머신 메모리(>65GB)를 초과해 OOM(SIGKILL "Killed: 9")으로 빌드를 깨뜨린다.
            // 6000.3의 신형 툴체인은 동일한 DiskSizeLTO를 정상 링크함을 실측 확인했다.
            // 따라서 6000.0.x에서만 LTO 적용을 건너뛰어 하드 빌드 실패를 막는다
            // (저메모리 빌드 머신을 쓰는 실사용자 보호 + 비-LTO 산출물로 빌드 완주).
            bool webGLCodeOptOomSkipped =
                applyWebGLCodeOpt
                && webGLCodeOptTarget == AITWebGLCodeOptimization.DiskSizeLTO
                && Application.unityVersion.StartsWith("6000.0.");
            if (webGLCodeOptOomSkipped)
            {
                Debug.LogWarning(
                    $"[AIT] WebGL Code Optimization({webGLCodeOptTarget}) 건너뜀 — Unity 6000.0 emscripten은 대형 프로젝트 LTO 링크에서 메모리 초과(OOM)로 빌드가 실패할 수 있어 이 버전에서만 비활성화 (6000.1+ 권장, 빌드는 계속)");
            }
            else if (applyWebGLCodeOpt)
            {
                // TrySetDiskSizeLTO: DiskSizeLTO 미지원 버전에서 DiskSize로 자동 폴백
                // (Sentry APPS-IN-TOSS-UNITY-SDK-10W: 멤버 미정의 시 건너뛰던 동작 개선)
                webGLCodeOptApplied = AITWebGLCodeOptimization.TrySetDiskSizeLTO();
                if (!webGLCodeOptApplied)
                {
                    Debug.LogWarning(
                        $"[AIT] WebGL Code Optimization({webGLCodeOptTarget}) 미적용 — 이 Unity 버전에서 API/멤버 부재 (빌드는 계속)");
                }
            }
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

            // ===== Unity 로고 표시 (사용자 지정 또는 자동, Pro 라이선스 필요) =====
            // -1(자동)은 AITDefaultSettings.GetDefaultShowUnityLogo()(=!HasPro())로 위임 — 다른 설정과 동일한
            // Resolve 패턴. 실제 PlayerSettings 적용은 ApplyShowUnityLogoSetting에서 Pro 라이선스를 재확인한다.
            bool showUnityLogo = editorConfig.showUnityLogo >= 0
                ? editorConfig.showUnityLogo == 1
                : AITDefaultSettings.GetDefaultShowUnityLogo();
            bool showUnityLogoApplied = ApplyShowUnityLogoSetting(showUnityLogo);

            // ===== 디버그 심볼 (빌드 프로필에서 설정 - ApplyBuildProfileSettings 참조) =====
            // 프로필 기반 설정은 DoExport()에서 ApplyBuildProfileSettings()를 통해 적용됨

            // ===== Decompression Fallback (사용자 지정 또는 자동) =====
            // 출처: StartupOptimization.md:93
            bool decompressionFallback = editorConfig.decompressionFallback >= 0
                ? editorConfig.decompressionFallback == 1
                : AITDefaultSettings.GetDefaultDecompressionFallback();
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;

            // ===== Mip Stripping (빌드 산출물 .data 축소, AITBuildSession이 빌드 후 원복) =====
            // mipStripping은 프로젝트 전역 PlayerSettings라 비활성화 선택 시에도 명시 적용해 결정성 보장.
            bool mipStripping = editorConfig.mipStripping >= 0
                ? editorConfig.mipStripping == 1
                : AITDefaultSettings.GetDefaultMipStripping();
            PlayerSettings.mipStripping = mipStripping;

            // Mip Stripping이 활성화된 경우, 런타임 Quality 변경 코드가 있는지 경고
            if (mipStripping)
            {
                WarnIfRuntimeQualityChangingCodeExists();
            }

            // ===== Optimize Mesh Data (빌드 산출물 .data 축소, AITBuildSession이 빌드 후 원복) =====
            // stripUnusedMeshComponents는 프로젝트 전역 PlayerSettings라 비활성화 선택 시에도 명시 적용해 결정성 보장.
            bool stripUnusedMeshComponents = editorConfig.stripUnusedMeshComponents >= 0
                ? editorConfig.stripUnusedMeshComponents == 1
                : AITDefaultSettings.GetDefaultStripUnusedMeshComponents();
            PlayerSettings.stripUnusedMeshComponents = stripUnusedMeshComponents;

            // stripUnusedMeshComponents가 활성화된 경우: 런타임 머티리얼 교체 코드 스캔
            // false positive 허용(경고일 뿐 동작 변경 없음) — 정규식 스캔은 의미론적 분석이 아니므로
            // 실제로 문제가 없는 코드도 검출될 수 있다. 그러나 경고를 보고 사용자가 직접 판단하도록 유도하는 것이 목적.
            if (stripUnusedMeshComponents)
            {
                ScanRuntimeMaterialReplacementCode();
            }

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
            string showUnityLogoLog = showUnityLogo ? "표시"
                : showUnityLogoApplied ? "숨김"
                : "숨김 요청됨 (Pro 라이선스 없음 — 미적용)";
            Debug.Log($"[AIT]   - Unity 로고: {showUnityLogoLog}{(editorConfig.showUnityLogo < 0 ? " (자동)" : "")}");
            string webGLCodeOptLog = !applyWebGLCodeOpt ? "미적용 (off)"
                : webGLCodeOptOomSkipped ? "미적용 (6000.0 LTO OOM 회피)"
                : webGLCodeOptApplied ? $"{webGLCodeOptTarget}{(editorConfig.webGLCodeOptimization < 0 ? " (자동)" : "")}"
                : "미적용 (API 부재)";
            Debug.Log($"[AIT]   - WebGL Code Optimization: {webGLCodeOptLog}");
#if UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - IL2CPP Code Generation: {il2cppCodeGen}{(editorConfig.il2cppCodeGeneration < 0 ? " (자동)" : "")}");
#endif
#if UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - WebAssembly 2023: {wasm2023}{(editorConfig.wasm2023 < 0 ? " (자동)" : "")}");
#endif
            Debug.Log($"[AIT]   - Mip Stripping: {mipStripping}{(editorConfig.mipStripping < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Optimize Mesh Data: {stripUnusedMeshComponents}{(editorConfig.stripUnusedMeshComponents < 0 ? " (자동)" : "")}");
#if UNITY_2023_3_OR_NEWER
            Debug.Log($"[AIT]   - Power Preference: {powerPreference}{(editorConfig.powerPreference < 0 ? " (자동)" : "")}");
#if !UNITY_6000_0_OR_NEWER
            Debug.Log($"[AIT]   - WASM Streaming: {editorConfig.wasmStreaming}");
#endif
#endif
            // first-interactive 계측은 BuildPlayer 후 WebGLBuildCopier에서 처리
            bool firstInteractiveEnabled = editorConfig.firstInteractiveLog >= 0
                ? editorConfig.firstInteractiveLog == 1
                : AITDefaultSettings.GetDefaultFirstInteractiveLog();
            Debug.Log($"[AIT]   - first-interactive 계측: {firstInteractiveEnabled}{(editorConfig.firstInteractiveLog < 0 ? " (자동)" : "")}");

            // 오디오 스트리밍은 BuildPlayer 직전 AITAudioStreamingProcessor 에서 처리
            bool audioStreamingEnabled = editorConfig.audioStreaming >= 0
                ? editorConfig.audioStreaming == 1
                : AITDefaultSettings.GetDefaultAudioStreaming();
            Debug.Log($"[AIT]   - 오디오 스트리밍: {audioStreamingEnabled}{(editorConfig.audioStreaming < 0 ? " (자동)" : "")}");

            // 오디오 재인코딩은 BuildPlayer 직전 AITAudioReencodeProcessor 에서 처리
            bool audioReencodeEnabled = editorConfig.audioReencode >= 0
                ? editorConfig.audioReencode == 1
                : AITDefaultSettings.GetDefaultAudioReencode();
            Debug.Log($"[AIT]   - 오디오 재인코딩: {audioReencodeEnabled}{(editorConfig.audioReencode < 0 ? " (자동)" : "")}{(audioReencodeEnabled ? $" (Vorbis q{Mathf.Clamp01(editorConfig.audioReencodeQuality):0.00})" : "")}");

            // 텍스처 crunch 는 BuildPlayer 직전 AITTextureCrunchProcessor 에서 처리
            bool textureCrunchEnabled = editorConfig.textureCrunch >= 0
                ? editorConfig.textureCrunch == 1
                : AITDefaultSettings.GetDefaultTextureCrunch();
            Debug.Log($"[AIT]   - 텍스처 crunch: {textureCrunchEnabled}{(editorConfig.textureCrunch < 0 ? " (자동)" : "")}");
            // 텍스처 크기 클램프는 BuildPlayer 직전 AITTextureSizeClampProcessor 에서 처리
            bool textureSizeClampEnabled = editorConfig.textureSizeClamp >= 0
                ? editorConfig.textureSizeClamp == 1
                : AITDefaultSettings.GetDefaultTextureSizeClamp();
            Debug.Log($"[AIT]   - 텍스처 크기 클램프: {textureSizeClampEnabled}{(editorConfig.textureSizeClamp < 0 ? " (자동)" : "")}{(textureSizeClampEnabled ? $" (≤{editorConfig.textureClampMaxSize})" : "")}");
            // ASTC 블록 에스컬레이션은 BuildPlayer 직전 AITAstcBlockProcessor 에서 처리
            bool astcBlockEnabled = editorConfig.astcBlockEscalation >= 0
                ? editorConfig.astcBlockEscalation == 1
                : AITDefaultSettings.GetDefaultAstcBlockEscalation();
            Debug.Log($"[AIT]   - ASTC 블록 에스컬레이션: {astcBlockEnabled}{(editorConfig.astcBlockEscalation < 0 ? " (자동)" : "")}");
            // 텍스처 스트리밍은 ExternalizeForBuild 에서 별도 리포트를 출력하므로 활성 여부만 기록.
            bool texStreamEnabled = editorConfig.textureStreaming == 1
                || (editorConfig.textureStreaming < 0 && AITDefaultSettings.GetDefaultTextureStreaming());
            Debug.Log($"[AIT]   - 텍스처 스트리밍: {(texStreamEnabled ? "활성화" : "비활성화")}{(editorConfig.textureStreaming < 0 ? " (자동)" : "")}");
        }

        /// <summary>
        /// Unity WebGL 빌드의 템플릿 전처리기(Preprocess.js)가 템플릿 폴더를 재귀 순회하다
        /// BuildConfig~ 하위의 잔여 빌드 산출물에 들어가 크래시하는 것을 막기 위해, 빌드 직전에
        /// 해당 산출물 폴더를 제거한다. (Sentry SDK-137)
        ///
        /// 배경: SDK 자신은 pnpm install을 ait-build/ 사본에서만 수행하므로
        /// Assets/WebGLTemplates/AITTemplate/BuildConfig~/node_modules를 만들지 않는다. 그러나
        /// 개발자가 BuildConfig~ 안에서 직접 install을 돌리면 node_modules가 남고, 그 안의 전이
        /// 의존성(예: @react-native/codegen의 C++ 코드젠 .js)에 들어 있는 bare #endif를 Unity 템플릿
        /// 전처리기가 "found #endif without matching #if"로 오인해 빌드가 중단된다. 폴더명 끝의 '~'는
        /// AssetDatabase import만 가리고 WebGL 템플릿 파일 스캔은 가리지 못하기 때문이다.
        ///
        /// 제거 대상 세 폴더는 이미 WebGLBuildCopier.CopyAdditionalUserFiles의 excludeFolders로
        /// 취급되는 비-소스 산출물이며 모두 gitignore + 재생성 가능하므로 빌드 직전 제거해도 안전하다.
        /// pnpm install은 ait-build/node_modules에서 별도로 일어나므로 재설치 비용도 없다.
        /// </summary>
        internal static void ScrubTemplatePreprocessorHazards()
        {
            ScrubTemplatePreprocessorHazards(System.IO.Path.Combine(
                Application.dataPath, "WebGLTemplates/AITTemplate/BuildConfig~"));
        }

        /// <summary>
        /// <see cref="ScrubTemplatePreprocessorHazards()"/>의 테스트 가능한 본체.
        /// Application.dataPath 의존 없이 임의의 BuildConfig 경로를 받아 정리한다.
        /// </summary>
        /// <param name="projectBuildConfigPath">프로젝트의 BuildConfig~ 절대 경로</param>
        internal static void ScrubTemplatePreprocessorHazards(string projectBuildConfigPath)
        {
            if (string.IsNullOrEmpty(projectBuildConfigPath))
                return;

            // 전처리기가 들어가면 안 되는 비-소스 산출물 (WebGLBuildCopier excludeFolders와 동일 집합)
            string[] hazardFolders = { "node_modules", ".npm-cache", "dist" };

            foreach (var folder in hazardFolders)
            {
                string target = System.IO.Path.Combine(projectBuildConfigPath, folder);
                if (!System.IO.Directory.Exists(target))
                    continue;

                bool removed = AITFileSystemHelper.SafeDeleteDirectory(target);
                if (removed)
                    Debug.Log($"[AIT] 템플릿 전처리 위험 폴더 제거: BuildConfig~/{folder} " +
                              "(Unity WebGL 전처리기 크래시 방지 — ait-build 재설치에는 영향 없음)");
                else
                    Debug.LogWarning($"[AIT] BuildConfig~/{folder} 제거 실패 — " +
                                     "Unity WebGL 빌드가 전처리 단계에서 실패할 수 있습니다. 수동 삭제를 권장합니다.");
            }
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
        /// Unity 로고(스플래시 스크린) 표시 설정을 적용한다.
        /// 호출 위치: <see cref="Init"/>. PlayerSettingsSnapshot.Capture()가 호출 전에 찍혀 있어야
        /// 빌드 후 Restore()로 사용자의 원래 스플래시 설정이 복원된다(AITConvertCore.DoExport 참조).
        ///
        /// showUnityLogoResolved == true(로고 표시)인 경우는 사용자의 기존 PlayerSettings를 그대로
        /// 두고 아무것도 쓰지 않는다 — Unity 기본 동작이 이미 로고를 표시하므로 명시 적용이 불필요하다.
        /// showUnityLogoResolved == false(로고 숨김)이 요청된 경우에만 Pro 라이선스를 확인한 뒤 적용한다:
        /// Unity Personal 라이선스는 스플래시 화면에 Unity 로고 표시를 강제하므로 HasPro()가 false면
        /// SplashScreen.show/showUnityLogo를 건드리지 않고 사유만 로그로 남긴다(시도해도 무의미).
        /// </summary>
        /// <returns>실제로 로고 숨김을 적용했으면 true.</returns>
        internal static bool ApplyShowUnityLogoSetting(bool showUnityLogoResolved)
        {
            return ApplyShowUnityLogoSetting(showUnityLogoResolved, UnityEditorInternal.InternalEditorUtility.HasPro());
        }

        /// <summary>
        /// <see cref="ApplyShowUnityLogoSetting(bool)"/>의 테스트 가능한 본체.
        /// HasPro()는 실행 환경의 실제 라이선스에 의존하므로, 테스트가 Pro/Personal 양쪽 분기를
        /// 검증할 수 있도록 hasProLicense를 파라미터로 분리했다.
        /// </summary>
        internal static bool ApplyShowUnityLogoSetting(bool showUnityLogoResolved, bool hasProLicense)
        {
            if (showUnityLogoResolved)
                return false;

            if (!hasProLicense)
            {
                Debug.Log("[AIT] Unity 로고 숨김이 설정되었지만 건너뜁니다 — Unity Personal 라이선스는 " +
                          "스플래시 화면에 Unity 로고 표시를 강제합니다(Unity Pro 라이선스 필요).");
                return false;
            }

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            return true;
        }

        /// <summary>
        /// 런타임에서 Quality 레벨을 변경하는 코드가 감지되면 경고를 출력한다.
        /// Mip Stripping은 빌드 시점의 현재 Quality 기준으로 밉맵을 제거하므로,
        /// 런타임에 더 낮은 Quality로 전환하면 렌더링 아티팩트가 발생할 수 있다.
        /// 스캔 실패(IO 예외 등)는 흡수하고 빌드를 계속한다.
        /// </summary>
        private static void WarnIfRuntimeQualityChangingCodeExists()
        {
            // Quality 레벨이 1개 이하이면 런타임 변경이 무의미하므로 스캔 불필요
            if (UnityEngine.QualitySettings.names.Length <= 1)
                return;

            try
            {
                string assetsPath = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "Assets");

                if (!System.IO.Directory.Exists(assetsPath))
                    return;

                // Assets/ 하위 .cs 파일을 검색하되 Editor/ 폴더는 제외
                var csFiles = System.IO.Directory.GetFiles(
                    assetsPath, "*.cs", System.IO.SearchOption.AllDirectories);

                var matchedFiles = new System.Collections.Generic.List<string>();

                foreach (string filePath in csFiles)
                {
                    // Editor 폴더 내 파일은 런타임 코드가 아니므로 제외
                    string normalizedPath = filePath.Replace('\\', '/');
                    if (normalizedPath.Contains("/Editor/"))
                        continue;

                    string content = System.IO.File.ReadAllText(filePath);
                    if (content.Contains("QualitySettings.SetQualityLevel") ||
                        content.Contains("QualitySettings.IncreaseLevel") ||
                        content.Contains("QualitySettings.DecreaseLevel"))
                    {
                        // 상대 경로로 변환해 로그를 간결하게 출력
                        string relativePath = filePath.Replace(
                            System.IO.Directory.GetCurrentDirectory() + System.IO.Path.DirectorySeparatorChar, "");
                        matchedFiles.Add(relativePath.Replace('\\', '/'));
                    }
                }

                if (matchedFiles.Count == 0)
                    return;

                // 최대 5개 파일 목록 구성
                int displayCount = System.Math.Min(matchedFiles.Count, 5);
                var fileList = new System.Text.StringBuilder();
                for (int i = 0; i < displayCount; i++)
                    fileList.AppendLine($"  - {matchedFiles[i]}");
                if (matchedFiles.Count > 5)
                    fileList.AppendLine($"  ... 외 {matchedFiles.Count - 5}개 파일");

                Debug.LogWarning(
                    $"[AIT] 런타임 Quality 변경 코드가 감지되었습니다({matchedFiles.Count}개 파일). " +
                    "Mip Stripping은 현재 Quality의 텍스처 품질 기준으로 밉을 제거하므로 런타임에 Quality를 낮추면 " +
                    "렌더링 아티팩트가 발생할 수 있습니다. 문제가 있으면 AIT Configuration에서 Mip Stripping을 비활성화하세요.\n" +
                    fileList.ToString().TrimEnd());
            }
            catch (System.Exception e)
            {
                // IO 예외 등 스캔 실패는 흡수하고 빌드 계속
                Debug.Log($"[AIT] Mip Stripping 런타임 Quality 변경 스캔 중 예외 발생 (무시됨): {e.Message}");
            }
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

        /// <summary>
        /// Assets/ 하위 런타임 C# 코드에서 머티리얼 교체 패턴을 정적으로 스캔하여 경고를 출력한다.
        /// Optimize Mesh Data(stripUnusedMeshComponents)가 활성화될 때만 호출된다.
        ///
        /// 목적: 빌드 시점 머티리얼 기준으로 미사용 정점 채널을 제거하므로, 교체된 머티리얼이
        /// 제거된 채널(노멀/탄젠트/UV 등)을 요구하면 시각 오류가 발생할 수 있다.
        /// 이 스캔은 사용자에게 확인을 유도할 뿐, 동작을 변경하지 않는다.
        ///
        /// false positive 허용: 정규식 스캔은 의미론적 분석이 아니므로 실제로 문제없는
        /// 코드(주석 내 패턴, 에디터 전용 코드 등)도 검출될 수 있다.
        /// 스캔 실패는 try/catch로 흡수하며 빌드에 영향을 주지 않는다.
        /// </summary>
        private static void ScanRuntimeMaterialReplacementCode()
        {
            try
            {
                string assetsPath = Path.Combine(Application.dataPath);
                // 머티리얼 런타임 대입 패턴: .material =, .materials =, .sharedMaterial =, .sharedMaterials =
                var pattern = new Regex(
                    @"\.(material|materials|sharedMaterial|sharedMaterials)\s*=",
                    RegexOptions.Compiled);

                var matchedFiles = new List<string>();
                int totalCount = 0;

                // Assets/ 하위 .cs 파일 탐색 (Editor/ 폴더 제외)
                string[] csFiles = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);
                foreach (string filePath in csFiles)
                {
                    // Editor 폴더 경로 제외 (경로 구분자 통일 후 검사)
                    string normalizedPath = filePath.Replace('\\', '/');
                    if (normalizedPath.Contains("/Editor/"))
                        continue;

                    string source = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    var matches = pattern.Matches(source);
                    if (matches.Count > 0)
                    {
                        totalCount += matches.Count;
                        // Assets/ 기준 상대 경로로 변환
                        string relativePath = "Assets" + normalizedPath.Substring(assetsPath.Replace('\\', '/').Length);
                        matchedFiles.Add(relativePath);
                    }
                }

                if (totalCount > 0)
                {
                    // 파일 목록은 최대 5개까지만 출력
                    var displayFiles = matchedFiles.Count > 5
                        ? matchedFiles.GetRange(0, 5)
                        : matchedFiles;
                    string fileList = string.Join("\n  - ", displayFiles);
                    string moreNote = matchedFiles.Count > 5 ? $"\n  … 외 {matchedFiles.Count - 5}개 파일" : "";

                    Debug.LogWarning(
                        $"[AIT] 런타임 머티리얼 교체 코드가 감지되었습니다({totalCount}건).\n" +
                        "Optimize Mesh Data는 빌드 시점 머티리얼 기준으로 미사용 정점 채널(노멀/탄젠트/UV)을 제거하므로, " +
                        "교체된 머티리얼이 제거된 채널을 요구하면 시각 오류가 발생할 수 있습니다.\n" +
                        "문제가 있으면 AIT Configuration에서 Optimize Mesh Data를 비활성화하세요.\n" +
                        $"감지된 파일:\n  - {fileList}{moreNote}");
                }
            }
            catch (System.Exception e)
            {
                // 스캔 실패는 흡수 — 빌드에 영향을 주지 않음
                Debug.Log($"[AIT] 런타임 머티리얼 교체 코드 스캔 중 오류 발생 (무시): {e.Message}");
            }
        }
    }
}
