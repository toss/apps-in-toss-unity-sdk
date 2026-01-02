// -----------------------------------------------------------------------
// <copyright file="AIT.Events.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Events APIs
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
    /// Apps in Toss Platform API - Events
    /// </summary>
    public static partial class AIT
    {
        /// <param name="paramsParam">로그 기록에 필요한 매개변수 객체예요.</param>
        /// <returns>로그 기록이 완료되면 해결되는 Promise예요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Events")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> EventLog(EventLogParams paramsParam)
#else
        public static async Task<bool> EventLog(EventLogParams paramsParam)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __eventLog_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] EventLog called");
            await Task.CompletedTask;
            return true; // test return value
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __eventLog_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
