using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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

    // ─── 실기기 자동 진단 하니스(버튼 하나로 진행) extern ───

    /// <summary>localStorage['__AIT_PP_PROBE__']에 저장된 진단 저널 JSON 문자열을 반환합니다. 없으면 빈 문자열.</summary>
    [DllImport("__Internal")]
    private static extern string PP_ProbeJournalLoad();

    /// <summary>진단 저널 JSON 문자열을 localStorage['__AIT_PP_PROBE__']에 저장합니다(실패해도 throw하지 않음).</summary>
    [DllImport("__Internal")]
    private static extern void PP_ProbeJournalSave(string str);

    /// <summary>localStorage['__AIT_PP_PROBE__']를 삭제합니다.</summary>
    [DllImport("__Internal")]
    private static extern void PP_ProbeJournalClear();

    /// <summary>최종 진단 저널 JSON 문자열을 콘솔 로그 + 클립보드 복사 시도 + 화면 오버레이로 노출합니다.</summary>
    [DllImport("__Internal")]
    private static extern void PP_EmitResult(string str);
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

    // ─── 실기기 자동 진단 하니스 저널 모델 ───
    // JsonUtility는 로드(역직렬화) 편의를 위해서만 사용합니다.
    // 저장(직렬화)은 중첩 문자열 이스케이프가 지저분해지는 것을 피하기 위해 BuildJournalJson()에서
    // StringBuilder + JsonEscape로 직접 조립합니다.

    [Serializable]
    private class ProbeJournalEntry
    {
        public string step;
        public string label;
        public long ts;
        public int sessionMs;
        public string data;
    }

    [Serializable]
    private class ProbeJournal
    {
        public int v = 1;
        public string step = "IDLE";
        public string unity = "";
        public long startedAt;
        public int reloadCount;
        public string abort = "";
        public List<ProbeJournalEntry> entries = new List<ProbeJournalEntry>();
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

    // ─── 실기기 자동 진단 하니스(버튼 하나로 진행) 상태 ───
    private Button _probeButton;
    private Text _probeGuideText;
    private Text _probeProgressText;
    private bool _probeChainRunning = false;
    private ProbeJournal _journal = new ProbeJournal { step = "IDLE", entries = new List<ProbeJournalEntry>() };

    /// <summary>reload를 이 횟수 넘게 반복하면 버그로 간주하고 DONE으로 강제 종료합니다(무한 reload 방지).</summary>
    private const int MaxReloadCount = 8;

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

        // ─── 자동 진단 하니스(버튼 하나로 진행, S0~S13 상태 기계) ───
        _probeButton = UIBuilder.CreateButton(section, "🔬 자동 실측 시작",
            onClick: OnProbeButtonClick, style: UIBuilder.ButtonStyle.Accent);
        _probeGuideText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);
        _probeGuideText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _probeProgressText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextAccent);
        _probeProgressText.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIBuilder.CreateButton(section, "진단 초기화", onClick: OnProbeResetClick, style: UIBuilder.ButtonStyle.Danger);

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

        RestoreProbeState();
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

    /// <summary>
    /// Storage 크기 프로브 스윕을 실행하고 최종 결과 문자열을 반환합니다.
    /// onProgress가 있으면 각 크기 처리 직후 중간 결과 문자열로 호출됩니다(수동 버튼의 실시간 UI 갱신용).
    /// 자동 진단 하니스(S6)에서도 이 메서드를 그대로 재사용합니다.
    /// </summary>
    private async Task<string> RunSizeProbeAsync(Action<string> onProgress = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Storage 크기 프로브 시작...");
        onProgress?.Invoke(sb.ToString());

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

            onProgress?.Invoke(sb.ToString());
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
        string result = sb.ToString();
        onProgress?.Invoke(result);
        return result;
    }

    private async void OnSizeProbeClick()
    {
        if (_sizeProbeRunning) return;
        _sizeProbeRunning = true;
        await RunSizeProbeAsync(UpdateManualResultUI);
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

    // =====================================================
    // 실기기 자동 진단 하니스 (버튼 하나로 S0~S13 순차 진행)
    // =====================================================
    //
    // 상태표(요약):
    //   IDLE --(클릭)--> S0,S1 실행 --> S2 대기
    //   S2   --(클릭)--> S2 실행     --> S3 대기
    //   S3   --(클릭)--> S3,S4 실행  --> S5 대기
    //   S5   --(클릭)--> S5,S6,S7 실행 --> reload --> S8 (자동 재개)
    //   S8   --(자동)--> S8,S9 실행  --> reload --> S10 (자동 재개)
    //   S10  --(자동)--> S10,S11 실행 --> reload --> S12 (자동 재개)
    //   S12  --(자동)--> S12,S13 실행 --> DONE
    //   DONE --(클릭)--> EmitResult 재호출
    //
    // 저널의 "step"에는 위 대기/재개 지점(IDLE/S2/S3/S5/S8/S10/S12/DONE)만 영속화됩니다.
    // S0/S1/S4/S6/S7/S9/S11/S13은 체인 중간에 실행되는 동작이며 entries의 step 태그로만 남습니다.

    // ─── 버튼 클릭 / 재개 진입점 ───

    private void OnProbeButtonClick()
    {
        if (_probeChainRunning) return;

        switch (_journal.step)
        {
            case "IDLE":
                InitJournal();
                StartProbeChain(Chain_Idle());
                break;
            case "S2":
                StartProbeChain(Chain_S2());
                break;
            case "S3":
                StartProbeChain(Chain_S3());
                break;
            case "S5":
                StartProbeChain(Chain_S5());
                break;
            case "DONE":
                EmitResult();
                break;
            default:
                // S8/S10/S12는 reload 직후 자동 재개 지점이라 버튼이 비활성화되어 있어야 함(방어적 무시)
                break;
        }
    }

    private void OnProbeResetClick()
    {
        StopAllCoroutines();
        _probeChainRunning = false;
        _journal = new ProbeJournal { step = "IDLE", entries = new List<ProbeJournalEntry>() };
        ClearJournalRaw();
        if (_probeProgressText != null) _probeProgressText.text = "";
        RefreshProbeUI();
    }

    /// <summary>
    /// Start()(uGUI가 아직 없을 수 있어 SetupUI 직후) 한 번 호출되어 localStorage에 저장된 저널을 복원합니다.
    /// step이 S8/S10/S12(리로드 직후 자동 재개 지점)이면 사람 개입 없이 즉시 체인을 이어서 실행합니다.
    /// </summary>
    private void RestoreProbeState()
    {
        string loaded = LoadJournalRaw();

        if (string.IsNullOrEmpty(loaded))
        {
            _journal = new ProbeJournal { step = "IDLE", entries = new List<ProbeJournalEntry>() };
            RefreshProbeUI();
            return;
        }

        try
        {
            var parsed = JsonUtility.FromJson<ProbeJournal>(loaded);
            _journal = parsed ?? new ProbeJournal { step = "IDLE", entries = new List<ProbeJournalEntry>() };
            if (_journal.entries == null) _journal.entries = new List<ProbeJournalEntry>();
            if (string.IsNullOrEmpty(_journal.step)) _journal.step = "IDLE";
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] 진단 저널 파싱 실패, 초기화합니다: {e.Message}");
            _journal = new ProbeJournal { step = "IDLE", entries = new List<ProbeJournalEntry>() };
        }

        RefreshProbeUI();

        switch (_journal.step)
        {
            case "S8":
                StartProbeChain(Chain_S8());
                break;
            case "S10":
                StartProbeChain(Chain_S10());
                break;
            case "S12":
                StartProbeChain(Chain_S12());
                break;
        }
    }

    private void InitJournal()
    {
        _journal = new ProbeJournal
        {
            step = "IDLE",
            unity = Application.unityVersion,
            startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            reloadCount = 0,
            entries = new List<ProbeJournalEntry>()
        };
    }

    private void StartProbeChain(IEnumerator chain)
    {
        if (_probeChainRunning) return;
        _probeChainRunning = true;

        if (_probeButton != null)
        {
            _probeButton.interactable = false;
            var label = _probeButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "진행 중...";
        }
        if (_probeGuideText != null)
            _probeGuideText.text = "자동 진단이 진행되고 있습니다. 화면을 켜둔 채로 기다려주세요.";

        StartCoroutine(RunChainAndUnlock(chain));
    }

    private IEnumerator RunChainAndUnlock(IEnumerator chain)
    {
        yield return StartCoroutine(chain);
        _probeChainRunning = false;
        RefreshProbeUI();
    }

    /// <summary>현재 저널 step에 맞춰 버튼 라벨/활성화 여부/안내 텍스트를 갱신합니다.</summary>
    private void RefreshProbeUI()
    {
        if (_probeButton == null) return;

        var label = _probeButton.GetComponentInChildren<Text>();
        string btnText;
        bool interactable = true;
        string guide;

        switch (_journal.step)
        {
            case "IDLE":
                btnText = "🔬 자동 실측 시작";
                guide = "버튼을 누르면 자동 진단이 시작됩니다. 중간에 안내에 따라 앱을 백그라운드로 보내거나 완전히 종료했다가 다시 열어야 하는 지점이 몇 번 있습니다.";
                break;
            case "S2":
                btnText = "계속 (백그라운드 다녀온 뒤)";
                guide = "홈 버튼으로 앱을 나갔다가 5초 뒤 돌아와서 [계속]을 누르세요.";
                break;
            case "S3":
                btnText = "계속 (앱 재실행 뒤)";
                guide = "이제 앱을 완전히 종료(앱 스위처에서 위로 스와이프)했다가 다시 열고 [계속]을 누르세요.";
                break;
            case "S5":
                btnText = "계속 (앱 재실행 뒤)";
                guide = "다시 앱을 완전히 종료했다가 열고 [계속]을 누르세요.";
                break;
            case "S8":
            case "S10":
            case "S12":
                btnText = "진행 중... (자동)";
                interactable = false;
                guide = "재실행 후 자동으로 진단이 이어지는 중입니다. 화면을 켜둔 채로 기다려주세요.";
                break;
            case "DONE":
                btnText = string.IsNullOrEmpty(_journal.abort)
                    ? "결과 다시 표시 / 복사"
                    : $"결과 다시 표시 / 복사 (중단됨: {_journal.abort})";
                guide = "진단이 끝났습니다. 결과 JSON을 클립보드 복사 또는 화면 오버레이에서 가져가세요.";
                break;
            default:
                btnText = "진행 중...";
                interactable = false;
                guide = "알 수 없는 상태입니다. [진단 초기화]를 눌러 다시 시작하세요.";
                break;
        }

        if (label != null) label.text = btnText;
        _probeButton.interactable = interactable;
        if (_probeGuideText != null) _probeGuideText.text = guide;
    }

    // ─── 저널 entries 기록 헬퍼 ───

    private void AddEntry(string step, string label, string data)
    {
        if (_journal.entries == null) _journal.entries = new List<ProbeJournalEntry>();
        _journal.entries.Add(new ProbeJournalEntry
        {
            step = step,
            label = label,
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            sessionMs = Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f),
            data = data ?? ""
        });
    }

    private string GetPPValueForLog()
    {
        string v = PlayerPrefs.GetString(_manualKey, "");
        return string.IsNullOrEmpty(v) ? "<빈값>" : v;
    }

    private string GetStatusJson()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return PP_GetDiagnosticsStatusJson();
        }
        catch (Exception e)
        {
            return $"status 조회 실패: {e.Message}";
        }
#else
        return "(Editor: N/A)";
#endif
    }

    private string GetVisibilityLogJsonSafe()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return PP_GetVisibilityLogJson();
        }
        catch (Exception e)
        {
            return $"백그라운드 로그 조회 실패: {e.Message}";
        }
#else
        return "(Editor: N/A)";
#endif
    }

    private void SetL3DisabledSafe(int disabled)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_SetL3Disabled(disabled);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] L3 설정 실패: {e.Message}");
        }
#endif
    }

    // ─── 저널 직렬화/영속화 ───

    /// <summary>JSON 문자열 값으로 안전하게 넣을 수 있도록 이스케이프합니다(따옴표/역슬래시/개행/제어문자).</summary>
    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        var sb = new StringBuilder(s.Length + 16);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 현재 _journal 상태를 JSON 문자열로 직접 조립합니다.
    /// entries[].data(status/vislog JSON 등)는 중첩 파싱하지 않고 JsonEscape로만 감싼 문자열로 들어갑니다.
    /// </summary>
    private string BuildJournalJson()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"v\":1,");
        sb.Append("\"step\":\"").Append(JsonEscape(_journal.step)).Append("\",");
        sb.Append("\"unity\":\"").Append(JsonEscape(_journal.unity)).Append("\",");
        sb.Append("\"startedAt\":").Append(_journal.startedAt).Append(',');
        sb.Append("\"reloadCount\":").Append(_journal.reloadCount);

        if (!string.IsNullOrEmpty(_journal.abort))
        {
            sb.Append(",\"abort\":\"").Append(JsonEscape(_journal.abort)).Append("\"");
        }

        sb.Append(",\"entries\":[");
        var entries = _journal.entries ?? new List<ProbeJournalEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var e = entries[i];
            sb.Append('{');
            sb.Append("\"step\":\"").Append(JsonEscape(e.step)).Append("\",");
            sb.Append("\"label\":\"").Append(JsonEscape(e.label)).Append("\",");
            sb.Append("\"ts\":").Append(e.ts).Append(',');
            sb.Append("\"sessionMs\":").Append(e.sessionMs).Append(',');
            sb.Append("\"data\":\"").Append(JsonEscape(e.data)).Append("\"");
            sb.Append('}');
        }
        sb.Append(']');
        sb.Append('}');
        return sb.ToString();
    }

    private string LoadJournalRaw()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return PP_ProbeJournalLoad();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] 진단 저널 로드 실패: {e.Message}");
            return "";
        }
#else
        return "";
#endif
    }

    private void SaveJournal()
    {
        string json = BuildJournalJson();
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_ProbeJournalSave(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] 진단 저널 저장 실패: {e.Message}");
        }
#else
        Debug.Log($"[PlayerPrefsTester] (Editor) 진단 저널 저장(no-op): step={_journal.step}");
#endif
    }

    private void ClearJournalRaw()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_ProbeJournalClear();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] 진단 저널 삭제 실패: {e.Message}");
        }
#endif
    }

    private void EmitResult()
    {
        string json = BuildJournalJson();
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_EmitResult(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] 결과 emit 실패: {e.Message}");
        }
#else
        Debug.Log($"[PlayerPrefsTester] (Editor) EmitResult: {json}");
#endif
    }

    /// <summary>
    /// reloadCount를 증가시키고 PP_Reload()를 호출합니다.
    /// 이미 MaxReloadCount에 도달했으면 reload하지 않고 DONE으로 강제 종료 + abort 사유를 남깁니다.
    /// </summary>
    private void DoReloadOrAbort()
    {
        if (_journal.reloadCount >= MaxReloadCount)
        {
            _journal.abort = "reload-limit";
            _journal.step = "DONE";
            SaveJournal();
            EmitResult();
            return;
        }

        _journal.reloadCount++;
        SaveJournal();
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PP_Reload();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsTester] Reload 실패: {e.Message}");
        }
#else
        Debug.Log("[PlayerPrefsTester] (Editor) Reload는 no-op이라 자동 진단이 여기서 멈춥니다(실기기 전용 흐름).");
#endif
    }

    // ─── 각 단계별 실행 동작 (Do*Action) ───

    private void DoS0Action()
    {
        AddEntry("S0", "baseline get", GetPPValueForLog());
        AddEntry("S0", "baseline status", GetStatusJson());
    }

    private void DoS1Action()
    {
        // 의도적으로 Save() 미호출: 백그라운드 전환 시 레이어가 자동 flush 하는지 보는 지점
        PlayerPrefs.SetString(_manualKey, _manualValue);
        AddEntry("S1", "set (no save)", $"{_manualKey}={_manualValue}");
        AddEntry("S1", "status", GetStatusJson());
    }

    private void DoS2Action()
    {
        AddEntry("S2", "vislog", GetVisibilityLogJsonSafe());
        AddEntry("S2", "status", GetStatusJson());
    }

    private void DoS3Action()
    {
        AddEntry("S3", "get (V7/V8 판정)", GetPPValueForLog());
        AddEntry("S3", "status", GetStatusJson());
    }

    private void DoS4Action()
    {
        PlayerPrefs.SetString(_manualKey, _manualValue);
        PlayerPrefs.Save();
        AddEntry("S4", "set+save", $"{_manualKey}={_manualValue}");
    }

    private void DoS5Action()
    {
        AddEntry("S5", "get (레이어 end-to-end 판정)", GetPPValueForLog());
        AddEntry("S5", "status", GetStatusJson());
    }

    private void DoS8Action()
    {
        AddEntry("S8", "get (노화 세션 ait 판정)", GetPPValueForLog());
        AddEntry("S8", "status (collectFallbackCount 확인)", GetStatusJson());
    }

    private void DoS10Action()
    {
        AddEntry("S10", "status (mode=vanilla 확인)", GetStatusJson());
        AddEntry("S10", "get (클린 체크, 빈 값이어야 함)", GetPPValueForLog());
    }

    private void DoS12Action()
    {
        AddEntry("S12", "get (2021.3 유실 재현 판정)", GetPPValueForLog());
        AddEntry("S12", "status", GetStatusJson());
    }

    // ─── 60초 카운트다운 / Storage 크기 프로브를 코루틴으로 브리지 ───

    private IEnumerator CountdownRoutine(int seconds, string label)
    {
        for (int remain = seconds; remain > 0; remain--)
        {
            if (_probeProgressText != null)
                _probeProgressText.text = $"{label}: {remain}초 남음 (화면을 켜둔 채로 기다려주세요)";
            yield return new WaitForSeconds(1f);
        }

        if (_probeProgressText != null)
            _probeProgressText.text = $"{label}: 완료, 계속 진행합니다...";
    }

    private IEnumerator StorageProbeRoutine()
    {
        if (_probeProgressText != null)
            _probeProgressText.text = "S6: Storage 크기 프로브 진행 중...";

        var task = RunSizeProbeAsync(s =>
        {
            if (_probeProgressText != null)
                _probeProgressText.text = "S6: Storage 크기 프로브 진행 중...\n" + s;
        });

        while (!task.IsCompleted) yield return null;

        string result = task.IsFaulted
            ? $"FAILED: {(task.Exception?.InnerException?.Message ?? task.Exception?.Message ?? "unknown error")}"
            : task.Result;

        AddEntry("S6", "size-probe", result);
        SaveJournal();
    }

    // ─── 단계 체인 (버튼 클릭 1회 또는 reload 재개 1회당 하나씩 실행) ───

    private IEnumerator Chain_Idle()
    {
        DoS0Action();
        DoS1Action();
        _journal.step = "S2";
        SaveJournal();
        yield break;
    }

    private IEnumerator Chain_S2()
    {
        DoS2Action();
        _journal.step = "S3";
        SaveJournal();
        yield break;
    }

    private IEnumerator Chain_S3()
    {
        DoS3Action();
        DoS4Action();
        _journal.step = "S5";
        SaveJournal();
        yield break;
    }

    private IEnumerator Chain_S5()
    {
        DoS5Action();

        yield return StorageProbeRoutine();
        yield return CountdownRoutine(60, "재시작 전 대기(1/2)");

        PlayerPrefs.SetString(_manualKey, _manualValue);
        PlayerPrefs.Save();
        AddEntry("S7", "set+save (reload#1 전)", $"{_manualKey}={_manualValue}");

        _journal.step = "S8";
        DoReloadOrAbort();
    }

    private IEnumerator Chain_S8()
    {
        DoS8Action();

        SetL3DisabledSafe(1);
        AddEntry("S9", "L3 disabled 세팅 (reload#2 전)", "disabled=1");

        _journal.step = "S10";
        DoReloadOrAbort();
        yield break;
    }

    private IEnumerator Chain_S10()
    {
        DoS10Action();

        yield return CountdownRoutine(60, "재시작 전 대기(2/2, vanilla 모드)");

        PlayerPrefs.SetString(_manualKey, _manualValue);
        PlayerPrefs.Save();
        AddEntry("S11", "set+save (reload#3 전, vanilla 모드)", $"{_manualKey}={_manualValue}");

        _journal.step = "S12";
        DoReloadOrAbort();
    }

    private IEnumerator Chain_S12()
    {
        DoS12Action();

        SetL3DisabledSafe(0);
        AddEntry("S13", "L3 disabled 해제", "disabled=0");

        _journal.step = "DONE";
        SaveJournal();
        EmitResult();
        yield break;
    }
}
