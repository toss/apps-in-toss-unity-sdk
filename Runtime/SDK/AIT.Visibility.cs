// -----------------------------------------------------------------------
// <copyright file="AIT.Visibility.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Visibility APIs
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
    /// Apps in Toss Platform API - Visibility
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Visibility")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<System.Action> OnVisibilityChangedByTransparentServiceWeb(OnVisibilityChangedByTransparentServiceWebEventParams eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __onVisibilityChangedByTransparentServiceWeb_Internal(AITJsonSettings.Serialize(eventParams), callbackId, "System.Action");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OnVisibilityChangedByTransparentServiceWeb called");
            await Awaitable.NextFrameAsync();
            return default(System.Action);
#endif
        }
#else
        public static async Task<System.Action> OnVisibilityChangedByTransparentServiceWeb(OnVisibilityChangedByTransparentServiceWebEventParams eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __onVisibilityChangedByTransparentServiceWeb_Internal(AITJsonSettings.Serialize(eventParams), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OnVisibilityChangedByTransparentServiceWeb called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __onVisibilityChangedByTransparentServiceWeb_Internal(string eventParams, string callbackId, string typeName);
#endif
    }
}
