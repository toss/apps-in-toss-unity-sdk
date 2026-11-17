// -----------------------------------------------------------------------
// <copyright file="AIT.Device.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Device APIs
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
    /// Apps in Toss Platform API - Device
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 사용자 로케일 정보예요. 값을 못 가져오면 기본값은 `ko-KR`이에요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> DeviceLocale(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceLocale"
            );
            __DeviceLocale_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceLocale called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> DeviceLocale(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceLocale"
            );
            __DeviceLocale_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceLocale called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceLocale_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 플랫폼(`ios`/`android`) 정보예요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> DeviceOs(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceOs"
            );
            __DeviceOs_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOs called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> DeviceOs(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceOs"
            );
            __DeviceOs_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOs called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceOs_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 앨범에서 사진 목록을 불러와요.
        /// 기본값은 `maxCount=10`, `maxWidth=1024`, `base64=false`예요. 권한이 없으면 `FetchAlbumPhotosPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse[]> DeviceGetPhotos(DeviceGetPhotosOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetPhotos"
            );
            __DeviceGetPhotos_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse[]");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetPhotos called");
            await Awaitable.NextFrameAsync();
            return new ImageResponse[0];
#endif
        }
#else
        public static async Task<ImageResponse[]> DeviceGetPhotos(DeviceGetPhotosOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetPhotos"
            );
            __DeviceGetPhotos_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse[]");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetPhotos called");
            await Task.CompletedTask;
            return new ImageResponse[0];
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetPhotos_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.getPhotos`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> DeviceGetPhotosGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetPhotosGetPermission"
            );
            __DeviceGetPhotosGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetPhotosGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> DeviceGetPhotosGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetPhotosGetPermission"
            );
            __DeviceGetPhotosGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetPhotosGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetPhotosGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.getPhotos`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> DeviceGetPhotosOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetPhotosOpenPermissionDialog"
            );
            __DeviceGetPhotosOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetPhotosOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> DeviceGetPhotosOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetPhotosOpenPermissionDialog"
            );
            __DeviceGetPhotosOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetPhotosOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetPhotosOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 연락처 목록을 페이지 단위로 가져와요. `offset`은 첫 호출에 `0`, 이후 `nextOffset`을 사용해요.
        /// 권한이 없으면 `FetchContactsPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ContactResult> DeviceGetContacts(FetchContactsOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ContactResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ContactResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetContacts"
            );
            __DeviceGetContacts_Internal(AITJsonSettings.Serialize(options), callbackId, "ContactResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetContacts called");
            await Awaitable.NextFrameAsync();
            return default(ContactResult);
#endif
        }
#else
        public static async Task<ContactResult> DeviceGetContacts(FetchContactsOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ContactResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ContactResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetContacts"
            );
            __DeviceGetContacts_Internal(AITJsonSettings.Serialize(options), callbackId, "ContactResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetContacts called");
            await Task.CompletedTask;
            return default(ContactResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetContacts_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.getContacts`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> DeviceGetContactsGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetContactsGetPermission"
            );
            __DeviceGetContactsGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetContactsGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> DeviceGetContactsGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetContactsGetPermission"
            );
            __DeviceGetContactsGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetContactsGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetContactsGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.getContacts`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> DeviceGetContactsOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetContactsOpenPermissionDialog"
            );
            __DeviceGetContactsOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetContactsOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> DeviceGetContactsOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetContactsOpenPermissionDialog"
            );
            __DeviceGetContactsOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetContactsOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetContactsOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 디바이스의 현재 위치를 한 번 조회해요. 지속 감지가 필요하면 `subscribeLocation`을 사용하세요.
        /// 권한이 없으면 `GetCurrentLocationPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<Location> DeviceGetLocation(GetCurrentLocationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetLocation"
            );
            __DeviceGetLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetLocation called");
            await Awaitable.NextFrameAsync();
            return default(Location);
#endif
        }
#else
        public static async Task<Location> DeviceGetLocation(GetCurrentLocationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetLocation"
            );
            __DeviceGetLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetLocation called");
            await Task.CompletedTask;
            return default(Location);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetLocation_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.getLocation`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> DeviceGetLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetLocationGetPermission"
            );
            __DeviceGetLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetLocationGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> DeviceGetLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetLocationGetPermission"
            );
            __DeviceGetLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetLocationGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetLocationGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.getLocation`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> DeviceGetLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetLocationOpenPermissionDialog"
            );
            __DeviceGetLocationOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetLocationOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> DeviceGetLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetLocationOpenPermissionDialog"
            );
            __DeviceGetLocationOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetLocationOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetLocationOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 카메라를 실행해 촬영한 이미지를 `{ id, dataUri }`로 반환해요.
        /// 기본값은 `base64=false`, `maxWidth=1024`예요. 권한이 없으면 `OpenCameraPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse> DeviceOpenCamera(DeviceOpenCameraOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceOpenCamera"
            );
            __DeviceOpenCamera_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenCamera called");
            await Awaitable.NextFrameAsync();
            return default(ImageResponse);
#endif
        }
#else
        public static async Task<ImageResponse> DeviceOpenCamera(DeviceOpenCameraOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceOpenCamera"
            );
            __DeviceOpenCamera_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenCamera called");
            await Task.CompletedTask;
            return default(ImageResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceOpenCamera_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.openCamera`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> DeviceOpenCameraGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceOpenCameraGetPermission"
            );
            __DeviceOpenCameraGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenCameraGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> DeviceOpenCameraGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceOpenCameraGetPermission"
            );
            __DeviceOpenCameraGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenCameraGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceOpenCameraGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.openCamera`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> DeviceOpenCameraOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceOpenCameraOpenPermissionDialog"
            );
            __DeviceOpenCameraOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenCameraOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> DeviceOpenCameraOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceOpenCameraOpenPermissionDialog"
            );
            __DeviceOpenCameraOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenCameraOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceOpenCameraOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 앨범에서 사진·동영상을 선택해 가져와요. 취소하면 빈 배열을 반환하고, `types`를 안 넘기면 사진만 선택돼요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AlbumItemResponse[]> DeviceGetAlbumItems(DeviceGetAlbumItemsOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<AlbumItemResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<AlbumItemResponse[]>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetAlbumItems"
            );
            __DeviceGetAlbumItems_Internal(AITJsonSettings.Serialize(options), callbackId, "AlbumItemResponse[]");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetAlbumItems called");
            await Awaitable.NextFrameAsync();
            return new AlbumItemResponse[0];
#endif
        }
#else
        public static async Task<AlbumItemResponse[]> DeviceGetAlbumItems(DeviceGetAlbumItemsOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AlbumItemResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<AlbumItemResponse[]>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetAlbumItems"
            );
            __DeviceGetAlbumItems_Internal(AITJsonSettings.Serialize(options), callbackId, "AlbumItemResponse[]");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetAlbumItems called");
            await Task.CompletedTask;
            return new AlbumItemResponse[0];
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetAlbumItems_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Device.getAlbumItems`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> DeviceGetAlbumItemsIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceGetAlbumItemsIsSupported"
            );
            __DeviceGetAlbumItemsIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetAlbumItemsIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> DeviceGetAlbumItemsIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceGetAlbumItemsIsSupported"
            );
            __DeviceGetAlbumItemsIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceGetAlbumItemsIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceGetAlbumItemsIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 디바이스에 햅틱 진동을 일으켜요. `type`이 필수예요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable DeviceTriggerHaptic(HapticFeedbackOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceTriggerHaptic"
            );
            __DeviceTriggerHaptic_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceTriggerHaptic called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task DeviceTriggerHaptic(HapticFeedbackOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceTriggerHaptic"
            );
            __DeviceTriggerHaptic_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceTriggerHaptic called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceTriggerHaptic_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 위치 변경을 계속 감지하고 콜백을 실행해요. 반환되는 cleanup 함수로 구독을 해제해야 해요.
        /// `timeInterval`은 시스템 영향으로 지정값보다 길어질 수 있어요.
        /// 권한이 없으면 `StartUpdateLocationPermissionError`가 발생해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Device")]
        public static Action DeviceSubscribeLocation(
            Action<Location> onEvent,
            StartUpdateLocationOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<Location>(
                onEvent,
                onError
            );
            __DeviceSubscribeLocation_Internal(AITJsonSettings.Serialize(options), subscriptionId, "Location");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceSubscribeLocation called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] DeviceSubscribeLocation cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceSubscribeLocation_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// `Device.subscribeLocation`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> DeviceSubscribeLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceSubscribeLocationGetPermission"
            );
            __DeviceSubscribeLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceSubscribeLocationGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> DeviceSubscribeLocationGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceSubscribeLocationGetPermission"
            );
            __DeviceSubscribeLocationGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceSubscribeLocationGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceSubscribeLocationGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `Device.subscribeLocation`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionDialogResult> DeviceSubscribeLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionDialogResult>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionDialogResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceSubscribeLocationOpenPermissionDialog"
            );
            __DeviceSubscribeLocationOpenPermissionDialog_Internal(callbackId, "PermissionDialogResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceSubscribeLocationOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return default(PermissionDialogResult);
#endif
        }
#else
        public static async Task<PermissionDialogResult> DeviceSubscribeLocationOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionDialogResult>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionDialogResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceSubscribeLocationOpenPermissionDialog"
            );
            __DeviceSubscribeLocationOpenPermissionDialog_Internal(callbackId, "PermissionDialogResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceSubscribeLocationOpenPermissionDialog called");
            await Task.CompletedTask;
            return default(PermissionDialogResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceSubscribeLocationOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 지정한 URL을 기기의 기본 브라우저나 관련 앱에서 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable DeviceOpenURL(string url, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "DeviceOpenURL"
            );
            __DeviceOpenURL_Internal(url, callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenURL called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task DeviceOpenURL(string url, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "DeviceOpenURL"
            );
            __DeviceOpenURL_Internal(url, callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] DeviceOpenURL called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __DeviceOpenURL_Internal(string url, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 화면을 닫아요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ScreenClose(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ScreenClose"
            );
            __ScreenClose_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ScreenClose called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ScreenClose(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ScreenClose"
            );
            __ScreenClose_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ScreenClose called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ScreenClose_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 화면이 항상 켜져 있도록 설정하거나 해제해요. 앱 전체에 영향을 주므로 화면을 벗어날 때 이전 상태로 복구하세요.
        /// 앱을 벗어나면 항상 켜짐 모드가 풀릴 수 있어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ScreenSetAwakeModeResult> ScreenSetAwakeMode(SetScreenAwakeModeOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ScreenSetAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ScreenSetAwakeModeResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ScreenSetAwakeMode"
            );
            __ScreenSetAwakeMode_Internal(AITJsonSettings.Serialize(options), callbackId, "ScreenSetAwakeModeResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetAwakeMode called");
            await Awaitable.NextFrameAsync();
            return default(ScreenSetAwakeModeResult);
#endif
        }
#else
        public static async Task<ScreenSetAwakeModeResult> ScreenSetAwakeMode(SetScreenAwakeModeOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ScreenSetAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ScreenSetAwakeModeResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ScreenSetAwakeMode"
            );
            __ScreenSetAwakeMode_Internal(AITJsonSettings.Serialize(options), callbackId, "ScreenSetAwakeModeResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetAwakeMode called");
            await Task.CompletedTask;
            return default(ScreenSetAwakeModeResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ScreenSetAwakeMode_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 화면 캡쳐를 차단하거나 허용해요. `{ enabled: true }`면 차단, `false`면 허용이에요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ScreenSetSecureResult> ScreenSetSecure(SetSecureScreenOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ScreenSetSecureResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ScreenSetSecureResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ScreenSetSecure"
            );
            __ScreenSetSecure_Internal(AITJsonSettings.Serialize(options), callbackId, "ScreenSetSecureResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetSecure called");
            await Awaitable.NextFrameAsync();
            return default(ScreenSetSecureResult);
#endif
        }
#else
        public static async Task<ScreenSetSecureResult> ScreenSetSecure(SetSecureScreenOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ScreenSetSecureResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ScreenSetSecureResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ScreenSetSecure"
            );
            __ScreenSetSecure_Internal(AITJsonSettings.Serialize(options), callbackId, "ScreenSetSecureResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetSecure called");
            await Task.CompletedTask;
            return default(ScreenSetSecureResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ScreenSetSecure_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// iOS에서 화면을 스와이프해 뒤로가기하는 기능을 켜거나 꺼요. 옵션 키는 `isEnabled`예요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ScreenSetIosSwipeBack(SetIosSwipeGestureEnabledOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ScreenSetIosSwipeBack"
            );
            __ScreenSetIosSwipeBack_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetIosSwipeBack called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ScreenSetIosSwipeBack(SetIosSwipeGestureEnabledOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ScreenSetIosSwipeBack"
            );
            __ScreenSetIosSwipeBack_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetIosSwipeBack called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ScreenSetIosSwipeBack_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 기기 화면 방향을 `'portrait'` 또는 `'landscape'`로 설정해요. 앱 전체에 영향을 주므로 화면을 벗어날 때 복구하세요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ScreenSetOrientation(SetDeviceOrientationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ScreenSetOrientation"
            );
            __ScreenSetOrientation_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetOrientation called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ScreenSetOrientation(SetDeviceOrientationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ScreenSetOrientation"
            );
            __ScreenSetOrientation_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetOrientation called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ScreenSetOrientation_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Screen.setOrientation`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> ScreenSetOrientationIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ScreenSetOrientationIsSupported"
            );
            __ScreenSetOrientationIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetOrientationIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> ScreenSetOrientationIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ScreenSetOrientationIsSupported"
            );
            __ScreenSetOrientationIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ScreenSetOrientationIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ScreenSetOrientationIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 디바이스에 햅틱 진동을 일으켜요. `type`이 필수예요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceTriggerHaptic")]
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable GenerateHapticFeedback(HapticFeedbackOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "GenerateHapticFeedback"
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
        public static async Task GenerateHapticFeedback(HapticFeedbackOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GenerateHapticFeedback"
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
        /// <summary>
        /// 기기 화면 방향을 `'portrait'` 또는 `'landscape'`로 설정해요. 앱 전체에 영향을 주므로 화면을 벗어날 때 복구하세요.
        /// 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않아요.
        /// </summary>
        /// <param name="options">화면 방향 설정 값이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>화면 방향 설정이 완료되면 해결되는 Promise를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ScreenSetOrientation")]
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SetDeviceOrientation(SetDeviceOrientationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "SetDeviceOrientation"
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
        public static async Task SetDeviceOrientation(SetDeviceOrientationOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetDeviceOrientation"
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
        /// <summary>
        /// 현재 앱 버전과 환경에서 `setDeviceOrientation`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ScreenSetOrientationIsSupported")]
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> SetDeviceOrientationIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SetDeviceOrientationIsSupported"
            );
            __setDeviceOrientationIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SetDeviceOrientationIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> SetDeviceOrientationIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetDeviceOrientationIsSupported"
            );
            __setDeviceOrientationIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetDeviceOrientationIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setDeviceOrientationIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// iOS에서 화면을 스와이프해 뒤로가기하는 기능을 켜거나 꺼요. 옵션 키는 `isEnabled`예요.
        /// </summary>
        /// <param name="options">스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ScreenSetIosSwipeBack")]
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SetIosSwipeGestureEnabled(SetIosSwipeGestureEnabledOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "SetIosSwipeGestureEnabled"
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
        public static async Task SetIosSwipeGestureEnabled(SetIosSwipeGestureEnabledOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetIosSwipeGestureEnabled"
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
        /// <summary>
        /// 화면이 항상 켜져 있도록 설정하거나 해제해요. 앱 전체에 영향을 주므로 화면을 벗어날 때 이전 상태로 복구하세요.
        /// 앱을 벗어나면 항상 켜짐 모드가 풀릴 수 있어요.
        /// </summary>
        /// <param name="options">화면 항상 켜짐 모드의 설정 값이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>현재 화면 항상 켜짐 모드의 설정 상태를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ScreenSetAwakeMode")]
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SetScreenAwakeModeResult> SetScreenAwakeMode(SetScreenAwakeModeOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SetScreenAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetScreenAwakeModeResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SetScreenAwakeMode"
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
        public static async Task<SetScreenAwakeModeResult> SetScreenAwakeMode(SetScreenAwakeModeOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SetScreenAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetScreenAwakeModeResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetScreenAwakeMode"
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
        /// <summary>
        /// 화면 캡쳐를 차단하거나 허용해요. `{ enabled: true }`면 차단, `false`면 허용이에요.
        /// </summary>
        /// <param name="options">화면 캡쳐 설정 옵션이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>현재 설정된 캡쳐 차단 상태를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ScreenSetSecure")]
        [Preserve]
        [APICategory("Device")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SetSecureScreenResult> SetSecureScreen(SetSecureScreenOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SetSecureScreenResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetSecureScreenResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SetSecureScreen"
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
        public static async Task<SetSecureScreenResult> SetSecureScreen(SetSecureScreenOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SetSecureScreenResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetSecureScreenResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SetSecureScreen"
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
