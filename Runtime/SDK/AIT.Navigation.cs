// -----------------------------------------------------------------------
// <copyright file="AIT.Navigation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Navigation APIs
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
    /// Apps in Toss Platform API - Navigation
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 네비게이션 바 버튼·타이틀·투명·배경색·테마를 바꿔요.
        /// 넘긴 필드만 반영하고, 생략한 필드는 직전에 적용된 값을 유지해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Navigation")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable NavigationBarSetOptions(SetNavigationBarOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "NavigationBarSetOptions"
            );
            __NavigationBarSetOptions_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] NavigationBarSetOptions called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task NavigationBarSetOptions(SetNavigationBarOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "NavigationBarSetOptions"
            );
            __NavigationBarSetOptions_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] NavigationBarSetOptions called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __NavigationBarSetOptions_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 화면을 닫아요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ScreenClose")]
        [Preserve]
        [APICategory("Navigation")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable CloseView(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "CloseView"
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
        public static async Task CloseView(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "CloseView"
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
        /// <summary>
        /// 지정한 URL을 기기의 기본 브라우저나 관련 앱에서 열어요.
        /// </summary>
        /// <param name="url">열고자 하는 URL 주소</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>URL이 성공적으로 열렸을 때 해결되는 Promise</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceOpenURL")]
        [Preserve]
        [APICategory("Navigation")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable OpenURL(string url, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "OpenURL"
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
        public static async Task OpenURL(string url, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenURL"
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
