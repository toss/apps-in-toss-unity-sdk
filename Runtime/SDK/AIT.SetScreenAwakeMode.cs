// -----------------------------------------------------------------------
// <copyright file="AIT.SetScreenAwakeMode.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SetScreenAwakeMode API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SetScreenAwakeMode
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">화면 항상 켜짐 모드의 설정 값이에요.</param>
        /// <returns>현재 화면 항상 켜짐 모드의 설정 상태를 반환해요.</returns>
        public static void SetScreenAwakeMode(object options, Action<object> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __setScreenAwakeMode_Internal(options, callbackId, "object");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetScreenAwakeMode called");
            var mockResult = default(object);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setScreenAwakeMode_Internal(object options, string callbackId, string typeName);
#endif
    }
}
