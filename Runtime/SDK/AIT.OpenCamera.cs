// -----------------------------------------------------------------------
// <copyright file="AIT.OpenCamera.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OpenCamera API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OpenCamera
    /// </summary>
    public static partial class AIT
    {
        public static Task<ImageResponse> OpenCamera(OpenCameraOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ImageResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<ImageResponse>(result => tcs.SetResult(result));
            __openCamera_Internal(options, callbackId, "ImageResponse");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenCamera called");
            return Task.FromResult(default(ImageResponse));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openCamera_Internal(OpenCameraOptions options, string callbackId, string typeName);
#endif
    }
}
