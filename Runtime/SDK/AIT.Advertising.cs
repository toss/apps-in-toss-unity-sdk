// -----------------------------------------------------------------------
// <copyright file="AIT.Advertising.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Advertising APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Advertising
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
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
        /// 현재 앱 버전과 환경에서 `GoogleAdMob.loadAppsInTossAdMob`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GoogleAdMobLoadAppsInTossAdMobIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GoogleAdMobLoadAppsInTossAdMobIsSupported"
            );
            __GoogleAdMobLoadAppsInTossAdMobIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMobIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GoogleAdMobLoadAppsInTossAdMobIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GoogleAdMobLoadAppsInTossAdMobIsSupported"
            );
            __GoogleAdMobLoadAppsInTossAdMobIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMobIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAppsInTossAdMobIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 광고를 사용자에게 노출해요. 이 함수는 `loadAppsInTossAdMob` 로 미리 불러온 광고를 실제로 사용자에게 노출해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
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
        /// <summary>
        /// 현재 앱 버전과 환경에서 `GoogleAdMob.showAppsInTossAdMob`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GoogleAdMobShowAppsInTossAdMobIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GoogleAdMobShowAppsInTossAdMobIsSupported"
            );
            __GoogleAdMobShowAppsInTossAdMobIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMobIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GoogleAdMobShowAppsInTossAdMobIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GoogleAdMobShowAppsInTossAdMobIsSupported"
            );
            __GoogleAdMobShowAppsInTossAdMobIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMobIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAppsInTossAdMobIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 이 함수는 `loadAppsInTossAdMob` 로 광고가 잘 불러와졌는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GoogleAdMobIsAppsInTossAdMobLoaded(IsAdMobLoadedOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GoogleAdMobIsAppsInTossAdMobLoaded"
            );
            __GoogleAdMobIsAppsInTossAdMobLoaded_Internal(AITJsonSettings.Serialize(options), callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobIsAppsInTossAdMobLoaded called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GoogleAdMobIsAppsInTossAdMobLoaded(IsAdMobLoadedOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GoogleAdMobIsAppsInTossAdMobLoaded"
            );
            __GoogleAdMobIsAppsInTossAdMobLoaded_Internal(AITJsonSettings.Serialize(options), callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobIsAppsInTossAdMobLoaded called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobIsAppsInTossAdMobLoaded_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `GoogleAdMob.isAppsInTossAdMobLoaded`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GoogleAdMobIsAppsInTossAdMobLoadedIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GoogleAdMobIsAppsInTossAdMobLoadedIsSupported"
            );
            __GoogleAdMobIsAppsInTossAdMobLoadedIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobIsAppsInTossAdMobLoadedIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GoogleAdMobIsAppsInTossAdMobLoadedIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GoogleAdMobIsAppsInTossAdMobLoadedIsSupported"
            );
            __GoogleAdMobIsAppsInTossAdMobLoadedIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobIsAppsInTossAdMobLoadedIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobIsAppsInTossAdMobLoadedIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable TossAdsInitialize(InitializeOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = __TossAdsInitialize_Subscribe(options);
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error =>
                {
                    AITCore.Instance.Unsubscribe(subscriptionId);
                    acs.SetException(error);
                },
                timeoutMs,
                "TossAdsInitialize"
            );
            __TossAdsInitialize_Internal(AITJsonSettings.Serialize(options), subscriptionId, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsInitialize called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task TossAdsInitialize(InitializeOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = __TossAdsInitialize_Subscribe(options);
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error =>
                {
                    AITCore.Instance.Unsubscribe(subscriptionId);
                    tcs.TrySetException(error);
                },
                timeoutMs,
                "TossAdsInitialize"
            );
            __TossAdsInitialize_Internal(AITJsonSettings.Serialize(options), subscriptionId, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsInitialize called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>options.Callbacks의 콜백을 JS 이벤트(AITCallbackBagEvent)에 연결하고 구독 ID를 돌려준다.</summary>
        private static string __TossAdsInitialize_Subscribe(InitializeOptions options)
        {
            var bagCallbacks = options?.Callbacks;
            return AITCore.Instance.RegisterSubscriptionCallback<AITCallbackBagEvent>(
                evt =>
                {
                    if (bagCallbacks == null || evt == null) return;
                    switch (evt.Event)
                    {
                        case "onInitialized":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnInitialized);
                            break;
                        case "onInitializationFailed":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnInitializationFailed, evt.Payload);
                            break;
                    }
                },
                null
            );
        }

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsInitialize_Internal(string options, string subscriptionId, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAds.initialize`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAdsInitializeIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsInitializeIsSupported"
            );
            __TossAdsInitializeIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsInitializeIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAdsInitializeIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsInitializeIsSupported"
            );
            __TossAdsInitializeIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsInitializeIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsInitializeIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("attach는 더 이상 권장되지 않습니다. attachBanner를 사용해주세요.")]
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable TossAdsAttach(string adGroupId, string target, TossAdsAttachOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = __TossAdsAttach_Subscribe(options);
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error =>
                {
                    AITCore.Instance.Unsubscribe(subscriptionId);
                    acs.SetException(error);
                },
                timeoutMs,
                "TossAdsAttach"
            );
            __TossAdsAttach_Internal(adGroupId, target, AITJsonSettings.Serialize(options), subscriptionId, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttach called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task TossAdsAttach(string adGroupId, string target, TossAdsAttachOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = __TossAdsAttach_Subscribe(options);
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error =>
                {
                    AITCore.Instance.Unsubscribe(subscriptionId);
                    tcs.TrySetException(error);
                },
                timeoutMs,
                "TossAdsAttach"
            );
            __TossAdsAttach_Internal(adGroupId, target, AITJsonSettings.Serialize(options), subscriptionId, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttach called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>options.Callbacks의 콜백을 JS 이벤트(AITCallbackBagEvent)에 연결하고 구독 ID를 돌려준다.</summary>
        private static string __TossAdsAttach_Subscribe(TossAdsAttachOptions options)
        {
            var bagCallbacks = options?.Callbacks;
            return AITCore.Instance.RegisterSubscriptionCallback<AITCallbackBagEvent>(
                evt =>
                {
                    if (bagCallbacks == null || evt == null) return;
                    switch (evt.Event)
                    {
                        case "onAdRendered":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdRendered, evt.Payload);
                            break;
                        case "onAdViewable":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdViewable, evt.Payload);
                            break;
                        case "onAdClicked":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdClicked, evt.Payload);
                            break;
                        case "onAdImpression":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdImpression, evt.Payload);
                            break;
                        case "onAdFailedToRender":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdFailedToRender, evt.Payload);
                            break;
                        case "onNoFill":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnNoFill, evt.Payload);
                            break;
                    }
                },
                null
            );
        }

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsAttach_Internal(string adGroupId, string target, string options, string subscriptionId, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAds.attach`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossAdsAttachBannerIsSupported")]
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAdsAttachIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsAttachIsSupported"
            );
            __TossAdsAttachIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAdsAttachIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsAttachIsSupported"
            );
            __TossAdsAttachIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsAttachIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>Destroy를 부르면 붙여 둔 JS 쪽 자원과 콜백 구독을 함께 해제한다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("CSS 셀렉터 기반 attachBanner는 더 이상 권장되지 않습니다. AITBannerAdView 컴포넌트 또는 AITBannerAd.Show를 사용해주세요.")]
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AttachBannerResult> TossAdsAttachBanner(string adGroupId, string target, TossAdsAttachBannerOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = __TossAdsAttachBanner_Subscribe(options);
            var acs = new AwaitableCompletionSource<AttachBannerResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AttachBannerResult>(
                result =>
                {
                    var handle = result ?? new AttachBannerResult();
                    handle.Destroy = () => AITCore.Instance.Unsubscribe(subscriptionId);
                    acs.SetResult(handle);
                },
                error =>
                {
                    AITCore.Instance.Unsubscribe(subscriptionId);
                    acs.SetException(error);
                },
                timeoutMs,
                "TossAdsAttachBanner"
            );
            __TossAdsAttachBanner_Internal(adGroupId, target, AITJsonSettings.Serialize(options), subscriptionId, callbackId, "AttachBannerResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachBanner called");
            await Awaitable.NextFrameAsync();
            return new AttachBannerResult { Destroy = () => UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachBanner destroyed") };
#endif
        }
#else
        public static async Task<AttachBannerResult> TossAdsAttachBanner(string adGroupId, string target, TossAdsAttachBannerOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = __TossAdsAttachBanner_Subscribe(options);
            var tcs = new TaskCompletionSource<AttachBannerResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AttachBannerResult>(
                result =>
                {
                    var handle = result ?? new AttachBannerResult();
                    handle.Destroy = () => AITCore.Instance.Unsubscribe(subscriptionId);
                    tcs.TrySetResult(handle);
                },
                error =>
                {
                    AITCore.Instance.Unsubscribe(subscriptionId);
                    tcs.TrySetException(error);
                },
                timeoutMs,
                "TossAdsAttachBanner"
            );
            __TossAdsAttachBanner_Internal(adGroupId, target, AITJsonSettings.Serialize(options), subscriptionId, callbackId, "AttachBannerResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachBanner called");
            await Task.CompletedTask;
            return new AttachBannerResult { Destroy = () => UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachBanner destroyed") };
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>options.Callbacks의 콜백을 JS 이벤트(AITCallbackBagEvent)에 연결하고 구독 ID를 돌려준다.</summary>
        private static string __TossAdsAttachBanner_Subscribe(TossAdsAttachBannerOptions options)
        {
            var bagCallbacks = options?.Callbacks;
            return AITCore.Instance.RegisterSubscriptionCallback<AITCallbackBagEvent>(
                evt =>
                {
                    if (bagCallbacks == null || evt == null) return;
                    switch (evt.Event)
                    {
                        case "onAdRendered":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdRendered, evt.Payload);
                            break;
                        case "onAdViewable":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdViewable, evt.Payload);
                            break;
                        case "onAdClicked":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdClicked, evt.Payload);
                            break;
                        case "onAdImpression":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdImpression, evt.Payload);
                            break;
                        case "onAdFailedToRender":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnAdFailedToRender, evt.Payload);
                            break;
                        case "onNoFill":
                            AITCore.InvokeCallbackBag(bagCallbacks.OnNoFill, evt.Payload);
                            break;
                    }
                },
                null
            );
        }

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsAttachBanner_Internal(string adGroupId, string target, string options, string subscriptionId, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAds.attachBanner`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAdsAttachBannerIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsAttachBannerIsSupported"
            );
            __TossAdsAttachBannerIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachBannerIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAdsAttachBannerIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsAttachBannerIsSupported"
            );
            __TossAdsAttachBannerIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsAttachBannerIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsAttachBannerIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable TossAdsDestroy(string slotId, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsDestroy"
            );
            __TossAdsDestroy_Internal(slotId, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroy called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task TossAdsDestroy(string slotId, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsDestroy"
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
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsDestroy_Internal(string slotId, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAds.destroy`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAdsDestroyIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsDestroyIsSupported"
            );
            __TossAdsDestroyIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroyIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAdsDestroyIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsDestroyIsSupported"
            );
            __TossAdsDestroyIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroyIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsDestroyIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable TossAdsDestroyAll(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsDestroyAll"
            );
            __TossAdsDestroyAll_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroyAll called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task TossAdsDestroyAll(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsDestroyAll"
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
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsDestroyAll_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAds.destroyAll`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAdsDestroyAllIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAdsDestroyAllIsSupported"
            );
            __TossAdsDestroyAllIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroyAllIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAdsDestroyAllIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAdsDestroyAllIsSupported"
            );
            __TossAdsDestroyAllIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAdsDestroyAllIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAdsDestroyAllIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 전면 광고를 미리 불러와서, 보여줄 시점에 바로 노출할 수 있도록 준비해요.
        /// `isSupported()`로 지원 여부를 확인할 수 있어요.
        /// </summary>
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
        /// <summary>
        /// 현재 앱 버전과 환경에서 `loadFullScreenAd`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> LoadFullScreenAdIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "LoadFullScreenAdIsSupported"
            );
            __loadFullScreenAdIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] LoadFullScreenAdIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> LoadFullScreenAdIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "LoadFullScreenAdIsSupported"
            );
            __loadFullScreenAdIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] LoadFullScreenAdIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __loadFullScreenAdIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 미리 불러온 전면 광고를 사용자에게 노출해요.
        /// `isSupported()`로 지원 여부를 확인할 수 있어요.
        /// </summary>
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
        /// <summary>
        /// 현재 앱 버전과 환경에서 `showFullScreenAd`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Advertising")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> ShowFullScreenAdIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ShowFullScreenAdIsSupported"
            );
            __showFullScreenAdIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ShowFullScreenAdIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> ShowFullScreenAdIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ShowFullScreenAdIsSupported"
            );
            __showFullScreenAdIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ShowFullScreenAdIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __showFullScreenAdIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
