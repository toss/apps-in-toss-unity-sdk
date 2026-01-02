// -----------------------------------------------------------------------
// <copyright file="AIT.Environment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Environment APIs
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
    /// Apps in Toss Platform API - Environment
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Environment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> envGetDeploymentId()
#else
        public static async Task<string> envGetDeploymentId()
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
            __envGetDeploymentId_Internal(callbackId, "string");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] envGetDeploymentId called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __envGetDeploymentId_Internal(string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Environment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AppsInTossGlobals> GetAppsInTossGlobals()
#else
        public static async Task<AppsInTossGlobals> GetAppsInTossGlobals()
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<AppsInTossGlobals>();
#else
            var tcs = new TaskCompletionSource<AppsInTossGlobals>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<AppsInTossGlobals>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getAppsInTossGlobals_Internal(callbackId, "AppsInTossGlobals");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetAppsInTossGlobals called");
            await Task.CompletedTask;
            return default(AppsInTossGlobals);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getAppsInTossGlobals_Internal(string callbackId, string typeName);
#endif
        /// <param name="minVersions">플랫폼별 최소 버전 요구사항을 지정하는 객체예요.</param>
        /// <returns>현재 앱 버전이 최소 버전 이상이면 true, 그렇지 않으면 false를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Environment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IsMinVersionSupported(IsMinVersionSupportedMinVersions minVersions)
#else
        public static async Task<bool> IsMinVersionSupported(IsMinVersionSupportedMinVersions minVersions)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __isMinVersionSupported_Internal(AITJsonSettings.Serialize(minVersions), callbackId, "bool");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IsMinVersionSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __isMinVersionSupported_Internal(string minVersions, string callbackId, string typeName);
#endif
    }
}
