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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getCurrentLocation_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Location")]
        public static Action StartUpdateLocation(
            Action<Location> onEvent,
            StartUpdateLocationOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<Location>(
                onEvent,
                onError
            );
            __startUpdateLocation_Internal(AITJsonSettings.Serialize(options), subscriptionId, "Location");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocation_Internal(string options, string subscriptionId, string typeName);
#endif
    }
}
