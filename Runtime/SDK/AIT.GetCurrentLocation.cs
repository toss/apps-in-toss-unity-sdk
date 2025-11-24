// -----------------------------------------------------------------------
// <copyright file="AIT.GetCurrentLocation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetCurrentLocation API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetCurrentLocation
    /// </summary>
    public static partial class AIT
    {
        public static void GetCurrentLocation(GetCurrentLocationOptions options, Action<Location1> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __getCurrentLocation_Internal(options, callbackId, "Location1");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            var mockResult = default(Location1);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocation_Internal(GetCurrentLocationOptions options, string callbackId, string typeName);
#endif
    }
}
