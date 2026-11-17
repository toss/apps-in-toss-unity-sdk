// -----------------------------------------------------------------------
// AITBridgeEnvelopeTests.cs - JS 브리지 봉투 파서 검증
// Level 0: AssetDatabase 비의존 순수 로직 EditMode 테스트
//   - ParseCallback / ParseResponse 결과가 Newtonsoft 역직렬화 결과와 같은지 대조
//   - 직접 읽지 않는 형식(숫자, 잘못된 이스케이프, 서로게이트 이스케이프 등)은 Newtonsoft 로 넘어가 같은 결과를 내는지 확인
// -----------------------------------------------------------------------

using System;
using NUnit.Framework;
using Newtonsoft.Json;
using AppsInToss;

[TestFixture]
public class AITBridgeEnvelopeTests
{
    private static string Describe(Func<object> parse)
    {
        try
        {
            var value = parse();
            return value == null ? "<null>" : JsonConvert.SerializeObject(value);
        }
        catch (Exception ex)
        {
            return "EX:" + ex.GetType().Name;
        }
    }

    private static void AssertCallbackParity(string json)
    {
        Assert.AreEqual(
            Describe(() => JsonConvert.DeserializeObject<AITCore.CallbackData>(json)),
            Describe(() => AITBridgeEnvelope.ParseCallback(json)),
            "CallbackData: " + json);
    }

    private static void AssertResponseParity(string json)
    {
        Assert.AreEqual(
            Describe(() => JsonConvert.DeserializeObject<APIResponse>(json)),
            Describe(() => AITBridgeEnvelope.ParseResponse(json)),
            "APIResponse: " + json);
    }

    [Test]
    public void ParseCallback_JslibPayload_MatchesNewtonsoft()
    {
        string data = JsonConvert.SerializeObject(new { top = 12.5, name = "a\"b\\c", list = new[] { 1, 2 } });
        string result = JsonConvert.SerializeObject(new { success = true, data, error = "" });
        string payload = JsonConvert.SerializeObject(new { CallbackId = "cb_1", TypeName = "SafeAreaInsets", Result = result });

        AssertCallbackParity(payload);
        AssertResponseParity(result);

        var parsed = AITBridgeEnvelope.ParseCallback(payload);
        Assert.AreEqual("cb_1", parsed.CallbackId);
        Assert.AreEqual("SafeAreaInsets", parsed.TypeName);
        Assert.AreEqual(result, parsed.Result);

        var response = AITBridgeEnvelope.ParseResponse(parsed.Result);
        Assert.IsTrue(response.success);
        Assert.AreEqual(data, response.data);
    }

    [TestCase("{\"success\":false,\"data\":\"\",\"error\":\"boom\"}")]
    [TestCase("{\"success\":true,\"error\":\"\"}")]
    [TestCase("{\"success\":true,\"data\":null,\"error\":\"\"}")]
    [TestCase(" \n{ \"SUCCESS\" : true , \"Data\" : \"\\u00e9\\n\\t\\/\" }\n")]
    [TestCase("{\"success\":true,\"data\":\"2024-01-01T00:00:00Z\",\"error\":\"\"}")]
    [TestCase("{\"success\":true,\"data\":\"😀 한글\",\"error\":\"\"}")]
    [TestCase("{\"success\":true,\"extra\":\"x\"}")]
    [TestCase("{}")]
    public void ParseResponse_DirectPath_MatchesNewtonsoft(string json)
    {
        AssertResponseParity(json);
    }

    [TestCase("")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{\"success\":\"true\"}")]
    [TestCase("{\"success\":null}")]
    [TestCase("{\"success\":true,\"data\":123}")]
    [TestCase("{\"success\":true,\"data\":\"\\ud83d\"}")]
    [TestCase("{\"success\":true,\"data\":\"\\q\"}")]
    [TestCase("{\"success\":true,}")]
    [TestCase("{\"success\":true}x")]
    [TestCase("{'success':true}")]
    public void ParseResponse_UnsupportedInput_FallsBackLikeNewtonsoft(string json)
    {
        AssertResponseParity(json);
    }

    [TestCase("{\"CallbackId\":\"a\",\"CallbackId\":\"b\"}")]
    [TestCase("{\"callbackid\":\"a\",\"typename\":\"T\",\"result\":null}")]
    [TestCase("{\"CallbackId\":\"a\",\"Other\":true}")]
    [TestCase("{\"CallbackId\":\"a\",\"Result\":{}}")]
    [TestCase("{\"CallbackId\":\"a\"")]
    [TestCase("  ")]
    public void ParseCallback_EdgeCases_MatchNewtonsoft(string json)
    {
        AssertCallbackParity(json);
    }
}
