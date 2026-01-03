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
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __getCurrentLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            await Awaitable.NextFrameAsync();
            return default(Location);
#endif
        }
#else
        public static async Task<Location> GetCurrentLocation(GetCurrentLocationOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Location>();
            string callbackId = AITCore.Instance.RegisterCallback<Location>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getCurrentLocation_Internal(AITJsonSettings.Serialize(options), callbackId, "Location");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetCurrentLocation called");
            await Task.CompletedTask;
            return default(Location);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocation_Internal(string options, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Location")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<System.Action> StartUpdateLocation(StartUpdateLocationEventParams eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __startUpdateLocation_Internal(AITJsonSettings.Serialize(eventParams), callbackId, "System.Action");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            await Awaitable.NextFrameAsync();
            return default(System.Action);
#endif
        }
#else
        public static async Task<System.Action> StartUpdateLocation(StartUpdateLocationEventParams eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __startUpdateLocation_Internal(AITJsonSettings.Serialize(eventParams), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocation_Internal(string eventParams, string callbackId, string typeName);
#endif
    }
}
