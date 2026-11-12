// -----------------------------------------------------------------------
// <copyright file="AIT.SafeArea.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SafeArea APIs
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
    /// Apps in Toss Platform API - SafeArea
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 현재 Safe Area insets(top/bottom/left/right)를 동기로 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SafeAreaInsets> SafeAreaGet(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SafeAreaGet"
            );
            __SafeAreaGet_Internal(callbackId, "SafeAreaInsets");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaGet called");
            await Awaitable.NextFrameAsync();
            return default(SafeAreaInsets);
#endif
        }
#else
        public static async Task<SafeAreaInsets> SafeAreaGet(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SafeAreaGet"
            );
            __SafeAreaGet_Internal(callbackId, "SafeAreaInsets");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaGet called");
            await Task.CompletedTask;
            return default(SafeAreaInsets);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaGet_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// Safe Area insets 변경을 구독해요. insets가 바뀔 때만 이벤트가 발생해요.
        /// 반환되는 구독 해제 함수는 언마운트 시 호출하세요.
        /// </summary>
        /// <param name="onEvent">Event callback when the event is triggered</param>
        /// <param name="onError">Error callback (optional)</param>
        /// <returns>Action to call for unsubscribing from the event</returns>
        [Preserve]
        [APICategory("SafeArea")]
        public static Action SafeAreaSubscribe(
            Action<SafeAreaInsets> onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<SafeAreaInsets>(
                onEvent,
                onError
            );
            __SafeAreaSubscribe_Internal(subscriptionId, "SafeAreaInsets");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaSubscribe subscribed");
            return () => UnityEngine.Debug.Log($"[AIT Mock] SafeAreaSubscribe unsubscribed");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaSubscribe_Internal(string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 현재 Safe Area insets(top/bottom/left/right)를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SafeAreaInsets> SafeAreaInsetsGet(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SafeAreaInsetsGet"
            );
            __SafeAreaInsetsGet_Internal(callbackId, "SafeAreaInsets");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsGet called");
            await Awaitable.NextFrameAsync();
            return default(SafeAreaInsets);
#endif
        }
#else
        public static async Task<SafeAreaInsets> SafeAreaInsetsGet(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SafeAreaInsetsGet"
            );
            __SafeAreaInsetsGet_Internal(callbackId, "SafeAreaInsets");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsGet called");
            await Task.CompletedTask;
            return default(SafeAreaInsets);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaInsetsGet_Internal(string callbackId, string typeName);
#endif
        /// <param name="onEvent">Event callback when the event is triggered</param>
        /// <param name="onError">Error callback (optional)</param>
        /// <returns>Action to call for unsubscribing from the event</returns>
        [Preserve]
        [APICategory("SafeArea")]
        public static Action SafeAreaInsetsSubscribe(
            Action<SafeAreaInsets> onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<SafeAreaInsets>(
                onEvent,
                onError
            );
            __SafeAreaInsetsSubscribe_Internal(subscriptionId, "SafeAreaInsets");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsSubscribe subscribed");
            return () => UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsSubscribe unsubscribed");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaInsetsSubscribe_Internal(string subscriptionId, string typeName);
#endif

        /// <summary>
        /// 이전 SDK의 시그니처다. 매개변수 묶음의 <c>OnEvent</c>로 구독을 시작하고 해제 Action을 돌려준다.
        /// 새 코드는 <c>SafeAreaInsetsSubscribe(onEvent, onError)</c>를 쓴다.
        /// </summary>
        [Obsolete("Use SafeAreaInsetsSubscribe(Action<SafeAreaInsets> onEvent, Action<AITException> onError = null)")]
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0, int timeoutMs = 0)
        {
            var unsubscribe = SafeAreaInsetsSubscribe(__0?.OnEvent ?? (_ => { }), null);
            await Awaitable.NextFrameAsync();
            return unsubscribe;
        }
#else
        public static async Task<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0, int timeoutMs = 0)
        {
            var unsubscribe = SafeAreaInsetsSubscribe(__0?.OnEvent ?? (_ => { }), null);
            await Task.CompletedTask;
            return unsubscribe;
        }
#endif

        /// <summary>
        /// <c>SafeAreaInsetsSubscribe(null)</c>처럼 인자 하나만 넘기는 옛 호출이 새 오버로드(<c>onError</c> 기본값)와 모호해지지 않도록 두는 오버로드다.
        /// 기본값 없이 인자가 모두 맞는 쪽이 우선하므로 옛 호출은 항상 이쪽에 묶인다.
        /// </summary>
        [Obsolete("Use SafeAreaInsetsSubscribe(Action<SafeAreaInsets> onEvent, Action<AITException> onError = null)")]
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static Awaitable<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)
            => SafeAreaInsetsSubscribe(__0, 0);
#else
        public static Task<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)
            => SafeAreaInsetsSubscribe(__0, 0);
#endif
        /// <summary>
        /// Safe Area insets 값을 가져와요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use SafeAreaGet")]
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SafeAreaInsets> GetSafeAreaInsets(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetSafeAreaInsets"
            );
            __getSafeAreaInsets_Internal(callbackId, "SafeAreaInsets");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetSafeAreaInsets called");
            await Awaitable.NextFrameAsync();
            return default(SafeAreaInsets);
#endif
        }
#else
        public static async Task<SafeAreaInsets> GetSafeAreaInsets(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetSafeAreaInsets"
            );
            __getSafeAreaInsets_Internal(callbackId, "SafeAreaInsets");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetSafeAreaInsets called");
            await Task.CompletedTask;
            return default(SafeAreaInsets);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getSafeAreaInsets_Internal(string callbackId, string typeName);
#endif
    }
}
