using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// 배너 광고 테스터 컴포넌트
/// AITBannerAdView 컴포넌트(RectTransform 추적)와 AITBannerAd 정적 helper(Top/Bottom 프리셋)를 테스트합니다.
/// 두 데모를 동시에 띄우면 multi-slot(배너 2개 동시 표시)을 확인할 수 있습니다.
/// 실제 광고 렌더링은 Toss 앱/샌드박스 안에서만 가능하며, 밖에서는 Initialized 이후
/// FailedToRender/NoFill 이벤트가 도착하는 것이 정상입니다.
/// </summary>
public class BannerAdTester : MonoBehaviour
{
    // 테스트용 광고 ID (CI에서 빌드 시 sed로 치환됨)
    private const string TEST_BANNER_AD_ID = "ait-ad-test-banner-id";

    private List<string> adEventLog = new List<string>();
    private int _lastRenderedLogCount = 0;

    private AITBannerAdView _bannerView;
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
        UIBuilder.CreateText(section, $"Ad ID: {TEST_BANNER_AD_ID}",
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

        // ── 컴포넌트 데모: AITBannerAdView (RectTransform 추적) ──
        UIBuilder.CreateText(section, "Component: AITBannerAdView (FollowRectTransform)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 배너가 오버레이될 placeholder 영역 — 이 RectTransform을 AITBannerAdView가 추적
        var placeholder = UIBuilder.CreatePanel(section, UIBuilder.Theme.InputBg);
        UIBuilder.SetLayout(placeholder.gameObject, minHeight: 100);
        var placeholderLabel = UIBuilder.CreateText(placeholder, "(banner renders here)",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback, TextAnchor.MiddleCenter);
        UIBuilder.SetStretch((RectTransform)placeholderLabel.transform);

        // AddComponent 시점엔 adGroupId가 비어 있어 OnEnable의 자동 표시가 동작하지 않음 — 버튼으로만 표시
        _bannerView = placeholder.gameObject.AddComponent<AITBannerAdView>();
        _bannerView.AdGroupId = TEST_BANNER_AD_ID;
        _bannerView.OnAdEvent += evt => LogEvent($"[View] {evt}");
        _bannerView.OnAdEventUnity.AddListener(evt => LogEvent($"[View/UnityEvent] {evt.Kind}"));

        var viewRow = UIBuilder.CreateHorizontalLayout(section, UIBuilder.Theme.SpacingSmall);
        viewRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var showViewBtn = UIBuilder.CreateButton(viewRow, "Show (Rect)", onClick: () =>
        {
            LogEvent("AITBannerAdView.Show()");
            _bannerView.Show();
            UpdateUI();
        });
        UIBuilder.SetLayout(showViewBtn.gameObject, flexibleWidth: 1);
        var hideViewBtn = UIBuilder.CreateButton(viewRow, "Hide", onClick: () =>
        {
            LogEvent("AITBannerAdView.Hide()");
            _bannerView.Hide();
            UpdateUI();
        });
        UIBuilder.SetLayout(hideViewBtn.gameObject, flexibleWidth: 1);

        // ── 정적 helper 데모: AITBannerAd (화면 Top/Bottom 프리셋) ──
        UIBuilder.CreateText(section, "Static: AITBannerAd.Show (Top/Bottom preset)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        var staticRow = UIBuilder.CreateHorizontalLayout(section, UIBuilder.Theme.SpacingSmall);
        staticRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var showTopBtn = UIBuilder.CreateButton(staticRow, "Show Top", onClick: () =>
        {
            LogEvent("AITBannerAd.Show(Top)");
            AITBannerAd.Show(TEST_BANNER_AD_ID, AITBannerPosition.Top);
            UpdateUI();
        });
        UIBuilder.SetLayout(showTopBtn.gameObject, flexibleWidth: 1);
        var showBottomBtn = UIBuilder.CreateButton(staticRow, "Show Bottom", onClick: () =>
        {
            LogEvent("AITBannerAd.Show(Bottom)");
            AITBannerAd.Show(TEST_BANNER_AD_ID, AITBannerPosition.Bottom);
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

    private void LogEvent(string message)
    {
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_statusText != null)
        {
            bool viewShowing = _bannerView != null && _bannerView.IsShowing;
            _statusText.text = $"View: {(viewShowing ? "Showing" : "Hidden")} / Static: {(AITBannerAd.IsShowing ? "Showing" : "Hidden")}";
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
        // _bannerView는 자기 OnDisable/OnDestroy에서 스스로 Hide함
    }
}
