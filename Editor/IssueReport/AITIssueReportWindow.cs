using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.IssueReport
{
    /// <summary>
    /// 이슈 제보 입력 폼을 제공하는 에디터 윈도우.
    /// 제목/내용/이메일을 수집해 <see cref="AITIssueReportService.SendAsync"/>를 호출합니다.
    /// 스크린샷 포함 토글은 Task 7a 보류 상태로, 비활성화되어 있습니다.
    /// </summary>
    internal sealed class AITIssueReportWindow : EditorWindow
    {
        // Done 은 콜백이 돌아온 직후의 과도 상태. 성공/실패 다이얼로그 표시와
        // Close()/재귀 Submit() 사이의 짧은 구간에서만 유지되므로 OnGUI 가 관찰할 일은 거의 없다.
        private enum State { Idle, Sending, Done }

        private AITIssueReportContext _context;
        private string _linkedEventId;
        private string _title;
        private string _body;
        private string _email;
        private bool _includeScreenshot;
        private State _state;
        private Vector2 _bodyScroll;

        /// <summary>
        /// 이슈 제보 윈도우를 엽니다. 이미 열려 있다면 해당 윈도우에 포커스합니다.
        /// </summary>
        /// <param name="context">제보 트리거 경로 (Manual / BuildFailure).</param>
        /// <param name="linkedEventId">기존 에러 이벤트에 연결할 경우의 event id.</param>
        /// <param name="prefilledTitle">제목 필드에 미리 채울 값.</param>
        public static void Open(
            AITIssueReportContext context,
            string linkedEventId = null,
            string prefilledTitle = null)
        {
            var window = GetWindow<AITIssueReportWindow>(true, "이슈 제보하기", true);
            window.minSize = new Vector2(480, 420);
            window._context = context;
            window._linkedEventId = linkedEventId;
            window._title = prefilledTitle ?? string.Empty;
            window._body = string.Empty;
            window._email = string.Empty;
            window._includeScreenshot = false;
            window._state = State.Idle;
            window._bodyScroll = Vector2.zero;
            window.Focus();
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(_state == State.Sending))
            {
                EditorGUILayout.LabelField("제목", EditorStyles.boldLabel);
                _title = EditorGUILayout.TextField(_title);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("내용", EditorStyles.boldLabel);
                _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll, GUILayout.MinHeight(160));
                _body = EditorGUILayout.TextArea(_body, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("이메일 (선택)", EditorStyles.boldLabel);
                _email = EditorGUILayout.TextField(_email);

                using (new EditorGUI.DisabledScope(true))
                {
                    _includeScreenshot = EditorGUILayout.ToggleLeft(
                        "스크린샷 포함 (준비 중)", _includeScreenshot);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "전송 시 환경 정보와 최근 콘솔 로그가 Toss 개발팀에 전달됩니다.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_state == State.Sending)
            {
                GUILayout.Label("전송 중...", EditorStyles.miniLabel);
            }
            else
            {
                if (GUILayout.Button("취소", GUILayout.Width(80)))
                {
                    Close();
                    return;
                }

                bool canSend = _state == State.Idle
                    && !string.IsNullOrWhiteSpace(_title)
                    && !string.IsNullOrWhiteSpace(_body);

                using (new EditorGUI.DisabledScope(!canSend))
                {
                    if (GUILayout.Button("전송", GUILayout.Width(80)))
                    {
                        Submit();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Submit()
        {
            _state = State.Sending;
            Repaint();

            var request = new AITIssueReportService.SubmitRequest
            {
                Context = _context,
                Title = _title,
                Body = _body,
                Email = _email,
                // TODO(Task 7a): 스크린샷 첨부가 Transport 에 연결되면 _includeScreenshot 로 교체.
                IncludeScreenshot = false,
                LinkedEventId = _linkedEventId,
            };

            AITIssueReportService.SendAsync(request, result =>
            {
                // 사용자가 전송 중 OS 창 닫기로 윈도우를 파괴한 경우, 콜백이 좀비 인스턴스에 접근하지 않도록 가드.
                if (this == null)
                {
                    return;
                }

                _state = State.Done;
                Repaint();

                if (result.Success)
                {
                    EditorUtility.DisplayDialog(
                        "제보 완료",
                        "제보가 전송되었습니다. 빠른 시일 내에 확인하겠습니다.",
                        "확인");
                    Close();
                }
                else
                {
                    bool retry = EditorUtility.DisplayDialog(
                        "제보 실패",
                        $"전송에 실패했습니다.\n{result.ErrorMessage}",
                        "다시 시도",
                        "취소");
                    if (retry)
                    {
                        Submit();
                    }
                    else
                    {
                        Close();
                    }
                }
            });
        }
    }
}
