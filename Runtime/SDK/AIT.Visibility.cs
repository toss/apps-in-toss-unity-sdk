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
        public static async Task<System.Action> OnVisibilityChangedByTransparentServiceWeb(System.Action eventParams)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __onVisibilityChangedByTransparentServiceWeb_Internal(string eventParams, string callbackId, string typeName);
#endif
    }
}
