using System;
using System.Runtime.InteropServices;
using System.Text;
using AppsInToss;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerPrefs 테스터 컴포넌트
/// PlayerPrefs.SetString/GetString 왕복(앱인토스 Storage 영속화 레이어)을 검증합니다.
/// E2E 트리거(TriggerPlayerPrefsSet/TriggerPlayerPrefsGet)와 InteractiveAPITester 수동 패널에서 공용으로 사용됩니다.
///
/// 실기기 샌드박스(QR) 수동 실측용 진단 기능(Storage 크기 프로브, 영속화 status 조회, 백그라운드 전환 로그,
/// 세션 경과시간 + L3 순정 IndexedDB 모드 토글)도 수동 검증 패널에 포함합니다.
/// </summary>
public class PlayerPrefsTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendPlayerPrefsResult(string json);

    // ─── 실기기 진단용 extern ───

    /// <summary>window.AITPlayerPrefs.status() + window.__AIT_PP 진단 정보를 JSON 문자열로 반환합니다.</summary>
    [DllImport("__Internal")]
    private static extern string PP_GetDiagnosticsStatusJson();

    /// <summary>visibilitychange/pagehide 리스너를 등록합니다(중복 호출 안전).</summary>
    [DllImport("__Internal")]
    private static extern void PP_InitVisibilityLog();

    /// <summary>기록된 백그라운드 전환 로그(최대 50개) + persistCount를 JSON 문자열로 반환합니다.</summary>
    [DllImport("__Internal")]
    private static extern string PP_GetVisibilityLogJson();

    /// <summary>sessionStorage['__ait_pp_disabled']를 세팅(1)하거나 제거(0)합니다.</summary>
    [DllImport("__Internal")]
    private static extern void PP_SetL3Disabled(int disabled);

    /// <summary>sessionStorage['__ait_pp_disabled']가 '1'이면 1, 아니면 0을 반환합니다.</summary>
    [DllImport("__Internal")]
    private static extern int PP_GetL3Disabled();

    [DllImport("__Internal")]
    private static extern void PP_Reload();
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

    // ─── 실기기 진단 패널 상태 ───
    private Text _sessionStatusText;
    private Text _l3StatusText;
    private float _lastSessionDisplayUpdate = -1f;
    private bool _sizeProbeRunning = false;

    /// <summary>Storage 크기 프로브 스윕 대상 크기(바이트). 이 값들만으로 어디까지 되는지 확인합니다.</summary>
    private static readonly int[] SizeProbeBytes = { 16 * 1024, 64 * 1024, 128 * 1024, 256 * 1024, 512 * 1024, 1024 * 1024 };

    /// <summary>Storage 크기 프로브에 사용하는 단일 키. 스윕 종료 시 정리됩니다.</summary>
    private const string SizeProbeKey = "AIT_PP_SIZE_PROBE";

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
        UIBuilder.CreateButton(section, "Set (Save 없음)", onClick: OnManualSetNoSaveClick);

        // 결과 표시
        _resultText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _resultText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // ─── 실기기 진단 섹션 ───
        UIBuilder.CreateText(section, "실기기 실측 진단",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);

        _sessionStatusText = UIBuilder.CreateText(section, "세션 경과: 0초",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        UIBuilder.CreateButton(section, "Storage 크기 프로브", onClick: OnSizeProbeClick);
        UIBuilder.CreateButton(section, "영속화 status", onClick: OnDiagnosticsStatusClick);
        UIBuilder.CreateButton(section, "백그라운드 로그", onClick: OnBackgroundLogClick);

        UIBuilder.CreateText(section,
            "Reload: 세션을 유지한 채 페이지만 새로고침합니다. 미니앱을 껐다 다시 열면 새 세션이라 L3 플래그가 초기화되므로, L3 시나리오의 reload는 반드시 이 버튼으로 하세요.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "Reload (세션 유지 새로고침)", onClick: OnReloadClick);

        UIBuilder.CreateText(section,
            "L3: 다음 reload 후 이 탭 세션 동안 앱인토스 Storage 레이어를 끄고 순정 IndexedDB 모드로 동작시킵니다.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "다음 reload부터 레이어 끄기(L3)", onClick: OnEnableL3Click);
        UIBuilder.CreateButton(section, "L3 해제", onClick: OnDisableL3Click);
        _l3StatusText = UIBuilder.CreateText(section, "L3 상태: (미조회)",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_InitVisibilityLog();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] PP_InitVisibilityLog failed: {e.Message}");
        }
#endif
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

    private void OnManualSetNoSaveClick()
    {
        // 의도적으로 Save() 호출 없음: 백그라운드 전환/pagehide 시 레이어가 자동으로
        // flush 하는지(백그라운드 전환 시 PlayerPrefs 자동 flush 여부와 JS 실행 시간 확보 여부) 실기기에서 확인하기 위한 버튼입니다.
        try
        {
            PlayerPrefs.SetString(_manualKey, _manualValue);
            UpdateManualResultUI($"Set(No Save) OK: {_manualKey} = {_manualValue} (백그라운드 전환 후 Get으로 자동 flush 여부 확인)");
        }
        catch (Exception e)
        {
            UpdateManualResultUI($"Set(No Save) FAILED: {e.Message}");
        }
    }

    // ─── 세션 경과시간 표시 ───

    private void Update()
    {
        if (_sessionStatusText == null) return;
        // 1초 주기로만 갱신 (매 프레임 Text 갱신 방지)
        if (_lastSessionDisplayUpdate >= 0f && Time.realtimeSinceStartup - _lastSessionDisplayUpdate < 1f) return;
        _lastSessionDisplayUpdate = Time.realtimeSinceStartup;
        int elapsedSec = Mathf.FloorToInt(Time.realtimeSinceStartup);
        _sessionStatusText.text = $"세션 경과: {elapsedSec}초";
    }

    // ─── Storage 크기 프로브 스윕 ───

    /// <summary>
    /// 크기별 결정적 페이로드 문자열을 생성합니다.
    /// 앞부분에 크기를 각인한 헤더를 넣고 나머지를 반복 패턴('0'~'9')으로 채웁니다.
    /// </summary>
    private static string BuildProbePayload(int sizeBytes)
    {
        string header = $"[AIT_PP_PROBE:{sizeBytes}]";
        var sb = new StringBuilder(sizeBytes);
        sb.Append(header.Length <= sizeBytes ? header : header.Substring(0, sizeBytes));
        int i = 0;
        while (sb.Length < sizeBytes)
        {
            sb.Append((char)('0' + (i % 10)));
            i++;
        }
        return sb.ToString();
    }

    private static string FormatSizeLabel(int bytes)
    {
        return bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)}MB" : $"{bytes / 1024}KB";
    }

    private async void OnSizeProbeClick()
    {
        if (_sizeProbeRunning) return;
        _sizeProbeRunning = true;

        var sb = new StringBuilder();
        sb.AppendLine("Storage 크기 프로브 시작...");
        UpdateManualResultUI(sb.ToString());

        foreach (var size in SizeProbeBytes)
        {
            string label = FormatSizeLabel(size);
            try
            {
                string payload = BuildProbePayload(size);

                var setSw = System.Diagnostics.Stopwatch.StartNew();
                await AIT.StorageSetItem(SizeProbeKey, payload, timeoutMs: 30000);
                setSw.Stop();

                var getSw = System.Diagnostics.Stopwatch.StartNew();
                string readBack = await AIT.StorageGetItem(SizeProbeKey, timeoutMs: 30000);
                getSw.Stop();

                bool ok = readBack != null && readBack.Length == payload.Length && string.Equals(readBack, payload, StringComparison.Ordinal);
                string status = ok ? "OK" : $"MISMATCH(len={(readBack != null ? readBack.Length.ToString() : "null")})";
                sb.AppendLine($"{label}: set {setSw.ElapsedMilliseconds}ms / get {getSw.ElapsedMilliseconds}ms / {status}");
            }
            catch (Exception e)
            {
                // 실패한 크기와 에러만 기록하고 다음 크기로 계속 진행 (어디까지 되는지가 목적)
                sb.AppendLine($"{label}: FAILED - {e.Message}");
            }

            UpdateManualResultUI(sb.ToString());
        }

        try
        {
            await AIT.StorageRemoveItem(SizeProbeKey, timeoutMs: 30000);
        }
        catch (Exception e)
        {
            // 프로브 키 정리 실패는 무시 (다음 스윕이 덮어씀)
            Debug.LogWarning($"[PlayerPrefsTester] Size probe cleanup failed: {e.Message}");
        }

        sb.AppendLine("(프로브 완료, 키 정리 시도됨)");
        UpdateManualResultUI(sb.ToString());
        _sizeProbeRunning = false;
    }

    // ─── 영속화 status 조회 ───

    private void OnDiagnosticsStatusClick()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            UpdateManualResultUI(PP_GetDiagnosticsStatusJson());
        }
        catch (Exception e)
        {
            UpdateManualResultUI($"status 조회 실패: {e.Message}");
        }
#else
        UpdateManualResultUI("(Editor: N/A)");
#endif
    }

    // ─── 백그라운드 전환 로그 ───

    private void OnBackgroundLogClick()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            UpdateManualResultUI(PP_GetVisibilityLogJson());
        }
        catch (Exception e)
        {
            UpdateManualResultUI($"백그라운드 로그 조회 실패: {e.Message}");
        }
#else
        UpdateManualResultUI("(Editor: N/A)");
#endif
    }

    // ─── Reload: 세션 유지 새로고침(location.reload) ───

    private void OnReloadClick()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_Reload();
        }
        catch (Exception e)
        {
            UpdateManualResultUI($"Reload 실패: {e.Message}");
        }
#else
        UpdateManualResultUI("(Editor: N/A)");
#endif
    }

    // ─── L3: 세션 킬 토글(sessionStorage['__ait_pp_disabled']) ───

    private void OnEnableL3Click()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_SetL3Disabled(1);
            UpdateL3StatusUI();
            UpdateManualResultUI("L3 세팅됨: reload 후 이 탭 세션 동안 순정 IndexedDB 모드로 동작합니다.");
        }
        catch (Exception e)
        {
            UpdateManualResultUI($"L3 세팅 실패: {e.Message}");
        }
#else
        UpdateManualResultUI("(Editor: N/A)");
#endif
    }

    private void OnDisableL3Click()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_SetL3Disabled(0);
            UpdateL3StatusUI();
            UpdateManualResultUI("L3 해제됨: reload 후 앱인토스 Storage 영속화 레이어가 다시 켜집니다.");
        }
        catch (Exception e)
        {
            UpdateManualResultUI($"L3 해제 실패: {e.Message}");
        }
#else
        UpdateManualResultUI("(Editor: N/A)");
#endif
    }

    private void UpdateL3StatusUI()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_l3StatusText == null) return;
        try
        {
            int disabled = PP_GetL3Disabled();
            _l3StatusText.text = $"L3 상태: {(disabled == 1 ? "OFF 예약됨(순정 IndexedDB)" : "정상(레이어 켜짐)")}";
        }
        catch (Exception e)
        {
            _l3StatusText.text = $"L3 상태 조회 실패: {e.Message}";
        }
#endif
    }
}
