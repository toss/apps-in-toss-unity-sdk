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

            EditorGUILayout.HelpBox(
                "Compression Format은 자동으로 Disabled로 설정됩니다 (Apps in Toss 권장)",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
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
            EditorGUILayout.LabelField("Unity 버전:", Application.unityVersion);
            EditorGUILayout.LabelField("SDK 버전:", "1.0.0");

            GUILayout.Space(5);

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
