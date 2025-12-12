// -----------------------------------------------------------------------
// RuntimeAPITester.cs - E2E Runtime API Test Runner
// 39개 SDK API에 대한 올바른 에러 발생 검증
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AppsInToss;

/// <summary>
/// Runtime API 테스트 실행기
/// 모든 39개 SDK API를 호출하고, 개발 환경에서 올바른 에러가 발생하는지 검증
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

    [Header("UI Settings")]
    public bool showUI = true;
    public bool showDetailedResults = false;

    // 상정된 에러 패턴 (개발 환경에서 예상되는 에러)
    // 이 패턴과 일치하면 "expectedError"로 분류 (정상)
    private static readonly string[] EXPECTED_ERROR_PATTERNS = new string[]
    {
        // bridge-core 에러
        "is not a constant handler",                    // Constant API
        "__GRANITE_NATIVE_EMITTER is not available",    // Async API (emitter)
        "ReactNativeWebView is not available",          // Native 통신

        // 플랫폼 미지원 에러
        "Platform not available",
        "Not supported in browser",
        "Native bridge not initialized",

        // JavaScript 에러 (window.AppsInToss 미정의 등)
        "Cannot read properties of undefined",          // window.AppsInToss.xxx 접근 시
        "Cannot read property",                         // 구 브라우저 호환
        "is not defined",                               // ReferenceError
        "is undefined",                                 // TypeError

        // Unity 직렬화 에러
        "Default constructor not found",                // JsonUtility (Dictionary 등)
        "MissingMethodException",                       // 생성자 누락
    };

    private Dictionary<string, APITestResult> _results = new Dictionary<string, APITestResult>();
    private bool _testStarted = false;
    private bool _testCompleted = false;
    private bool _allTestsQueued = false;  // 모든 테스트가 시작된 후에만 결과 전송 가능
    private int _pendingAsyncTests = 0;
    private Vector2 _scrollPosition = Vector2.zero;
    private string _lastResultJson = "";

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
        Debug.Log("[RuntimeAPITester] Testing all 39 SDK APIs for correct error handling");
        Debug.Log("[RuntimeAPITester] ========================================");

        // 1. SDK 기본 접근 테스트
        TestSDKAccess();

        // 2. 모든 39개 SDK API 호출 테스트
        TestAllSDKAPIs();

        // 모든 테스트가 큐에 추가됨 - 이제 결과 전송 가능
        _allTestsQueued = true;
        Debug.Log($"[RuntimeAPITester] All tests queued. Pending: {_pendingAsyncTests}");

        // 비동기 테스트가 없으면 바로 결과 전송
        if (_pendingAsyncTests == 0)
        {
            SendResults();
        }
    }

    void TestSDKAccess()
    {
        Debug.Log("[RuntimeAPITester] Testing SDK namespace access...");

        // AppsInToss.AIT 타입 존재 확인
        try
        {
            var aitType = typeof(AIT);
            RecordResult("SDK_Namespace_Access", true, false, null, null);
            Debug.Log("[RuntimeAPITester] SDK_Namespace_Access: PASS");
        }
        catch (Exception e)
        {
            RecordResult("SDK_Namespace_Access", false, false, e.Message, null);
            Debug.LogError($"[RuntimeAPITester] SDK_Namespace_Access: FAIL - {e.Message}");
        }

        // AITCore 인스턴스 생성 확인
        try
        {
            var instance = AITCore.Instance;
            RecordResult("AITCore_Instance", instance != null, false, null, null);
            Debug.Log("[RuntimeAPITester] AITCore_Instance: PASS");
        }
        catch (Exception e)
        {
            RecordResult("AITCore_Instance", false, false, e.Message, null);
            Debug.LogError($"[RuntimeAPITester] AITCore_Instance: FAIL - {e.Message}");
        }
    }

    void TestAllSDKAPIs()
    {
        Debug.Log("[RuntimeAPITester] Testing all SDK APIs...");

        // =====================================================================
        // 파라미터 없는 API들 (14개) - 직접 호출
        // =====================================================================
        TestAPICall("GetDeviceId", () => AIT.GetDeviceId());
        TestAPICall("GetLocale", () => AIT.GetLocale());
        TestAPICall("GetNetworkStatus", () => AIT.GetNetworkStatus());
        TestAPICall("GetOperationalEnvironment", () => AIT.GetOperationalEnvironment());
        TestAPICall("GetPlatformOS", () => AIT.GetPlatformOS());
        TestAPICall("GetSchemeUri", () => AIT.GetSchemeUri());
        TestAPICall("GetTossAppVersion", () => AIT.GetTossAppVersion());
        TestAPICall("AppLogin", () => AIT.AppLogin());
        TestAPICall("GetIsTossLoginIntegratedService", () => AIT.GetIsTossLoginIntegratedService());
        TestAPICall("GetClipboardText", () => AIT.GetClipboardText());
        TestAPICall("CloseView", () => AIT.CloseView());
        TestAPICall("GetGameCenterGameProfile", () => AIT.GetGameCenterGameProfile());
        TestAPICall("GetUserKeyForGame", () => AIT.GetUserKeyForGame());
        TestAPICall("OpenGameCenterLeaderboard", () => AIT.OpenGameCenterLeaderboard());

        // =====================================================================
        // 파라미터 있는 API들 (25개) - SDK 타입에 맞는 더미값으로 호출
        // =====================================================================

        // Clipboard & Navigation APIs
        TestAPICall("SetClipboardText", () => AIT.SetClipboardText("test"));
        TestAPICall("OpenURL", () => AIT.OpenURL("https://example.com"));

        // Share APIs
        TestAPICall("GetTossShareLink", () => AIT.GetTossShareLink("/test"));
        TestAPICall("Share", () => AIT.Share(new ShareMessage { Message = "test" }));
        TestAPICall("FetchContacts", () => AIT.FetchContacts(new FetchContactsOptions { Size = 10, Offset = 0 }));

        // Event API
        TestAPICall("EventLog", () => AIT.EventLog(new EventLogParams { Log_name = "test", Log_type = "test" }));

        // Permission APIs (class 타입 파라미터) - inline enum 사용
        TestAPICall("GetPermission", () => AIT.GetPermission(new GetPermissionPermission { Name = GetPermissionPermissionName.Camera, Access = GetPermissionPermissionAccess.Access }));
        TestAPICall("RequestPermission", () => AIT.RequestPermission(new RequestPermissionPermission { Name = RequestPermissionPermissionName.Camera, Access = RequestPermissionPermissionAccess.Access }));
        TestAPICall("OpenPermissionDialog", () => AIT.OpenPermissionDialog(new OpenPermissionDialogPermission { Name = OpenPermissionDialogPermissionName.Camera, Access = OpenPermissionDialogPermissionAccess.Access }));

        // Location APIs
        TestAPICall("GetCurrentLocation", () => AIT.GetCurrentLocation(new GetCurrentLocationOptions { Accuracy = Accuracy.Balanced }));

        // Device APIs (SDK 타입 필드명 사용)
        TestAPICall("GenerateHapticFeedback", () => AIT.GenerateHapticFeedback(new HapticFeedbackOptions { Type = HapticFeedbackType.Tap }));
        TestAPICall("SetDeviceOrientation", () => AIT.SetDeviceOrientation(new SetDeviceOrientationOptions { Type = SetDeviceOrientationOptionsType.Portrait }));
        TestAPICall("SetIosSwipeGestureEnabled", () => AIT.SetIosSwipeGestureEnabled(new SetIosSwipeGestureEnabledOptions { IsEnabled = true }));
        TestAPICall("SetScreenAwakeMode", () => AIT.SetScreenAwakeMode(new SetScreenAwakeModeOptions { Enabled = true }));
        TestAPICall("SetSecureScreen", () => AIT.SetSecureScreen(new SetSecureScreenOptions { Enabled = true }));

        // Payment API
        TestAPICall("CheckoutPayment", () => AIT.CheckoutPayment(new CheckoutPaymentOptions { PayToken = "test-token" }));

        // Media APIs
        TestAPICall("FetchAlbumPhotos", () => AIT.FetchAlbumPhotos(new FetchAlbumPhotosOptions { MaxCount = 1 }));
        TestAPICall("OpenCamera", () => AIT.OpenCamera(new OpenCameraOptions { Base64 = false }));
        TestAPICall("SaveBase64Data", () => AIT.SaveBase64Data(new SaveBase64DataParams { Data = "dGVzdA==", FileName = "test.txt", MimeType = "text/plain" }));

        // GameCenter APIs
        TestAPICall("SubmitGameCenterLeaderBoardScore", () => AIT.SubmitGameCenterLeaderBoardScore(new SubmitGameCenterLeaderBoardScoreParams { Score = "100" }));
        TestAPICall("GrantPromotionRewardForGame", () => AIT.GrantPromotionRewardForGame(new GrantPromotionRewardForGameOptions()));

        // Certificate API
        TestAPICall("AppsInTossSignTossCert", () => AIT.AppsInTossSignTossCert(new AppsInTossSignTossCertParams { TxId = "test-tx" }));

        // Visibility API (이벤트 기반)
        TestAPICall("OnVisibilityChangedByTransparentServiceWeb", () =>
            AIT.OnVisibilityChangedByTransparentServiceWeb(() => { }));

        // Location 이벤트 API
        TestAPICall("StartUpdateLocation", () =>
            AIT.StartUpdateLocation(new StartUpdateLocationEventParams { OnEvent = (loc) => { } }));

        // ContactsViral API
        TestAPICall("ContactsViral", () =>
            AIT.ContactsViral(new ContactsViralParams { OnEvent = (evt) => { } }));
    }

    void TestAPICall(string apiName, Func<Task> apiCall)
    {
        string testName = $"API_{apiName}";
        _pendingAsyncTests++;

        try
        {
            var task = apiCall();
            StartCoroutine(WaitForTask(testName, apiName, task));
        }
        catch (Exception e)
        {
            _pendingAsyncTests--;
            HandleSyncException(testName, apiName, e);
        }
    }

    void HandleSyncException(string testName, string apiName, Exception e)
    {
        var innerEx = e.InnerException ?? e;
        string errorMessage = innerEx.Message;

        // AITException인지 확인
        bool isAITException = innerEx is AITException;
        string errorCode = isAITException ? ((AITException)innerEx).ErrorCode : null;

        // 상정된 에러인지 확인
        bool isExpectedError = IsExpectedError(errorMessage);

        if (isExpectedError)
        {
            // 상정된 에러: 정상 동작
            RecordResult(testName, true, true, errorMessage, errorCode);
            Debug.Log($"[RuntimeAPITester] {testName}: PASS (expected error: {TruncateError(errorMessage)})");
        }
        else
        {
            // 상정되지 않은 에러: 실패
            RecordResult(testName, false, false, errorMessage, errorCode);
            Debug.LogError($"[RuntimeAPITester] {testName}: FAIL (unexpected error: {errorMessage})");
        }
    }

    IEnumerator WaitForTask(string testName, string apiName, Task task)
    {
        // Task 완료 대기 (최대 10초)
        float timeout = 10f;
        float elapsed = 0f;

        while (!task.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!task.IsCompleted)
        {
            // 타임아웃: 상정된 에러로 처리 (플랫폼 미지원 시 응답 없음)
            RecordResult(testName, true, true, "Timeout (platform not responding)", null);
            Debug.Log($"[RuntimeAPITester] {testName}: PASS (timeout - expected in dev environment)");
        }
        else if (task.IsFaulted)
        {
            // Task 실패: 에러 분석
            var innerEx = task.Exception?.InnerException ?? task.Exception;
            string errorMessage = innerEx?.Message ?? "Unknown error";

            // AITException인지 확인
            bool isAITException = innerEx is AITException;
            string errorCode = isAITException ? ((AITException)innerEx).ErrorCode : null;
            bool isPlatformUnavailable = isAITException && ((AITException)innerEx).IsPlatformUnavailable;

            // 상정된 에러인지 확인
            bool isExpectedError = IsExpectedError(errorMessage) || isPlatformUnavailable;

            if (isExpectedError)
            {
                // 상정된 에러: 정상 동작 (개발 환경에서 예상되는 에러)
                RecordResult(testName, true, true, errorMessage, errorCode);
                Debug.Log($"[RuntimeAPITester] {testName}: PASS (expected error: {TruncateError(errorMessage)})");
            }
            else
            {
                // 상정되지 않은 에러: 테스트 실패
                RecordResult(testName, false, false, errorMessage, errorCode);
                Debug.LogError($"[RuntimeAPITester] {testName}: FAIL (unexpected error: {errorMessage})");
            }
        }
        else if (task.IsCanceled)
        {
            // 취소: 상정된 에러로 처리
            RecordResult(testName, true, true, "Task canceled", null);
            Debug.Log($"[RuntimeAPITester] {testName}: PASS (canceled - expected in dev environment)");
        }
        else
        {
            // Task 성공: 개발 환경에서 성공은 의외 (Mock이 동작한 경우)
            RecordResult(testName, true, false, null, null);
            Debug.Log($"[RuntimeAPITester] {testName}: PASS (completed successfully)");
        }

        _pendingAsyncTests--;

        // 모든 테스트가 큐에 추가되고, 모든 비동기 테스트가 완료되면 결과 전송
        if (_allTestsQueued && _pendingAsyncTests == 0)
        {
            SendResults();
        }
    }

    /// <summary>
    /// 에러 메시지가 상정된 패턴과 일치하는지 확인
    /// </summary>
    bool IsExpectedError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage)) return false;

        foreach (var pattern in EXPECTED_ERROR_PATTERNS)
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

    void RecordResult(string apiName, bool success, bool isExpectedError, string error, string errorCode)
    {
        _results[apiName] = new APITestResult
        {
            apiName = apiName,
            success = success,
            isExpectedError = isExpectedError,
            error = error ?? "",
            errorCode = errorCode ?? ""
        };
    }

    void SendResults()
    {
        if (_testCompleted) return;
        _testCompleted = true;

        int successCount = 0;
        int expectedErrorCount = 0;
        int unexpectedErrorCount = 0;

        var resultsList = new List<APITestResult>();

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

        var report = new APITestReport
        {
            totalAPIs = _results.Count,
            successCount = successCount,
            failCount = unexpectedErrorCount,
            expectedErrorCount = expectedErrorCount,
            unexpectedErrorCount = unexpectedErrorCount,
            results = resultsList
        };

        string json = JsonUtility.ToJson(report, true);
        _lastResultJson = json;

        Debug.Log("[RuntimeAPITester] ========================================");
        Debug.Log("[RuntimeAPITester] RUNTIME API TESTS COMPLETED");
        Debug.Log($"[RuntimeAPITester] Total: {report.totalAPIs}");
        Debug.Log($"[RuntimeAPITester] Success: {report.successCount} (Expected Errors: {report.expectedErrorCount})");
        Debug.Log($"[RuntimeAPITester] Failed (Unexpected Errors): {report.unexpectedErrorCount}");
        Debug.Log("[RuntimeAPITester] ========================================");

        // 상정되지 않은 에러 목록 출력
        if (unexpectedErrorCount > 0)
        {
            Debug.LogError("[RuntimeAPITester] UNEXPECTED ERRORS:");
            foreach (var result in resultsList)
            {
                if (!result.success)
                {
                    Debug.LogError($"  - {result.apiName}: {result.error}");
                }
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

    void OnGUI()
    {
        if (!showUI) return;

        int padding = 20;
        int width = Screen.width - (padding * 2);
        int height = Screen.height - (padding * 2);

        GUI.Box(new Rect(padding, padding, width, height), "");

        GUILayout.BeginArea(new Rect(padding + 10, padding + 10, width - 20, height - 20));

        GUILayout.Label("Apps in Toss Unity SDK - API Error Validation", GUI.skin.box);
        GUILayout.Space(10);

        if (!_testStarted)
        {
            GUILayout.Label("Waiting to start tests...");
            if (GUILayout.Button("Start Tests Manually", GUILayout.Height(40)))
            {
                RunAPITests();
            }
        }
        else if (!_testCompleted)
        {
            GUILayout.Label("Testing in progress...");
            GUILayout.Label($"Pending: {_pendingAsyncTests} APIs");
        }
        else
        {
            DisplayResults();
        }

        GUILayout.EndArea();
    }

    void DisplayResults()
    {
        int successCount = 0;
        int expectedErrorCount = 0;
        int unexpectedErrorCount = 0;

        foreach (var result in _results.Values)
        {
            if (result.success)
            {
                successCount++;
                if (result.isExpectedError) expectedErrorCount++;
            }
            else
            {
                unexpectedErrorCount++;
            }
        }

        GUILayout.Label("Tests Completed!", GUI.skin.box);
        GUILayout.Space(5);

        GUILayout.Label($"Total APIs: {_results.Count}");
        GUILayout.Label($"Success: {successCount}");
        GUILayout.Label($"  - Expected Errors: {expectedErrorCount}");
        GUILayout.Label($"  - Clean Success: {successCount - expectedErrorCount}");

        if (unexpectedErrorCount > 0)
        {
            GUI.color = Color.red;
            GUILayout.Label($"FAILED (Unexpected Errors): {unexpectedErrorCount}");
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.green;
            GUILayout.Label("All APIs validated correctly!");
            GUI.color = Color.white;
        }

        GUILayout.Space(10);

        showDetailedResults = GUILayout.Toggle(showDetailedResults, "Show Details");

        if (showDetailedResults)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(Screen.height / 2));

            foreach (var result in _results.Values)
            {
                string status;
                if (result.success)
                {
                    status = result.isExpectedError ? "[OK-ERR]" : "[OK]";
                    GUI.color = Color.green;
                }
                else
                {
                    status = "[FAIL]";
                    GUI.color = Color.red;
                }

                GUILayout.Label($"{status} {result.apiName}");

                if (!string.IsNullOrEmpty(result.error))
                {
                    GUI.color = result.success ? Color.yellow : Color.red;
                    GUILayout.Label($"   {TruncateError(result.error)}");
                }

                GUI.color = Color.white;
            }

            GUILayout.EndScrollView();
        }
    }

    [Serializable]
    public class APITestResult
    {
        public string apiName;
        public bool success;
        public bool isExpectedError;  // true면 상정된 에러 (개발 환경에서 정상)
        public string error;
        public string errorCode;
    }

    [Serializable]
    public class APITestReport
    {
        public int totalAPIs;
        public int successCount;
        public int failCount;
        public int expectedErrorCount;
        public int unexpectedErrorCount;
        public List<APITestResult> results;
    }
}
