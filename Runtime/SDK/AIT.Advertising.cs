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
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static async Task<System.Action> GoogleAdMobLoadAdMobInterstitialAd(GoogleAdMobLoadAdMobInterstitialAdArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobLoadAdMobInterstitialAd_Internal(AITJsonSettings.Serialize(args), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAdMobInterstitialAd called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAdMobInterstitialAd_Internal(string args, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static async Task<System.Action> GoogleAdMobShowAdMobInterstitialAd(GoogleAdMobShowAdMobInterstitialAdArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobShowAdMobInterstitialAd_Internal(AITJsonSettings.Serialize(args), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAdMobInterstitialAd called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAdMobInterstitialAd_Internal(string args, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static async Task<System.Action> GoogleAdMobLoadAdMobRewardedAd(GoogleAdMobLoadAdMobRewardedAdArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobLoadAdMobRewardedAd_Internal(AITJsonSettings.Serialize(args), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAdMobRewardedAd called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAdMobRewardedAd_Internal(string args, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static async Task<System.Action> GoogleAdMobShowAdMobRewardedAd(GoogleAdMobShowAdMobRewardedAdArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobShowAdMobRewardedAd_Internal(AITJsonSettings.Serialize(args), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAdMobRewardedAd called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAdMobRewardedAd_Internal(string args, string callbackId, string typeName);
#endif
        /// <summary>
        /// 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static async Task<System.Action> GoogleAdMobLoadAppsInTossAdMob(GoogleAdMobLoadAppsInTossAdMobArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobLoadAppsInTossAdMob_Internal(AITJsonSettings.Serialize(args), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMob called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAppsInTossAdMob_Internal(string args, string callbackId, string typeName);
#endif
        /// <summary>
        /// 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [Preserve]
        [APICategory("Advertising")]
        public static async Task<System.Action> GoogleAdMobShowAppsInTossAdMob(GoogleAdMobShowAppsInTossAdMobArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobShowAppsInTossAdMob_Internal(AITJsonSettings.Serialize(args), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMob called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAppsInTossAdMob_Internal(string args, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Advertising")]
        public static async Task TossAdsInitialize(InitializeOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __TossAdsInitialize_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsInitialize called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsInitialize_Internal(string options, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Advertising")]
        public static async Task TossAdsAttach(string adGroupId, string target, TossAdsAttachOptions options = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __TossAdsAttach_Internal(adGroupId, target, AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttach called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsAttach_Internal(string adGroupId, string target, string options, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Advertising")]
        public static async Task TossAdsDestroy(string slotId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __TossAdsDestroy_Internal(slotId, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroy called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsDestroy_Internal(string slotId, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Advertising")]
        public static async Task TossAdsDestroyAll()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __TossAdsDestroyAll_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroyAll called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsDestroyAll_Internal(string callbackId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Advertising")]
        public static Action LoadFullScreenAd(
            string adGroupId,
            Action<LoadFullScreenAdEvent> onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<LoadFullScreenAdEvent>(
                onEvent,
                onError
            );
            __loadFullScreenAd_Internal(adGroupId, subscriptionId, "LoadFullScreenAdEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] LoadFullScreenAd called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] LoadFullScreenAd cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __loadFullScreenAd_Internal(string adGroupId, string subscriptionId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Advertising")]
        public static Action ShowFullScreenAd(
            string adGroupId,
            Action<ShowFullScreenAdEvent> onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ShowFullScreenAdEvent>(
                onEvent,
                onError
            );
            __showFullScreenAd_Internal(adGroupId, subscriptionId, "ShowFullScreenAdEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ShowFullScreenAd called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] ShowFullScreenAd cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __showFullScreenAd_Internal(string adGroupId, string subscriptionId, string typeName);
#endif
    }
}
