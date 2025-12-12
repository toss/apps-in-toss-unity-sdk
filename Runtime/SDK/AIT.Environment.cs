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
        public static async Task<AppsInTossGlobals> GetAppsInTossGlobals()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AppsInTossGlobals>();
            string callbackId = AITCore.Instance.RegisterCallback<AppsInTossGlobals>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getAppsInTossGlobals_Internal(string callbackId, string typeName);
#endif
        /// <param name="minVersions">플랫폼별 최소 버전 요구사항을 지정하는 객체예요.</param>
        /// <returns>현재 앱 버전이 최소 버전 이상이면 true, 그렇지 않으면 false를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Environment")]
        public static async Task<bool> IsMinVersionSupported(IsMinVersionSupportedMinVersions minVersions)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __isMinVersionSupported_Internal(string minVersions, string callbackId, string typeName);
#endif
    }
}
