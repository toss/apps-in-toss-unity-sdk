// -----------------------------------------------------------------------
// <copyright file="AIT.IAP.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - IAP APIs
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
    /// Apps in Toss Platform API - IAP
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 단건 인앱결제 주문서 페이지로 이동해요.
        /// 반환되는 cleanup 함수는 결제 흐름이 끝나면 반드시 호출해야 해요.
        /// 상품 식별자는 `sku`를 사용하세요. (`productId`는 deprecated)
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
        /// 현재 앱 버전과 환경에서 `IAP.createOneTimePurchaseOrder`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPCreateOneTimePurchaseOrderIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPCreateOneTimePurchaseOrderIsSupported"
            );
            __IAPCreateOneTimePurchaseOrderIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateOneTimePurchaseOrderIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPCreateOneTimePurchaseOrderIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPCreateOneTimePurchaseOrderIsSupported"
            );
            __IAPCreateOneTimePurchaseOrderIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateOneTimePurchaseOrderIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCreateOneTimePurchaseOrderIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 구독 인앱결제 주문서 페이지로 이동해요.
        /// 반환되는 cleanup 함수는 결제 흐름이 끝나면 반드시 호출해야 해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="options">옵션</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("IAP")]
        public static Action IAPCreateSubscriptionPurchaseOrder(
            Action<SubscriptionSuccessEvent> onEvent,
            CreateSubscriptionPurchaseOrderOptionsOptions options,
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
            AITCore.Instance.RegisterSubscriptionCallback<SubscriptionSuccessEvent>(
                subscriptionId,
                onEvent,
                onError
            );

            __IAPCreateSubscriptionPurchaseOrder_Internal(AITJsonSettings.Serialize(options), subscriptionId, "SubscriptionSuccessEvent");

            return () => {
                AITCore.Instance.Unsubscribe(subscriptionId);
                AITCore.Instance.RemoveNestedCallbacks(subscriptionId);
            };
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateSubscriptionPurchaseOrder called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] IAPCreateSubscriptionPurchaseOrder cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCreateSubscriptionPurchaseOrder_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `IAP.createSubscriptionPurchaseOrder`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPCreateSubscriptionPurchaseOrderIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPCreateSubscriptionPurchaseOrderIsSupported"
            );
            __IAPCreateSubscriptionPurchaseOrderIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateSubscriptionPurchaseOrderIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPCreateSubscriptionPurchaseOrderIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPCreateSubscriptionPurchaseOrderIsSupported"
            );
            __IAPCreateSubscriptionPurchaseOrderIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateSubscriptionPurchaseOrderIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCreateSubscriptionPurchaseOrderIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 구매 가능한 상품 목록을 가져와요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IAPGetProductItemListResult> IAPGetProductItemList(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<IAPGetProductItemListResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetProductItemListResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetProductItemList"
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
        public static async Task<IAPGetProductItemListResult> IAPGetProductItemList(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<IAPGetProductItemListResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetProductItemListResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetProductItemList"
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
        /// 현재 앱 버전과 환경에서 `IAP.getProductItemList`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPGetProductItemListIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetProductItemListIsSupported"
            );
            __IAPGetProductItemListIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetProductItemListIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPGetProductItemListIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetProductItemListIsSupported"
            );
            __IAPGetProductItemListIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetProductItemListIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetProductItemListIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 결제는 완료됐지만 상품이 아직 지급되지 않은 주문 목록을 가져와요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IAPGetPendingOrdersResult> IAPGetPendingOrders(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<IAPGetPendingOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetPendingOrdersResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetPendingOrders"
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
        public static async Task<IAPGetPendingOrdersResult> IAPGetPendingOrders(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<IAPGetPendingOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<IAPGetPendingOrdersResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetPendingOrders"
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
        /// 현재 앱 버전과 환경에서 `IAP.getPendingOrders`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPGetPendingOrdersIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetPendingOrdersIsSupported"
            );
            __IAPGetPendingOrdersIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetPendingOrdersIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPGetPendingOrdersIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetPendingOrdersIsSupported"
            );
            __IAPGetPendingOrdersIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetPendingOrdersIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetPendingOrdersIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 완료·환불된 주문 목록을 가져와요. `hasNext` / `nextKey`로 페이지네이션해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders(IAPGetCompletedOrRefundedOrdersParams paramsParam = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<CompletedOrRefundedOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CompletedOrRefundedOrdersResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetCompletedOrRefundedOrders"
            );
            __IAPGetCompletedOrRefundedOrders_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "CompletedOrRefundedOrdersResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrders called");
            await Awaitable.NextFrameAsync();
            return default(CompletedOrRefundedOrdersResult);
#endif
        }
#else
        public static async Task<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders(IAPGetCompletedOrRefundedOrdersParams paramsParam = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<CompletedOrRefundedOrdersResult>();
            string callbackId = AITCore.Instance.RegisterCallback<CompletedOrRefundedOrdersResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetCompletedOrRefundedOrders"
            );
            __IAPGetCompletedOrRefundedOrders_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "CompletedOrRefundedOrdersResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrders called");
            await Task.CompletedTask;
            return default(CompletedOrRefundedOrdersResult);
#endif
        }
#endif

        /// <summary>
        /// 이전 버전 호출 <c>IAPGetCompletedOrRefundedOrders(timeoutMs)</c>를 그대로 컴파일하기 위한 오버로드다.
        /// 새 코드는 <c>IAPGetCompletedOrRefundedOrders(null, timeoutMs)</c> 형태를 쓴다.
        /// </summary>
        [Preserve]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#if UNITY_6000_0_OR_NEWER
        public static Awaitable<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders(int timeoutMs)
            => IAPGetCompletedOrRefundedOrders(null, timeoutMs);
#else
        public static Task<CompletedOrRefundedOrdersResult> IAPGetCompletedOrRefundedOrders(int timeoutMs)
            => IAPGetCompletedOrRefundedOrders(null, timeoutMs);
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetCompletedOrRefundedOrders_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `IAP.getCompletedOrRefundedOrders`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPGetCompletedOrRefundedOrdersIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetCompletedOrRefundedOrdersIsSupported"
            );
            __IAPGetCompletedOrRefundedOrdersIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrdersIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPGetCompletedOrRefundedOrdersIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetCompletedOrRefundedOrdersIsSupported"
            );
            __IAPGetCompletedOrRefundedOrdersIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetCompletedOrRefundedOrdersIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetCompletedOrRefundedOrdersIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 상품 지급 완료를 앱에 알려요. `{ params: { orderId } }` 형태이며, 지급 완료 여부(boolean)를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPCompleteProductGrant"
            );
            __IAPCompleteProductGrant_Internal(AITJsonSettings.Serialize(args), callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPCompleteProductGrant called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPCompleteProductGrant"
            );
            __IAPCompleteProductGrant_Internal(AITJsonSettings.Serialize(args), callbackId, "bool");
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
        private static extern void __IAPCompleteProductGrant_Internal(string args, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `IAP.completeProductGrant`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPCompleteProductGrantIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPCompleteProductGrantIsSupported"
            );
            __IAPCompleteProductGrantIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPCompleteProductGrantIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPCompleteProductGrantIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPCompleteProductGrantIsSupported"
            );
            __IAPCompleteProductGrantIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCompleteProductGrantIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCompleteProductGrantIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 구독 주문의 현재 상태 정보를 가져와요. `{ params: { orderId } }` 형태예요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<IapSubscriptionInfoResponse> IAPGetSubscriptionInfo(IAPGetSubscriptionInfoArgs_0 args, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<IapSubscriptionInfoResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<IapSubscriptionInfoResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetSubscriptionInfo"
            );
            __IAPGetSubscriptionInfo_Internal(AITJsonSettings.Serialize(args), callbackId, "IapSubscriptionInfoResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetSubscriptionInfo called");
            await Awaitable.NextFrameAsync();
            return default(IapSubscriptionInfoResponse);
#endif
        }
#else
        public static async Task<IapSubscriptionInfoResponse> IAPGetSubscriptionInfo(IAPGetSubscriptionInfoArgs_0 args, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<IapSubscriptionInfoResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<IapSubscriptionInfoResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetSubscriptionInfo"
            );
            __IAPGetSubscriptionInfo_Internal(AITJsonSettings.Serialize(args), callbackId, "IapSubscriptionInfoResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetSubscriptionInfo called");
            await Task.CompletedTask;
            return default(IapSubscriptionInfoResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetSubscriptionInfo_Internal(string args, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `IAP.getSubscriptionInfo`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("IAP")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IAPGetSubscriptionInfoIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IAPGetSubscriptionInfoIsSupported"
            );
            __IAPGetSubscriptionInfoIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetSubscriptionInfoIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IAPGetSubscriptionInfoIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IAPGetSubscriptionInfoIsSupported"
            );
            __IAPGetSubscriptionInfoIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPGetSubscriptionInfoIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetSubscriptionInfoIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
