// -----------------------------------------------------------------------
// <copyright file="AIT.Advertising.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Advertising APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Advertising
    /// </summary>
    public static partial class AIT
    {
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobLoadAdMobInterstitialAd(
            Action<LoadAdMobInterstitialAdEvent> onEvent,
            LoadAdMobInterstitialAdOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<LoadAdMobInterstitialAdEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobLoadAdMobInterstitialAd_Internal(AITJsonSettings.Serialize(options), subscriptionId, "LoadAdMobInterstitialAdEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAdMobInterstitialAd called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAdMobInterstitialAd cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAdMobInterstitialAd_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobShowAdMobInterstitialAd(
            Action<ShowAdMobInterstitialAdEvent> onEvent,
            ShowAdMobInterstitialAdOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ShowAdMobInterstitialAdEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobShowAdMobInterstitialAd_Internal(AITJsonSettings.Serialize(options), subscriptionId, "ShowAdMobInterstitialAdEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAdMobInterstitialAd called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAdMobInterstitialAd cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAdMobInterstitialAd_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobLoadAdMobRewardedAd(
            Action<LoadAdMobRewardedAdEvent> onEvent,
            LoadAdMobRewardedAdOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<LoadAdMobRewardedAdEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobLoadAdMobRewardedAd_Internal(AITJsonSettings.Serialize(options), subscriptionId, "LoadAdMobRewardedAdEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAdMobRewardedAd called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAdMobRewardedAd cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAdMobRewardedAd_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobShowAdMobRewardedAd(
            Action<ShowAdMobRewardedAdEvent> onEvent,
            ShowAdMobRewardedAdOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ShowAdMobRewardedAdEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobShowAdMobRewardedAd_Internal(AITJsonSettings.Serialize(options), subscriptionId, "ShowAdMobRewardedAdEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAdMobRewardedAd called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAdMobRewardedAd cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAdMobRewardedAd_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobLoadAppsInTossAdMob(
            Action<LoadAdMobEvent> onEvent,
            LoadAdMobOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<LoadAdMobEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobLoadAppsInTossAdMob_Internal(AITJsonSettings.Serialize(options), subscriptionId, "LoadAdMobEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMob called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMob cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAppsInTossAdMob_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobShowAppsInTossAdMob(
            Action<ShowAdMobEvent> onEvent,
            ShowAdMobOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ShowAdMobEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobShowAppsInTossAdMob_Internal(AITJsonSettings.Serialize(options), subscriptionId, "ShowAdMobEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMob called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMob cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAppsInTossAdMob_Internal(string options, string subscriptionId, string typeName);
#endif
    }
}
