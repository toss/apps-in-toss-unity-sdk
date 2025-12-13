using UnityEngine;

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

        // AutoBenchmarkRunner 추가
        if (benchmarkManager.GetComponent<AutoBenchmarkRunner>() == null)
        {
            var autoRunner = benchmarkManager.AddComponent<AutoBenchmarkRunner>();
            autoRunner.autoRunOnStart = true;
            autoRunner.quitAfterComplete = true;
            Debug.Log("[E2EBootstrapper] Added AutoBenchmarkRunner component");
        }

        // PerformanceBenchmark 추가
        if (benchmarkManager.GetComponent<PerformanceBenchmark>() == null)
        {
            benchmarkManager.AddComponent<PerformanceBenchmark>();
            Debug.Log("[E2EBootstrapper] Added PerformanceBenchmark component");
        }

        // PhysicsStressTest 추가
        if (benchmarkManager.GetComponent<PhysicsStressTest>() == null)
        {
            var physicsTest = benchmarkManager.AddComponent<PhysicsStressTest>();
            physicsTest.autoStart = false;
            Debug.Log("[E2EBootstrapper] Added PhysicsStressTest component");
        }

        // RenderingBenchmark 추가
        if (benchmarkManager.GetComponent<RenderingBenchmark>() == null)
        {
            var renderingBenchmark = benchmarkManager.AddComponent<RenderingBenchmark>();
            renderingBenchmark.enabled = false;
            Debug.Log("[E2EBootstrapper] Added RenderingBenchmark component");
        }

        // RuntimeAPITester 추가 (Test 7용)
        if (benchmarkManager.GetComponent<RuntimeAPITester>() == null)
        {
            var runtimeTester = benchmarkManager.AddComponent<RuntimeAPITester>();
            runtimeTester.autoRunOnStart = true;
            runtimeTester.startDelay = 3f;
            runtimeTester.showUI = true;
            runtimeTester.showDetailedResults = false;
            Debug.Log("[E2EBootstrapper] Added RuntimeAPITester component");
        }

        // SerializationTester 추가 (Test 8용)
        if (benchmarkManager.GetComponent<SerializationTester>() == null)
        {
            var serializationTester = benchmarkManager.AddComponent<SerializationTester>();
            serializationTester.autoRunOnStart = true;
            serializationTester.startDelay = 4f; // RuntimeAPITester 이후에 실행
            serializationTester.showUI = true;
            serializationTester.showDetailedResults = false;
            Debug.Log("[E2EBootstrapper] Added SerializationTester component");
        }

        // MemoryPressureTester 추가 (Test 9용)
        if (benchmarkManager.GetComponent<MemoryPressureTester>() == null)
        {
            var memoryTester = benchmarkManager.AddComponent<MemoryPressureTester>();
            memoryTester.autoRunOnStart = true;
            memoryTester.startDelay = 5f; // SerializationTester 이후에 실행
            memoryTester.showUI = true;
            Debug.Log("[E2EBootstrapper] Added MemoryPressureTester component");
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

        // InteractiveAPITester 추가
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
