// -----------------------------------------------------------------------
// AITProcessExecutorTests.cs - 공통 프로세스 실행기 동작 검증
// Level 0: 실제 자식 프로세스를 spawn해 "성공/비정상 종료/타임아웃 Kill" 3경로를
//          검증한다. ProcessStartInfo는 ExecuteCommand가 쓰는 것과 동일한
//          AITPlatformHelper.CreateProcessStartInfo(셸 래핑)로 구성해 통합 경로를
//          그대로 탄다. 플랫폼 의존 명령은 IsWindows로 분기.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.Diagnostics;
using AppsInToss.Editor;

[TestFixture]
public class AITProcessExecutorTests
{
    // 셸 래핑된 ProcessStartInfo 생성 (ExecuteCommand와 동일 경로).
    private static ProcessStartInfo Psi(string command)
    {
        return AITPlatformHelper.CreateProcessStartInfo(command, null, null);
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
}
