// -----------------------------------------------------------------------
// <copyright file="AIT.Media.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Media APIs
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
    /// Apps in Toss Platform API - Media
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
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
        public static async Task<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse> OpenCamera(OpenCameraOptions options = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
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
        public static async Task<ImageResponse> OpenCamera(OpenCameraOptions options = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable SaveBase64Data(SaveBase64DataParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
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
        public static async Task SaveBase64Data(SaveBase64DataParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
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
