// -----------------------------------------------------------------------
// SerializationTester.cs - E2E Serialization Round-trip Test Runner
// C# ↔ JavaScript 직렬화/역직렬화 일관성 검증
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AppsInToss;

/// <summary>
/// C# 직렬화/역직렬화 Round-trip 테스트 실행기
/// JSON 직렬화 후 역직렬화하여 원본과 일치하는지 검증
/// </summary>
public class SerializationTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendSerializationTestResults(string json);

    [DllImport("__Internal")]
    private static extern string ValidateJsonInJS(string json, string typeName);
#endif

    [Header("Test Settings")]
    public float startDelay = 2f;
    public bool autoRunOnStart = true;

    [Header("UI Settings")]
    public bool showUI = true;
    public bool showDetailedResults = false;

    private List<SerializationTestResult> _results = new List<SerializationTestResult>();
    private bool _testStarted = false;
    private bool _testCompleted = false;
    private Vector2 _scrollPosition = Vector2.zero;

    void Start()
    {
        if (autoRunOnStart)
        {
            StartCoroutine(DelayedStart());
        }
    }

    IEnumerator DelayedStart()
    {
        Debug.Log("[SerializationTester] Waiting for Unity to initialize...");
        yield return new WaitForSeconds(startDelay);
        RunSerializationTests();
    }

    public void RunSerializationTests()
    {
        if (_testStarted) return;
        _testStarted = true;

        Debug.Log("[SerializationTester] ========================================");
        Debug.Log("[SerializationTester] SERIALIZATION ROUND-TRIP TESTS STARTING");
        Debug.Log("[SerializationTester] Testing C# ↔ JSON type mapping consistency");
        Debug.Log("[SerializationTester] ========================================");

        // 1. Enum 타입 테스트
        TestEnumSerialization();

        // 2. 기본 클래스 타입 테스트
        TestBasicClassSerialization();

        // 3. Discriminated Union (Result 타입) 테스트
        TestResultTypeSerialization();

        // 4. 복잡한 중첩 타입 테스트
        TestNestedTypeSerialization();

        // 결과 전송
        SendResults();
    }

    void TestEnumSerialization()
    {
        Debug.Log("[SerializationTester] Testing Enum serialization...");

        // AppLoginResultReferrer 테스트
        TestEnumValue("AppLoginResultReferrer.DEFAULT", AppLoginResultReferrer.DEFAULT, "DEFAULT");
        TestEnumValue("AppLoginResultReferrer.SANDBOX", AppLoginResultReferrer.SANDBOX, "SANDBOX");

        // PermissionName 테스트 (이전 GetPermissionPermissionName, named enum으로 통합됨)
        TestEnumValue("PermissionName.Camera", PermissionName.Camera, "camera");
        TestEnumValue("PermissionName.Clipboard", PermissionName.Clipboard, "clipboard");

        // SetDeviceOrientationOptionsType 테스트
        TestEnumValue("SetDeviceOrientationOptionsType.Portrait", SetDeviceOrientationOptionsType.Portrait, "portrait");
        TestEnumValue("SetDeviceOrientationOptionsType.Landscape", SetDeviceOrientationOptionsType.Landscape, "landscape");

        // HapticFeedbackType 테스트 (숫자 enum이 있다면)
        TestEnumValue("HapticFeedbackType.Tap", HapticFeedbackType.Tap, "tap");

        // Accuracy 테스트 (숫자 enum: [EnumMember]가 없으므로 숫자로 직렬화)
        TestEnumValue("Accuracy.Balanced", Accuracy.Balanced, null, "3");
    }

    void TestEnumValue<T>(string testName, T enumValue, string expectedJsonValue, string expectedRawJson = null) where T : Enum
    {
        try
        {
            // 1. C# enum → JSON 직렬화 (SDK 직렬화 경로)
            string json = AITJsonSettings.Serialize(enumValue);

            // 2. 기대값과 비교 (문자열 enum은 "value", 숫자 enum은 숫자)
            string expectedJson = expectedRawJson ?? $"\"{expectedJsonValue}\"";
            bool jsonMatches = json.Equals(expectedJson, StringComparison.OrdinalIgnoreCase);

            // 3. JSON → C# enum 역직렬화
            T deserialized = AITJsonSettings.Deserialize<T>(json);
            bool roundTripSuccess = enumValue.Equals(deserialized);

            var result = new SerializationTestResult
            {
                testName = testName,
                typeName = typeof(T).Name,
                originalValue = enumValue.ToString(),
                serializedJson = json,
                expectedJson = expectedJson,
                jsonMatches = jsonMatches,
                roundTripSuccess = roundTripSuccess,
                success = jsonMatches && roundTripSuccess,
                error = ""
            };

            _results.Add(result);

            if (result.success)
            {
                Debug.Log($"[SerializationTester] {testName}: PASS (JSON: {json})");
            }
            else
            {
                Debug.LogError($"[SerializationTester] {testName}: FAIL (expected: {expectedJson}, got: {json}, roundTrip: {roundTripSuccess})");
            }
        }
        catch (Exception e)
        {
            _results.Add(new SerializationTestResult
            {
                testName = testName,
                typeName = typeof(T).Name,
                success = false,
                error = e.Message
            });
            Debug.LogError($"[SerializationTester] {testName}: ERROR - {e.Message}");
        }
    }

    void TestBasicClassSerialization()
    {
        Debug.Log("[SerializationTester] Testing basic class serialization...");

        // ShareMessage 테스트
        TestClassSerialization("ShareMessage", new ShareMessage
        {
            Message = "Test message"
        }, json => json.Contains("Test message"));

        // GetCurrentLocationOptions 테스트
        TestClassSerialization("GetCurrentLocationOptions", new GetCurrentLocationOptions
        {
            Accuracy = Accuracy.Balanced
        }, json => json.Contains("\"accuracy\":3")); // 숫자 enum은 숫자로 나간다(SDK 와이어 형식)

        // HapticFeedbackOptions 테스트
        TestClassSerialization("HapticFeedbackOptions", new HapticFeedbackOptions
        {
            Type = HapticFeedbackType.Tap
        }, json => json.ToLower().Contains("tap"));

        // CheckoutPaymentOptions 테스트
        TestClassSerialization("CheckoutPaymentOptions", new CheckoutPaymentOptions
        {
            Params = new CheckoutPaymentParams { PayToken = "test-token-123" }
        }, json => json.Contains("test-token-123"));

        // EventLogParams 테스트
        TestClassSerialization("EventLogParams", new EventLogParams
        {
            Log_name = "test_event",
            Log_typeTyped = EventLogParamsLog_type.Event
        }, json => json.Contains("test_event") && json.Contains("\"event\""));
    }

    void TestClassSerialization<T>(string testName, T instance, Func<string, bool> validateJson) where T : class
    {
        try
        {
            // 1. C# → JSON 직렬화 (SDK 직렬화 경로)
            string json = AITJsonSettings.Serialize(instance);

            // 2. JSON 유효성 검증
            bool jsonValid = validateJson(json);

            // 3. JSON → C# 역직렬화
            T deserialized = AITJsonSettings.Deserialize<T>(json);
            bool roundTripSuccess = deserialized != null;

            // 4. 재직렬화하여 비교
            string reserializedJson = AITJsonSettings.Serialize(deserialized);
            bool jsonConsistent = json == reserializedJson;

            var result = new SerializationTestResult
            {
                testName = testName,
                typeName = typeof(T).Name,
                originalValue = instance.ToString(),
                serializedJson = json,
                jsonMatches = jsonValid,
                roundTripSuccess = roundTripSuccess && jsonConsistent,
                success = jsonValid && roundTripSuccess && jsonConsistent,
                error = ""
            };

            _results.Add(result);

            if (result.success)
            {
                Debug.Log($"[SerializationTester] {testName}: PASS");
            }
            else
            {
                Debug.LogWarning($"[SerializationTester] {testName}: WARN (valid: {jsonValid}, roundTrip: {roundTripSuccess}, consistent: {jsonConsistent})");
            }
        }
        catch (Exception e)
        {
            _results.Add(new SerializationTestResult
            {
                testName = testName,
                typeName = typeof(T).Name,
                success = false,
                error = e.Message
            });
            Debug.LogError($"[SerializationTester] {testName}: ERROR - {e.Message}");
        }
    }

    void TestResultTypeSerialization()
    {
        Debug.Log("[SerializationTester] Testing Result type (discriminated union) serialization...");

        var successJson = @"{""_type"":""success"",""_successJson"":{""hash"":""test-hash-123"",""type"":""test-type""},""_errorCode"":null}";
        TestResultDeserialization<GetAnonymousKeyResult>("DiscriminatedUnion.Success", successJson, result =>
            result.IsSuccess && result.GetSuccess()?.Hash == "test-hash-123");

        var errorJson = @"{""_type"":""error"",""_successJson"":null,""_errorCode"":""ERROR""}";
        TestResultDeserialization<GetAnonymousKeyResult>("DiscriminatedUnion.Error", errorJson, result =>
            result.IsError && result.GetErrorCode() == "ERROR");
    }

    void TestResultDeserialization<T>(string testName, string inputJson, Func<T, bool> validate) where T : class
    {
        try
        {
            // 1. JSON → C# 역직렬화
            T deserialized = AITJsonSettings.Deserialize<T>(inputJson);

            // 2. 유효성 검증
            bool isValid = deserialized != null && validate(deserialized);

            // 3. 재직렬화
            string reserializedJson = AITJsonSettings.Serialize(deserialized);

            var result = new SerializationTestResult
            {
                testName = testName,
                typeName = typeof(T).Name,
                serializedJson = reserializedJson,
                expectedJson = inputJson,
                roundTripSuccess = isValid,
                success = isValid,
                error = isValid ? "" : "Validation failed"
            };

            _results.Add(result);

            if (result.success)
            {
                Debug.Log($"[SerializationTester] {testName}: PASS");
            }
            else
            {
                Debug.LogError($"[SerializationTester] {testName}: FAIL");
            }
        }
        catch (Exception e)
        {
            _results.Add(new SerializationTestResult
            {
                testName = testName,
                typeName = typeof(T).Name,
                success = false,
                error = e.Message
            });
            Debug.LogError($"[SerializationTester] {testName}: ERROR - {e.Message}");
        }
    }

    void TestNestedTypeSerialization()
    {
        Debug.Log("[SerializationTester] Testing nested type serialization...");

        // Permission 클래스 (중첩된 enum)
        TestClassSerialization("GetPermissionPermission", new GetPermissionPermission
        {
            Name = PermissionName.Camera,
            Access = PermissionAccess.Access
        }, json => json.ToLower().Contains("camera") && json.ToLower().Contains("access"));

        // SetDeviceOrientationOptions
        TestClassSerialization("SetDeviceOrientationOptions", new SetDeviceOrientationOptions
        {
            Type = SetDeviceOrientationOptionsType.Landscape
        }, json => json.ToLower().Contains("landscape"));
    }

    void SendResults()
    {
        _testCompleted = true;

        int successCount = 0;
        int failCount = 0;

        foreach (var result in _results)
        {
            if (result.success) successCount++;
            else failCount++;
        }

        var report = new SerializationTestReport
        {
            totalTests = _results.Count,
            successCount = successCount,
            failCount = failCount,
            results = _results
        };

        string json = JsonUtility.ToJson(report, true);

        Debug.Log("[SerializationTester] ========================================");
        Debug.Log("[SerializationTester] SERIALIZATION TESTS COMPLETED");
        Debug.Log($"[SerializationTester] Total: {report.totalTests}");
        Debug.Log($"[SerializationTester] Success: {report.successCount}");
        Debug.Log($"[SerializationTester] Failed: {report.failCount}");
        Debug.Log("[SerializationTester] ========================================");

        // 실패한 테스트 상세 출력
        if (failCount > 0)
        {
            Debug.LogError("[SerializationTester] FAILED TESTS:");
            foreach (var result in _results)
            {
                if (!result.success)
                {
                    Debug.LogError($"  - {result.testName}: {result.error}");
                    if (!string.IsNullOrEmpty(result.serializedJson))
                    {
                        Debug.LogError($"    Got: {result.serializedJson}");
                    }
                    if (!string.IsNullOrEmpty(result.expectedJson))
                    {
                        Debug.LogError($"    Expected: {result.expectedJson}");
                    }
                }
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SendSerializationTestResults(json);
            Debug.Log("[SerializationTester] Results sent to JavaScript");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SerializationTester] Failed to send results: {e.Message}");
        }
#else
        Debug.Log($"[SerializationTester] Results (Editor): {json}");
#endif
    }

    void OnGUI()
    {
        if (!showUI) return;

        int padding = 20;
        int width = 400;
        int height = 300;
        int x = Screen.width - width - padding;

        GUI.Box(new Rect(x, padding, width, height), "");

        GUILayout.BeginArea(new Rect(x + 10, padding + 10, width - 20, height - 20));

        GUILayout.Label("Serialization Tests", GUI.skin.box);
        GUILayout.Space(5);

        if (!_testStarted)
        {
            GUILayout.Label("Waiting...");
        }
        else if (!_testCompleted)
        {
            GUILayout.Label("Testing...");
        }
        else
        {
            int successCount = 0;
            int failCount = 0;
            foreach (var r in _results)
            {
                if (r.success) successCount++;
                else failCount++;
            }

            GUILayout.Label($"Total: {_results.Count}");
            GUI.color = Color.green;
            GUILayout.Label($"Pass: {successCount}");
            GUI.color = failCount > 0 ? Color.red : Color.white;
            GUILayout.Label($"Fail: {failCount}");
            GUI.color = Color.white;

            showDetailedResults = GUILayout.Toggle(showDetailedResults, "Show Details");

            if (showDetailedResults)
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
                foreach (var r in _results)
                {
                    GUI.color = r.success ? Color.green : Color.red;
                    GUILayout.Label($"{(r.success ? "✓" : "✗")} {r.testName}");
                }
                GUI.color = Color.white;
                GUILayout.EndScrollView();
            }
        }

        GUILayout.EndArea();
    }

    [Serializable]
    public class SerializationTestResult
    {
        public string testName;
        public string typeName;
        public string originalValue;
        public string serializedJson;
        public string expectedJson;
        public bool jsonMatches;
        public bool roundTripSuccess;
        public bool success;
        public string error;
    }

    [Serializable]
    public class SerializationTestReport
    {
        public int totalTests;
        public int successCount;
        public int failCount;
        public List<SerializationTestResult> results;
    }
}
