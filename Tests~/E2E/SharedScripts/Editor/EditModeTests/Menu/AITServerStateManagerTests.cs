// -----------------------------------------------------------------------
// AITServerStateManagerTests.cs
// Level 0: AITServerStateManager.ShouldClearPersistedState 순수 함수 검증
//
// 배경: Dev 서버가 시작되어 프로세스는 살아있지만 아직 포트를 열기 전인
// 순간(SetExpectedPortAndProcess ~ OnServerStarted 사이)에 ValidateState()가
// 호출되면, 예전에는 portInUse == false 라는 이유만으로 영속 상태(EditorPrefs)를
// 무조건 지워버려 서버가 실제로는 실행 중인데도 메뉴가 영구히 NotRunning으로
// 굳는 문제가 있었다. PID가 살아있으면 지우지 않도록 판단 로직을 순수 함수로
// 분리해 회귀를 막는다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITServerStateManagerTests
{
    [Test]
    public void ShouldClearPersistedState_PortInUse_ReturnsFalse()
    {
        // 포트가 열려 있으면 PID/isAlive 값과 무관하게 절대 지우지 않는다.
        Assert.IsFalse(AITServerStateManager.ShouldClearPersistedState(
            portInUse: true, pid: 0, isAlive: _ => false));
    }

    [Test]
    public void ShouldClearPersistedState_PortNotInUse_PidAlive_ReturnsFalse()
    {
        // 핵심 회귀 케이스: 서버 프로세스는 살아있지만 아직 포트를 열지 않은
        // 시작 중 상태 — 상태를 보존해야 다음 검증에서 자동 복구된다.
        Assert.IsFalse(AITServerStateManager.ShouldClearPersistedState(
            portInUse: false, pid: 1234, isAlive: pid => pid == 1234));
    }

    [Test]
    public void ShouldClearPersistedState_PortNotInUse_PidDead_ReturnsTrue()
    {
        // PID가 이미 종료된 경우(서버가 죽었거나 정상 종료됨) 정리해야 한다.
        Assert.IsTrue(AITServerStateManager.ShouldClearPersistedState(
            portInUse: false, pid: 1234, isAlive: _ => false));
    }

    [Test]
    public void ShouldClearPersistedState_PortNotInUse_NoPid_ReturnsTrue()
    {
        // 추적할 PID 자체가 없으면(0 이하) 정리해야 한다.
        Assert.IsTrue(AITServerStateManager.ShouldClearPersistedState(
            portInUse: false, pid: 0, isAlive: _ => true));
    }

    [Test]
    public void ShouldClearPersistedState_PortNotInUse_NegativePid_ReturnsTrue()
    {
        Assert.IsTrue(AITServerStateManager.ShouldClearPersistedState(
            portInUse: false, pid: -1, isAlive: _ => true));
    }

    [Test]
    public void ShouldClearPersistedState_NullIsAliveFunc_TreatsAsDead()
    {
        // isAlive 콜백이 null이면(방어적 널 체크) 죽은 것으로 간주해 정리한다.
        Assert.IsTrue(AITServerStateManager.ShouldClearPersistedState(
            portInUse: false, pid: 1234, isAlive: null));
    }

    [Test]
    public void ShouldClearPersistedState_PortInUse_OverridesDeadPid()
    {
        // portInUse가 최우선 신호: PID가 죽은 것으로 보고돼도 포트가 열려 있으면 지우지 않는다.
        Assert.IsFalse(AITServerStateManager.ShouldClearPersistedState(
            portInUse: true, pid: 1234, isAlive: _ => false));
    }
}
