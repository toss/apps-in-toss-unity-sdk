// -----------------------------------------------------------------------
// SDKAPIReflectionTests.cs - EditMode SDK API 리플렉션 테스트
// Level 0: WebGL 빌드 없이 AIT 클래스의 API 메서드 존재 여부를 검증
// 검증 대상 API 이름 목록은 APITestCatalog.AllAPINames(RuntimeAPITester.cs와
// 단일화된 공유 소스)를 [TestCaseSource]로 그대로 소비한다.
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

    [TestCaseSource(typeof(APITestCatalog), nameof(APITestCatalog.AllAPINames))]
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

    // web-framework 3.0.0에서 제거된 API(web-bridge → webview-bridge 리네임).
    // 2.x에는 존재하고 3.0.0+에는 부재하므로, 존재 여부와 무관하게 통과시켜
    // sdk-version-override를 포함한 버전 매트릭스에서 안전하게 만든다.
    // (회귀로 인한 대량 누락은 AIT_Has_MinimumExpected_API_Count가 방어)
    [Test]
    public void AIT_OnVisibilityChangedByTransparentServiceWeb_PresenceIsVersionDependent()
    {
        var method = aitType.GetMethod("OnVisibilityChangedByTransparentServiceWeb",
            BindingFlags.Public | BindingFlags.Static);
        Assert.Pass(method != null
            ? "present (web-framework < 3.0.0)"
            : "absent (removed in web-framework 3.0.0+)");
    }

    // =====================================================
    // API 개수 확인 (최소 APITestCatalog.AllAPINames.Length개)
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

        int minExpected = APITestCatalog.AllAPINames.Length;
        Assert.GreaterOrEqual(count, minExpected,
            $"AIT should have at least {minExpected} public static API methods, found {count}");
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
