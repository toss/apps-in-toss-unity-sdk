using System;
using System.Threading.Tasks;
using UnityEngine;
using AppsInToss;

/// <summary>
/// E2E 테스트용 부트스트래퍼 - 런타임에 필요한 컴포넌트들을 동적으로 생성
/// Unity 6에서 Editor에서 AddComponent로 추가한 스크립트가 Scene 직렬화 시 누락되는 문제를 해결
/// </summary>
public static class E2EBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        Initialize();
    }

    /// <summary>
    /// Public initialization method that can be called from E2EBootstrapperHelper
    /// </summary>
    public static void Initialize()
    {
        Debug.Log("[E2EBootstrapper] ===== BOOTSTRAPPER STARTED =====");

        // URL 파라미터로 E2E 모드 확인
        bool isE2EMode = IsE2EMode();
        Debug.Log($"[E2EBootstrapper] Mode: {(isE2EMode ? "E2E Test" : "Interactive Test App")}");

        // BenchmarkManager 찾기 또는 생성
        GameObject benchmarkManager = GameObject.Find("BenchmarkManager");
        if (benchmarkManager == null)
        {
            benchmarkManager = new GameObject("BenchmarkManager");
            Debug.Log("[E2EBootstrapper] Created BenchmarkManager GameObject");
        }

        // E2E 모드인 경우 자동 테스트 컴포넌트 추가
        if (isE2EMode)
        {
            InitializeE2EComponents(benchmarkManager);
        }
        else
        {
            // 일반 모드인 경우 대화형 테스터 추가
            InitializeInteractiveComponents(benchmarkManager);
        }
    }

    /// <summary>
    /// URL 파라미터로 E2E 모드 여부 확인
    /// WebGL: ?e2e=true 또는 &e2e=true 파라미터 체크
    /// Editor: 항상 E2E 모드로 취급 (개발 편의성)
    /// </summary>
    private static bool IsE2EMode()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[E2EBootstrapper] Application.absoluteURL is empty, defaulting to Interactive mode");
                return false;
            }

            string urlLower = url.ToLower();
            bool isE2E = urlLower.Contains("?e2e=true") || urlLower.Contains("&e2e=true");
            Debug.Log($"[E2EBootstrapper] URL: {url}, IsE2E: {isE2E}");
            return isE2E;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[E2EBootstrapper] Failed to check URL: {ex.Message}");
            return false;
        }
#else
        // Editor에서는 Interactive 모드로 변경 (InteractiveAPITester 테스트용)
        return false;
#endif
    }

    /// <summary>
    /// E2E 자동 테스트 컴포넌트 초기화
    /// </summary>
    private static void InitializeE2EComponents(GameObject benchmarkManager)
    {
        Debug.Log("[E2EBootstrapper] Initializing E2E test components...");

        // RuntimeAPITester 추가 (Test 4용)
        if (benchmarkManager.GetComponent<RuntimeAPITester>() == null)
        {
            var runtimeTester = benchmarkManager.AddComponent<RuntimeAPITester>();
            runtimeTester.autoRunOnStart = false;  // JavaScript에서 트리거
            runtimeTester.startDelay = 0f;
            runtimeTester.showUI = true;
            runtimeTester.showDetailedResults = false;
            Debug.Log("[E2EBootstrapper] Added RuntimeAPITester component (waiting for trigger)");
        }

        // SerializationTester 추가 (Test 5용)
        if (benchmarkManager.GetComponent<SerializationTester>() == null)
        {
            var serializationTester = benchmarkManager.AddComponent<SerializationTester>();
            serializationTester.autoRunOnStart = false;  // JavaScript에서 트리거
            serializationTester.startDelay = 0f;
            serializationTester.showUI = true;
            serializationTester.showDetailedResults = false;
            Debug.Log("[E2EBootstrapper] Added SerializationTester component (waiting for trigger)");
        }

        // E2ETestTrigger 추가 (JavaScript → Unity 테스트 트리거용)
        if (benchmarkManager.GetComponent<E2ETestTrigger>() == null)
        {
            benchmarkManager.AddComponent<E2ETestTrigger>();
            Debug.Log("[E2EBootstrapper] Added E2ETestTrigger component");
        }

        // CameraController 추가
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraController>();
            Debug.Log("[E2EBootstrapper] Added CameraController component");
        }

        Debug.Log("[E2EBootstrapper] E2E test components initialization complete");
    }

    /// <summary>
    /// 대화형 테스터 컴포넌트 초기화
    /// </summary>
    private static void InitializeInteractiveComponents(GameObject benchmarkManager)
    {
        Debug.Log("[E2EBootstrapper] Initializing interactive test app components...");

        // InteractiveAPITester 추가 (VisibilityBGMTester 등 하위 테스터 포함)
        if (benchmarkManager.GetComponent<InteractiveAPITester>() == null)
        {
            benchmarkManager.AddComponent<InteractiveAPITester>();
            Debug.Log("[E2EBootstrapper] Added InteractiveAPITester component");
        }

        // CameraController는 양쪽 모두 필요
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraController>();
            Debug.Log("[E2EBootstrapper] Added CameraController component");
        }

        Debug.Log("[E2EBootstrapper] Interactive test app components initialization complete");
    }
}

/// <summary>
/// JavaScript에서 Unity 테스트를 트리거하기 위한 헬퍼 컴포넌트
/// SendMessage는 MonoBehaviour에만 동작하므로 별도 컴포넌트 필요
/// </summary>
public class E2ETestTrigger : MonoBehaviour
{
    // 중첩 콜백 비동기 왕복 검증용 사전 등록 콜백 식별자.
    // 등록 키는 "{CallbackId}_{CallbackName}" 이므로 Playwright는 OnNestedCallback payload의
    // CallbackId/CallbackName을 이 값들로 맞춰 결제 없이 콜백을 직접 구동한다.
    public const string NestedCallbackName = "processProductGrant";
    public const string NestedAsyncCallbackId = "e2e-nested-async";
    public const string NestedThrowCallbackId = "e2e-nested-throw";
    // 동기 구현으로는 불가능한 "지연 후 응답"을 증명하기 위한 대기 시간(ms).
    public const int NestedAsyncDelayMs = 300;

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void RegisterTriggerFunctions();
#endif

    void Awake()
    {
        // JavaScript에서 호출 가능한 트리거 함수 등록
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            RegisterTriggerFunctions();
            Debug.Log("[E2ETestTrigger] Trigger functions registered in JavaScript");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[E2ETestTrigger] Failed to register trigger functions: {e.Message}");
        }
#endif
    }

    void Start()
    {
        // 결제 이벤트 없이 중첩 콜백(processProductGrant) 비동기 왕복을 검증하기 위해
        // 테스트용 콜백을 사전 등록한다. AITCore.Instance 접근이 "AITCore" GameObject를
        // 생성하므로 이후 SendMessage('AITCore','OnNestedCallback', ...)가 도달한다.
        // Playwright가 이 콜백들을 직접 구동하고 __AITRespondToNestedCallback 응답을 관찰한다.
#if AIT_SDK_1_7_OR_LATER
        try
        {
            // 지연 후 true 반환 — async/await 왕복이 실제로 대기됨을 증명 (지연 resolve).
            // WebGL은 단일 스레드라 Task.Delay 대신 플레이어 루프가 구동하는 코루틴으로 대기한다.
            AITCore.Instance.RegisterNestedCallback<object, bool>(
                NestedAsyncCallbackId, NestedCallbackName, (object data) => DelayedGrant());

            // 예외를 던지는 콜백 — dispatch가 잡아 false로 응답(응답 유실 없음)
            AITCore.Instance.RegisterNestedCallback<object, bool>(
                NestedThrowCallbackId, NestedCallbackName, (object data) =>
                    Task.FromException<bool>(new Exception("e2e forced failure")));

            Debug.Log("[E2ETestTrigger] Registered e2e nested callbacks (async/throw)");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[E2ETestTrigger] Failed to register nested callbacks: {e.Message}");
        }
#endif
    }

    /// <summary>
    /// 지연 후 true로 완료되는 Task를 반환한다 (WebGL-safe: WaitForSecondsRealtime 코루틴 → TCS).
    /// 지연 resolve는 구 동기 구현으로는 불가능하므로 async 왕복의 결정적 증거가 된다.
    /// </summary>
    private Task<bool> DelayedGrant()
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(DelayThenResolve(tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator DelayThenResolve(TaskCompletionSource<bool> tcs)
    {
        yield return new WaitForSecondsRealtime(NestedAsyncDelayMs / 1000f);
        tcs.SetResult(true);
    }

    /// <summary>
    /// JavaScript에서 호출: window.TriggerAPITest() → SendMessage('BenchmarkManager', 'TriggerAPITest')
    /// </summary>
    public void TriggerAPITest()
    {
        Debug.Log("[E2ETestTrigger] API Test triggered from JavaScript");
        var tester = GetComponent<RuntimeAPITester>();
        if (tester != null)
        {
            tester.RunAPITests();
        }
        else
        {
            Debug.LogError("[E2ETestTrigger] RuntimeAPITester component not found!");
        }
    }

    /// <summary>
    /// JavaScript에서 호출: window.TriggerSerializationTest()
    /// </summary>
    public void TriggerSerializationTest()
    {
        Debug.Log("[E2ETestTrigger] Serialization Test triggered from JavaScript");
        var tester = GetComponent<SerializationTester>();
        if (tester != null)
        {
            tester.RunSerializationTests();
        }
        else
        {
            Debug.LogError("[E2ETestTrigger] SerializationTester component not found!");
        }
    }

}
