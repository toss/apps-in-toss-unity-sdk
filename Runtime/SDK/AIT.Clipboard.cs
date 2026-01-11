// -----------------------------------------------------------------------
// <copyright file="AIT.Clipboard.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Clipboard APIs
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
    /// Apps in Toss Platform API - Clipboard
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetClipboardText()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __getClipboardText_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardText called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetClipboardText()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getClipboardText_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardText called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getClipboardText_Internal(string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SetClipboardText(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __setClipboardText_Internal(text, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardText called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task SetClipboardText(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __setClipboardText_Internal(text, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardText called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setClipboardText_Internal(string text, string callbackId, string typeName);
#endif
    }
}
