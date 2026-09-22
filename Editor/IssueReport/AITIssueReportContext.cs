namespace AppsInToss.Editor.IssueReport
{
    /// <summary>
    /// 이슈 제보가 어떤 경로에서 트리거되었는지를 구분합니다.
    /// 선택된 값에 따라 envelope 구성(신규 이벤트 vs 기존 이벤트 연결)과 태그가 달라집니다.
    /// </summary>
    internal enum AITIssueReportContext
    {
        /// <summary>메뉴에서 사용자가 직접 제보를 시작한 경우.</summary>
        Manual,

        /// <summary>빌드 실패 다이얼로그에서 제보를 시작한 경우 — 가능하면 직전 에러 이벤트에 연결합니다.</summary>
        BuildFailure,
    }
}
