// -----------------------------------------------------------------------
// PortResolverParserTests.cs
// Level 0: PortResolver.IsPortConflictError 순수 파서 검증
// Level 0/2: PortResolver.ParseListeningPids (순수) + Windows netstat/프로세스 종료 실경로 검증
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using AppsInToss.Editor;
using AppsInToss.Editor.Menu;

[TestFixture]
public class PortResolverParserTests
{
    [Test]
    public void IsPortConflictError_EaddrInUse_ReturnsTrue()
    {
        Assert.IsTrue(PortResolver.IsPortConflictError("Error: listen EADDRINUSE: address already in use :::5173"));
    }

    [Test]
    public void IsPortConflictError_PortIsAlreadyInUseMessage_ReturnsTrue()
    {
        Assert.IsTrue(PortResolver.IsPortConflictError("Port is already in use"));
    }

    [Test]
    public void IsPortConflictError_AddressAlreadyInUseMessage_ReturnsTrue()
    {
        Assert.IsTrue(PortResolver.IsPortConflictError("bind: address already in use"));
    }

    [Test]
    public void IsPortConflictError_NormalOutput_ReturnsFalse()
    {
        Assert.IsFalse(PortResolver.IsPortConflictError("Build succeeded in 1.2s"));
    }

    [Test]
    public void IsPortConflictError_EmptyString_ReturnsFalse()
    {
        Assert.IsFalse(PortResolver.IsPortConflictError(string.Empty));
    }

    [Test]
    public void IsPortConflictError_Null_ReturnsFalse()
    {
        Assert.IsFalse(PortResolver.IsPortConflictError(null));
    }

    // ToLowerInvariant() 계약 고정: 3개 패턴 모두 완전 대문자 입력도 매칭되어야 함
    [TestCase("EADDRINUSE")]
    [TestCase("PORT IS ALREADY IN USE")]
    [TestCase("ADDRESS ALREADY IN USE")]
    public void IsPortConflictError_AllUppercase_ReturnsTrue(string input)
    {
        Assert.IsTrue(PortResolver.IsPortConflictError(input));
    }

    [Test]
    public void IsPortConflictError_SubstringInMultiLineOutput_ReturnsTrue()
    {
        // 실제 툴 출력은 다중 라인 스택트레이스일 수 있음. 트리거가 중간 라인에 있어도 감지되어야 함.
        string output = "Starting dev server...\n  at node (internal)\nError: listen EADDRINUSE on :::5173\n  at Server.listen";
        Assert.IsTrue(PortResolver.IsPortConflictError(output));
    }

    // 느슨한 문구 매칭으로 false positive가 생기지 않아야 함
    [TestCase("the address has already been used")]
    [TestCase("port already in use")] // "is" 누락
    public void IsPortConflictError_NearMissPhrase_ReturnsFalse(string input)
    {
        Assert.IsFalse(PortResolver.IsPortConflictError(input));
    }

    [Test]
    public void IsPortConflictError_WhitespaceOnly_ReturnsFalse()
    {
        Assert.IsFalse(PortResolver.IsPortConflictError("   "));
        Assert.IsFalse(PortResolver.IsPortConflictError("\n"));
        Assert.IsFalse(PortResolver.IsPortConflictError("\t"));
    }

    // 실제 `netstat -ano` 컬럼 레이아웃(Proto/로컬 주소/외부 주소/상태/PID)을 그대로 모사한 픽스처.
    // 한글 헤더 + CRLF 개행 + 로케일이 다른 LISTEN 상태 문자열(ABHÖREN)까지 포함해
    // ParseListeningPids가 상태 문자열이 아니라 "원격 포트 0" 규칙으로만 LISTEN을 판별함을 검증한다.
    private const string MixedNetstatFixture =
        "\r\n" +
        "활성 연결\r\n" +
        "\r\n" +
        "  프로토콜  로컬 주소              외부 주소              상태           PID\r\n" +
        "  TCP    0.0.0.0:8081           0.0.0.0:0              LISTENING       1234\r\n" +
        "  TCP    [::]:8081              [::]:0                 LISTENING       5555\r\n" +
        "  TCP    [::]:8081              [::]:0                 LISTENING       1234\r\n" +
        "  TCP    127.0.0.1:80810        0.0.0.0:0              LISTENING       9999\r\n" +
        "  TCP    127.0.0.1:54321        127.0.0.1:8081         ESTABLISHED     4321\r\n" +
        "  TCP    127.0.0.1:8081         127.0.0.1:52000        TIME_WAIT       6001\r\n" +
        "  TCP    0.0.0.0:8081           0.0.0.0:0              ABHÖREN         0\r\n" +
        "  TCP    0.0.0.0:8081           0.0.0.0:0              LISTENING       4\r\n" +
        "  TCP    0.0.0.0:8081           0.0.0.0:0              LISTENING       7777\r\n" +
        "  UDP    0.0.0.0:8081           *:*                                    777\r\n";

    [Test]
    public void ParseListeningPids_MixedFixture_ReturnsOnlyListeningOwners()
    {
        // 8081 포트를 LISTEN 중인 건 IPv4(1234)와 IPv6(5555)뿐이다.
        // - 80810(다른 포트)·127.0.0.1:8081이 "원격" 주소인 ESTABLISHED 행·TIME_WAIT은 제외되어야 하고,
        // - IPv6 쪽의 1234 중복 행은 한 번만 세어야 하며,
        // - PID 0/4(시스템 예약)와 excludedPid(7777, 자기 자신 가정)는 제외되어야 하고,
        // - UDP 행과 ABHÖREN(독일어 LISTENING) 상태 표기는 TCP 여부/원격 포트 0 규칙만으로 걸러져야 한다.
        var result = PortResolver.ParseListeningPids(MixedNetstatFixture, 8081, excludedPid: 7777);

        Assert.That(result, Is.EqualTo(new List<int> { 1234, 5555 }));
    }

    [Test]
    public void ParseListeningPids_NullEmptyOrPortZero_ReturnsEmpty()
    {
        Assert.That(PortResolver.ParseListeningPids(null, 8081, -1), Is.Empty);
        Assert.That(PortResolver.ParseListeningPids(string.Empty, 8081, -1), Is.Empty);
        Assert.That(PortResolver.ParseListeningPids(MixedNetstatFixture, 0, -1), Is.Empty);
    }

    [Test]
    public void CreateNetstatStartInfo_RunsOnWindows()
    {
        Assume.That(AITPlatformHelper.IsWindows, "netstat.exe 실행 경로 검증은 Windows 에서만 가능");

        // %SystemRoot%\System32\netstat.exe 를 리터럴로 쓰던 예전 버그의 회귀 테스트:
        // CreateProcess(UseShellExecute=false)는 환경변수를 확장하지 않으므로, 실제로 프로세스가
        // 뜨고 정상 종료해야만 이 테스트가 통과한다.
        var run = AITProcessExecutor.Run(PortResolver.CreateNetstatStartInfo(), 3000);

        Assert.That(run.TimedOut, Is.False);
        Assert.That(run.ExitCode, Is.EqualTo(0));
        Assert.That(run.StdOut, Does.Contain("TCP"));
    }

    [Test]
    public void FindListeningPidsWindows_FindsOwnListener_AndHonorsExclusion()
    {
        Assume.That(AITPlatformHelper.IsWindows, "실제 netstat/TcpListener 상호작용 검증은 Windows 에서만 가능");

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            int selfPid = Process.GetCurrentProcess().Id;

            Assert.That(PortResolver.FindListeningPidsWindows(port, -1), Does.Contain(selfPid));
            CollectionAssert.DoesNotContain(PortResolver.FindListeningPidsWindows(port, selfPid), selfPid);

            // Unity(테스트를 실행 중인 이 프로세스)는 node/pnpm/npm 계열이 아니므로
            // KillProcessOnPort가 자기 자신은 건너뛰고, 리스너는 살아있어야 한다.
            Assert.DoesNotThrow(() => PortResolver.KillProcessOnPort(port));
            Assert.That(listener.Server.IsBound, Is.True);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public void KillProcessOnPort_UnusedPort_ReturnsQuickly()
    {
        Assume.That(AITPlatformHelper.IsWindows, "실제 netstat 호출 경로 타이밍 검증은 Windows 에서만 가능");

        // 임시로 열었다 바로 닫은 포트 — 실사용 중이 아니므로 netstat 결과에 LISTEN 행이 없어야 한다.
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var stopwatch = Stopwatch.StartNew();
        Assert.DoesNotThrow(() => PortResolver.KillProcessOnPort(port));
        stopwatch.Stop();

        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }
}
