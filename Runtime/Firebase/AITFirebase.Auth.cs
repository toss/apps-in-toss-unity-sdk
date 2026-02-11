// -----------------------------------------------------------------------
// <copyright file="AITFirebase.Auth.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Auth APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
    /// <summary>
    /// Firebase Auth APIs
    /// </summary>
    public static partial class AITFirebase
    {
        /// <summary>익명으로 로그인합니다.</summary>
        [Preserve]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<FirebaseUser> SignInAnonymously()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<FirebaseUser>();
            string callbackId = AITCore.Instance.RegisterCallback<FirebaseUser>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __Firebase_signInAnonymously_Internal(callbackId, "FirebaseUser");
            return await acs.Awaitable;
#else
            Debug.Log($"[AITFirebase Mock] SignInAnonymously called");
            await Awaitable.NextFrameAsync();
            return default(FirebaseUser);
#endif
        }
#else
        public static async Task<FirebaseUser> SignInAnonymously()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<FirebaseUser>();
            string callbackId = AITCore.Instance.RegisterCallback<FirebaseUser>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __Firebase_signInAnonymously_Internal(callbackId, "FirebaseUser");
            return await tcs.Task;
#else
            Debug.Log($"[AITFirebase Mock] SignInAnonymously called");
            await Task.CompletedTask;
            return default(FirebaseUser);
#endif
        }
#endif

        /// <summary>커스텀 토큰으로 로그인합니다.</summary>
        /// <param name="token">커스텀 토큰</param>
        [Preserve]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<FirebaseUser> SignInWithCustomToken(string token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<FirebaseUser>();
            string callbackId = AITCore.Instance.RegisterCallback<FirebaseUser>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __Firebase_signInWithCustomToken_Internal(token, callbackId, "FirebaseUser");
            return await acs.Awaitable;
#else
            Debug.Log($"[AITFirebase Mock] SignInWithCustomToken called");
            await Awaitable.NextFrameAsync();
            return default(FirebaseUser);
#endif
        }
#else
        public static async Task<FirebaseUser> SignInWithCustomToken(string token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<FirebaseUser>();
            string callbackId = AITCore.Instance.RegisterCallback<FirebaseUser>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __Firebase_signInWithCustomToken_Internal(token, callbackId, "FirebaseUser");
            return await tcs.Task;
#else
            Debug.Log($"[AITFirebase Mock] SignInWithCustomToken called");
            await Task.CompletedTask;
            return default(FirebaseUser);
#endif
        }
#endif

        /// <summary>로그아웃합니다.</summary>
        [Preserve]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SignOut()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterVoidCallback(
                () => acs.SetResult(),
                error => acs.SetException(error)
            );
            __Firebase_signOut_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            Debug.Log($"[AITFirebase Mock] SignOut called");
            await Awaitable.NextFrameAsync();
#endif
        }
#else
        public static async Task SignOut()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterVoidCallback(
                () => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __Firebase_signOut_Internal(callbackId, "void");
            await tcs.Task;
#else
            Debug.Log($"[AITFirebase Mock] SignOut called");
            await Task.CompletedTask;
#endif
        }
#endif

        /// <summary>인증 상태 변경을 구독합니다. 로그인/로그아웃 시 콜백이 호출됩니다.</summary>
        /// <param name="onEvent">인증 상태 변경 시 호출되는 콜백</param>
        /// <param name="onError">에러 발생 시 호출되는 콜백</param>
        /// <returns>구독 해제를 위한 Action. 호출 시 구독이 취소됩니다.</returns>
        [Preserve]
        public static Action OnAuthStateChanged(Action<FirebaseUser> onEvent, Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<FirebaseUser>(onEvent, onError);
            __Firebase_onAuthStateChanged_Internal(subscriptionId, "FirebaseUser");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            Debug.Log($"[AITFirebase Mock] OnAuthStateChanged subscribed");
            return () => Debug.Log($"[AITFirebase Mock] OnAuthStateChanged unsubscribed");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_signInAnonymously_Internal(string callbackId, string typeName);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_signInWithCustomToken_Internal(string token, string callbackId, string typeName);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_signOut_Internal(string callbackId, string typeName);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_onAuthStateChanged_Internal(string subscriptionId, string typeName);
#endif
    }
}
