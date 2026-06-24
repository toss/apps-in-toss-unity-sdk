// ---------------------------------------------------------------------------
// BuildFailureSummaryTests.cs - AITConvertCore.BuildFailureSummary / ReadEditorLogTail 단위 테스트
// 에러/경고 캡 분리(에러 우선, 상호 비잠식)·심각도별 생략 표기·무진단 실패 시 에디터 로그 꼬리
// 첨부를 검증한다. BuildReport 없이 순수 함수로 검증 가능(Level 0, Unity/빌드 실행 불필요).
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;

[TestFixture]
[Category("Unit")]
public class BuildFailureSummaryTests
{
    private static List<(LogType type, string content)> Msgs(params (LogType, string)[] items)
        => new List<(LogType type, string content)>(items);

    // ---- BuildFailureSummary ----

    [Test]
    public void Header_AlwaysIncludesResultAndTotals()
    {
        string s = AITConvertCore.BuildFailureSummary("Failed", 3, 7, null, null);
        StringAssert.Contains("[AIT] WebGL 빌드가 실패했습니다.", s);
        StringAssert.Contains("결과: Failed", s);
        StringAssert.Contains("총 에러: 3, 총 경고: 7", s);
    }

    [Test]
    public void ErrorsShownBeforeWarnings()
    {
        string s = AITConvertCore.BuildFailureSummary("Failed", 1, 1,
            Msgs((LogType.Warning, "W1"), (LogType.Error, "E1")), null);
        int eIdx = s.IndexOf("E1");
        int wIdx = s.IndexOf("W1");
        Assert.Greater(eIdx, -1);
        Assert.Greater(wIdx, -1);
        Assert.Less(eIdx, wIdx, "에러가 경고보다 먼저 출력되어야 함.");
    }

    [Test]
    public void WarningsNotCrowdedOutByErrors()
    {
        // 과거 버그(이 PR이 해소): 공유 캡이라 에러가 캡을 채우면 경고가 한 줄도 안 보였다.
        // 이제 경고는 별도 예산(maxWarnings)으로 항상 보장된다.
        var msgs = new List<(LogType type, string content)>();
        for (int i = 0; i < 20; i++) msgs.Add((LogType.Error, "E" + i));
        msgs.Add((LogType.Warning, "WKEEP"));

        string s = AITConvertCore.BuildFailureSummary("Failed", 20, 1, msgs, null,
            maxErrors: 10, maxWarnings: 5);

        StringAssert.Contains("WKEEP", s);
        StringAssert.Contains("생략: 에러 10개, 경고 0개", s);
    }

    [Test]
    public void PerSeverityOmissionCounts()
    {
        var msgs = new List<(LogType type, string content)>();
        for (int i = 0; i < 12; i++) msgs.Add((LogType.Error, "E" + i));
        for (int i = 0; i < 8; i++) msgs.Add((LogType.Warning, "W" + i));

        string s = AITConvertCore.BuildFailureSummary("Failed", 12, 8, msgs, null,
            maxErrors: 10, maxWarnings: 5);

        // 에러 12개 중 10개 표시 → 2개 생략, 경고 8개 중 5개 표시 → 3개 생략
        StringAssert.Contains("생략: 에러 2개, 경고 3개", s);
    }

    [Test]
    public void NoOmissionLine_WhenWithinCaps()
    {
        string s = AITConvertCore.BuildFailureSummary("Failed", 1, 1,
            Msgs((LogType.Error, "E1"), (LogType.Warning, "W1")), null);
        StringAssert.DoesNotContain("생략", s);
    }

    [Test]
    public void AssertAndExceptionCountAsErrors()
    {
        string s = AITConvertCore.BuildFailureSummary("Failed", 2, 0,
            Msgs((LogType.Exception, "EX1"), (LogType.Assert, "AS1")), null);
        StringAssert.Contains("EX1", s);
        StringAssert.Contains("AS1", s);
    }

    [Test]
    public void LogTailAttached_OnlyWhenNoStepMessages()
    {
        string s = AITConvertCore.BuildFailureSummary("Failed", 0, 0,
            new List<(LogType type, string content)>(), "TAIL-CONTENT-XYZ");
        StringAssert.Contains("에디터 로그 꼬리", s);
        StringAssert.Contains("TAIL-CONTENT-XYZ", s);
    }

    [Test]
    public void LogTailNotAttached_WhenStepMessagesPresent()
    {
        string s = AITConvertCore.BuildFailureSummary("Failed", 1, 0,
            Msgs((LogType.Error, "E1")), "TAIL-CONTENT-XYZ");
        StringAssert.DoesNotContain("TAIL-CONTENT-XYZ", s);
    }

    // ---- ReadEditorLogTail ----

    [Test]
    public void ReadEditorLogTail_ReturnsLastBytes()
    {
        string path = TempLogPath();
        try
        {
            File.WriteAllText(path, "HEAD-0123456789-TAILSEGMENT");
            string tail = AITConvertCore.ReadEditorLogTail(path, maxBytes: 11);
            Assert.AreEqual("TAILSEGMENT", tail);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void ReadEditorLogTail_WholeFile_WhenShorterThanMax()
    {
        string path = TempLogPath();
        try
        {
            File.WriteAllText(path, "short");
            Assert.AreEqual("short", AITConvertCore.ReadEditorLogTail(path, maxBytes: 1000));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void ReadEditorLogTail_MissingFile_ReturnsNull()
    {
        Assert.IsNull(AITConvertCore.ReadEditorLogTail(
            Path.Combine(Path.GetTempPath(), "ait-nonexistent-" + System.Guid.NewGuid().ToString("N") + ".log")));
    }

    [Test]
    public void ReadEditorLogTail_NullPath_ReturnsNull()
    {
        Assert.IsNull(AITConvertCore.ReadEditorLogTail(null));
    }

    private static string TempLogPath()
        => Path.Combine(Path.GetTempPath(),
            "ait-logtail-" + System.Guid.NewGuid().ToString("N").Substring(0, 8) + ".log");
}
