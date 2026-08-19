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
        /// <param name="fastBuild">
        /// 빠른 반복 빌드(Dev Server·Deploy (Test)) 경로에서의 빌드인지 여부.
        /// IL2CPP 컴파일러 구성 결정(<see cref="ResolveIl2CppConfiguration"/>)과
        /// IL2CPP Code Generation 결정(<see cref="ResolveIl2CppCodeGeneration"/>)에 영향을 주며,
        /// 기본값 false는 Production/Deploy (Production)/Build & Package 등 기존 호출부의 동작을 보존한다.
        /// </param>
        internal static void Init(AITBuildProfile profile = null, bool fastBuild = false)
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
            WebGLExceptionSupport exceptionSupport = ConvertToExceptionSupport(editorConfig.exceptionSupport);

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
            ManagedStrippingLevel strippingLevel = profile != null
                ? ConvertToManagedStrippingLevel(profile.managedStrippingLevel)
                : AITDefaultSettings.GetDefaultManagedStrippingLevel();
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, strippingLevel);
#else
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, strippingLevel);
#endif

            Il2CppCompilerConfiguration il2cppConfig = ResolveIl2CppConfiguration(editorConfig.il2cppConfiguration, fastBuild);

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

            // ===== IL2CPP Code Generation (빠른 반복 빌드 레버) =====
            // OptimizeSize("Faster (smaller) builds")는 제네릭 공유로 IL2CPP 변환량을 대폭 줄여
            // 빌드 속도를 개선하지만 런타임 성능은 OptimizeSpeed(Unity 기본) 대비 저하될 수 있다.
            // fastBuild가 아니면 null을 돌려받아 프로젝트에 이미 설정된 Code Generation 값을
            // 그대로 유지한다 — Player Settings에서 명시적으로 OptimizeSize를 선택한 사용자의
            // 설정을 Production/Deploy (Production)/Build & Package에서 조용히 덮어쓰지 않기 위함.
            UnityEditor.Build.Il2CppCodeGeneration? il2cppCodeGeneration = ResolveIl2CppCodeGeneration(fastBuild);

            string il2cppCodeGenEnv = System.Environment.GetEnvironmentVariable("AIT_IL2CPP_CODE_GENERATION");
            if (!string.IsNullOrEmpty(il2cppCodeGenEnv))
            {
                if (System.Enum.TryParse<UnityEditor.Build.Il2CppCodeGeneration>(il2cppCodeGenEnv, ignoreCase: true, out var parsedCodeGen))
                {
                    il2cppCodeGeneration = parsedCodeGen;
                    Debug.Log($"[AIT] 환경 변수 오버라이드: AIT_IL2CPP_CODE_GENERATION={parsedCodeGen}");
                }
                else
                {
                    Debug.LogWarning($"[AIT] AIT_IL2CPP_CODE_GENERATION 환경 변수 값이 올바르지 않습니다: '{il2cppCodeGenEnv}' (OptimizeSpeed/OptimizeSize 필요)");
                }
            }

            // 값이 있을 때만(빠른 빌드 또는 env 오버라이드) PlayerSettings/EditorUserBuildSettings에 적용.
            // 값이 없으면(Production 등) 프로젝트에 이미 설정된 값을 그대로 둔다.
            if (il2cppCodeGeneration.HasValue)
            {
                // PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget, ...)은 Unity 2022.2+ API.
                // 최소 지원 버전(2021.3)에서는 프로젝트 전역 설정인 EditorUserBuildSettings.il2CppCodeGeneration(2021.2+)을 사용.
#if UNITY_2022_2_OR_NEWER
                PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, il2cppCodeGeneration.Value);
#else
                EditorUserBuildSettings.il2CppCodeGeneration = il2cppCodeGeneration.Value;
#endif

                if (il2cppCodeGeneration.Value == UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize)
                {
                    Debug.Log("[AIT] IL2CPP Code Generation: OptimizeSize (Faster (smaller) builds) 적용 — " +
                              "제네릭 공유로 IL2CPP 변환량이 줄어 빌드 속도가 개선되지만 런타임 성능은 저하될 수 있습니다.");
                }
            }

            // ===== Unity 6 (2023.3+) 전용 설정 =====
#if UNITY_2023_3_OR_NEWER
            // 출처: UnityVersion.md:394-402
            WebGLPowerPreference powerPreference = ConvertToPowerPreference(editorConfig.powerPreference);
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
            Debug.Log($"[AIT]   - IL2CPP Code Generation: {DescribeIl2CppCodeGeneration(il2cppCodeGeneration, il2cppCodeGenEnv)}");
            Debug.Log($"[AIT]   - Run In Background: {runInBackground}{(editorConfig.runInBackground < 0 ? " (자동)" : "")}");
            Debug.Log($"[AIT]   - Decompression Fallback: {decompressionFallback}{(editorConfig.decompressionFallback < 0 ? " (자동)" : "")}");
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
            // PlayerPrefs 영속화는 BuildPlayer 후 WebGLBuildCopier에서 처리
            bool playerPrefsPersistenceEnabled = editorConfig.playerPrefsPersistence >= 0
                ? editorConfig.playerPrefsPersistence == 1
                : AITDefaultSettings.GetDefaultPlayerPrefsPersistence();
            Debug.Log($"[AIT]   - PlayerPrefs 영속화: {playerPrefsPersistenceEnabled}{(editorConfig.playerPrefsPersistence < 0 ? " (자동)" : " (명시)")}");
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
        /// IL2CPP 컴파일러 구성 결정 (env 오버라이드 제외 — 그건 Init에서 이 함수 호출 이후 별도 적용).
        /// 우선순위:
        /// 1. editorConfig.il2cppConfiguration 명시값(-1 아님) — 사용자의 명시 선택 존중
        /// 2. 빠른 빌드(Dev Server·Deploy (Test))면 Debug — 반복 루프 빌드 속도 우선 (런타임 성능은 Release 대비 저하)
        /// 3. 그 외 기본값(Release) — Production/Deploy (Production)/Build & Package 동작 불변
        /// </summary>
        internal static Il2CppCompilerConfiguration ResolveIl2CppConfiguration(int editorConfigValue, bool fastBuild)
        {
            if (editorConfigValue >= 0)
                return (Il2CppCompilerConfiguration)editorConfigValue;

            if (fastBuild)
            {
                Debug.Log("[AIT] 빠른 빌드: IL2CPP 컴파일러 구성을 Debug로 설정합니다. " +
                          "빌드 속도가 개선되지만 런타임 성능은 Release 대비 저하될 수 있습니다.");
                return Il2CppCompilerConfiguration.Debug;
            }

            return AITDefaultSettings.GetDefaultIl2CppConfiguration();
        }

        /// <summary>
        /// IL2CPP Code Generation(Faster runtime / Faster (smaller) builds) 결정 (env 오버라이드 제외
        /// — 그건 Init에서 이 함수 호출 이후 별도 적용).
        /// fastBuild면 OptimizeSize("Faster (smaller) builds") — 제네릭 공유로 IL2CPP 변환량이 대폭
        /// 줄어 빌드 속도가 개선되지만 런타임 성능은 저하될 수 있다.
        /// fastBuild가 아니면 null을 돌려준다 — il2cppConfiguration(-1=자동)과 달리 Code Generation은
        /// 이 SDK가 관리하는 사용자 설정 경로가 없으므로, 강제로 값을 적용하는 대신 Player Settings에
        /// 이미 설정된 프로젝트 값을 그대로 유지한다(Production/Deploy (Production)/Build & Package 동작 불변).
        /// </summary>
        internal static UnityEditor.Build.Il2CppCodeGeneration? ResolveIl2CppCodeGeneration(bool fastBuild)
        {
            return fastBuild
                ? UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize
                : (UnityEditor.Build.Il2CppCodeGeneration?)null;
        }

        /// <summary>
        /// 요약 로그용: IL2CPP Code Generation 값의 출처를 함께 표기한다.
        /// 값이 없으면(=미적용) 프로젝트에 현재 설정된 값을 조회해 "(유지)"로 표기한다.
        /// </summary>
        private static string DescribeIl2CppCodeGeneration(UnityEditor.Build.Il2CppCodeGeneration? resolved, string envOverride)
        {
            if (resolved.HasValue)
            {
                string suffix = !string.IsNullOrEmpty(envOverride) ? " (환경 변수)" : " (빠른 빌드)";
                return $"{resolved.Value}{suffix}";
            }

#if UNITY_2022_2_OR_NEWER
            var current = PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL);
#else
            var current = EditorUserBuildSettings.il2CppCodeGeneration;
#endif
            return $"{current} (유지 · 프로젝트 설정)";
        }

        /// <summary>
        /// 프로필 저장값을 WebGLCompressionFormat enum으로 변환
        /// 저장값: -1=자동, 0=Disabled, 1=Gzip, 2=Brotli
        /// enum값: 0=Brotli, 1=Gzip, 2=Disabled
        /// </summary>
        internal static WebGLCompressionFormat ConvertToCompressionFormat(int storedValue)
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
        /// 프로필 저장값을 ManagedStrippingLevel enum으로 변환
        /// 저장값(UI 순서): -1=자동, 0=Disabled(레거시), 1=Minimal, 2=Low, 3=Medium, 4=High
        /// enum값: Disabled=0, Low=1, Medium=2, High=3, Minimal=4 — Minimal이 나중에 추가되어 순서가 달라 직접 캐스팅 금지
        /// WebGL(IL2CPP)은 Disabled를 지원하지 않으므로 레거시 저장값 0은 Minimal로 폴백
        /// </summary>
        internal static ManagedStrippingLevel ConvertToManagedStrippingLevel(int storedValue)
        {
            return storedValue switch
            {
                0 => ManagedStrippingLevel.Minimal,
                1 => ManagedStrippingLevel.Minimal,
                2 => ManagedStrippingLevel.Low,
                3 => ManagedStrippingLevel.Medium,
                4 => ManagedStrippingLevel.High,
                _ => AITDefaultSettings.GetDefaultManagedStrippingLevel()
            };
        }

        /// <summary>
        /// 설정 저장값을 WebGLExceptionSupport enum으로 변환
        /// 저장값(UI 순서): -1=자동, 0=None, 1=ExplicitlyThrownOnly, 2=FullWithStacktrace, 3=FullWithoutStacktrace
        /// enum값: None=0, ExplicitlyThrownExceptionsOnly=1, FullWithoutStacktrace=2, FullWithStacktrace=3 — 2·3 순서가 반대라 직접 캐스팅 금지
        /// </summary>
        internal static WebGLExceptionSupport ConvertToExceptionSupport(int storedValue)
        {
            return storedValue switch
            {
                0 => WebGLExceptionSupport.None,
                1 => WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                2 => WebGLExceptionSupport.FullWithStacktrace,
                3 => WebGLExceptionSupport.FullWithoutStacktrace,
                _ => AITDefaultSettings.GetDefaultExceptionSupport()
            };
        }

#if UNITY_2023_3_OR_NEWER
        /// <summary>
        /// 설정 저장값을 WebGLPowerPreference enum으로 변환
        /// 저장값(UI 순서): -1=자동, 0=Default, 1=HighPerformance, 2=LowPower
        /// enum값: Default=0, LowPower=1, HighPerformance=2 — 1·2 순서가 반대라 직접 캐스팅 금지
        /// </summary>
        internal static WebGLPowerPreference ConvertToPowerPreference(int storedValue)
        {
            return storedValue switch
            {
                0 => WebGLPowerPreference.Default,
                1 => WebGLPowerPreference.HighPerformance,
                2 => WebGLPowerPreference.LowPower,
                _ => AITDefaultSettings.GetDefaultPowerPreference()
            };
        }
#endif

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

            // Stripping Level 문자열 생성 (실제 적용될 enum 기준)
            string strippingStr = profile.managedStrippingLevel < 0
                ? "자동 (High)"
                : ConvertToManagedStrippingLevel(profile.managedStrippingLevel).ToString();

            Debug.Log("[AIT] ========================================");
            Debug.Log($"[AIT] 빌드 프로필: {profileName}");
            Debug.Log("[AIT] ========================================");
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
