using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 성능 벤치마크 매니저
/// FPS, 메모리 사용량, 렌더링 통계 등을 측정하고 표시합니다.
/// </summary>
public class PerformanceBenchmark : MonoBehaviour
{
    [Header("UI Settings")]
    public Text statsText;
    public bool showStats = true;

    [Header("Performance Settings")]
    public float updateInterval = 0.5f;

    private float fps;
    private float deltaTime;
    private float accum = 0.0f;
    private int frames = 0;
    private float timeleft;

    private StringBuilder statsBuilder = new StringBuilder();

    void Start()
    {
        timeleft = updateInterval;

        if (statsText == null)
        {
            CreateStatsUI();
        }
    }

    void Update()
    {
        // FPS 계산
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        timeleft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        if (timeleft <= 0.0)
        {
            fps = accum / frames;
            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;

            UpdateStatsDisplay();
        }
    }

    void UpdateStatsDisplay()
    {
        if (!showStats || statsText == null) return;

        statsBuilder.Clear();

        // FPS
        statsBuilder.AppendLine($"<b>Performance Stats</b>");
        statsBuilder.AppendLine($"FPS: {fps:F1}");
        statsBuilder.AppendLine($"Frame Time: {deltaTime * 1000.0f:F2} ms");

        // 메모리 사용량
        long totalMemory = System.GC.GetTotalMemory(false) / (1024 * 1024);
        statsBuilder.AppendLine($"Managed Memory: {totalMemory} MB");

        // Unity 통계
        statsBuilder.AppendLine($"DrawCalls: {GetDrawCallCount()}");
        statsBuilder.AppendLine($"Batches: {GetBatchCount()}");
        statsBuilder.AppendLine($"Triangles: {GetTriangleCount()}");
        statsBuilder.AppendLine($"Vertices: {GetVertexCount()}");

        statsText.text = statsBuilder.ToString();
    }

    void CreateStatsUI()
    {
        // Canvas 생성
        GameObject canvasObj = new GameObject("StatsCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Text 오브젝트 생성
        GameObject textObj = new GameObject("StatsText");
        textObj.transform.SetParent(canvasObj.transform);

        statsText = textObj.AddComponent<Text>();
        statsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statsText.fontSize = 14;
        statsText.color = Color.white;
        statsText.alignment = TextAnchor.UpperLeft;

        // RectTransform 설정
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(10, -10);
        rectTransform.sizeDelta = new Vector2(300, 200);

        // 배경 추가
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(textObj.transform);
        bgObj.transform.SetAsFirstSibling();

        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
    }

    // Unity Stats API 호출 (WebGL에서는 제한적)
    private int GetDrawCallCount()
    {
#if UNITY_EDITOR
        return UnityEditor.UnityStats.drawCalls;
#else
        return 0;
#endif
    }

    private int GetBatchCount()
    {
#if UNITY_EDITOR
        return UnityEditor.UnityStats.batches;
#else
        return 0;
#endif
    }

    private int GetTriangleCount()
    {
#if UNITY_EDITOR
        return UnityEditor.UnityStats.triangles;
#else
        return 0;
#endif
    }

    private int GetVertexCount()
    {
#if UNITY_EDITOR
        return UnityEditor.UnityStats.vertices;
#else
        return 0;
#endif
    }

    // 벤치마크 테스트 실행
    public void RunBenchmark()
    {
        StartCoroutine(BenchmarkRoutine());
    }

    private IEnumerator BenchmarkRoutine()
    {
        Debug.Log("Starting Performance Benchmark...");

        float startTime = Time.realtimeSinceStartup;
        List<float> fpsSamples = new List<float>();

        // 10초 동안 FPS 샘플링
        while (Time.realtimeSinceStartup - startTime < 10f)
        {
            fpsSamples.Add(1.0f / Time.deltaTime);
            yield return new WaitForSeconds(0.1f);
        }

        // 통계 계산
        float avgFps = 0;
        float minFps = float.MaxValue;
        float maxFps = float.MinValue;

        foreach (float sample in fpsSamples)
        {
            avgFps += sample;
            if (sample < minFps) minFps = sample;
            if (sample > maxFps) maxFps = sample;
        }
        avgFps /= fpsSamples.Count;

        Debug.Log($"Benchmark Complete - Avg FPS: {avgFps:F2}, Min: {minFps:F2}, Max: {maxFps:F2}");
    }
}
