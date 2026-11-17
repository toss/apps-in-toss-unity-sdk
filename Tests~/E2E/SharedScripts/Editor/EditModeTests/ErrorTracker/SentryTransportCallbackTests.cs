// ---------------------------------------------------------------------------
// SentryTransportCallbackTests.cs - AITSentryTransport 완료 콜백 단위 테스트
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class SentryTransportCallbackTests
{
    [Test]
    public void SendEnvelope_WithoutDsn_InvokesCallbackAsFailureImmediately()
    {
        // DSN 해제 시 콜백은 즉시(동기적으로) 실패 통보되어야 한다.
        AITSentryTransport.SetDsn(null);

        bool called = false;
        bool success = true;
        string errorMessage = null;

        AITSentryTransport.SendEnvelope("dummy", result =>
        {
            called = true;
            success = result.Success;
            errorMessage = result.ErrorMessage;
        });

        Assert.IsTrue(called, "DSN 미설정 시 콜백이 즉시 호출되어야 함");
        Assert.IsFalse(success);
        StringAssert.Contains("DSN", errorMessage);
    }

    [Test]
    public void SendEnvelope_EmptyEnvelope_InvokesCallbackAsFailureImmediately()
    {
        AITSentryTransport.SetDsn("https://abc@o1.ingest.us.sentry.io/42");

        bool called = false;
        AITSentryTransport.SendEnvelope("", result =>
        {
            called = true;
            Assert.IsFalse(result.Success);
        });

        Assert.IsTrue(called);
    }
}
