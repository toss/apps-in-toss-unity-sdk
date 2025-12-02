using UnityEngine;

/// <summary>
/// E2E 테스트용 부트스트래퍼 - 런타임에 필요한 컴포넌트들을 동적으로 생성
/// Unity 6에서 Editor에서 AddComponent로 추가한 스크립트가 Scene 직렬화 시 누락되는 문제를 해결
/// </summary>
public class E2EBootstrapper : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        Debug.Log("[E2EBootstrapper] Initializing E2E test components...");

        // BenchmarkManager 찾기 또는 생성
        GameObject benchmarkManager = GameObject.Find("BenchmarkManager");
        if (benchmarkManager == null)
        {
            benchmarkManager = new GameObject("BenchmarkManager");
            Debug.Log("[E2EBootstrapper] Created BenchmarkManager GameObject");
        }

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

        // CameraController 추가
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraController>();
            Debug.Log("[E2EBootstrapper] Added CameraController component");
        }

        Debug.Log("[E2EBootstrapper] E2E test components initialization complete");
    }
}
