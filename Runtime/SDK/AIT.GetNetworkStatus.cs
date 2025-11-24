// -----------------------------------------------------------------------
// <copyright file="AIT.GetNetworkStatus.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetNetworkStatus API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetNetworkStatus
    /// </summary>
    public static partial class AIT
    {
        /// <returns>네트워크 상태를 반환해요.</returns>
        public static void GetNetworkStatus(Action<NetworkStatus> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __getNetworkStatus_Internal(callbackId, "NetworkStatus");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetNetworkStatus called");
            var mockResult = default(NetworkStatus);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getNetworkStatus_Internal(string callbackId, string typeName);
#endif
    }
}
