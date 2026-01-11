// -----------------------------------------------------------------------
// <copyright file="AIT.Navigation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Navigation APIs
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
    /// Apps in Toss Platform API - Navigation
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Navigation")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable CloseView()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __closeView_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] CloseView called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task CloseView()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __closeView_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] CloseView called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __closeView_Internal(string callbackId, string typeName);
#endif
        /// <param name="url">열고자 하는 URL 주소</param>
        /// <returns>URL이 성공적으로 열렸을 때 해결되는 Promise</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Navigation")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable OpenURL(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __openURL_Internal(url, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenURL called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task OpenURL(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __openURL_Internal(url, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenURL called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openURL_Internal(string url, string callbackId, string typeName);
#endif
    }
}
