using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 전 에셋 최적화 제안 다이얼로그
    /// </summary>
    public class AITBuildOptimizationWindow : EditorWindow
    {
        private enum UserDecision { Pending, Proceed, FixThenProceed, Cancel }

        private UserDecision decision = UserDecision.Pending;
        private List<OptimizationIssue> issues;
        private AITEditorScriptObject editorConfig;
        private bool skipNextTime = false;
        private Vector2 scrollPos;

        /// <summary>
        /// 최적화 이슈 다이얼로그를 표시하고 결과를 반환
        /// </summary>
        /// <returns>true = 빌드 진행, false = 빌드 취소</returns>
        public static bool ShowAndWait(List<OptimizationIssue> issues, AITEditorScriptObject editorConfig)
        {
            // 1단계: 이슈 목록 모달 표시
            var window = CreateInstance<AITBuildOptimizationWindow>();
            window.titleContent = new GUIContent("Apps in Toss - 빌드 최적화 제안");
            window.issues = issues;
            window.editorConfig = editorConfig;
            window.decision = UserDecision.Pending;
            window.skipNextTime = false;
            window.scrollPos = Vector2.zero;

            ConfigureWindowSize(window, 160 + issues.Count * 70);
            window.ShowModal();

            // ShowModal()은 Close() 호출 시 반환됨
            var userDecision = window.decision;
            DestroyImmediate(window);

            if (userDecision == UserDecision.Cancel)
                return false;

            if (userDecision == UserDecision.Proceed)
                return true;

            // 2단계: 자동 수정 실행 (모달 밖에서 프로그레스바 표시 가능)
            var fixResults = AITBuildOptimizationScanner.ApplyFixes(issues);

            // 3단계: 결과 모달 표시
            return ShowResultsDialog(fixResults);
        }

        /// <summary>
        /// 자동 수정 결과를 별도 모달로 표시
        /// </summary>
        private static bool ShowResultsDialog(List<OptimizationFixResult> fixResults)
        {
            if (fixResults == null || fixResults.Count == 0)
                return true;

            var sb = new System.Text.StringBuilder();
            bool allSuccess = true;

            foreach (var result in fixResults)
            {
                string icon = result.success ? "\u2705" : "\u274c";
                sb.AppendLine($"{icon} {result.label}: {result.message}");
                if (!result.success) allSuccess = false;
            }

            if (allSuccess)
            {
                sb.AppendLine();
                sb.AppendLine("빌드를 진행하시겠습니까?");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("일부 수정에 실패했습니다. 빌드를 진행하시겠습니까?");
            }

            return EditorUtility.DisplayDialog(
                "Apps in Toss - 최적화 결과",
                sb.ToString(),
                "빌드 진행",
                "취소");
        }

        private static void ConfigureWindowSize(EditorWindow window, float contentHeight)
        {
            window.minSize = new Vector2(480, 200);
            window.maxSize = new Vector2(600, 700);

            float height = Mathf.Clamp(contentHeight, 250, 500);
            float width = 520;

            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                mainWindow.x + (mainWindow.width - width) / 2,
                mainWindow.y + (mainWindow.height - height) / 2,
                width,
                height);
        }

        private void OnGUI()
        {
            if (issues == null)
            {
                Close();
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawIssueList();
            EditorGUILayout.EndScrollView();
        }

        private void DrawIssueList()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("WebGL 빌드 전 에셋 최적화 검사 결과:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            foreach (var issue in issues)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (issue.status == OptimizationStatus.AlreadyOptimal)
                {
                    GUI.enabled = false;
                    EditorGUILayout.ToggleLeft($"\u2705 {issue.label}", false, EditorStyles.boldLabel);
                    GUI.enabled = true;

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(issue.description, EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    issue.isSelected = EditorGUILayout.ToggleLeft(
                        $"\u26a0 {issue.label} ({issue.assetPaths.Count}건)",
                        issue.isSelected,
                        EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(issue.description, EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(issue.recommendation))
                    {
                        EditorGUILayout.LabelField($"\u2192 {issue.recommendation}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            skipNextTime = EditorGUILayout.ToggleLeft("다음부터 이 검사 건너뛰기", skipNextTime);

            EditorGUILayout.Space(8);

            // 버튼 영역
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool anySelected = false;
            foreach (var issue in issues)
            {
                if (issue.isSelected && issue.status == OptimizationStatus.Issue)
                {
                    anySelected = true;
                    break;
                }
            }

            GUI.enabled = anySelected;
            if (GUILayout.Button("자동 수정", GUILayout.Width(100), GUILayout.Height(28)))
            {
                ApplySkipSetting();
                decision = UserDecision.FixThenProceed;
                Close();
            }
            GUI.enabled = true;

            if (GUILayout.Button("무시하고 빌드", GUILayout.Width(110), GUILayout.Height(28)))
            {
                ApplySkipSetting();
                decision = UserDecision.Proceed;
                Close();
            }

            if (GUILayout.Button("취소", GUILayout.Width(60), GUILayout.Height(28)))
            {
                decision = UserDecision.Cancel;
                Close();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
        }

        private void ApplySkipSetting()
        {
            if (skipNextTime && editorConfig != null)
            {
                editorConfig.enableBuildOptimizationCheck = false;
                EditorUtility.SetDirty(editorConfig);
                AssetDatabase.SaveAssets();
            }
        }

        private void OnDestroy()
        {
            // X 버튼으로 닫힌 경우 취소 처리
            if (decision == UserDecision.Pending)
            {
                decision = UserDecision.Cancel;
            }
        }
    }
}
