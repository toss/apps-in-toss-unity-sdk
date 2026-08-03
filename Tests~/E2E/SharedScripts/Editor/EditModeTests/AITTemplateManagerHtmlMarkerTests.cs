// -----------------------------------------------------------------------
// AITTemplateManagerHtmlMarkerTests.cs - HTML 마커 머지 회귀 테스트
// Level 0: ReplaceHtmlUserSection이 마커 미발견 시 Sentry로 노이즈를 보내지 않는지 검증
//          (Sentry 이슈 APPS-IN-TOSS-UNITY-SDK-RA / -RB)
// -----------------------------------------------------------------------

using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
public class AITTemplateManagerHtmlMarkerTests
{
    private static int GetSuppressDepth()
    {
        var field = typeof(AITEditorErrorTracker).GetField(
            "_suppressLogCaptureCount",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "_suppressLogCaptureCount field should exist on AITEditorErrorTracker");
        return (int)field.GetValue(null);
    }

    [Test]
    public void ReplaceHtmlUserSection_MissingStartMarker_ReturnsContentUnchanged()
    {
        const string content = "<html><head></head><body></body></html>";
        const string newSection = "<!-- USER_HEAD_START -->custom<!-- USER_HEAD_END -->";

        // 마커가 없으므로 콘솔에 fallback 경고가 한 번 발생해야 한다.
        LogAssert.Expect(LogType.Warning, new Regex(@"\[AIT\] HTML 마커를 찾을 수 없습니다: <!-- USER_HEAD_START"));

        string result = AITTemplateManager.ReplaceHtmlUserSection(
            content,
            AITTemplateManager.HTML_USER_HEAD_START,
            AITTemplateManager.HTML_USER_HEAD_END,
            newSection);

        // 입력 콘텐츠는 그대로 반환되어야 한다 (사용자 환경 차이 fallback).
        Assert.AreEqual(content, result);
    }

    [Test]
    public void ReplaceHtmlUserSection_MissingMarker_DoesNotCaptureToSentry()
    {
        // 회귀 검증: 사용자 환경 차이(빌드 산출물에 마커 누락 등)로 발생하는 fallback warning은
        // Sentry로 전송되지 않아야 한다 (sentryCapture: false). 이를 위해
        // AITLog.SuppressScope 가 BeginSuppressLogCapture/EndSuppressLogCapture 를 호출하면서
        // Debug.LogWarning 이 발생하는 동안 _suppressLogCaptureCount 가 1 이상이어야 한다.
        //
        // logMessageReceived 콜백은 메인 스레드에서 동기적으로 호출되므로, 콜백 시점의
        // suppress depth 를 캡처해 검증한다.

        int observedDepth = -1;
        Application.LogCallback handler = (msg, stack, type) =>
        {
            if (type == LogType.Warning && msg != null && msg.Contains("[AIT] HTML 마커를 찾을 수 없습니다"))
            {
                observedDepth = GetSuppressDepth();
            }
        };

        Application.logMessageReceived += handler;
        try
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AIT\] HTML 마커를 찾을 수 없습니다: <!-- USER_BODY_END_START"));

            AITTemplateManager.ReplaceHtmlUserSection(
                "<html></html>",
                AITTemplateManager.HTML_USER_BODY_END_START,
                AITTemplateManager.HTML_USER_BODY_END_END,
                "<!-- USER_BODY_END_START -->x<!-- USER_BODY_END_END -->");
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        Assert.GreaterOrEqual(
            observedDepth, 1,
            "fallback warning이 Sentry로 캡처되지 않도록 SuppressScope 가 활성 상태여야 함 (sentryCapture: false)");
        // 호출이 끝난 뒤 suppress depth 는 원상복귀 되어야 한다.
        Assert.AreEqual(0, GetSuppressDepth(), "ReplaceHtmlUserSection 종료 후 suppress depth 는 0이어야 함");
    }

    [Test]
    public void ReplaceHtmlUserSection_MarkerPresent_ReplacesUserSection()
    {
        // 정상 경로: 마커가 있으면 경고 없이 사용자 섹션이 교체되어야 한다.
        string content =
            "<html><head>"
            + AITTemplateManager.HTML_USER_HEAD_START + " -->old"
            + AITTemplateManager.HTML_USER_HEAD_END
            + "</head></html>";
        string newSection = AITTemplateManager.HTML_USER_HEAD_START + " -->new" + AITTemplateManager.HTML_USER_HEAD_END;

        string result = AITTemplateManager.ReplaceHtmlUserSection(
            content,
            AITTemplateManager.HTML_USER_HEAD_START,
            AITTemplateManager.HTML_USER_HEAD_END,
            newSection);

        StringAssert.Contains("new", result);
        StringAssert.DoesNotContain("old", result);
        // LogAssert.NoUnexpectedReceived 는 Error 만 감시하므로, 패턴 누락 검증은 Expect 없이 기본 동작.
    }
}
