// -----------------------------------------------------------------------
// <copyright file="AIT.SetDeviceOrientation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SetDeviceOrientation API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SetDeviceOrientation
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">화면 방향 설정 값이에요.</param>
        /// <returns>화면 방향 설정이 완료되면 해결되는 Promise를 반환해요.</returns>
        public static void SetDeviceOrientation(object options, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __setDeviceOrientation_Internal(options, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetDeviceOrientation called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setDeviceOrientation_Internal(object options, string callbackId, string typeName);
#endif
    }
}
