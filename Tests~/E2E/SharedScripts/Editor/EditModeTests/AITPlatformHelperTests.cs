// -----------------------------------------------------------------------
// AITPlatformHelperTests.cs - 크로스 플랫폼 헬퍼 순수 로직 검증
// Level 0: ANSI 스트리핑 / 실행파일 이름 / PATH 구성 / Bash 이스케이프 등
//          프로세스를 띄우지 않는 결정적 메서드의 특성화 테스트.
// 플랫폼 의존 동작은 AITPlatformHelper.IsWindows로 분기해 macOS/Windows
// CI 양쪽에서 통과하도록 작성한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
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

    // =====================================================
    // Windows PowerShell argv 인용 — BuildPowerShellArguments 계열
    // 순수 함수이므로 macOS/Windows 양쪽 EditMode CI에서 실행된다.
    // =====================================================

    /// <summary>
    /// 테스트 전용 CommandLineToArgvW/MSVCRT 참조 파서. QuoteWindowsCommandLineArgument가 만든
    /// 인용이 원문 argv로 정확히 복원되는지 검증하는 용도로만 쓰인다(실제 소스에는 존재하지 않음).
    /// 규칙: 백슬래시 뒤에 "가 오면 짝수 개는 절반으로 줄고 "는 quote 토글, 홀수 개는 (n-1)/2개로
    /// 줄고 마지막 백슬래시가 "를 이스케이프(리터럴 "). quote 안에서 ""는 리터럴 "(in-quote 규칙).
    /// </summary>
    private static List<string> ParseWindowsArgv(string commandLine)
    {
        var args = new List<string>();
        if (string.IsNullOrEmpty(commandLine))
        {
            return args;
        }

        int i = 0;
        int n = commandLine.Length;
        while (i < n)
        {
            while (i < n && (commandLine[i] == ' ' || commandLine[i] == '\t')) i++;
            if (i >= n) break;

            var sb = new StringBuilder();
            bool inQuotes = false;
            while (i < n)
            {
                if (commandLine[i] == '\\')
                {
                    int backslashCount = 0;
                    while (i < n && commandLine[i] == '\\') { backslashCount++; i++; }

                    if (i < n && commandLine[i] == '"')
                    {
                        sb.Append('\\', backslashCount / 2);
                        if (backslashCount % 2 == 1)
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                            i++;
                        }
                    }
                    else
                    {
                        sb.Append('\\', backslashCount);
                    }
                }
                else if (commandLine[i] == '"')
                {
                    if (inQuotes && i + 1 < n && commandLine[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                        i++;
                    }
                }
                else if (!inQuotes && (commandLine[i] == ' ' || commandLine[i] == '\t'))
                {
                    break;
                }
                else
                {
                    sb.Append(commandLine[i]);
                    i++;
                }
            }

            args.Add(sb.ToString());
        }

        return args;
    }

    [Test]
    public void ParseWindowsArgv_MatchesMsDocsVectors()
    {
        // MS 문서("Parse C++ command-line arguments")에 실린 표준 5개 벡터.
        CollectionAssert.AreEqual(new[] { "abc", "d", "e" }, ParseWindowsArgv(@"""abc"" d e"));
        CollectionAssert.AreEqual(new[] { @"a\\b", "de fg", "h" }, ParseWindowsArgv(@"a\\b d""e f""g h"));
        CollectionAssert.AreEqual(new[] { @"a\""b", "c", "d" }, ParseWindowsArgv(@"a\\\""b c d"));
        CollectionAssert.AreEqual(new[] { @"a\\b c", "d", "e" }, ParseWindowsArgv(@"a\\\\""b c"" d e"));
        CollectionAssert.AreEqual(new[] { @"a\\""b", "c", "d" }, ParseWindowsArgv(@"a\\\\\""b c d"));
    }

    [TestCase("")]
    [TestCase("a b")]
    [TestCase("a\"b")]
    [TestCase("a\\\"b")]
    [TestCase("trail\\")]
    [TestCase("\\\\")]
    [TestCase("\"\"")]
    [TestCase("\t")]
    [TestCase("·")]
    [TestCase("한글")]
    [TestCase("\u201C")]
    [TestCase("$")]
    [TestCase("`")]
    public void QuoteWindowsCommandLineArgument_RoundTrips(string value)
    {
        string quoted = AITPlatformHelper.QuoteWindowsCommandLineArgument(value);
        var parsed = ParseWindowsArgv("-X " + quoted);

        Assert.AreEqual(2, parsed.Count, $"인용된 인자는 argv 원소 하나여야 한다: quoted=[{quoted}]");
        Assert.AreEqual("-X", parsed[0]);
        Assert.AreEqual(value, parsed[1], $"왕복 변환 후 원문과 같아야 한다: quoted=[{quoted}]");
    }

    [Test]
    public void QuoteWindowsCommandLineArgument_FuzzRoundTrip()
    {
        // 고정 시드 퍼즈: ASCII, 연속 백슬래시, 탭, 한글, 스마트따옴표, $, 백틱을 섞은 무작위 문자열
        // 2000건을 인용 → 파싱 왕복시켜 원문과 정확히 일치하는지 확인한다.
        var random = new System.Random(20260922);
        char[] pool = "ab \\\"$`\t·한글'\u2019\u201C".ToCharArray();

        for (int iter = 0; iter < 2000; iter++)
        {
            int length = random.Next(0, 16);
            var sb = new StringBuilder();
            for (int k = 0; k < length; k++)
            {
                sb.Append(pool[random.Next(pool.Length)]);
            }
            string value = sb.ToString();

            string quoted = AITPlatformHelper.QuoteWindowsCommandLineArgument(value);
            var parsed = ParseWindowsArgv("-X " + quoted);

            Assert.AreEqual(2, parsed.Count, $"iter={iter} value=[{value}] quoted=[{quoted}]");
            Assert.AreEqual(value, parsed[1], $"iter={iter} value=[{value}] quoted=[{quoted}]");
        }
    }

    [Test]
    public void BuildPowerShellArguments_ForumDeployCommand_IsSingleScriptArg()
    {
        // 포럼 제보 재현: 공백 포함 사용자 경로의 pnpm + 공백· · ·타임스탬프를 포함한 실제 형태 memo.
        string pnpmPath = @"C:\Users\A B\pnpm.cmd";
        string memo = "[Test] My-Game v1.0.0 · Unity SDK 3.2.0 · 2026-09-22 19:42 KST";
        string command = $"\"{pnpmPath}\" exec ait deploy --api-key \"k\" -m \"{memo}\"";
        string pathEnv = @"C:\q r\;" + "\u2019dir";

        string shellArgs = AITPlatformHelper.BuildPowerShellArguments(command, pathEnv);
        var parsed = ParseWindowsArgv("powershell.exe " + shellArgs);

        Assert.AreEqual(7, parsed.Count, "powershell.exe + 고정 플래그 4개 + -Command + 스크립트 1개 = 7");

        string expectedScript = AITPlatformHelper.BuildPowerShellScript(command, pathEnv);
        Assert.AreEqual(expectedScript, parsed[parsed.Count - 1], "마지막 argv 원소는 BuildPowerShellScript 결과와 같아야 한다");

        string expectedSuffix = $"& \"{pnpmPath}\" exec ait deploy --api-key \"k\" -m \"{memo}\"";
        StringAssert.EndsWith(expectedSuffix, expectedScript);
    }

    [TestCase("\"C:\\p\\a.cmd\" x", true)]
    [TestCase("  \"C:\\p\\a.cmd\" x", true)]
    [TestCase("'C:\\p\\a.cmd' x", true)]
    [TestCase("Expand-Archive -Path 'x'", false)]
    [TestCase("taskkill /F /T /PID 1", false)]
    [TestCase("echo x", false)]
    [TestCase("exit 3", false)]
    [TestCase("[Console]::Error.WriteLine('x')", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void BuildPowerShellScript_CallOperator(string command, bool expectAmpersand)
    {
        const string pathEnv = @"C:\p";
        string script = AITPlatformHelper.BuildPowerShellScript(command, pathEnv);

        string prefix = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $env:CI = 'true'; $env:PATH = "
            + AITPlatformHelper.ToPowerShellSingleQuotedLiteral(pathEnv) + "; ";
        string body = script.Substring(prefix.Length);

        Assert.AreEqual(expectAmpersand, body.StartsWith("& "), script);
    }

    [Test]
    public void BuildPowerShellScript_KeepsDollarAndBacktickEscaping()
    {
        string script = AITPlatformHelper.BuildPowerShellScript("echo $HOME `ver`", @"C:\p");

        // $HOME → `$HOME, `ver` → ``ver`` (명령 부분만 이스케이프)
        StringAssert.Contains("echo `$HOME ``ver``", script);
        // $env: 프리픽스 자체는 이스케이프 대상이 아니다.
        StringAssert.Contains("$env:CI = 'true'", script);
        StringAssert.Contains("$env:PATH = ", script);
    }

    [Test]
    public void ToPowerShellSingleQuotedLiteral_DoublesAllSingleQuoteVariants()
    {
        Assert.AreEqual("'it''s'", AITPlatformHelper.ToPowerShellSingleQuotedLiteral("it's"));
        Assert.AreEqual("'a\u2018\u2018b'", AITPlatformHelper.ToPowerShellSingleQuotedLiteral("a\u2018b"));
        Assert.AreEqual("'a\u2019\u2019b'", AITPlatformHelper.ToPowerShellSingleQuotedLiteral("a\u2019b"));
        Assert.AreEqual("'a\u201A\u201Ab'", AITPlatformHelper.ToPowerShellSingleQuotedLiteral("a\u201Ab"));
        Assert.AreEqual("'a\u201B\u201Bb'", AITPlatformHelper.ToPowerShellSingleQuotedLiteral("a\u201Bb"));
    }

    // =====================================================
    // RedactSecrets / ExecuteCommand(sensitiveValues:) — 배포 키 로그 마스킹
    // =====================================================

    [Test]
    public void RedactSecrets_RawKey_IsMasked()
    {
        string text = "command --api-key AITSECRET-7f3a done";
        string result = AITPlatformHelper.RedactSecrets(text, new[] { "AITSECRET-7f3a" });

        Assert.IsFalse(result.Contains("AITSECRET-7f3a"), result);
        StringAssert.Contains(AITPlatformHelper.RedactedPlaceholder, result);
    }

    [Test]
    public void RedactSecrets_NullEmptyWhitespaceSecrets_ReturnInputUnchanged()
    {
        const string text = "unchanged text";
        Assert.AreEqual(text, AITPlatformHelper.RedactSecrets(text, null));
        Assert.AreEqual(text, AITPlatformHelper.RedactSecrets(text, new string[] { null, "", "   " }));
        Assert.IsNull(AITPlatformHelper.RedactSecrets(null, new[] { "x" }));
        Assert.AreEqual("", AITPlatformHelper.RedactSecrets("", new[] { "x" }));
    }

    [Test]
    public void RedactSecrets_ShellEscapedForms_AreMasked()
    {
        // $·백틱·끝 \ 를 포함한 키가 실제 셸 이스케이프된 형태 안에서도 완전히 마스킹되는지 확인한다
        // (배포 명령 형태를 그대로 재현). CreateProcessStartInfo는 실행 중인 OS의 셸(bash 또는
        // PowerShell) 한쪽만 타므로, Windows 분기는 BuildPowerShellArguments를 직접 호출해
        // macOS에서도 검증한다.
        const string key = "k$`\\";
        string command = "\"C:\\pnpm.cmd\" exec ait deploy --api-key \"" + key + "\" -m \"memo\"";

        var processInfo = AITPlatformHelper.CreateProcessStartInfo(command, null, null);
        string redacted = AITPlatformHelper.RedactSecrets(processInfo.Arguments, new[] { key });
        AssertNoSecretForms(redacted, key);

        string pathEnv = AITPlatformHelper.BuildPathEnv();
        string windowsShellArgs = AITPlatformHelper.BuildPowerShellArguments(command, pathEnv);
        string redactedWindows = AITPlatformHelper.RedactSecrets(windowsShellArgs, new[] { key });
        AssertNoSecretForms(redactedWindows, key);
    }

    private static void AssertNoSecretForms(string redacted, string key)
    {
        Assert.IsFalse(redacted.Contains(key), redacted);
        Assert.IsFalse(redacted.Contains(AITPlatformHelper.EscapeForPowerShell(key)), redacted);
        Assert.IsFalse(redacted.Contains(AITPlatformHelper.EscapeForBashDoubleQuotes(key)), redacted);
        StringAssert.Contains(AITPlatformHelper.RedactedPlaceholder, redacted);
    }

    [Test]
    public void ExecuteCommand_SensitiveValues_NotInLogsOrOutput()
    {
        const string secret = "AITSECRET-7f3a";
        var capturedLogs = new List<string>();
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (condition != null) capturedLogs.Add(condition);
        };
        Application.logMessageReceived += handler;

        AITPlatformHelper.CommandResult result;
        try
        {
            result = AITPlatformHelper.ExecuteCommand($"echo {secret}", verbose: true, sensitiveValues: new[] { secret });
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        Assert.IsTrue(result.Success, $"echo 명령은 성공해야 한다: {result.Error}");
        Assert.IsFalse(result.Output.Contains(secret), $"result.Output에 비밀값이 남아있으면 안 된다: {result.Output}");
        StringAssert.Contains(AITPlatformHelper.RedactedPlaceholder, result.Output);

        foreach (var log in capturedLogs)
        {
            Assert.IsFalse(log.Contains(secret), $"로그 라인에 비밀값이 남아있으면 안 된다: {log}");
        }
    }
}
