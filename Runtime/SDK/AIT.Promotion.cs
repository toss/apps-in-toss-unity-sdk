// -----------------------------------------------------------------------
// <copyright file="AIT.Promotion.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Promotion APIs
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
    /// Apps in Toss Platform API - Promotion
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 프로모션 코드를 사용해 유저에게 리워드를 지급해요. 성공 시 `{ key }`를 반환해요.
        /// 지급 실패면 실패 코드(`error.code`)를 담은 에러를, 알 수 없는 오류면 `UNKNOWN_ERROR`를 throw해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Promotion")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GrantPromotionRewardResponse> PromotionGrantReward(GrantRewardParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GrantPromotionRewardResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "PromotionGrantReward"
            );
            __PromotionGrantReward_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] PromotionGrantReward called");
            await Awaitable.NextFrameAsync();
            return default(GrantPromotionRewardResponse);
#endif
        }
#else
        public static async Task<GrantPromotionRewardResponse> PromotionGrantReward(GrantRewardParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GrantPromotionRewardResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "PromotionGrantReward"
            );
            __PromotionGrantReward_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] PromotionGrantReward called");
            await Task.CompletedTask;
            return default(GrantPromotionRewardResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __PromotionGrantReward_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Promotion.grantReward`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Promotion")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> PromotionGrantRewardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "PromotionGrantRewardIsSupported"
            );
            __PromotionGrantRewardIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] PromotionGrantRewardIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> PromotionGrantRewardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "PromotionGrantRewardIsSupported"
            );
            __PromotionGrantRewardIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] PromotionGrantRewardIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __PromotionGrantRewardIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 친구에게 공유하고 리워드를 받을 수 있는 기능을 제공해요.
        /// `options.moduleId`는 콘솔 공유 리워드 UUID예요. 반환되는 cleanup 함수로 구독을 해제해야 해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("이 API는 더 이상 사용을 권장하지 않아요.")]
        [Preserve]
        [APICategory("Promotion")]
        public static Action PromotionOpenContactsInvite(
            Action<ContactsViralEvent> onEvent,
            ContactsViralParamsOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ContactsViralEvent>(
                onEvent,
                onError
            );
            __PromotionOpenContactsInvite_Internal(AITJsonSettings.Serialize(options), subscriptionId, "ContactsViralEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] PromotionOpenContactsInvite called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] PromotionOpenContactsInvite cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __PromotionOpenContactsInvite_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Promotion.openContactsInvite`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("이 API는 더 이상 사용을 권장하지 않아요.")]
        [Preserve]
        [APICategory("Promotion")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> PromotionOpenContactsInviteIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "PromotionOpenContactsInviteIsSupported"
            );
            __PromotionOpenContactsInviteIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] PromotionOpenContactsInviteIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> PromotionOpenContactsInviteIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "PromotionOpenContactsInviteIsSupported"
            );
            __PromotionOpenContactsInviteIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] PromotionOpenContactsInviteIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __PromotionOpenContactsInviteIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 프로모션 코드를 사용해 유저에게 리워드를 지급해요.
        /// 성공 시 `{ key }`, 실패 시 `{ errorCode, message }` 또는 `'ERROR'`를 반환하고,
        /// 미지원 앱 버전이면 `undefined`를 반환해요. 실패는 reject 대신 반환값으로 흡수해요.
        /// </summary>
        /// <param name="paramsParam">포인트를 지급하기 위해 필요한 정보예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. "4100": 프로모션 정보를 찾을 수 없을 때 "4104": 프로모션이 중지되었을 때 "4105": 프로모션이 종료되었을 때 "4108": 프로모션이 승인되지 않았을 때 "4109": 프로모션이 실행중이 아닐 때 "4110": 리워드를 지급/회수할 수 없을 때 "4112": 프로모션 머니가 부족할 때 "4113": 이미 지급/회수된 내역일 때 "4114": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use PromotionGrantReward")]
        [Preserve]
        [APICategory("Promotion")]
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
        /// <summary>
        /// 현재 앱 버전과 환경에서 `grantPromotionReward`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use PromotionGrantRewardIsSupported")]
        [Preserve]
        [APICategory("Promotion")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GrantPromotionRewardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GrantPromotionRewardIsSupported"
            );
            __grantPromotionRewardIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GrantPromotionRewardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GrantPromotionRewardIsSupported"
            );
            __grantPromotionRewardIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionRewardIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
