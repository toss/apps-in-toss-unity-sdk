// ---------------------------------------------------------------------------
// SentryEnvelopeUserReportTests.cs - user_report envelope 아이템 빌더 단위 테스트
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class SentryEnvelopeUserReportTests
{
    [Test]
    public void BuildUserReportItem_ContainsHeaderAndPayload()
    {
        string item = AITSentryEnvelope.BuildUserReportItem(
            eventId: "abcdef0123456789abcdef0123456789",
            email: "dev@toss.im",
            name: "dave",
            comments: "빌드 직후 런타임 크래시가 납니다");

        string[] lines = item.Split('\n');
        Assert.AreEqual(2, lines.Length, "user_report item은 header 1줄 + payload 1줄");
        StringAssert.Contains("\"type\":\"user_report\"", lines[0]);
        StringAssert.Contains("\"event_id\":\"abcdef0123456789abcdef0123456789\"", lines[1]);
        StringAssert.Contains("\"email\":\"dev@toss.im\"", lines[1]);
        StringAssert.Contains("\"name\":\"dave\"", lines[1]);
        StringAssert.Contains("\"comments\":", lines[1]);
    }

    [Test]
    public void BuildUserReportItem_EscapesQuotesInComments()
    {
        string item = AITSentryEnvelope.BuildUserReportItem(
            eventId: "abcdef0123456789abcdef0123456789",
            email: "x@y.z",
            name: "n",
            comments: "She said \"hi\"");

        StringAssert.Contains("\\\"hi\\\"", item);
    }
}
