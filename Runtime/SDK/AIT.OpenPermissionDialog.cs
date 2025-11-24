// -----------------------------------------------------------------------
// <copyright file="AIT.OpenPermissionDialog.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OpenPermissionDialog API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OpenPermissionDialog
    /// </summary>
    public static partial class AIT
    {
        /// <returns>권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요.</returns>
        public static void OpenPermissionDialog(object permission, Action<string> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __openPermissionDialog_Internal(permission, callbackId, "string");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenPermissionDialog called");
            var mockResult = "";
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openPermissionDialog_Internal(object permission, string callbackId, string typeName);
#endif
    }
}
