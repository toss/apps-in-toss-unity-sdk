// -----------------------------------------------------------------------
// AITPlatformHelperTests.cs - 크로스 플랫폼 헬퍼 순수 로직 검증
// Level 0: ANSI 스트리핑 / 실행파일 이름 / PATH 구성 / Bash 이스케이프 등
//          프로세스를 띄우지 않는 결정적 메서드의 특성화 테스트.
// 플랫폼 의존 동작은 AITPlatformHelper.IsWindows로 분기해 macOS/Windows
// CI 양쪽에서 통과하도록 작성한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using AppsInToss.Editor;

[TestFixture]
public class AITPlatformHelperTests
{
    // =====================================================
    // StripAnsiCodes — 순수(플랫폼 무관)
    // =====================================================

    [Test]
    public void StripAnsiCodes_Null_ReturnsNull()
    {
        Assert.IsNull(AITPlatformHelper.StripAnsiCodes(null));
    }

    [Test]
    public void StripAnsiCodes_Empty_ReturnsEmpty()
    {
        Assert.AreEqual("", AITPlatformHelper.StripAnsiCodes(""));
    }

    [Test]
    public void StripAnsiCodes_PlainTextWithoutBrackets_Unchanged()
    {
        // 대괄호가 없는 평문은 그대로 유지된다.
        const string plain = "plain build output 123 ok";
        Assert.AreEqual(plain, AITPlatformHelper.StripAnsiCodes(plain));
    }

    [Test]
    public void StripAnsiCodes_StandardColorSequence_Removed()
    {
        // ESC[31m ... ESC[0m (빨간색) → 텍스트만 남는다.
        Assert.AreEqual("red", AITPlatformHelper.StripAnsiCodes("\u001b[31mred\u001b[0m"));
    }

    [Test]
    public void StripAnsiCodes_MultiParamSequence_Removed()
    {
        // ESC[1;32m (굵게+초록) 같은 복합 파라미터 시퀀스도 제거.
        Assert.AreEqual("green", AITPlatformHelper.StripAnsiCodes("\u001b[1;32mgreen\u001b[39m"));
    }

    [Test]
    public void StripAnsiCodes_OscSequence_Removed()
    {
        // OSC 시퀀스: ESC]0;title BEL → 제거되고 본문만 남는다.
        Assert.AreEqual("hello", AITPlatformHelper.StripAnsiCodes("\u001b]0;my-title\u0007hello"));
    }

    [Test]
    public void StripAnsiCodes_BareBracketSequenceWithoutEsc_AlsoRemoved()
    {
        // 일부 터미널은 ESC 없이 "[..m"만 emit한다 — 정규식 셋째 대안이 이를 흡수한다.
        // 의도된 공격적 스트리핑임을 특성화로 못 박는다.
        Assert.AreEqual("textmore", AITPlatformHelper.StripAnsiCodes("text[0mmore"));
    }

    // =====================================================
    // GetExecutableName — 플랫폼 의존
    // =====================================================

    [Test]
    public void GetExecutableName_RespectsPlatformExtension()
    {
        if (AITPlatformHelper.IsWindows)
        {
            // npm/pnpm/npx는 .cmd, 그 외는 .exe
            Assert.AreEqual("node.exe", AITPlatformHelper.GetExecutableName("node"));
            Assert.AreEqual("npm.cmd", AITPlatformHelper.GetExecutableName("npm"));
            Assert.AreEqual("pnpm.cmd", AITPlatformHelper.GetExecutableName("pnpm"));
            Assert.AreEqual("npx.cmd", AITPlatformHelper.GetExecutableName("npx"));
        }
        else
        {
            // Unix 계열은 확장자 없이 이름 그대로.
            Assert.AreEqual("node", AITPlatformHelper.GetExecutableName("node"));
            Assert.AreEqual("npm", AITPlatformHelper.GetExecutableName("npm"));
            Assert.AreEqual("pnpm", AITPlatformHelper.GetExecutableName("pnpm"));
        }
    }

    // =====================================================
    // 플랫폼 상수 일관성
    // =====================================================

    [Test]
    public void PlatformConstants_MatchCurrentPlatform()
    {
        if (AITPlatformHelper.IsWindows)
        {
            Assert.AreEqual(".exe", AITPlatformHelper.ExecutableExtension);
            Assert.AreEqual(".cmd", AITPlatformHelper.ScriptExtension);
            Assert.AreEqual(';', AITPlatformHelper.PathSeparator);
        }
        else
        {
            Assert.AreEqual("", AITPlatformHelper.ExecutableExtension);
            Assert.AreEqual("", AITPlatformHelper.ScriptExtension);
            Assert.AreEqual(':', AITPlatformHelper.PathSeparator);
        }
    }

    [Test]
    public void IsUnix_IsConsistentWithMacOsOrLinux()
    {
        Assert.AreEqual(AITPlatformHelper.IsMacOS || AITPlatformHelper.IsLinux, AITPlatformHelper.IsUnix);
    }

    // =====================================================
    // BuildPathEnv — 존재하는 경로만 통과 + 기본 경로 추가
    // =====================================================

    [Test]
    public void BuildPathEnv_IncludesExistingDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ait-test-pathenv-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);
        try
        {
            string result = AITPlatformHelper.BuildPathEnv(tempDir);
            StringAssert.Contains(tempDir, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void BuildPathEnv_ExcludesNonexistentDirectory()
    {
        string bogus = Path.Combine(Path.GetTempPath(), "ait-does-not-exist-" + Guid.NewGuid().ToString("N"));
        string result = AITPlatformHelper.BuildPathEnv(bogus);
        Assert.IsFalse(result.Contains(bogus), "존재하지 않는 경로는 PATH에서 제외돼야 한다");
    }

    [Test]
    public void BuildPathEnv_UsesPlatformSeparatorAndDefaults()
    {
        // 인자가 없어도 플랫폼 기본 경로가 추가돼 비어있지 않으며, 여러 경로가
        // 플랫폼 구분자로 연결된다.
        string result = AITPlatformHelper.BuildPathEnv();
        Assert.IsFalse(string.IsNullOrEmpty(result));
        StringAssert.Contains(AITPlatformHelper.PathSeparator.ToString(), result);
        if (!AITPlatformHelper.IsWindows)
        {
            StringAssert.Contains("/usr/bin", result);
        }
    }

    // =====================================================
    // EscapeForBashDoubleQuotes — internal (InternalsVisibleTo로 접근)
    // =====================================================

    [Test]
    public void EscapeForBashDoubleQuotes_NullOrEmpty_Unchanged()
    {
        Assert.IsNull(AITPlatformHelper.EscapeForBashDoubleQuotes(null));
        Assert.AreEqual("", AITPlatformHelper.EscapeForBashDoubleQuotes(""));
    }

    [Test]
    public void EscapeForBashDoubleQuotes_PlainText_Unchanged()
    {
        Assert.AreEqual("simple-text", AITPlatformHelper.EscapeForBashDoubleQuotes("simple-text"));
    }

    [Test]
    public void EscapeForBashDoubleQuotes_EscapesSpecialChars()
    {
        // 백슬래시 → 두 개
        Assert.AreEqual("a\\\\b", AITPlatformHelper.EscapeForBashDoubleQuotes("a\\b"));
        // 큰따옴표 → \"
        Assert.AreEqual("a\\\"b", AITPlatformHelper.EscapeForBashDoubleQuotes("a\"b"));
        // 달러 → \$
        Assert.AreEqual("a\\$b", AITPlatformHelper.EscapeForBashDoubleQuotes("a$b"));
        // 백틱 → \`
        Assert.AreEqual("a\\`b", AITPlatformHelper.EscapeForBashDoubleQuotes("a`b"));
    }

    [Test]
    public void EscapeForBashDoubleQuotes_BackslashEscapedFirst()
    {
        // 백슬래시가 먼저 이스케이프되므로, 입력의 백슬래시는 더블되고
        // 따옴표가 추가하는 백슬래시와 섞이지 않는다.
        // 입력: \"  (백슬래시 + 큰따옴표)
        // 기대: \\\"  (더블된 백슬래시 + 이스케이프된 따옴표)
        Assert.AreEqual("\\\\\\\"", AITPlatformHelper.EscapeForBashDoubleQuotes("\\\""));
    }
}
