// -----------------------------------------------------------------------
// <copyright file="AIT.Location.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Location APIs
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
    /// Apps in Toss Platform API - Location
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<Location> GetCurrentLocation(GetCurrentLocationOptions options)
#else
        public static async Task<Location> GetCurrentLocation(GetCurrentLocationOptions options)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<Location>();
#else
            var tcs = new TaskCompletionSource<Location>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getCurrentLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            await Task.CompletedTask;
            return default(Location);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocation_Internal(string options, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<System.Action> StartUpdateLocation(StartUpdateLocationEventParams eventParams)
#else
        public static async Task<System.Action> StartUpdateLocation(StartUpdateLocationEventParams eventParams)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<System.Action>();
#else
            var tcs = new TaskCompletionSource<System.Action>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __startUpdateLocation_Internal(AITJsonSettings.Serialize(eventParams), callbackId, "System.Action");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocation_Internal(string eventParams, string callbackId, string typeName);
#endif
    }
}
