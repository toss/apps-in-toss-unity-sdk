// -----------------------------------------------------------------------
// <copyright file="AIT.OpenCamera.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OpenCamera API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OpenCamera
    /// </summary>
    public static partial class AIT
    {
        public static void OpenCamera(OpenCameraOptions options, Action<ImageResponse> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __openCamera_Internal(options, callbackId, "ImageResponse");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenCamera called");
            var mockResult = default(ImageResponse);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCamera_Internal(OpenCameraOptions options, string callbackId, string typeName);
#endif
    }
}
