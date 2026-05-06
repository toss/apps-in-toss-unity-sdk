// ---------------------------------------------------------------------------
// ErrorTrackerIntegrationTests.cs - 실제 Sentry 전송 통합 테스트
// 실제 DSN으로 envelope을 전송하고 HTTP 200 응답을 검증합니다.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using AppsInToss;
using AppsInToss.Editor.ErrorTracker;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Breadcrumb = AppsInToss.Editor.ErrorTracker.AITSentryEnvelope.Breadcrumb;
using SpanData = AppsInToss.Editor.ErrorTracker.AITSentryEnvelope.SpanData;

[TestFixture]
[Category("Integration")]
[Ignore("Sentry organization quota 소진 시 모든 envelope이 429로 throttle됨. CI에서는 quota 영향을 피하기 위해 skip하고, 필요 시 로컬에서 명시적으로 실행한다.")]
public class ErrorTrackerIntegrationTests
{
    private string _dsn;
    private AITSentryEnvelope.DsnComponents _dsnComponents;

    [SetUp]
    public void SetUp()
    {
        _dsn = AITEditorErrorTracker.GetDsn();
        Assert.IsNotNull(_dsn, "DSN이 설정되어 있어야 합니다");
        _dsnComponents = AITSentryEnvelope.ParseDsn(_dsn);
    }

    [TearDown]
    public void TearDown()
    {
        _dsn = null;
        _dsnComponents = default;
    }

    /// <summary>
    /// Envelope을 Sentry로 직접 전송하고 HTTP 상태 코드를 반환합니다.
    /// </summary>
    private long SendEnvelopeSync(string envelope)
    {
        var url = _dsnComponents.GetEnvelopeUrl();
        var bodyBytes = Encoding.UTF8.GetBytes(envelope);

        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.timeout = 10;
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/x-sentry-envelope");
        request.SetRequestHeader(
            "X-Sentry-Auth",
            $"Sentry sentry_key={_dsnComponents.PublicKey}, sentry_version=7, sentry_client=apps-in-toss.unity.test/{AITVersion.Version}"
        );

        var operation = request.SendWebRequest();

        // 동기 대기 (EditMode 테스트에서는 코루틴 불가)
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!operation.isDone)
        {
            if (DateTime.UtcNow >= deadline)
            {
                request.Abort();
                request.Dispose();
                Assert.Fail("Sentry 전송 타임아웃 (10초)");
                return -1; // Assert.Fail이 예외를 던지므로 도달하지 않지만 컴파일러 요구
            }
            System.Threading.Thread.Sleep(50);
        }

        long statusCode = request.responseCode;
        string error = request.error;
        request.Dispose();

        if (!string.IsNullOrEmpty(error))
            Debug.Log($"[Test] HTTP 에러: {error}");

        return statusCode;
    }

    #region Error Event 전송

    [Test]
    public void SendErrorEvent_ReturnsHttp200()
    {
        var envelope = AITSentryEnvelope.BuildErrorEventEnvelope(
            dsn: _dsn,
            exceptionType: "TestException",
            exceptionValue: "[AIT-TEST] EditMode 통합 테스트: 에러 이벤트 전송 확인",
            stackTrace: "  at ErrorTrackerIntegrationTests.SendErrorEvent_ReturnsHttp200 () in Tests/ErrorTrackerIntegrationTests.cs:1",
            level: "error",
            tags: new Dictionary<string, string>
            {
                { "test", "true" },
                { "sdk_version", AITVersion.Version },
                { "unity_version", Application.unityVersion }
            },
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        const string expectedEnv = "edit-mode-test";
        StringAssert.Contains($"\"environment\":\"{expectedEnv}\"", envelope,
            $"통합 테스트는 environment='{expectedEnv}' 로 전송되어야 합니다");

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    [Test]
    public void SendErrorEvent_WithBreadcrumbs_ReturnsHttp200()
    {
        var breadcrumbs = new List<Breadcrumb>
        {
            new Breadcrumb
            {
                Timestamp = DateTime.UtcNow.AddSeconds(-5),
                Category = "build",
                Message = "빌드 시작",
                Level = "info"
            },
            new Breadcrumb
            {
                Timestamp = DateTime.UtcNow.AddSeconds(-2),
                Category = "build",
                Message = "WebGL 빌드 완료",
                Level = "info"
            }
        };

        var envelope = AITSentryEnvelope.BuildErrorEventEnvelope(
            dsn: _dsn,
            exceptionType: "AITBuildError.FAIL_NPM_BUILD",
            exceptionValue: "[AIT-TEST] EditMode 통합 테스트: pnpm build 실패 시뮬레이션",
            stackTrace: null,
            level: "error",
            tags: new Dictionary<string, string>
            {
                { "test", "true" },
                { "error_code", "FAIL_NPM_BUILD" }
            },
            breadcrumbs: breadcrumbs,
            fingerprint: new[] { "{{ default }}", "FAIL_NPM_BUILD" },
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    #endregion

    #region 빌드 에러 전송

    [Test]
    public void SendBuildWebglFailedError_ReturnsHttp200()
    {
        var envelope = AITSentryEnvelope.BuildErrorEventEnvelope(
            dsn: _dsn,
            exceptionType: "AITBuildError.BUILD_WEBGL_FAILED",
            exceptionValue: "[AIT-TEST] EditMode 통합 테스트: WebGL 빌드 실패 시뮬레이션",
            stackTrace: null,
            level: "error",
            tags: new Dictionary<string, string>
            {
                { "test", "true" },
                { "error_code", "BUILD_WEBGL_FAILED" },
                { "error_code_int", "2" },
                { "build_profile", "TestProfile" }
            },
            fingerprint: new[] { "{{ default }}", "BUILD_WEBGL_FAILED" },
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    [Test]
    public void SendNpmRunnerError_ReturnsHttp200()
    {
        var envelope = AITSentryEnvelope.BuildErrorEventEnvelope(
            dsn: _dsn,
            exceptionType: "UnityError",
            exceptionValue: "[AIT-TEST] AITNpmRunner: pnpm install 실패 — ENOENT: no such file or directory",
            stackTrace: null,
            level: "error",
            tags: new Dictionary<string, string>
            {
                { "test", "true" },
                { "sdk_version", AITVersion.Version },
                { "unity_version", Application.unityVersion }
            },
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    #endregion

    #region Session 전송

    [Test]
    public void SendSessionInit_ReturnsHttp200()
    {
        var envelope = AITSentryEnvelope.BuildSessionEnvelope(
            dsn: _dsn,
            sessionId: Guid.NewGuid().ToString("N"),
            distinctId: "test_device_hash",
            status: "ok",
            isInit: true,
            started: DateTime.UtcNow,
            errorCount: 0,
            duration: 0,
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    [Test]
    public void SendSessionClose_ReturnsHttp200()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var started = DateTime.UtcNow.AddMinutes(-5);

        // Init 먼저 전송
        var initEnvelope = AITSentryEnvelope.BuildSessionEnvelope(
            dsn: _dsn,
            sessionId: sessionId,
            distinctId: "test_device_hash",
            status: "ok",
            isInit: true,
            started: started,
            errorCount: 0,
            duration: 0,
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );
        var initStatus = SendEnvelopeSync(initEnvelope);
        Assert.AreEqual(200, initStatus, $"Session init 전송 실패 (HTTP {initStatus})");

        // Close 전송
        var closeEnvelope = AITSentryEnvelope.BuildSessionEnvelope(
            dsn: _dsn,
            sessionId: sessionId,
            distinctId: "test_device_hash",
            status: "exited",
            isInit: false,
            started: started,
            errorCount: 2,
            duration: 300.0,
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(closeEnvelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    #endregion

    #region LastEventId 검증

    [Test]
    public void CaptureError_UpdatesLastEventId()
    {
        if (!AppsInToss.Editor.ErrorTracker.AITErrorTrackerConsent.IsEnabled())
        {
            Assert.Ignore("Consent disabled in test env; LastEventId 검증 스킵");
        }

        AITEditorErrorTracker.CaptureError(
            exceptionType: "TestException_" + System.Guid.NewGuid().ToString("N"),
            message: "test message",
            stackTrace: null,
            level: "error");

        string id = AITEditorErrorTracker.LastEventId;
        Assert.IsNotNull(id);
        Assert.AreEqual(32, id.Length);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9a-f]{32}$"),
            $"Event id must be 32 lowercase hex chars, got: {id}");
    }

    #endregion

    #region Transaction 전송

    [Test]
    public void SendBuildTransaction_ReturnsHttp200()
    {
        var traceId = AITSentryEnvelope.GenerateTraceId();
        var rootSpanId = AITSentryEnvelope.GenerateSpanId();
        var start = DateTime.UtcNow.AddSeconds(-10);
        var end = DateTime.UtcNow;

        var spans = new List<SpanData>
        {
            new SpanData
            {
                SpanId = AITSentryEnvelope.GenerateSpanId(),
                ParentSpanId = rootSpanId,
                Op = "webgl.build",
                Description = "WebGL Build",
                Status = "ok",
                StartTimestamp = start,
                EndTimestamp = start.AddSeconds(7)
            },
            new SpanData
            {
                SpanId = AITSentryEnvelope.GenerateSpanId(),
                ParentSpanId = rootSpanId,
                Op = "packaging",
                Description = "npm run build (granite)",
                Status = "ok",
                StartTimestamp = start.AddSeconds(7),
                EndTimestamp = end
            }
        };

        var measurements = new Dictionary<string, double>
        {
            { "webgl_build_ms", 7000.0 },
            { "packaging_ms", 3000.0 }
        };

        var tags = new Dictionary<string, string>
        {
            { "test", "true" },
            { "clean_build", "false" },
            { "build_profile", "TestProfile" }
        };

        var envelope = AITSentryEnvelope.BuildTransactionEnvelope(
            dsn: _dsn,
            transactionName: "EditMode Integration Test Build",
            op: "build",
            traceId: traceId,
            spanId: rootSpanId,
            startTimestamp: start,
            endTimestamp: end,
            status: "ok",
            spans: spans,
            tags: tags,
            measurements: measurements,
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    [Test]
    public void SendFailedBuildTransaction_ReturnsHttp200()
    {
        var traceId = AITSentryEnvelope.GenerateTraceId();
        var rootSpanId = AITSentryEnvelope.GenerateSpanId();
        var start = DateTime.UtcNow.AddSeconds(-5);
        var end = DateTime.UtcNow;

        var spans = new List<SpanData>
        {
            new SpanData
            {
                SpanId = AITSentryEnvelope.GenerateSpanId(),
                ParentSpanId = rootSpanId,
                Op = "webgl.build",
                Description = "WebGL Build",
                Status = "ok",
                StartTimestamp = start,
                EndTimestamp = start.AddSeconds(3)
            },
            new SpanData
            {
                SpanId = AITSentryEnvelope.GenerateSpanId(),
                ParentSpanId = rootSpanId,
                Op = "packaging",
                Description = "pnpm run build 실패",
                Status = "internal_error",
                StartTimestamp = start.AddSeconds(3),
                EndTimestamp = end
            }
        };

        var envelope = AITSentryEnvelope.BuildTransactionEnvelope(
            dsn: _dsn,
            transactionName: "EditMode Integration Test Build (Failed)",
            op: "build",
            traceId: traceId,
            spanId: rootSpanId,
            startTimestamp: start,
            endTimestamp: end,
            status: "internal_error",
            spans: spans,
            tags: new Dictionary<string, string> { { "test", "true" } },
            release: AITEditorErrorTracker.GetRelease(),
            environment: "edit-mode-test"
        );

        var statusCode = SendEnvelopeSync(envelope);
        Assert.AreEqual(200, statusCode, $"Sentry가 200을 반환해야 합니다 (실제: {statusCode})");
    }

    #endregion
}
