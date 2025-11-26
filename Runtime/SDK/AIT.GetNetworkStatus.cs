// -----------------------------------------------------------------------
// <copyright file="AIT.GetNetworkStatus.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetNetworkStatus API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetNetworkStatus
    /// </summary>
    public static partial class AIT
    {
        /// <returns>네트워크 상태를 반환해요.</returns>
        public static Task<NetworkStatus> GetNetworkStatus()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<NetworkStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<NetworkStatus>(result => tcs.SetResult(result));
            __getNetworkStatus_Internal(callbackId, "NetworkStatus");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetNetworkStatus called");
            return Task.FromResult(default(NetworkStatus));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getNetworkStatus_Internal(string callbackId, string typeName);
#endif
    }
}
