// -----------------------------------------------------------------------
// SDKJsonCodecTests.cs - EditMode SDK JSON 읽기·쓰기 호환성 검증
// Level 0: SDK는 Newtonsoft를 참조하지 않는다. 이 테스트 어셈블리만 Newtonsoft를 참조해
//          같은 입력에 대한 Newtonsoft 출력과 SDK 출력을 비교한다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using AppsInToss;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class SDKJsonCodecTests
{
    // 이전 SDK(Newtonsoft + [JsonProperty] + SmartEnumConverter)가 같은 입력을 읽고 다시 쓴 결과를 기록해 둔 것.
    // 입력 → SDK 타입 → JSON 결과가 바이트 단위로 같아야 한다.
    [TestCase(typeof(ContactsViralEventData), @"{""rewardAmount"":10,""rewardUnit"":""P"",""closeReason"":""noReward"",""sentRewardAmount"":null}", @"{""rewardAmount"":10.0,""rewardUnit"":""P"",""closeReason"":""noReward"",""sentRewardAmount"":null,""sendableRewardsCount"":null,""sentRewardsCount"":null}")]
    [TestCase(typeof(ContactsViralEventData), @"{""rewardUnit"":""P""}", @"{""rewardUnit"":""P"",""closeReason"":null,""sentRewardAmount"":null,""sendableRewardsCount"":null,""sentRewardsCount"":null}")]
    [TestCase(typeof(ContactsViralEventData), @"{""rewardAmount"":null,""extra"":{""a"":[1,2.5,""x"",null,true]}}", @"{""rewardUnit"":null,""closeReason"":null,""sentRewardAmount"":null,""sendableRewardsCount"":null,""sentRewardsCount"":null,""extra"":{""a"":[1,2.5,""x"",null,true]}}")]
    [TestCase(typeof(GetAnonymousKeyResult), @"{""_type"":""success"",""_successJson"":{""hash"":""h-1"",""type"":""t""},""_errorCode"":null}", @"{""_type"":""success"",""_successJson"":{""hash"":""h-1"",""type"":""t""},""_errorCode"":null,""IsSuccess"":true,""IsError"":false}")]
    [TestCase(typeof(GetAnonymousKeyResult), @"{""_type"":""error"",""_successJson"":null,""_errorCode"":""ERROR""}", @"{""_type"":""error"",""_successJson"":null,""_errorCode"":""ERROR"",""IsSuccess"":false,""IsError"":true}")]
    [TestCase(typeof(GetConsentedUserDataOptions), @"{""consentedUserDataKey"":""cud_delivery""}", @"{""consentedUserDataKey"":""cud_delivery"",""shouldRequestAgreementWhenUserDeclined"":null}")]
    [TestCase(typeof(GetConsentedUserDataOptions), @"{""consentedUserDataKey"":""cud_delivery"",""shouldRequestAgreementWhenUserDeclined"":null,""futureField"":{""nested"":true}}", @"{""consentedUserDataKey"":""cud_delivery"",""shouldRequestAgreementWhenUserDeclined"":null,""futureField"":{""nested"":true}}")]
    [TestCase(typeof(GetConsentedUserDataOptions), @"{""CONSENTEDUSERDATAKEY"":""upper"",""shouldRequestAgreementWhenUserDeclined"":true}", @"{""consentedUserDataKey"":""upper"",""shouldRequestAgreementWhenUserDeclined"":true}")]
    [TestCase(typeof(Location), @"{""accessLocation"":""FINE"",""timestamp"":1712345678901,""coords"":{""latitude"":37.566535,""longitude"":126.9779692,""altitude"":0,""accuracy"":12.5,""altitudeAccuracy"":0.1,""heading"":-1}}", @"{""accessLocation"":""FINE"",""timestamp"":1712345678901.0,""coords"":{""latitude"":37.566535,""longitude"":126.9779692,""altitude"":0.0,""accuracy"":12.5,""altitudeAccuracy"":0.1,""heading"":-1.0},""error"":null}")]
    [TestCase(typeof(OriginStorageDump), @"{""origin"":""https://a.example"",""localStorage"":{""k"":""v"",""n"":42,""big"":9007199254740993,""f"":1.25,""b"":false,""o"":{""x"":[1]},""z"":null},""indexedDB"":[{""name"":""db"",""version"":3,""objectStores"":[{""name"":""s"",""keyPath"":[""a"",""b""],""autoIncrement"":false,""indexes"":[{""name"":""i"",""keyPath"":""a"",""multiEntry"":false,""unique"":true}],""records"":[]}]}],""opfs"":null,""errors"":[],""unknownTop"":""keep""}", @"{""origin"":""https://a.example"",""localStorage"":{""k"":""v"",""n"":42,""big"":9007199254740993,""f"":1.25,""b"":false,""o"":{""x"":[1]},""z"":null},""indexedDB"":[{""name"":""db"",""version"":3.0,""objectStores"":[{""name"":""s"",""keyPath"":[""a"",""b""],""autoIncrement"":false,""indexes"":[{""name"":""i"",""keyPath"":""a"",""multiEntry"":false,""unique"":true}],""records"":[]}]}],""opfs"":null,""errors"":[],""unknownTop"":""keep""}")]
    [TestCase(typeof(SafeAreaInsets), @"{""top"":47,""left"":0,""right"":0,""bottom"":34.5,""error"":""line\nbreak \""q\"" \u2028 \u0001 한글""}", @"{""top"":47.0,""left"":0.0,""right"":0.0,""bottom"":34.5,""error"":""line\nbreak \""q\"" \u2028 \u0001 한글""}")]
    [TestCase(typeof(GetCurrentLocationOptions), @"{""accuracy"":3}", @"{""accuracy"":3}")]
    [TestCase(typeof(EventLogParams), @"{""log_name"":""test_event"",""log_type"":""event"",""params"":{""a"":1,""b"":""two"",""c"":[true,null]}}", @"{""log_type"":""event"",""log_name"":""test_event"",""params"":{""a"":1,""b"":""two"",""c"":[true,null]}}")]
    public void RoundTrip_MatchesRecordedNewtonsoftOutput(Type type, string input, string expected)
    {
        var value = AITJsonSettings.Deserialize(input, type);
        Assert.IsNotNull(value);
        Assert.AreEqual(expected, AITJsonSettings.Serialize(value));
    }

    // =====================================================
    // Newtonsoft를 직접 오라클로 쓰는 비교
    // =====================================================

    private static string U(int code) => "\\u" + code.ToString("x4");

    [Test]
    public void Strings_AreEscapedLikeNewtonsoft()
    {
        var samples = new List<string> { "", "plain", "한글", "quote\"back\\slash/", "tab\tnl\nrc\rbs\bff\f", "emoji 😀", "<>&'" };
        for (int c = 0; c < 0x20; c++) samples.Add("c" + (char)c);
        samples.Add("x" + (char)0x7f + (char)0x85 + (char)0x2028 + (char)0x2029 + (char)0xfeff);
        foreach (var s in samples)
        {
            Assert.AreEqual(JsonConvert.SerializeObject(s), AITJsonSettings.Serialize(s), "string: " + s);
            Assert.AreEqual(s, AITJsonSettings.Deserialize<string>(JsonConvert.SerializeObject(s)));
        }
    }

    [Test]
    public void Numbers_AreFormattedLikeNewtonsoft()
    {
        var doubles = new[] { 0.0, 1.0, -1.0, 0.1, 1.5, 100.0, 1e21, 1e-7, 123456789.125, 9007199254740993.0, double.MaxValue, double.Epsilon, Math.PI, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        foreach (var d in doubles)
        {
            Assert.AreEqual(JsonConvert.SerializeObject(d), AITJsonSettings.Serialize(d), "double: " + d);
            var back = AITJsonSettings.Deserialize<double>(AITJsonSettings.Serialize(d));
            Assert.IsTrue(back.Equals(d), "double round-trip: " + d);
        }
        foreach (var l in new[] { 0L, -1L, long.MaxValue, long.MinValue, 9007199254740993L })
        {
            Assert.AreEqual(JsonConvert.SerializeObject(l), AITJsonSettings.Serialize(l));
            Assert.AreEqual(l, AITJsonSettings.Deserialize<long>(AITJsonSettings.Serialize(l)));
        }
        Assert.AreEqual(JsonConvert.SerializeObject(0.1f), AITJsonSettings.Serialize(0.1f));
        Assert.AreEqual(JsonConvert.SerializeObject(3.25m), AITJsonSettings.Serialize(3.25m));
    }

    public class UserPayload
    {
        public string Name = "n";
        [JsonProperty("renamed")] public string Original = "r";
        [JsonIgnore] public string Hidden = "h";
        public int Count { get; set; } = 2;
        public List<double> Values = new List<double> { 1, 2.5 };
    }

    [Test]
    public void NonSdkObjects_AreWrittenLikeNewtonsoft()
    {
        var samples = new object[]
        {
            null,
            new[] { "a", "b" },
            new List<int> { 1, 2 },
            new Dictionary<string, string> { ["k"] = "v" },
            new Dictionary<string, object> { ["n"] = 1, ["d"] = 2.0, ["s"] = "x", ["b"] = false, ["nul"] = null, ["arr"] = new object[] { 1, "two" }, ["nested"] = new Dictionary<string, object> { ["z"] = 1L } },
            new { screenName = "main", count = 3, ratio = 0.5, tags = new[] { "x" } },
            new UserPayload(),
            new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Guid.Empty,
            new byte[] { 1, 2, 3 },
        };
        foreach (var o in samples)
        {
            Assert.AreEqual(JsonConvert.SerializeObject(o), AITJsonSettings.Serialize(o), "value: " + o);
        }
    }

    [Test]
    public void JsonValue_ParsesAndWritesLikeJToken()
    {
        var inputs = new[]
        {
            "null", "true", "false", "0", "-12", "3.5", "\"s\"", "[]", "{}",
            "{\"a\":[1,2,{\"b\":null}],\"c\":\"d\",\"e\":{\"f\":[true,false]}}",
            "[\"" + U(0x2028) + "\",\"\\t\",\"\\\"\"]",
            "{ \"spaced\" : [ 1 , 2 ] }",
        };
        foreach (var json in inputs)
        {
            var expected = JToken.Parse(json).ToString(Formatting.None);
            Assert.AreEqual(expected, JsonValue.Parse(json).ToString(), "json: " + json);
        }
        Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\":}"));
        Assert.Throws<FormatException>(() => JsonValue.Parse("[1,]"));
        Assert.Throws<FormatException>(() => JsonValue.Parse("1 2"));
    }

    // =====================================================
    // 와이어 규칙
    // =====================================================

    [Test]
    public void MissingKeyAndExplicitNull_BothReadAsNull_AndAreWrittenAsNull()
    {
        var missing = AITJsonSettings.Deserialize<GetConsentedUserDataOptions>("{\"consentedUserDataKey\":\"k\"}");
        var explicitNull = AITJsonSettings.Deserialize<GetConsentedUserDataOptions>("{\"consentedUserDataKey\":\"k\",\"shouldRequestAgreementWhenUserDeclined\":null}");
        Assert.IsNull(missing.ShouldRequestAgreementWhenUserDeclined);
        Assert.IsNull(explicitNull.ShouldRequestAgreementWhenUserDeclined);
        Assert.AreEqual("{\"consentedUserDataKey\":\"k\",\"shouldRequestAgreementWhenUserDeclined\":null}", AITJsonSettings.Serialize(missing));
    }

    [Test]
    public void SpecifiedFlag_TracksPresenceOnTheWire()
    {
#pragma warning disable CS0618
        var absent = AITJsonSettings.Deserialize<ContactsViralEventData>("{\"rewardUnit\":\"P\"}");
        Assert.IsFalse(absent.RewardAmountSpecified);
        Assert.IsNull(absent.RewardAmountTyped);
        Assert.IsFalse(AITJsonSettings.Serialize(absent).Contains("rewardAmount"));

        var zero = AITJsonSettings.Deserialize<ContactsViralEventData>("{\"rewardAmount\":0}");
        Assert.IsTrue(zero.RewardAmountSpecified);
        Assert.AreEqual(0.0, zero.RewardAmountTyped);
#pragma warning restore CS0618
    }

    [Test]
    public void UnknownKeys_ArePreservedAndWrittenBack()
    {
        var json = "{\"top\":47,\"left\":0,\"right\":0,\"bottom\":34,\"newField\":{\"x\":[1,\"y\"]},\"newNumber\":12345678901234567890}";
        var insets = AITJsonSettings.Deserialize<SafeAreaInsets>(json);
        Assert.IsNotNull(insets._extensionData);
        Assert.AreEqual(JsonValueKind.Object, insets._extensionData["newField"].Kind);
        Assert.AreEqual("y", insets._extensionData["newField"]["x"][1].AsString());
        var written = AITJsonSettings.Serialize(insets);
        StringAssert.EndsWith(",\"newField\":{\"x\":[1,\"y\"]},\"newNumber\":12345678901234567890}", written);

        // 게임이 직접 넣은 확장 필드도 와이어로 나간다
        var options = new GetConsentedUserDataOptions { ConsentedUserDataKey = "k" };
        options._extensionData = new Dictionary<string, JsonValue> { ["extra"] = "v", ["n"] = 2 };
        StringAssert.EndsWith(",\"extra\":\"v\",\"n\":2}", AITJsonSettings.Serialize(options));
    }

    [Test]
    public void KeysMatchCaseInsensitively_WhenNoExactMatch()
    {
        var o = AITJsonSettings.Deserialize<GetConsentedUserDataOptions>("{\"CONSENTEDUSERDATAKEY\":\"k\"}");
        Assert.AreEqual("k", o.ConsentedUserDataKey);
    }

    [Test]
    public void Enums_UseWireStrings_NumericEnumsUseNumbers_UnknownValuesDoNotThrow()
    {
        Assert.AreEqual("\"4G\"", AITJsonSettings.Serialize(NetworkStatus._4G));
        Assert.AreEqual(NetworkStatus._4G, AITJsonSettings.Deserialize<NetworkStatus>("\"4G\""));
        Assert.AreEqual(NetworkStatus._4G, AITJsonSettings.Deserialize<NetworkStatus>("\"_4G\""), "C# 이름도 받는다");
        Assert.AreEqual("3", AITJsonSettings.Serialize(Accuracy.Balanced));
        Assert.AreEqual(Accuracy.Balanced, AITJsonSettings.Deserialize<Accuracy>("3"));
        Assert.IsNull(AITJsonSettings.Deserialize<NetworkStatus?>("\"6G\""));

        // 객체 안의 모르는 enum 값은 객체 전체를 실패시키지 않는다
        ContactsViralEventData data = null;
        Assert.DoesNotThrow(() => data = AITJsonSettings.Deserialize<ContactsViralEventData>("{\"rewardUnit\":\"P\",\"closeReason\":\"somethingNew\"}"));
        Assert.AreEqual("P", data.RewardUnit);
        Assert.IsNull(data.CloseReason);
    }

    [Test]
    public void NestedArraysAndObjectFields_Read()
    {
        var dump = AITJsonSettings.Deserialize<OriginStorageDump>(
            "{\"localStorage\":{\"n\":9007199254740993,\"f\":1.5,\"s\":\"x\",\"o\":{\"a\":1}},\"indexedDB\":[{\"name\":\"db\",\"objectStores\":[{\"keyPath\":[\"a\",\"b\"],\"indexes\":[]}]}]}");
        Assert.AreEqual(9007199254740993L, dump.LocalStorage["n"]);
        Assert.AreEqual(1.5, dump.LocalStorage["f"]);
        Assert.AreEqual("x", dump.LocalStorage["s"]);
        Assert.IsInstanceOf<JsonValue>(dump.LocalStorage["o"]);
        Assert.AreEqual("db", dump.IndexedDB[0].Name);
        Assert.AreEqual(2, ((JsonValue)dump.IndexedDB[0].ObjectStores[0].KeyPath).Count);
    }

    [Test]
    public void DiscriminatedUnion_ReadsBothBranches()
    {
        var ok = AITJsonSettings.Deserialize<GetAnonymousKeyResult>("{\"_type\":\"success\",\"_successJson\":{\"hash\":\"h\",\"type\":\"t\"}}");
        Assert.IsTrue(ok.IsSuccess);
        Assert.AreEqual("h", ok.GetSuccess().Hash);
        var err = AITJsonSettings.Deserialize<GetAnonymousKeyResult>("{\"_type\":\"error\",\"_errorCode\":\"ERROR\"}");
        Assert.IsTrue(err.IsError);
        Assert.AreEqual("ERROR", err.GetErrorCode());
    }

    [Test]
    public void EmptyInput_ReturnsDefault()
    {
        Assert.IsNull(AITJsonSettings.Deserialize<SafeAreaInsets>(""));
        Assert.IsNull(AITJsonSettings.Deserialize<SafeAreaInsets>("null"));
        Assert.AreEqual(0.0, AITJsonSettings.Deserialize<double>(""));
    }
}
