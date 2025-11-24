// -----------------------------------------------------------------------
// <copyright file="AIT.OpenURL.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OpenURL API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OpenURL
    /// </summary>
    public static partial class AIT
    {
        /// <param name="url">열고자 하는 URL 주소</param>
        /// <returns>URL이 성공적으로 열렸을 때 해결되는 Promise</returns>
        public static void OpenURL(string url, Action<object> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __openURL_Internal(url, callbackId, "object");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenURL called");
            var mockResult = default(object);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openURL_Internal(string url, string callbackId, string typeName);
#endif
    }
}
