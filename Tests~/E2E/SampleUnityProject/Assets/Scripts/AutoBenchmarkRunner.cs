using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// 자동 벤치마크 실행 및 결과 저장
/// 씬 로드 시 자동으로 벤치마크를 순차 실행하고 결과를 파일로 저장 후 종료
/// </summary>
public class AutoBenchmarkRunner : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendBenchmarkData(string json);
#endif

    [Header("Benchmark Settings")]
    public bool autoRunOnStart = true;
    public string outputFileName = "benchmark_results.json";
    public bool quitAfterComplete = true;

    [Header("Test Duration (seconds)")]
    public float baselineTestDuration = 10f;
    public float physicsTestDuration = 15f;
    public float renderingTestDuration = 15f;
    public float combinedTestDuration = 20f;

    [Header("Physics Test Settings")]
    public int physicsTestObjectCount = 50;

    [Header("Rendering Test Settings")]
    public int renderingTestGridSize = 10;

    private BenchmarkResults results;
    private List<float> fpsSamples;
    private bool isRunning = false;

    void Start()
    {
        if (autoRunOnStart)
        {
            StartCoroutine(RunAllBenchmarks());
        }
    }

    IEnumerator RunAllBenchmarks()
    {
        if (isRunning)
        {
            Debug.LogWarning("Benchmark already running!");
            yield break;
        }

        isRunning = true;
        results = new BenchmarkResults();
        results.testStartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        results.unityVersion = Application.unityVersion;
        results.platform = Application.platform.ToString();

        Debug.Log("========================================");
        Debug.Log("AUTO BENCHMARK RUNNER STARTED");
        Debug.Log("========================================");

        // 1. Baseline Test (아무것도 없는 상태)
        yield return StartCoroutine(RunBaselineTest());

        // 2. Physics Stress Test
        yield return StartCoroutine(RunPhysicsTest());

        // 3. Rendering Benchmark Test
        yield return StartCoroutine(RunRenderingTest());

        // 4. Combined Test (Physics + Rendering)
        yield return StartCoroutine(RunCombinedTest());

        // 결과 저장
        SaveResults();

        Debug.Log("========================================");
        Debug.Log("AUTO BENCHMARK RUNNER COMPLETED");
        Debug.Log("Results saved to: " + GetOutputPath());
        Debug.Log("========================================");

        isRunning = false;

        // 자동 종료
        if (quitAfterComplete)
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("Quitting application...");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    IEnumerator RunBaselineTest()
    {
        Debug.Log("\n[1/4] Running Baseline Test...");

        TestResult testResult = new TestResult();
        testResult.testName = "Baseline";
        testResult.description = "Empty scene with camera and light only";

        yield return StartCoroutine(MeasurePerformance(baselineTestDuration, testResult));

        results.baselineTest = testResult;
        Debug.Log($"Baseline Test Complete - Avg FPS: {testResult.avgFps:F2}");
    }

    IEnumerator RunPhysicsTest()
    {
        Debug.Log("\n[2/4] Running Physics Stress Test...");

        // PhysicsStressTest 컴포넌트 찾기 또는 생성
        PhysicsStressTest physicsTest = FindObjectOfType<PhysicsStressTest>();
        if (physicsTest == null)
        {
            GameObject go = new GameObject("PhysicsStressTest");
            physicsTest = go.AddComponent<PhysicsStressTest>();
        }

        physicsTest.objectsPerWave = 10;
        physicsTest.spawnInterval = 0.5f;
        physicsTest.maxObjects = physicsTestObjectCount;
        physicsTest.autoStart = false;

        TestResult testResult = new TestResult();
        testResult.testName = "Physics Stress Test";
        testResult.description = $"Spawning {physicsTestObjectCount} physics objects";

        // 물리 오브젝트 생성 시작
        physicsTest.StartSpawning();

        // 측정
        yield return StartCoroutine(MeasurePerformance(physicsTestDuration, testResult));

        // 정리
        physicsTest.StopSpawning();
        yield return new WaitForSeconds(1f);
        physicsTest.ClearAllObjects();

        results.physicsTest = testResult;
        Debug.Log($"Physics Test Complete - Avg FPS: {testResult.avgFps:F2}");
    }

    IEnumerator RunRenderingTest()
    {
        Debug.Log("\n[3/4] Running Rendering Benchmark Test...");

        // RenderingBenchmark 컴포넌트 찾기 또는 생성
        RenderingBenchmark renderingBenchmark = FindObjectOfType<RenderingBenchmark>();
        if (renderingBenchmark == null)
        {
            GameObject go = new GameObject("RenderingBenchmark");
            renderingBenchmark = go.AddComponent<RenderingBenchmark>();
        }

        renderingBenchmark.gridSize = renderingTestGridSize;
        renderingBenchmark.spacing = 2f;
        renderingBenchmark.animateObjects = true;
        renderingBenchmark.useInstancing = false;

        TestResult testResult = new TestResult();
        testResult.testName = "Rendering Benchmark";
        testResult.description = $"Grid {renderingTestGridSize}x{renderingTestGridSize} ({renderingTestGridSize * renderingTestGridSize} objects)";

        // 벤치마크 생성 대기
        yield return new WaitForSeconds(1f);

        // 측정
        yield return StartCoroutine(MeasurePerformance(renderingTestDuration, testResult));

        // 정리
        renderingBenchmark.enabled = false;

        results.renderingTest = testResult;
        Debug.Log($"Rendering Test Complete - Avg FPS: {testResult.avgFps:F2}");
    }

    IEnumerator RunCombinedTest()
    {
        Debug.Log("\n[4/4] Running Combined Test (Physics + Rendering)...");

        // 둘 다 활성화
        PhysicsStressTest physicsTest = FindObjectOfType<PhysicsStressTest>();
        RenderingBenchmark renderingBenchmark = FindObjectOfType<RenderingBenchmark>();

        if (physicsTest != null && renderingBenchmark != null)
        {
            physicsTest.maxObjects = physicsTestObjectCount / 2; // 절반만
            physicsTest.StartSpawning();
            renderingBenchmark.enabled = true;
        }

        TestResult testResult = new TestResult();
        testResult.testName = "Combined Test";
        testResult.description = "Physics + Rendering simultaneously";

        // 측정
        yield return StartCoroutine(MeasurePerformance(combinedTestDuration, testResult));

        // 정리
        if (physicsTest != null)
        {
            physicsTest.StopSpawning();
            physicsTest.ClearAllObjects();
        }
        if (renderingBenchmark != null)
        {
            renderingBenchmark.enabled = false;
        }

        results.combinedTest = testResult;
        Debug.Log($"Combined Test Complete - Avg FPS: {testResult.avgFps:F2}");
    }

    IEnumerator MeasurePerformance(float duration, TestResult result)
    {
        fpsSamples = new List<float>();
        float startTime = Time.realtimeSinceStartup;
        float lastSampleTime = startTime;
        float sampleInterval = 0.1f; // 0.1초마다 샘플링

        long startMemory = GC.GetTotalMemory(false);

        while (Time.realtimeSinceStartup - startTime < duration)
        {
            if (Time.realtimeSinceStartup - lastSampleTime >= sampleInterval)
            {
                float fps = 1.0f / Time.unscaledDeltaTime;
                fpsSamples.Add(fps);
                lastSampleTime = Time.realtimeSinceStartup;
            }
            yield return null;
        }

        long endMemory = GC.GetTotalMemory(false);

        // 통계 계산
        if (fpsSamples.Count > 0)
        {
            float sum = 0;
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (float fps in fpsSamples)
            {
                sum += fps;
                if (fps < min) min = fps;
                if (fps > max) max = fps;
            }

            result.avgFps = sum / fpsSamples.Count;
            result.minFps = min;
            result.maxFps = max;
            result.sampleCount = fpsSamples.Count;
        }

        result.memoryUsedMB = (endMemory - startMemory) / (1024f * 1024f);
        result.totalMemoryMB = endMemory / (1024f * 1024f);
    }

    void SaveResults()
    {
        string json = JsonUtility.ToJson(results, true);
        string csv = GenerateCSV();

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 환경: 브라우저 다운로드 + LocalStorage 저장
        SaveResultsWebGL(json, csv);
#else
        // Standalone/Editor: 파일로 저장
        SaveResultsStandalone(json, csv);
#endif
    }

    void SaveResultsWebGL(string json, string csv)
    {
        try
        {
            // 콘솔에 결과 출력
            Debug.Log("=== BENCHMARK RESULTS ===");
            Debug.Log($"Baseline: {results.baselineTest.avgFps:F2} FPS");
            Debug.Log($"Physics: {results.physicsTest.avgFps:F2} FPS");
            Debug.Log($"Rendering: {results.renderingTest.avgFps:F2} FPS");
            Debug.Log($"Combined: {results.combinedTest.avgFps:F2} FPS");

            // JavaScript로 벤치마크 데이터 전송
            var benchmarkData = new BenchmarkDataForJS
            {
                avgFps = results.baselineTest.avgFps,
                minFps = results.baselineTest.minFps,
                maxFps = results.baselineTest.maxFps,
                memoryUsageMB = results.baselineTest.totalMemoryMB,
                physicsAvgFps = results.physicsTest.avgFps,
                renderingAvgFps = results.renderingTest.avgFps,
                combinedAvgFps = results.combinedTest.avgFps
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            SendBenchmarkData(JsonUtility.ToJson(benchmarkData));
            Debug.Log("[Benchmark] Data sent to JavaScript");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save WebGL results: {e.Message}");
        }
    }

    [System.Serializable]
    public class BenchmarkDataForJS
    {
        public float avgFps;
        public float minFps;
        public float maxFps;
        public float memoryUsageMB;
        public float physicsAvgFps;
        public float renderingAvgFps;
        public float combinedAvgFps;
    }

    void SaveResultsStandalone(string json, string csv)
    {
        string path = GetOutputPath();

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"Results saved successfully to: {path}");

            string csvPath = path.Replace(".json", ".csv");
            File.WriteAllText(csvPath, csv);
            Debug.Log($"CSV results saved to: {csvPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save results: {e.Message}");
        }
    }


    string GenerateCSV()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Test Name,Avg FPS,Min FPS,Max FPS,Memory Used (MB),Total Memory (MB),Sample Count");

        AppendTestToCSV(csv, results.baselineTest);
        AppendTestToCSV(csv, results.physicsTest);
        AppendTestToCSV(csv, results.renderingTest);
        AppendTestToCSV(csv, results.combinedTest);

        return csv.ToString();
    }

    void AppendTestToCSV(StringBuilder csv, TestResult test)
    {
        if (test != null)
        {
            csv.AppendLine($"{test.testName},{test.avgFps:F2},{test.minFps:F2},{test.maxFps:F2},{test.memoryUsedMB:F2},{test.totalMemoryMB:F2},{test.sampleCount}");
        }
    }

    string GetOutputPath()
    {
        // 프로젝트 루트 디렉토리에 저장
        string directory = Application.dataPath + "/../BenchmarkResults";

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"benchmark_{timestamp}.json";

        return Path.Combine(directory, filename);
    }

    void OnGUI()
    {
        if (isRunning)
        {
            GUI.color = Color.white;
            GUI.Box(new Rect(10, 10, 300, 100), "");

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(20, 20, 280, 30), "AUTO BENCHMARK RUNNING", style);

            style.fontSize = 12;
            GUI.Label(new Rect(20, 50, 280, 20), "Please wait...", style);
            GUI.Label(new Rect(20, 70, 280, 20), $"FPS: {(1.0f / Time.unscaledDeltaTime):F1}", style);
        }
    }
}

[System.Serializable]
public class BenchmarkResults
{
    public string testStartTime;
    public string unityVersion;
    public string platform;
    public TestResult baselineTest;
    public TestResult physicsTest;
    public TestResult renderingTest;
    public TestResult combinedTest;
}

[System.Serializable]
public class TestResult
{
    public string testName;
    public string description;
    public float avgFps;
    public float minFps;
    public float maxFps;
    public float memoryUsedMB;
    public float totalMemoryMB;
    public int sampleCount;
}
