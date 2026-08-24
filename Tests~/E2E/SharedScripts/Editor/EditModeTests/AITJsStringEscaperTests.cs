// -----------------------------------------------------------------------
// AITJsStringEscaperTests.cs
// AITJsStringEscaper.EscapeSingleQuoted 단위 테스트.
//
// 배선(어느 치환부가 이 함수를 거쳐야 하는가)은 AITJsStringEscaperWiringTests 가 검증하고,
// 여기서는 이스케이프 자체가 옳은지 — 특히 산출물을 깨뜨릴 수 있는 문자들이 실제로
// 무력화되는지 — 를 본다.
// -----------------------------------------------------------------------

using System.Text;
using NUnit.Framework;
using AppsInToss.Editor.Package;

[TestFixture]
[Category("Unit")]
public class AITJsStringEscaperTests
{
    [Test]
    public void SafeValue_PassesThroughUnchanged()
    {
        // 흔한 값들은 이스케이프로 오염되지 않아야 한다(산출물 diff 노이즈 방지).
        Assert.AreEqual("#3182F6", AITJsStringEscaper.EscapeSingleQuoted("#3182F6"));
        Assert.AreEqual("localhost", AITJsStringEscaper.EscapeSingleQuoted("localhost"));
        Assert.AreEqual("dist", AITJsStringEscaper.EscapeSingleQuoted("dist"));
        Assert.AreEqual("true", AITJsStringEscaper.EscapeSingleQuoted("true"));
        Assert.AreEqual("https://example.com/icon.png",
            AITJsStringEscaper.EscapeSingleQuoted("https://example.com/icon.png"));
    }

    [Test]
    public void NullOrEmpty_BecomesEmptyString()
    {
        Assert.AreEqual(string.Empty, AITJsStringEscaper.EscapeSingleQuoted(null));
        Assert.AreEqual(string.Empty, AITJsStringEscaper.EscapeSingleQuoted(string.Empty));
    }

    [Test]
    public void SingleQuote_IsEscaped_SoLiteralDoesNotTerminateEarly()
    {
        // 이 저장소를 실제로 깨뜨렸을 케이스: 표시 이름의 아포스트로피.
        Assert.AreEqual("Dave\\'s Adventure", AITJsStringEscaper.EscapeSingleQuoted("Dave's Adventure"));
    }

    [Test]
    public void Backslash_IsEscapedBeforeOtherSequences()
    {
        // 백슬래시를 먼저 처리하지 않으면 뒤이어 넣은 이스케이프가 다시 해석되어 무력화된다.
        Assert.AreEqual("a\\\\b", AITJsStringEscaper.EscapeSingleQuoted("a\\b"));

        // 값이 이미 \' 를 담고 있어도 결과가 리터럴을 탈출하지 않아야 한다.
        Assert.AreEqual("a\\\\\\'b", AITJsStringEscaper.EscapeSingleQuoted("a\\'b"));
    }

    [Test]
    public void Newlines_AreEscaped()
    {
        Assert.AreEqual("a\\nb", AITJsStringEscaper.EscapeSingleQuoted("a\nb"));
        Assert.AreEqual("a\\rb", AITJsStringEscaper.EscapeSingleQuoted("a\rb"));
        Assert.AreEqual("a\\tb", AITJsStringEscaper.EscapeSingleQuoted("a\tb"));
    }

    [Test]
    public void ScriptCloseTag_CannotTerminateInlineScriptBlock()
    {
        string escaped = AITJsStringEscaper.EscapeSingleQuoted("</script><script>alert(1)</script>");

        // '<' 가 남아 있으면 HTML 파서가 인라인 <script> 블록을 그 자리에서 끝내 버린다.
        StringAssert.DoesNotContain("<", escaped);
        StringAssert.Contains("\\x3C", escaped);
    }

    [Test]
    public void HtmlCommentOpener_IsNeutralized()
    {
        StringAssert.DoesNotContain("<", AITJsStringEscaper.EscapeSingleQuoted("<!--"));
    }

    [Test]
    public void LineSeparators_AreEscaped()
    {
        // U+2028/U+2029 는 ES2019 이전 파서가 줄바꿈으로 취급해 문법 오류를 낸다.
        Assert.AreEqual("a\\u2028b", AITJsStringEscaper.EscapeSingleQuoted("a\u2028b"));
        Assert.AreEqual("a\\u2029b", AITJsStringEscaper.EscapeSingleQuoted("a\u2029b"));
    }

    /// <summary>
    /// 이스케이프 결과를 작은따옴표 리터럴로 감쌌을 때, JS 파서가 읽어들이는 값이 원본과
    /// 같아야 한다(= 이스케이프가 과하지도 부족하지도 않다). 테스트 안에 최소 언이스케이퍼를
    /// 두어 왕복을 확인한다 — EditMode 에서 실제 JS 엔진을 띄울 수 없기 때문이다.
    /// </summary>
    [Test]
    public void EscapedValue_RoundTripsBackToOriginal()
    {
        string[] hostileValues =
        {
            "Dave's Adventure",
            "back\\slash",
            "quote'and\\backslash",
            "line\nbreak",
            "tab\there",
            "</script>",
            "<!-- comment",
            "sep\u2028arator",
            "#3182F6",
            "",
        };

        foreach (string original in hostileValues)
        {
            string escaped = AITJsStringEscaper.EscapeSingleQuoted(original);

            Assert.AreEqual(original, UnescapeJsSingleQuoted(escaped),
                $"이스케이프 왕복이 값을 바꿨다. 원본=[{original}] 이스케이프=[{escaped}]");
        }
    }

    // 작은따옴표 리터럴 안쪽 문자열을 JS 파서처럼 되돌린다(이 escaper 가 내는 시퀀스만 지원).
    private static string UnescapeJsSingleQuoted(string escaped)
    {
        var sb = new StringBuilder(escaped.Length);

        for (int i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] != '\\')
            {
                sb.Append(escaped[i]);
                continue;
            }

            Assert.Less(i + 1, escaped.Length, "이스케이프 문자로 문자열이 끝났다(리터럴이 깨진다).");
            char next = escaped[++i];

            switch (next)
            {
                case '\\': sb.Append('\\'); break;
                case '\'': sb.Append('\''); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'x':
                    sb.Append((char)System.Convert.ToInt32(escaped.Substring(i + 1, 2), 16));
                    i += 2;
                    break;
                case 'u':
                    sb.Append((char)System.Convert.ToInt32(escaped.Substring(i + 1, 4), 16));
                    i += 4;
                    break;
                default:
                    Assert.Fail($"예상하지 못한 이스케이프 시퀀스: \\{next}");
                    break;
            }
        }

        return sb.ToString();
    }
}
