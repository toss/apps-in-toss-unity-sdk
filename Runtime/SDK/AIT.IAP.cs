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
        /// 인앱결제로 구매할 수 있는 상품 목록을 가져와요. 상품 목록 화면에 진입할 때 호출해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IAPGetProductItemListResult> IAPGetProductItemList()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<IAPGetProductItemListResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetProductItemListResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __IAPGetProductItemList_Internal(callbackId, "IAPGetProductItemListResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetProductItemList called");
            await Awaitable.NextFrameAsync();
            return default(IAPGetProductItemListResult);
#endif
        }
#else
        public static async Task<IAPGetProductItemListResult> IAPGetProductItemList()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<IAPGetProductItemListResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetProductItemListResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPGetProductItemList_Internal(callbackId, "IAPGetProductItemListResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetProductItemList called");
            await Task.CompletedTask;
            return default(IAPGetProductItemListResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetProductItemList_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 대기 중인 주문 목록을 가져와요. 이 함수를 사용하면 결제가 아직 완료되지 않은 주문 정보를 확인할 수 있어요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IAPGetPendingOrdersResult> IAPGetPendingOrders()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<IAPGetPendingOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetPendingOrdersResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __IAPGetPendingOrders_Internal(callbackId, "IAPGetPendingOrdersResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetPendingOrders called");
            await Awaitable.NextFrameAsync();
            return default(IAPGetPendingOrdersResult);
#endif
        }
#else
        public static async Task<IAPGetPendingOrdersResult> IAPGetPendingOrders()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<IAPGetPendingOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetPendingOrdersResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPGetPendingOrders_Internal(callbackId, "IAPGetPendingOrdersResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetPendingOrders called");
            await Task.CompletedTask;
            return default(IAPGetPendingOrdersResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetPendingOrders_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 인앱결제로 구매하거나 환불한 주문 목록을 가져와요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<CompletedOrRefundedOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CompletedOrRefundedOrdersResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __IAPGetCompletedOrRefundedOrders_Internal(callbackId, "CompletedOrRefundedOrdersResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrders called");
            await Awaitable.NextFrameAsync();
            return default(CompletedOrRefundedOrdersResult);
#endif
        }
#else
        public static async Task<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CompletedOrRefundedOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CompletedOrRefundedOrdersResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPGetCompletedOrRefundedOrders_Internal(callbackId, "CompletedOrRefundedOrdersResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrders called");
            await Task.CompletedTask;
            return default(CompletedOrRefundedOrdersResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetCompletedOrRefundedOrders_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 상품 지급 처리를 완료했다는 메시지를 앱에 전달해요. 이 함수를 사용하면 결제가 완료된 주문의 상품 지급이 정상적으로 완료되었음을 알릴 수 있어요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __IAPCompleteProductGrant_Internal(AITJsonSettings.Serialize(args_0), callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPCompleteProductGrant called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPCompleteProductGrant_Internal(AITJsonSettings.Serialize(args_0), callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCompleteProductGrant called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCompleteProductGrant_Internal(string args_0, string callbackId, string typeName);
#endif
    }
}
