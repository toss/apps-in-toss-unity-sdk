// -----------------------------------------------------------------------
// <copyright file="AIT.CheckoutPayment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - CheckoutPayment API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - CheckoutPayment
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">결제창을 띄울 때 필요한 옵션이에요.</param>
        /// <returns>인증 성공 여부를 포함한 결과를 반환해요.</returns>
        public static void CheckoutPayment(CheckoutPaymentOptions options, Action<CheckoutPaymentResult> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __checkoutPayment_Internal(options, callbackId, "CheckoutPaymentResult");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] CheckoutPayment called");
            var mockResult = default(CheckoutPaymentResult);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __checkoutPayment_Internal(CheckoutPaymentOptions options, string callbackId, string typeName);
#endif
    }
}
