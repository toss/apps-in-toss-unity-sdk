// -----------------------------------------------------------------------
// <copyright file="AIT.RequestPermission.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - RequestPermission API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - RequestPermission
    /// </summary>
    public static partial class AIT
    {
        /// <returns>사용자가 선택한 최종 권한 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 경우 denied: 권한이 거부된 경우</returns>
        public static Task<string> RequestPermission(RequestPermissionPermission permission)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(result => tcs.SetResult(result));
            __requestPermission_Internal(permission, callbackId, "string");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestPermission called");
            return Task.FromResult("");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestPermission_Internal(RequestPermissionPermission permission, string callbackId, string typeName);
#endif
    }
}
