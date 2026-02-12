// -----------------------------------------------------------------------
// FirebaseAPIReflectionTests.cs - EditMode Firebase API 리플렉션 테스트
// Level 0: WebGL 빌드 없이 AITFirebase 클래스의 API 메서드 존재 여부를 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Reflection;
using AppsInToss.Firebase;

[TestFixture]
public class FirebaseAPIReflectionTests
{
    private Type firebaseType;

    [SetUp]
    public void Setup()
    {
        firebaseType = typeof(AITFirebase);
        Assert.IsNotNull(firebaseType, "AITFirebase type should exist");
    }

    // =====================================================
    // API 메서드 존재 확인 (9개)
    // =====================================================

    [TestCase("Initialize")]
    [TestCase("LogEvent")]
    [TestCase("SetUserId")]
    [TestCase("SetUserProperties")]
    [TestCase("SetAnalyticsCollectionEnabled")]
    [TestCase("SignInAnonymously")]
    [TestCase("SignInWithCustomToken")]
    [TestCase("SignOut")]
    [TestCase("OnAuthStateChanged")]
    public void AITFirebase_API_Exists(string methodName)
    {
        var methods = firebaseType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        bool found = false;
        foreach (var method in methods)
        {
            if (method.Name == methodName)
            {
                found = true;
                break;
            }
        }
        Assert.IsTrue(found, $"AITFirebase.{methodName}() should exist as a public static method");
    }

    // =====================================================
    // FirebaseUser 타입 + 프로퍼티 존재 확인
    // =====================================================

    [Test]
    public void FirebaseUser_Type_Exists_With_Properties()
    {
        var userType = typeof(FirebaseUser);
        Assert.IsNotNull(userType, "FirebaseUser type should exist");

        string[] expectedProperties = new[]
        {
            "Uid", "Email", "DisplayName", "PhotoURL",
            "PhoneNumber", "IsAnonymous", "EmailVerified"
        };

        foreach (var propName in expectedProperties)
        {
            var prop = userType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(prop, $"FirebaseUser.{propName} property should exist");
        }
    }

    // =====================================================
    // FirebaseCallbackRouter 존재 확인
    // =====================================================

    [Test]
    public void FirebaseCallbackRouter_Exists()
    {
        var routerType = typeof(FirebaseCallbackRouter);
        Assert.IsNotNull(routerType, "FirebaseCallbackRouter type should exist");
    }

    // =====================================================
    // 최소 API 개수 확인 (9개 이상)
    // =====================================================

    [Test]
    public void AITFirebase_Has_MinimumExpected_API_Count()
    {
        var methods = firebaseType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        int count = 0;
        foreach (var method in methods)
        {
            if (!method.IsSpecialName)
            {
                count++;
            }
        }

        Assert.GreaterOrEqual(count, 9,
            $"AITFirebase should have at least 9 public static API methods, found {count}");
    }
}
