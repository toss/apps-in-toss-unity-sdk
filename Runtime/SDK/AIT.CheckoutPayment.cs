// -----------------------------------------------------------------------
// <copyright file="AIT.CheckoutPayment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - CheckoutPayment API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - CheckoutPayment
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">결제창을 띄울 때 필요한 옵션이에요.</param>
        /// <returns>인증 성공 여부를 포함한 결과를 반환해요.</returns>
        public static Task<CheckoutPaymentResult> CheckoutPayment(CheckoutPaymentOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CheckoutPaymentResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CheckoutPaymentResult>(result => tcs.SetResult(result));
            __checkoutPayment_Internal(options, callbackId, "CheckoutPaymentResult");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] CheckoutPayment called");
            return Task.FromResult(default(CheckoutPaymentResult));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __checkoutPayment_Internal(CheckoutPaymentOptions options, string callbackId, string typeName);
#endif
    }
}
