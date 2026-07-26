// -----------------------------------------------------------------------
// <copyright file="AIT.Environment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Environment APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Environment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> EnvGetDeploymentId(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "EnvGetDeploymentId"
            );
            __envGetDeploymentId_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] EnvGetDeploymentId called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> EnvGetDeploymentId(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "EnvGetDeploymentId"
            );
            __envGetDeploymentId_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] EnvGetDeploymentId called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __envGetDeploymentId_Internal(string callbackId, string typeName);
#endif
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Environment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AppsInTossGlobals> GetAppsInTossGlobals(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<AppsInTossGlobals>();
            string callbackId = AITCore.Instance.RegisterCallback<AppsInTossGlobals>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetAppsInTossGlobals"
            );
            __getAppsInTossGlobals_Internal(callbackId, "AppsInTossGlobals");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetAppsInTossGlobals called");
            await Awaitable.NextFrameAsync();
            return default(AppsInTossGlobals);
#endif
        }
#else
        public static async Task<AppsInTossGlobals> GetAppsInTossGlobals(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AppsInTossGlobals>();
            string callbackId = AITCore.Instance.RegisterCallback<AppsInTossGlobals>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetAppsInTossGlobals"
            );
            __getAppsInTossGlobals_Internal(callbackId, "AppsInTossGlobals");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetAppsInTossGlobals called");
            await Task.CompletedTask;
            return default(AppsInTossGlobals);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getAppsInTossGlobals_Internal(string callbackId, string typeName);
#endif
        /// <param name="minVersions">플랫폼별 최소 버전 요구사항을 지정하는 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>현재 앱 버전이 최소 버전 이상이면 true, 그렇지 않으면 false를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Environment")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> IsMinVersionSupported(IsMinVersionSupportedMinVersions minVersions, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "IsMinVersionSupported"
            );
            __isMinVersionSupported_Internal(AITJsonSettings.Serialize(minVersions), callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] IsMinVersionSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> IsMinVersionSupported(IsMinVersionSupportedMinVersions minVersions, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "IsMinVersionSupported"
            );
            __isMinVersionSupported_Internal(AITJsonSettings.Serialize(minVersions), callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] IsMinVersionSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __isMinVersionSupported_Internal(string minVersions, string callbackId, string typeName);
#endif
    }
}
