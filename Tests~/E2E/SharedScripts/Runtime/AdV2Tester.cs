using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// AdMob 광고 테스터 컴포넌트
/// GoogleAdMobLoadAppsInTossAdMob/GoogleAdMobShowAppsInTossAdMob API를 테스트합니다.
/// </summary>
public class AdV2Tester : MonoBehaviour
{
    // 테스트용 광고 ID (CI에서 빌드 시 sed로 치환됨)
    private const string TEST_INTERSTITIAL_AD_ID = "ait-ad-test-interstitial-id";
    private const string TEST_REWARDED_AD_ID = "ait-ad-test-rewarded-id";

    // 광고 상태
    private string adStatus = "";
    private bool isAdLoaded = false;
    private string selectedAdType = "interstitial";
    private List<string> adEventLog = new List<string>();
    private int _lastRenderedLogCount = 0;

    // uGUI 참조
    private Text _loadStatusText;
    private Text _statusDetailText;
    private Text _adIdText;
    private GameObject _eventLogContainer;
    private Button _interstitialBtn;
    private Button _rewardedBtn;
    private Image _interstitialBtnImg;
    private Image _rewardedBtnImg;
    private Button _showAdBtn;
    private Text _showAdHintText;
    private Button _clearLogBtn;

    /// <summary>
    /// 마지막 작업 상태 메시지
    /// </summary>
    public string Status => adStatus;

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

        UIBuilder.CreateText(section, "AdMob Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "loadAppsInTossAdMob/showAppsInTossAdMob API를 테스트합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateText(section, "Load → Show 순서로 호출해야 합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 상태
        _loadStatusText = UIBuilder.CreateText(section, "Status: Not Loaded",
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

        // 광고 타입 선택
        UIBuilder.CreateText(section, "Ad Type:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        var typeRow = UIBuilder.CreateHorizontalLayout(section, UIBuilder.Theme.SpacingSmall);
        typeRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _interstitialBtn = UIBuilder.CreateButton(typeRow, "[Interstitial]", onClick: () => SelectAdType("interstitial"));
        UIBuilder.SetLayout(_interstitialBtn.gameObject, flexibleWidth: 1);
        _interstitialBtnImg = _interstitialBtn.GetComponent<Image>();

        _rewardedBtn = UIBuilder.CreateButton(typeRow, "Rewarded", onClick: () => SelectAdType("rewarded"));
        UIBuilder.SetLayout(_rewardedBtn.gameObject, flexibleWidth: 1);
        _rewardedBtnImg = _rewardedBtn.GetComponent<Image>();

        _adIdText = UIBuilder.CreateText(section, $"Ad ID: {TEST_INTERSTITIAL_AD_ID}",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);

        // Step 1: Load
        UIBuilder.CreateText(section, "Step 1: Load Ad",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "loadAppsInTossAdMob(...)", onClick: ExecuteLoadAd);

        // Step 2: Show
        UIBuilder.CreateText(section, "Step 2: Show Ad",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        _showAdBtn = UIBuilder.CreateButton(section, "showAppsInTossAdMob(...)", onClick: ExecuteShowAd);
        _showAdBtn.interactable = false;
        _showAdHintText = UIBuilder.CreateText(section, "(광고를 먼저 로드해주세요)",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);

        // Clear Log
        _clearLogBtn = UIBuilder.CreateButton(section, "Clear Log", onClick: () =>
        {
            adEventLog.Clear();
            _lastRenderedLogCount = 0;
            adStatus = "";
            UpdateUI();
        });
        _clearLogBtn.gameObject.SetActive(false);

        UpdateAdTypeUI();
    }

    private void SelectAdType(string type)
    {
        selectedAdType = type;
        UpdateAdTypeUI();
    }

    private void UpdateAdTypeUI()
    {
        if (_interstitialBtn == null) return;

        var interText = _interstitialBtn.GetComponentInChildren<Text>();
        var rewardText = _rewardedBtn.GetComponentInChildren<Text>();
        if (interText != null) interText.text = selectedAdType == "interstitial" ? "[Interstitial]" : "Interstitial";
        if (rewardText != null) rewardText.text = selectedAdType == "rewarded" ? "[Rewarded]" : "Rewarded";

        if (_interstitialBtnImg != null)
            _interstitialBtnImg.color = selectedAdType == "interstitial" ? UIBuilder.Theme.AccentBg : UIBuilder.Theme.ButtonBg;
        if (_rewardedBtnImg != null)
            _rewardedBtnImg.color = selectedAdType == "rewarded" ? UIBuilder.Theme.AccentBg : UIBuilder.Theme.ButtonBg;

        string adId = selectedAdType == "interstitial" ? TEST_INTERSTITIAL_AD_ID : TEST_REWARDED_AD_ID;
        if (_adIdText != null) _adIdText.text = $"Ad ID: {adId}";
    }

    private void UpdateUI()
    {
        if (_loadStatusText != null)
        {
            string loadStatus = isAdLoaded ? "Loaded" : "Not Loaded";
            _loadStatusText.text = $"Status: {loadStatus}";
        }

        if (_statusDetailText != null)
        {
            _statusDetailText.text = adStatus;
            _statusDetailText.gameObject.SetActive(!string.IsNullOrEmpty(adStatus));
        }

        if (_showAdBtn != null)
            _showAdBtn.interactable = isAdLoaded;
        if (_showAdHintText != null)
            _showAdHintText.gameObject.SetActive(!isAdLoaded);

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

    private Action _loadUnsubscribe;

    private void ExecuteLoadAd()
    {
#if AIT_SDK_1_7_OR_LATER
        string adId = selectedAdType == "interstitial" ? TEST_INTERSTITIAL_AD_ID : TEST_REWARDED_AD_ID;
        adStatus = $"Loading {selectedAdType} ad...";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] loadAppsInTossAdMob(adGroupId: {adId})");
        UpdateUI();

        _loadUnsubscribe?.Invoke();

#pragma warning disable CS0618
        _loadUnsubscribe = AIT.GoogleAdMobLoadAppsInTossAdMob(
            options: new LoadAdMobOptions { AdGroupId = adId },
            onEvent: (result) =>
            {
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Load event: {result.Type}");
                if (result.Type == "loaded")
                {
                    isAdLoaded = true;
                    adStatus = $"{selectedAdType} ad loaded";
                    if (result.Data != null)
                    {
                        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] AdGroupId: {result.Data.AdGroupId}, AdUnitId: {result.Data.AdUnitId}");
                    }
                }
                UpdateUI();
            },
            onError: (error) =>
            {
                isAdLoaded = false;
                adStatus = $"Error: {error.Message}";
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {error.ErrorCode} - {error.Message}");
                UpdateUI();
            }
        );
#pragma warning restore CS0618
#else
        adStatus = "AdMob API requires SDK 1.7.0+";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] AdMob Load API not available in this SDK version");
        UpdateUI();
#endif
    }

    private Action _showUnsubscribe;

    private void ExecuteShowAd()
    {
#if AIT_SDK_1_7_OR_LATER
        if (!isAdLoaded)
        {
            adStatus = "Please load ad first";
            UpdateUI();
            return;
        }

        string adId = selectedAdType == "interstitial" ? TEST_INTERSTITIAL_AD_ID : TEST_REWARDED_AD_ID;
        adStatus = $"Showing {selectedAdType} ad...";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] showAppsInTossAdMob(adGroupId: {adId})");
        UpdateUI();

        _showUnsubscribe?.Invoke();

#pragma warning disable CS0618
        _showUnsubscribe = AIT.GoogleAdMobShowAppsInTossAdMob(
            options: new ShowAdMobOptions { AdGroupId = adId },
            onEvent: (result) =>
            {
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Show event: {result.Type}");
                if (result.Type == "dismissed")
                {
                    isAdLoaded = false;
                    adStatus = $"{selectedAdType} ad shown (reload required for next ad)";
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: Ad dismissed");
                }
                else if (result.Type == "userEarnedReward" && result.Data != null)
                {
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Reward: {result.Data.UnitAmount} {result.Data.UnitType}");
                }
                UpdateUI();
            },
            onError: (error) =>
            {
                adStatus = $"Error: {error.Message}";
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {error.ErrorCode} - {error.Message}");
                UpdateUI();
            }
        );
#pragma warning restore CS0618
#else
        adStatus = "AdMob API requires SDK 1.7.0+";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] AdMob Show API not available in this SDK version");
        UpdateUI();
#endif
    }

    private void OnDestroy()
    {
        _loadUnsubscribe?.Invoke();
        _showUnsubscribe?.Invoke();
    }
}
