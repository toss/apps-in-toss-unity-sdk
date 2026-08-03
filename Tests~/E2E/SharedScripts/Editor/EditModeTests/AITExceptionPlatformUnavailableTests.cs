// -----------------------------------------------------------------------
// AITExceptionPlatformUnavailableTests.cs
// Level 0: AITException.IsPlatformUnavailable 회귀 가드
//
// APPS-IN-TOSS-UNITY-SDK-11C:
//   WebGL 환경에서 window.AppsInToss JS 브리지가 아직 초기화되지 않은 상태로
//   AITSentryContextEnricher.EnrichAsync()가 GetPlatformOS()를 호출하면
//   JS TypeError "Cannot read properties of undefined (reading 'getPlatformOS')"가
//   발생하고, AITException.IsPlatformUnavailable == false로 분류되어
//   Debug.LogWarning으로 기록 → Sentry 노이즈 유입.
//
//   수정: CheckPlatformUnavailable에 "Cannot read properties of undefined" 패턴을
//         추가하여 JS 브리지 미초기화를 "플랫폼 미지원"과 동일하게 처리한다.
//         AITSentryContextEnricher.CollectSafe에서 IsPlatformUnavailable == true이면
//         Debug.Log (Info 레벨)로 기록하여 Sentry로 전송되지 않는다.
//
// APPS-IN-TOSS-UNITY-SDK-11G:
//   동일 패턴 — GetTossAppVersion 호출 시 "Cannot read properties of undefined
//   (reading 'getTossAppVersion')" 발생. CheckPlatformUnavailable의
//   "Cannot read properties of undefined" 패턴이 이 케이스도 포괄하므로
//   IsPlatformUnavailable == true가 보장됨을 명시적으로 회귀 가드한다.
// -----------------------------------------------------------------------

using AppsInToss;
using NUnit.Framework;

[TestFixture]
[Category("Unit")]
public class AITExceptionPlatformUnavailableTests
{
    // =====================================================
    // APPS-IN-TOSS-UNITY-SDK-11C 회귀 가드
    // window.AppsInToss 미초기화 TypeError → IsPlatformUnavailable == true
    // =====================================================

    [TestCase("Cannot read properties of undefined (reading 'getPlatformOS')",
        TestName = "getPlatformOS — SDK-11C 재현 메시지")]
    [TestCase("Cannot read properties of undefined (reading 'getDeviceId')",
        TestName = "getDeviceId — 동일 패턴")]
    [TestCase("Cannot read properties of undefined (reading 'getLocale')",
        TestName = "getLocale — 동일 패턴")]
    [TestCase("Cannot read properties of undefined (reading 'getTossAppVersion')",
        TestName = "getTossAppVersion — SDK-11G 재현 메시지")]
    [TestCase("Cannot read properties of undefined",
        TestName = "접두 메시지만 있는 경우")]
    public void IsPlatformUnavailable_JsBridgeUndefined_ReturnsTrue(string errorMessage)
    {
        var ex = new AITException(errorMessage);
        Assert.IsTrue(ex.IsPlatformUnavailable,
            $"window.AppsInToss 미초기화 TypeError는 플랫폼 미지원으로 분류되어야 함. " +
            $"메시지: '{errorMessage}'. IsPlatformUnavailable이 false이면 " +
            "AITSentryContextEnricher.CollectSafe가 Debug.LogWarning을 내보내 " +
            "Sentry 노이즈가 발생한다 (APPS-IN-TOSS-UNITY-SDK-11C).");
    }

    // =====================================================
    // 기존 패턴 회귀 확인
    // =====================================================

    [TestCase("__GRANITE_NATIVE_EMITTER is not defined")]
    [TestCase("ReactNativeWebView is not available")]
    [TestCase("getLocale is not a constant handler")]
    public void IsPlatformUnavailable_ExistingPatterns_StillReturnTrue(string errorMessage)
    {
        var ex = new AITException(errorMessage);
        Assert.IsTrue(ex.IsPlatformUnavailable,
            $"기존 플랫폼 미지원 패턴이 여전히 true를 반환해야 함: '{errorMessage}'");
    }

    // =====================================================
    // 실제 버그는 false여야 하는 케이스 — 오탐 방지
    // =====================================================

    [TestCase("Network request failed")]
    [TestCase("Callback timeout")]
    [TestCase("JSON parse error")]
    public void IsPlatformUnavailable_RealErrors_ReturnFalse(string errorMessage)
    {
        var ex = new AITException(errorMessage);
        Assert.IsFalse(ex.IsPlatformUnavailable,
            $"실제 오류는 플랫폼 미지원으로 분류되지 않아야 함: '{errorMessage}'");
    }
}
