// -----------------------------------------------------------------------
// AITProcessExecutorTests.cs - 공통 프로세스 실행기 동작 검증
// Level 0: 실제 자식 프로세스를 spawn해 "성공/비정상 종료/타임아웃 Kill" 3경로를
//          검증한다. ProcessStartInfo는 ExecuteCommand가 쓰는 것과 동일한
//          AITPlatformHelper.CreateProcessStartInfo(셸 래핑)로 구성해 통합 경로를
//          그대로 탄다. 플랫폼 의존 명령은 IsWindows로 분기.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AppsInToss.Editor;

[TestFixture]
public class AITProcessExecutorTests
{
    // 셸 래핑된 ProcessStartInfo 생성 (ExecuteCommand와 동일 경로).
    private static ProcessStartInfo Psi(string command)
    {
        return AITPlatformHelper.CreateProcessStartInfo(command, null, null);
    }

    // Windows 전용 테스트가 쓰는, 공백 포함 임시 디렉터리 + dump.cmd. 다른 테스트(비-Windows 포함)에
    // 영향 없도록 SetUp/TearDown에서만 다루고, 정리는 best-effort(try/catch)로 한다.
    private string tempDirWithSpace;

    [SetUp]
    public void SetUp()
    {
        tempDirWithSpace = Path.Combine(Path.GetTempPath(), "ait quote test " + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDirWithSpace);

        // dump.cmd: PowerShell이 각 인자에 씌운 인용부호를 %N(비-%~N)으로 그대로 echo해 "인자가
        // 정확히 몇 개, 어떤 값으로 도착했는지"를 눈으로 확인한다. %~N을 쓰면 인용부호가 벗겨져
        // cmd 메타문자(& | ^ < >)가 다시 해석될 위험이 생기므로 반드시 %N을 쓴다.
        string dumpCmdPath = Path.Combine(tempDirWithSpace, "dump.cmd");
        File.WriteAllText(dumpCmdPath,
            "@echo off\r\n" +
            "echo(A1=[%1]\r\n" +
            "echo(A2=[%2]\r\n" +
            "echo(A3=[%3]\r\n" +
            "echo(A4=[%4]\r\n" +
            "echo(A5=[%5]\r\n");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (!string.IsNullOrEmpty(tempDirWithSpace) && Directory.Exists(tempDirWithSpace))
            {
                Directory.Delete(tempDirWithSpace, true);
            }
        }
        catch
        {
            // 임시 디렉터리 정리 실패는 테스트 결과에 영향을 주지 않는 best-effort.
        }
    }

    [Test]
    public void Run_EchoCommand_CapturesStdoutAndExitsZero()
    {
        var result = AITProcessExecutor.Run(Psi("echo ait-proc-marker"), 30000);

        Assert.IsFalse(result.TimedOut, "정상 종료 명령은 타임아웃이 아니어야 한다");
        Assert.AreEqual(0, result.ExitCode, "echo는 0으로 종료해야 한다");
        StringAssert.Contains("ait-proc-marker", result.StdOut, "stdout에 echo 출력이 캡처돼야 한다");
    }

    [Test]
    public void Run_NonZeroExit_ReportsExitCode()
    {
        // bash/powershell 모두 `exit N`으로 프로세스 종료 코드를 설정한다.
        var result = AITProcessExecutor.Run(Psi("exit 3"), 30000);

        Assert.IsFalse(result.TimedOut, "비정상 종료라도 타임아웃은 아니어야 한다");
        Assert.AreEqual(3, result.ExitCode, "exit 3의 종료 코드가 전달돼야 한다");
    }

    [Test]
    public void Run_StderrIsCaptured()
    {
        // 셸 무관하게 stderr로 메시지를 보낸다(파일 디스크립터 2 리다이렉트).
        string cmd = AITPlatformHelper.IsWindows
            ? "[Console]::Error.WriteLine('ait-err-marker')"
            : "echo ait-err-marker 1>&2";
        var result = AITProcessExecutor.Run(Psi(cmd), 30000);

        Assert.IsFalse(result.TimedOut);
        StringAssert.Contains("ait-err-marker", result.StdErr, "stderr가 캡처돼야 한다");
    }

    [Test]
    public void Run_LongRunningProcess_TimesOutAndKills()
    {
        // 2초 sleep을 300ms 타임아웃으로 — TimedOut=true, Kill 후 상한 drain으로
        // 무한 대기 없이 즉시 반환돼야 한다(장수명 자식 파이프 hang 방지 검증).
        string sleepCmd = AITPlatformHelper.IsWindows ? "Start-Sleep -Seconds 2" : "sleep 2";
        var sw = Stopwatch.StartNew();
        var result = AITProcessExecutor.Run(Psi(sleepCmd), 300);
        sw.Stop();

        Assert.IsTrue(result.TimedOut, "타임아웃을 초과한 프로세스는 TimedOut이어야 한다");
        Assert.AreEqual(-1, result.ExitCode, "타임아웃 시 ExitCode는 -1 규약");
        // 타임아웃(300ms) + drain 상한(500ms)을 크게 넘기지 않아야 한다(2초 sleep 완료 대기 금지).
        Assert.Less(sw.ElapsedMilliseconds, 1800,
            "Kill 후 상한 drain으로 sleep 종료를 기다리지 않고 반환해야 한다");
    }

    // =====================================================
    // Windows: 실제 powershell.exe → dump.cmd 인자 전달 검증
    // (포럼 제보 재현 — 공백 포함 사용자 경로의 pnpm + memo가 argv 하나로 정확히 도착하는지)
    // =====================================================

    [Test]
    public void Windows_QuotedPathAndSpacedArgs_ArriveAsSingleArguments()
    {
        Assume.That(AITPlatformHelper.IsWindows, "실제 powershell.exe → dump.cmd 인자 전달 검증은 Windows에서만 가능");

        string dumpCmdPath = Path.Combine(tempDirWithSpace, "dump.cmd");
        string command = $"\"{dumpCmdPath}\" --api-key \"k-1\" -m \"[Test] Fish & Chips | a^b <c> v1.0.0 - Unity SDK 3.2.0\"";

        var result = AITProcessExecutor.Run(Psi(command), 30000);

        Assert.IsFalse(result.TimedOut, "정상 종료 명령은 타임아웃이 아니어야 한다");
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}");
        // --api-key는 공백이 없어 PowerShell이 네이티브 인자로 넘길 때 인용부호를 씌우지 않는다.
        StringAssert.Contains("A1=[--api-key]", result.StdOut);
        StringAssert.Contains("A2=[k-1]", result.StdOut);
        // memo는 공백을 포함하므로 PowerShell이 네이티브 인자 경계 보존을 위해 인용부호를 씌운다.
        StringAssert.Contains("A4=[\"[Test] Fish & Chips | a^b <c> v1.0.0 - Unity SDK 3.2.0\"]", result.StdOut);
        // 여분의 인자가 없다 — 제보된 증상("Extraneous positional argument")의 정반대 결과.
        StringAssert.Contains("A5=[]", result.StdOut);
    }

    [Test]
    public void Windows_AdditionalPathWithSpace_IsUsedInScriptPath()
    {
        Assume.That(AITPlatformHelper.IsWindows, "additionalPaths를 통한 공백 포함 디렉터리 PATH 해석은 Windows에서만 가능");

        var psi = AITPlatformHelper.CreateProcessStartInfo("dump.cmd hello", null, new[] { tempDirWithSpace });
        var result = AITProcessExecutor.Run(psi, 30000);

        Assert.IsFalse(result.TimedOut);
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}");
        StringAssert.Contains("A1=[hello]", result.StdOut);
    }

    [Test]
    public void Windows_PowerShellQuotedLiterals_Survive()
    {
        Assume.That(AITPlatformHelper.IsWindows, "PowerShell 인용 리터럴 검증은 Windows에서만 가능");

        var result = AITProcessExecutor.Run(Psi("Write-Output \"a b\" 'c d'"), 30000);

        Assert.IsFalse(result.TimedOut);
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}");
        StringAssert.Contains("a b", result.StdOut);
        StringAssert.Contains("c d", result.StdOut);
    }

    [Test]
    public void Windows_PathWithSmartSingleQuote_RoundTrips()
    {
        Assume.That(AITPlatformHelper.IsWindows, "ToPowerShellSingleQuotedLiteral의 실제 PowerShell 왕복 검증은 Windows에서만 가능");

        // U+2019(스마트 홑따옴표)를 포함한 디렉터리 — $env:PATH는 ToPowerShellSingleQuotedLiteral로
        // 단일 인용되므로, 이 문자가 실제 PowerShell 5.1 파서에서 올바르게 왕복되는지 확인한다.
        // $는 항상 이스케이프되므로 [Environment]::GetEnvironmentVariable을 쓴다(설계 문서 참조).
        string smartQuoteDir = Path.Combine(Path.GetTempPath(), "ait’q " + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(smartQuoteDir);
        try
        {
            var psi = AITPlatformHelper.CreateProcessStartInfo(
                "[Console]::Out.Write([Environment]::GetEnvironmentVariable('PATH'))",
                null,
                new[] { smartQuoteDir });
            var result = AITProcessExecutor.Run(psi, 30000);

            Assert.IsFalse(result.TimedOut);
            Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}");
            StringAssert.StartsWith(smartQuoteDir, result.StdOut);
        }
        finally
        {
            try { Directory.Delete(smartQuoteDir, true); } catch { /* best-effort 정리 */ }
        }
    }

    // =====================================================
    // 크로스 플랫폼: additionalEnvVars의 JSON 값이 손상 없이 전달되는지
    // (AITAsyncCommandRunner가 CreateProcessStartInfo를 공유하게 된 뒤의 UNITY_METADATA 회귀 테스트 겸용)
    // =====================================================

    [Test]
    public void CreateProcessStartInfo_EnvJsonValue_IsPreservedExactly()
    {
        const string jsonValue = "{\"a\":\"b c\"}";
        var additionalEnvVars = new Dictionary<string, string> { { "AIT_QT", jsonValue } };
        string command = AITPlatformHelper.IsWindows
            ? "[Console]::Out.Write([Environment]::GetEnvironmentVariable('AIT_QT'))"
            : "printenv AIT_QT";

        var psi = AITPlatformHelper.CreateProcessStartInfo(command, null, null, additionalEnvVars);
        var result = AITProcessExecutor.Run(psi, 30000);

        Assert.IsFalse(result.TimedOut);
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}");
        string output = AITPlatformHelper.IsWindows ? result.StdOut : result.StdOut.Trim();
        Assert.AreEqual(jsonValue, output, "JSON 값의 큰따옴표가 손상 없이 전달돼야 한다(UNITY_METADATA 회귀 방지)");
    }
}
