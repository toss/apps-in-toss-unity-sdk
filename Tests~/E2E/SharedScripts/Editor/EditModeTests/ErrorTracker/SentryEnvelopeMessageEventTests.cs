// ---------------------------------------------------------------------------
// SentryEnvelopeMessageEventTests.cs - 메시지 이벤트 envelope 단위 테스트
// ---------------------------------------------------------------------------

using NUnit.Framework;
using System.Collections.Generic;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class SentryEnvelopeMessageEventTests
{
    private const string Dsn = "https://abc@o1.ingest.us.sentry.io/42";

    [Test]
    public void BuildMessageEventEnvelope_ReturnsEnvelopeAndEventId()
    {
        string envelope = AITSentryEnvelope.BuildMessageEventEnvelope(
            dsn: Dsn,
            message: "유저 제보 제목",
            level: "info",
            tags: new Dictionary<string, string> { { "feedback.source", "manual_menu" } },
            breadcrumbs: null,
            release: null,
            environment: "dev",
            eventId: out string eventId);

        Assert.IsFalse(string.IsNullOrEmpty(eventId));
        Assert.AreEqual(32, eventId.Length, "eventId는 32자 hex");
        StringAssert.Contains("\"type\":\"event\"", envelope);
        StringAssert.Contains("\"message\":", envelope);
        StringAssert.Contains("\"level\":\"info\"", envelope);
        StringAssert.Contains("\"feedback.source\":\"manual_menu\"", envelope);
        StringAssert.Contains(eventId, envelope);
    }

    [Test]
    public void BuildMessageEventEnvelope_EmptyLevelDefaultsToInfo()
    {
        string envelope = AITSentryEnvelope.BuildMessageEventEnvelope(
            dsn: Dsn,
            message: "x",
            level: null,
            tags: null,
            breadcrumbs: null,
            release: null,
            environment: null,
            eventId: out _);

        StringAssert.Contains("\"level\":\"info\"", envelope);
    }
}
