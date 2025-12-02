// -----------------------------------------------------------------------
// <copyright file="AIT.GetPlatformOS.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetPlatformOS API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetPlatformOS
    /// </summary>
    public static partial class AIT
    {
        /// <returns>현재 실행 중인 플랫폼</returns>
        public static Task GetPlatformOS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __getPlatformOS_Internal(callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetPlatformOS called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getPlatformOS_Internal(string callbackId, string typeName);
#endif
    }
}
