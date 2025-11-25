using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Apps in Toss 로그 윈도우
    /// </summary>
    public class AITLogsWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string logs = "";

        public static void ShowWindow()
        {
            var window = GetWindow<AITLogsWindow>("AIT Logs");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            DrawHeader();
            GUILayout.Space(10);
            DrawLogs();
            GUILayout.Space(10);
            DrawActions();
        }

        private void DrawHeader()
        {
            GUILayout.Label("Apps in Toss Build Logs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "빌드 및 배포 작업의 상세 로그를 확인할 수 있습니다.",
                MessageType.Info
            );
        }

        private void DrawLogs()
        {
            EditorGUILayout.LabelField("빌드 히스토리", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 빌드 히스토리 표시
            var history = AITBuildHistory.LoadHistory();
            if (history.Count == 0)
            {
                EditorGUILayout.HelpBox("빌드 히스토리가 없습니다.", MessageType.Info);
            }
            else
            {
                foreach (var entry in history)
                {
                    DrawHistoryEntry(entry);
                    GUILayout.Space(5);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHistoryEntry(BuildHistoryEntry entry)
        {
            EditorGUILayout.BeginVertical("box");

            // 타임스탬프와 상태
            GUILayout.BeginHorizontal();
            GUILayout.Label(entry.timestamp, EditorStyles.boldLabel, GUILayout.Width(150));

            // 성공/실패 표시
            if (entry.success)
            {
                GUI.color = Color.green;
                GUILayout.Label("✓ 성공", GUILayout.Width(50));
            }
            else
            {
                GUI.color = Color.red;
                GUILayout.Label("✗ 실패", GUILayout.Width(50));
            }
            GUI.color = Color.white;

            GUILayout.Label($"[{entry.buildType}]", GUILayout.Width(80));
            GUILayout.Label($"{entry.buildTimeSeconds:F1}초", GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 버전 정보
            EditorGUILayout.LabelField("버전:", entry.appVersion);

            // 에러 메시지 (실패시)
            if (!entry.success && !string.IsNullOrEmpty(entry.errorMessage))
            {
                GUI.color = new Color(1f, 0.8f, 0.8f);
                EditorGUILayout.TextArea(entry.errorMessage, GUILayout.Height(40));
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            // 통계 표시
            var stats = AITBuildHistory.GetStatistics();
            if (stats.totalBuilds > 0)
            {
                GUILayout.Label($"총 빌드: {stats.totalBuilds}회 | 성공률: {stats.SuccessRate:F1}% | 평균 시간: {stats.averageBuildTime:F1}초",
                    EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            // 새로고침
            if (GUILayout.Button("새로고침", GUILayout.Width(80)))
            {
                Repaint();
            }

            // 히스토리 초기화
            if (GUILayout.Button("히스토리 초기화", GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("히스토리 초기화", "모든 빌드 히스토리를 삭제하시겠습니까?", "삭제", "취소"))
                {
                    AITBuildHistory.ClearHistory();
                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
