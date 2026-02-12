// -----------------------------------------------------------------------
// FirebaseAPITester.cs - E2E Runtime Firebase API Test Runner
// Firebase SDK API 동작 검증 (enableFirebase 설정에 따라 자동 모드 전환)
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AppsInToss;
using AppsInToss.Firebase;

#if UNITY_6000_0_OR_NEWER
using APICallFunc = System.Func<UnityEngine.Awaitable>;
#else
using APICallFunc = System.Func<System.Threading.Tasks.Task>;
#endif

/// <summary>
/// Firebase API 테스트 실행기
/// enableFirebase=false: API 호출 시 예상 에러 발생 검증
/// enableFirebase=true: Firebase 초기화 + 익명 로그인 등 실제 동작 검증
/// </summary>
public class FirebaseAPITester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendFirebaseTestResults(string json);
#endif

    [Header("Test Settings")]
    public float startDelay = 3f;
    public bool autoRunOnStart = true;

    [Header("UI Settings")]
    public bool showUI = true;
    public bool showDetailedResults = false;

    // Firebase 비활성 환경에서 예상되는 에러 패턴
    private static readonly string[] FIREBASE_EXPECTED_ERROR_PATTERNS = new string[]
    {
        "__AIT_Firebase is not defined",
        "Cannot read properties of undefined",
        "Cannot read property",
        "is not a function",
        "is not defined",
        "Firebase not initialized",
        "is undefined",
    };

    private Dictionary<string, FirebaseTestResult> _results = new Dictionary<string, FirebaseTestResult>();
    private bool _testStarted = false;
    private bool _testCompleted = false;
    private bool _allTestsQueued = false;
    private int _pendingAsyncTests = 0;
    private Vector2 _scrollPosition = Vector2.zero;
    private bool _firebaseEnabled = false;

    void Start()
    {
        if (autoRunOnStart)
        {
            StartCoroutine(DelayedStart());
        }
    }

    IEnumerator DelayedStart()
    {
        Debug.Log("[FirebaseAPITester] Waiting for Unity to initialize...");
        yield return new WaitForSeconds(startDelay);
        RunFirebaseTests();
    }

    public void RunFirebaseTests()
    {
        if (_testStarted) return;
        _testStarted = true;

        // Firebase 브릿지 존재 여부로 모드 감지
        _firebaseEnabled = CheckFirebaseBridgeExists();

        Debug.Log("[FirebaseAPITester] ========================================");
        Debug.Log("[FirebaseAPITester] FIREBASE API TESTS STARTING");
        Debug.Log($"[FirebaseAPITester] Mode: {(_firebaseEnabled ? "Firebase ENABLED" : "Firebase DISABLED")}");
        Debug.Log("[FirebaseAPITester] ========================================");

        if (_firebaseEnabled)
        {
            TestFirebaseEnabled();
        }
        else
        {
            TestFirebaseDisabled();
        }

        _allTestsQueued = true;
        Debug.Log($"[FirebaseAPITester] All tests queued. Pending: {_pendingAsyncTests}");

        if (_pendingAsyncTests == 0)
        {
            SendResults();
        }
    }

    /// <summary>
    /// WebGL에서 window.__AIT_Firebase 존재 여부 확인
    /// </summary>
    bool CheckFirebaseBridgeExists()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            // AITFirebase.Initialize()를 호출하지 않고 브릿지 존재만 확인
            // jslib에서 __Firebase_initializeApp_Internal이 존재하면 true
            return CheckFirebaseBridgeInJS();
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool CheckFirebaseBridgeInJS();
#endif

    // =====================================================
    // Firebase DISABLED 모드 테스트
    // =====================================================
    void TestFirebaseDisabled()
    {
        Debug.Log("[FirebaseAPITester] Testing Firebase DISABLED mode...");

        // Initialize → 예상 에러
        TestAPICall("Firebase_Initialize", async () => { await AITFirebase.Initialize(); });

        // SignInAnonymously → 예상 에러
        TestAPICall("Firebase_SignInAnonymously", async () => { await AITFirebase.SignInAnonymously(); });

        // SignOut → 예상 에러
        TestAPICall("Firebase_SignOut", async () => { await AITFirebase.SignOut(); });

        // LogEvent → void 메서드, 에러가 발생할 수도/안 할 수도
        TestSyncCall("Firebase_LogEvent", () =>
        {
            AITFirebase.LogEvent("e2e_test");
        });

        // SetUserId → void 메서드
        TestSyncCall("Firebase_SetUserId", () =>
        {
            AITFirebase.SetUserId("e2e-user");
        });

        // OnAuthStateChanged → 예상 에러 또는 noop 반환
        TestSyncCall("Firebase_OnAuthStateChanged", () =>
        {
            var unsub = AITFirebase.OnAuthStateChanged((user) => { }, null);
            if (unsub != null)
            {
                unsub(); // 구독 해제 호출
            }
        });
    }

    // =====================================================
    // Firebase ENABLED 모드 테스트
    // =====================================================
    void TestFirebaseEnabled()
    {
        Debug.Log("[FirebaseAPITester] Testing Firebase ENABLED mode...");

        // Initialize → 성공
        TestAPICall("Firebase_Initialize", async () => { await AITFirebase.Initialize(); });

        // SignInAnonymously → FirebaseUser 반환
        TestAPICallWithValidation("Firebase_SignInAnonymously", async () =>
        {
            var user = await AITFirebase.SignInAnonymously();
            if (user == null)
                throw new Exception("SignInAnonymously returned null user");
            if (string.IsNullOrEmpty(user.Uid))
                throw new Exception("SignInAnonymously returned user with empty Uid");
            if (!user.IsAnonymous)
                throw new Exception("SignInAnonymously returned non-anonymous user");
        });

        // LogEvent → 에러 없이 완료
        TestSyncCall("Firebase_LogEvent", () =>
        {
            AITFirebase.LogEvent("e2e_test", "{\"source\":\"e2e\"}");
        });

        // SetUserId → 에러 없이 완료
        TestSyncCall("Firebase_SetUserId", () =>
        {
            AITFirebase.SetUserId("e2e-user");
        });

        // SetUserProperties → 에러 없이 완료
        TestSyncCall("Firebase_SetUserProperties", () =>
        {
            AITFirebase.SetUserProperties("{\"env\":\"e2e\"}");
        });

        // SetAnalyticsCollectionEnabled → 에러 없이 완료
        TestSyncCall("Firebase_SetAnalyticsCollectionEnabled", () =>
        {
            AITFirebase.SetAnalyticsCollectionEnabled(true);
        });

        // OnAuthStateChanged → 콜백 등록 성공
        TestSyncCall("Firebase_OnAuthStateChanged", () =>
        {
            bool callbackReceived = false;
            var unsub = AITFirebase.OnAuthStateChanged(
                (user) => { callbackReceived = true; },
                null
            );
            if (unsub == null)
                throw new Exception("OnAuthStateChanged returned null unsubscribe action");
            unsub(); // 구독 해제
        });

        // SignOut → 성공
        TestAPICall("Firebase_SignOut", async () => { await AITFirebase.SignOut(); });
    }

    // =====================================================
    // 테스트 헬퍼
    // =====================================================

    void TestSyncCall(string testName, Action action)
    {
        try
        {
            action();
            RecordResult(testName, true, false, null);
            Debug.Log($"[FirebaseAPITester] {testName}: PASS");
        }
        catch (Exception e)
        {
            string errorMessage = e.InnerException?.Message ?? e.Message;
            bool isExpected = !_firebaseEnabled && IsExpectedError(errorMessage);

            if (isExpected)
            {
                RecordResult(testName, true, true, errorMessage);
                Debug.Log($"[FirebaseAPITester] {testName}: PASS (expected error: {TruncateError(errorMessage)})");
            }
            else
            {
                RecordResult(testName, false, false, errorMessage);
                Debug.LogError($"[FirebaseAPITester] {testName}: FAIL ({errorMessage})");
            }
        }
    }

    void TestAPICall(string testName, APICallFunc apiCall)
    {
        _pendingAsyncTests++;
        try
        {
#if UNITY_6000_0_OR_NEWER
            var awaitable = apiCall();
            StartCoroutine(WaitForAwaitable(testName, awaitable));
#else
            var task = apiCall();
            StartCoroutine(WaitForTask(testName, task));
#endif
        }
        catch (Exception e)
        {
            _pendingAsyncTests--;
            HandleException(testName, e);
        }
    }

    void TestAPICallWithValidation(string testName, APICallFunc apiCall)
    {
        // Same as TestAPICall - validation logic is inside the lambda
        TestAPICall(testName, apiCall);
    }

    void HandleException(string testName, Exception e)
    {
        var innerEx = e.InnerException ?? e;
        string errorMessage = innerEx.Message;

        bool isExpected = !_firebaseEnabled && IsExpectedError(errorMessage);
        bool isPlatformUnavailable = innerEx is AITException aitEx && aitEx.IsPlatformUnavailable;

        if (isExpected || (!_firebaseEnabled && isPlatformUnavailable))
        {
            RecordResult(testName, true, true, errorMessage);
            Debug.Log($"[FirebaseAPITester] {testName}: PASS (expected error: {TruncateError(errorMessage)})");
        }
        else
        {
            RecordResult(testName, false, false, errorMessage);
            Debug.LogError($"[FirebaseAPITester] {testName}: FAIL ({errorMessage})");
        }
    }

#if UNITY_6000_0_OR_NEWER
    IEnumerator WaitForAwaitable(string testName, Awaitable awaitable)
    {
        float timeout = 30f;
        float elapsed = 0f;
        bool completed = false;
        Exception caughtException = null;

        RunAwaitableAsync(awaitable, () => completed = true, ex => { completed = true; caughtException = ex; });

        while (!completed && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!completed)
        {
            bool isExpected = !_firebaseEnabled;
            RecordResult(testName, isExpected, isExpected, "Timeout");
            Debug.Log($"[FirebaseAPITester] {testName}: {(isExpected ? "PASS (timeout - expected)" : "FAIL (timeout)")}");
        }
        else if (caughtException != null)
        {
            HandleException(testName, caughtException);
        }
        else
        {
            RecordResult(testName, true, false, null);
            Debug.Log($"[FirebaseAPITester] {testName}: PASS");
        }

        _pendingAsyncTests--;
        if (_allTestsQueued && _pendingAsyncTests == 0)
        {
            SendResults();
        }
    }

    async void RunAwaitableAsync(Awaitable awaitable, Action onComplete, Action<Exception> onError)
    {
        try
        {
            await awaitable;
            onComplete?.Invoke();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }
#endif

    IEnumerator WaitForTask(string testName, Task task)
    {
        float timeout = 30f;
        float elapsed = 0f;

        while (!task.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!task.IsCompleted)
        {
            bool isExpected = !_firebaseEnabled;
            RecordResult(testName, isExpected, isExpected, "Timeout");
            Debug.Log($"[FirebaseAPITester] {testName}: {(isExpected ? "PASS (timeout - expected)" : "FAIL (timeout)")}");
        }
        else if (task.IsFaulted)
        {
            var innerEx = task.Exception?.InnerException ?? task.Exception;
            HandleException(testName, innerEx);
        }
        else if (task.IsCanceled)
        {
            bool isExpected = !_firebaseEnabled;
            RecordResult(testName, isExpected, isExpected, "Task canceled");
            Debug.Log($"[FirebaseAPITester] {testName}: {(isExpected ? "PASS (canceled - expected)" : "FAIL (canceled)")}");
        }
        else
        {
            RecordResult(testName, true, false, null);
            Debug.Log($"[FirebaseAPITester] {testName}: PASS");
        }

        _pendingAsyncTests--;
        if (_allTestsQueued && _pendingAsyncTests == 0)
        {
            SendResults();
        }
    }

    bool IsExpectedError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage)) return false;

        foreach (var pattern in FIREBASE_EXPECTED_ERROR_PATTERNS)
        {
            if (errorMessage.Contains(pattern))
            {
                return true;
            }
        }
        return false;
    }

    string TruncateError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "";
        return error.Length > 60 ? error.Substring(0, 60) + "..." : error;
    }

    void RecordResult(string testName, bool success, bool isExpectedError, string error)
    {
        _results[testName] = new FirebaseTestResult
        {
            testName = testName,
            success = success,
            isExpectedError = isExpectedError,
            error = error ?? ""
        };
    }

    void SendResults()
    {
        if (_testCompleted) return;
        _testCompleted = true;

        int successCount = 0;
        int expectedErrorCount = 0;
        int unexpectedErrorCount = 0;

        var resultsList = new List<FirebaseTestResult>();

        foreach (var kv in _results)
        {
            resultsList.Add(kv.Value);
            if (kv.Value.success)
            {
                successCount++;
                if (kv.Value.isExpectedError)
                {
                    expectedErrorCount++;
                }
            }
            else
            {
                unexpectedErrorCount++;
            }
        }

        var report = new FirebaseTestReport
        {
            firebaseEnabled = _firebaseEnabled,
            totalTests = _results.Count,
            successCount = successCount,
            expectedErrorCount = expectedErrorCount,
            unexpectedErrorCount = unexpectedErrorCount,
            results = resultsList
        };

        string json = JsonUtility.ToJson(report, true);

        Debug.Log("[FirebaseAPITester] ========================================");
        Debug.Log("[FirebaseAPITester] FIREBASE API TESTS COMPLETED");
        Debug.Log($"[FirebaseAPITester] Mode: {(_firebaseEnabled ? "ENABLED" : "DISABLED")}");
        Debug.Log($"[FirebaseAPITester] Total: {report.totalTests}");
        Debug.Log($"[FirebaseAPITester] Success: {report.successCount} (Expected Errors: {report.expectedErrorCount})");
        Debug.Log($"[FirebaseAPITester] Failed: {report.unexpectedErrorCount}");
        Debug.Log("[FirebaseAPITester] ========================================");

        if (unexpectedErrorCount > 0)
        {
            Debug.LogError("[FirebaseAPITester] UNEXPECTED ERRORS:");
            foreach (var result in resultsList)
            {
                if (!result.success)
                {
                    Debug.LogError($"  - {result.testName}: {result.error}");
                }
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SendFirebaseTestResults(json);
            Debug.Log("[FirebaseAPITester] Results sent to JavaScript");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseAPITester] Failed to send results: {e.Message}");
        }
#else
        Debug.Log($"[FirebaseAPITester] Results (Editor): {json}");
#endif
    }

    void OnGUI()
    {
        if (!showUI || !_testCompleted) return;

        int yOffset = Screen.height / 2;
        GUI.Box(new Rect(10, yOffset, 300, 120), "Firebase API Tests");

        int y = yOffset + 25;
        GUI.Label(new Rect(20, y, 280, 20), $"Mode: {(_firebaseEnabled ? "ENABLED" : "DISABLED")}");
        y += 20;
        GUI.Label(new Rect(20, y, 280, 20), $"Total: {_results.Count}");
        y += 20;

        int fails = 0;
        foreach (var r in _results.Values) { if (!r.success) fails++; }

        if (fails > 0)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(20, y, 280, 20), $"FAILED: {fails}");
        }
        else
        {
            GUI.color = Color.green;
            GUI.Label(new Rect(20, y, 280, 20), "All tests passed!");
        }
        GUI.color = Color.white;
    }

    [Serializable]
    public class FirebaseTestResult
    {
        public string testName;
        public bool success;
        public bool isExpectedError;
        public string error;
    }

    [Serializable]
    public class FirebaseTestReport
    {
        public bool firebaseEnabled;
        public int totalTests;
        public int successCount;
        public int expectedErrorCount;
        public int unexpectedErrorCount;
        public List<FirebaseTestResult> results;
    }
}
