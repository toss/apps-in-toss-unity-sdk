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
