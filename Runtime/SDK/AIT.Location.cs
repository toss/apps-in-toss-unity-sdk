// -----------------------------------------------------------------------
// <copyright file="AIT.Location.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Location APIs
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
    /// Apps in Toss Platform API - Location
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 디바이스의 현재 위치를 한 번 조회해요. 지속 감지가 필요하면 `startUpdateLocation`(`Device.subscribeLocation`)을 사용하세요.
        /// 권한이 없으면 `GetCurrentLocationPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetLocation")]
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<Location> GetCurrentLocation(GetCurrentLocationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetCurrentLocation"
            );
            __getCurrentLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            await Awaitable.NextFrameAsync();
            return default(Location);
#endif
        }
#else
        public static async Task<Location> GetCurrentLocation(GetCurrentLocationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetCurrentLocation"
            );
            __getCurrentLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            await Task.CompletedTask;
            return default(Location);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocation_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `getCurrentLocation`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetLocationGetPermission")]
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> GetCurrentLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetCurrentLocationGetPermission"
            );
            __getCurrentLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocationGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> GetCurrentLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetCurrentLocationGetPermission"
            );
            __getCurrentLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocationGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocationGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `getCurrentLocation`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetLocationOpenPermissionDialog")]
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetCurrentLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetCurrentLocationOpenPermissionDialog"
            );
            __getCurrentLocationOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocationOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetCurrentLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetCurrentLocationOpenPermissionDialog"
            );
            __getCurrentLocationOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocationOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocationOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 위치 변경을 계속 감지하고 콜백을 실행해요. 반환되는 cleanup 함수로 구독을 해제해야 해요.
        /// `timeInterval`은 시스템 영향으로 지정값보다 길어질 수 있어요.
        /// 권한이 없으면 `onError`에 `StartUpdateLocationPermissionError`가 전달돼요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Obsolete("Use DeviceSubscribeLocation")]
        [Preserve]
        [APICategory("Location")]
        public static Action StartUpdateLocation(
            Action<Location> onEvent,
            StartUpdateLocationOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<Location>(
                onEvent,
                onError
            );
            __startUpdateLocation_Internal(AITJsonSettings.Serialize(options), subscriptionId, "Location");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocation_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// `startUpdateLocation`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceSubscribeLocationGetPermission")]
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> StartUpdateLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "StartUpdateLocationGetPermission"
            );
            __startUpdateLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocationGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> StartUpdateLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "StartUpdateLocationGetPermission"
            );
            __startUpdateLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocationGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocationGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `startUpdateLocation`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceSubscribeLocationOpenPermissionDialog")]
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionDialogResult> StartUpdateLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionDialogResult>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionDialogResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "StartUpdateLocationOpenPermissionDialog"
            );
            __startUpdateLocationOpenPermissionDialog_Internal(callbackId, "PermissionDialogResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocationOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return default(PermissionDialogResult);
#endif
        }
#else
        public static async Task<PermissionDialogResult> StartUpdateLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionDialogResult>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionDialogResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "StartUpdateLocationOpenPermissionDialog"
            );
            __startUpdateLocationOpenPermissionDialog_Internal(callbackId, "PermissionDialogResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocationOpenPermissionDialog called");
            await Task.CompletedTask;
            return default(PermissionDialogResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocationOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
    }
}
