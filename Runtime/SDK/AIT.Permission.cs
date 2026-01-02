// -----------------------------------------------------------------------
// <copyright file="AIT.Permission.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Permission APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Permission
    /// </summary>
    public static partial class AIT
    {
        /// <returns>권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요. notDetermined: 아직 권한 요청에 대한 결정이 이루어지지 않은 상태예요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Permission")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> GetPermission(GetPermissionPermission permission)
#else
        public static async Task<PermissionStatus> GetPermission(GetPermissionPermission permission)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<PermissionStatus>();
#else
            var tcs = new TaskCompletionSource<PermissionStatus>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getPermission_Internal(AITJsonSettings.Serialize(permission), callbackId, "PermissionStatus");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getPermission_Internal(string permission, string callbackId, string typeName);
#endif
        /// <returns>권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Permission")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> OpenPermissionDialog(OpenPermissionDialogPermission permission)
#else
        public static async Task<string> OpenPermissionDialog(OpenPermissionDialogPermission permission)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<string>();
#else
            var tcs = new TaskCompletionSource<string>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __openPermissionDialog_Internal(AITJsonSettings.Serialize(permission), callbackId, "string");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openPermissionDialog_Internal(string permission, string callbackId, string typeName);
#endif
        /// <returns>사용자가 선택한 최종 권한 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 경우 denied: 권한이 거부된 경우</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Permission")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> RequestPermission(RequestPermissionPermission permission)
#else
        public static async Task<string> RequestPermission(RequestPermissionPermission permission)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<string>();
#else
            var tcs = new TaskCompletionSource<string>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __requestPermission_Internal(AITJsonSettings.Serialize(permission), callbackId, "string");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestPermission called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestPermission_Internal(string permission, string callbackId, string typeName);
#endif
    }
}
