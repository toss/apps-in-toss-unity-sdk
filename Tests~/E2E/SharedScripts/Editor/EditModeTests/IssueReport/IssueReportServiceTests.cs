// ---------------------------------------------------------------------------
// IssueReportServiceTests.cs - AITIssueReportService.BuildEnvelopeForTest 단위 테스트
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.IssueReport;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

[TestFixture]
[Category("Unit")]
public class IssueReportServiceTests
{
    private const string Dsn = "https://abc@o1.ingest.us.sentry.io/42";

    [Test]
    public void BuildEnvelope_Manual_IncludesEventAndUserReport()
    {
        var request = new AITIssueReportService.SubmitRequest
        {
            Context = AITIssueReportContext.Manual,
            Title = "버그 제보",
            Body = "저장 실패 발생",
            Email = "dev@toss.im",
        };

        string envelope = AITIssueReportService.BuildEnvelopeForTest(request, Dsn);

        StringAssert.Contains("\"type\":\"event\"", envelope);
        StringAssert.Contains("\"type\":\"user_report\"", envelope);
        StringAssert.Contains("\"comments\":", envelope);
        StringAssert.Contains("\"email\":\"dev@toss.im\"", envelope);
    }

    [Test]
    public void BuildEnvelope_BuildFailure_OnlyUserReport_UsingLinkedEventId()
    {
        const string linkedEventId = "0123456789abcdef0123456789abcdef";
        var request = new AITIssueReportService.SubmitRequest
        {
            Context = AITIssueReportContext.BuildFailure,
            Title = "빌드 실패",
            Body = "Brotli 압축 중 크래시",
            Email = "dev@toss.im",
            LinkedEventId = linkedEventId,
        };

        string envelope = AITIssueReportService.BuildEnvelopeForTest(request, Dsn);

        StringAssert.Contains("\"type\":\"user_report\"", envelope);
        StringAssert.Contains(linkedEventId, envelope);
        Assert.IsFalse(
            envelope.Contains("\"type\":\"event\""),
            "LinkedEventId가 있으면 새 event 아이템이 포함되지 않아야 합니다");
    }

    [Test]
    public void BuildEnvelope_BuildFailure_NullEventId_FallbacksToManual()
    {
        var request = new AITIssueReportService.SubmitRequest
        {
            Context = AITIssueReportContext.BuildFailure,
            Title = "빌드 실패 (연결 이벤트 없음)",
            Body = "에러 이벤트를 못 찾음",
            Email = "dev@toss.im",
            LinkedEventId = null,
        };

        // Fallback 경로에서 출력하는 경고는 테스트 실패로 잡히지 않도록 기대값으로 등록
        LogAssert.Expect(LogType.Warning,
            new Regex("이전 에러 이벤트를 찾지 못해 새 이벤트로 전송합니다"));

        string envelope = AITIssueReportService.BuildEnvelopeForTest(request, Dsn);

        StringAssert.Contains("\"type\":\"event\"", envelope);
        StringAssert.Contains("\"type\":\"user_report\"", envelope);
    }

    [Test]
    public void EmptyEmail_IsAutoFilled()
    {
        var request = new AITIssueReportService.SubmitRequest
        {
            Context = AITIssueReportContext.Manual,
            Title = "t",
            Body = "b",
            Email = "",
        };

        string envelope = AITIssueReportService.BuildEnvelopeForTest(request, Dsn);

        StringAssert.Contains("\"email\":", envelope);
    }
}
