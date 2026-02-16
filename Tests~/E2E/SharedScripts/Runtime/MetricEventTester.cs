using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Metric Explorer 검증용 이벤트 트리거 컴포넌트
/// AITEventLogger가 자동 캡처하는 이벤트를 수동으로 발생시켜 테스트
/// InteractiveAPITester의 섹션으로 표시됨
/// </summary>
public class MetricEventTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void E2E_SimulateFocusChange(int delayMs);
#endif

    // UI 상태
    private string lastAction = "";
    private string lastResult = "";
    private Color resultColor = Color.white;

    /// <summary>
    /// Metric Event Tester UI를 렌더링합니다.
    /// </summary>
    public void DrawUI(GUIStyle boxStyle, GUIStyle groupHeaderStyle, GUIStyle labelStyle, GUIStyle buttonStyle)
    {
        GUILayout.BeginVertical(boxStyle);

        // 섹션 헤더
        GUILayout.Label("\ud83d\udcca Metric Event Tester", groupHeaderStyle);
        GUILayout.Label("AITEventLogger 자동 캡처 이벤트 트리거", labelStyle);

        GUILayout.Space(10);

        // 상태 표시
        if (!string.IsNullOrEmpty(lastAction))
        {
            GUIStyle statusStyle = new GUIStyle(labelStyle);
            statusStyle.fontStyle = FontStyle.Bold;
            statusStyle.normal.textColor = resultColor;
            GUILayout.Label($"{lastAction}: {lastResult}", statusStyle);
            GUILayout.Space(5);
        }

        // Scene Transition 버튼
        if (GUILayout.Button("\ud83c\udfac Scene Load/Unload", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerSceneTransition();
        }

        GUILayout.Space(3);

        // Frame Stall 버튼
        if (GUILayout.Button("\ud83d\udc0c Frame Stall (700ms)", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerFrameStall();
        }

        GUILayout.Space(3);

        // GC Collect 버튼
        if (GUILayout.Button("\u267b\ufe0f GC Collect", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerGCCollect();
        }

        GUILayout.Space(3);

        // TimeScale 버튼들
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("\u23f1 TimeScale 0x", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            SetTimeScale(0f);
        }
        if (GUILayout.Button("\u23f1 TimeScale 2x", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            SetTimeScale(2f);
        }
        if (GUILayout.Button("\u23f1 TimeScale 1x", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            SetTimeScale(1f);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(3);

        // LogError 버튼
        if (GUILayout.Button("\u26a0\ufe0f LogError", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerLogError();
        }

        GUILayout.Space(3);

        // Exception 버튼
        if (GUILayout.Button("\ud83d\udca5 Exception", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerException();
        }

        GUILayout.Space(3);

        // Screen Change 버튼
        if (GUILayout.Button("\ud83d\udcf1 Screen Resize", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerScreenChange();
        }

        GUILayout.Space(3);

        // Focus Change 버튼
        if (GUILayout.Button("\ud83d\udd0d Focus Blur/Restore", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            TriggerFocusChange();
        }

        GUILayout.EndVertical();
    }

    private void TriggerSceneTransition()
    {
        lastAction = "\ud83c\udfac Scene Transition";
        Debug.Log("[MetricEventTester] Triggering scene load/unload...");

        try
        {
            // 런타임에 빈 씬을 Additive로 생성 (추가 에셋 불필요)
            Scene tempScene = SceneManager.CreateScene("MetricTest_TempScene");
            Debug.Log($"[MetricEventTester] Created temp scene: {tempScene.name}");

            // 즉시 언로드하여 scene_unloaded 이벤트도 발생
            SceneManager.UnloadSceneAsync(tempScene);

            lastResult = "완료 (생성 + 언로드)";
            resultColor = Color.green;
            Debug.Log("[MetricEventTester] Scene transition done");
        }
        catch (Exception ex)
        {
            lastResult = $"실패: {ex.Message}";
            resultColor = Color.red;
            Debug.LogWarning($"[MetricEventTester] Scene transition failed: {ex.Message}");
        }
    }

    private void TriggerFrameStall()
    {
        lastAction = "\ud83d\udc0c Frame Stall";
        Debug.Log("[MetricEventTester] Triggering frame stall (700ms)...");

        // CPU-bound 루프로 메인 스레드 블로킹 (Thread.Sleep보다 확실한 프레임 스톨)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 700)
        {
            // busy wait
        }
        sw.Stop();

        lastResult = $"완료 ({sw.ElapsedMilliseconds}ms 블로킹)";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] Frame stall done ({sw.ElapsedMilliseconds}ms)");
    }

    private void TriggerGCCollect()
    {
        lastAction = "\u267b\ufe0f GC Collect";
        Debug.Log("[MetricEventTester] Triggering GC.Collect()...");

        int gen0Before = GC.CollectionCount(0);
        GC.Collect();
        int gen0After = GC.CollectionCount(0);

        lastResult = $"완료 (Gen0: {gen0Before} → {gen0After})";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] GC.Collect done (Gen0: {gen0Before} → {gen0After})");
    }

    private void SetTimeScale(float scale)
    {
        lastAction = $"\u23f1 TimeScale {scale}x";
        float previousScale = Time.timeScale;
        Time.timeScale = scale;

        lastResult = $"완료 ({previousScale} → {scale})";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] TimeScale changed: {previousScale} → {scale}");
    }

    private void TriggerLogError()
    {
        lastAction = "\u26a0\ufe0f LogError";
        Debug.LogError("[MetricEventTester] Test error for Metric Explorer verification");

        lastResult = "완료 (Debug.LogError 발생)";
        resultColor = Color.green;
        Debug.Log("[MetricEventTester] LogError triggered");
    }

    private void TriggerScreenChange()
    {
        lastAction = "\ud83d\udcf1 Screen Resize";
        Debug.Log("[MetricEventTester] Triggering screen resize...");

        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        // 해상도를 살짝 변경하여 screen_change 이벤트 발생
        int newWidth = currentWidth - 2;
        int newHeight = currentHeight - 2;
        Screen.SetResolution(newWidth, newHeight, Screen.fullScreen);

        lastResult = $"완료 ({currentWidth}x{currentHeight} → {newWidth}x{newHeight})";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] Screen resize: {currentWidth}x{currentHeight} → {newWidth}x{newHeight}");
    }

    private void TriggerFocusChange()
    {
        lastAction = "\ud83d\udd0d Focus Change";
        Debug.Log("[MetricEventTester] Triggering focus blur/restore...");

#if UNITY_WEBGL && !UNITY_EDITOR
        // JS blur → 1초 후 focus 복원
        E2E_SimulateFocusChange(1000);
        lastResult = "완료 (blur → 1초 후 focus)";
        resultColor = Color.green;
#else
        lastResult = "WebGL 전용 (에디터 미지원)";
        resultColor = Color.yellow;
#endif
        Debug.Log("[MetricEventTester] Focus change triggered");
    }

    private void TriggerException()
    {
        lastAction = "\ud83d\udca5 Exception";

        try
        {
            throw new Exception("[MetricEventTester] Test exception for Metric Explorer verification");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        lastResult = "완료 (Exception → LogException)";
        resultColor = Color.green;
        Debug.Log("[MetricEventTester] Exception triggered and logged");
    }
}
