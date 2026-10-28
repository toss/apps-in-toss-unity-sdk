// -----------------------------------------------------------------------
// <copyright file="AIT.Media.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Media APIs
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
    /// Apps in Toss Platform API - Media
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// Base64로 인코딩된 데이터를 지정한 파일 이름과 MIME 타입으로 기기에 저장해요.
        /// `fileName`에는 확장자를 포함해야 해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable FileSaveBase64(SaveBase64DataParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "FileSaveBase64"
            );
            __FileSaveBase64_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FileSaveBase64 called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task FileSaveBase64(SaveBase64DataParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FileSaveBase64"
            );
            __FileSaveBase64_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FileSaveBase64 called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __FileSaveBase64_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `File.saveBase64`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> FileSaveBase64IsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FileSaveBase64IsSupported"
            );
            __FileSaveBase64IsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FileSaveBase64IsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> FileSaveBase64IsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FileSaveBase64IsSupported"
            );
            __FileSaveBase64IsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FileSaveBase64IsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __FileSaveBase64IsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// Base64로 인코딩된 PDF 데이터를 네이티브 PDF 뷰어로 열어요. 뷰어를 닫으면 `'CLOSE'`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> FileOpenPDFViewer(OpenPDFViewerParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FileOpenPDFViewer"
            );
            __FileOpenPDFViewer_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FileOpenPDFViewer called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> FileOpenPDFViewer(OpenPDFViewerParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FileOpenPDFViewer"
            );
            __FileOpenPDFViewer_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FileOpenPDFViewer called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __FileOpenPDFViewer_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `File.openPDFViewer`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> FileOpenPDFViewerIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FileOpenPDFViewerIsSupported"
            );
            __FileOpenPDFViewerIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FileOpenPDFViewerIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> FileOpenPDFViewerIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FileOpenPDFViewerIsSupported"
            );
            __FileOpenPDFViewerIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FileOpenPDFViewerIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __FileOpenPDFViewerIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 앨범에서 사진·동영상을 선택해 가져와요. 취소하면 빈 배열을 반환하고, `types`를 안 넘기면 사진만 선택돼요.
        /// `isSupported()`로 지원 여부를 확인할 수 있어요.
        /// </summary>
        /// <param name="options">조회 옵션을 담은 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>선택한 미디어 목록을 반환해요. 취소 시 빈 배열을 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetAlbumItems")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AlbumItemResponse[]> FetchAlbumItems(FetchAlbumItemsOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<AlbumItemResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<AlbumItemResponse[]>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchAlbumItems"
            );
            __fetchAlbumItems_Internal(AITJsonSettings.Serialize(options), callbackId, "AlbumItemResponse[]");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumItems called");
            await Awaitable.NextFrameAsync();
            return new AlbumItemResponse[0];
#endif
        }
#else
        public static async Task<AlbumItemResponse[]> FetchAlbumItems(FetchAlbumItemsOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AlbumItemResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<AlbumItemResponse[]>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchAlbumItems"
            );
            __fetchAlbumItems_Internal(AITJsonSettings.Serialize(options), callbackId, "AlbumItemResponse[]");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumItems called");
            await Task.CompletedTask;
            return new AlbumItemResponse[0];
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumItems_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `fetchAlbumItems`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetAlbumItemsIsSupported")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> FetchAlbumItemsIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchAlbumItemsIsSupported"
            );
            __fetchAlbumItemsIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumItemsIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> FetchAlbumItemsIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchAlbumItemsIsSupported"
            );
            __fetchAlbumItemsIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumItemsIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumItemsIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 앨범에서 사진 목록을 불러와요.
        /// 기본값은 `maxCount=10`, `maxWidth=1024`, `base64=false`예요. 권한이 없으면 `FetchAlbumPhotosPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetPhotos")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchAlbumPhotos"
            );
            __fetchAlbumPhotos_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse[]");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotos called");
            await Awaitable.NextFrameAsync();
            return new ImageResponse[0];
#endif
        }
#else
        public static async Task<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchAlbumPhotos"
            );
            __fetchAlbumPhotos_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse[]");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotos called");
            await Task.CompletedTask;
            return new ImageResponse[0];
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotos_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `fetchAlbumPhotos`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetPhotosGetPermission")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> FetchAlbumPhotosGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchAlbumPhotosGetPermission"
            );
            __fetchAlbumPhotosGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotosGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> FetchAlbumPhotosGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchAlbumPhotosGetPermission"
            );
            __fetchAlbumPhotosGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotosGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotosGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `fetchAlbumPhotos`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetPhotosOpenPermissionDialog")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> FetchAlbumPhotosOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchAlbumPhotosOpenPermissionDialog"
            );
            __fetchAlbumPhotosOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotosOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> FetchAlbumPhotosOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchAlbumPhotosOpenPermissionDialog"
            );
            __fetchAlbumPhotosOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotosOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotosOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 카메라를 실행해 촬영한 이미지를 `{ id, dataUri }`로 반환해요.
        /// 기본값은 `base64=false`, `maxWidth=1024`예요. 권한이 없으면 `OpenCameraPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceOpenCamera")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse> OpenCamera(OpenCameraOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "OpenCamera"
            );
            __openCamera_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenCamera called");
            await Awaitable.NextFrameAsync();
            return default(ImageResponse);
#endif
        }
#else
        public static async Task<ImageResponse> OpenCamera(OpenCameraOptions options = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenCamera"
            );
            __openCamera_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenCamera called");
            await Task.CompletedTask;
            return default(ImageResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCamera_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `openCamera`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceOpenCameraGetPermission")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> OpenCameraGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "OpenCameraGetPermission"
            );
            __openCameraGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenCameraGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> OpenCameraGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenCameraGetPermission"
            );
            __openCameraGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenCameraGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCameraGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `openCamera`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceOpenCameraOpenPermissionDialog")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> OpenCameraOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "OpenCameraOpenPermissionDialog"
            );
            __openCameraOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenCameraOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> OpenCameraOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenCameraOpenPermissionDialog"
            );
            __openCameraOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenCameraOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCameraOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// Base64로 인코딩된 PDF 데이터를 네이티브 PDF 뷰어로 열어요. 뷰어를 닫으면 `'CLOSE'`를 반환해요.
        /// `openPDFViewer.isSupported()`로 지원 여부를 확인할 수 있어요.
        /// </summary>
        /// <param name="paramsParam">PDF 데이터와 파일 이름을 담은 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>PDF 뷰어가 닫히면 'CLOSE'를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use FileOpenPDFViewer")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> OpenPDFViewer(OpenPDFViewerParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "OpenPDFViewer"
            );
            __openPDFViewer_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenPDFViewer called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> OpenPDFViewer(OpenPDFViewerParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenPDFViewer"
            );
            __openPDFViewer_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenPDFViewer called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openPDFViewer_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `openPDFViewer`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use FileOpenPDFViewerIsSupported")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> OpenPDFViewerIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "OpenPDFViewerIsSupported"
            );
            __openPDFViewerIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenPDFViewerIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> OpenPDFViewerIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenPDFViewerIsSupported"
            );
            __openPDFViewerIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenPDFViewerIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openPDFViewerIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// Base64로 인코딩된 데이터를 지정한 파일 이름과 MIME 타입으로 기기에 저장해요.
        /// `fileName`에는 확장자를 포함해야 해요.
        /// 앱 버전이 최소 지원 버전보다 낮으면 경고 로그만 남기고 아무 동작도 하지 않아요.
        /// </summary>
        /// <param name="paramsParam">저장할 데이터와 파일 정보를 담은 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use FileSaveBase64")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SaveBase64Data(SaveBase64DataParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "SaveBase64Data"
            );
            __saveBase64Data_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SaveBase64Data called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task SaveBase64Data(SaveBase64DataParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SaveBase64Data"
            );
            __saveBase64Data_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SaveBase64Data called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __saveBase64Data_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `saveBase64Data`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use FileSaveBase64IsSupported")]
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> SaveBase64DataIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SaveBase64DataIsSupported"
            );
            __saveBase64DataIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SaveBase64DataIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> SaveBase64DataIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SaveBase64DataIsSupported"
            );
            __saveBase64DataIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SaveBase64DataIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __saveBase64DataIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
