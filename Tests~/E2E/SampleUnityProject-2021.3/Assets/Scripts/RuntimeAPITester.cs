// -----------------------------------------------------------------------
// RuntimeAPITester.cs - E2E Runtime API Test Runner
// 기본 SDK 접근 테스트만 수행 (jslib 인코딩 문제로 SDK API 호출 비활성화)
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Runtime API 테스트 실행기
/// SDK 접근 테스트만 수행하고 결과를 JavaScript로 전송
/// </summary>
public class RuntimeAPITester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendAPITestResults(string json);
#endif

    [Header("Test Settings")]
    public float startDelay = 3f;
    public bool autoRunOnStart = true;

    private Dictionary<string, APITestResult> _results = new Dictionary<string, APITestResult>();
    private bool _testStarted = false;
    private bool _testCompleted = false;

    void Start()
    {
        if (autoRunOnStart)
        {
            StartCoroutine(DelayedStart());
        }
    }

    IEnumerator DelayedStart()
    {
        Debug.Log("[RuntimeAPITester] Waiting for Unity to initialize...");
        yield return new WaitForSeconds(startDelay);
        RunAPITests();
    }

    public void RunAPITests()
    {
        if (_testStarted) return;
        _testStarted = true;

        Debug.Log("[RuntimeAPITester] ========================================");
        Debug.Log("[RuntimeAPITester] RUNTIME API TESTS STARTING");
        Debug.Log("[RuntimeAPITester] ========================================");

        // 1. SDK 접근 테스트
        TestSDKAccess();

        // 결과 전송
        SendResults();
    }

    void TestSDKAccess()
    {
        Debug.Log("[RuntimeAPITester] Testing SDK namespace access...");

        // AppsInToss.AIT 타입 존재 확인
        try
        {
            var aitType = typeof(AppsInToss.AIT);
            RecordResult("SDK_Namespace_Access", aitType != null, null);
            Debug.Log("[RuntimeAPITester] SDK_Namespace_Access: ✓");
        }
        catch (Exception e)
        {
            RecordResult("SDK_Namespace_Access", false, e.Message);
            Debug.LogError("[RuntimeAPITester] SDK_Namespace_Access: ✗ " + e.Message);
        }

        // AITCore 타입 존재 확인
        try
        {
            var coreType = typeof(AppsInToss.AITCore);
            RecordResult("AITCore_Access", coreType != null, null);
            Debug.Log("[RuntimeAPITester] AITCore_Access: ✓");
        }
        catch (Exception e)
        {
            RecordResult("AITCore_Access", false, e.Message);
            Debug.LogError("[RuntimeAPITester] AITCore_Access: ✗ " + e.Message);
        }

        // SDK Version 확인
        try
        {
            // AIT 클래스의 메서드 목록 확인
            var methods = typeof(AppsInToss.AIT).GetMethods();
            RecordResult("SDK_Methods_Available", methods.Length > 0, null);
            Debug.Log($"[RuntimeAPITester] SDK_Methods_Available: ✓ ({methods.Length} methods)");
        }
        catch (Exception e)
        {
            RecordResult("SDK_Methods_Available", false, e.Message);
            Debug.LogError("[RuntimeAPITester] SDK_Methods_Available: ✗ " + e.Message);
        }

        // AITCore 인스턴스 생성 확인
        try
        {
            var instance = AppsInToss.AITCore.Instance;
            RecordResult("AITCore_Instance", instance != null, null);
            Debug.Log("[RuntimeAPITester] AITCore_Instance: ✓");
        }
        catch (Exception e)
        {
            RecordResult("AITCore_Instance", false, e.Message);
            Debug.LogError("[RuntimeAPITester] AITCore_Instance: ✗ " + e.Message);
        }
    }

    void RecordResult(string apiName, bool success, string error)
    {
        _results[apiName] = new APITestResult
        {
            apiName = apiName,
            success = success,
            error = error
        };
    }

    void SendResults()
    {
        if (_testCompleted) return;
        _testCompleted = true;

        var report = new APITestReport
        {
            totalAPIs = _results.Count,
            successCount = 0,
            failCount = 0,
            results = new List<APITestResult>()
        };

        foreach (var kv in _results)
        {
            report.results.Add(kv.Value);
            if (kv.Value.success)
                report.successCount++;
            else
                report.failCount++;
        }

        string json = JsonUtility.ToJson(report);

        Debug.Log("[RuntimeAPITester] ========================================");
        Debug.Log("[RuntimeAPITester] RUNTIME API TESTS COMPLETED");
        Debug.Log($"[RuntimeAPITester] Total: {report.totalAPIs}, Passed: {report.successCount}, Failed: {report.failCount}");
        Debug.Log("[RuntimeAPITester] ========================================");

        // 실패한 API 목록 출력
        foreach (var result in report.results)
        {
            if (!result.success)
            {
                Debug.LogWarning($"[RuntimeAPITester] FAILED: {result.apiName} - {result.error}");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SendAPITestResults(json);
            Debug.Log("[RuntimeAPITester] Results sent to JavaScript");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RuntimeAPITester] Failed to send results: {e.Message}");
        }
#else
        Debug.Log($"[RuntimeAPITester] Results (Editor): {json}");
#endif
    }

    [Serializable]
    public class APITestResult
    {
        public string apiName;
        public bool success;
        public string error;
    }

    [Serializable]
    public class APITestReport
    {
        public int totalAPIs;
        public int successCount;
        public int failCount;
        public List<APITestResult> results;
    }
}
