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
        /// <param name="options">조회 옵션을 담은 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>선택한 미디어 목록을 반환해요. 취소 시 빈 배열을 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
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
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
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
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
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
        /// <param name="paramsParam">저장할 데이터와 파일 정보를 담은 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
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
    }
}
