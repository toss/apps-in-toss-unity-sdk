// ---------------------------------------------------------------------------
// StrictErrorSourceGateTests.cs - strict error_source 게이트 단위 테스트
// OnLogMessageReceived 필터 체인 7.5 단계의 ShouldDropAsNonSdkSource 헬퍼 검증.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class StrictErrorSourceGateTests
{
    #region SDK 출처 → 통과 (false 반환)

    [Test]
    public void SdkStackFrame_DoesNotDrop()
    {
        string stackTrace =
            "at AppsInToss.Editor.AITConvertCore.DoExport () in Packages/im.toss.apps-in-toss-unity-sdk/Editor/AITConvertCore.cs:42";

        Assert.IsFalse(AITEditorErrorTracker.ShouldDropAsNonSdkSource("some sdk error", stackTrace));
    }

    [Test]
    public void NoStackTrace_AitKeywordInMessage_DoesNotDrop()
    {
        // 스택 없으면 메시지 기반 fallback이 SDK 키워드로 sdk 분류
        Assert.IsFalse(AITEditorErrorTracker.ShouldDropAsNonSdkSource("[AIT] build failed", null));
    }

    [Test]
    public void NoStackTrace_SentryPrefix_DoesNotDrop()
    {
        // Sentry transport 자체 에러는 sdk로 분류되어 통과
        Assert.IsFalse(AITEditorErrorTracker.ShouldDropAsNonSdkSource("Sentry: failed to flush envelope", ""));
    }

    [Test]
    public void NoStackTrace_SdkMessagePattern_DoesNotDrop()
    {
        // SdkMessagePatterns의 [Validation], [pnpm], webgl/Build/ 등은 sdk
        Assert.IsFalse(AITEditorErrorTracker.ShouldDropAsNonSdkSource("[Validation] failed", ""));
    }

    #endregion

    #region 사용자 프로젝트 출처 → 드롭 (true 반환)

    [Test]
    public void UserProjectStackFrame_Drops()
    {
        string stackTrace =
            "at MyGame.PlayerController.Start () in Assets/Scripts/PlayerController.cs:15";

        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource("user error", stackTrace));
    }

    #endregion

    #region 분류 불가 (unknown) → 드롭 (true 반환)

    [Test]
    public void NoStackTrace_NoSdkKeyword_Drops()
    {
        // 스택 없음 + AIT/Sentry/SdkMessagePatterns 어느 것도 매칭 안 됨 → unknown → 드롭
        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource("Random Unity warning text", null));
    }

    [Test]
    public void EmptyStackAndMessage_Drops()
    {
        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource("", ""));
    }

    [Test]
    public void NullMessageAndStack_Drops()
    {
        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource(null, null));
    }

    #endregion

    #region 메시지 기반 fallback과의 상호작용

    [Test]
    public void UserStackButSdkMessage_Drops()
    {
        // 스택은 user_project로 결정됨 — 메시지 키워드보다 스택이 우선이므로 드롭
        // (DetermineErrorSource는 스택이 있으면 스택으로 결정, 없을 때만 메시지 fallback)
        string stackTrace =
            "at MyGame.PlayerController.Start () in Assets/Scripts/PlayerController.cs:15";

        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource("[AIT] suppressed by user code", stackTrace));
    }

    #endregion
}
