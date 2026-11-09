// -----------------------------------------------------------------------
// SDKSerializationTests.cs - EditMode 직렬화 Round-trip 테스트
// Level 0: WebGL 빌드 없이 C# 순수 로직을 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using AppsInToss;

[TestFixture]
public class SDKSerializationTests
{
    // =====================================================
    // Enum 직렬화 Round-trip 테스트
    // =====================================================

    [TestCase(typeof(AppLoginResultReferrer), "DEFAULT")]
    [TestCase(typeof(AppLoginResultReferrer), "SANDBOX")]
    [TestCase(typeof(PermissionName), "Camera")]
    [TestCase(typeof(PermissionName), "Clipboard")]
    [TestCase(typeof(SetDeviceOrientationOptionsType), "Portrait")]
    [TestCase(typeof(SetDeviceOrientationOptionsType), "Landscape")]
    [TestCase(typeof(HapticFeedbackType), "Tap")]
    [TestCase(typeof(Accuracy), "Balanced")]
    [TestCase(typeof(NetworkStatus), "_4G")]
    [TestCase(typeof(NetworkStatus), "_3G")]
    [TestCase(typeof(NetworkStatus), "_2G")]
    [TestCase(typeof(NetworkStatus), "_5G")]
    [TestCase(typeof(NetworkStatus), "WIFI")]
    [TestCase(typeof(NetworkStatus), "OFFLINE")]
    [TestCase(typeof(NetworkStatus), "WWAN")]
    [TestCase(typeof(NetworkStatus), "UNKNOWN")]
    public void EnumSerialization_RoundTrip(Type enumType, string valueName)
    {
        var enumValue = Enum.Parse(enumType, valueName);

        // C# → JSON
        string json = AITJsonSettings.Serialize(enumValue);
        Assert.IsNotNull(json, $"Serialization of {enumType.Name}.{valueName} should not return null");
        Assert.IsNotEmpty(json, $"Serialization of {enumType.Name}.{valueName} should not return empty");

        // JSON → C#
        var deserialized = AITJsonSettings.Deserialize(json, enumType);
        Assert.AreEqual(enumValue, deserialized,
            $"Round-trip failed for {enumType.Name}.{valueName}: serialized={json}");
    }

    // =====================================================
    // JS 브릿지 응답 값으로 직접 역직렬화 테스트 (EnumMember Value 기반)
    // =====================================================

    [TestCase(typeof(NetworkStatus), "4G")]
    [TestCase(typeof(NetworkStatus), "3G")]
    [TestCase(typeof(NetworkStatus), "2G")]
    [TestCase(typeof(NetworkStatus), "5G")]
    [TestCase(typeof(NetworkStatus), "WIFI")]
    [TestCase(typeof(NetworkStatus), "OFFLINE")]
    [TestCase(typeof(NetworkStatus), "WWAN")]
    [TestCase(typeof(NetworkStatus), "UNKNOWN")]
    [TestCase(typeof(PermissionStatus), "notDetermined")]
    [TestCase(typeof(PermissionStatus), "denied")]
    [TestCase(typeof(PermissionStatus), "allowed")]
    public void EnumDeserialization_FromEnumMemberValue(Type enumType, string jsonValue)
    {
        // JS 브릿지가 보내는 실제 문자열(예: "4G")로 역직렬화 가능한지 검증
        var json = $"\"{jsonValue}\"";
        var result = AITJsonSettings.Deserialize(json, enumType);
        Assert.IsNotNull(result, $"Failed to deserialize \"{jsonValue}\" to {enumType.Name}");
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
        var original = new CheckoutPaymentOptions { Params = new CheckoutPaymentParams { PayToken = "test-token-123" } };
        AssertClassRoundTrip(original);
    }

    [Test]
    public void ClassSerialization_EventLogParams_RoundTrip()
    {
        var original = new EventLogParams { Log_name = "test_event", Log_typeTyped = EventLogParamsLog_type.Event };
        AssertClassRoundTrip(original);
    }

    [Test]
    public void ClassSerialization_EventLogParams_LegacyStringLogType_RoundTrip()
    {
#pragma warning disable CS0618 // 하위 호환 경로: 옛 string Log_type 은 알 수 없는 값도 보존한다
        var original = new EventLogParams { Log_name = "test_event", Log_type = "custom" };
        Assert.IsNull(original.Log_typeTyped, "알 수 없는 값은 Typed 가 null");
        var restored = AITJsonSettings.Deserialize<EventLogParams>(AITJsonSettings.Serialize(original));
        Assert.AreEqual("custom", restored.Log_type);
        restored.Log_typeTyped = EventLogParamsLog_type.Event;
        Assert.AreEqual("event", restored.Log_type, "Typed 설정이 string 에 반영돼야 함");
#pragma warning restore CS0618
    }

    [Test]
    public void ClassSerialization_CheckoutPaymentOptions_LegacyPayTokenForwardsToParams()
    {
#pragma warning disable CS0618
        var options = new CheckoutPaymentOptions { PayToken = "legacy-token" };
        Assert.AreEqual("legacy-token", options.Params.PayToken);
        Assert.IsTrue(AITJsonSettings.Serialize(options).Contains("legacy-token"));
#pragma warning restore CS0618
    }

    // =====================================================
    // Discriminated Union (Result 타입) 테스트
    // =====================================================

    [Test]
    public void DiscriminatedUnion_Success()
    {
        var successJson = @"{""_type"":""success"",""_successJson"":{""hash"":""test-hash-123"",""type"":""test-type""},""_errorCode"":null}";

        var result = AITJsonSettings.Deserialize<GetAnonymousKeyResult>(successJson);
        Assert.IsNotNull(result, "Deserialization should succeed");
        Assert.IsTrue(result.IsSuccess, "Should be a success result");
        Assert.AreEqual("test-hash-123", result.GetSuccess()?.Hash, "Hash should match");
    }

    [Test]
    public void DiscriminatedUnion_Error()
    {
        var errorJson = @"{""_type"":""error"",""_successJson"":null,""_errorCode"":""ERROR""}";

        var result = AITJsonSettings.Deserialize<GetAnonymousKeyResult>(errorJson);
        Assert.IsNotNull(result, "Deserialization should succeed");
        Assert.IsTrue(result.IsError, "Should be an error result");
        Assert.AreEqual("ERROR", result.GetErrorCode(), "Error code should match");
    }

    // =====================================================
    // 헬퍼
    // =====================================================

    private void AssertClassRoundTrip<T>(T original) where T : class
    {
        string json = AITJsonSettings.Serialize(original);
        Assert.IsNotNull(json, $"Serialization of {typeof(T).Name} should not return null");

        var deserialized = AITJsonSettings.Deserialize<T>(json);
        Assert.IsNotNull(deserialized, $"Deserialization of {typeof(T).Name} should not return null");

        string reserializedJson = AITJsonSettings.Serialize(deserialized);
        Assert.AreEqual(json, reserializedJson,
            $"Round-trip JSON should be consistent for {typeof(T).Name}");
    }
}
