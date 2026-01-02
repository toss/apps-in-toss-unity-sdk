// -----------------------------------------------------------------------
// <copyright file="AIT.IAP.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - IAP APIs
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
    /// Apps in Toss Platform API - IAP
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="options">옵션</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("IAP")]
        public static Action IAPCreateOneTimePurchaseOrder(
            Action<SuccessEvent> onEvent,
            IapCreateOneTimePurchaseOrderOptionsOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = Guid.NewGuid().ToString();

            // 중첩 콜백 등록
            if (options?.ProcessProductGrant != null)
            {
                AITCore.Instance.RegisterNestedCallback(
                    subscriptionId,
                    "processProductGrant",
                    options.ProcessProductGrant
                );
            }

            // 이벤트 콜백 등록
            AITCore.Instance.RegisterSubscriptionCallback<SuccessEvent>(
                subscriptionId,
                onEvent,
                onError
            );

            __IAPCreateOneTimePurchaseOrder_Internal(AITJsonSettings.Serialize(options), subscriptionId, "SuccessEvent");

            return () => {
                AITCore.Instance.Unsubscribe(subscriptionId);
                AITCore.Instance.RemoveNestedCallbacks(subscriptionId);
            };
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateOneTimePurchaseOrder called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] IAPCreateOneTimePurchaseOrder cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCreateOneTimePurchaseOrder_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IAPGetProductItemListResult> IAPGetProductItemList()
#else
        public static async Task<IAPGetProductItemListResult> IAPGetProductItemList()
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<IAPGetProductItemListResult>();
#else
            var tcs = new TaskCompletionSource<IAPGetProductItemListResult>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetProductItemListResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPGetProductItemList_Internal(callbackId, "IAPGetProductItemListResult");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetProductItemList called");
            await Task.CompletedTask;
            return default(IAPGetProductItemListResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetProductItemList_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IAPGetPendingOrdersResult> IAPGetPendingOrders()
#else
        public static async Task<IAPGetPendingOrdersResult> IAPGetPendingOrders()
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<IAPGetPendingOrdersResult>();
#else
            var tcs = new TaskCompletionSource<IAPGetPendingOrdersResult>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetPendingOrdersResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPGetPendingOrders_Internal(callbackId, "IAPGetPendingOrdersResult");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetPendingOrders called");
            await Task.CompletedTask;
            return default(IAPGetPendingOrdersResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetPendingOrders_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders()
#else
        public static async Task<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders()
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<CompletedOrRefundedOrdersResult>();
#else
            var tcs = new TaskCompletionSource<CompletedOrRefundedOrdersResult>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<CompletedOrRefundedOrdersResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPGetCompletedOrRefundedOrders_Internal(callbackId, "CompletedOrRefundedOrdersResult");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrders called");
            await Task.CompletedTask;
            return default(CompletedOrRefundedOrdersResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetCompletedOrRefundedOrders_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args_0)
#else
        public static async Task<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args_0)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPCompleteProductGrant_Internal(AITJsonSettings.Serialize(args_0), callbackId, "bool");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCompleteProductGrant called");
            await Task.CompletedTask;
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCompleteProductGrant_Internal(string args_0, string callbackId, string typeName);
#endif
    }
}
