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

    #region 분류기 false positive 회귀 (Sentry APPS-IN-TOSS-UNITY-SDK-R2)

    // 사용자 코드의 Debug.LogError("order Recover fail ex: ...") 가
    // SDK v2.4.7 (strict gate 도입 전) 에서 38건 캡처된 시나리오.
    // 메시지에는 SDK 키워드가 없지만 스택의 어딘가(파일 경로/네임스페이스 substring)에
    // AIT/AppsInToss 토큰이 들어가 IsAitRelated 의 substring 매치를 통과한 케이스.
    // SanitizeFilePath 가 Library/PackageCache/<vendor>@... 경로를 그대로 보존하므로
    // DetermineErrorSource 의 frame filename 비교는 SdkPackagePath / SdkPackageCachePath /
    // UserProjectPathPrefix(Assets/) 어느 것에도 매칭되지 않고, 메시지 fallback 단계에서도
    // MessageContainsSdkKeyword 가 message 본문에서 SDK 키워드를 찾지 못해 unknown 을 반환 →
    // strict gate 가 드롭해야 한다.

    [Test]
    public void OrderRecoverFail_AitSubstringInNonSdkPath_Drops()
    {
        // 메시지 본문은 사용자 코드 출력 — SDK 키워드 없음
        const string message = "order Recover fail ex: Object reference not set to an instance of an object";

        // 스택의 frame filename 이 SDK package path (Packages/im.toss.apps-in-toss-unity-sdk/...) 도 아니고
        // Assets/ 도 아닌 third-party/UPM 경로. 그러나 substring "AppsInToss" 가 들어 있어
        // 과거 IsAitRelated 의 단순 IndexOf 매치를 통과시켰던 케이스.
        const string stackTrace =
            "at SomeVendor.AppsInTossBridge.Recover () in Library/PackageCache/com.vendor.bridge@1.0.0/Runtime/Bridge.cs:42";

        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource(message, stackTrace));
    }

    [Test]
    public void OrderRecoverFail_NoStackNoSdkKeyword_Drops()
    {
        // R2 의 실제 재현 경로(스택에 AIT 토큰 substring 존재) 는 위 테스트가 커버한다.
        // 본 케이스는 strict gate 도입 후 동일 메시지가 스택 없이 들어와도 일관되게 드롭됨을
        // 확인하는 보완 케이스 — NoStackTrace_NoSdkKeyword_Drops 와 경로는 같지만 R2 메시지 문자열
        // 로 회귀 표면을 한 번 더 고정한다.
        const string message = "order Recover fail ex: Object reference not set to an instance of an object";
        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource(message, ""));
        Assert.IsTrue(AITEditorErrorTracker.ShouldDropAsNonSdkSource(message, null));
    }

    #endregion
}
