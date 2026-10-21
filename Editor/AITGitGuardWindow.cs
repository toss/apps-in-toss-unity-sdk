using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AppsInToss.Editor
{
    /// <summary>
    /// GitGuard가 감지한 이슈들을 체크박스 목록으로 보여주고 일괄 해결하는 윈도우
    /// </summary>
    public class AITGitGuardWindow : EditorWindow
    {
        private enum ViewState { IssueList, Results }

        private ViewState viewState = ViewState.IssueList;
        private List<GitGuardIssue> issues;
        private List<GitGuardFixResult> results;
        private Vector2 scrollPos;

        public static void Show(List<GitGuardIssue> issues)
        {
            var window = GetWindow<AITGitGuardWindow>(true, "Apps in Toss SDK - Git 보호 설정", true);
            window.issues = issues;
            window.results = null;
            window.viewState = ViewState.IssueList;
            window.scrollPos = Vector2.zero;

            window.minSize = new Vector2(450, 200);
            float height = 120 + issues.Count * 60;
            foreach (var issue in issues)
            {
                if (issue.type == GitGuardIssueType.MissingGitignore && issue.missingPatterns != null)
                {
                    height += issue.missingPatterns.Count * 18;
                }
            }
            height = Mathf.Clamp(height, 200, 500);
            window.maxSize = new Vector2(600, 700);

            var pos = window.position;
            pos.width = 500;
            pos.height = height;
            window.position = pos;

            window.ShowUtility();
            window.CenterOnMainWin();
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
            EditorGUILayout.LabelField("다음 항목을 감지했습니다:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            foreach (var issue in issues)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                issue.isSelected = EditorGUILayout.ToggleLeft(issue.label, issue.isSelected, EditorStyles.boldLabel);

                if (issue.type == GitGuardIssueType.MissingGitignore && issue.missingPatterns != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (var pattern in issue.missingPatterns)
                    {
                        EditorGUILayout.LabelField($"• {pattern}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
                else if (!string.IsNullOrEmpty(issue.description))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(issue.description, EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // 선택된 항목이 없으면 비활성화
            bool anySelected = false;
            foreach (var issue in issues)
            {
                if (issue.isSelected) { anySelected = true; break; }
            }

            GUI.enabled = anySelected;
            if (GUILayout.Button("일괄 해결", GUILayout.Width(100), GUILayout.Height(28)))
            {
                EditorUtility.DisplayProgressBar("Git 보호 설정", "처리 중...", 0.5f);
                try
                {
                    results = AITGitGuard.ExecuteBatchFix(issues);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                viewState = ViewState.Results;

                // 결과 뷰에 맞게 윈도우 크기 조정
                float height = 120 + results.Count * 40;
                height = Mathf.Clamp(height, 200, 500);
                var pos = position;
                pos.height = height;
                position = pos;
                minSize = new Vector2(450, 200);
                maxSize = new Vector2(600, 700);
                CenterOnMainWin();
            }
            GUI.enabled = true;

            if (GUILayout.Button("나중에", GUILayout.Width(80), GUILayout.Height(28)))
            {
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
            EditorGUILayout.LabelField("처리 결과:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            foreach (var result in results)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                string icon = result.success ? "✅" : "❌";
                EditorGUILayout.LabelField($"{icon} {result.label}", EditorStyles.boldLabel);

                if (!result.success)
                {
                    EditorGUI.indentLevel++;
                    if (!string.IsNullOrEmpty(result.message))
                    {
                        EditorGUILayout.LabelField(result.message, EditorStyles.wordWrappedMiniLabel);
                    }
                    if (!string.IsNullOrEmpty(result.command))
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"→ {result.command}", EditorStyles.miniLabel);
                        if (GUILayout.Button("복사", GUILayout.Width(40)))
                        {
                            GUIUtility.systemCopyBuffer = result.command;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("확인", GUILayout.Width(80), GUILayout.Height(28)))
            {
                Close();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
        }
    }
}
