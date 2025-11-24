// -----------------------------------------------------------------------
// <copyright file="AIT.SetSecureScreen.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SetSecureScreen API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SetSecureScreen
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">화면 캡쳐 설정 옵션이에요.</param>
        /// <returns>: boolean} 현재 설정된 캡쳐 차단 상태를 반환해요.</returns>
        public static void SetSecureScreen(object options, Action<object> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __setSecureScreen_Internal(options, callbackId, "object");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetSecureScreen called");
            var mockResult = default(object);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setSecureScreen_Internal(object options, string callbackId, string typeName);
#endif
    }
}
