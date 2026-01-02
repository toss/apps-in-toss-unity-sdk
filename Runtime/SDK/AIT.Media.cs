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
#else
        public static async Task<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options = null)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<ImageResponse[]>();
#else
            var tcs = new TaskCompletionSource<ImageResponse[]>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __fetchAlbumPhotos_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse[]");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotos called");
            await Task.CompletedTask;
            return new ImageResponse[0];
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotos_Internal(string options, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ImageResponse> OpenCamera(OpenCameraOptions options = null)
#else
        public static async Task<ImageResponse> OpenCamera(OpenCameraOptions options = null)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<ImageResponse>();
#else
            var tcs = new TaskCompletionSource<ImageResponse>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __openCamera_Internal(AITJsonSettings.Serialize(options), callbackId, "ImageResponse");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenCamera called");
            await Task.CompletedTask;
            return default(ImageResponse);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCamera_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="paramsParam">저장할 데이터와 파일 정보를 담은 객체예요.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> SaveBase64Data(SaveBase64DataParams paramsParam)
#else
        public static async Task<bool> SaveBase64Data(SaveBase64DataParams paramsParam)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __saveBase64Data_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SaveBase64Data called");
            await Task.CompletedTask;
            return true; // test return value
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __saveBase64Data_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
