// -----------------------------------------------------------------------
// <copyright file="AIT.OnVisibilityChangedByTransparentServiceWeb.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OnVisibilityChangedByTransparentServiceWeb API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OnVisibilityChangedByTransparentServiceWeb
    /// </summary>
    public static partial class AIT
    {
        public static Task OnVisibilityChangedByTransparentServiceWeb(System.Action eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __onVisibilityChangedByTransparentServiceWeb_Internal(eventParams, callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OnVisibilityChangedByTransparentServiceWeb called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __onVisibilityChangedByTransparentServiceWeb_Internal(System.Action eventParams, string callbackId, string typeName);
#endif
    }
}
