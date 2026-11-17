// -----------------------------------------------------------------------
// VitePollDecisionTests.cs
// Level 0: PortResolver.EvaluateVitePollDecision 순수 함수 검증
// Sentry 이슈 APPS-IN-TOSS-UNITY-SDK-7D 회귀 방지 — Vite 포트 5173 대기 타임아웃
// 경계 (15초 → 60초로 상향) 와 폴링 인터벌 동작이 시간 진행만으로 결정되는지 고정.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.Menu;

[TestFixture]
public class VitePollDecisionTests
{
    private const double Interval = PortResolver.DefaultViteWaitIntervalSeconds;
    private const double MaxWait = PortResolver.DefaultViteWaitMaxSeconds;

    [Test]
    public void DefaultMaxWaitIsAtLeastSixtySeconds()
    {
        // 15초 타임아웃이 부족했던 회귀 — 60초 이상이어야 함
        Assert.GreaterOrEqual(MaxWait, 60.0,
            "Vite 포트 대기 기본 타임아웃은 60초 이상이어야 한다 (cold-start 환경 대응)");
    }

    [Test]
    public void Wait_WhenIntervalNotElapsed()
    {
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: 0.1, lastCheckSeconds: 0.0,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreEqual(PortResolver.VitePollDecision.Wait, decision);
    }

    [Test]
    public void CheckPort_WhenIntervalElapsed()
    {
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: Interval + 0.01, lastCheckSeconds: 0.0,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreEqual(PortResolver.VitePollDecision.CheckPort, decision);
    }

    [Test]
    public void CheckPort_WhenIntervalElapsedSinceLastCheck()
    {
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: 5.0, lastCheckSeconds: 5.0 - Interval,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreEqual(PortResolver.VitePollDecision.CheckPort, decision);
    }

    [Test]
    public void Wait_WhenJustChecked()
    {
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: 5.0, lastCheckSeconds: 4.9,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreEqual(PortResolver.VitePollDecision.Wait, decision);
    }

    [Test]
    public void Timeout_WhenElapsedExceedsMaxWait()
    {
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: MaxWait + 0.1, lastCheckSeconds: MaxWait - 1.0,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreEqual(PortResolver.VitePollDecision.Timeout, decision);
    }

    // 회귀 회피의 핵심: 사용자 환경에서 15초가 부족했던 케이스가 60초 기본값으로 흡수되는지 확인
    [Test]
    public void NotTimeout_AtFifteenSeconds_WithDefaultMaxWait()
    {
        // 과거 15초 타임아웃에서 발생하던 시점에 새 기본값(60s)에서는 아직 폴링이 진행되어야 함
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: 15.5, lastCheckSeconds: 15.0,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreNotEqual(PortResolver.VitePollDecision.Timeout, decision,
            "기본 타임아웃이 60초 이상이어야 15초 시점에는 타임아웃이 발생하지 않아야 한다");
    }

    [Test]
    public void Timeout_BoundaryAtMaxWait()
    {
        // elapsed == maxWait 정확히 같을 때는 타임아웃 아님 (>로 비교)
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: MaxWait, lastCheckSeconds: MaxWait - Interval,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: Interval);
        Assert.AreNotEqual(PortResolver.VitePollDecision.Timeout, decision);
    }

    [Test]
    public void Timeout_TakesPrecedenceOverInterval()
    {
        // 타임아웃이 인터벌 검사보다 우선 — checkInterval이 매우 커도 타임아웃은 발생해야 함
        var decision = PortResolver.EvaluateVitePollDecision(
            elapsedSeconds: MaxWait + 1.0, lastCheckSeconds: MaxWait + 0.99,
            maxWaitSeconds: MaxWait, checkIntervalSeconds: 100.0);
        Assert.AreEqual(PortResolver.VitePollDecision.Timeout, decision);
    }
}
