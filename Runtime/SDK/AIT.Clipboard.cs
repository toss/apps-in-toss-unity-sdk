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
#else
        public static async Task<string> GetClipboardText()
#endif
        {
#if UNITY_WEBGL// && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<string>();
#else
            var tcs = new TaskCompletionSource<string>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getClipboardText_Internal(callbackId, "string");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardText called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL// && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getClipboardText_Internal(string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> SetClipboardText(string text)
#else
        public static async Task<bool> SetClipboardText(string text)
#endif
        {
#if UNITY_WEBGL// && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __setClipboardText_Internal(text, callbackId, "void");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardText called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL// && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setClipboardText_Internal(string text, string callbackId, string typeName);
#endif
    }
}
