// -----------------------------------------------------------------------
// <copyright file="AIT.Payment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Payment APIs
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
    /// Apps in Toss Platform API - Payment
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 토스페이 결제창을 띄우고 사용자 인증을 수행해요.
        /// 결제창을 통해 사용자 인증만 하며, 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요.
        /// `payToken`이 필요하며 `{ success, reason? }`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CheckoutPaymentResult> TossPayAuthorize(CheckoutPaymentParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossPayAuthorize"
            );
            __TossPayAuthorize_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "CheckoutPaymentResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossPayAuthorize called");
            await Awaitable.NextFrameAsync();
            return default(CheckoutPaymentResult);
#endif
        }
#else
        public static async Task<CheckoutPaymentResult> TossPayAuthorize(CheckoutPaymentParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossPayAuthorize"
            );
            __TossPayAuthorize_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "CheckoutPaymentResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossPayAuthorize called");
            await Task.CompletedTask;
            return default(CheckoutPaymentResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossPayAuthorize_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 토스페이 정기결제창을 띄우고 사용자 인증을 수행해요.
        /// 결제창을 통해 사용자 인증만 하며, 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요.
        /// `wrappedToken`이 필요하며 `{ success, reason? }`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<RequestTossPayPaysBillingResult> TossPayAuthorizeSubscription(RequestTossPayPaysBillingParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<RequestTossPayPaysBillingResult>();
            string callbackId = AITCore.Instance.RegisterCallback<RequestTossPayPaysBillingResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossPayAuthorizeSubscription"
            );
            __TossPayAuthorizeSubscription_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "RequestTossPayPaysBillingResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossPayAuthorizeSubscription called");
            await Awaitable.NextFrameAsync();
            return default(RequestTossPayPaysBillingResult);
#endif
        }
#else
        public static async Task<RequestTossPayPaysBillingResult> TossPayAuthorizeSubscription(RequestTossPayPaysBillingParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<RequestTossPayPaysBillingResult>();
            string callbackId = AITCore.Instance.RegisterCallback<RequestTossPayPaysBillingResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossPayAuthorizeSubscription"
            );
            __TossPayAuthorizeSubscription_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "RequestTossPayPaysBillingResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossPayAuthorizeSubscription called");
            await Task.CompletedTask;
            return default(RequestTossPayPaysBillingResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossPayAuthorizeSubscription_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossPay.authorizeSubscription`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossPayAuthorizeSubscriptionIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossPayAuthorizeSubscriptionIsSupported"
            );
            __TossPayAuthorizeSubscriptionIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossPayAuthorizeSubscriptionIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossPayAuthorizeSubscriptionIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossPayAuthorizeSubscriptionIsSupported"
            );
            __TossPayAuthorizeSubscriptionIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossPayAuthorizeSubscriptionIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossPayAuthorizeSubscriptionIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossPayAuthorize")]
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CheckoutPaymentResult> TossPayCheckoutPayment(TossPayCheckoutPaymentOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossPayCheckoutPayment"
            );
            __TossPayCheckoutPayment_Internal(AITJsonSettings.Serialize(options), callbackId, "CheckoutPaymentResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossPayCheckoutPayment called");
            await Awaitable.NextFrameAsync();
            return default(CheckoutPaymentResult);
#endif
        }
#else
        public static async Task<CheckoutPaymentResult> TossPayCheckoutPayment(TossPayCheckoutPaymentOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossPayCheckoutPayment"
            );
            __TossPayCheckoutPayment_Internal(AITJsonSettings.Serialize(options), callbackId, "CheckoutPaymentResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossPayCheckoutPayment called");
            await Task.CompletedTask;
            return default(CheckoutPaymentResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossPayCheckoutPayment_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>결과 값 또는 null</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossPayAuthorizeSubscription")]
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<RequestTossPayPaysBillingResult> TossPayRequestTossPayPaysBilling(TossPayRequestTossPayPaysBillingOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<RequestTossPayPaysBillingResult>();
            string callbackId = AITCore.Instance.RegisterCallback<RequestTossPayPaysBillingResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossPayRequestTossPayPaysBilling"
            );
            __TossPayRequestTossPayPaysBilling_Internal(AITJsonSettings.Serialize(options), callbackId, "RequestTossPayPaysBillingResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossPayRequestTossPayPaysBilling called");
            await Awaitable.NextFrameAsync();
            return default(RequestTossPayPaysBillingResult);
#endif
        }
#else
        public static async Task<RequestTossPayPaysBillingResult> TossPayRequestTossPayPaysBilling(TossPayRequestTossPayPaysBillingOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<RequestTossPayPaysBillingResult>();
            string callbackId = AITCore.Instance.RegisterCallback<RequestTossPayPaysBillingResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossPayRequestTossPayPaysBilling"
            );
            __TossPayRequestTossPayPaysBilling_Internal(AITJsonSettings.Serialize(options), callbackId, "RequestTossPayPaysBillingResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossPayRequestTossPayPaysBilling called");
            await Task.CompletedTask;
            return default(RequestTossPayPaysBillingResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossPayRequestTossPayPaysBilling_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossPay.requestTossPayPaysBilling`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossPayAuthorizeSubscriptionIsSupported")]
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossPayRequestTossPayPaysBillingIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossPayRequestTossPayPaysBillingIsSupported"
            );
            __TossPayRequestTossPayPaysBillingIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossPayRequestTossPayPaysBillingIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossPayRequestTossPayPaysBillingIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossPayRequestTossPayPaysBillingIsSupported"
            );
            __TossPayRequestTossPayPaysBillingIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossPayRequestTossPayPaysBillingIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossPayRequestTossPayPaysBillingIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 토스페이 결제창을 띄우고 사용자 인증을 수행해요.
        /// 결제창을 통해 사용자 인증만 하며, 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요.
        /// `payToken`이 필요하며 `{ success, reason? }`를 반환해요.
        /// </summary>
        /// <param name="options">결제창을 띄울 때 필요한 옵션이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>인증 성공 여부를 포함한 결과를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossPayAuthorize")]
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CheckoutPaymentResult> CheckoutPayment(CheckoutPaymentOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "CheckoutPayment"
            );
            __checkoutPayment_Internal(AITJsonSettings.Serialize(options), callbackId, "CheckoutPaymentResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] CheckoutPayment called");
            await Awaitable.NextFrameAsync();
            return default(CheckoutPaymentResult);
#endif
        }
#else
        public static async Task<CheckoutPaymentResult> CheckoutPayment(CheckoutPaymentOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "CheckoutPayment"
            );
            __checkoutPayment_Internal(AITJsonSettings.Serialize(options), callbackId, "CheckoutPaymentResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] CheckoutPayment called");
            await Task.CompletedTask;
            return default(CheckoutPaymentResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __checkoutPayment_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 토스페이 정기결제창을 띄우고 사용자 인증을 수행해요.
        /// 결제창을 통해 사용자 인증만 하며, 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요.
        /// 미지원 버전이면 경고 로그 후 `undefined`를 반환해요. `isSupported()`로 지원 여부를 확인할 수 있어요.
        /// </summary>
        /// <param name="options">정기결제창을 띄울 때 필요한 옵션이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>인증 성공 여부를 포함한 결과를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossPayAuthorizeSubscription")]
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<RequestTossPayPaysBillingResult> RequestTossPayPaysBilling(RequestTossPayPaysBillingOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<RequestTossPayPaysBillingResult>();
            string callbackId = AITCore.Instance.RegisterCallback<RequestTossPayPaysBillingResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "RequestTossPayPaysBilling"
            );
            __requestTossPayPaysBilling_Internal(AITJsonSettings.Serialize(options), callbackId, "RequestTossPayPaysBillingResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] RequestTossPayPaysBilling called");
            await Awaitable.NextFrameAsync();
            return default(RequestTossPayPaysBillingResult);
#endif
        }
#else
        public static async Task<RequestTossPayPaysBillingResult> RequestTossPayPaysBilling(RequestTossPayPaysBillingOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<RequestTossPayPaysBillingResult>();
            string callbackId = AITCore.Instance.RegisterCallback<RequestTossPayPaysBillingResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "RequestTossPayPaysBilling"
            );
            __requestTossPayPaysBilling_Internal(AITJsonSettings.Serialize(options), callbackId, "RequestTossPayPaysBillingResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestTossPayPaysBilling called");
            await Task.CompletedTask;
            return default(RequestTossPayPaysBillingResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestTossPayPaysBilling_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `requestTossPayPaysBilling`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossPayAuthorizeSubscriptionIsSupported")]
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> RequestTossPayPaysBillingIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "RequestTossPayPaysBillingIsSupported"
            );
            __requestTossPayPaysBillingIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] RequestTossPayPaysBillingIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> RequestTossPayPaysBillingIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "RequestTossPayPaysBillingIsSupported"
            );
            __requestTossPayPaysBillingIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestTossPayPaysBillingIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestTossPayPaysBillingIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
