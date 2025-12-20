using System;
using System.Collections.Generic;
using UnityEngine;
using AppsInToss;

/// <summary>
/// AdV1 (인앱광고 v1 - Deprecated) 테스터 컴포넌트
/// GoogleAdMob 네임스페이스의 deprecated API를 테스트합니다.
/// 새 프로젝트는 AdV2Tester (LoadFullScreenAd/ShowFullScreenAd)를 사용하세요.
/// </summary>
public class AdV1Tester : MonoBehaviour
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
    /// AdMob 테스터 UI를 렌더링합니다.
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
        GUILayout.Label("AdV1 Tester (Deprecated)", groupHeaderStyle);
        GUILayout.Label("GoogleAdMob 네임스페이스의 deprecated API를 테스트합니다.", labelStyle);
        GUILayout.Label("새 프로젝트는 AdV2Tester를 사용하세요.", labelStyle);

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
        if (GUILayout.Button("GoogleAdMobLoadAppsInTossAdMob(...)", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteLoadAd();
        }

        GUILayout.Space(10);

        // Step 2: 광고 표시
        GUILayout.Label("Step 2: Show Ad", fieldLabelStyle);
        GUI.enabled = isAdLoaded;
        if (GUILayout.Button("GoogleAdMobShowAppsInTossAdMob(...)", buttonStyle, GUILayout.Height(40)))
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
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] LoadAppsInTossAdMob({selectedAdType})");

        // 기존 구독 해제
        _loadUnsubscribe?.Invoke();

        // 새 API 시그니처: onEvent, options, onError
        var options = new LoadAdMobOptions
        {
            AdGroupId = adId
        };

        _loadUnsubscribe = AIT.GoogleAdMobLoadAppsInTossAdMob(
            onEvent: (result) =>
            {
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Ad load event: {result.Type}");
                if (result.Type == "loaded")
                {
                    isAdLoaded = true;
                    adStatus = $"{selectedAdType} ad loaded";
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: Ad loaded");
                }
            },
            options: options,
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

        adStatus = $"Showing {selectedAdType} ad...";
        adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ShowAppsInTossAdMob({selectedAdType})");

        // 기존 구독 해제
        _showUnsubscribe?.Invoke();

        // 새 API 시그니처: onEvent, options, onError
        _showUnsubscribe = AIT.GoogleAdMobShowAppsInTossAdMob(
            onEvent: (result) =>
            {
                adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Ad show event: {result.Type}");
                if (result.Type == "closed")
                {
                    // 광고 표시 후 다시 로드 필요 (한 번에 1개만 로드 가능)
                    isAdLoaded = false;
                    adStatus = $"{selectedAdType} ad shown (reload required for next ad)";
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: Ad closed");
                }
                else if (result.Type == "rewarded" && result.Data != null)
                {
                    adEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Reward: {result.Data.UnitAmount} {result.Data.UnitType}");
                }
            },
            options: null,
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
