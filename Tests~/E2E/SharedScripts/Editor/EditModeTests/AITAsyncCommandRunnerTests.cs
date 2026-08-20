// -----------------------------------------------------------------------
// AITAsyncCommandRunnerTests.cs - TailLines 경계 동작 검증
// 배경: 비동기 명령 실패 시 stderr가 비어 있으면(vite/esbuild 등이 에러를 stdout에 쓰는 경우)
// Console에 원인이 한 글자도 남지 않는 진단 공백이 있었다(실사례: vite build 실패 →
// 원인 불명 → node_modules 재설치 캐스케이드 183초). TailLines는 stdout 폴백 로깅 시
// Console 폭주를 막기 위한 말미 N줄 자르기 순수 함수이며, 프로세스 실행 없이 검증 가능하다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITAsyncCommandRunnerTests
{
    #region null / 빈 문자열

    [Test]
    public void TailLines_Null_ReturnsNull()
    {
        Assert.IsNull(AITAsyncCommandRunner.TailLines(null, 40));
    }

    [Test]
    public void TailLines_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AITAsyncCommandRunner.TailLines(string.Empty, 40));
    }

    #endregion

    #region maxLines 이하 (잘리지 않음)

    [Test]
    public void TailLines_SingleLine_ReturnsUnchanged()
    {
        string text = "vite build failed: Could not resolve entry module";

        string result = AITAsyncCommandRunner.TailLines(text, 40);

        Assert.AreEqual(text, result);
    }

    [Test]
    public void TailLines_LineCountEqualsMaxLines_ReturnsUnchanged()
    {
        string text = string.Join("\n", new[] { "line1", "line2", "line3" });

        string result = AITAsyncCommandRunner.TailLines(text, 3);

        Assert.AreEqual(text, result, "줄 수가 정확히 maxLines와 같으면 자르지 않아야 한다");
    }

    [Test]
    public void TailLines_LineCountBelowMaxLines_ReturnsUnchanged()
    {
        string text = string.Join("\n", new[] { "line1", "line2" });

        string result = AITAsyncCommandRunner.TailLines(text, 40);

        Assert.AreEqual(text, result);
        StringAssert.DoesNotContain("마지막", result, "잘리지 않았으면 잘림 안내 문구가 붙지 않아야 한다");
    }

    #endregion

    #region maxLines 초과 (마지막 N줄 + 잘림 표시)

    [Test]
    public void TailLines_ExceedsMaxLines_KeepsOnlyLastNLinesWithMarker()
    {
        string[] lines = { "line1", "line2", "line3", "line4", "line5" };
        string text = string.Join("\n", lines);

        string result = AITAsyncCommandRunner.TailLines(text, 2);

        StringAssert.Contains("마지막 2줄만 표시", result, "잘림 사실이 드러나는 문구가 있어야 한다");
        StringAssert.Contains("전체 5줄", result, "원본 줄 수가 안내되어야 한다");
        StringAssert.DoesNotContain("line1", result, "잘려나간 앞부분은 남지 않아야 한다");
        StringAssert.DoesNotContain("line3", result, "잘려나간 앞부분은 남지 않아야 한다");
        StringAssert.Contains("line4", result, "마지막 N줄은 남아야 한다");
        StringAssert.Contains("line5", result, "마지막 N줄은 남아야 한다");
    }

    [Test]
    public void TailLines_CrlfLineEndings_HandledSameAsLf()
    {
        string text = "line1\r\nline2\r\nline3\r\nline4";

        string result = AITAsyncCommandRunner.TailLines(text, 2);

        StringAssert.DoesNotContain("line1", result);
        StringAssert.DoesNotContain("line2", result);
        StringAssert.Contains("line3", result);
        StringAssert.Contains("line4", result);
    }

    #endregion

    #region maxLines 경계값

    [Test]
    public void TailLines_MaxLinesZero_ReturnsEmpty()
    {
        string text = "line1\nline2";

        string result = AITAsyncCommandRunner.TailLines(text, 0);

        Assert.AreEqual(string.Empty, result);
    }

    [Test]
    public void TailLines_MaxLinesNegative_ReturnsEmpty()
    {
        string text = "line1\nline2";

        string result = AITAsyncCommandRunner.TailLines(text, -1);

        Assert.AreEqual(string.Empty, result);
    }

    #endregion
}
