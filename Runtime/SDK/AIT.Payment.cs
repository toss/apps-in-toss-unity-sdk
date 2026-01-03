// -----------------------------------------------------------------------
// <copyright file="AIT.Payment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Payment APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
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
        /// <param name="options">결제창을 띄울 때 필요한 옵션이에요.</param>
        /// <returns>인증 성공 여부를 포함한 결과를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Payment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CheckoutPaymentResult> CheckoutPayment(CheckoutPaymentOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
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
        public static async Task<CheckoutPaymentResult> CheckoutPayment(CheckoutPaymentOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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
    }
}
