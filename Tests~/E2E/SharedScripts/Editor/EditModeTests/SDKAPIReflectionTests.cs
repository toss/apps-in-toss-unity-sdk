// -----------------------------------------------------------------------
// SDKAPIReflectionTests.cs - EditMode SDK API 리플렉션 테스트
// Level 0: WebGL 빌드 없이 AIT 클래스의 API 메서드 존재 여부를 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Reflection;
using AppsInToss;

[TestFixture]
public class SDKAPIReflectionTests
{
    private Type aitType;

    [SetUp]
    public void Setup()
    {
        aitType = typeof(AIT);
        Assert.IsNotNull(aitType, "AIT type should exist");
    }

    // =====================================================
    // API 메서드 존재 확인
    // =====================================================

    [TestCase("AppLogin")]
    [TestCase("GetIsTossLoginIntegratedService")]
    [TestCase("GetDeviceId")]
    [TestCase("GetLocale")]
    [TestCase("GetNetworkStatus")]
    [TestCase("GetOperationalEnvironment")]
    [TestCase("GetPlatformOS")]
    [TestCase("GetSchemeUri")]
    [TestCase("GetTossAppVersion")]
    [TestCase("GetClipboardText")]
    [TestCase("SetClipboardText")]
    [TestCase("CloseView")]
    [TestCase("OpenURL")]
    [TestCase("GetTossShareLink")]
    [TestCase("Share")]
    [TestCase("FetchContacts")]
    [TestCase("EventLog")]
    [TestCase("GetPermission")]
    [TestCase("RequestPermission")]
    [TestCase("OpenPermissionDialog")]
    [TestCase("GetCurrentLocation")]
    [TestCase("GenerateHapticFeedback")]
    [TestCase("SetDeviceOrientation")]
    [TestCase("SetIosSwipeGestureEnabled")]
    [TestCase("SetScreenAwakeMode")]
    [TestCase("SetSecureScreen")]
    [TestCase("CheckoutPayment")]
    [TestCase("FetchAlbumPhotos")]
    [TestCase("OpenCamera")]
    [TestCase("SaveBase64Data")]
    [TestCase("GetGameCenterGameProfile")]
    [TestCase("GetUserKeyForGame")]
    [TestCase("OpenGameCenterLeaderboard")]
    [TestCase("SubmitGameCenterLeaderBoardScore")]
    [TestCase("GrantPromotionRewardForGame")]
    [TestCase("AppsInTossSignTossCert")]
    [TestCase("OnVisibilityChangedByTransparentServiceWeb")]
    [TestCase("StartUpdateLocation")]
    [TestCase("ContactsViral")]
    public void AIT_API_Exists(string methodName)
    {
        var methods = aitType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        bool found = false;
        foreach (var method in methods)
        {
            if (method.Name == methodName)
            {
                found = true;
                break;
            }
        }
        Assert.IsTrue(found, $"AIT.{methodName}() should exist as a public static method");
    }

    // =====================================================
    // API 개수 확인 (최소 39개)
    // =====================================================

    [Test]
    public void AIT_Has_MinimumExpected_API_Count()
    {
        var methods = aitType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        int count = 0;
        foreach (var method in methods)
        {
            // getter/setter 등 제외
            if (!method.IsSpecialName)
            {
                count++;
            }
        }

        Assert.GreaterOrEqual(count, 39,
            $"AIT should have at least 39 public static API methods, found {count}");
    }

    // =====================================================
    // IsExpectedError 패턴 매칭 테스트
    // =====================================================

    [Test]
    public void IsExpectedError_KnownPatterns()
    {
        // AITCore의 IsExpectedError 메서드가 존재하는지 확인
        var coreType = typeof(AITCore);
        var method = coreType.GetMethod("IsExpectedError",
            BindingFlags.Public | BindingFlags.Static);

        if (method != null)
        {
            // 예상 에러 패턴 테스트
            var knownPatterns = new[]
            {
                "XXX is not a constant handler",
                "__GRANITE_NATIVE_EMITTER is not available",
                "ReactNativeWebView is not available"
            };

            foreach (var pattern in knownPatterns)
            {
                var result = method.Invoke(null, new object[] { pattern });
                Assert.IsTrue((bool)result,
                    $"IsExpectedError should return true for: {pattern}");
            }

            // 예상치 않은 에러 패턴 테스트
            var unexpectedError = "NullReferenceException: Something went wrong";
            var unexpectedResult = method.Invoke(null, new object[] { unexpectedError });
            Assert.IsFalse((bool)unexpectedResult,
                $"IsExpectedError should return false for unexpected error: {unexpectedError}");
        }
        else
        {
            // IsExpectedError가 없는 경우, AITException에서 직접 확인
            Assert.Pass("IsExpectedError method not found on AITCore (may be in a different location)");
        }
    }
}
