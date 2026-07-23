using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// 배포 성공 시 URL과 QR 코드를 표시하고 복사할 수 있는 창
    /// </summary>
    internal class DeploySuccessWindow : EditorWindow
    {
        private string deployUrl;
        private bool copied = false;
        private Texture2D qrTexture;
        private bool qrFailed = false;
        private GUIStyle urlStyle;
        private GUIStyle buttonStyle;
        private GUIStyle copiedLabelStyle;

        internal static void Show(string url)
        {
            var window = GetWindow<DeploySuccessWindow>(true, "배포 완료", true);
            window.deployUrl = url;
            window.copied = false;
            window.qrFailed = false;

            // 기존 QR 텍스처 정리
            if (window.qrTexture != null)
            {
                DestroyImmediate(window.qrTexture);
                window.qrTexture = null;
            }

            window.minSize = new Vector2(500, 400);
            window.maxSize = new Vector2(700, 500);
            window.ShowUtility();
            window.CenterOnMainWin();

            // QR 코드 로컬 생성 (외부 서비스 미사용)
            var qrTex = AppsInToss.Editor.AITQRCodeGenerator.Generate(url, 200);
            if (qrTex != null)
            {
                window.qrTexture = qrTex;
            }
            else
            {
                window.qrFailed = true;
                window.minSize = new Vector2(500, 160);
                window.maxSize = new Vector2(700, 200);
                window.CenterOnMainWin();
            }
            window.Repaint();
        }

        private void CenterOnMainWin()
        {
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var pos = position;
            pos.x = mainWindow.x + (mainWindow.width - pos.width) / 2;
            pos.y = mainWindow.y + (mainWindow.height - pos.height) / 2;
            position = pos;
        }

        private void OnGUI()
        {
            // 스타일 초기화
            if (urlStyle == null)
            {
                urlStyle = new GUIStyle(EditorStyles.textField)
                {
                    fontSize = 12,
                    wordWrap = false,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 8, 8)
                };
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 32
                };
            }

            if (copiedLabelStyle == null)
            {
                copiedLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.2f, 0.7f, 0.3f) }
                };
            }

            GUILayout.Space(15);

            // 성공 메시지
            EditorGUILayout.LabelField("Apps in Toss에 배포되었습니다!", EditorStyles.boldLabel);

            GUILayout.Space(10);

            // QR 코드 표시
            if (qrTexture != null)
            {
                EditorGUILayout.LabelField("모바일에서 QR 코드를 스캔하여 테스트하세요:", EditorStyles.miniLabel);
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(qrTexture, GUILayout.Width(200), GUILayout.Height(200));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            else if (!qrFailed)
            {
                EditorGUILayout.LabelField("QR 코드 로딩 중...", EditorStyles.miniLabel);
                GUILayout.Space(210);
            }

            // URL 표시
            EditorGUILayout.LabelField("배포 URL:", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(deployUrl, urlStyle, GUILayout.Height(30));

            GUILayout.Space(10);

            // 버튼 영역
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("URL 복사", buttonStyle, GUILayout.Width(120)))
            {
                EditorGUIUtility.systemCopyBuffer = deployUrl;
                copied = true;
                Debug.Log($"AIT: 배포 URL이 클립보드에 복사되었습니다: {deployUrl}");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(80)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 복사 완료 표시
            if (copied)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("✓ 클립보드에 복사되었습니다", copiedLabelStyle);
            }

            GUILayout.Space(10);
        }

        private void OnDestroy()
        {
            if (qrTexture != null)
            {
                DestroyImmediate(qrTexture);
                qrTexture = null;
            }
        }
    }
}
