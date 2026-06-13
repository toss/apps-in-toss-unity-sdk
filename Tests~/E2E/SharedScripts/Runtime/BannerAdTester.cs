using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// 배너 광고 테스터 컴포넌트
/// AITBannerAdView 컴포넌트(RectTransform 추적)와 AITBannerAd 정적 helper(Top/Bottom 프리셋)를 테스트합니다.
/// 배너 강조 유형 2종(문구 강조 ~90px 고정 / 이미지 강조 16:9 가변 높이)을 각각의 컴포넌트 데모로 띄워
/// ResizeObserver 기반 자동 높이 조정이 실제 렌더 높이를 따라가는지 한 번에 확인합니다.
/// 컴포넌트 데모를 동시에 띄우면 multi-slot(배너 여러 개 동시 표시)도 확인할 수 있습니다.
/// 실제 광고 렌더링은 Toss 앱/샌드박스 안에서만 가능하며, 밖에서는 Initialized 이후
/// FailedToRender/NoFill 이벤트가 도착하는 것이 정상입니다.
/// </summary>
public class BannerAdTester : MonoBehaviour
{
    // 테스트용 광고 ID (CI에서 빌드 시 sed로 치환됨) — 강조 유형은 콘솔의 광고 그룹 설정이 결정하므로 ID를 분리
    private const string TEST_BANNER_TEXT_AD_ID = "ait-ad-test-banner-text-id";   // 문구 강조 (text-emphasis)
    private const string TEST_BANNER_IMAGE_AD_ID = "ait-ad-test-banner-image-id"; // 이미지 강조 (image-emphasis)

    private List<string> adEventLog = new List<string>();
    private int _lastRenderedLogCount = 0;

    private AITBannerAdView _textView;
    private AITBannerAdView _imageView;
    private Action<AITBannerAdEvent> _staticEventHandler;

    // uGUI 참조
    private Text _statusText;
    private GameObject _eventLogContainer;
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

        UIBuilder.CreateText(section, "Banner Ad Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "AITBannerAdView 컴포넌트 / AITBannerAd 정적 helper를 테스트합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateText(section, "실제 광고는 Toss 앱/샌드박스 안에서만 렌더링됩니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateText(section, $"문구 강조 ID: {TEST_BANNER_TEXT_AD_ID}",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
        UIBuilder.CreateText(section, $"이미지 강조 ID: {TEST_BANNER_IMAGE_AD_ID}",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);

        _statusText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);

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

        // ── 컴포넌트 데모 2종: 문구 강조 / 이미지 강조 (자동 높이) ──
        _textView = BuildComponentDemo(section,
            "Component: 문구 강조 (text-emphasis, ~90px 고정)", TEST_BANNER_TEXT_AD_ID, "Text");
        _imageView = BuildComponentDemo(section,
            "Component: 이미지 강조 (image-emphasis, 16:9 가변 높이)", TEST_BANNER_IMAGE_AD_ID, "Image");

        // ── 정적 helper 데모: AITBannerAd (화면 Top/Bottom 프리셋, 단일 슬롯) ──
        UIBuilder.CreateText(section, "Static: AITBannerAd.Show (Top/Bottom preset, 단일 슬롯)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        var staticRow = UIBuilder.CreateHorizontalLayout(section, UIBuilder.Theme.SpacingSmall);
        staticRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var showTopBtn = UIBuilder.CreateButton(staticRow, "Show Top (문구)", onClick: () =>
        {
            LogEvent("AITBannerAd.Show(Top, 문구)");
            AITBannerAd.Show(TEST_BANNER_TEXT_AD_ID, AITBannerPosition.Top);
            UpdateUI();
        });
        UIBuilder.SetLayout(showTopBtn.gameObject, flexibleWidth: 1);
        var showBottomBtn = UIBuilder.CreateButton(staticRow, "Show Bottom (이미지)", onClick: () =>
        {
            LogEvent("AITBannerAd.Show(Bottom, 이미지)");
            AITBannerAd.Show(TEST_BANNER_IMAGE_AD_ID, AITBannerPosition.Bottom);
            UpdateUI();
        });
        UIBuilder.SetLayout(showBottomBtn.gameObject, flexibleWidth: 1);
        var hideStaticBtn = UIBuilder.CreateButton(staticRow, "Hide", onClick: () =>
        {
            LogEvent("AITBannerAd.Hide()");
            AITBannerAd.Hide();
            UpdateUI();
        });
        UIBuilder.SetLayout(hideStaticBtn.gameObject, flexibleWidth: 1);

        // Clear Log
        _clearLogBtn = UIBuilder.CreateButton(section, "Clear Log", onClick: () =>
        {
            adEventLog.Clear();
            _lastRenderedLogCount = 0;
            UpdateUI();
        });
        _clearLogBtn.gameObject.SetActive(false);

        // 정적 helper 이벤트 구독 (OnDestroy에서 해제)
        _staticEventHandler = evt => LogEvent($"[Static] {evt}");
        AITBannerAd.OnAdEvent += _staticEventHandler;

        UpdateUI();
    }

    /// <summary>
    /// FollowRectTransform 모드 AITBannerAdView 데모 한 벌(placeholder + Show/Hide)을 만든다.
    /// 레이아웃 그룹이 placeholder 높이를 제어하므로 컴포넌트 자동 리사이즈는 끄고,
    /// Resized 이벤트에서 역산된 RenderedHeightLocal을 LayoutElement.preferredHeight에 직접 반영해
    /// placeholder가 실제 배너 높이를 따라가게 한다 (문구 강조 ~90px vs 이미지 강조 가변).
    /// </summary>
    private AITBannerAdView BuildComponentDemo(Transform section, string title, string adId, string slotTag)
    {
        UIBuilder.CreateText(section, title,
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 배너가 오버레이될 placeholder 영역 — 이 RectTransform을 AITBannerAdView가 추적
        var placeholder = UIBuilder.CreatePanel(section, UIBuilder.Theme.InputBg);
        var placeholderLayout = UIBuilder.SetLayout(placeholder.gameObject, minHeight: 100);
        var placeholderLabel = UIBuilder.CreateText(placeholder, "(banner renders here)",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback, TextAnchor.MiddleCenter);
        UIBuilder.SetStretch((RectTransform)placeholderLabel.transform);

        // AddComponent 시점엔 adGroupId가 비어 있어 OnEnable의 자동 표시가 동작하지 않음 — 버튼으로만 표시
        var view = placeholder.gameObject.AddComponent<AITBannerAdView>();
        view.AdGroupId = adId;
        // 레이아웃 그룹이 높이를 제어하므로 컴포넌트의 RectTransform 자동 리사이즈는 끈다 (preferredHeight로 대체)
        view.AutoResizeHeight = false;
        view.OnAdEvent += evt =>
        {
            LogEvent($"[{slotTag}] {evt}");
            if (evt.Kind == AITBannerAdEventKind.Resized && view.RenderedHeightLocal > 0f)
            {
                placeholderLayout.preferredHeight = view.RenderedHeightLocal;
            }
        };
        view.OnAdEventUnity.AddListener(evt => LogEvent($"[{slotTag}/UnityEvent] {evt.Kind}"));

        var row = UIBuilder.CreateHorizontalLayout(section, UIBuilder.Theme.SpacingSmall);
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var showBtn = UIBuilder.CreateButton(row, "Show (Rect)", onClick: () =>
        {
            LogEvent($"[{slotTag}] Show()");
            view.Show();
            UpdateUI();
        });
        UIBuilder.SetLayout(showBtn.gameObject, flexibleWidth: 1);
        var hideBtn = UIBuilder.CreateButton(row, "Hide", onClick: () =>
        {
            LogEvent($"[{slotTag}] Hide()");
            view.Hide();
            UpdateUI();
        });
        UIBuilder.SetLayout(hideBtn.gameObject, flexibleWidth: 1);

        return view;
    }

    private void LogEvent(string message)
    {
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_statusText != null)
        {
            bool textShowing = _textView != null && _textView.IsShowing;
            bool imageShowing = _imageView != null && _imageView.IsShowing;
            _statusText.text = $"문구: {(textShowing ? "표시" : "숨김")} / 이미지: {(imageShowing ? "표시" : "숨김")} / Static: {(AITBannerAd.IsShowing ? "표시" : "숨김")}";
        }

        UpdateEventLog();

        if (_clearLogBtn != null)
            _clearLogBtn.gameObject.SetActive(adEventLog.Count > 0);
    }

    private void UpdateEventLog()
    {
        if (_eventLogContainer == null) return;

        if (adEventLog.Count == 0)
        {
            _eventLogContainer.SetActive(false);
            _lastRenderedLogCount = 0;
            for (int i = _eventLogContainer.transform.childCount - 1; i >= 0; i--)
                Destroy(_eventLogContainer.transform.GetChild(i).gameObject);
            return;
        }

        _eventLogContainer.SetActive(true);

        int displayStart = Math.Max(0, adEventLog.Count - 5);
        int prevDisplayStart = Math.Max(0, _lastRenderedLogCount - 5);

        if (_lastRenderedLogCount == 0 || displayStart != prevDisplayStart)
        {
            // 전체 재구축
            for (int i = _eventLogContainer.transform.childCount - 1; i >= 0; i--)
                Destroy(_eventLogContainer.transform.GetChild(i).gameObject);

            UIBuilder.CreateText(_eventLogContainer.transform, "Event Log:",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            for (int i = displayStart; i < adEventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {adEventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }
        else
        {
            for (int i = _lastRenderedLogCount; i < adEventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {adEventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }

        _lastRenderedLogCount = adEventLog.Count;
    }

    private void OnDestroy()
    {
        if (_staticEventHandler != null)
        {
            AITBannerAd.OnAdEvent -= _staticEventHandler;
            _staticEventHandler = null;
        }

        AITBannerAd.Hide();
        // _textView/_imageView는 각자 OnDisable/OnDestroy에서 스스로 Hide함
    }
}
