// -----------------------------------------------------------------------
// <copyright file="AIT.RequestPermission.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - RequestPermission API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - RequestPermission
    /// </summary>
    public static partial class AIT
    {
        /// <returns>사용자가 선택한 최종 권한 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 경우 denied: 권한이 거부된 경우</returns>
        public static void RequestPermission(object permission, Action<string> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __requestPermission_Internal(permission, callbackId, "string");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestPermission called");
            var mockResult = "";
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestPermission_Internal(object permission, string callbackId, string typeName);
#endif
    }
}
