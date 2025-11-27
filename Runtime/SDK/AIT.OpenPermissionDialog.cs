// -----------------------------------------------------------------------
// <copyright file="AIT.OpenPermissionDialog.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OpenPermissionDialog API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OpenPermissionDialog
    /// </summary>
    public static partial class AIT
    {
        /// <returns>권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요.</returns>
        public static Task<string> OpenPermissionDialog(OpenPermissionDialogPermission permission)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(result => tcs.SetResult(result));
            __openPermissionDialog_Internal(permission, callbackId, "string");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenPermissionDialog called");
            return Task.FromResult("");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openPermissionDialog_Internal(OpenPermissionDialogPermission permission, string callbackId, string typeName);
#endif
    }
}
