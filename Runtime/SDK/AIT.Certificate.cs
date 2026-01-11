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
        public static async Awaitable AppsInTossSignTossCert(AppsInTossSignTossCertParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __appsInTossSignTossCert_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] AppsInTossSignTossCert called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task AppsInTossSignTossCert(AppsInTossSignTossCertParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __appsInTossSignTossCert_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] AppsInTossSignTossCert called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __appsInTossSignTossCert_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
