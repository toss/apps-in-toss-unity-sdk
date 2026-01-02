// -----------------------------------------------------------------------
// <copyright file="AIT.SafeArea.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SafeArea APIs
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
    /// Apps in Toss Platform API - SafeArea
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SafeAreaInsetsGetResult> SafeAreaInsetsGet()
#else
        public static async Task<SafeAreaInsetsGetResult> SafeAreaInsetsGet()
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<SafeAreaInsetsGetResult>();
#else
            var tcs = new TaskCompletionSource<SafeAreaInsetsGetResult>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsetsGetResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __SafeAreaInsetsGet_Internal(callbackId, "SafeAreaInsetsGetResult");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
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
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)
#else
        public static async Task<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<System.Action>();
#else
            var tcs = new TaskCompletionSource<System.Action>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __SafeAreaInsetsSubscribe_Internal(AITJsonSettings.Serialize(__0), callbackId, "System.Action");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
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
    }
}
