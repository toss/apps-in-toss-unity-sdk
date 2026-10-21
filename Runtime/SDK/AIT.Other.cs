// -----------------------------------------------------------------------
// <copyright file="AIT.Other.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Other APIs
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
    /// Apps in Toss Platform API - Other
    /// </summary>
    public static partial class AIT
    {
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>사용자 키 조회 결과를 반환해요. GetAnonymousKeyResponse: 사용자 키 조회에 성공했어요. { type: 'HASH', hash: string } 형태로 반환돼요. 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GetAnonymousKeyResult> GetAnonymousKey(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GetAnonymousKeyResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetAnonymousKeyResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetAnonymousKey"
            );
            __getAnonymousKey_Internal(callbackId, "GetAnonymousKeyResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetAnonymousKey called");
            await Awaitable.NextFrameAsync();
            return default(GetAnonymousKeyResult);
#endif
        }
#else
        public static async Task<GetAnonymousKeyResult> GetAnonymousKey(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetAnonymousKeyResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetAnonymousKeyResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetAnonymousKey"
            );
            __getAnonymousKey_Internal(callbackId, "GetAnonymousKeyResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetAnonymousKey called");
            await Task.CompletedTask;
            return default(GetAnonymousKeyResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getAnonymousKey_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>그룹 ID</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetGroupId(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetGroupId"
            );
            __getGroupId_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetGroupId called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetGroupId(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetGroupId"
            );
            __getGroupId_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGroupId called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGroupId_Internal(string callbackId, string typeName);
#endif
        /// <param name="paramsParam">포인트를 지급하기 위해 필요한 정보예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. "4100": 프로모션 정보를 찾을 수 없을 때 "4104": 프로모션이 중지되었을 때 "4105": 프로모션이 종료되었을 때 "4108": 프로모션이 승인되지 않았을 때 "4109": 프로모션이 실행중이 아닐 때 "4110": 리워드를 지급/회수할 수 없을 때 "4112": 프로모션 머니가 부족할 때 "4113": 이미 지급/회수된 내역일 때 "4114": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GrantPromotionRewardResult> GrantPromotionReward(GrantPromotionRewardParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GrantPromotionRewardResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GrantPromotionReward"
            );
            __grantPromotionReward_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionReward called");
            await Awaitable.NextFrameAsync();
            return default(GrantPromotionRewardResult);
#endif
        }
#else
        public static async Task<GrantPromotionRewardResult> GrantPromotionReward(GrantPromotionRewardParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GrantPromotionRewardResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GrantPromotionReward"
            );
            __grantPromotionReward_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionReward called");
            await Task.CompletedTask;
            return default(GrantPromotionRewardResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionReward_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Other")]
        public static Action RequestNotificationAgreement(
            Action<RequestNotificationAgreementOptionsOnEventParam> onEvent,
            RequestNotificationAgreementOptionsOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<RequestNotificationAgreementOptionsOnEventParam>(
                onEvent,
                onError
            );
            __requestNotificationAgreement_Internal(AITJsonSettings.Serialize(options), subscriptionId, "RequestNotificationAgreementOptionsOnEventParam");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestNotificationAgreement called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] RequestNotificationAgreement cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestNotificationAgreement_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable RequestReview(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "RequestReview"
            );
            __requestReview_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] RequestReview called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task RequestReview(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "RequestReview"
            );
            __requestReview_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestReview called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestReview_Internal(string callbackId, string typeName);
#endif
        /// <param name="options">정기결제창을 띄울 때 필요한 옵션이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>인증 성공 여부를 포함한 결과를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Other")]
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
    }
}
