// -----------------------------------------------------------------------
// SDKSerializationTests.cs - EditMode 직렬화 Round-trip 테스트
// Level 0: WebGL 빌드 없이 C# 순수 로직을 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using Newtonsoft.Json;
using System;
using AppsInToss;

[TestFixture]
public class SDKSerializationTests
{
    private JsonSerializerSettings settings;

    [SetUp]
    public void Setup()
    {
        settings = AITJsonSettings.Default;
    }

    // =====================================================
    // Enum 직렬화 Round-trip 테스트
    // =====================================================

    [TestCase(typeof(AppLoginResultReferrer), "DEFAULT")]
    [TestCase(typeof(AppLoginResultReferrer), "SANDBOX")]
    [TestCase(typeof(GetPermissionPermissionName), "Camera")]
    [TestCase(typeof(GetPermissionPermissionName), "Clipboard")]
    [TestCase(typeof(SetDeviceOrientationOptionsType), "Portrait")]
    [TestCase(typeof(SetDeviceOrientationOptionsType), "Landscape")]
    [TestCase(typeof(HapticFeedbackType), "Tap")]
    [TestCase(typeof(Accuracy), "Balanced")]
    public void EnumSerialization_RoundTrip(Type enumType, string valueName)
    {
        var enumValue = Enum.Parse(enumType, valueName);

        // C# → JSON
        string json = JsonConvert.SerializeObject(enumValue, settings);
        Assert.IsNotNull(json, $"Serialization of {enumType.Name}.{valueName} should not return null");
        Assert.IsNotEmpty(json, $"Serialization of {enumType.Name}.{valueName} should not return empty");

        // JSON → C#
        var deserialized = JsonConvert.DeserializeObject(json, enumType, settings);
        Assert.AreEqual(enumValue, deserialized,
            $"Round-trip failed for {enumType.Name}.{valueName}: serialized={json}");
    }

    // =====================================================
    // 클래스 직렬화 Round-trip 테스트
    // =====================================================

    [Test]
    public void ClassSerialization_ShareMessage_RoundTrip()
    {
        var original = new ShareMessage { Message = "Test message" };
        AssertClassRoundTrip(original);
    }

    [Test]
    public void ClassSerialization_GetCurrentLocationOptions_RoundTrip()
    {
        var original = new GetCurrentLocationOptions { Accuracy = Accuracy.Balanced };
        AssertClassRoundTrip(original);
    }

    [Test]
    public void ClassSerialization_HapticFeedbackOptions_RoundTrip()
    {
        var original = new HapticFeedbackOptions { Type = HapticFeedbackType.Tap };
        AssertClassRoundTrip(original);
    }

    [Test]
    public void ClassSerialization_CheckoutPaymentOptions_RoundTrip()
    {
        var original = new CheckoutPaymentOptions { PayToken = "test-token-123" };
        AssertClassRoundTrip(original);
    }

    [Test]
    public void ClassSerialization_EventLogParams_RoundTrip()
    {
        var original = new EventLogParams { Log_name = "test_event", Log_type = "custom" };
        AssertClassRoundTrip(original);
    }

    // =====================================================
    // Discriminated Union (Result 타입) 테스트
    // =====================================================

    [Test]
    public void DiscriminatedUnion_Success()
    {
        var successJson = @"{""_type"":""success"",""_successJson"":{""hash"":""test-hash-123"",""type"":""test-type""},""_errorCode"":null}";

        var result = JsonConvert.DeserializeObject<GetUserKeyForGameResult>(successJson, settings);
        Assert.IsNotNull(result, "Deserialization should succeed");
        Assert.IsTrue(result.IsSuccess, "Should be a success result");
        Assert.AreEqual("test-hash-123", result.GetSuccess()?.Hash, "Hash should match");
    }

    [Test]
    public void DiscriminatedUnion_Error()
    {
        var errorJson = @"{""_type"":""error"",""_successJson"":null,""_errorCode"":""INVALID_CATEGORY""}";

        var result = JsonConvert.DeserializeObject<GetUserKeyForGameResult>(errorJson, settings);
        Assert.IsNotNull(result, "Deserialization should succeed");
        Assert.IsTrue(result.IsError, "Should be an error result");
        Assert.AreEqual("INVALID_CATEGORY", result.GetErrorCode(), "Error code should match");
    }

    // =====================================================
    // 헬퍼
    // =====================================================

    private void AssertClassRoundTrip<T>(T original) where T : class
    {
        var serSettings = new JsonSerializerSettings
        {
            Converters = settings.Converters,
            NullValueHandling = NullValueHandling.Ignore
        };

        string json = JsonConvert.SerializeObject(original, serSettings);
        Assert.IsNotNull(json, $"Serialization of {typeof(T).Name} should not return null");

        var deserialized = JsonConvert.DeserializeObject<T>(json, serSettings);
        Assert.IsNotNull(deserialized, $"Deserialization of {typeof(T).Name} should not return null");

        string reserializedJson = JsonConvert.SerializeObject(deserialized, serSettings);
        Assert.AreEqual(json, reserializedJson,
            $"Round-trip JSON should be consistent for {typeof(T).Name}");
    }
}
