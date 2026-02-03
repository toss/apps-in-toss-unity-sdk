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
        public static async Awaitable<SafeAreaInsets> SafeAreaInsetsGet()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
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
        public static async Task<SafeAreaInsets> SafeAreaInsetsGet()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SafeAreaInsets>();
            string callbackId = AITCore.Instance.RegisterCallback<SafeAreaInsets>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SafeArea")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __SafeAreaInsetsSubscribe_Internal(AITJsonSettings.Serialize(__0), callbackId, "System.Action");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsSubscribe called");
            await Awaitable.NextFrameAsync();
            return default(System.Action);
#endif
        }
#else
        public static async Task<System.Action> SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __SafeAreaInsetsSubscribe_Internal(AITJsonSettings.Serialize(__0), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SafeAreaInsetsSubscribe called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __SafeAreaInsetsSubscribe_Internal(string __0, string callbackId, string typeName);
#endif
    }
}
