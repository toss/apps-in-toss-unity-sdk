// -----------------------------------------------------------------------
// <copyright file="AIT.StartUpdateLocation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - StartUpdateLocation API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - StartUpdateLocation
    /// </summary>
    public static partial class AIT
    {
        public static Task StartUpdateLocation(StartUpdateLocationEventParams eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __startUpdateLocation_Internal(eventParams, callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __startUpdateLocation_Internal(StartUpdateLocationEventParams eventParams, string callbackId, string typeName);
#endif
    }
}
