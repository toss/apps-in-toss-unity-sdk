using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Apps in Toss 설정 윈도우
    /// </summary>
    public class AITConfigurationWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private AITEditorScriptObject config;

        // UI 접힘 상태
        private bool showWebGLSettings = true;
        private bool showAdvancedSettings = false;

        // 하이라이트 색상
        private static readonly Color ModifiedColor = new Color(1f, 0.6f, 0f); // 주황색

        public static void ShowWindow()
        {
            var window = GetWindow<AITConfigurationWindow>("AIT Configuration");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }

        private void OnEnable()
        {
            config = UnityUtil.GetEditorConf();
        }

        private void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("설정을 불러올 수 없습니다.", MessageType.Error);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);
            DrawHeader();
            GUILayout.Space(10);
            DrawAppInfo();
            GUILayout.Space(10);
            DrawBrandSettings();
            GUILayout.Space(10);
            DrawDevServerSettings();
            GUILayout.Space(10);
            DrawBuildSettings();
            GUILayout.Space(10);
            DrawWebGLOptimizationSettings();
            GUILayout.Space(10);
            DrawAdvancedSettings();
            GUILayout.Space(10);
            DrawAdvertisementSettings();
            GUILayout.Space(10);
            DrawDeploymentSettings();
            GUILayout.Space(10);
            DrawProjectInfo();

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                SaveSettings();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("Apps in Toss Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "앱 빌드 및 배포에 필요한 설정을 관리합니다.",
                MessageType.Info
            );
        }

        private void DrawAppInfo()
        {
            EditorGUILayout.LabelField("앱 기본 정보", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // 앱 ID (검증 포함)
            config.appName = EditorGUILayout.TextField("앱 ID", config.appName);
            if (!string.IsNullOrWhiteSpace(config.appName) && !config.IsAppNameValid())
            {
                EditorGUILayout.HelpBox("앱 ID는 영문, 숫자, 하이픈(-)만 사용할 수 있습니다.", MessageType.Warning);
            }

            config.displayName = EditorGUILayout.TextField("표시 이름", config.displayName);

            // 버전 (검증 포함)
            config.version = EditorGUILayout.TextField("버전", config.version);
            if (!string.IsNullOrWhiteSpace(config.version) && !config.IsVersionValid())
            {
                EditorGUILayout.HelpBox("버전은 x.y.z 형식이어야 합니다. (예: 1.0.0)", MessageType.Warning);
            }

            config.description = EditorGUILayout.TextArea(config.description, GUILayout.Height(60));

            EditorGUILayout.EndVertical();
        }

        private void DrawBrandSettings()
        {
            EditorGUILayout.LabelField("브랜드 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.primaryColor = EditorGUILayout.TextField("기본 색상", config.primaryColor);
            config.iconUrl = EditorGUILayout.TextField("아이콘 URL (필수)", config.iconUrl);

            // 아이콘 URL 검증
            if (string.IsNullOrWhiteSpace(config.iconUrl))
            {
                EditorGUILayout.HelpBox(
                    "아이콘 URL을 입력해주세요. 빌드 시 필수입니다.\n예: https://your-domain.com/icon.png",
                    MessageType.Warning
                );
            }
            else if (!config.IsIconUrlValid())
            {
                EditorGUILayout.HelpBox(
                    "아이콘 URL은 http:// 또는 https://로 시작해야 합니다.",
                    MessageType.Error
                );
            }
            else
            {
                EditorGUILayout.HelpBox("아이콘 URL이 올바른 형식입니다.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDevServerSettings()
        {
            EditorGUILayout.LabelField("개발 서버 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.localPort = EditorGUILayout.IntField("로컬 포트", config.localPort);

            EditorGUILayout.HelpBox(
                "개발 서버가 사용할 로컬 포트 번호입니다. (기본값: 5173)",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildSettings()
        {
            EditorGUILayout.LabelField("빌드 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.isProduction = EditorGUILayout.Toggle("프로덕션 모드", config.isProduction);
            config.enableOptimization = EditorGUILayout.Toggle("최적화 활성화", config.enableOptimization);

            EditorGUILayout.EndVertical();
        }

        private void DrawWebGLOptimizationSettings()
        {
            showWebGLSettings = EditorGUILayout.Foldout(showWebGLSettings, "WebGL 최적화 설정", true);

            if (!showWebGLSettings) return;

            EditorGUILayout.BeginVertical("box");

            // 현재 Unity 버전 정보
            EditorGUILayout.HelpBox(
                $"현재 Unity 버전: {AITDefaultSettings.GetUnityVersionGroup()}\n" +
                "각 설정은 Unity 버전에 맞게 자동으로 최적화됩니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 메모리 크기
            DrawMemorySizeSetting();

            // 압축 포맷
            DrawCompressionFormatSetting();

            // 스레딩 지원
            DrawThreadsSupportSetting();

            // 데이터 캐싱
            DrawDataCachingSetting();

            // 파일 해싱
            config.nameFilesAsHashes = EditorGUILayout.Toggle("파일명 해싱", config.nameFilesAsHashes);

            GUILayout.Space(10);

            // 변경된 설정 개수 표시
            int modifiedCount = CountModifiedWebGLSettings();
            if (modifiedCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{modifiedCount}개 설정이 기본값에서 변경됨",
                    MessageType.Warning
                );

                if (GUILayout.Button("모든 WebGL 설정 기본값으로 복원"))
                {
                    ResetWebGLSettings();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMemorySizeSetting()
        {
            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            bool isModified = config.memorySize > 0 && config.memorySize != defaultMemory;

            EditorGUILayout.BeginHorizontal();

            // 하이라이트 표시
            DrawModifiedIndicator(isModified);

            string label = config.memorySize <= 0
                ? $"메모리 크기 (자동: {defaultMemory}MB)"
                : "메모리 크기";

            string[] options = { $"자동 ({defaultMemory}MB)", "256MB", "512MB", "768MB", "1024MB", "1536MB" };
            int currentIndex = GetMemorySizeIndex(config.memorySize, defaultMemory);
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.memorySize = IndexToMemorySize(newIndex);

            if (isModified && DrawResetButton())
            {
                config.memorySize = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCompressionFormatSetting()
        {
            WebGLCompressionFormat defaultCompression = AITDefaultSettings.GetDefaultCompressionFormat();
            bool isModified = config.compressionFormat >= 0 && (WebGLCompressionFormat)config.compressionFormat != defaultCompression;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.compressionFormat < 0
                ? $"압축 포맷 (자동: {defaultCompression})"
                : "압축 포맷";

            string[] options = { $"자동 ({defaultCompression})", "Disabled", "Gzip", "Brotli" };
            int currentIndex = config.compressionFormat < 0 ? 0 : config.compressionFormat + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.compressionFormat = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.compressionFormat = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawThreadsSupportSetting()
        {
            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            bool isModified = config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.threadsSupport < 0
                ? $"스레딩 지원 (자동: {(defaultThreads ? "활성화" : "비활성화")})"
                : "스레딩 지원";

            string[] options = { $"자동 ({(defaultThreads ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.threadsSupport < 0 ? 0 : config.threadsSupport + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.threadsSupport = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.threadsSupport = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDataCachingSetting()
        {
            bool defaultCaching = AITDefaultSettings.GetDefaultDataCaching();
            bool isModified = config.dataCaching >= 0 && (config.dataCaching == 1) != defaultCaching;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.dataCaching < 0
                ? $"데이터 캐싱 (자동: {(defaultCaching ? "활성화" : "비활성화")})"
                : "데이터 캐싱";

            string[] options = { $"자동 ({(defaultCaching ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.dataCaching < 0 ? 0 : config.dataCaching + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.dataCaching = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.dataCaching = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "고급 설정", true);

            if (!showAdvancedSettings) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("IL2CPP / Stripping 설정", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 엔진 코드 제거
            config.stripEngineCode = EditorGUILayout.Toggle("엔진 코드 제거", config.stripEngineCode);

            // Managed Stripping Level
            DrawManagedStrippingLevelSetting();

            // IL2CPP 컴파일러 설정
            DrawIl2CppConfigurationSetting();

#if UNITY_2023_3_OR_NEWER
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Unity 6 전용 설정", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Power Preference
            DrawPowerPreferenceSetting();

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

            // 고급 설정 초기화
            if (GUILayout.Button("고급 설정 기본값으로 복원"))
            {
                ResetAdvancedSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawManagedStrippingLevelSetting()
        {
            ManagedStrippingLevel defaultLevel = AITDefaultSettings.GetDefaultManagedStrippingLevel();
            bool isModified = config.managedStrippingLevel >= 0 && (ManagedStrippingLevel)config.managedStrippingLevel != defaultLevel;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.managedStrippingLevel < 0
                ? $"Stripping Level (자동: {defaultLevel})"
                : "Stripping Level";

            string[] options = { $"자동 ({defaultLevel})", "Disabled", "Minimal", "Low", "Medium", "High" };
            int currentIndex = config.managedStrippingLevel < 0 ? 0 : config.managedStrippingLevel + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.managedStrippingLevel = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.managedStrippingLevel = -1;
            }

            EditorGUILayout.EndHorizontal();
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

#if UNITY_2023_3_OR_NEWER
        private void DrawPowerPreferenceSetting()
        {
            WebGLPowerPreference defaultPower = AITDefaultSettings.GetDefaultPowerPreference();
            bool isModified = config.powerPreference >= 0 && (WebGLPowerPreference)config.powerPreference != defaultPower;

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
            bool isModified = config.exceptionSupport >= 0 && (WebGLExceptionSupport)config.exceptionSupport != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.exceptionSupport < 0
                ? $"예외 처리 모드 (자동: ExplicitlyThrown)"
                : "예외 처리 모드";

            string[] options = { "자동 (ExplicitlyThrown)", "None", "ExplicitlyThrownOnly", "FullWithStacktrace", "FullWithoutStacktrace" };
            int currentIndex = config.exceptionSupport < 0 ? 0 : config.exceptionSupport + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.exceptionSupport = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.exceptionSupport = -1;
            }

            EditorGUILayout.EndHorizontal();
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

        // ===== 유틸리티 메서드 =====

        private void DrawModifiedIndicator(bool isModified)
        {
            if (isModified)
            {
                var originalColor = GUI.color;
                GUI.color = ModifiedColor;
                GUILayout.Label("●", GUILayout.Width(15));
                GUI.color = originalColor;
            }
            else
            {
                GUILayout.Label("", GUILayout.Width(15));
            }
        }

        private bool DrawResetButton()
        {
            return GUILayout.Button("↺", GUILayout.Width(25));
        }

        private int GetMemorySizeIndex(int memorySize, int defaultMemory)
        {
            if (memorySize <= 0) return 0;

            switch (memorySize)
            {
                case 256: return 1;
                case 512: return 2;
                case 768: return 3;
                case 1024: return 4;
                case 1536: return 5;
                default: return 0;
            }
        }

        private int IndexToMemorySize(int index)
        {
            switch (index)
            {
                case 0: return -1;  // 자동
                case 1: return 256;
                case 2: return 512;
                case 3: return 768;
                case 4: return 1024;
                case 5: return 1536;
                default: return -1;
            }
        }

        private int CountModifiedWebGLSettings()
        {
            int count = 0;

            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            if (config.memorySize > 0 && config.memorySize != defaultMemory) count++;

            WebGLCompressionFormat defaultCompression = AITDefaultSettings.GetDefaultCompressionFormat();
            if (config.compressionFormat >= 0 && (WebGLCompressionFormat)config.compressionFormat != defaultCompression) count++;

            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            if (config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads) count++;

            bool defaultCaching = AITDefaultSettings.GetDefaultDataCaching();
            if (config.dataCaching >= 0 && (config.dataCaching == 1) != defaultCaching) count++;

            return count;
        }

        private void ResetWebGLSettings()
        {
            config.memorySize = -1;
            config.compressionFormat = -1;
            config.threadsSupport = -1;
            config.dataCaching = -1;
        }

        private void ResetAdvancedSettings()
        {
            config.stripEngineCode = true;
            config.managedStrippingLevel = -1;
            config.il2cppConfiguration = -1;
            config.powerPreference = -1;
#if !UNITY_6000_0_OR_NEWER
            config.wasmStreaming = true;
            config.webAssemblyArithmeticExceptions = -1;
#endif
            config.exceptionSupport = -1;
            config.showUnityLogo = -1;
            config.decompressionFallback = -1;
            config.runInBackground = -1;
        }

        private void DrawAdvertisementSettings()
        {
            EditorGUILayout.LabelField("광고 설정 (선택)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.enableAdvertisement = EditorGUILayout.Toggle("광고 활성화", config.enableAdvertisement);

            if (config.enableAdvertisement)
            {
                EditorGUI.indentLevel++;
                config.interstitialAdGroupId = EditorGUILayout.TextField("전면 광고 ID", config.interstitialAdGroupId);
                config.rewardedAdGroupId = EditorGUILayout.TextField("보상형 광고 ID", config.rewardedAdGroupId);
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    "광고 ID는 Apps in Toss 콘솔에서 발급받을 수 있습니다.",
                    MessageType.Info
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDeploymentSettings()
        {
            EditorGUILayout.LabelField("배포 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.deploymentKey = EditorGUILayout.PasswordField("배포 키 (API Key)", config.deploymentKey);

            if (string.IsNullOrWhiteSpace(config.deploymentKey))
            {
                EditorGUILayout.HelpBox(
                    "배포 키를 입력해주세요. 배포 시 필수입니다.",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox("배포 키가 설정되었습니다.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProjectInfo()
        {
            EditorGUILayout.LabelField("프로젝트 정보", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("프로젝트 이름:", PlayerSettings.productName);
            EditorGUILayout.LabelField("Unity 버전:", $"{Application.unityVersion} ({AITDefaultSettings.GetUnityVersionGroup()})");
            EditorGUILayout.LabelField("SDK 버전:", "1.0.0");

            GUILayout.Space(10);

            // 적용될 WebGL 설정 요약
            EditorGUILayout.LabelField("적용될 WebGL 설정", EditorStyles.boldLabel);

            int effectiveMemory = config.memorySize > 0 ? config.memorySize : AITDefaultSettings.GetDefaultMemorySize();
            EditorGUILayout.LabelField($"  메모리: {effectiveMemory}MB");

            WebGLCompressionFormat effectiveCompression = config.compressionFormat >= 0
                ? (WebGLCompressionFormat)config.compressionFormat
                : AITDefaultSettings.GetDefaultCompressionFormat();
            EditorGUILayout.LabelField($"  압축: {effectiveCompression}");

            bool effectiveThreads = config.threadsSupport >= 0
                ? config.threadsSupport == 1
                : AITDefaultSettings.GetDefaultThreadsSupport();
            EditorGUILayout.LabelField($"  스레딩: {(effectiveThreads ? "활성화" : "비활성화")}");

            bool effectiveCaching = config.dataCaching >= 0
                ? config.dataCaching == 1
                : AITDefaultSettings.GetDefaultDataCaching();
            EditorGUILayout.LabelField($"  데이터 캐싱: {(effectiveCaching ? "활성화" : "비활성화")}");

            GUILayout.Space(10);

            // 설정 검증 상태 요약
            bool readyForBuild = config.IsIconUrlValid() && config.IsAppNameValid() && config.IsVersionValid();
            bool readyForDeploy = config.IsReadyForDeploy();

            if (readyForBuild)
            {
                EditorGUILayout.HelpBox("빌드 준비 완료", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("설정을 완료해주세요 (아이콘 URL, 앱 ID, 버전)", MessageType.Warning);
            }

            if (readyForDeploy)
            {
                EditorGUILayout.HelpBox("배포 준비 완료", MessageType.Info);
            }
            else if (!readyForDeploy && readyForBuild)
            {
                EditorGUILayout.HelpBox("배포 키를 입력해주세요", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void SaveSettings()
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
