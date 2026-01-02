// -----------------------------------------------------------------------
// <copyright file="AIT.Certificate.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Certificate APIs
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
    /// Apps in Toss Platform API - Certificate
    /// </summary>
    public static partial class AIT
    {
        /// <param name="paramsParam">서명에 필요한 파라미터를 포함하는 객체예요.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Certificate")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> AppsInTossSignTossCert(AppsInTossSignTossCertParams paramsParam)
#else
        public static async Task<bool> AppsInTossSignTossCert(AppsInTossSignTossCertParams paramsParam)
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
            __appsInTossSignTossCert_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] AppsInTossSignTossCert called");
            await Task.CompletedTask;
            return true; // test return value
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __appsInTossSignTossCert_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
