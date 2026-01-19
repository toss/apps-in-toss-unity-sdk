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
            DrawAppInfo();
            GUILayout.Space(10);
            DrawBrandSettings();
            GUILayout.Space(10);
            DrawLoadingScreenSettings();
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

        private void DrawLoadingScreenSettings()
        {
            EditorGUILayout.LabelField("로딩 화면 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            config.loadingTitle = EditorGUILayout.TextField("로딩 제목", config.loadingTitle);
            EditorGUILayout.HelpBox(
                "줄바꿈은 \\n을 사용합니다. 예: 게임을 불러오고 있어요\\n조금만 기다려주세요",
                MessageType.Info
            );

            GUILayout.Space(10);

            // 커스텀 로딩 화면 상태 표시
            string customLoadingPath = Path.Combine(Application.dataPath, "AppsInToss", "loading.html");
            bool hasCustomLoading = File.Exists(customLoadingPath);

            if (hasCustomLoading)
            {
                EditorGUILayout.HelpBox(
                    "커스텀 로딩 화면이 감지되었습니다.\n" + customLoadingPath,
                    MessageType.Info
                );

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("파일 열기", GUILayout.Width(100)))
                {
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(customLoadingPath, 1);
                }
                if (GUILayout.Button("파일 삭제", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog(
                        "커스텀 로딩 화면 삭제",
                        "커스텀 로딩 화면을 삭제하시겠습니까?\n기본 TDS 스타일 로딩 화면이 사용됩니다.",
                        "삭제", "취소"))
                    {
                        File.Delete(customLoadingPath);
                        string metaPath = customLoadingPath + ".meta";
                        if (File.Exists(metaPath)) File.Delete(metaPath);
                        AssetDatabase.Refresh();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "기본 TDS 스타일 로딩 화면이 사용됩니다.\n" +
                    "커스텀 로딩 화면을 사용하려면 아래 버튼을 클릭하세요.",
                    MessageType.Info
                );

                if (GUILayout.Button("커스텀 로딩 화면 생성"))
                {
                    CreateCustomLoadingScreen(customLoadingPath);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateCustomLoadingScreen(string path)
        {
            // 디렉토리 생성
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 기본 로딩 화면 템플릿 생성 (TDS 스타일 기반)
            string template = @"<!--
    커스텀 로딩 화면 템플릿

    이 파일을 수정하여 로딩 화면을 커스터마이징하세요.
    CSS 변수를 수정하면 쉽게 색상과 크기를 변경할 수 있습니다.

    사용 가능한 API:
    - AITLoading.appInfo: 앱 정보 (iconUrl, displayName, primaryColor, loadingTitle)
    - AITLoading.onProgress(callback): 진행률 업데이트 콜백 (0.0 ~ 1.0)
    - AITLoading.onComplete(callback): 로딩 완료 콜백
    - AITLoading.onError(callback): 에러 발생 콜백
    - AITLoading.hide(): 로딩 화면 숨기기

    자세한 내용은 Docs/LoadingScreenCustomization.md를 참조하세요.
-->

<style>
    /* ===== 커스터마이징 가능한 CSS 변수 ===== */
    :root {
        /* 배경 및 색상 */
        --loading-bg: #ffffff;
        --title-color: #191f28;
        --app-name-color: #333d4b;
        --progress-bg: #e5e8eb;
        --card-border: #e5e8eb;
        --icon-bg: rgba(2, 32, 71, 0.05);

        /* 크기 */
        --title-size: 22px;
        --app-name-size: 15px;
        --icon-size: 30px;
        --progress-height: 5px;
        --card-radius: 16px;
        --icon-radius: 8px;

        /* 여백 */
        --top-padding: 120px;
        --side-padding: 20px;
    }

    /* ===== 로딩 화면 스타일 ===== */
    .loading-container {
        position: fixed;
        inset: 0;
        background: var(--loading-bg);
        display: flex;
        flex-direction: column;
        padding: var(--top-padding) var(--side-padding) 0;
        font-family: -apple-system, BlinkMacSystemFont, sans-serif;
    }

    .loading-title {
        font-size: var(--title-size);
        font-weight: 600;
        color: var(--title-color);
        line-height: 1.4;
        margin-bottom: 44px;
    }

    .loading-card {
        display: flex;
        flex-direction: column;
        padding: 16px;
        background: var(--loading-bg);
        border: 1px solid var(--card-border);
        border-radius: var(--card-radius);
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
    }

    .loading-header {
        display: flex;
        align-items: center;
        margin-bottom: 12px;
    }

    .loading-icon {
        width: var(--icon-size);
        height: var(--icon-size);
        border-radius: var(--icon-radius);
        background: var(--icon-bg);
        overflow: hidden;
        flex-shrink: 0;
    }

    .loading-icon img {
        width: 100%;
        height: 100%;
        object-fit: cover;
    }

    .loading-app-name {
        margin-left: 12px;
        font-size: var(--app-name-size);
        font-weight: 500;
        color: var(--app-name-color);
    }

    .loading-progress {
        height: var(--progress-height);
        background: var(--progress-bg);
        border-radius: calc(var(--progress-height) / 2);
        overflow: hidden;
    }

    .loading-progress-bar {
        height: 100%;
        width: 0%;
        border-radius: inherit;
        transition: width 0.3s ease;
    }
</style>

<!-- ===== HTML 구조 ===== -->
<div class=""loading-container"" id=""ait-loading"">
    <div class=""loading-title"" id=""loading-title""></div>

    <div class=""loading-card"">
        <div class=""loading-header"">
            <div class=""loading-icon"">
                <img id=""app-icon"" src="""" alt="""" />
            </div>
            <div class=""loading-app-name"" id=""app-name""></div>
        </div>
        <div class=""loading-progress"">
            <div class=""loading-progress-bar"" id=""progress-bar""></div>
        </div>
    </div>
</div>

<!-- ===== JavaScript ===== -->
<script>
(function() {
    // 앱 정보로 UI 초기화
    function initUI(appInfo) {
        document.getElementById('loading-title').innerHTML =
            (appInfo.loadingTitle || '').replace(/\n/g, '<br>');
        document.getElementById('app-icon').src = appInfo.iconUrl || '';
        document.getElementById('app-name').textContent = appInfo.displayName || '';
        document.getElementById('progress-bar').style.background =
            appInfo.primaryColor || '#3182f6';
    }

    // 진행률 업데이트
    AITLoading.onProgress(function(progress) {
        document.getElementById('progress-bar').style.width = (progress * 100) + '%';
    });

    // 로딩 완료 시 화면 숨기기
    AITLoading.onComplete(function() {
        AITLoading.hide();
    });

    // SDK 초기화 완료 시 UI 업데이트
    if (AITLoading._initialized) {
        initUI(AITLoading.appInfo);
    } else {
        var check = setInterval(function() {
            if (AITLoading._initialized) {
                clearInterval(check);
                initUI(AITLoading.appInfo);
            }
        }, 50);
    }
})();
</script>";

            File.WriteAllText(path, template, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "커스텀 로딩 화면 생성 완료",
                "커스텀 로딩 화면이 생성되었습니다.\n\n" +
                "경로: Assets/AppsInToss/loading.html\n\n" +
                "이 파일을 편집하여 로딩 화면을 커스터마이징하세요.\n" +
                "AITLoading.appInfo를 사용하여 앱 정보에 접근할 수 있습니다.",
                "확인"
            );

            // 파일 열기
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1);
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

            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            if (config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads) count++;

            bool defaultCaching = AITDefaultSettings.GetDefaultDataCaching();
            if (config.dataCaching >= 0 && (config.dataCaching == 1) != defaultCaching) count++;

            return count;
        }

        private void ResetWebGLSettings()
        {
            config.memorySize = -1;
            config.threadsSupport = -1;
            config.dataCaching = -1;
        }

        private void ResetAdvancedSettings()
        {
            config.stripEngineCode = true;
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
            EditorGUILayout.LabelField("SDK 버전:", "1.0.0");

            GUILayout.Space(10);

            // 적용될 WebGL 설정 요약
            EditorGUILayout.LabelField("적용될 WebGL 설정 (Production 프로필)", EditorStyles.boldLabel);

            int effectiveMemory = config.memorySize > 0 ? config.memorySize : AITDefaultSettings.GetDefaultMemorySize();
            EditorGUILayout.LabelField($"  메모리: {effectiveMemory}MB");

            WebGLCompressionFormat effectiveCompression = config.productionProfile.compressionFormat >= 0
                ? (WebGLCompressionFormat)config.productionProfile.compressionFormat
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
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
