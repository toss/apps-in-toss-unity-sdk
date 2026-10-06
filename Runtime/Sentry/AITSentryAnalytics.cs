// -----------------------------------------------------------------------
// <copyright file="AITSentryAnalytics.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry Analytics Integration
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

// Sentry Unity SDK 4.0+: SentrySdk는 Sentry.Unity 네임스페이스에 위치
using SentrySdk = Sentry.Unity.SentrySdk;

namespace AppsInToss.Sentry
{
    /// <summary>
    /// Analytics API 호출을 Sentry breadcrumb 및 컨텍스트와 자동으로 연동하는 모듈.
    /// AIT.AnalyticsScreen/Impression/Click 호출 시 Sentry에 디버깅 컨텍스트를 자동 기록합니다.
    /// </summary>
    [Preserve]
    public static class AITSentryAnalytics
    {
        private const string Tag = "[AITSentry]";
        private const string ContextKey = "ait_analytics";
        private const string BreadcrumbCategory = "analytics";

        private static bool _initialized;
        private static int _screenCount;
        private static int _impressionCount;
        private static int _clickCount;
        private static string _lastScreenName;

        /// <summary>
        /// 씬 전환 시 자동으로 AnalyticsScreen을 호출할지 여부.
        /// true로 설정하면 SceneManager.sceneLoaded 이벤트에서 자동으로 TrackScreen을 호출합니다.
        /// </summary>
        /// <remarks>
        /// AITSentryIntegration의 scene breadcrumb과 별도로 analytics breadcrumb이 추가로 기록됩니다.
        /// </remarks>
        public static bool AutoScreenTrackingEnabled { get; set; }

        /// <summary>
        /// AITSentryIntegration에서 호출되어 초기화합니다.
        /// </summary>
        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateContext();

            Debug.Log($"{Tag} Analytics 연동 초기화 완료");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!AutoScreenTrackingEnabled) return;
            if (!SentrySdk.IsEnabled) return;

            TrackScreenFireAndForget(scene.name);
        }

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// AnalyticsScreen API를 호출하고 Sentry breadcrumb을 기록합니다.
        /// </summary>
        /// <param name="paramsParam">Analytics 파라미터 (선택)</param>
        public static async Awaitable TrackScreen(object paramsParam = null)
        {
            await AIT.AnalyticsScreen(paramsParam);
            RecordBreadcrumb("screen", paramsParam, ref _screenCount);
        }

        /// <summary>
        /// AnalyticsImpression API를 호출하고 Sentry breadcrumb을 기록합니다.
        /// </summary>
        /// <param name="paramsParam">Analytics 파라미터 (선택)</param>
        public static async Awaitable TrackImpression(object paramsParam = null)
        {
            await AIT.AnalyticsImpression(paramsParam);
            RecordBreadcrumb("impression", paramsParam, ref _impressionCount);
        }

        /// <summary>
        /// AnalyticsClick API를 호출하고 Sentry breadcrumb을 기록합니다.
        /// </summary>
        /// <param name="paramsParam">Analytics 파라미터 (선택)</param>
        public static async Awaitable TrackClick(object paramsParam = null)
        {
            await AIT.AnalyticsClick(paramsParam);
            RecordBreadcrumb("click", paramsParam, ref _clickCount);
        }

        private static async void TrackScreenFireAndForget(string sceneName)
        {
            try
            {
                await TrackScreen(new { screen_name = sceneName });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} 자동 화면 추적 실패: {ex.Message}");
            }
        }
#else
        /// <summary>
        /// AnalyticsScreen API를 호출하고 Sentry breadcrumb을 기록합니다.
        /// </summary>
        /// <param name="paramsParam">Analytics 파라미터 (선택)</param>
        public static async System.Threading.Tasks.Task TrackScreen(object paramsParam = null)
        {
            await AIT.AnalyticsScreen(paramsParam);
            RecordBreadcrumb("screen", paramsParam, ref _screenCount);
        }

        /// <summary>
        /// AnalyticsImpression API를 호출하고 Sentry breadcrumb을 기록합니다.
        /// </summary>
        /// <param name="paramsParam">Analytics 파라미터 (선택)</param>
        public static async System.Threading.Tasks.Task TrackImpression(object paramsParam = null)
        {
            await AIT.AnalyticsImpression(paramsParam);
            RecordBreadcrumb("impression", paramsParam, ref _impressionCount);
        }

        /// <summary>
        /// AnalyticsClick API를 호출하고 Sentry breadcrumb을 기록합니다.
        /// </summary>
        /// <param name="paramsParam">Analytics 파라미터 (선택)</param>
        public static async System.Threading.Tasks.Task TrackClick(object paramsParam = null)
        {
            await AIT.AnalyticsClick(paramsParam);
            RecordBreadcrumb("click", paramsParam, ref _clickCount);
        }

        private static async void TrackScreenFireAndForget(string sceneName)
        {
            try
            {
                await TrackScreen(new { screen_name = sceneName });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} 자동 화면 추적 실패: {ex.Message}");
            }
        }
#endif

        private static void RecordBreadcrumb(string type, object paramsParam, ref int counter)
        {
            counter++;
            var sceneName = SceneManager.GetActiveScene().name;

            if (type == "screen")
            {
                _lastScreenName = !string.IsNullOrEmpty(sceneName) ? sceneName : "unknown";
            }

            if (!SentrySdk.IsEnabled) return;

            var data = new Dictionary<string, string> { { "type", type } };

            if (paramsParam != null)
            {
                try
                {
                    data["params"] = AITJsonSettings.Serialize(paramsParam);
                }
                catch (Exception)
                {
                    data["params"] = paramsParam.ToString();
                }
            }

            if (!string.IsNullOrEmpty(sceneName))
            {
                data["scene"] = sceneName;
            }

            SentrySdk.AddBreadcrumb(
                message: $"Analytics {type} tracked",
                category: BreadcrumbCategory,
                level: BreadcrumbLevel.Info,
                data: data
            );

            UpdateContext();
        }

        private static void UpdateContext()
        {
            if (!SentrySdk.IsEnabled) return;

            try
            {
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.Contexts[ContextKey] = new Dictionary<string, object>
                    {
                        { "screen_count", _screenCount },
                        { "impression_count", _impressionCount },
                        { "click_count", _clickCount },
                        { "last_screen", _lastScreenName ?? "none" },
                        { "auto_tracking", AutoScreenTrackingEnabled }
                    };
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Analytics 컨텍스트 업데이트 실패: {ex.Message}");
            }
        }
    }
}
