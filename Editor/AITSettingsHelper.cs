using AppsInToss.Editor;
using UnityEditor;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Settings Helper Interface for Apps in Toss
    /// </summary>
    public static class AITSettingsHelperInterface
    {
        public static IAITSettingsHelper helper = new AITSettingsHelper();
    }

    public interface IAITSettingsHelper
    {
        void OnFocus();
        void OnLostFocus();
        void OnDisable();
        void OnSettingsGUI(EditorWindow window);
        void OnBuildButtonGUI(EditorWindow window);
    }

    public class AITSettingsHelper : IAITSettingsHelper
    {
        private Vector2 scrollPosition;
        private AITEditorScriptObject config;

        public void OnFocus()
        {
            config = UnityUtil.GetEditorConf();
        }

        public void OnLostFocus()
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }

        public void OnDisable()
        {
            OnLostFocus();
        }

        public void OnSettingsGUI(EditorWindow window)
        {
            if (config == null)
                config = UnityUtil.GetEditorConf();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(10);
            GUILayout.Label("Apps in Toss 미니앱 설정", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // 앱 기본 정보
            GUILayout.Label("앱 기본 정보", EditorStyles.boldLabel);
            config.appName = EditorGUILayout.TextField("앱 이름", config.appName);
            config.version = EditorGUILayout.TextField("버전", config.version);
            config.description = EditorGUILayout.TextArea(config.description, GUILayout.Height(60));

            GUILayout.Space(10);

            // 빌드 설정 (빌드 프로필 UI는 AITConfigurationWindow에서 관리)
            GUILayout.Label("빌드 프로필", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("빌드 프로필 설정은 Apps in Toss > Configuration 메뉴에서 확인하세요.", MessageType.Info);

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }

        public void OnBuildButtonGUI(EditorWindow window)
        {
            GUILayout.Space(20);
            GUILayout.Label("빌드", EditorStyles.boldLabel);

            if (GUILayout.Button("미니앱으로 변환", GUILayout.Height(40)))
            {
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: true);
                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    AITPlatformHelper.ShowInfoDialog("성공", "Apps in Toss 미니앱 변환이 완료되었습니다!", "확인");
                }
                else
                {
                    // 빌드 실패의 단일 구조화 Sentry 이벤트(errorCode 기반 fingerprint). 메뉴 경로
                    // (ShowBuildFailedDialog)와 동일하게 Configuration Window 경로도 여기서 캡처해야
                    // runner 레벨 raw LogError(SDK-R5/10Q/VH)를 sentryCapture:false로 위임해도
                    // 캡처 공백이 생기지 않는다.
                    AppsInToss.Editor.ErrorTracker.AITEditorErrorTracker.CaptureBuildError(
                        result, $"AIT: 빌드 실패: {result}", "Configuration Window");
                    AITPlatformHelper.ShowInfoDialog("실패", $"변환 중 오류가 발생했습니다: {result}", "확인");
                }
            }

            if (GUILayout.Button("WebGL 빌드만 실행"))
            {
                var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: false);
                if (result == AITConvertCore.AITExportError.SUCCEED)
                {
                    AITPlatformHelper.ShowInfoDialog("성공", "WebGL 빌드가 완료되었습니다!", "확인");
                }
                else
                {
                    // 위와 동일 — Configuration Window WebGL 빌드 실패도 단일 구조화 이벤트로 캡처.
                    AppsInToss.Editor.ErrorTracker.AITEditorErrorTracker.CaptureBuildError(
                        result, $"AIT: 빌드 실패: {result}", "Configuration Window (WebGL)");
                    AITPlatformHelper.ShowInfoDialog("실패", $"빌드 중 오류가 발생했습니다: {result}", "확인");
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("설정 초기화"))
            {
                if (AITPlatformHelper.ShowConfirmDialog("설정 초기화", "모든 설정을 초기화하시겠습니까?", "예", "아니오", autoApprove: true))
                {
                    string configPath = "Assets/AppsInToss/Editor/AITConfig.asset";
                    AssetDatabase.DeleteAsset(configPath);
                    config = UnityUtil.GetEditorConf();
                }
            }
        }
    }
}
