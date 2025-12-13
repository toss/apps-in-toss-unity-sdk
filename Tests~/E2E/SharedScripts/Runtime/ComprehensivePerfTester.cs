// -----------------------------------------------------------------------
// ComprehensivePerfTester.cs - 종합 성능 테스트 실행기
// Physics + Rendering + Memory 통합 벤치마크
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// 종합 성능 테스트 실행기
/// CPU/GPU 부하 + 메모리 압박을 동시에 적용하여 현실적인 성능 측정
/// </summary>
public class ComprehensivePerfTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // Memory allocation (from MemoryPressureTester)
    [DllImport("__Internal")]
    private static extern int AllocateWasmMemory(int sizeMB);

    [DllImport("__Internal")]
    private static extern void FreeWasmMemory();

    [DllImport("__Internal")]
    private static extern int AllocateJSMemory(int sizeMB);

    [DllImport("__Internal")]
    private static extern void FreeJSMemory();

    [DllImport("__Internal")]
    private static extern int AllocateCanvasMemory(int count, int width, int height);

    [DllImport("__Internal")]
    private static extern void FreeCanvasMemory();

    [DllImport("__Internal")]
    private static extern string GetMemoryStatus();

    // Result sending
    [DllImport("__Internal")]
    private static extern void SendComprehensivePerfResults(string json);
#endif

    [Header("Test Settings")]
    public float startDelay = 2f;
    public bool autoRunOnStart = true;

    [Header("Test Durations (seconds)")]
    public float baselineDuration = 10f;
    public float physicsWithMemoryDuration = 15f;
    public float renderingWithMemoryDuration = 15f;
    public float fullLoadDuration = 20f;

    [Header("Physics Load Settings")]
    public int physicsObjectCount = 200;

    [Header("Rendering Load Settings")]
    public int renderingGridSize = 20;

    [Header("Memory Load Settings")]
    public int wasmMemoryMB = 40;
    public int jsMemoryMB = 40;
    public int canvasCount = 3;
    public int canvasSize = 1024;

    [Header("UI Settings")]
    public bool showUI = true;

    private ComprehensivePerfResults _results;
    private bool _testStarted = false;
    private bool _testCompleted = false;
    private string _currentPhase = "";
    private float _currentFps = 0f;
    private bool _oomOccurred = false;

    void Start()
    {
        if (autoRunOnStart)
        {
            StartCoroutine(DelayedStart());
        }
    }

    IEnumerator DelayedStart()
    {
        Debug.Log("[ComprehensivePerfTester] Waiting for Unity to initialize...");
        yield return new WaitForSeconds(startDelay);
        StartCoroutine(RunComprehensiveTests());
    }

    public void RunTests()
    {
        if (!_testStarted)
        {
            StartCoroutine(RunComprehensiveTests());
        }
    }

    IEnumerator RunComprehensiveTests()
    {
        if (_testStarted) yield break;
        _testStarted = true;

        _results = new ComprehensivePerfResults
        {
            testStartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            unityVersion = Application.unityVersion,
            platform = Application.platform.ToString()
        };

        Debug.Log("[ComprehensivePerfTester] ========================================");
        Debug.Log("[ComprehensivePerfTester] COMPREHENSIVE PERFORMANCE TEST STARTING");
        Debug.Log("[ComprehensivePerfTester] ========================================");

        // Phase 1: Baseline (no load)
        yield return StartCoroutine(RunBaselineTest());

        // Phase 2: Physics + Memory
        yield return StartCoroutine(RunPhysicsWithMemoryTest());

        // Phase 3: Rendering + Memory
        yield return StartCoroutine(RunRenderingWithMemoryTest());

        // Phase 4: Full Combined Load
        yield return StartCoroutine(RunFullLoadTest());

        // Send results
        SendResults();

        _testCompleted = true;

        Debug.Log("[ComprehensivePerfTester] ========================================");
        Debug.Log("[ComprehensivePerfTester] COMPREHENSIVE PERFORMANCE TEST COMPLETED");
        Debug.Log("[ComprehensivePerfTester] ========================================");
    }

    IEnumerator RunBaselineTest()
    {
        _currentPhase = "Baseline";
        Debug.Log("[ComprehensivePerfTester] Phase 1/4: Baseline Test (no load)...");

        var result = new PerfPhaseResult { phaseName = "Baseline" };
        yield return StartCoroutine(MeasurePerformance(baselineDuration, result));

        _results.baseline = result;
        Debug.Log($"[ComprehensivePerfTester] Baseline: Avg FPS={result.avgFps:F2}");
    }

    IEnumerator RunPhysicsWithMemoryTest()
    {
        _currentPhase = "Physics + Memory";
        Debug.Log($"[ComprehensivePerfTester] Phase 2/4: Physics ({physicsObjectCount} objects) + Memory ({wasmMemoryMB}MB)...");

        // Setup physics
        PhysicsStressTest physicsTest = GetOrCreatePhysicsTest();
        physicsTest.maxObjects = physicsObjectCount;
        physicsTest.objectsPerWave = 20;
        physicsTest.spawnInterval = 0.3f;

        // Allocate WASM memory
        AllocateWasm(wasmMemoryMB);

        // Start physics spawning
        physicsTest.StartSpawning();
        yield return new WaitForSeconds(2f); // Let objects spawn

        var result = new PerfPhaseResult { phaseName = "PhysicsWithMemory" };
        yield return StartCoroutine(MeasurePerformance(physicsWithMemoryDuration, result));

        // Cleanup
        physicsTest.StopSpawning();
        physicsTest.ClearAllObjects();
        FreeWasm();

        _results.physicsWithMemory = result;
        Debug.Log($"[ComprehensivePerfTester] Physics+Memory: Avg FPS={result.avgFps:F2}");

        yield return new WaitForSeconds(1f); // Recovery time
    }

    IEnumerator RunRenderingWithMemoryTest()
    {
        _currentPhase = "Rendering + Memory";
        Debug.Log($"[ComprehensivePerfTester] Phase 3/4: Rendering ({renderingGridSize}x{renderingGridSize}) + Canvas Memory...");

        // Setup rendering
        RenderingBenchmark renderingBenchmark = GetOrCreateRenderingBenchmark();
        renderingBenchmark.gridSize = renderingGridSize;
        renderingBenchmark.enabled = true;

        // Allocate JS and Canvas memory
        AllocateJS(jsMemoryMB);
        AllocateCanvas(canvasCount, canvasSize);

        yield return new WaitForSeconds(1f); // Let objects render

        var result = new PerfPhaseResult { phaseName = "RenderingWithMemory" };
        yield return StartCoroutine(MeasurePerformance(renderingWithMemoryDuration, result));

        // Cleanup
        renderingBenchmark.enabled = false;
        FreeJS();
        FreeCanvas();

        _results.renderingWithMemory = result;
        Debug.Log($"[ComprehensivePerfTester] Rendering+Memory: Avg FPS={result.avgFps:F2}");

        yield return new WaitForSeconds(1f); // Recovery time
    }

    IEnumerator RunFullLoadTest()
    {
        _currentPhase = "Full Load";
        Debug.Log("[ComprehensivePerfTester] Phase 4/4: Full Combined Load (Physics + Rendering + All Memory)...");

        // Setup all loads
        PhysicsStressTest physicsTest = GetOrCreatePhysicsTest();
        physicsTest.maxObjects = physicsObjectCount / 2; // Half for combined test
        physicsTest.objectsPerWave = 15;
        physicsTest.spawnInterval = 0.4f;

        RenderingBenchmark renderingBenchmark = GetOrCreateRenderingBenchmark();
        renderingBenchmark.gridSize = renderingGridSize;
        renderingBenchmark.enabled = true;

        // Allocate all memory types
        AllocateWasm(wasmMemoryMB);
        AllocateJS(jsMemoryMB);
        AllocateCanvas(canvasCount, canvasSize);

        // Start physics
        physicsTest.StartSpawning();
        yield return new WaitForSeconds(2f);

        var result = new PerfPhaseResult { phaseName = "FullLoad" };
        yield return StartCoroutine(MeasurePerformance(fullLoadDuration, result));

        // Cleanup everything
        physicsTest.StopSpawning();
        physicsTest.ClearAllObjects();
        renderingBenchmark.enabled = false;
        FreeAllMemory();

        result.oomOccurred = _oomOccurred;
        _results.fullLoad = result;
        Debug.Log($"[ComprehensivePerfTester] Full Load: Avg FPS={result.avgFps:F2}, OOM={_oomOccurred}");
    }

    IEnumerator MeasurePerformance(float duration, PerfPhaseResult result)
    {
        List<float> fpsSamples = new List<float>();
        float startTime = Time.realtimeSinceStartup;
        float sampleInterval = 0.1f;
        float lastSampleTime = startTime;
        long startMemory = GC.GetTotalMemory(false);

        while (Time.realtimeSinceStartup - startTime < duration)
        {
            if (Time.realtimeSinceStartup - lastSampleTime >= sampleInterval)
            {
                float fps = 1.0f / Time.unscaledDeltaTime;
                fpsSamples.Add(fps);
                _currentFps = fps;
                lastSampleTime = Time.realtimeSinceStartup;
            }
            yield return null;
        }

        long endMemory = GC.GetTotalMemory(false);

        if (fpsSamples.Count > 0)
        {
            float sum = 0, min = float.MaxValue, max = float.MinValue;
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

        result.memoryMB = endMemory / (1024f * 1024f);
    }

    #region Helper Components

    PhysicsStressTest GetOrCreatePhysicsTest()
    {
#if UNITY_2023_1_OR_NEWER
        PhysicsStressTest test = FindFirstObjectByType<PhysicsStressTest>();
#else
        PhysicsStressTest test = FindObjectOfType<PhysicsStressTest>();
#endif
        if (test == null)
        {
            GameObject go = new GameObject("PhysicsStressTest");
            test = go.AddComponent<PhysicsStressTest>();
            test.autoStart = false;
        }
        return test;
    }

    RenderingBenchmark GetOrCreateRenderingBenchmark()
    {
#if UNITY_2023_1_OR_NEWER
        RenderingBenchmark benchmark = FindFirstObjectByType<RenderingBenchmark>();
#else
        RenderingBenchmark benchmark = FindObjectOfType<RenderingBenchmark>();
#endif
        if (benchmark == null)
        {
            GameObject go = new GameObject("RenderingBenchmark");
            benchmark = go.AddComponent<RenderingBenchmark>();
            benchmark.animateObjects = true;
            benchmark.useInstancing = false;
        }
        return benchmark;
    }

    #endregion

    #region Memory Allocation Helpers

    void AllocateWasm(int sizeMB)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            int ptr = AllocateWasmMemory(sizeMB);
            if (ptr == 0)
            {
                Debug.LogWarning("[ComprehensivePerfTester] WASM allocation failed");
                _oomOccurred = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComprehensivePerfTester] WASM allocation exception: {e.Message}");
            _oomOccurred = true;
        }
#endif
    }

    void FreeWasm()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { FreeWasmMemory(); } catch { }
#endif
    }

    void AllocateJS(int sizeMB)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            int buffers = AllocateJSMemory(sizeMB);
            if (buffers < 0)
            {
                Debug.LogWarning("[ComprehensivePerfTester] JS allocation failed");
                _oomOccurred = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComprehensivePerfTester] JS allocation exception: {e.Message}");
            _oomOccurred = true;
        }
#endif
    }

    void FreeJS()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { FreeJSMemory(); } catch { }
#endif
    }

    void AllocateCanvas(int count, int size)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            AllocateCanvasMemory(count, size, size);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComprehensivePerfTester] Canvas allocation exception: {e.Message}");
        }
#endif
    }

    void FreeCanvas()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { FreeCanvasMemory(); } catch { }
#endif
    }

    void FreeAllMemory()
    {
        FreeWasm();
        FreeJS();
        FreeCanvas();
    }

    #endregion

    void SendResults()
    {
        _results.oomOccurred = _oomOccurred;

        string json = JsonUtility.ToJson(_results, true);

        Debug.Log("[ComprehensivePerfTester] ========================================");
        Debug.Log($"[ComprehensivePerfTester] Baseline:          {_results.baseline?.avgFps:F2} FPS");
        Debug.Log($"[ComprehensivePerfTester] Physics+Memory:    {_results.physicsWithMemory?.avgFps:F2} FPS");
        Debug.Log($"[ComprehensivePerfTester] Rendering+Memory:  {_results.renderingWithMemory?.avgFps:F2} FPS");
        Debug.Log($"[ComprehensivePerfTester] Full Load:         {_results.fullLoad?.avgFps:F2} FPS");
        Debug.Log($"[ComprehensivePerfTester] OOM Occurred:      {_oomOccurred}");
        Debug.Log("[ComprehensivePerfTester] ========================================");

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SendComprehensivePerfResults(json);
            Debug.Log("[ComprehensivePerfTester] Results sent to JavaScript");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ComprehensivePerfTester] Failed to send results: {e.Message}");
        }
#else
        Debug.Log($"[ComprehensivePerfTester] Results (Editor): {json}");
#endif
    }

    void OnGUI()
    {
        if (!showUI) return;

        int padding = 20;
        int width = 350;
        int height = 200;
        int x = padding;
        int y = Screen.height - height - padding;

        GUI.Box(new Rect(x, y, width, height), "");

        GUILayout.BeginArea(new Rect(x + 10, y + 10, width - 20, height - 20));

        GUILayout.Label("Comprehensive Performance Test", GUI.skin.box);
        GUILayout.Space(5);

        if (!_testStarted)
        {
            GUILayout.Label("Status: Waiting...");
            if (GUILayout.Button("Start Test"))
            {
                RunTests();
            }
        }
        else if (!_testCompleted)
        {
            GUILayout.Label($"Phase: {_currentPhase}");
            GUILayout.Label($"FPS: {_currentFps:F1}");

            if (_oomOccurred)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("OOM Detected");
                GUI.color = Color.white;
            }
        }
        else
        {
            GUI.color = _oomOccurred ? Color.red : Color.green;
            GUILayout.Label(_oomOccurred ? "COMPLETED (OOM)" : "COMPLETED");
            GUI.color = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"Baseline:         {_results.baseline?.avgFps:F1} FPS");
            GUILayout.Label($"Physics+Mem:      {_results.physicsWithMemory?.avgFps:F1} FPS");
            GUILayout.Label($"Rendering+Mem:    {_results.renderingWithMemory?.avgFps:F1} FPS");
            GUILayout.Label($"Full Load:        {_results.fullLoad?.avgFps:F1} FPS");
        }

        GUILayout.EndArea();
    }

    #region Result Data Classes

    [Serializable]
    public class PerfPhaseResult
    {
        public string phaseName;
        public float avgFps;
        public float minFps;
        public float maxFps;
        public float memoryMB;
        public int sampleCount;
        public bool oomOccurred;
    }

    [Serializable]
    public class ComprehensivePerfResults
    {
        public string testStartTime;
        public string unityVersion;
        public string platform;
        public bool oomOccurred;
        public PerfPhaseResult baseline;
        public PerfPhaseResult physicsWithMemory;
        public PerfPhaseResult renderingWithMemory;
        public PerfPhaseResult fullLoad;
    }

    #endregion
}
