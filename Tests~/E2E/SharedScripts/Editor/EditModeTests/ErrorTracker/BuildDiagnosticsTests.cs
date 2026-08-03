// ---------------------------------------------------------------------------
// BuildDiagnosticsTests.cs - AITBuildDiagnostics 단위 테스트 (계획 §5)
// pnpm/granite 등 외부 명령의 마지막 실패(exit code + stderr 말미)를 보관해
// 상위 단일 빌드 실패 Sentry 캡처(CaptureBuildError)에 extra로 첨부하는 버퍼 검증.
// 정책: 실패 시 RecordFailure(마지막 1건), 성공 시 ClearOnSuccess, 캡처 시 ConsumeForCapture(소비-once).
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class BuildDiagnosticsTests
{
    [SetUp]
    public void Reset()
    {
        // 정적 시계 시드를 실제 시각으로 복원(직전 테스트가 가짜 시각을 주입했을 수 있음).
        AITBuildDiagnostics.UtcNowProvider = () => System.DateTime.UtcNow;
        // 정적 버퍼이므로 각 테스트 전에 비워 상호 간섭을 막는다.
        AITBuildDiagnostics.ClearOnSuccess();
    }

    [TearDown]
    public void Drain()
    {
        // 혹시 남은 기록이 다음 테스트로 새지 않도록 소비.
        AITBuildDiagnostics.ConsumeForCapture();
        // 가짜 시계가 다음 테스트로 누수되지 않도록 복원.
        AITBuildDiagnostics.UtcNowProvider = () => System.DateTime.UtcNow;
    }

    #region 기본 기록/소비

    [Test]
    public void NoFailureRecorded_ConsumeReturnsNull()
    {
        Assert.IsNull(AITBuildDiagnostics.ConsumeForCapture());
    }

    [Test]
    public void RecordFailure_ConsumeReturnsStageExitAndStderr()
    {
        AITBuildDiagnostics.RecordFailure("pnpm install", 1, "ERR_PNPM_FROZEN_LOCKFILE: lockfile drift");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("stage: pnpm install", diag);
        StringAssert.Contains("exit_code: 1", diag);
        StringAssert.Contains("stderr_tail:", diag);
        StringAssert.Contains("ERR_PNPM_FROZEN_LOCKFILE", diag);
    }

    [Test]
    public void Consume_IsConsumeOnce()
    {
        AITBuildDiagnostics.RecordFailure("granite build", 2, "some build error");

        Assert.IsNotNull(AITBuildDiagnostics.ConsumeForCapture());
        // 두 번째 호출은 비어 있어야 한다(소비-once) — 무관한 후속 캡처에 재첨부 방지.
        Assert.IsNull(AITBuildDiagnostics.ConsumeForCapture());
    }

    #endregion

    #region 성공 시 초기화 (복구된 폴백 단계 stale 제거)

    [Test]
    public void ClearOnSuccess_DiscardsPriorFailure()
    {
        AITBuildDiagnostics.RecordFailure("pnpm install (frozen)", 1, "frozen lockfile failed");
        AITBuildDiagnostics.ClearOnSuccess();

        Assert.IsNull(AITBuildDiagnostics.ConsumeForCapture(),
            "성공으로 복구되면 직전 폴백 단계 실패 진단은 비워져야 한다.");
    }

    #endregion

    #region 마지막 1건 유지 (터미널 실패가 남음)

    [Test]
    public void RecordFailure_KeepsMostRecent()
    {
        AITBuildDiagnostics.RecordFailure("pnpm install (frozen)", 1, "first stage stderr");
        AITBuildDiagnostics.RecordFailure("pnpm install (clean retry)", 7, "final stage stderr");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("stage: pnpm install (clean retry)", diag);
        StringAssert.Contains("exit_code: 7", diag);
        StringAssert.Contains("final stage stderr", diag);
        StringAssert.DoesNotContain("first stage stderr", diag);
    }

    #endregion

    #region stderr 폴백 / 마스킹 / 절단

    [Test]
    public void EmptyStderr_FallsBackToStdout()
    {
        AITBuildDiagnostics.RecordFailure("granite build", 1, stderr: "", stdout: "only on stdout");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("only on stdout", diag);
    }

    [Test]
    public void StderrAndStdoutEmpty_StillRecordsStageAndExit()
    {
        AITBuildDiagnostics.RecordFailure("pnpm install (timeout 300s)", -1, stderr: null, stdout: null);

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("stage: pnpm install (timeout 300s)", diag);
        StringAssert.Contains("exit_code: -1", diag);
        // stderr이 없으면 stderr_tail 섹션은 생략된다.
        StringAssert.DoesNotContain("stderr_tail:", diag);
    }

    [Test]
    public void UserHomePath_IsMasked()
    {
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            Assert.Ignore("이 환경에서는 홈 경로를 조회할 수 없어 마스킹 검증을 건너뜀.");

        AITBuildDiagnostics.RecordFailure("pnpm install", 1,
            $"ENOENT: no such file at {home}/proj/node_modules/.bin");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("<home>", diag);
        StringAssert.DoesNotContain(home, diag);
    }

    [Test]
    public void LongStderr_IsTruncatedToTail()
    {
        // 앞부분은 잘리고 말미만 남아야 한다 — 절단 마커 + 말미 보존 검증.
        string head = new string('A', 4000);
        string tail = "TAIL_MARKER_END_OF_STDERR";
        AITBuildDiagnostics.RecordFailure("pnpm install", 1, head + tail);

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("…(truncated)", diag);
        StringAssert.Contains(tail, diag, "stderr 말미는 보존되어야 한다.");
        Assert.Less(diag.Length, head.Length, "절단으로 전체 길이가 원본보다 짧아야 한다.");
    }

    #endregion

    #region stage 캡 / 마스킹 / sentinel

    [Test]
    public void LongStage_IsCapped()
    {
        // stage 라벨이 비정상적으로 길어도(외부 명령 인자 폭주 등) extra가 부풀지 않도록 캡된다.
        string longStage = new string('S', 500);
        AITBuildDiagnostics.RecordFailure(longStage, 1, "short err");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("…", diag, "캡 시 절단 마커(…)가 붙어야 한다.");
        StringAssert.DoesNotContain(longStage, diag, "원본 길이의 stage가 그대로 실리면 안 된다.");
    }

    [Test]
    public void UserHomePath_InStage_IsMasked()
    {
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            Assert.Ignore("이 환경에서는 홈 경로를 조회할 수 없어 stage 마스킹 검증을 건너뜀.");

        AITBuildDiagnostics.RecordFailure($"pnpm install (cwd {home}/proj)", 1, "boom");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("<home>", diag);
        StringAssert.DoesNotContain(home, diag, "stage에 박힌 홈 경로도 마스킹되어야 한다.");
    }

    [Test]
    public void NullStage_RendersUnknownSentinel()
    {
        AITBuildDiagnostics.RecordFailure(null, 5, "some error");

        string diag = AITBuildDiagnostics.ConsumeForCapture();

        Assert.IsNotNull(diag);
        StringAssert.Contains("stage: (unknown)", diag);
        StringAssert.Contains("exit_code: 5", diag);
    }

    #endregion

    #region 신선도 창 (도메인 리로드 / 세션 교체 stale 차단)

    [Test]
    public void FreshWithinWindow_ReturnsContent()
    {
        var t0 = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        AITBuildDiagnostics.UtcNowProvider = () => t0;
        AITBuildDiagnostics.RecordFailure("pnpm install", 1, "boom");

        // 임계(30분) 직전 — 여전히 신선.
        AITBuildDiagnostics.UtcNowProvider = () => t0.AddMinutes(29);

        Assert.IsNotNull(AITBuildDiagnostics.ConsumeForCapture(),
            "신선도 창 안의 진단은 반환되어야 한다.");
    }

    [Test]
    public void StaleBeyondWindow_ReturnsNullAndClears()
    {
        var t0 = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        AITBuildDiagnostics.UtcNowProvider = () => t0;
        AITBuildDiagnostics.RecordFailure("pnpm install", 1, "boom");

        // 임계(30분) 초과 — 도메인 리로드/세션 교체로 남은 stale로 간주.
        AITBuildDiagnostics.UtcNowProvider = () => t0.AddMinutes(31);

        Assert.IsNull(AITBuildDiagnostics.ConsumeForCapture(),
            "신선도 창을 벗어난 진단은 무관한 후속 빌드에 첨부되면 안 된다.");
        // stale도 소비-once로 비워져, 시계를 되돌려도 재등장하지 않아야 한다.
        AITBuildDiagnostics.UtcNowProvider = () => t0;
        Assert.IsNull(AITBuildDiagnostics.ConsumeForCapture(),
            "stale 소비 후 버퍼는 비워져야 한다.");
    }

    #endregion
}
