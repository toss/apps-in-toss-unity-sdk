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

    [Test]
    public void BuildAttachmentItem_ContainsBinaryPayload()
    {
        byte[] data = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic
        string item = AITSentryEnvelope.BuildAttachmentItem(
            data: data,
            filename: "screenshot.png",
            contentType: "image/png");

        int newlineIdx = item.IndexOf('\n');
        Assert.Greater(newlineIdx, 0, "header와 payload가 \\n으로 구분되어야 함");

        string header = item.Substring(0, newlineIdx);
        StringAssert.Contains("\"type\":\"attachment\"", header);
        StringAssert.Contains("\"length\":4", header);
        StringAssert.Contains("\"filename\":\"screenshot.png\"", header);
        StringAssert.Contains("\"content_type\":\"image/png\"", header);
    }
}
