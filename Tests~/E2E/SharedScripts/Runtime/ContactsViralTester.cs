using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// ContactsViral 테스터 컴포넌트
/// contactsViral API를 테스트합니다.
/// 공유 완료 후 onEvent/onError 콜백이 Unity로 전달되는지 확인합니다.
/// </summary>
public class ContactsViralTester : MonoBehaviour
{
    // 테스트용 moduleId (실제 환경에서는 유효한 ID 필요)
    private string moduleId = "test-module-id";

    // 상태
    private string status = "";
    private bool isActive = false;
    private List<string> eventLog = new List<string>();
    private int _lastRenderedLogCount = 0;
    private bool isPasting = false;

    // 구독 해제 액션
    private Action _unsubscribe;

    // uGUI 참조
    private Text _activeStatusText;
    private Text _statusDetailText;
    private InputField _moduleIdInput;
    private GameObject _eventLogContainer;
    private Button _actionButton;
    private Text _actionButtonText;
    private Button _clearLogBtn;

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

        UIBuilder.CreateText(section, "ContactsViral Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "contactsViral API를 테스트합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateText(section, "공유 창에서 공유 완료 후 콜백 이벤트를 확인합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 상태 표시
        _activeStatusText = UIBuilder.CreateText(section, "Status: Inactive",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _statusDetailText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextCallback);
        _statusDetailText.gameObject.SetActive(false);

        // 이벤트 로그 컨테이너
        _eventLogContainer = new GameObject("EventLog");
        _eventLogContainer.AddComponent<RectTransform>().SetParent(section, false);
        var elVlg = _eventLogContainer.AddComponent<VerticalLayoutGroup>();
        elVlg.spacing = 2;
        elVlg.childForceExpandWidth = true;
        elVlg.childForceExpandHeight = false;
        elVlg.childControlWidth = true;
        elVlg.childControlHeight = true;
        _eventLogContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _eventLogContainer.SetActive(false);

        // Module ID 입력
        var idRow = UIBuilder.CreateHorizontalLayout(section, 8);
        idRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var idLabel = UIBuilder.CreateText(idRow, "Module ID:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(idLabel.gameObject, minWidth: 90, preferredWidth: 90);

        _moduleIdInput = UIBuilder.CreateInputField(idRow, "test-module-id",
            onValueChanged: (v) => moduleId = v);
        _moduleIdInput.text = moduleId;
        UIBuilder.SetLayout(_moduleIdInput.gameObject, flexibleWidth: 1);

        var pasteBtn = UIBuilder.CreateButton(idRow, "PASTE", onClick: PasteFromClipboard);
        UIBuilder.SetLayout(pasteBtn.gameObject, minWidth: 70, preferredWidth: 70);

        // 액션 버튼
        _actionButton = UIBuilder.CreateButton(section, "contactsViral(...) 호출",
            onClick: OnActionButtonClick);
        _actionButtonText = _actionButton.GetComponentInChildren<Text>();

        // Clear Log
        _clearLogBtn = UIBuilder.CreateButton(section, "Clear Log", onClick: () =>
        {
            eventLog.Clear();
            _lastRenderedLogCount = 0;
            status = "";
            UpdateUI();
        });
        _clearLogBtn.gameObject.SetActive(false);

        UpdateUI();
    }

    private void OnActionButtonClick()
    {
        if (isActive) Unsubscribe(); else ExecuteContactsViral();
        UpdateUI();
    }

    private void UpdateEventLog()
    {
        if (_eventLogContainer == null) return;

        if (eventLog.Count == 0)
        {
            _eventLogContainer.SetActive(false);
            _lastRenderedLogCount = 0;
            // 기존 자식 제거
            for (int i = _eventLogContainer.transform.childCount - 1; i >= 0; i--)
                Destroy(_eventLogContainer.transform.GetChild(i).gameObject);
            if (_clearLogBtn != null) _clearLogBtn.gameObject.SetActive(false);
            return;
        }

        _eventLogContainer.SetActive(true);

        // 최근 10개만 표시하므로, 표시 시작 인덱스가 변경되면 전체 재구축
        int displayStart = Math.Max(0, eventLog.Count - 10);
        int prevDisplayStart = Math.Max(0, _lastRenderedLogCount - 10);

        if (_lastRenderedLogCount == 0 || displayStart != prevDisplayStart)
        {
            // 전체 재구축
            for (int i = _eventLogContainer.transform.childCount - 1; i >= 0; i--)
                Destroy(_eventLogContainer.transform.GetChild(i).gameObject);

            UIBuilder.CreateText(_eventLogContainer.transform, "Event Log:",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            for (int i = displayStart; i < eventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {eventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }
        else
        {
            // 새 항목만 추가
            for (int i = _lastRenderedLogCount; i < eventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {eventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }

        _lastRenderedLogCount = eventLog.Count;
        if (_clearLogBtn != null) _clearLogBtn.gameObject.SetActive(true);
    }

    private void UpdateUI()
    {
        if (_activeStatusText != null)
        {
            string activeStatus = isActive ? "Active (구독 중)" : "Inactive";
            _activeStatusText.text = $"Status: {activeStatus}";
        }

        if (_statusDetailText != null)
        {
            _statusDetailText.text = status;
            _statusDetailText.gameObject.SetActive(!string.IsNullOrEmpty(status));
        }

        if (_actionButtonText != null)
        {
            _actionButtonText.text = isActive ? "구독 해제 (Unsubscribe)" : "contactsViral(...) 호출";
        }

        UpdateEventLog();
    }

    private void ExecuteContactsViral()
    {
        status = "공유 창 열기 중...";
        eventLog.Add($"[{DateTime.Now:HH:mm:ss}] contactsViral(moduleId: {moduleId}) 호출");

        try
        {
            Action<ContactsViralEvent> onEvent = (evt) =>
            {
                Debug.Log($"[ContactsViralTester] onEvent: {evt?.Type}");
                eventLog.Add($"[{DateTime.Now:HH:mm:ss}] onEvent: Type={evt?.Type}");

                if (evt?.Data != null)
                {
                    eventLog.Add($"[{DateTime.Now:HH:mm:ss}]   Data: {JsonUtility.ToJson(evt.Data)}");
                }

                if (evt?.Type == "success" || evt?.Type == "completed")
                {
                    status = "공유 완료!";
                    eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 공유 완료 이벤트 수신됨");
                }
                else if (evt?.Type == "reward")
                {
                    status = "리워드 수신!";
                    eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 리워드 이벤트 수신됨");
                }
                else if (evt?.Type == "closed" || evt?.Type == "dismissed")
                {
                    status = "공유 창 닫힘";
                    eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 공유 창이 닫혔습니다");
                }
                UpdateUI();
            };

            _unsubscribe = AIT.ContactsViral(onEvent, new ContactsViralParamsOptions { ModuleId = moduleId });

            isActive = true;
            status = "공유 창 열림 (이벤트 대기 중...)";
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 구독 시작됨");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ContactsViralTester] Exception: {ex}");
            status = $"Exception: {ex.Message}";
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}");
            isActive = false;
        }
        UpdateUI();
    }

    private void Unsubscribe()
    {
        if (_unsubscribe != null)
        {
            _unsubscribe.Invoke();
            _unsubscribe = null;
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 구독 해제됨");
        }
        isActive = false;
        status = "구독 해제됨";
    }

    private async void PasteFromClipboard()
    {
        if (isPasting) return;
        isPasting = true;
        try
        {
            string text = await AIT.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
            {
                moduleId = text.Trim();
                if (_moduleIdInput != null) _moduleIdInput.text = moduleId;
                eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 클립보드에서 붙여넣기: {moduleId}");
                UpdateUI();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ContactsViralTester] Clipboard read failed: {ex.Message}");
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 클립보드 읽기 실패: {ex.Message}");
            UpdateUI();
        }
        finally
        {
            isPasting = false;
        }
    }

    private void OnDestroy()
    {
        _unsubscribe?.Invoke();
    }
}
