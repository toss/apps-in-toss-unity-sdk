// -----------------------------------------------------------------------
// <copyright file="AIT.FetchAlbumPhotos.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - FetchAlbumPhotos API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - FetchAlbumPhotos
    /// </summary>
    public static partial class AIT
    {
        public static Task<ImageResponse[]> FetchAlbumPhotos(FetchAlbumPhotosOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse[]>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse[]>(result => tcs.SetResult(result));
            __fetchAlbumPhotos_Internal(options, callbackId, "ImageResponse[]");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotos called");
            return Task.FromResult(new ImageResponse[0]);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotos_Internal(FetchAlbumPhotosOptions options, string callbackId, string typeName);
#endif
    }
}
