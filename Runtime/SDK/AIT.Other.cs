// -----------------------------------------------------------------------
// <copyright file="AIT.Other.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Other APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Other
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Obsolete("이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요. *")]
        [APICategory("Other")]
        public static async Task<System.Action> GoogleAdMobLoadAdMobInterstitialAd(System.Action args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobLoadAdMobInterstitialAd_Internal(JsonConvert.SerializeObject(args), callbackId, "System.Action");
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
        [APICategory("Other")]
        public static async Task<System.Action> GoogleAdMobShowAdMobInterstitialAd(System.Action args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobShowAdMobInterstitialAd_Internal(JsonConvert.SerializeObject(args), callbackId, "System.Action");
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
        [APICategory("Other")]
        public static async Task<System.Action> GoogleAdMobLoadAdMobRewardedAd(System.Action args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobLoadAdMobRewardedAd_Internal(JsonConvert.SerializeObject(args), callbackId, "System.Action");
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
        [APICategory("Other")]
        public static async Task<System.Action> GoogleAdMobShowAdMobRewardedAd(System.Action args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobShowAdMobRewardedAd_Internal(JsonConvert.SerializeObject(args), callbackId, "System.Action");
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
        [APICategory("Other")]
        public static async Task<System.Action> GoogleAdMobLoadAppsInTossAdMob(System.Action args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobLoadAppsInTossAdMob_Internal(JsonConvert.SerializeObject(args), callbackId, "System.Action");
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
        [APICategory("Other")]
        public static async Task<System.Action> GoogleAdMobShowAppsInTossAdMob(System.Action args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __GoogleAdMobShowAppsInTossAdMob_Internal(JsonConvert.SerializeObject(args), callbackId, "System.Action");
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
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task<System.Action> IAPCreateOneTimePurchaseOrder(IapCreateOneTimePurchaseOrderOptions paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPCreateOneTimePurchaseOrder_Internal(JsonConvert.SerializeObject(paramsParam), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IAPCreateOneTimePurchaseOrder called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPCreateOneTimePurchaseOrder_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetProductItemList_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetPendingOrders_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __IAPGetCompletedOrRefundedOrders_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task<bool> IAPCompleteProductGrant(IAPCompleteProductGrantArgs_0 args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __IAPCompleteProductGrant_Internal(JsonConvert.SerializeObject(args_0), callbackId, "bool");
            return await tcs.Task;
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
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task<SafeAreaInsetsGetResult> SafeAreaInsetsGet()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SafeAreaInsetsGetResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsetsGetResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __SafeAreaInsetsGet_Internal(callbackId, "SafeAreaInsetsGetResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsGet called");
            await Task.CompletedTask;
            return default(SafeAreaInsetsGetResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaInsetsGet_Internal(string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task<System.Action> SafeAreaInsetsSubscribe(System.Action __0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __SafeAreaInsetsSubscribe_Internal(JsonConvert.SerializeObject(__0), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsSubscribe called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaInsetsSubscribe_Internal(string __0, string callbackId, string typeName);
#endif
        /// <summary>
        /// 모바일 앱의 로컬 저장소에서 문자열 데이터를 가져와요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task<string> StorageGetItem(string args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __StorageGetItem_Internal(args_0, callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StorageGetItem called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __StorageGetItem_Internal(string args_0, string callbackId, string typeName);
#endif
        /// <summary>
        /// 모바일 앱의 로컬 저장소에서 문자열 데이터를 가져와요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task StorageSetItem(string args_0, string args_1)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __StorageSetItem_Internal(args_0, args_1, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StorageSetItem called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __StorageSetItem_Internal(string args_0, string args_1, string callbackId, string typeName);
#endif
        /// <summary>
        /// 모바일 앱의 로컬 저장소에서 문자열 데이터를 가져와요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task StorageRemoveItem(string args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __StorageRemoveItem_Internal(args_0, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StorageRemoveItem called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __StorageRemoveItem_Internal(string args_0, string callbackId, string typeName);
#endif
        /// <summary>
        /// 모바일 앱의 로컬 저장소에서 문자열 데이터를 가져와요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task StorageClearItems()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __StorageClearItems_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StorageClearItems called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __StorageClearItems_Internal(string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task<string> envGetDeploymentId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __envGetDeploymentId_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] envGetDeploymentId called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __envGetDeploymentId_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 상단 네비게이션의 악세서리 버튼을 추가해요. callback에 대한 정의는 `tdsEvent.addEventListener("navigationAccessoryEvent", callback)`를 참고해주세요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task partnerAddAccessoryButton(AddAccessoryButtonOptions args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __partnerAddAccessoryButton_Internal(JsonConvert.SerializeObject(args_0), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] partnerAddAccessoryButton called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __partnerAddAccessoryButton_Internal(string args_0, string callbackId, string typeName);
#endif
        /// <summary>
        /// 상단 네비게이션의 악세서리 버튼을 추가해요. callback에 대한 정의는 `tdsEvent.addEventListener("navigationAccessoryEvent", callback)`를 참고해주세요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [APICategory("Other")]
        public static async Task partnerRemoveAccessoryButton()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __partnerRemoveAccessoryButton_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] partnerRemoveAccessoryButton called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __partnerRemoveAccessoryButton_Internal(string callbackId, string typeName);
#endif
    }
}
