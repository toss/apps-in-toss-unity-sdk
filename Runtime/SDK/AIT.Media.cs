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
        public static async Task<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotos_Internal(string options, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
        public static async Task<ImageResponse> OpenCamera(OpenCameraOptions options)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCamera_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="paramsParam">저장할 데이터와 파일 정보를 담은 객체예요.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Media")]
        public static async Task SaveBase64Data(SaveBase64DataParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __saveBase64Data_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
