using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    public partial class AITConfigurationWindow
    {
        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "고급 설정", true);

            if (!showAdvancedSettings) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("IL2CPP / Stripping 설정", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 엔진 코드 제거
            config.stripEngineCode = EditorGUILayout.Toggle("엔진 코드 제거", config.stripEngineCode);

            // IL2CPP 컴파일러 설정
            DrawIl2CppConfigurationSetting();

            // WebGL 코드 최적화 (Disk Size with LTO) — Meta 로드타임 스택의 실제 LTO 레버
            // (전 버전 reflection 적용이라 #if 가드 없음. API 부재 버전은 fail-safe)
            DrawWebGLCodeOptimizationSetting();

            // IL2CPP Code Generation (OptimizeSize) — Meta 로드타임 스택
            DrawIl2CppCodeGenerationSetting();

#if UNITY_2023_3_OR_NEWER
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Unity 6 전용 설정", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Power Preference
            DrawPowerPreferenceSetting();

#if UNITY_6000_0_OR_NEWER
            // WebAssembly 2023 — Meta 로드타임 스택 (미지원 브라우저 로드 실패 주의)
            DrawWasm2023Setting();
#endif

#if !UNITY_6000_0_OR_NEWER
            // WASM Streaming (Unity 6000에서 deprecated - decompressionFallback에 의해 자동 결정)
            config.wasmStreaming = EditorGUILayout.Toggle("WASM 스트리밍", config.wasmStreaming);

            // WASM 산술 예외 (Unity 6000에서 제거됨)
            DrawWasmArithmeticExceptionsSetting();
#endif
#endif

            GUILayout.Space(10);
            EditorGUILayout.LabelField("기타 고급 설정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "주의: 아래 설정을 변경하면 호환성 문제가 발생할 수 있습니다.",
                MessageType.Warning
            );
            GUILayout.Space(5);

            // 예외 처리 모드
            DrawExceptionSupportSetting();

            // Unity 로고 표시
            DrawShowUnityLogoSetting();

            // Decompression Fallback
            DrawDecompressionFallbackSetting();

            // Run In Background
            DrawRunInBackgroundSetting();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("콘텐츠 축소 (빌드 산출물 크기)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "빌드 산출물(.data)에서 사용되지 않는 데이터를 제거해 다운로드/로드 시간을 줄입니다. " +
                "프로젝트 설정은 빌드 후 원래대로 복원됩니다.",
                MessageType.Info
            );
            GUILayout.Space(5);

            // Mip Stripping
            DrawMipStrippingSetting();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("콘텐츠 축소 (빌드 산출물 크기)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "빌드 산출물(.data)에서 사용되지 않는 데이터를 제거해 다운로드/로드 시간을 줄입니다. " +
                "프로젝트 설정은 빌드 후 원래대로 복원됩니다.",
                MessageType.Info
            );
            GUILayout.Space(5);

            // Optimize Mesh Data
            DrawStripUnusedMeshComponentsSetting();

            GUILayout.Space(10);
            DrawUIToolkitModuleSetting();

            GUILayout.Space(10);
            DrawPhysicsBackendSetting();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("빌드 전 검사", EditorStyles.boldLabel);

            // 빌드 전 최적화 검사
            config.enableBuildOptimizationCheck = EditorGUILayout.Toggle(
                "빌드 전 에셋 최적화 검사",
                config.enableBuildOptimizationCheck);

            GUILayout.Space(10);

            // 폰트 스트리밍
            DrawFontStreamingSettings();

            GUILayout.Space(10);

            // 고급 설정 초기화
            if (GUILayout.Button("고급 설정 기본값으로 복원"))
            {
                ResetAdvancedSettings();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 모바일 런타임 최적화(구형 기기 메모리·전력) tri-state 레버 UI.
        /// WebGL 최적화 설정 패널(DrawWebGLOptimizationSettings)에서 호출되며, 변경 배지·"기본값 복원"도 그 패널의
        /// CountModifiedWebGLSettings / ResetWebGLOptimizationDefaults 가 담당한다(레버 목록을 양쪽과 맞출 것).
        /// 자동(-1)의 실효값은 AITDefaultSettings.GetDefault* 가 정한다.
        /// </summary>
        private void DrawMobileRuntimeSettings()
        {
            EditorGUILayout.LabelField("모바일 런타임 최적화 (구형 기기)", EditorStyles.boldLabel);

            // webglAntialiasOpt: 자동(기록만) / 0(훅 끔) / 1(AA 끔) 은 서로 다른 동작이라 명시값이면 모두 '변경됨'.
            config.webglAntialiasOpt = DrawPerfTriState(
                config.webglAntialiasOpt, "WebGL antialias 처리",
                "자동은 컨텍스트 요청/실제 속성을 기록하고 probe 컨텍스트만 해제합니다(antialias 는 끄지 않음). " +
                "활성화는 모바일에서 DPR 1.5 이상이거나 메모리 6GB 미만이면 antialias 를 끕니다. " +
                "⚠ QualitySettings.antiAliasing > 0 인 프로젝트에서는 Unity 가 자체 MSAA 렌더 타깃을 만들어 메모리가 오히려 늘 수 있어 실기기 검증 전까지 자동은 끄지 않습니다.",
                AITDefaultSettings.GetDefaultWebglAntialiasOpt(),
                autoText: "기록만", offText: "비활성화 (훅 끔)", onText: "활성화 (AA 끄기)",
                anyExplicitIsModified: true);

            config.webglContextRecovery = DrawPerfTriState(
                config.webglContextRecovery, "WebGL 컨텍스트 손실 복구",
                "webglcontextlost 가 오면 페이지를 다시 불러옵니다. 120초 안에 반복되면 reload 루프를 막고 안내 화면을 띄우며, " +
                "다음 부팅부터 렌더 해상도 상한(DPR)을 낮춥니다. 게임 상태는 사라지지만 영구 검은 화면보다 낫습니다.",
                AITDefaultSettings.GetDefaultWebglContextRecovery());

            config.frameRateCap = DrawPerfTriState(
                config.frameRateCap, "프레임레이트 상한 (60fps)",
                "100Hz 이상 디스플레이에서 60fps 로 제한해 전력을 줄입니다. 60Hz 기기에서는 효과가 없습니다. 120fps 를 의도한 게임은 비활성화하세요.",
                AITDefaultSettings.GetDefaultFrameRateCap());

            config.adaptiveFrameRate = DrawPerfTriState(
                config.adaptiveFrameRate, "적응형 프레임레이트 (30fps)",
                "배터리·발열 압력 신호가 오면 30fps 로 낮춥니다. 오탐 시 30fps 에 갇힐 수 있어 실기기 데이터 전까지 자동은 비활성입니다.",
                AITDefaultSettings.GetDefaultAdaptiveFrameRate());

            config.mobileLifecycle = DrawPerfTriState(
                config.mobileLifecycle, "라이프사이클 게이트 (hidden 시 정지)",
                "페이지가 hidden/pagehide/freeze 가 되면 메인 루프와 오디오를 멈췄다가 visible 이 되면 재개합니다. " +
                "숨겨진 동안 게임 타이머가 멈추므로 백그라운드 진행이 필요한 게임은 비활성화하세요.",
                AITDefaultSettings.GetDefaultMobileLifecycle());

            config.memoryTelemetry = DrawPerfTriState(
                config.memoryTelemetry, "메모리 텔레메트리",
                "WebAssembly.Memory.grow 와 OOM 을 기록하고 이전 세션 비정상 종료를 추적합니다. 게임 동작은 바꾸지 않습니다.",
                AITDefaultSettings.GetDefaultMemoryTelemetry());

            config.exactDataBody = DrawPerfTriState(
                config.exactDataBody, "data 정확한 크기 재포장",
                "빌드 때 .data 의 압축 해제 크기를 재서 로더가 버퍼를 한 번만 할당하게 합니다(로드 시점 메모리 피크 감소). " +
                "Decompression Fallback(.unityweb) 이면 적용되지 않습니다.",
                AITDefaultSettings.GetDefaultExactDataBody());

            config.releaseConsumedData = DrawPerfTriState(
                config.releaseConsumedData, "소비한 data 버퍼 해제",
                "한 번 읽고 다시 쓰지 않는 data 구간(global-metadata.dat 등)을 읽은 뒤 해제합니다. " +
                "Unity 버전별 동작 검증 전이라 자동은 비활성입니다(Chrome 111 / iOS 16.4 이상 전용).",
                AITDefaultSettings.GetDefaultReleaseConsumedData());

            config.audioForceCompressedPlayback = DrawPerfTriState(
                config.audioForceCompressedPlayback, "긴 오디오 강제 압축 재생 (framework 패치)",
                "외부화되지 않은 긴 클립도 PCM 으로 풀지 않고 압축 상태로 브라우저 미디어 요소로 재생하도록 framework 를 빌드 후 패치합니다. " +
                "3분 스테레오 BGM 하나가 약 63MB 를 차지하는 문제를 줄입니다. iOS 실기기 검증 전이라 자동은 비활성입니다.",
                AITDefaultSettings.GetDefaultAudioForceCompressedPlayback());

            config.lowMemoryTier = DrawPerfTriState(
                config.lowMemoryTier, "저사양 기기 티어 판별",
                "기기 메모리와 직전 세션 비정상 종료 이력으로 저사양 기기를 판별해 AITMemory.lowMemTier 를 켭니다. " +
                "판별과 진단만 켜며 게임 동작을 직접 바꾸지 않습니다. 후속 저메모리 최적화가 이 값을 읽습니다.",
                AITDefaultSettings.GetDefaultLowMemoryTier());

            config.pageCacheDeferredPut = DrawPerfTriState(
                config.pageCacheDeferredPut, "페이지 캐시 put 지연",
                "early-fetch 캐시 put 을 첫 프레임 이후로 미룹니다. 자동은 WebKit(iOS) 계열에서만 켭니다.",
                AITDefaultSettings.GetDefaultPageCacheDeferredPut(), autoText: "WebKit 만 활성", anyExplicitIsModified: true);

            config.audioStreamLoopTranscode = DrawPerfTriState(
                config.audioStreamLoopTranscode, "루프 오디오 재인코딩",
                "audioStreamTranscode 가 건너뛰던 루프 클립도 재인코딩합니다. 루프 이음새 청취 검증 전이라 자동은 비활성입니다.",
                AITDefaultSettings.GetDefaultAudioStreamLoopTranscode());

            bool forceCompressedEffective = config.audioForceCompressedPlayback >= 0
                ? config.audioForceCompressedPlayback == 1
                : AITDefaultSettings.GetDefaultAudioForceCompressedPlayback();
            if (forceCompressedEffective)
            {
                EditorGUI.indentLevel++;
                config.audioForceCompressedMinSeconds = EditorGUILayout.FloatField(
                    new GUIContent("최소 클립 길이(초)",
                        "이 길이 이상인 클립만 압축 재생으로 강제합니다(기본 10). 0 이하이면 10 으로 취급합니다."),
                    config.audioForceCompressedMinSeconds);
                if (config.audioForceCompressedMinSeconds <= 0f)
                {
                    config.audioForceCompressedMinSeconds = AITDefaultSettings.DefaultAudioForceCompressedMinSeconds;
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// tri-state(-1 자동 / 0 비활성 / 1 활성) 팝업 한 줄을 그리고 새 값을 돌려준다. 다른 레버 UI 와 같은 모양
        /// (변경 점·팝업·↺ 버튼)이다.
        /// </summary>
        /// <param name="anyExplicitIsModified">true 면 0/1 어느 쪽이든 명시값이면 '변경됨'으로 본다(자동·0·1 이 모두 다른 동작인 레버용).</param>
        private int DrawPerfTriState(
            int value,
            string label,
            string tooltip,
            bool defaultValue,
            string autoText = null,
            string offText = "비활성화",
            string onText = "활성화",
            bool anyExplicitIsModified = false)
        {
            bool isModified = value >= 0 && (anyExplicitIsModified || (value == 1) != defaultValue);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = autoText ?? (defaultValue ? onText : offText);
            string shownLabel = value < 0 ? $"{label} (자동: {autoLabel})" : label;

            string[] options = { $"자동 ({autoLabel})", offText, onText };
            int currentIndex = value < 0 ? 0 : value + 1;
            int newIndex = EditorGUILayout.Popup(new GUIContent(shownLabel, tooltip), currentIndex, options);
            int result = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                result = -1;
            }

            EditorGUILayout.EndHorizontal();
            return result;
        }

        private void DrawFontStreamingSettings()
        {
            EditorGUILayout.LabelField("폰트 스트리밍 (대형 폰트 deferral)", EditorStyles.boldLabel);

            // -1=자동, 0=비활성화, 1=수동 설정
            bool defaultEnabled = AITDefaultSettings.GetDefaultFontStreaming();
            string autoLabel = defaultEnabled ? "자동 (활성화)" : "자동 (비활성화)";
            bool isModified = config.fontStreaming >= 0;

            EditorGUILayout.BeginHorizontal();
            DrawModifiedIndicator(isModified);

            string[] fontStreamingOptions = { autoLabel, "비활성화", "수동 설정" };
            int currentIndex = config.fontStreaming < 0 ? 0 : config.fontStreaming + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("폰트 스트리밍",
                    "자동: 소스 1MB 이상이고 부팅 씬에 포함되지 않은 TMP_FontAsset 을 자동 스캔하여 외부화합니다.\n" +
                    "비활성화: 외부화를 수행하지 않습니다.\n" +
                    "수동 설정: fontStreamingTargetPaths 에 명시한 경로만 외부화합니다."),
                currentIndex,
                fontStreamingOptions);
            config.fontStreaming = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.fontStreaming = -1;
            }
            EditorGUILayout.EndHorizontal();

            // 수동 모드에서만 targetPaths 노출
            if (config.fontStreaming == 1)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("외부화 대상 TMP_FontAsset 경로 (쉼표 구분)", EditorStyles.boldLabel);
                config.fontStreamingTargetPaths = EditorGUILayout.TextArea(
                    config.fontStreamingTargetPaths,
                    GUILayout.Height(50));
                EditorGUILayout.HelpBox(
                    "Assets/ 기준의 .asset 경로를 쉼표로 구분하여 입력합니다.\n" +
                    "예) Assets/Fonts/NotoSansSC SDF.asset,Assets/Fonts/NotoSansJP SDF.asset",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // HelpBox: 자동/수동 공통 리스크 안내
            if (config.fontStreaming != 0)
            {
                EditorGUILayout.HelpBox(
                    "⚠ 재수화 전(또는 TMP_Settings 미설정 시) 대상 폰트의 글리프는 □ 로 렌더됩니다.\n" +
                    "부팅 씬에서 해당 글자를 사용하지 않는 경우 TTFF 를 크게 줄일 수 있습니다.",
                    MessageType.Warning);
            }
        }

        private void DrawIl2CppConfigurationSetting()
        {
            Il2CppCompilerConfiguration defaultConfig = AITDefaultSettings.GetDefaultIl2CppConfiguration();
            bool isModified = config.il2cppConfiguration >= 0 && (Il2CppCompilerConfiguration)config.il2cppConfiguration != defaultConfig;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.il2cppConfiguration < 0
                ? $"IL2CPP 컴파일러 (자동: {defaultConfig})"
                : "IL2CPP 컴파일러";

            string[] options = { $"자동 ({defaultConfig})", "Debug", "Release", "Master" };
            int currentIndex = config.il2cppConfiguration < 0 ? 0 : config.il2cppConfiguration + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.il2cppConfiguration = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.il2cppConfiguration = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWebGLCodeOptimizationSetting()
        {
            // 기본 동작 = 적용(DiskSizeLTO). 토글: -1=자동(적용) / 0=미적용 / 1=적용.
            // (config.webGLCodeOptimization==1)은 기본(적용)과 동일하므로 "미적용"(0)만 modified.
            bool isModified = config.webGLCodeOptimization == 0;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.webGLCodeOptimization < 0
                ? "WebGL 코드 최적화 (자동: Disk Size with LTO)"
                : "WebGL 코드 최적화";

            string[] options = { "자동 (Disk Size with LTO)", "미적용", "적용 (Disk Size with LTO)" };
            int currentIndex = config.webGLCodeOptimization < 0 ? 0 : config.webGLCodeOptimization + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.webGLCodeOptimization = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.webGLCodeOptimization = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 이 Unity 버전에서 codeOptimization API가 없으면 fail-safe로 무시됨을 안내
            if (config.webGLCodeOptimization != 0 && !AITWebGLCodeOptimization.IsSupported)
            {
                EditorGUILayout.HelpBox(
                    "이 Unity 버전에는 WebGL code optimization API가 없어 이 설정은 빌드 시 무시됩니다 " +
                    "(빌드는 정상 진행, LTO 이득만 없음).",
                    MessageType.Info
                );
            }
        }

        private void DrawIl2CppCodeGenerationSetting()
        {
            UnityEditor.Build.Il2CppCodeGeneration defaultCodeGen = AITDefaultSettings.GetDefaultIl2CppCodeGeneration();
            bool isModified = config.il2cppCodeGeneration >= 0 && (UnityEditor.Build.Il2CppCodeGeneration)config.il2cppCodeGeneration != defaultCodeGen;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.il2cppCodeGeneration < 0
                ? $"IL2CPP 코드 생성 (자동: {defaultCodeGen})"
                : "IL2CPP 코드 생성";

            string[] options = { $"자동 ({defaultCodeGen})", "OptimizeSpeed", "OptimizeSize" };
            int currentIndex = config.il2cppCodeGeneration < 0 ? 0 : config.il2cppCodeGeneration + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.il2cppCodeGeneration = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.il2cppCodeGeneration = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

#if UNITY_6000_0_OR_NEWER
        private void DrawWasm2023Setting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultWasm2023();
            bool isModified = config.wasm2023 >= 0 && (config.wasm2023 == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.wasm2023 < 0
                ? $"WebAssembly 2023 (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "WebAssembly 2023";

            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.wasm2023 < 0 ? 0 : config.wasm2023 + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.wasm2023 = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.wasm2023 = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 미지원 브라우저 하드 페일 경고 (graceful degradation 아님)
            if (config.wasm2023 != 0)
            {
                EditorGUILayout.HelpBox(
                    "WebAssembly 2023을 켜면 미지원 브라우저(대략 Chrome<91 / Safari<16.4)에서 " +
                    "로드가 실패합니다. Apps in Toss WebView 최소 사양 충족 시에만 사용하세요.",
                    MessageType.Warning
                );
            }
        }
#endif

#if UNITY_2023_3_OR_NEWER
        private void DrawPowerPreferenceSetting()
        {
            WebGLPowerPreference defaultPower = AITDefaultSettings.GetDefaultPowerPreference();
            bool isModified = config.powerPreference >= 0 && AITBuildInitializer.ConvertToPowerPreference(config.powerPreference) != defaultPower;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.powerPreference < 0
                ? $"Power Preference (자동: {defaultPower})"
                : "Power Preference";

            string[] options = { $"자동 ({defaultPower})", "Default", "HighPerformance", "LowPower" };
            int currentIndex = config.powerPreference < 0 ? 0 : config.powerPreference + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.powerPreference = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.powerPreference = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

#if !UNITY_6000_0_OR_NEWER
        private void DrawWasmArithmeticExceptionsSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultWebAssemblyArithmeticExceptions();
            bool isModified = config.webAssemblyArithmeticExceptions >= 0 && (config.webAssemblyArithmeticExceptions == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.webAssemblyArithmeticExceptions < 0
                ? $"WASM 산술 예외 (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "WASM 산술 예외";

            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.webAssemblyArithmeticExceptions < 0 ? 0 : config.webAssemblyArithmeticExceptions + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.webAssemblyArithmeticExceptions = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.webAssemblyArithmeticExceptions = -1;
            }

            EditorGUILayout.EndHorizontal();
        }
#endif
#endif

        private void DrawExceptionSupportSetting()
        {
            WebGLExceptionSupport defaultValue = AITDefaultSettings.GetDefaultExceptionSupport();
            bool isModified = config.exceptionSupport >= 0 && AITBuildInitializer.ConvertToExceptionSupport(config.exceptionSupport) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.exceptionSupport < 0
                ? $"예외 처리 모드 (자동: {defaultValue}, Dev 빌드는 FullWithStacktrace)"
                : "예외 처리 모드";

            string[] options = { $"자동 ({defaultValue})", "None", "ExplicitlyThrownOnly", "FullWithStacktrace", "FullWithoutStacktrace" };
            int currentIndex = config.exceptionSupport < 0 ? 0 : config.exceptionSupport + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.exceptionSupport = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.exceptionSupport = -1;
            }

            EditorGUILayout.EndHorizontal();

            // "None"은 iOS WebView에서 "RuntimeError: Unreachable code" 크래시가 실측 확인된 값이라
            // 선택 자체는 막지 않되(명시적 override 존중) 경고로 위험을 알린다.
            if (config.exceptionSupport >= 0 && (WebGLExceptionSupport)config.exceptionSupport == WebGLExceptionSupport.None)
            {
                EditorGUILayout.HelpBox(
                    "예외 처리 모드 'None'은 iOS WebView에서 'RuntimeError: Unreachable code' 크래시가 실측 확인되었습니다.\n" +
                    "사용을 권장하지 않습니다 — 최소 'ExplicitlyThrownOnly' 이상을 선택하세요.",
                    MessageType.Warning
                );
            }
        }

        private void DrawShowUnityLogoSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultShowUnityLogo();
            bool isModified = config.showUnityLogo >= 0 && (config.showUnityLogo == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoText = defaultValue ? "표시" : "숨김";
            string label = config.showUnityLogo < 0
                ? $"Unity 로고 (자동: {autoText})"
                : "Unity 로고";

            string[] options = { $"자동 ({autoText})", "숨김 (Pro 필요)", "표시" };
            int currentIndex = config.showUnityLogo < 0 ? 0 : config.showUnityLogo + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.showUnityLogo = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.showUnityLogo = -1;
            }

            EditorGUILayout.EndHorizontal();

            // Unity Pro 라이선스 경고
            if (config.showUnityLogo == 0 && !UnityEditorInternal.InternalEditorUtility.HasPro())
            {
                EditorGUILayout.HelpBox(
                    "Unity Pro 라이선스가 없으면 로고를 숨길 수 없습니다.",
                    MessageType.Warning
                );
            }
        }

        private void DrawDecompressionFallbackSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultDecompressionFallback();
            bool isModified = config.decompressionFallback >= 0 && (config.decompressionFallback == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.decompressionFallback < 0
                ? $"Decompression Fallback (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "Decompression Fallback";

            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.decompressionFallback < 0 ? 0 : config.decompressionFallback + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.decompressionFallback = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.decompressionFallback = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 끄면(기본값) JS 디컴프레서가 번들에서 제거되어 호스팅 CDN이 Content-Encoding: br를
            // 직접 서빙해야 한다. AIT 플랫폼 CDN은 보장하지만, 자체 호스팅 시 미설정이면 로드 실패.
            if (config.decompressionFallback != 1)
            {
                EditorGUILayout.HelpBox(
                    "Decompression Fallback이 꺼지면 호스팅 서버가 Content-Encoding: br(또는 gzip)을 " +
                    "직접 서빙해야 합니다. Apps in Toss 플랫폼 CDN은 보장하지만, 자체 호스팅 시 " +
                    "압축 헤더 미설정이면 로드가 실패합니다.",
                    MessageType.Warning
                );
            }
        }

        private void DrawRunInBackgroundSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultRunInBackground();
            bool isModified = config.runInBackground >= 0 && (config.runInBackground == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.runInBackground < 0
                ? $"Run In Background (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "Run In Background";

            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.runInBackground < 0 ? 0 : config.runInBackground + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.runInBackground = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.runInBackground = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMipStrippingSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultMipStripping();
            bool isModified = config.mipStripping >= 0 && (config.mipStripping == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.mipStripping < 0
                ? $"Mip Stripping (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "Mip Stripping";

            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.mipStripping < 0 ? 0 : config.mipStripping + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.mipStripping = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.mipStripping = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStripUnusedMeshComponentsSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultStripUnusedMeshComponents();
            bool isModified = config.stripUnusedMeshComponents >= 0 && (config.stripUnusedMeshComponents == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.stripUnusedMeshComponents < 0
                ? $"Optimize Mesh Data (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "Optimize Mesh Data";

            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.stripUnusedMeshComponents < 0 ? 0 : config.stripUnusedMeshComponents + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.stripUnusedMeshComponents = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.stripUnusedMeshComponents = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        // 검사는 비용이 있어 섹션이 처음 그려질 때와 "다시 검사" 클릭 때만 돌리고 결과를 캐시한다.
        private AITUIToolkitModuleAdvisor.Result uiToolkitResult;

        private void DrawUIToolkitModuleSetting()
        {
            EditorGUILayout.LabelField(AITUIToolkitModuleAdvisor.SectionName + " (빌드 크기/첫 화면)", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (uiToolkitResult == null)
                uiToolkitResult = AITUIToolkitModuleAdvisor.Analyze();
            var r = uiToolkitResult;

            EditorGUILayout.LabelField("상태", r.ModuleEnabled ? "켜짐 (com.unity.modules.uielements)" : "꺼짐");

            if (r.Failed)
            {
                EditorGUILayout.HelpBox("검사에 실패했습니다. 콘솔 로그를 확인하세요.", MessageType.Warning);
            }
            else if (r.Recommended)
            {
                EditorGUILayout.HelpBox(
                    "런타임에서 UI Toolkit을 쓰는 흔적이 없습니다. 모듈을 끄면 빈 앱 기준 첫 화면이 약 0.15~0.55초 빨라지고 wasm이 약 0.5~1MB 줄었습니다.",
                    MessageType.Info);
            }
            else if (r.ModuleEnabled)
            {
                EditorGUILayout.HelpBox("끄기를 권장하지 않습니다. 아래 사유를 확인하세요.", MessageType.None);
            }

            if (r.DependentPackages.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "이 모듈에 의존하는 패키지가 있어 manifest에서 지워도 실제로는 제거되지 않습니다: " +
                    string.Join(", ", r.DependentPackages),
                    MessageType.Warning);
            }
            foreach (var usage in r.RuntimeUsages)
                EditorGUILayout.HelpBox(usage, MessageType.Warning);

            bool uiAuto = EditorGUILayout.Toggle(
                new GUIContent("에디터 로드 시 자동 제거", "켜 두면 사용 흔적이 없을 때 에디터를 열 때 모듈을 자동으로 제거합니다(기본 켜짐). 끄면 값이 0이 됩니다."),
                config.uiToolkitAutoRemove != 0);
            if (uiAuto != (config.uiToolkitAutoRemove != 0))
                config.uiToolkitAutoRemove = uiAuto ? -1 : 0;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("다시 검사"))
                uiToolkitResult = AITUIToolkitModuleAdvisor.Analyze();
            if (r.ModuleEnabled)
            {
                if (GUILayout.Button("모듈 끄기"))
                    AITUIToolkitModuleAdvisor.Disable();
            }
            else if (GUILayout.Button("모듈 켜기"))
            {
                AITUIToolkitModuleAdvisor.Enable();
            }
            EditorGUILayout.EndHorizontal();
        }

        private AITPhysicsBackendAdvisor.Result physicsBackendResult;

        private void DrawPhysicsBackendSetting()
        {
            EditorGUILayout.LabelField(AITPhysicsBackendAdvisor.SectionName + " (빌드 크기)", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (physicsBackendResult == null)
                physicsBackendResult = AITPhysicsBackendAdvisor.Analyze();
            var r = physicsBackendResult;

            if (!r.Applicable)
            {
                EditorGUILayout.HelpBox("Unity 6.3 이상에서만 쓰는 설정입니다. 이 버전에서는 해당하지 않습니다.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("상태", r.PhysXEnabled ? "켜짐 (PhysX)" : "꺼짐 (none)");

            if (r.Failed)
            {
                EditorGUILayout.HelpBox("검사에 실패했습니다. 콘솔 로그를 확인하세요.", MessageType.Warning);
            }
            else if (r.Recommended)
            {
                EditorGUILayout.HelpBox(
                    "런타임에서 3D 물리를 쓰는 흔적이 없습니다. Unity 6.3 이상의 물리 백엔드(PhysX)를 끄면 wasm이 약 0.8MB(brotli) 줄어듭니다. " +
                    "설정 파일을 직접 고치므로 에디터를 다시 시작해야 적용됩니다.",
                    MessageType.Info);
            }
            else if (r.PhysXEnabled)
            {
                EditorGUILayout.HelpBox("끄기를 권장하지 않습니다. 아래 사유를 확인하세요.", MessageType.None);
            }

            foreach (var usage in r.RuntimeUsages)
                EditorGUILayout.HelpBox(usage, MessageType.Warning);

            bool physAuto = EditorGUILayout.Toggle(
                new GUIContent("에디터 로드 시 자동 끄기", "켜 두면 3D 물리 사용 흔적이 없을 때 에디터를 열 때 PhysX를 자동으로 끕니다(기본 켜짐, 재시작 후 반영). 끄면 값이 0이 됩니다."),
                config.physicsBackendAutoDisable != 0);
            if (physAuto != (config.physicsBackendAutoDisable != 0))
                config.physicsBackendAutoDisable = physAuto ? -1 : 0;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("다시 검사"))
                physicsBackendResult = AITPhysicsBackendAdvisor.Analyze();
            if (r.PhysXEnabled)
            {
                if (GUILayout.Button("PhysX 끄기"))
                {
                    AITPhysicsBackendAdvisor.Disable();
                    physicsBackendResult = AITPhysicsBackendAdvisor.Analyze();
                }
            }
            else if (GUILayout.Button("PhysX 켜기"))
            {
                AITPhysicsBackendAdvisor.Enable();
                physicsBackendResult = AITPhysicsBackendAdvisor.Analyze();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ResetAdvancedSettings()
        {
            config.stripEngineCode = true;
            config.il2cppConfiguration = -1;
            config.webGLCodeOptimization = -1;
            config.powerPreference = -1;
#if UNITY_6000_0_OR_NEWER
            config.il2cppCodeGeneration = -1;
#endif
#if UNITY_6000_0_OR_NEWER
            config.wasm2023 = -1;
#endif
#if !UNITY_6000_0_OR_NEWER
            config.wasmStreaming = true;
            config.webAssemblyArithmeticExceptions = -1;
#endif
            config.exceptionSupport = -1;
            config.showUnityLogo = -1;
            config.decompressionFallback = -1;
            config.runInBackground = -1;
            config.mipStripping = -1;
            config.stripUnusedMeshComponents = -1;
            config.enableBuildOptimizationCheck = true;
            // 폰트 스트리밍(fontStreaming + targetPaths/maxConcurrent)은 형제 레버 textureStreaming 과 동일하게
            // ResetWebGLOptimizationDefaults("모든 WebGL 설정 기본값으로 복원")가 master+서브필드를 일괄 복원한다.
            // 여기서 master 만 부분 복원하면 targetPaths/maxConcurrent 가 stale 로 남는 split-reset 위험이 있어 제외한다
            // (폰트 스트리밍 master 단독 복원은 해당 UI 의 인라인 리셋 버튼이 제공).
        }

    }
}
