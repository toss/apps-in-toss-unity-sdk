// -----------------------------------------------------------------------
// SdkSelfCompileGuardTests.cs - SDK 자체 컴파일 에러 defer+confirm 게이트 회귀 테스트 (#42)
// Level 0: AITSdkSelfCompileGuard의 순수 판정 로직 검증 (클럭/에디터/도메인 리로드 무의존)
//   - IsSdkSelfCompileError: SDK 패키지 경로 컴파일 에러만 게이트 대상으로 분류
//   - Decide: 성공 리로드(transient) → Drop, 리로드 없이 settle+grace(실 회귀) → Capture
// 배경: UPM 임포트 중 일시적 CS0103/CS0234가 0-user 노이즈로 Sentry 유입
//   (SDK-133/130/131/12Z/12P/12W/12V/12T/12S). 도메인 리로드 생존 여부로 판별.
// -----------------------------------------------------------------------

using System;
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
public class SdkSelfCompileGuardTests
{
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

    private static readonly DateTime Base = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static long Ms(DateTime dt)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }

    // ── IsSdkSelfCompileError 분류 ───────────────────────────────────────

    [Test]
    public void IsSdkSelfCompileError_PackageCacheCompileError_True()
    {
        // 실제 Sentry 유입 형태(SDK-133): PackageCache 경로 + error CS0103
        const string msg =
            @"Library\PackageCache\im.toss.apps-in-toss-unity-sdk@b5594c837ee7\Editor\ErrorTracker\AITEditorErrorTracker.cs(1416,47): error CS0103: The name 'AITVersion' does not exist in the current context";
        Assert.IsTrue(AITSdkSelfCompileGuard.IsSdkSelfCompileError(msg));
    }

    [Test]
    public void IsSdkSelfCompileError_ComTossPackagePath_True()
    {
        const string msg =
            "Library/PackageCache/com.toss.apps-in-toss-unity-sdk@abc/Runtime/X.cs(1,1): error CS0234: The type or namespace name 'AITVersion' does not exist in the namespace 'AppsInToss'";
        Assert.IsTrue(AITSdkSelfCompileGuard.IsSdkSelfCompileError(msg));
    }

    [Test]
    public void IsSdkSelfCompileError_UserProjectCompileError_False()
    {
        // 사용자 프로젝트(Assets/) 컴파일 에러는 SDK 패키지 경로가 없으므로 게이트 대상 아님
        // (SDK 브레이킹 체인지 경로가 별도 처리).
        const string msg =
            "Assets/Scripts/Foo.cs(10,5): error CS0103: The name 'AppsInToss' does not exist in the current context";
        Assert.IsFalse(AITSdkSelfCompileGuard.IsSdkSelfCompileError(msg));
    }

    [Test]
    public void IsSdkSelfCompileError_WarningInSdkPath_False()
    {
        // 경고(warning CS)는 컴파일을 막지 않으므로 게이트 대상 아님.
        const string msg =
            "Library/PackageCache/im.toss.apps-in-toss-unity-sdk@abc/Editor/X.cs(1,1): warning CS0168: variable declared but never used";
        Assert.IsFalse(AITSdkSelfCompileGuard.IsSdkSelfCompileError(msg));
    }

    [Test]
    public void IsSdkSelfCompileError_NullOrEmpty_False()
    {
        Assert.IsFalse(AITSdkSelfCompileGuard.IsSdkSelfCompileError(null));
        Assert.IsFalse(AITSdkSelfCompileGuard.IsSdkSelfCompileError(string.Empty));
    }

    // ── Decide 판정 ──────────────────────────────────────────────────────

    private static AITSdkSelfCompileGuard.GateDecision Decide(
        DateTime now, int recordGen, int currentGen, bool settled,
        string recUnity = "6000.2", string curUnity = "6000.2",
        string recSdk = "1.2.3", string curSdk = "1.2.3")
    {
        return AITSdkSelfCompileGuard.Decide(
            firstSeenUnixMs: Ms(Base),
            recordReloadGen: recordGen,
            recordUnityVer: recUnity,
            recordSdkVer: recSdk,
            nowUtc: now,
            currentReloadGen: currentGen,
            isSettled: settled,
            currentUnityVer: curUnity,
            currentSdkVer: curSdk,
            staleThreshold: Stale,
            minGrace: Grace);
    }

    [Test]
    public void Decide_SuccessfulReloadHappened_Drops()
    {
        // 성공적인 도메인 리로드(currentGen > recordGen) == SDK가 깨끗하게 컴파일됨 == transient → Drop.
        // settle됐고 grace를 넘겼어도 리로드 신호가 우선한다.
        var d = Decide(now: Base.AddSeconds(10), recordGen: 5, currentGen: 6, settled: true);
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Drop, d);
    }

    [Test]
    public void Decide_SettledNoReloadPastGrace_Captures()
    {
        // settle됐고 성공 리로드 없이 grace 경과 == 컴파일이 끝내 실패한 실 회귀 → Capture.
        var d = Decide(now: Base.AddSeconds(10), recordGen: 5, currentGen: 5, settled: true);
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Capture, d);
    }

    [Test]
    public void Decide_NotSettled_Waits()
    {
        // 컴파일/임포트가 아직 진행 중이면 판정 보류.
        var d = Decide(now: Base.AddSeconds(10), recordGen: 5, currentGen: 5, settled: false);
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Wait, d);
    }

    [Test]
    public void Decide_SettledWithinGrace_Waits()
    {
        // settle됐지만 아직 grace 이내 → 보류(디바운스).
        var d = Decide(now: Base.AddSeconds(2), recordGen: 5, currentGen: 5, settled: true);
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Wait, d);
    }

    [Test]
    public void Decide_Stale_Drops()
    {
        // 30분 신선도 창을 넘긴 미확정 기록은 드롭(무한 보류 방지).
        var d = Decide(now: Base.AddMinutes(31), recordGen: 5, currentGen: 5, settled: true);
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Drop, d);
    }

    [Test]
    public void Decide_StaleAndReloadHappened_Drops()
    {
        // stale 창과 성공 리로드 신호가 동시에 참인 경우에도 결과는 Drop으로 일관(둘 다 transient/폐기 신호).
        var d = Decide(now: Base.AddMinutes(31), recordGen: 5, currentGen: 6, settled: true);
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Drop, d);
    }

    [Test]
    public void Decide_UnityVersionChanged_Drops()
    {
        // 버전 전환 자체가 일시적 에러의 원인일 수 있어 동일 맥락이 아니므로 드롭.
        var d = Decide(now: Base.AddSeconds(10), recordGen: 5, currentGen: 5, settled: true,
            recUnity: "2021.3.45f1", curUnity: "6000.2.1f1");
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Drop, d);
    }

    [Test]
    public void Decide_SdkVersionChanged_Drops()
    {
        var d = Decide(now: Base.AddSeconds(10), recordGen: 5, currentGen: 5, settled: true,
            recSdk: "1.2.3", curSdk: "1.3.0");
        Assert.AreEqual(AITSdkSelfCompileGuard.GateDecision.Drop, d);
    }
}
