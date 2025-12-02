// -----------------------------------------------------------------------
// <copyright file="AIT.GetSchemeUri.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetSchemeUri API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetSchemeUri
    /// </summary>
    public static partial class AIT
    {
        /// <returns>처음에 화면에 진입한 스킴 값을 반환해요.</returns>
        public static Task GetSchemeUri()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __getSchemeUri_Internal(callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetSchemeUri called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getSchemeUri_Internal(string callbackId, string typeName);
#endif
    }
}
