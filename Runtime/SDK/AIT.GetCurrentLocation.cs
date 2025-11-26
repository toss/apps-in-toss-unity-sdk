// -----------------------------------------------------------------------
// <copyright file="AIT.GetCurrentLocation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetCurrentLocation API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetCurrentLocation
    /// </summary>
    public static partial class AIT
    {
        public static Task<Location> GetCurrentLocation(GetCurrentLocationOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(result => tcs.SetResult(result));
            __getCurrentLocation_Internal(options, callbackId, "Location");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            return Task.FromResult(default(Location));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocation_Internal(GetCurrentLocationOptions options, string callbackId, string typeName);
#endif
    }
}
