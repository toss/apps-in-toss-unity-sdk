// -----------------------------------------------------------------------
// <copyright file="AIT.Notification.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Notification APIs
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
    /// Apps in Toss Platform API - Notification
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 알림 동의 화면을 요청하고, 동의 결과를 콜백으로 전달해요.
        /// `templateCode`는 콘솔 스마트발송 템플릿 코드예요. 반환되는 cleanup 함수로 콜백을 해제해야 해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Notification")]
        public static Action NotificationRequestAgreement(
            Action<RequestNotificationAgreementOptionsOnEventParam> onEvent,
            RequestNotificationAgreementOptionsOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<RequestNotificationAgreementOptionsOnEventParam>(
                onEvent,
                onError
            );
            __NotificationRequestAgreement_Internal(AITJsonSettings.Serialize(options), subscriptionId, "RequestNotificationAgreementOptionsOnEventParam");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] NotificationRequestAgreement called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] NotificationRequestAgreement cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __NotificationRequestAgreement_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Notification.requestAgreement`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Notification")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> NotificationRequestAgreementIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "NotificationRequestAgreementIsSupported"
            );
            __NotificationRequestAgreementIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] NotificationRequestAgreementIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> NotificationRequestAgreementIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "NotificationRequestAgreementIsSupported"
            );
            __NotificationRequestAgreementIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] NotificationRequestAgreementIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __NotificationRequestAgreementIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 알림 동의 화면을 요청하고, 동의 결과를 콜백으로 전달해요.
        /// `templateCode`는 콘솔 스마트발송 템플릿 코드예요. 반환되는 cleanup 함수로 콜백을 해제해야 해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("Use NotificationRequestAgreement")]
        [Preserve]
        [APICategory("Notification")]
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
        /// <summary>
        /// 현재 앱 버전과 환경에서 `requestNotificationAgreement`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use NotificationRequestAgreementIsSupported")]
        [Preserve]
        [APICategory("Notification")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> RequestNotificationAgreementIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "RequestNotificationAgreementIsSupported"
            );
            __requestNotificationAgreementIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] RequestNotificationAgreementIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> RequestNotificationAgreementIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "RequestNotificationAgreementIsSupported"
            );
            __requestNotificationAgreementIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestNotificationAgreementIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestNotificationAgreementIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
