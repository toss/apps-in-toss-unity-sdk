// -----------------------------------------------------------------------
// MemoryPressureTester.cs - E2E Memory Pressure Test Runner
// WASM 힙 + JavaScript 힙 + Canvas(GPU) 메모리 압박 테스트
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// 메모리 압박 테스트 실행기
/// WebGL 환경에서 WASM, JavaScript, Canvas 메모리에 부하를 주어 안정성 검증
/// </summary>
public class MemoryPressureTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
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

    [DllImport("__Internal")]
    private static extern void SendMemoryPressureResults(string json);
#endif

    [Header("Test Settings")]
    public float startDelay = 3f;
    public bool autoRunOnStart = true;

    [Header("Memory Allocation Settings")]
    [Tooltip("각 단계에서 할당할 메모리 크기 (MB)")]
    public int allocationSizeMB = 50;

    [Tooltip("WASM 메모리 할당 횟수")]
    public int wasmAllocationSteps = 5;

    [Tooltip("JS 메모리 할당 횟수")]
    public int jsAllocationSteps = 5;

    [Tooltip("동시 압박 테스트용 Canvas 개수")]
    public int canvasCount = 5;

    [Tooltip("Canvas 크기")]
    public int canvasSize = 2048;

    [Header("Performance Measurement")]
    [Tooltip("각 단계별 측정 시간 (초)")]
    public float measurementDuration = 5f;

    [Tooltip("동시 압박 측정 시간 (초)")]
    public float combinedMeasurementDuration = 10f;

    [Header("UI Settings")]
    public bool showUI = true;

    private List<MemoryPressureStepResult> _stepResults = new List<MemoryPressureStepResult>();
    private bool _testStarted = false;
    private bool _testCompleted = false;
    private string _currentPhase = "";
    private float _currentFps = 0f;
    private bool _oomOccurred = false;
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
        Debug.Log("[MemoryPressureTester] Waiting for Unity to initialize...");
        yield return new WaitForSeconds(startDelay);
        StartCoroutine(RunMemoryPressureTests());
    }

    public void RunTests()
    {
        if (!_testStarted)
        {
            StartCoroutine(RunMemoryPressureTests());
        }
    }

    IEnumerator RunMemoryPressureTests()
    {
        if (_testStarted) yield break;
        _testStarted = true;

        Debug.Log("[MemoryPressureTester] ========================================");
        Debug.Log("[MemoryPressureTester] MEMORY PRESSURE TESTS STARTING");
        Debug.Log("[MemoryPressureTester] ========================================");

        // 초기 상태 기록
        LogMemoryStatus("Initial");

        // Phase 1: WASM 힙 메모리 압박
        yield return StartCoroutine(RunWasmMemoryPressure());

        // Phase 2: JavaScript 힙 메모리 압박
        yield return StartCoroutine(RunJSMemoryPressure());

        // Phase 3: 동시 압박 (WASM + JS + Canvas)
        yield return StartCoroutine(RunCombinedMemoryPressure());

        // 결과 전송
        SendResults();

        _testCompleted = true;
    }

    IEnumerator RunWasmMemoryPressure()
    {
        Debug.Log("[MemoryPressureTester] Phase 1: WASM Heap Memory Pressure");
        _currentPhase = "WASM Memory Pressure";

        int totalAllocated = 0;

        for (int i = 0; i < wasmAllocationSteps; i++)
        {
            string stepName = $"WASM_{totalAllocated + allocationSizeMB}MB";
            Debug.Log($"[MemoryPressureTester] Allocating {allocationSizeMB}MB WASM memory (step {i + 1}/{wasmAllocationSteps})");

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                int ptr = AllocateWasmMemory(allocationSizeMB);
                if (ptr == 0)
                {
                    Debug.LogWarning($"[MemoryPressureTester] WASM allocation failed at step {i + 1}");
                    _oomOccurred = true;
                    break;
                }
                totalAllocated += allocationSizeMB;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoryPressureTester] WASM allocation exception: {e.Message}");
                _oomOccurred = true;
                break;
            }
#else
            totalAllocated += allocationSizeMB;
#endif

            yield return StartCoroutine(MeasurePerformance(measurementDuration, stepName, "WASM"));
            LogMemoryStatus($"After WASM step {i + 1}");
        }

        // WASM 메모리 해제
        Debug.Log("[MemoryPressureTester] Freeing WASM memory...");
#if UNITY_WEBGL && !UNITY_EDITOR
        FreeWasmMemory();
#endif
        yield return new WaitForSeconds(1f);
        LogMemoryStatus("After WASM cleanup");
    }

    IEnumerator RunJSMemoryPressure()
    {
        Debug.Log("[MemoryPressureTester] Phase 2: JavaScript Heap Memory Pressure");
        _currentPhase = "JS Memory Pressure";

        int totalAllocated = 0;

        for (int i = 0; i < jsAllocationSteps; i++)
        {
            string stepName = $"JS_{totalAllocated + allocationSizeMB}MB";
            Debug.Log($"[MemoryPressureTester] Allocating {allocationSizeMB}MB JS memory (step {i + 1}/{jsAllocationSteps})");

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                int bufferCount = AllocateJSMemory(allocationSizeMB);
                if (bufferCount < 0)
                {
                    Debug.LogWarning($"[MemoryPressureTester] JS allocation failed at step {i + 1}");
                    _oomOccurred = true;
                    break;
                }
                totalAllocated += allocationSizeMB;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoryPressureTester] JS allocation exception: {e.Message}");
                _oomOccurred = true;
                break;
            }
#else
            totalAllocated += allocationSizeMB;
#endif

            yield return StartCoroutine(MeasurePerformance(measurementDuration, stepName, "JS"));
            LogMemoryStatus($"After JS step {i + 1}");
        }

        // JS 메모리 해제
        Debug.Log("[MemoryPressureTester] Freeing JS memory...");
#if UNITY_WEBGL && !UNITY_EDITOR
        FreeJSMemory();
#endif
        yield return new WaitForSeconds(1f);
        LogMemoryStatus("After JS cleanup");
    }

    IEnumerator RunCombinedMemoryPressure()
    {
        Debug.Log("[MemoryPressureTester] Phase 3: Combined Memory Pressure");
        _currentPhase = "Combined Pressure";

        int combinedWasmMB = allocationSizeMB * 2; // 100MB default
        int combinedJSMB = allocationSizeMB * 2;   // 100MB default

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            Debug.Log($"[MemoryPressureTester] Allocating {combinedWasmMB}MB WASM + {combinedJSMB}MB JS + {canvasCount} canvases ({canvasSize}x{canvasSize})");

            int wasmPtr = AllocateWasmMemory(combinedWasmMB);
            if (wasmPtr == 0)
            {
                Debug.LogWarning("[MemoryPressureTester] Combined WASM allocation failed");
                _oomOccurred = true;
            }

            int jsBuffers = AllocateJSMemory(combinedJSMB);
            if (jsBuffers < 0)
            {
                Debug.LogWarning("[MemoryPressureTester] Combined JS allocation failed");
                _oomOccurred = true;
            }

            int canvases = AllocateCanvasMemory(canvasCount, canvasSize, canvasSize);
            Debug.Log($"[MemoryPressureTester] Created {canvases} canvases");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoryPressureTester] Combined allocation exception: {e.Message}");
            _oomOccurred = true;
        }
#endif

        LogMemoryStatus("Combined pressure active");

        yield return StartCoroutine(MeasurePerformance(combinedMeasurementDuration, "CombinedPressure", "Combined"));

        // 전체 정리
        Debug.Log("[MemoryPressureTester] Freeing all memory...");
#if UNITY_WEBGL && !UNITY_EDITOR
        FreeWasmMemory();
        FreeJSMemory();
        FreeCanvasMemory();
#endif
        yield return new WaitForSeconds(1f);
        LogMemoryStatus("After full cleanup");
    }

    IEnumerator MeasurePerformance(float duration, string stepName, string category)
    {
        Debug.Log($"[MemoryPressureTester] Measuring performance for {stepName} ({duration}s)...");

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

        // 통계 계산
        var result = new MemoryPressureStepResult
        {
            stepName = stepName,
            category = category,
            sampleCount = fpsSamples.Count,
            managedMemoryDeltaMB = (endMemory - startMemory) / (1024f * 1024f)
        };

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
        }

        _stepResults.Add(result);
        Debug.Log($"[MemoryPressureTester] {stepName}: Avg FPS={result.avgFps:F2}, Min={result.minFps:F2}, Max={result.maxFps:F2}");
    }

    void LogMemoryStatus(string context)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            string statusJson = GetMemoryStatus();
            Debug.Log($"[MemoryPressureTester] Memory Status ({context}): {statusJson}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MemoryPressureTester] Failed to get memory status: {e.Message}");
        }
#else
        long managedMemory = GC.GetTotalMemory(false);
        Debug.Log($"[MemoryPressureTester] Memory Status ({context}): Managed={managedMemory / (1024 * 1024)}MB");
#endif
    }

    void SendResults()
    {
        // Combined 압박 결과 찾기
        MemoryPressureStepResult combinedResult = null;
        foreach (var r in _stepResults)
        {
            if (r.stepName == "CombinedPressure")
            {
                combinedResult = r;
                break;
            }
        }

        var report = new MemoryPressureTestReport
        {
            totalSteps = _stepResults.Count,
            oomOccurred = _oomOccurred,
            combinedPressureAvgFps = combinedResult?.avgFps ?? 0,
            combinedPressureMinFps = combinedResult?.minFps ?? 0,
            steps = _stepResults
        };

        string json = JsonUtility.ToJson(report, true);

        Debug.Log("[MemoryPressureTester] ========================================");
        Debug.Log("[MemoryPressureTester] MEMORY PRESSURE TESTS COMPLETED");
        Debug.Log($"[MemoryPressureTester] Total Steps: {report.totalSteps}");
        Debug.Log($"[MemoryPressureTester] OOM Occurred: {report.oomOccurred}");
        Debug.Log($"[MemoryPressureTester] Combined Avg FPS: {report.combinedPressureAvgFps:F2}");
        Debug.Log("[MemoryPressureTester] ========================================");

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SendMemoryPressureResults(json);
            Debug.Log("[MemoryPressureTester] Results sent to JavaScript");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoryPressureTester] Failed to send results: {e.Message}");
        }
#else
        Debug.Log($"[MemoryPressureTester] Results (Editor): {json}");
#endif
    }

    void OnGUI()
    {
        if (!showUI) return;

        int padding = 20;
        int width = 350;
        int height = 280;
        int x = padding;
        int y = Screen.height - height - padding;

        GUI.Box(new Rect(x, y, width, height), "");

        GUILayout.BeginArea(new Rect(x + 10, y + 10, width - 20, height - 20));

        GUILayout.Label("Memory Pressure Test", GUI.skin.box);
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
                GUILayout.Label("⚠️ OOM Detected");
                GUI.color = Color.white;
            }
        }
        else
        {
            GUILayout.Label($"Steps: {_stepResults.Count}");

            GUI.color = _oomOccurred ? Color.red : Color.green;
            GUILayout.Label(_oomOccurred ? "❌ OOM Occurred" : "✅ No OOM");
            GUI.color = Color.white;

            // 결과 요약
            GUILayout.Space(5);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
            foreach (var r in _stepResults)
            {
                GUI.color = r.avgFps < 15 ? Color.red : (r.avgFps < 30 ? Color.yellow : Color.green);
                GUILayout.Label($"{r.stepName}: {r.avgFps:F1} FPS");
            }
            GUI.color = Color.white;
            GUILayout.EndScrollView();
        }

        GUILayout.EndArea();
    }

    [Serializable]
    public class MemoryPressureStepResult
    {
        public string stepName;
        public string category;
        public float avgFps;
        public float minFps;
        public float maxFps;
        public int sampleCount;
        public float managedMemoryDeltaMB;
    }

    [Serializable]
    public class MemoryPressureTestReport
    {
        public int totalSteps;
        public bool oomOccurred;
        public float combinedPressureAvgFps;
        public float combinedPressureMinFps;
        public List<MemoryPressureStepResult> steps;
    }
}
