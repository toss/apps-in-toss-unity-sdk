using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Apps in Toss 설정 윈도우
    /// partial: AITConfigurationWindow.BuildProfiles.cs, AITConfigurationWindow.WebGLSettings.cs, AITConfigurationWindow.AdvancedSettings.cs
    /// </summary>
    public partial class AITConfigurationWindow : EditorWindow
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
            DrawDevtoolsSettings();
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

        private void DrawDevtoolsSettings()
        {
            EditorGUILayout.LabelField("Devtools 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (config.devtools == null)
            {
                config.devtools = new AITDevtoolsSettings();
            }

            config.devtools.enabled = EditorGUILayout.Toggle(
                new GUIContent("Devtools 모드 사용 (브라우저 Mock SDK)", "Dev Server에서 @apps-in-toss/devtools로 SDK API를 브라우저 로컬 Mock으로 동작시킵니다"),
                config.devtools.enabled
            );

            using (new EditorGUI.DisabledScope(!config.devtools.enabled))
            {
                config.devtools.panel = EditorGUILayout.Toggle(
                    new GUIContent("패널 표시", "Mock 상태를 제어하는 플로팅 패널을 브라우저에 표시합니다"),
                    config.devtools.panel
                );
            }

            config.devtools.mcp = EditorGUILayout.Toggle(
                new GUIContent("MCP 활성화", "AI 에이전트가 제어할 수 있는 로컬 MCP 엔드포인트를 엽니다"),
                config.devtools.mcp
            );

            EditorGUILayout.HelpBox(
                "Devtools가 켜져 있으면 Dev Server는 브라우저에서 Mock SDK로 동작합니다.\n" +
                "끄면 대부분의 SDK API가 예외를 던집니다(실기기/WebView 없이는 네이티브 브릿지가 없기 때문).\n" +
                "MCP는 AI 에이전트가 Mock 상태를 제어할 수 있는 로컬 엔드포인트를 엽니다.",
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

            WebGLCompressionFormat effectiveCompression = AITBuildInitializer.ConvertToCompressionFormat(config.productionProfile.compressionFormat);
            EditorGUILayout.LabelField($"  압축: {effectiveCompression}");

            bool effectiveThreads = config.threadsSupport >= 0
                ? config.threadsSupport == 1
                : AITDefaultSettings.GetDefaultThreadsSupport();
            EditorGUILayout.LabelField($"  스레딩: {(effectiveThreads ? "활성화" : "비활성화")}");

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
