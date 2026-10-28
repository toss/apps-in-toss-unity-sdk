// -----------------------------------------------------------------------
// <copyright file="AIT.Clipboard.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Clipboard APIs
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
    /// Apps in Toss Platform API - Clipboard
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 클립보드에 저장된 텍스트를 가져와요. 텍스트가 없으면 빈 문자열을 반환해요.
        /// 권한이 없으면 `GetClipboardTextPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> ClipboardGetText(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ClipboardGetText"
            );
            __ClipboardGetText_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardGetText called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> ClipboardGetText(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ClipboardGetText"
            );
            __ClipboardGetText_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardGetText called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ClipboardGetText_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Clipboard.getText`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> ClipboardGetTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ClipboardGetTextGetPermission"
            );
            __ClipboardGetTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardGetTextGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> ClipboardGetTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ClipboardGetTextGetPermission"
            );
            __ClipboardGetTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardGetTextGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ClipboardGetTextGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Clipboard.getText`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> ClipboardGetTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ClipboardGetTextOpenPermissionDialog"
            );
            __ClipboardGetTextOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardGetTextOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> ClipboardGetTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ClipboardGetTextOpenPermissionDialog"
            );
            __ClipboardGetTextOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardGetTextOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ClipboardGetTextOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 텍스트를 클립보드에 복사해요.
        /// 권한이 없으면 `SetClipboardTextPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ClipboardSetText(string text, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ClipboardSetText"
            );
            __ClipboardSetText_Internal(text, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardSetText called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ClipboardSetText(string text, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ClipboardSetText"
            );
            __ClipboardSetText_Internal(text, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardSetText called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ClipboardSetText_Internal(string text, string callbackId, string typeName);
#endif
        /// <summary>
        /// `Clipboard.setText`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> ClipboardSetTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ClipboardSetTextGetPermission"
            );
            __ClipboardSetTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardSetTextGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> ClipboardSetTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ClipboardSetTextGetPermission"
            );
            __ClipboardSetTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardSetTextGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ClipboardSetTextGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Clipboard.setText`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> ClipboardSetTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ClipboardSetTextOpenPermissionDialog"
            );
            __ClipboardSetTextOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardSetTextOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> ClipboardSetTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ClipboardSetTextOpenPermissionDialog"
            );
            __ClipboardSetTextOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ClipboardSetTextOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ClipboardSetTextOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 클립보드에 저장된 텍스트를 가져와요. 텍스트가 없으면 빈 문자열을 반환해요.
        /// 권한이 없으면 `GetClipboardTextPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ClipboardGetText")]
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetClipboardText(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetClipboardText"
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
        public static async Task<string> GetClipboardText(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetClipboardText"
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
        /// <summary>
        /// `getClipboardText`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ClipboardGetTextGetPermission")]
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> GetClipboardTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetClipboardTextGetPermission"
            );
            __getClipboardTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardTextGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> GetClipboardTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetClipboardTextGetPermission"
            );
            __getClipboardTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardTextGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getClipboardTextGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `getClipboardText`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ClipboardGetTextOpenPermissionDialog")]
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetClipboardTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetClipboardTextOpenPermissionDialog"
            );
            __getClipboardTextOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardTextOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetClipboardTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetClipboardTextOpenPermissionDialog"
            );
            __getClipboardTextOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardTextOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getClipboardTextOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 텍스트를 클립보드에 복사해요.
        /// 권한이 없으면 `SetClipboardTextPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ClipboardSetText")]
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SetClipboardText(string text, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "SetClipboardText"
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
        public static async Task SetClipboardText(string text, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetClipboardText"
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
        /// <summary>
        /// `setClipboardText`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ClipboardSetTextGetPermission")]
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> SetClipboardTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SetClipboardTextGetPermission"
            );
            __setClipboardTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardTextGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> SetClipboardTextGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetClipboardTextGetPermission"
            );
            __setClipboardTextGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardTextGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setClipboardTextGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `setClipboardText`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ClipboardSetTextOpenPermissionDialog")]
        [Preserve]
        [APICategory("Clipboard")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> SetClipboardTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SetClipboardTextOpenPermissionDialog"
            );
            __setClipboardTextOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardTextOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> SetClipboardTextOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetClipboardTextOpenPermissionDialog"
            );
            __setClipboardTextOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardTextOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setClipboardTextOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
    }
}
