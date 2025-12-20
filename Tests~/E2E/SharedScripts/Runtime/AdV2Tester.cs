using System;
using System.Collections.Generic;
using UnityEngine;
using AppsInToss;

/// <summary>
/// AdV2 (인앱광고 v2) 테스터 컴포넌트
/// LoadFullScreenAd/ShowFullScreenAd API를 테스트합니다.
/// 이것이 현재 권장되는 광고 API입니다.
/// </summary>
public class AdV2Tester : MonoBehaviour
{
    // 테스트용 광고 ID (문서 참조: https://developers-apps-in-toss.toss.im/ads/develop.html)
    private const string TEST_INTERSTITIAL_AD_ID = "ait-ad-test-interstitial-id";
    private const string TEST_REWARDED_AD_ID = "ait-ad-test-rewarded-id";

    // 광고 상태
    private string adStatus = "";
    private bool isAdLoaded = false;
    private string selectedAdType = "interstitial"; // interstitial 또는 rewarded
    private List<string> adEventLog = new List<string>();

    /// <summary>
    /// 마지막 작업 상태 메시지
    /// </summary>
    public string Status => adStatus;

    /// <summary>
    /// AdV2 테스터 UI를 렌더링합니다.
    /// </summary>
    public void DrawUI(
        GUIStyle boxStyle,
        GUIStyle groupHeaderStyle,
        GUIStyle labelStyle,
        GUIStyle buttonStyle,
        GUIStyle textFieldStyle,
        GUIStyle fieldLabelStyle,
        GUIStyle callbackLabelStyle)
    {
        GUILayout.BeginVertical(boxStyle);

        // 섹션 헤더
        GUILayout.Label("AdV2 Tester (인앱광고v2)", groupHeaderStyle);
        GUILayout.Label("LoadFullScreenAd/ShowFullScreenAd API를 테스트합니다.", labelStyle);
        GUILayout.Label("Load → Show 순서로 호출해야 합니다.", labelStyle);

        GUILayout.Space(10);

        // 상태 표시
        string loadStatus = isAdLoaded ? "Loaded" : "Not Loaded";
        GUILayout.Label($"Status: {loadStatus}", labelStyle);
        if (!string.IsNullOrEmpty(adStatus))
        {
            GUILayout.Label($"  {adStatus}", callbackLabelStyle);
        }

        // 이벤트 로그 표시 (최근 5개)
        if (adEventLog.Count > 0)
        {
            GUILayout.Label("Event Log:", labelStyle);
            int startIndex = Math.Max(0, adEventLog.Count - 5);
            for (int i = startIndex; i < adEventLog.Count; i++)
            {
                GUILayout.Label($"  {adEventLog[i]}", callbackLabelStyle);
            }
        }

        GUILayout.Space(10);

        // 광고 타입 선택
        GUILayout.Label("Ad Type:", fieldLabelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(
            selectedAdType == "interstitial" ? "[Interstitial]" : "Interstitial",
            buttonStyle, GUILayout.Height(36)))
        {
            selectedAdType = "interstitial";
        }
        if (GUILayout.Button(
            selectedAdType == "rewarded" ? "[Rewarded]" : "Rewarded",
            buttonStyle, GUILayout.Height(36)))
        {
            selectedAdType = "rewarded";
        }
        GUILayout.EndHorizontal();

        // 현재 선택된 광고 ID 표시
        string currentAdId = selectedAdType == "interstitial" ? TEST_INTERSTITIAL_AD_ID : TEST_REWARDED_AD_ID;
        GUILayout.Label($"Ad ID: {currentAdId}", callbackLabelStyle);

        GUILayout.Space(10);

        // Step 1: 광고 로드
        GUILayout.Label("Step 1: Load Ad", fieldLabelStyle);
        if (GUILayout.Button("LoadFullScreenAd(...)", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteLoadAd();
        }

        GUILayout.Space(10);

        // Step 2: 광고 표시
        GUILayout.Label("Step 2: Show Ad", fieldLabelStyle);
        GUI.enabled = isAdLoaded;
        if (GUILayout.Button("ShowFullScreenAd(...)", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteShowAd();
        }
        GUI.enabled = true;

        if (!isAdLoaded)
        {
            GUILayout.Label("(광고를 먼저 로드해주세요)", callbackLabelStyle);
        }

        GUILayout.Space(10);

        // 로그 초기화
        if (adEventLog.Count > 0)
        {
            if (GUILayout.Button("Clear Log", buttonStyle, GUILayout.Height(32)))
            {
                adEventLog.Clear();
                adStatus = "";
            }
        }

        GUILayout.EndVertical();
    }

    private Action _loadUnsubscribe;

    private void ExecuteLoadAd()
    {
        string adId = selectedAdType == "interstitial" ? TEST_INTERSTITIAL_AD_ID : TEST_REWARDED_AD_ID;
        adStatus = $"Loading {selectedAdType} ad...";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] LoadFullScreenAd({adId})");

        // 기존 구독 해제
        _loadUnsubscribe?.Invoke();

        // LoadFullScreenAd API 호출
        _loadUnsubscribe = AIT.LoadFullScreenAd(
            adGroupId: adId,
            onEvent: (result) =>
            {
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Load event: {result.Type}");
                if (result.Type == "loaded")
                {
                    isAdLoaded = true;
                    adStatus = $"{selectedAdType} ad loaded";
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: Ad loaded");
                }
            },
            onError: (error) =>
            {
                isAdLoaded = false;
                adStatus = $"Error: {error.Message}";
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {error.ErrorCode} - {error.Message}");
            }
        );
    }

    private Action _showUnsubscribe;

    private void ExecuteShowAd()
    {
        if (!isAdLoaded)
        {
            adStatus = "Please load ad first";
            return;
        }

        string adId = selectedAdType == "interstitial" ? TEST_INTERSTITIAL_AD_ID : TEST_REWARDED_AD_ID;
        adStatus = $"Showing {selectedAdType} ad...";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ShowFullScreenAd({adId})");

        // 기존 구독 해제
        _showUnsubscribe?.Invoke();

        // ShowFullScreenAd API 호출
        _showUnsubscribe = AIT.ShowFullScreenAd(
            adGroupId: adId,
            onEvent: (result) =>
            {
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Show event: {result.Type}");
                if (result.Type == "dismissed")
                {
                    // 광고 표시 후 다시 로드 필요 (한 번에 1개만 로드 가능)
                    isAdLoaded = false;
                    adStatus = $"{selectedAdType} ad shown (reload required for next ad)";
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: Ad dismissed");
                }
                else if (result.Type == "userEarnedReward" && result.Data != null)
                {
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Reward: {result.Data.UnitAmount} {result.Data.UnitType}");
                }
            },
            onError: (error) =>
            {
                adStatus = $"Error: {error.Message}";
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {error.ErrorCode} - {error.Message}");
            }
        );
    }

    private void OnDestroy()
    {
        // 컴포넌트 제거 시 구독 해제
        _loadUnsubscribe?.Invoke();
        _showUnsubscribe?.Invoke();
    }
}
