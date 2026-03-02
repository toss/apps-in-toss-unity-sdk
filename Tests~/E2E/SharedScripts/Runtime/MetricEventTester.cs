using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
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

    // uGUI 참조
    private Text _statusText;

    /// <summary>
    /// uGUI 기반 UI를 생성합니다.
    /// </summary>
    public void SetupUI(Transform parent)
    {
        var section = UIBuilder.CreatePanel(parent, UIBuilder.Theme.SectionBg);
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingSmall;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        section.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(section, "Metric Event Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "AITEventLogger 자동 캡처 이벤트 트리거",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        _statusText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, resultColor, fontStyle: FontStyle.Bold);
        _statusText.gameObject.SetActive(false);

        UIBuilder.CreateButton(section, "Scene Load/Unload", onClick: TriggerSceneTransition);
        UIBuilder.CreateButton(section, "Frame Stall (700ms)", onClick: TriggerFrameStall);
        UIBuilder.CreateButton(section, "GC Collect", onClick: TriggerGCCollect);

        // TimeScale 버튼 행
        var tsRow = UIBuilder.CreateHorizontalLayout(section, UIBuilder.Theme.SpacingSmall);
        tsRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var ts0 = UIBuilder.CreateButton(tsRow, "TimeScale 0x", onClick: () => SetTimeScale(0f));
        UIBuilder.SetLayout(ts0.gameObject, flexibleWidth: 1);
        var ts2 = UIBuilder.CreateButton(tsRow, "TimeScale 2x", onClick: () => SetTimeScale(2f));
        UIBuilder.SetLayout(ts2.gameObject, flexibleWidth: 1);
        var ts1 = UIBuilder.CreateButton(tsRow, "TimeScale 1x", onClick: () => SetTimeScale(1f));
        UIBuilder.SetLayout(ts1.gameObject, flexibleWidth: 1);

        UIBuilder.CreateButton(section, "LogError", onClick: TriggerLogError);
        UIBuilder.CreateButton(section, "Exception", onClick: TriggerException);
        UIBuilder.CreateButton(section, "Screen Resize", onClick: TriggerScreenChange);
        UIBuilder.CreateButton(section, "Focus Blur/Restore", onClick: TriggerFocusChange);
    }

    private void UpdateUI()
    {
        if (_statusText != null)
        {
            if (!string.IsNullOrEmpty(lastAction))
            {
                _statusText.gameObject.SetActive(true);
                _statusText.text = $"{lastAction}: {lastResult}";
                _statusText.color = resultColor;
            }
        }
    }

    private void TriggerSceneTransition()
    {
        lastAction = "Scene Transition";
        Debug.Log("[MetricEventTester] Triggering scene load/unload...");

        try
        {
            Scene tempScene = SceneManager.CreateScene("MetricTest_TempScene");
            Debug.Log($"[MetricEventTester] Created temp scene: {tempScene.name}");

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
        UpdateUI();
    }

    private void TriggerFrameStall()
    {
        lastAction = "Frame Stall";
        Debug.Log("[MetricEventTester] Triggering frame stall (700ms)...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 700)
        {
            // busy wait
        }
        sw.Stop();

        lastResult = $"완료 ({sw.ElapsedMilliseconds}ms 블로킹)";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] Frame stall done ({sw.ElapsedMilliseconds}ms)");
        UpdateUI();
    }

    private void TriggerGCCollect()
    {
        lastAction = "GC Collect";
        Debug.Log("[MetricEventTester] Triggering GC.Collect()...");

        int gen0Before = GC.CollectionCount(0);
        GC.Collect();
        int gen0After = GC.CollectionCount(0);

        lastResult = $"완료 (Gen0: {gen0Before} → {gen0After})";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] GC.Collect done (Gen0: {gen0Before} → {gen0After})");
        UpdateUI();
    }

    private void SetTimeScale(float scale)
    {
        lastAction = $"TimeScale {scale}x";
        float previousScale = Time.timeScale;
        Time.timeScale = scale;

        lastResult = $"완료 ({previousScale} → {scale})";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] TimeScale changed: {previousScale} → {scale}");
        UpdateUI();
    }

    private void TriggerLogError()
    {
        lastAction = "LogError";
        Debug.LogError("[MetricEventTester] Test error for Metric Explorer verification");

        lastResult = "완료 (Debug.LogError 발생)";
        resultColor = Color.green;
        Debug.Log("[MetricEventTester] LogError triggered");
        UpdateUI();
    }

    private void TriggerScreenChange()
    {
        lastAction = "Screen Resize";
        Debug.Log("[MetricEventTester] Triggering screen resize...");

        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        int newWidth = currentWidth - 2;
        int newHeight = currentHeight - 2;
        Screen.SetResolution(newWidth, newHeight, Screen.fullScreen);

        lastResult = $"완료 ({currentWidth}x{currentHeight} → {newWidth}x{newHeight})";
        resultColor = Color.green;
        Debug.Log($"[MetricEventTester] Screen resize: {currentWidth}x{currentHeight} → {newWidth}x{newHeight}");
        UpdateUI();
    }

    private void TriggerFocusChange()
    {
        lastAction = "Focus Change";
        Debug.Log("[MetricEventTester] Triggering focus blur/restore...");

#if UNITY_WEBGL && !UNITY_EDITOR
        E2E_SimulateFocusChange(1000);
        lastResult = "완료 (blur → 1초 후 focus)";
        resultColor = Color.green;
#else
        lastResult = "WebGL 전용 (에디터 미지원)";
        resultColor = Color.yellow;
#endif
        Debug.Log("[MetricEventTester] Focus change triggered");
        UpdateUI();
    }

    private void TriggerException()
    {
        lastAction = "Exception";

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
        UpdateUI();
    }
}
