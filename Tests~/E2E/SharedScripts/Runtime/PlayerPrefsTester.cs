using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerPrefs 테스터 컴포넌트
/// PlayerPrefs.SetString/GetString 왕복(앱인토스 Storage 영속화 레이어)을 검증합니다.
/// E2E 트리거(TriggerPlayerPrefsSet/TriggerPlayerPrefsGet)와 InteractiveAPITester 수동 패널에서 공용으로 사용됩니다.
/// </summary>
public class PlayerPrefsTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendPlayerPrefsResult(string json);
#endif

    [Serializable]
    private class PlayerPrefsSetRequest
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class PlayerPrefsResult
    {
        public string op;
        public string key;
        public string value;
        public bool success;
    }

    // ─── 수동 검증 패널 UI 참조 ───
    private InputField _keyInput;
    private InputField _valueInput;
    private Text _resultText;
    private string _manualKey = "test-key";
    private string _manualValue = "test-value";

    /// <summary>
    /// JavaScript에서 호출: window.TriggerPlayerPrefsSet(jsonStr)
    /// → SendMessage('BenchmarkManager', 'TriggerPlayerPrefsSet', json) → E2ETestTrigger → 여기로 위임
    /// json: {"key":"...","value":"..."}
    /// PlayerPrefs.SetString + Save() 후 결과를 { op:'set', key, value, success } JSON으로 송출합니다.
    /// </summary>
    public void SetAndSave(string json)
    {
        string key = null;
        string value = null;
        bool success = true;

        try
        {
            var req = JsonUtility.FromJson<PlayerPrefsSetRequest>(json);
            key = req.key;
            value = req.value ?? "";

            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();

            Debug.Log($"[PlayerPrefsTester] SetAndSave: key={key}, value={value}");
        }
        catch (Exception e)
        {
            success = false;
            Debug.LogError($"[PlayerPrefsTester] SetAndSave failed: {e.Message}");
        }

        UpdateManualResultUI(success ? $"Set OK: {key} = {value}" : "Set FAILED");
        SendResult("set", key, value, success);
    }

    /// <summary>
    /// JavaScript에서 호출: window.TriggerPlayerPrefsGet(key)
    /// → SendMessage('BenchmarkManager', 'TriggerPlayerPrefsGet', key) → E2ETestTrigger → 여기로 위임
    /// PlayerPrefs.GetString(key, "") 결과를 { op:'get', key, value, success } JSON으로 송출합니다.
    /// </summary>
    public void GetAndReport(string key)
    {
        string value = "";
        bool success = true;

        try
        {
            value = PlayerPrefs.GetString(key, "");
            Debug.Log($"[PlayerPrefsTester] GetAndReport: key={key}, value={value}");
        }
        catch (Exception e)
        {
            success = false;
            Debug.LogError($"[PlayerPrefsTester] GetAndReport failed: {e.Message}");
        }

        UpdateManualResultUI(success ? $"Get OK: {key} = {value}" : "Get FAILED");
        SendResult("get", key, value, success);
    }

    private void SendResult(string op, string key, string value, bool success)
    {
        var result = new PlayerPrefsResult
        {
            op = op,
            key = key ?? "",
            value = value ?? "",
            success = success
        };
        string json = JsonUtility.ToJson(result);

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SendPlayerPrefsResult(json);
            Debug.Log("[PlayerPrefsTester] Result sent to JavaScript");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] Failed to send result: {e.Message}");
        }
#else
        Debug.Log($"[PlayerPrefsTester] Result (Editor): {json}");
#endif
    }

    // ─── 수동 검증 패널 (InteractiveAPITester, 실기기 샌드박스용) ───

    /// <summary>
    /// uGUI 기반 수동 검증 패널을 생성합니다.
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

        UIBuilder.CreateText(section, "PlayerPrefs Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "앱인토스 Storage 영속화 레이어를 수동으로 검증합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // Key 입력
        var keyRow = UIBuilder.CreateHorizontalLayout(section, 8);
        keyRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var keyLabel = UIBuilder.CreateText(keyRow, "Key:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(keyLabel.gameObject, minWidth: 60, preferredWidth: 60);
        _keyInput = UIBuilder.CreateInputField(keyRow, "test-key",
            onValueChanged: (v) => _manualKey = v);
        _keyInput.text = _manualKey;
        UIBuilder.SetLayout(_keyInput.gameObject, flexibleWidth: 1);

        // Value 입력
        var valueRow = UIBuilder.CreateHorizontalLayout(section, 8);
        valueRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var valueLabel = UIBuilder.CreateText(valueRow, "Value:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(valueLabel.gameObject, minWidth: 60, preferredWidth: 60);
        _valueInput = UIBuilder.CreateInputField(valueRow, "test-value",
            onValueChanged: (v) => _manualValue = v);
        _valueInput.text = _manualValue;
        UIBuilder.SetLayout(_valueInput.gameObject, flexibleWidth: 1);

        // 액션 버튼
        UIBuilder.CreateButton(section, "Set + Save", onClick: OnManualSetClick);
        UIBuilder.CreateButton(section, "Get", onClick: OnManualGetClick);

        // 결과 표시
        _resultText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void OnManualSetClick()
    {
        var req = new PlayerPrefsSetRequest { key = _manualKey, value = _manualValue };
        SetAndSave(JsonUtility.ToJson(req));
    }

    private void OnManualGetClick()
    {
        GetAndReport(_manualKey);
    }

    private void UpdateManualResultUI(string text)
    {
        if (_resultText != null) _resultText.text = text;
    }
}
