// -----------------------------------------------------------------------
// <copyright file="AIT.Device.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Device APIs
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
    /// Apps in Toss Platform API - Device
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable GenerateHapticFeedback(HapticFeedbackOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __generateHapticFeedback_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GenerateHapticFeedback called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task GenerateHapticFeedback(HapticFeedbackOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __generateHapticFeedback_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GenerateHapticFeedback called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __generateHapticFeedback_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="options">화면 방향 설정 값이에요.</param>
        /// <returns>화면 방향 설정이 완료되면 해결되는 Promise를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SetDeviceOrientation(SetDeviceOrientationOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __setDeviceOrientation_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetDeviceOrientation called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task SetDeviceOrientation(SetDeviceOrientationOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __setDeviceOrientation_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetDeviceOrientation called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setDeviceOrientation_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="options">스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SetIosSwipeGestureEnabled(SetIosSwipeGestureEnabledOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __setIosSwipeGestureEnabled_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetIosSwipeGestureEnabled called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task SetIosSwipeGestureEnabled(SetIosSwipeGestureEnabledOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __setIosSwipeGestureEnabled_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetIosSwipeGestureEnabled called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setIosSwipeGestureEnabled_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="options">화면 항상 켜짐 모드의 설정 값이에요.</param>
        /// <returns>현재 화면 항상 켜짐 모드의 설정 상태를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SetScreenAwakeModeResult> SetScreenAwakeMode(SetScreenAwakeModeOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SetScreenAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetScreenAwakeModeResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __setScreenAwakeMode_Internal(AITJsonSettings.Serialize(options), callbackId, "SetScreenAwakeModeResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetScreenAwakeMode called");
            await Awaitable.NextFrameAsync();
            return default(SetScreenAwakeModeResult);
#endif
        }
#else
        public static async Task<SetScreenAwakeModeResult> SetScreenAwakeMode(SetScreenAwakeModeOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SetScreenAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetScreenAwakeModeResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __setScreenAwakeMode_Internal(AITJsonSettings.Serialize(options), callbackId, "SetScreenAwakeModeResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetScreenAwakeMode called");
            await Task.CompletedTask;
            return default(SetScreenAwakeModeResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setScreenAwakeMode_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="options">화면 캡쳐 설정 옵션이에요.</param>
        /// <returns>: boolean} 현재 설정된 캡쳐 차단 상태를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SetSecureScreenResult> SetSecureScreen(SetSecureScreenOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SetSecureScreenResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetSecureScreenResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __setSecureScreen_Internal(AITJsonSettings.Serialize(options), callbackId, "SetSecureScreenResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetSecureScreen called");
            await Awaitable.NextFrameAsync();
            return default(SetSecureScreenResult);
#endif
        }
#else
        public static async Task<SetSecureScreenResult> SetSecureScreen(SetSecureScreenOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SetSecureScreenResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetSecureScreenResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __setSecureScreen_Internal(AITJsonSettings.Serialize(options), callbackId, "SetSecureScreenResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetSecureScreen called");
            await Task.CompletedTask;
            return default(SetSecureScreenResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setSecureScreen_Internal(string options, string callbackId, string typeName);
#endif
    }
}
