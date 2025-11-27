// -----------------------------------------------------------------------
// <copyright file="AIT.GetPermission.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetPermission API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetPermission
    /// </summary>
    public static partial class AIT
    {
        /// <returns>권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요. notDetermined: 아직 권한 요청에 대한 결정이 이루어지지 않은 상태예요.</returns>
        public static Task<string> GetPermission(GetPermissionPermission permission)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(result => tcs.SetResult(result));
            __getPermission_Internal(permission, callbackId, "string");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetPermission called");
            return Task.FromResult("");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getPermission_Internal(GetPermissionPermission permission, string callbackId, string typeName);
#endif
    }
}
