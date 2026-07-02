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
        private bool showBuildProfiles = true;
        private bool showDevServerProfile = false;
        private bool showProductionProfile = false;
        private bool showTextureStreamingSettings = true;

        // 하이라이트 색상
        private static readonly Color ModifiedColor = new Color(1f, 0.6f, 0f); // 주황색
        private static readonly Color RequiredFieldColor = new Color(1f, 0.4f, 0.4f); // 붉은색 (필수 필드 누락)

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
            AITDeprecationChecker.DrawDeprecationBanner();
            DrawAppInfo();
            GUILayout.Space(10);
            DrawBrandSettings();
            GUILayout.Space(10);
            DrawWebViewSettings();
            GUILayout.Space(10);
            DrawDevServerSettings();
            GUILayout.Space(10);
            DrawBuildOutputSettings();
            GUILayout.Space(10);
            DrawBuildSettings();
            GUILayout.Space(10);
            DrawWebGLOptimizationSettings();
            GUILayout.Space(10);
            DrawTextureStreamingSettings();
            GUILayout.Space(10);
            DrawPermissionSettings();
            GUILayout.Space(10);
            DrawAdvancedSettings();
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

            // 앱 ID (필수 필드)
            bool appNameMissing = string.IsNullOrWhiteSpace(config.appName);
            bool appNameInvalid = !appNameMissing && !config.IsAppNameValid();

            if (appNameMissing || appNameInvalid)
            {
                GUI.backgroundColor = RequiredFieldColor;
            }
            config.appName = EditorGUILayout.TextField("앱 ID *", config.appName);
            GUI.backgroundColor = Color.white;

            if (appNameInvalid)
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
            config.iconUrl = EditorGUILayout.TextField("아이콘 URL", config.iconUrl);

            // 아이콘 URL 검증 (선택 사항)
            if (!string.IsNullOrWhiteSpace(config.iconUrl) && !config.IsIconUrlValid())
            {
                EditorGUILayout.HelpBox(
                    "아이콘 URL은 http:// 또는 https://로 시작해야 합니다.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWebViewSettings()
        {
            EditorGUILayout.LabelField("WebView 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // bridgeColorMode 드롭다운
            string[] bridgeColorModeOptions = { "inverted (게임앱 권장)", "basic (일반앱)" };
            config.bridgeColorMode = EditorGUILayout.Popup("Bridge Color Mode", config.bridgeColorMode, bridgeColorModeOptions);

            EditorGUILayout.HelpBox(
                "게임앱은 'inverted' (다크 모드)를 사용합니다.\n" +
                "일반앱은 'basic'을 사용합니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // webViewProps.type 드롭다운
            string[] webViewTypeOptions = { "game (게임앱 - 투명배경)", "partner (일반앱 - 흰색배경)" };
            config.webViewType = EditorGUILayout.Popup("WebView Type", config.webViewType, webViewTypeOptions);

            EditorGUILayout.HelpBox(
                "게임앱은 'game' 타입으로 투명 배경 내비게이션이 적용됩니다.\n" +
                "일반앱은 'partner' 타입으로 흰색 배경 내비게이션이 적용됩니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 네비게이션 바 설정 (game 타입에서만 적용)
            using (new EditorGUI.DisabledScope(config.webViewType != 0))
            {
                config.navigationBarTransparentBackground = EditorGUILayout.Toggle(
                    new GUIContent("Transparent Nav Bar", "상단 네비게이션 바 투명 배경 (게임 풀스크린)"),
                    config.navigationBarTransparentBackground
                );

                string[] navBarThemeOptions = { "기본 (미지정)", "light", "dark" };
                config.navigationBarTheme = EditorGUILayout.Popup(
                    "Nav Bar Theme", config.navigationBarTheme, navBarThemeOptions);
            }

            EditorGUILayout.HelpBox(
                "Navigation Bar 옵션은 'game' 타입에서만 적용됩니다.\n" +
                "Transparent Nav Bar를 켜면 상단(노치/내비게이션) 영역까지 투명하게 풀스크린 렌더링됩니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 미디어 재생 설정
            config.allowsInlineMediaPlayback = EditorGUILayout.Toggle(
                new GUIContent("Inline Media Playback", "인라인 미디어 재생 허용"),
                config.allowsInlineMediaPlayback
            );

            config.mediaPlaybackRequiresUserAction = EditorGUILayout.Toggle(
                new GUIContent("Require User Action", "미디어 재생 시 사용자 액션 필요"),
                config.mediaPlaybackRequiresUserAction
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawDevServerSettings()
        {
            EditorGUILayout.LabelField("서버 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.graniteHost = EditorGUILayout.TextField("Granite 호스트", config.graniteHost);
            config.granitePort = EditorGUILayout.IntField("Granite 포트", config.granitePort);
            config.viteHost = EditorGUILayout.TextField("Vite 호스트", config.viteHost);
            config.vitePort = EditorGUILayout.IntField("Vite 포트", config.vitePort);

            EditorGUILayout.HelpBox(
                "Granite (Metro) 서버와 Vite 서버 설정입니다.\n" +
                "기본값: Granite 0.0.0.0:8081, Vite localhost:5173\n" +
                "브라우저는 Vite 포트로 열립니다.\n" +
                "환경 변수: AIT_GRANITE_HOST, AIT_GRANITE_PORT, AIT_VITE_HOST, AIT_VITE_PORT",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildOutputSettings()
        {
            EditorGUILayout.LabelField("빌드 출력 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.outdir = EditorGUILayout.TextField("출력 디렉토리", config.outdir);

            EditorGUILayout.HelpBox(
                "granite build 결과물이 저장될 디렉토리입니다. (기본값: dist)",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildSettings()
        {
            showBuildProfiles = EditorGUILayout.Foldout(showBuildProfiles, "빌드 프로필", true);

            if (!showBuildProfiles) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.HelpBox(
                "Dev Server: 로컬 개발/테스트용 (빌드 속도 우선)\n" +
                "Production: 배포용 (Prod Server, Build & Package, Publish에서 공통 사용)",
                MessageType.Info
            );

            GUILayout.Space(10);

            // Dev Server 프로필
            DrawBuildProfile(
                ref showDevServerProfile,
                "Dev Server",
                "로컬 개발/테스트용 (빌드 속도 우선, Mock 브릿지 활성화)",
                config.devServerProfile,
                AITBuildProfile.CreateDevServerProfile()
            );

            GUILayout.Space(5);

            // Production 프로필
            DrawBuildProfile(
                ref showProductionProfile,
                "Production",
                "배포용 (Prod Server, Build & Package, Publish에서 공통 사용)",
                config.productionProfile,
                AITBuildProfile.CreateProductionProfile()
            );

            GUILayout.Space(10);

            // 모든 프로필 초기화 버튼
            if (GUILayout.Button("모든 프로필 기본값으로 초기화"))
            {
                if (AITPlatformHelper.ShowConfirmDialog("프로필 초기화", "모든 빌드 프로필을 기본값으로 초기화하시겠습니까?", "예", "아니오", autoApprove: true))
                {
                    config.devServerProfile = AITBuildProfile.CreateDevServerProfile();
                    config.productionProfile = AITBuildProfile.CreateProductionProfile();
                    SaveSettings();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildProfile(ref bool foldout, string name, string description, AITBuildProfile profile, AITBuildProfile defaultProfile)
        {
            EditorGUILayout.BeginVertical("box");

            foldout = EditorGUILayout.Foldout(foldout, $"{name}", true);

            if (!foldout)
            {
                // 접힌 상태에서 요약 표시
                EditorGUI.indentLevel++;
                string summary = GetProfileSummary(profile);
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(description, MessageType.None);

            EditorGUI.indentLevel++;

            // 런타임 설정 헤더
            EditorGUILayout.LabelField("런타임 설정", EditorStyles.boldLabel);

            // Mock 브릿지
            profile.enableMockBridge = EditorGUILayout.Toggle(
                new GUIContent("Mock 브릿지 사용", "로컬 테스트용, 네이티브 API 없이 동작"),
                profile.enableMockBridge
            );

            // 디버그 콘솔
            profile.enableDebugConsole = EditorGUILayout.Toggle(
                new GUIContent("디버그 콘솔 활성화", "개발/테스트 목적으로 콘솔 사용"),
                profile.enableDebugConsole
            );

            GUILayout.Space(5);

            // 빌드 설정 헤더
            EditorGUILayout.LabelField("빌드 설정", EditorStyles.boldLabel);

            // Development Build
            profile.developmentBuild = EditorGUILayout.Toggle(
                new GUIContent("Development Build", "빌드 속도 향상 및 디버깅 편의성 (배포용에서는 비활성화 권장)"),
                profile.developmentBuild
            );

            // LZ4 압축
            profile.enableLZ4Compression = EditorGUILayout.Toggle(
                new GUIContent("LZ4 압축", "빌드 프로세스 속도 향상을 위한 LZ4 압축"),
                profile.enableLZ4Compression
            );

            // 압축 포맷
            string[] compressionOptions = { "자동", "Disabled", "Gzip", "Brotli" };
            int compressionIndex = profile.compressionFormat < 0 ? 0 : profile.compressionFormat + 1;
            compressionIndex = EditorGUILayout.Popup(
                new GUIContent("WebGL 압축", "최종 빌드 결과물의 압축 포맷 (-1=자동: Brotli)"),
                compressionIndex,
                compressionOptions
            );
            profile.compressionFormat = compressionIndex == 0 ? -1 : compressionIndex - 1;

            // Stripping Level
            string[] strippingOptions = { "자동 (High)", "Disabled", "Minimal", "Low", "Medium", "High" };
            int strippingIndex = profile.managedStrippingLevel < 0 ? 0 : profile.managedStrippingLevel + 1;
            strippingIndex = EditorGUILayout.Popup(
                new GUIContent("Managed Stripping", "코드 스트리핑 레벨 (낮을수록 빌드 속도 향상)"),
                strippingIndex,
                strippingOptions
            );
            profile.managedStrippingLevel = strippingIndex == 0 ? -1 : strippingIndex - 1;

            // 디버그 심볼
            profile.debugSymbolsExternal = EditorGUILayout.Toggle(
                new GUIContent("디버그 심볼 외부 분리", "빌드 크기 감소를 위해 심볼을 외부 파일로 분리"),
                profile.debugSymbolsExternal
            );

            // 기본값과 다른 경우 리셋 버튼 표시
            if (!IsProfileDefault(profile, defaultProfile))
            {
                GUILayout.Space(5);
                if (GUILayout.Button($"{name} 프로필 기본값으로 복원", GUILayout.Height(20)))
                {
                    ResetProfile(profile, defaultProfile);
                }
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        private void ResetProfile(AITBuildProfile profile, AITBuildProfile defaultProfile)
        {
            profile.enableMockBridge = defaultProfile.enableMockBridge;
            profile.enableDebugConsole = defaultProfile.enableDebugConsole;
            profile.developmentBuild = defaultProfile.developmentBuild;
            profile.enableLZ4Compression = defaultProfile.enableLZ4Compression;
            profile.compressionFormat = defaultProfile.compressionFormat;
            profile.managedStrippingLevel = defaultProfile.managedStrippingLevel;
            profile.debugSymbolsExternal = defaultProfile.debugSymbolsExternal;
        }

        private string GetProfileSummary(AITBuildProfile profile)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (profile.enableMockBridge) parts.Add("Mock");
            if (profile.enableDebugConsole) parts.Add("Debug");
            if (profile.developmentBuild) parts.Add("Dev");
            if (profile.enableLZ4Compression) parts.Add("LZ4");

            // 압축 포맷
            string compression = profile.compressionFormat switch
            {
                0 => "NoCompress",
                1 => "Gzip",
                2 => "Brotli",
                _ => ""
            };
            if (!string.IsNullOrEmpty(compression)) parts.Add(compression);

            // Stripping
            string stripping = profile.managedStrippingLevel switch
            {
                0 => "NoStrip",
                1 => "Minimal",
                _ => ""
            };
            if (!string.IsNullOrEmpty(stripping)) parts.Add(stripping);

            return parts.Count > 0 ? string.Join(", ", parts) : "(기본 설정)";
        }

        private bool IsProfileDefault(AITBuildProfile profile, AITBuildProfile defaultProfile)
        {
            return profile.enableMockBridge == defaultProfile.enableMockBridge &&
                   profile.enableDebugConsole == defaultProfile.enableDebugConsole &&
                   profile.developmentBuild == defaultProfile.developmentBuild &&
                   profile.enableLZ4Compression == defaultProfile.enableLZ4Compression &&
                   profile.compressionFormat == defaultProfile.compressionFormat &&
                   profile.managedStrippingLevel == defaultProfile.managedStrippingLevel &&
                   profile.debugSymbolsExternal == defaultProfile.debugSymbolsExternal;
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

            // 스레딩 지원
            DrawThreadsSupportSetting();

            // 데이터 캐싱
            DrawDataCachingSetting();

            // 파일 해싱
            config.nameFilesAsHashes = EditorGUILayout.Toggle("파일명 해싱", config.nameFilesAsHashes);

            GUILayout.Space(10);

            // first-interactive 계측
            DrawFirstInteractiveLogSetting();
            // 페이지 캐시 (재방문 서빙, opt-in)
            DrawPageCacheSetting();

            // 콘텐츠 최적화 — 오디오 스트리밍
            DrawAudioStreamingSetting();

            GUILayout.Space(10);

            // 콘텐츠 최적화 — 텍스처 crunch (빌드 산출물 .data 축소)
            DrawTextureCrunchSetting();

            GUILayout.Space(10);

            // 콘텐츠 최적화 — 텍스처 크기 클램프 (maxTextureSize 캡)
            DrawTextureSizeClampSetting();
            // 콘텐츠 최적화 — ASTC 블록 에스컬레이션
            DrawAstcBlockSetting();
            // 콘텐츠 최적화 — 폰트 CJK subset (.data 폰트 데이터 축소)
            DrawFontSubsetSetting();

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

        private void DrawAudioStreamingSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultAudioStreaming();
            bool isModified = config.audioStreaming >= 0 && (config.audioStreaming == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 오디오 스트리밍", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.audioStreaming < 0
                ? $"오디오 스트리밍 (자동: {autoLabel})"
                : "오디오 스트리밍";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.audioStreaming < 0 ? 0 : config.audioStreaming + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대용량 오디오를 초기 .data에서 분리해 StreamingAssets로 외부화하고, 런타임에 비동기 스트리밍으로 복원합니다. " +
                    "초기 다운로드/TTI를 크게 줄입니다. 빌드 시 오디오를 무음 스텁으로 일시 치환했다가 빌드 후 원상 복원합니다."),
                currentIndex,
                options
            );
            config.audioStreaming = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.audioStreaming = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 오디오 스트리밍이 켜져 있으면 (자동 포함) 하위 옵션 + 지연 안내 항시 표시
            bool effectiveEnabled = config.audioStreaming >= 0 ? config.audioStreaming == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;

                config.audioStreamingMinBytes = EditorGUILayout.IntField(
                    new GUIContent("최소 크기(Bytes)", "이 바이트 수보다 큰 AudioClip만 외부화합니다 (기본 262144 = 256KB)."),
                    config.audioStreamingMinBytes);
                if (config.audioStreamingMinBytes <= 0)
                {
                    config.audioStreamingMinBytes = 262144;
                }

                config.audioStreamingDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체의 큰 오디오가 대상. 예) Assets/Sounds/BGM,Assets/Music"),
                    config.audioStreamingDirs);

                EditorGUI.indentLevel--;

                // 지연 안내 — 항시 표시 (active 상태인 경우)
                EditorGUILayout.HelpBox(
                    "interactive 이후 오디오가 비동기 복원되어 초기 BGM 시작이 수백 ms 지연될 수 있음. " +
                    "빌드 후 소스 오디오는 자동 복원됩니다.",
                    MessageType.Info);
            }
        }

        private void DrawTextureCrunchSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultTextureCrunch();
            bool isModified = config.textureCrunch >= 0 && (config.textureCrunch == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 텍스처 crunch", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.textureCrunch < 0
                ? $"텍스처 crunch (자동: {autoLabel})"
                : "텍스처 crunch";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.textureCrunch < 0 ? 0 : config.textureCrunch + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대상 텍스처/SpriteAtlas를 빌드 시 crunch(DXT 위 4~8x) 압축 + maxTextureSize 캡으로 reimport하여 다운로드/.data를 줄입니다. " +
                    "빌드 후 원본 임포트 설정으로 복원합니다. crunch reimport는 무겁습니다(에셋 수에 비례)."),
                currentIndex,
                options
            );
            config.textureCrunch = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.textureCrunch = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.textureCrunch >= 0 ? config.textureCrunch == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;
                config.textureCrunchMaxSize = EditorGUILayout.IntField(
                    new GUIContent("최대 텍스처 크기(0=캡 안 함)", "이 값보다 큰 텍스처만 축소합니다. 예) 512, 1024"),
                    config.textureCrunchMaxSize);

                config.textureCrunchQuality = EditorGUILayout.IntSlider(
                    new GUIContent("crunch 품질(0~100)", "낮을수록 작고 화질↓. 기본 50."),
                    Mathf.Clamp(config.textureCrunchQuality, 0, 100), 0, 100);

                config.textureCrunchAtlas = EditorGUILayout.Toggle(
                    new GUIContent("SpriteAtlas 포함", "SpriteAtlas도 함께 crunch + WebGL repack 합니다."),
                    config.textureCrunchAtlas);

                if (config.textureCrunchAtlas)
                {
                    config.textureCrunchAtlasMaxSize = EditorGUILayout.IntField(
                        new GUIContent("아틀라스 최대 크기(0=캡 안 함)", "예) 1024, 2048"),
                        config.textureCrunchAtlasMaxSize);
                }

                config.textureCrunchDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체 텍스처가 대상. 예) Assets/Art/Textures"),
                    config.textureCrunchDirs);

                EditorGUILayout.HelpBox(
                    "crunch는 lossy 압축입니다. 특히 그라데이션/UI 텍스처에서 화질이 눈에 띄게 저하될 수 있으므로 " +
                    "빌드 후 결과를 반드시 확인하세요. 빌드 후 원본 임포트 설정은 자동 복원됩니다.\n\n" +
                    "⚠ WebGL Texture Compression을 ASTC로 설정한 경우 crunch(DXT 기반)가 동작하지 않으며 " +
                    "오히려 RGBA32 비압축으로 팽창할 수 있습니다. 이 경우 빌드 시 자동으로 건너뜁니다(DXT 서브타겟에서만 유효).",
                    MessageType.Warning);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTextureSizeClampSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultTextureSizeClamp();
            bool isModified = config.textureSizeClamp >= 0 && (config.textureSizeClamp == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 텍스처 크기 클램프", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.textureSizeClamp < 0
                ? $"텍스처 크기 클램프 (자동: {autoLabel})"
                : "텍스처 크기 클램프";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.textureSizeClamp < 0 ? 0 : config.textureSizeClamp + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대상 텍스처의 maxTextureSize만 빌드 시 일시적으로 캡(상한)으로 낮춰 reimport하여 텍셀 수를 줄입니다 " +
                    "(format/compression/crunch 불변). 예) 2048→1024는 텍셀 1/4. 빌드 후 원본 임포트 설정으로 복원합니다."),
                currentIndex,
                options
            );
            config.textureSizeClamp = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.textureSizeClamp = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.textureSizeClamp >= 0 ? config.textureSizeClamp == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;
                config.textureClampMaxSize = EditorGUILayout.IntField(
                    new GUIContent("최대 텍스처 크기", "이 값보다 큰 텍스처만 축소합니다. 16 미만은 무시. 예) 512, 1024"),
                    config.textureClampMaxSize);

                int minBytesKb = EditorGUILayout.IntField(
                    new GUIContent("소스 크기 필터(KB, 0=없음)", "소스 파일이 이 크기 미만이면 제외(작은 아이콘 보호)."),
                    (int)(config.textureClampMinBytes / 1024));
                config.textureClampMinBytes = (long)Mathf.Max(0, minBytesKb) * 1024;

                config.textureClampDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체 텍스처가 대상. 예) Assets/Art/Backgrounds"),
                    config.textureClampDirs);

                config.textureClampExcludeDirs = EditorGUILayout.TextField(
                    new GUIContent("제외 폴더(쉼표 구분)", "Assets/ 기준 경로. 클램프에서 제외할 폴더(escape hatch)."),
                    config.textureClampExcludeDirs);

                EditorGUILayout.HelpBox(
                    "크기 클램프는 표시 해상도를 낮추는 lossy 변경입니다(예: 2048→1024). UI/텍스트 텍스처에서 흐려짐이 " +
                    "눈에 띌 수 있으므로 빌드 후 결과를 반드시 확인하세요. crunch와 달리 format/압축은 건드리지 않아 " +
                    "ASTC로 굽는 프로젝트에서도 그대로 적용됩니다. 빌드 후 원본 임포트 설정은 자동 복원됩니다.",
                    MessageType.Warning);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFontSubsetSetting()
        {
            EditorGUILayout.LabelField("콘텐츠 최적화 — 폰트 CJK subset", EditorStyles.boldLabel);

            bool defaultValue = AITDefaultSettings.GetDefaultFontSubset();
            // 수동 설정 모드: targetPaths 또는 unicodeRanges 가 채워져 있으면 override(수동) 모드로 간주.
            bool hasManualOverride = !string.IsNullOrEmpty(config.fontSubsetTargetPaths)
                || !string.IsNullOrEmpty(config.fontSubsetUnicodeRanges);

            // tri-state 매핑: 0=자동, 1=비활성화, 2=수동 설정.
            int currentIndex;
            if (config.fontSubset == 0)
            {
                currentIndex = 1;
            }
            else if (hasManualOverride)
            {
                currentIndex = 2;
            }
            else
            {
                currentIndex = 0;
            }

            EditorGUILayout.BeginHorizontal();

            bool isModified = config.fontSubset == 0 || hasManualOverride;
            DrawModifiedIndicator(isModified);

            string label = currentIndex == 0
                ? $"폰트 subset (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "폰트 subset";
            string[] options = { $"자동 ({(defaultValue ? "활성화" : "비활성화")})", "비활성화", "수동 설정" };
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "zero-config: 자동 모드는 크고(≥1MB) 빌드 포함 가능한 폰트를 탐지하고, 프로젝트에 등장하는 " +
                    "문자체계의 유니코드 블록 전체를 보존하도록 subset합니다(동적 텍스트도 □가 되지 않음). " +
                    "수동 설정은 대상/범위를 직접 지정합니다."),
                currentIndex, options);

            if (newIndex != currentIndex)
            {
                switch (newIndex)
                {
                    case 0: // 자동
                        config.fontSubset = -1;
                        config.fontSubsetTargetPaths = string.Empty;
                        config.fontSubsetUnicodeRanges = string.Empty;
                        break;
                    case 1: // 비활성화
                        config.fontSubset = 0;
                        break;
                    case 2: // 수동 설정
                        config.fontSubset = 1;
                        break;
                }

                currentIndex = newIndex;
            }

            if (isModified && DrawResetButton())
            {
                config.fontSubset = -1;
                config.fontSubsetTargetPaths = string.Empty;
                config.fontSubsetUnicodeRanges = string.Empty;
                currentIndex = 0;
            }

            EditorGUILayout.EndHorizontal();

            if (currentIndex == 0)
            {
                EditorGUILayout.HelpBox(
                    "자동 모드: 대상 폰트를 탐지하고, 씬/프리팹/asset/스크립트/로컬라이제이션에 등장하는 " +
                    "문자체계의 유니코드 블록 전체를 보존하도록 subset합니다. " +
                    "한자는 KS X 1001 상용 한자(4,888자) + 감지된 한자를 보존하며, ASCII·한글 등 베이스라인은 항상 포함됩니다. " +
                    "빌드 로그에 드롭 리포트가 출력됩니다.",
                    MessageType.Info);
            }
            else if (currentIndex == 2)
            {
                EditorGUI.indentLevel++;
                config.fontSubsetTargetPaths = EditorGUILayout.TextField(
                    new GUIContent("대상 폰트 경로(쉼표 구분)", "Assets/ 기준의 .ttf/.otf. 비우면 자동 탐지로 폴백됩니다. 예) Assets/Fonts/NotoSansKR.ttf"),
                    config.fontSubsetTargetPaths);

                config.fontSubsetUnicodeRanges = EditorGUILayout.TextField(
                    new GUIContent("보존 유니코드 범위", "쉼표 구분(fontTools 표기). 비우면 Auto 스캔이 범위를 결정합니다. 예) U+0020-007E,U+AC00-D7A3"),
                    config.fontSubsetUnicodeRanges);
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    "⚠ 수동 보존 범위를 지정하면 그 범위만 남고 나머지 글자(희귀 한자/이모지/동적 텍스트)는 □로 렌더됩니다. " +
                    "범위를 비우면 Auto 스캔이 등장 문자체계를 보존하므로 더 안전합니다.",
                    MessageType.Warning);
            }
        }

        private void DrawMemorySizeSetting()
        {
            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            bool isModified = config.memorySize > 0 && config.memorySize != defaultMemory;

            EditorGUILayout.BeginHorizontal();

            // 하이라이트 표시
            DrawModifiedIndicator(isModified);

            string label = config.memorySize <= 0
                ? $"초기 메모리 크기 (자동: {defaultMemory}MB)"
                : "초기 메모리 크기";

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

        private void DrawPageCacheSetting()
        {
            bool defaultPageCache = AITDefaultSettings.GetDefaultPageCache();
            bool isModified = config.pageCache >= 0 && (config.pageCache == 1) != defaultPageCache;

            EditorGUILayout.LabelField("WebGL 로딩 — 페이지 캐시(재방문 서빙)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.pageCache < 0
                ? $"페이지 캐시 (자동: {(defaultPageCache ? "활성화" : "비활성화")})"
                : "페이지 캐시";

            string[] options = { $"자동 ({(defaultPageCache ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.pageCache < 0 ? 0 : config.pageCache + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "재방문 시 Build/* 자산을 CacheStorage 에서 직접 서빙합니다(ServiceWorker 불필요). " +
                    "첫 방문(콜드)에는 효과가 없고, 미지원/비보안 환경에서는 자동으로 원래 로드로 무해 통과합니다. " +
                    "기본 자동(활성화). -1=자동, 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.pageCache = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.pageCache = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 활성(자동 또는 명시적 활성화) 시 캐시명 설정 표시
            bool pageCacheActive = config.pageCache < 0
                ? defaultPageCache
                : config.pageCache == 1;

            if (pageCacheActive)
            {
                EditorGUI.indentLevel++;

                // 자동 파생 캐시명 미리보기
                string derivedName = AppsInToss.Editor.Package.AITPageCacheEmitter.ResolveCacheName(config);
                bool isNameAuto = string.IsNullOrEmpty(config.pageCacheName);

                config.pageCacheName = EditorGUILayout.TextField(
                    new GUIContent(
                        isNameAuto ? $"캐시 이름 (자동: {derivedName})" : "캐시 이름",
                        "호스트 백그라운드 pre-fill 페이지와 동일한 이름을 써야 같은 캐시를 공유합니다. " +
                        "비우면 앱 ID(appName)에서 자동 파생합니다(멀티앱 오리진 공유 시 sweep 상호 간섭 방지). " +
                        "런타임 window.__AIT_CACHE_NAME 으로도 오버라이드 가능."),
                    config.pageCacheName
                );

                EditorGUILayout.HelpBox(
                    "재방문 전용 최적화입니다. 콘텐츠-해시 URL(파일명 해싱) 전제이며, " +
                    "부팅 시 현재 빌드에 없는 옛 캐시 엔트리는 자동 정리됩니다. " +
                    "저장 공간 초과 시 무해하게 건너뜁니다.\n" +
                    $"적용될 캐시명: {derivedName}{(isNameAuto ? " (appName 기반 자동 파생)" : "")}",
                    MessageType.Info
                );

                // appName 미설정 + pageCacheName 미설정 시 기본 폴백 경고
                if (isNameAuto && string.IsNullOrWhiteSpace(config.appName))
                {
                    EditorGUILayout.HelpBox(
                        "앱 ID(appName)가 비어 있어 기본 캐시명 'ait-page-cache' 을 사용합니다. " +
                        "멀티앱 오리진 공유 환경에서는 앱 ID를 설정하거나 캐시 이름을 직접 지정하세요.",
                        MessageType.Warning
                    );
                }

                EditorGUI.indentLevel--;
            }

            // warm manifest 산출 (tri-state) — pageCache 실효값이 OFF 이면 회색 비활성.
            // if (pageCacheActive) 블록 바깥에 배치해 항상 렌더링(숨김 없음).
            using (new EditorGUI.DisabledScope(!pageCacheActive))
            {
                DrawWarmManifestSetting();
            }

            // pageCache=OFF + warmManifest 실효값=ON 일 때 경고 HelpBox 표시.
            if (!pageCacheActive)
            {
                bool warmManifestEffective = config.warmManifest < 0
                    ? AITDefaultSettings.GetDefaultWarmManifest()
                    : config.warmManifest == 1;

                if (warmManifestEffective)
                {
                    EditorGUILayout.HelpBox(
                        "페이지 캐시가 비활성이므로 Warm Manifest 도 산출되지 않습니다. " +
                        "페이지 캐시를 활성화하거나 Warm Manifest 를 명시적으로 비활성화하세요.",
                        MessageType.Warning
                    );
                }
            }

            // warm 페이지 산출 (tri-state) — pageCache·warmManifest 실효값이 모두 true 일 때만 편집 가능.
            bool warmManifestEffectiveForPage = config.warmManifest < 0
                ? AITDefaultSettings.GetDefaultWarmManifest()
                : config.warmManifest == 1;
            using (new EditorGUI.DisabledScope(!(pageCacheActive && warmManifestEffectiveForPage)))
            {
                DrawWarmPageSetting();
            }

            // pageCache 또는 warmManifest 실효값=OFF + warmPage 실효값=ON 일 때 경고 HelpBox 표시.
            if (!(pageCacheActive && warmManifestEffectiveForPage))
            {
                bool warmPageEffective = config.warmPage < 0
                    ? AITDefaultSettings.GetDefaultWarmPage()
                    : config.warmPage == 1;

                if (warmPageEffective)
                {
                    EditorGUILayout.HelpBox(
                        "페이지 캐시 또는 Warm Manifest 가 비활성이므로 Warm 페이지도 산출되지 않습니다. " +
                        "페이지 캐시와 Warm Manifest 를 활성화하거나 Warm 페이지를 명시적으로 비활성화하세요.",
                        MessageType.Warning
                    );
                }
            }

            // 네이티브 에셋 소스 우선 (tri-state) — pageCache 실효값이 ON 일 때만 편집 가능(인터셉터 존재 전제).
            // warmManifest/warmPage 와 독립: 인터셉터만 있으면 신호를 노출하므로 pageCache 에만 AND 게이팅.
            using (new EditorGUI.DisabledScope(!pageCacheActive))
            {
                DrawNativeAssetSourceSetting();
            }

            // pageCache 실효값=OFF + nativeAssetSource 실효값=ON 일 때 경고 HelpBox 표시.
            if (!pageCacheActive)
            {
                bool nativeSourceEffective = config.nativeAssetSource < 0
                    ? AITDefaultSettings.GetDefaultNativeAssetSource()
                    : config.nativeAssetSource == 1;

                if (nativeSourceEffective)
                {
                    EditorGUILayout.HelpBox(
                        "페이지 캐시가 비활성이므로 네이티브 에셋 소스도 동작하지 않습니다(인터셉터 없음). " +
                        "페이지 캐시를 활성화하거나 네이티브 에셋 소스를 명시적으로 비활성화하세요.",
                        MessageType.Warning
                    );
                }
            }
        }

        private void DrawWarmManifestSetting()
        {
            bool defaultWarmManifest = AITDefaultSettings.GetDefaultWarmManifest();
            bool isModified = config.warmManifest >= 0 && (config.warmManifest == 1) != defaultWarmManifest;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.warmManifest < 0
                ? $"Warm Manifest 산출 (자동: {(defaultWarmManifest ? "활성화" : "비활성화")})"
                : "Warm Manifest 산출";

            string[] options = { $"자동 ({(defaultWarmManifest ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.warmManifest < 0 ? 0 : config.warmManifest + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "빌드 시 ait-warm-manifest.json 을 web 루트에 산출합니다. " +
                    "호스트(슈퍼앱)가 선다운로드(warm) diff 기준으로 사용합니다. " +
                    "페이지 캐시 실효값이 OFF 이면 회색 비활성화됩니다(AND 게이팅). " +
                    "-1=자동(활성화), 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.warmManifest = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.warmManifest = -1;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawWarmPageSetting()
        {
            bool defaultWarmPage = AITDefaultSettings.GetDefaultWarmPage();
            bool isModified = config.warmPage >= 0 && (config.warmPage == 1) != defaultWarmPage;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.warmPage < 0
                ? $"Warm 페이지 산출 (자동: {(defaultWarmPage ? "활성화" : "비활성화")})"
                : "Warm 페이지 산출";

            string[] options = { $"자동 ({(defaultWarmPage ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.warmPage < 0 ? 0 : config.warmPage + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "빌드 시 self-warming 페이지(ait-warm.html)를 함께 산출합니다. " +
                    "호스트가 숨김 WebView 로 열면 매니페스트 변경분을 미리 캐시에 적재합니다. " +
                    "페이지 캐시·Warm Manifest 실효값이 모두 OFF 이면 회색 비활성화됩니다(AND 게이팅). " +
                    "-1=자동(활성화), 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.warmPage = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.warmPage = -1;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawNativeAssetSourceSetting()
        {
            bool defaultNativeSource = AITDefaultSettings.GetDefaultNativeAssetSource();
            bool isModified = config.nativeAssetSource >= 0 && (config.nativeAssetSource == 1) != defaultNativeSource;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.nativeAssetSource < 0
                ? $"네이티브 에셋 소스 우선 (자동: {(defaultNativeSource ? "활성화" : "비활성화")})"
                : "네이티브 에셋 소스 우선";

            string[] options = { $"자동 ({(defaultNativeSource ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.nativeAssetSource < 0 ? 0 : config.nativeAssetSource + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "페이지 캐시 인터셉터가 Build/* 요청에 대해 호스트 네이티브 프리페치 결과를 우선 사용합니다. " +
                    "호스트가 window.__aitResolveAsset 리졸버를 주입하면 native→CacheStorage→network 순으로 해석합니다. " +
                    "리졸버 미주입 시 신호만 노출되고 기존 캐시-퍼스트 동작으로 자동 폴백됩니다. " +
                    "페이지 캐시 실효값이 OFF 이면 회색 비활성화됩니다(AND 게이팅). " +
                    "-1=자동(활성화), 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.nativeAssetSource = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.nativeAssetSource = -1;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawTextureStreamingSettings()
        {
            showTextureStreamingSettings = EditorGUILayout.Foldout(showTextureStreamingSettings, "콘텐츠 최적화 — 텍스처 스트리밍", true);

            if (!showTextureStreamingSettings) return;

            EditorGUILayout.BeginVertical("box");

            // tri-state 드롭다운
            DrawTextureStreamingSetting();

            GUILayout.Space(5);

            // 스텁 노출 구간 설명 HelpBox
            EditorGUILayout.HelpBox(
                "스텁 노출 구간: 첫 프레임 직후 ~ 비동기 로드 완료 사이에 단색 스텁이 잠깐 보일 수 있습니다.\n" +
                "항상 원본 텍스처가 보여야 하는 경로는 '제외 폴더'에 지정하세요.\n\n" +
                "외부화 대상: 부팅 씬에 의존하지 않는 대형(기본 512KB 이상) Texture2D.\n" +
                "자동 제외: 부팅 씬 의존 / Resources / SpriteAtlas 패킹 대상 / 동명·동차원 충돌 / linear / NormalMap.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 최소 바이트 설정
            config.textureStreamingMinBytes = EditorGUILayout.IntField(
                new GUIContent("외부화 최소 크기 (바이트)", "이 크기보다 큰 텍스처 소스만 외부화합니다 (기본 524288 = 512KB)."),
                config.textureStreamingMinBytes
            );

            // 대상 폴더
            config.textureStreamingDirs = EditorGUILayout.TextField(
                new GUIContent("대상 폴더 (쉼표 구분)", "비우면 프로젝트 전체. 예) Assets/Art/BG,Assets/Textures"),
                config.textureStreamingDirs
            );

            // 제외 폴더
            config.textureStreamingExcludeDirs = EditorGUILayout.TextField(
                new GUIContent("제외 폴더 (쉼표 구분)", "항상 원본 텍스처를 사용해야 하는 경로. 예) Assets/UI/Always"),
                config.textureStreamingExcludeDirs
            );

            // 최대 동시 스트리밍
            config.textureStreamingMaxConcurrent = EditorGUILayout.IntSlider(
                new GUIContent("최대 동시 스트리밍", "런타임 동시 다운로드/디코드 상한 (기본 3). VRAM/메인스레드 hitch 제한."),
                config.textureStreamingMaxConcurrent, 1, 8
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawTextureStreamingSetting()
        {
            bool defaultEnabled = AITDefaultSettings.GetDefaultTextureStreaming();
            bool isModified = config.textureStreaming >= 0 && (config.textureStreaming == 1) != defaultEnabled;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.textureStreaming < 0
                ? $"텍스처 스트리밍 (자동: {(defaultEnabled ? "활성화" : "비활성화")})"
                : "텍스처 스트리밍";

            string[] options = { $"자동 ({(defaultEnabled ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.textureStreaming < 0 ? 0 : config.textureStreaming + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.textureStreaming = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.textureStreaming = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPermissionSettings()
        {
            EditorGUILayout.LabelField("권한 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.HelpBox(
                "앱에서 사용할 권한을 선택하세요.\n" +
                "선택한 권한은 granite.config.ts의 permissions에 반영됩니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 권한이 null인 경우 초기화
            if (config.permissionConfig == null)
            {
                config.permissionConfig = new AITPermissionConfig();
            }

            // Clipboard 섹션
            EditorGUILayout.LabelField("Clipboard", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            config.permissionConfig.clipboardRead = EditorGUILayout.Toggle(
                new GUIContent("읽기 (Read)", "클립보드 내용 읽기 권한"),
                config.permissionConfig.clipboardRead
            );
            config.permissionConfig.clipboardWrite = EditorGUILayout.Toggle(
                new GUIContent("쓰기 (Write)", "클립보드에 내용 쓰기 권한"),
                config.permissionConfig.clipboardWrite
            );
            EditorGUI.indentLevel--;

            GUILayout.Space(5);

            // Contacts 섹션
            EditorGUILayout.LabelField("Contacts", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            config.permissionConfig.contacts = EditorGUILayout.Toggle(
                new GUIContent("읽기 (Read)", "연락처 읽기 권한 (read만 지원)"),
                config.permissionConfig.contacts
            );
            EditorGUI.indentLevel--;

            GUILayout.Space(5);

            // Photos 섹션
            EditorGUILayout.LabelField("Photos", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            config.permissionConfig.photos = EditorGUILayout.Toggle(
                new GUIContent("읽기 (Read)", "사진 앨범 읽기 권한 (read만 지원)"),
                config.permissionConfig.photos
            );
            EditorGUI.indentLevel--;

            GUILayout.Space(5);

            // Camera 섹션
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            config.permissionConfig.camera = EditorGUILayout.Toggle(
                new GUIContent("접근 (Access)", "카메라 접근 권한 (access만 지원)"),
                config.permissionConfig.camera
            );
            EditorGUI.indentLevel--;

            GUILayout.Space(5);

            // Geolocation 섹션
            EditorGUILayout.LabelField("Geolocation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            config.permissionConfig.geolocation = EditorGUILayout.Toggle(
                new GUIContent("접근 (Access)", "위치 정보 접근 권한 (access만 지원)"),
                config.permissionConfig.geolocation
            );
            EditorGUI.indentLevel--;

            GUILayout.Space(10);

            // 현재 권한 설정 미리보기
            string permissionsJson = config.GetPermissionsJson();
            if (permissionsJson != "[]")
            {
                EditorGUILayout.LabelField("적용될 권한:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(permissionsJson, MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("선택된 권한이 없습니다.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
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

            // IL2CPP 컴파일러 설정
            DrawIl2CppConfigurationSetting();

            // WebGL 코드 최적화 (Disk Size with LTO) — Meta 로드타임 스택의 실제 LTO 레버
            // (전 버전 reflection 적용이라 #if 가드 없음. API 부재 버전은 fail-safe)
            DrawWebGLCodeOptimizationSetting();

#if UNITY_6000_0_OR_NEWER
            // IL2CPP Code Generation (OptimizeSize) — Meta 로드타임 스택
            DrawIl2CppCodeGenerationSetting();
#endif

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

#if UNITY_6000_0_OR_NEWER
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
#endif

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
                ? $"예외 처리 모드 (자동: {defaultValue})"
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

        private void DrawFirstInteractiveLogSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultFirstInteractiveLog();
            bool isModified = config.firstInteractiveLog >= 0 && (config.firstInteractiveLog == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.firstInteractiveLog < 0
                ? $"first-interactive 계측 (자동: {autoLabel})"
                : "first-interactive 계측";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.firstInteractiveLog < 0 ? 0 : config.firstInteractiveLog + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "원래 첫 씬 로드 완료 시점(time-to-original-scene)을 호스트에 전송합니다. " +
                    "픽셀 불변·세션당 1회 단일 이벤트이므로 기본 활성화됩니다."),
                currentIndex, options);
            config.firstInteractiveLog = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.firstInteractiveLog = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private int CountModifiedWebGLSettings()
        {
            int count = 0;

            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            if (config.memorySize > 0 && config.memorySize != defaultMemory) count++;

            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            if (config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads) count++;

            bool defaultCaching = AITDefaultSettings.GetDefaultDataCaching();
            if (config.dataCaching >= 0 && (config.dataCaching == 1) != defaultCaching) count++;

            bool defaultFirstInteractive = AITDefaultSettings.GetDefaultFirstInteractiveLog();
            if (config.firstInteractiveLog >= 0 && (config.firstInteractiveLog == 1) != defaultFirstInteractive) count++;

            // 파일명 해싱: 선언 기본 true. false 로 바뀐 경우만 변경으로 집계(reset 버튼 노출 조건에 포함).
            if (!config.nameFilesAsHashes) count++;
            // 페이지 캐시: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultPageCache = AITDefaultSettings.GetDefaultPageCache();
            if (config.pageCache >= 0 && (config.pageCache == 1) != defaultPageCache) count++;

            // warm manifest: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultWarmManifest = AITDefaultSettings.GetDefaultWarmManifest();
            if (config.warmManifest >= 0 && (config.warmManifest == 1) != defaultWarmManifest) count++;

            // warm 페이지: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultWarmPage = AITDefaultSettings.GetDefaultWarmPage();
            if (config.warmPage >= 0 && (config.warmPage == 1) != defaultWarmPage) count++;

            // 네이티브 에셋 소스: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultNativeSource = AITDefaultSettings.GetDefaultNativeAssetSource();
            if (config.nativeAssetSource >= 0 && (config.nativeAssetSource == 1) != defaultNativeSource) count++;

            bool defaultStreaming = AITDefaultSettings.GetDefaultAudioStreaming();
            if (config.audioStreaming >= 0 && (config.audioStreaming == 1) != defaultStreaming) count++;

            bool defaultCrunch = AITDefaultSettings.GetDefaultTextureCrunch();
            if (config.textureCrunch >= 0 && (config.textureCrunch == 1) != defaultCrunch) count++;

            bool defaultTextureClamp = AITDefaultSettings.GetDefaultTextureSizeClamp();
            if (config.textureSizeClamp >= 0 && (config.textureSizeClamp == 1) != defaultTextureClamp) count++;
            bool defaultAstcBlock = AITDefaultSettings.GetDefaultAstcBlockEscalation();
            if (config.astcBlockEscalation >= 0 && (config.astcBlockEscalation == 1) != defaultAstcBlock) count++;
            // 폰트 subset: 비활성(0)이거나 수동 override(target/range 지정)면 변경으로 집계.
            if (config.fontSubset == 0
                || !string.IsNullOrEmpty(config.fontSubsetTargetPaths)
                || !string.IsNullOrEmpty(config.fontSubsetUnicodeRanges))
            {
                count++;
            }

            return count;
        }

        private void DrawAstcBlockSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultAstcBlockEscalation();
            bool isModified = config.astcBlockEscalation >= 0 && (config.astcBlockEscalation == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — ASTC 블록 에스컬레이션", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.astcBlockEscalation < 0
                ? $"ASTC 블록 에스컬레이션 (자동: {autoLabel})"
                : "ASTC 블록 에스컬레이션";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.astcBlockEscalation < 0 ? 0 : config.astcBlockEscalation + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "ASTC 서브타겟 전용: 텍스처를 더 큰 ASTC 블록으로 reimport 하여 .data 크기를 줄입니다. " +
                    "lossy. 빌드 후 원본 임포트 설정으로 복원합니다."),
                currentIndex,
                options
            );
            config.astcBlockEscalation = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.astcBlockEscalation = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.astcBlockEscalation >= 0 ? config.astcBlockEscalation == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;

                // 블록 크기 팝업
                int[] blockSizes = { 4, 5, 6, 8, 10, 12 };
                string[] blockSizeLabels = { "4x4 (최고화질, 최대용량)", "5x5", "6x6", "8x8", "10x10", "12x12 (최소용량, 최저화질)" };
                int currentBlockIndex = Array.IndexOf(blockSizes, config.astcBlockSize);
                if (currentBlockIndex < 0) currentBlockIndex = 5; // 기본값 12x12
                int newBlockIndex = EditorGUILayout.IntPopup(
                    "블록 크기",
                    currentBlockIndex,
                    blockSizeLabels,
                    new int[] { 0, 1, 2, 3, 4, 5 }
                );
                config.astcBlockSize = blockSizes[newBlockIndex];

                // maxTextureSize 캡
                config.astcBlockMaxSize = EditorGUILayout.IntField(
                    new GUIContent("maxTextureSize 캡", "0=캡 안 함(원본 크기 유지). 양수 입력 시 해당 크기로 제한."),
                    config.astcBlockMaxSize
                );

                // SpriteAtlas 포함
                config.astcBlockAtlas = EditorGUILayout.Toggle(
                    new GUIContent("SpriteAtlas 포함", "SpriteAtlas 의 WebGL 플랫폼 설정도 오버라이드하고 repack합니다."),
                    config.astcBlockAtlas
                );

                // 대상 폴더
                config.astcBlockDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더 (쉼표 구분)", "비우면 Assets 전체. 예: Assets/Textures,Assets/UI"),
                    config.astcBlockDirs
                );

                // 제외 폴더
                config.astcBlockExcludeDirs = EditorGUILayout.TextField(
                    new GUIContent("제외 폴더 (쉼표 구분)", "폰트/SDF/TextMeshPro 는 항상 자동 제외됩니다."),
                    config.astcBlockExcludeDirs
                );

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox(
                "⚠ lossy: 원본보다 화질이 낮아집니다.\n" +
                "ASTC 서브타겟 전용 — DXT(기본) 서브타겟 프로젝트에서는 빌드 시 자동 skip됩니다.\n" +
                "빌드 완료 후 원본 임포트 설정이 자동으로 복원됩니다(비파괴).",
                MessageType.Warning
            );
        }

        private void ResetWebGLSettings()
        {
            ResetWebGLOptimizationDefaults(config);
        }

        /// <summary>
        /// 모든 WebGL 최적화 레버를 AITEditorScriptObject 의 선언 기본값으로 되돌린다.
        /// 각 레버는 master 토글뿐 아니라 수치 파라미터·스코프 디렉터리/경로까지 전부 복원해야
        /// "기본값 복원"이 실제로 fresh 설정과 동일해진다(stale 스코프/임계값이 남으면 재활성 시 의도와 다르게 동작).
        /// 신규 레버 필드를 추가하면 이 메서드에도 반드시 그 기본값을 추가할 것
        /// (AITResetWebGLSettingsTests 가 fresh 인스턴스와 비교해 누락을 검출한다).
        /// </summary>
        internal static void ResetWebGLOptimizationDefaults(AITEditorScriptObject config)
        {
            if (config == null) return;

            // 엔진 / 전송
            config.memorySize = -1;
            config.threadsSupport = -1;
            config.dataCaching = -1;
            config.nameFilesAsHashes = true; // [Header "WebGL 최적화 설정"] 파일명 해싱 토글 (선언 기본 true)
            config.firstInteractiveLog = -1;

            // 페이지 캐시 / warm / 네이티브 프리페치
            config.pageCache = -1;
            config.pageCacheName = "";
            config.warmManifest = -1;
            config.warmPage = -1;
            config.nativeAssetSource = -1;

            // 콘텐츠 최적화 — 오디오 스트리밍
            config.audioStreaming = -1;
            config.audioStreamingMinBytes = 262144;
            config.audioStreamingDirs = "";

            // 콘텐츠 최적화 — 텍스처 crunch
            config.textureCrunch = -1;
            config.textureCrunchMaxSize = 0;
            config.textureCrunchQuality = 50;
            config.textureCrunchAtlas = true;
            config.textureCrunchAtlasMaxSize = 0;
            config.textureCrunchDirs = "";

            // 콘텐츠 최적화 — 텍스처 크기 클램프
            config.textureSizeClamp = -1;
            config.textureClampMaxSize = 1024;
            config.textureClampMinBytes = 0;
            config.textureClampDirs = "";
            config.textureClampExcludeDirs = "";

            // 콘텐츠 최적화 — ASTC 블록 에스컬레이션
            config.astcBlockEscalation = -1;
            config.astcBlockSize = 12;
            config.astcBlockMaxSize = 0;
            config.astcBlockAtlas = true;
            config.astcBlockDirs = "";
            config.astcBlockExcludeDirs = "";

            // 콘텐츠 최적화 — 폰트 CJK subset
            config.fontSubset = -1;
            config.fontSubsetTargetPaths = string.Empty;
            config.fontSubsetUnicodeRanges = string.Empty;

            // 콘텐츠 최적화 — 대형 텍스처 스트리밍
            config.textureStreaming = -1;
            config.textureStreamingMinBytes = 524288;
            config.textureStreamingDirs = "";
            config.textureStreamingExcludeDirs = "";
            config.textureStreamingMaxConcurrent = 3;

            // 콘텐츠 최적화 — 대형 폰트 deferral
            config.fontStreaming = -1;
            config.fontStreamingTargetPaths = string.Empty;
            config.fontStreamingMaxConcurrent = 2;
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

        private void DrawDeploymentSettings()
        {
            EditorGUILayout.LabelField("배포 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // AITCredentials에서 배포 키 로드
            var credentials = AITCredentialsUtil.GetCredentials();
            if (credentials != null)
            {
                EditorGUI.BeginChangeCheck();
                credentials.deploymentKey = EditorGUILayout.PasswordField("배포 키 (API Key)", credentials.deploymentKey);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(credentials);
                }

                if (string.IsNullOrWhiteSpace(credentials.deploymentKey))
                {
                    EditorGUILayout.HelpBox(
                        "배포 키를 입력해주세요. 배포 시 필수입니다.",
                        MessageType.Warning
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "배포 키가 설정되었습니다.\n" +
                        "※ 이 키는 AITCredentials.asset에 별도 저장되며, .gitignore로 보호됩니다.",
                        MessageType.Info
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "AITCredentials 파일을 로드할 수 없습니다.",
                    MessageType.Error
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProjectInfo()
        {
            EditorGUILayout.LabelField("프로젝트 정보", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("프로젝트 이름:", PlayerSettings.productName);
            EditorGUILayout.LabelField("Unity 버전:", $"{Application.unityVersion} ({AITDefaultSettings.GetUnityVersionGroup()})");
            EditorGUILayout.LabelField("SDK 버전:", AITVersion.FullVersion);

            // 에러 트래커 설정 (DSN이 설정된 경우에만 표시)
            if (ErrorTracker.AITEditorErrorTracker.IsDsnConfigured)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("에러 트래커", EditorStyles.boldLabel);

                bool isTrackerEnabled = ErrorTracker.AITErrorTrackerConsent.IsEnabled();
                string statusText = isTrackerEnabled
                    ? "활성 (익명 에러 리포트 전송 중)"
                    : "비활성";
                EditorGUILayout.LabelField("상태:", statusText);

                EditorGUILayout.BeginHorizontal();
                if (!isTrackerEnabled)
                {
                    if (GUILayout.Button("에러 리포트 활성화"))
                    {
                        ErrorTracker.AITErrorTrackerConsent.SetEnabled(true);
                    }
                }
                else
                {
                    if (GUILayout.Button("에러 리포트 비활성화"))
                    {
                        ErrorTracker.AITErrorTrackerConsent.SetEnabled(false);
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("에러 수집은 즉시 중단/재개됩니다. 세션 추적은 도메인 리로드 후 적용됩니다.", MessageType.Info);
            }

            GUILayout.Space(10);

            // 적용될 WebGL 설정 요약
            EditorGUILayout.LabelField("적용될 WebGL 설정 (Production 프로필)", EditorStyles.boldLabel);

            int effectiveMemory = config.memorySize > 0 ? config.memorySize : AITDefaultSettings.GetDefaultMemorySize();
            EditorGUILayout.LabelField($"  메모리: {effectiveMemory}MB");

            WebGLCompressionFormat effectiveCompression = config.productionProfile.compressionFormat switch
            {
                0 => WebGLCompressionFormat.Disabled,
                1 => WebGLCompressionFormat.Gzip,
                2 => WebGLCompressionFormat.Brotli,
                _ => AITDefaultSettings.GetDefaultCompressionFormat()
            };
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

            // 설정 검증 상태 요약 (appName만 필수)
            bool readyForBuild = config.IsAppNameValid();
            bool hasDeploymentKey = !string.IsNullOrWhiteSpace(AITCredentialsUtil.GetDeploymentKey());

            if (readyForBuild)
            {
                EditorGUILayout.HelpBox("빌드 준비 완료", MessageType.Info);

                if (hasDeploymentKey)
                {
                    EditorGUILayout.HelpBox("배포 준비 완료", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("배포 키를 입력해주세요", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void SaveSettings()
        {
            if (config == null) return;

            EditorUtility.SetDirty(config);
            // SaveAssets는 디스크 flush를 Unity 내부 스케줄링에 맡기므로, 도메인 리로드나
            // Editor 강제 종료가 발생하면 변경분이 유실될 수 있다. SaveAssetIfDirty는 해당
            // 에셋만 동기적으로 기록해 유실을 방지한다 (Unity 2020.1+).
            AssetDatabase.SaveAssetIfDirty(config);
        }
    }
}
