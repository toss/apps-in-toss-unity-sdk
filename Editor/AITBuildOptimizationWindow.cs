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
        private enum ViewState { IssueList, Results }
        private enum UserDecision { Pending, Proceed, Cancel }

        private ViewState viewState = ViewState.IssueList;
        private UserDecision decision = UserDecision.Pending;
        private List<OptimizationIssue> issues;
        private List<OptimizationFixResult> results;
        private AITEditorScriptObject editorConfig;
        private bool skipNextTime = false;
        private Vector2 scrollPos;

        /// <summary>
        /// 최적화 이슈 다이얼로그를 모달로 표시하고 결과를 반환
        /// </summary>
        /// <returns>true = 빌드 진행, false = 빌드 취소</returns>
        public static bool ShowAndWait(List<OptimizationIssue> issues, AITEditorScriptObject editorConfig)
        {
            var window = CreateInstance<AITBuildOptimizationWindow>();
            window.titleContent = new GUIContent("Apps in Toss - 빌드 최적화 제안");
            window.issues = issues;
            window.editorConfig = editorConfig;
            window.results = null;
            window.viewState = ViewState.IssueList;
            window.decision = UserDecision.Pending;
            window.skipNextTime = false;
            window.scrollPos = Vector2.zero;

            window.minSize = new Vector2(480, 200);
            window.maxSize = new Vector2(600, 700);

            float height = 160 + issues.Count * 70;
            height = Mathf.Clamp(height, 250, 500);

            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            float width = 520;
            var pos = new Rect(
                mainWindow.x + (mainWindow.width - width) / 2,
                mainWindow.y + (mainWindow.height - height) / 2,
                width,
                height);
            window.position = pos;

            window.ShowModal();

            // 모달이 닫힌 후 결정 반환
            return window.decision != UserDecision.Cancel;
        }

        private void OnGUI()
        {
            if (issues == null)
            {
                Close();
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (viewState == ViewState.IssueList)
            {
                DrawIssueList();
            }
            else
            {
                DrawResults();
            }

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
                    // 최적화 완료 항목은 비활성화 표시
                    GUI.enabled = false;
                    EditorGUILayout.ToggleLeft($"\u2705 {issue.label}", false, EditorStyles.boldLabel);
                    GUI.enabled = true;

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(issue.description, EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    // 이슈 항목은 체크박스로 선택 가능
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

            // 다음부터 건너뛰기 체크박스
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
                results = AITBuildOptimizationScanner.ApplyFixes(issues);
                viewState = ViewState.Results;

                // 결과 뷰에 맞게 크기 조정
                float height = 160 + results.Count * 50;
                height = Mathf.Clamp(height, 250, 500);
                var pos = position;
                pos.height = height;
                position = pos;
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

        private void DrawResults()
        {
            if (results == null) return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("최적화 적용 결과:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            foreach (var result in results)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                string icon = result.success ? "\u2705" : "\u274c";
                EditorGUILayout.LabelField($"{icon} {result.label}", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(result.message, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("빌드 진행", GUILayout.Width(100), GUILayout.Height(28)))
            {
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
                editorConfig.skipBuildOptimizationCheck = true;
                EditorUtility.SetDirty(editorConfig);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
