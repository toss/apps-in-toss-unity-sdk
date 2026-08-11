// -----------------------------------------------------------------------
// RuntimeAPITester.cs - E2E Runtime API Test Runner
// APITestCatalog.AllAPINames(3.0 풀빌드 기준 51개, 2026-08 SDK 3.0 커버리지
// 감사로 갱신)에 대한 올바른 에러 발생 검증. API 이름 목록은 APITestCatalog.cs와
// 단일화되어 있으며, 이 파일과 SDKAPIReflectionTests.cs가 함께 소비한다. 버전
// 종속 항목은 양쪽 모두 동일한 #if 가드로 감싸 sdk_version_override 매트릭스에서
// 카탈로그-호출부 드리프트가 나지 않게 한다.
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AppsInToss;

#if UNITY_6000_0_OR_NEWER
using APICallFunc = System.Func<UnityEngine.Awaitable>;
#else
using APICallFunc = System.Func<System.Threading.Tasks.Task>;
#endif

/// <summary>
/// Runtime API 테스트 실행기
/// APITestCatalog.AllAPINames의 모든 SDK API를 호출하고, 개발 환경에서 올바른
/// 에러가 발생하는지 검증
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
        "ReactNativeWebView is not available",          // Native 통신 (~2.x)

        // web-framework 3.x assertWebViewEnvironment 가드
        // ("apps-in-toss 웹뷰 환경이 아니에요. 토스 앱 안에서만 호출할 수 있어요.")
        // 가드 조건은 2.x와 동일하게 window.ReactNativeWebView 이지만 메시지만 바뀌었다.
        "웹뷰 환경이 아니",

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
        Debug.Log($"[RuntimeAPITester] Testing all {APITestCatalog.AllAPINames.Length} SDK APIs for correct error handling");
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
        TestAPICall("GetDeviceId", async () => { await AIT.GetDeviceId(); });
        TestAPICall("GetLocale", async () => { await AIT.GetLocale(); });
        TestAPICall("GetNetworkStatus", async () => { await AIT.GetNetworkStatus(); });
#if AIT_SDK_1_7_1_OR_LATER
        TestAPICall("GetOperationalEnvironment", async () => { await AIT.GetOperationalEnvironment(); });
#endif
        TestAPICall("GetPlatformOS", async () => { await AIT.GetPlatformOS(); });
        TestAPICall("GetSchemeUri", async () => { await AIT.GetSchemeUri(); });
        TestAPICall("GetTossAppVersion", async () => { await AIT.GetTossAppVersion(); });
        TestAPICall("AppLogin", async () => { await AIT.AppLogin(); });
        TestAPICall("GetIsTossLoginIntegratedService", async () => { await AIT.GetIsTossLoginIntegratedService(); });
        TestAPICall("GetClipboardText", async () => { await AIT.GetClipboardText(); });
        TestAPICall("CloseView", async () => { await AIT.CloseView(); });
        TestAPICall("GetGameCenterGameProfile", async () => { await AIT.GetGameCenterGameProfile(); });
        TestAPICall("GetUserKeyForGame", async () => { await AIT.GetUserKeyForGame(); });
        TestAPICall("OpenGameCenterLeaderboard", async () => { await AIT.OpenGameCenterLeaderboard(); });

        // =====================================================================
        // 파라미터 있는 API들 (25개) - SDK 타입에 맞는 더미값으로 호출
        // =====================================================================

        // Clipboard & Navigation APIs
        TestAPICall("SetClipboardText", async () => { await AIT.SetClipboardText("test"); });
        TestAPICall("OpenURL", async () => { await AIT.OpenURL("https://example.com"); });

        // Share APIs
        TestAPICall("GetTossShareLink", async () => { await AIT.GetTossShareLink("/test"); });
        TestAPICall("Share", async () => { await AIT.Share(new ShareMessage { Message = "test" }); });
        TestAPICall("FetchContacts", async () => { await AIT.FetchContacts(new FetchContactsOptions { Size = 10, Offset = 0 }); });

        // Event API
        TestAPICall("EventLog", async () => { await AIT.EventLog(new EventLogParams { Log_name = "test", Log_type = "test" }); });

        // Permission APIs (class 타입 파라미터) - PermissionName/PermissionAccess named enum 사용
        TestAPICall("GetPermission", async () => { await AIT.GetPermission(new GetPermissionPermission { Name = PermissionName.Camera, Access = PermissionAccess.Access }); });
        TestAPICall("RequestPermission", async () => { await AIT.RequestPermission(new RequestPermissionPermission { Name = PermissionName.Camera, Access = PermissionAccess.Access }); });
        TestAPICall("OpenPermissionDialog", async () => { await AIT.OpenPermissionDialog(new OpenPermissionDialogPermission { Name = PermissionName.Camera, Access = PermissionAccess.Access }); });

        // Location APIs
        TestAPICall("GetCurrentLocation", async () => { await AIT.GetCurrentLocation(new GetCurrentLocationOptions { Accuracy = Accuracy.Balanced }); });

        // Device APIs (SDK 타입 필드명 사용)
        TestAPICall("GenerateHapticFeedback", async () => { await AIT.GenerateHapticFeedback(new HapticFeedbackOptions { Type = HapticFeedbackType.Tap }); });
        TestAPICall("SetDeviceOrientation", async () => { await AIT.SetDeviceOrientation(new SetDeviceOrientationOptions { Type = SetDeviceOrientationOptionsType.Portrait }); });
        TestAPICall("SetIosSwipeGestureEnabled", async () => { await AIT.SetIosSwipeGestureEnabled(new SetIosSwipeGestureEnabledOptions { IsEnabled = true }); });
#if AIT_SDK_1_7_1_OR_LATER
        TestAPICall("SetScreenAwakeMode", async () => { await AIT.SetScreenAwakeMode(new SetScreenAwakeModeOptions { Enabled = true }); });
        TestAPICall("SetSecureScreen", async () => { await AIT.SetSecureScreen(new SetSecureScreenOptions { Enabled = true }); });
#endif

        // Payment API
        TestAPICall("CheckoutPayment", async () => { await AIT.CheckoutPayment(new CheckoutPaymentOptions { PayToken = "test-token" }); });

        // Media APIs
        TestAPICall("FetchAlbumPhotos", async () => { await AIT.FetchAlbumPhotos(new FetchAlbumPhotosOptions { MaxCount = 1 }); });
        TestAPICall("OpenCamera", async () => { await AIT.OpenCamera(new OpenCameraOptions { Base64 = false }); });
        TestAPICall("SaveBase64Data", async () => { await AIT.SaveBase64Data(new SaveBase64DataParams { Data = "dGVzdA==", FileName = "test.txt", MimeType = "text/plain" }); });

        // GameCenter APIs
        TestAPICall("SubmitGameCenterLeaderBoardScore", async () => { await AIT.SubmitGameCenterLeaderBoardScore(new SubmitGameCenterLeaderBoardScoreParams { Score = "100" }); });
        TestAPICall("GrantPromotionRewardForGame", async () => { await AIT.GrantPromotionRewardForGame(new GrantPromotionRewardForGameParams()); });

        // Other APIs
        TestAPICall("GetGroupId", async () => { await AIT.GetGroupId(); });

        // Certificate API
        TestAPICall("AppsInTossSignTossCert", async () => { await AIT.AppsInTossSignTossCert(new AppsInTossSignTossCertParams { TxId = "test-tx" }); });

        // Visibility API (이벤트 기반) - 콜백 분리 패턴
        // web-framework 3.0.0에서 제거됨(web-bridge → webview-bridge 리네임). semver상
        // 3.0.0-beta < 3.0.0 이라 asmdef versionDefines 임계값으로는 prerelease를 안정적으로
        // 분기할 수 없어, 메서드가 존재할 때만 리플렉션으로 호출한다 — 2.x/3.0.0 양쪽에서
        // 컴파일·동작을 모두 보장 (직접 호출 시 3.0.0 재생성 빌드에서 CS0117 발생).
        var onVisibilityMethod = typeof(AIT).GetMethod(
            "OnVisibilityChangedByTransparentServiceWeb",
            BindingFlags.Public | BindingFlags.Static);
        if (onVisibilityMethod != null)
        {
            TestAPICall("OnVisibilityChangedByTransparentServiceWeb", async () =>
            {
                onVisibilityMethod.Invoke(null, new object[] { (Action<bool>)((visible) => { }), null, null });
                await System.Threading.Tasks.Task.CompletedTask;
            });
        }

        // Location 이벤트 API - 콜백 분리 패턴
        TestAPICall("StartUpdateLocation", async () =>
            { AIT.StartUpdateLocation((loc) => { }, null); await System.Threading.Tasks.Task.CompletedTask; });

        // ContactsViral API - 콜백 분리 패턴
        TestAPICall("ContactsViral", async () =>
            { AIT.ContactsViral((evt) => { }, null); await System.Threading.Tasks.Task.CompletedTask; });

        // =====================================================================
        // SDK 3.0 신규 표면 (2026-08 감사로 편입, 12개)
        // 편입 기준: Task/Awaitable 반환 + 무인(unattended) 실행 안전.
        // 각 항목의 devtools mock 기본 동작(무인 자동 응답)은 감사 시점에
        // scratchpad의 devtools mock 소스로 확인했다 — file picker/사용자 입력을
        // 기다리는 API는 편입하지 않았다(SafeAreaInsetsSubscribe 등은 제외).
        //
        // sdk_version_override로 3.0 미만 web-framework를 재생성하면 이 타입/메서드가
        // 존재하지 않아 CS0117/CS0246로 컴파일이 깨지므로 AIT_SDK_3_0_OR_LATER로 감싼다
        // (asmdef versionDefines에 정의됨 — IAPv2Tester.cs와 동일 패턴).
        // =====================================================================

#if AIT_SDK_3_0_OR_LATER
        // Environment APIs
        TestAPICall("EnvGetDeploymentId", async () => { await AIT.EnvGetDeploymentId(); });
        TestAPICall("GetAppsInTossGlobals", async () => { await AIT.GetAppsInTossGlobals(); });
        TestAPICall("IsMinVersionSupported", async () => { await AIT.IsMinVersionSupported(new IsMinVersionSupportedMinVersions { Android = "1.0.0", Ios = "1.0.0" }); });

        // SystemInfo API
        TestAPICall("GetServerTime", async () => { await AIT.GetServerTime(); });

        // Storage APIs
        // 주의: 아래 set→get→remove→clear는 가독성을 위해 이 순서로 나열했을 뿐 실행 순서를
        // 보장하지 않는다. TestAPICall()은 apiCall()을 호출해 Task/Awaitable을 즉시 실행시키고
        // 완료를 기다리지 않은 채 바로 다음 TestAPICall()로 넘어가며(TestAllSDKAPIs()가 이 4개를
        // 연속 호출), 각 호출의 완료 대기는 별도 코루틴(WaitForTask/WaitForAwaitable)에 병렬로
        // 큐잉된다. 지금은 예외 없이 끝나는지만 확인하므로 문제 없지만, 나중에 "get으로 읽은 값이
        // set한 값과 같은지" 같은 값 검증을 추가하면 이 순서 비보장 때문에 flaky해질 수 있다 —
        // 값 검증이 필요해지면 개별 await 체이닝(순차 실행)으로 바꿀 것.
        TestAPICall("StorageSetItem", async () => { await AIT.StorageSetItem("e2e-test-key", "e2e-test-value"); });
        TestAPICall("StorageGetItem", async () => { await AIT.StorageGetItem("e2e-test-key"); });
        TestAPICall("StorageRemoveItem", async () => { await AIT.StorageRemoveItem("e2e-test-key"); });
        TestAPICall("StorageClearItems", async () => { await AIT.StorageClearItems(); });

        // Partner APIs
        TestAPICall("PartnerAddAccessoryButton", async () => { await AIT.PartnerAddAccessoryButton(new AddAccessoryButtonOptions { Id = "e2e-test", Title = "test", Icon = new AddAccessoryButtonOptionsIcon { Name = "test-icon" } }); });
        TestAPICall("PartnerRemoveAccessoryButton", async () => { await AIT.PartnerRemoveAccessoryButton(); });

        // Media API
        TestAPICall("FetchAlbumItems", async () => { await AIT.FetchAlbumItems(new FetchAlbumItemsOptions { MaxCount = 1 }); });

        // SafeArea API (Get만 편입 — Subscribe는 콜백 구독 패턴이라 이 하네스 제외)
        TestAPICall("SafeAreaInsetsGet", async () => { await AIT.SafeAreaInsetsGet(); });
#endif
    }

    /// <summary>
    /// APITestCatalog.AllAPINames(단일 소스)와 이 파일이 실제로 큐에 넣은 API 이름
    /// 사이의 드리프트를 검출한다. 카탈로그에는 있지만 위에서 TestAPICall로 호출하지
    /// 않은 이름, 혹은 그 반대(카탈로그에 없는데 호출된 이름)가 있으면 명시적으로
    /// 실패시켜 조용한 누락을 막는다.
    /// 모든 비동기 테스트가 완료된 뒤(SendResults 직전)에 호출해야 _results가
    /// 채워진 상태에서 정확히 비교할 수 있다.
    /// </summary>
    void VerifyCatalogConsistency()
    {
        var testedNames = new HashSet<string>();
        foreach (var key in _results.Keys)
        {
            if (key.StartsWith("API_"))
            {
                testedNames.Add(key.Substring("API_".Length));
            }
        }

        var catalogNames = new HashSet<string>(APITestCatalog.AllAPINames);
        var driftMessages = new List<string>();

        foreach (var name in testedNames)
        {
            // web-framework 3.0.0에서 제거된 API라 카탈로그에 의도적으로 없음(위
            // "Visibility API" 주석 참조) — 드리프트 아님.
            if (name == "OnVisibilityChangedByTransparentServiceWeb") continue;

            if (!catalogNames.Contains(name))
            {
                driftMessages.Add($"'{name}' 은 TestAllSDKAPIs()에서 호출되었지만 APITestCatalog.AllAPINames에 없어요");
            }
        }

        foreach (var name in catalogNames)
        {
            if (!testedNames.Contains(name))
            {
                driftMessages.Add($"'{name}' 은 APITestCatalog.AllAPINames에 있지만 TestAllSDKAPIs()에서 호출되지 않았어요");
            }
        }

        if (driftMessages.Count > 0)
        {
            string message = string.Join("; ", driftMessages);
            RecordResult("Catalog_Consistency", false, false, message, null);
            Debug.LogError($"[RuntimeAPITester] Catalog_Consistency: FAIL - {message}");
        }
        else
        {
            RecordResult("Catalog_Consistency", true, false, null, null);
            Debug.Log("[RuntimeAPITester] Catalog_Consistency: PASS");
        }
    }

    void TestAPICall(string apiName, APICallFunc apiCall)
    {
        string testName = $"API_{apiName}";
        _pendingAsyncTests++;

        try
        {
#if UNITY_6000_0_OR_NEWER
            var awaitable = apiCall();
            StartCoroutine(WaitForAwaitable(testName, apiName, awaitable));
#else
            var task = apiCall();
            StartCoroutine(WaitForTask(testName, apiName, task));
#endif
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

#if UNITY_6000_0_OR_NEWER
    IEnumerator WaitForAwaitable(string testName, string apiName, Awaitable awaitable)
    {
        // Awaitable 완료 대기 (최대 10초)
        float timeout = 10f;
        float elapsed = 0f;
        bool completed = false;
        Exception caughtException = null;

        // Awaitable을 async로 실행하고 완료 상태 추적
        RunAwaitableAsync(awaitable, () => completed = true, ex => { completed = true; caughtException = ex; });

        while (!completed && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!completed)
        {
            // 타임아웃: 상정된 에러로 처리
            RecordResult(testName, true, true, "Timeout (platform not responding)", null);
            Debug.Log($"[RuntimeAPITester] {testName}: PASS (timeout - expected in dev environment)");
        }
        else if (caughtException != null)
        {
            // 에러 발생: 분석
            var innerEx = caughtException.InnerException ?? caughtException;
            string errorMessage = innerEx?.Message ?? "Unknown error";

            bool isAITException = innerEx is AITException;
            string errorCode = isAITException ? ((AITException)innerEx).ErrorCode : null;
            bool isPlatformUnavailable = isAITException && ((AITException)innerEx).IsPlatformUnavailable;
            bool isExpectedError = IsExpectedError(errorMessage) || isPlatformUnavailable;

            if (isExpectedError)
            {
                RecordResult(testName, true, true, errorMessage, errorCode);
                Debug.Log($"[RuntimeAPITester] {testName}: PASS (expected error: {TruncateError(errorMessage)})");
            }
            else
            {
                RecordResult(testName, false, false, errorMessage, errorCode);
                Debug.LogError($"[RuntimeAPITester] {testName}: FAIL (unexpected error: {errorMessage})");
            }
        }
        else
        {
            // 성공
            RecordResult(testName, true, false, null, null);
            Debug.Log($"[RuntimeAPITester] {testName}: PASS (completed successfully)");
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

        // 모든 비동기 테스트가 큐에 추가되고 완료된 시점 — _results가 채워져 있으므로
        // 카탈로그 드리프트 검사를 여기서 수행한다.
        VerifyCatalogConsistency();

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
