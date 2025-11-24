// -----------------------------------------------------------------------
// <copyright file="AIT.FetchAlbumPhotos.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - FetchAlbumPhotos API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - FetchAlbumPhotos
    /// </summary>
    public static partial class AIT
    {
        public static void FetchAlbumPhotos(FetchAlbumPhotosOptions options, Action<ImageResponse[]> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __fetchAlbumPhotos_Internal(options, callbackId, "ImageResponse[]");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchAlbumPhotos called");
            var mockResult = new ImageResponse[0];
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchAlbumPhotos_Internal(FetchAlbumPhotosOptions options, string callbackId, string typeName);
#endif
    }
}
