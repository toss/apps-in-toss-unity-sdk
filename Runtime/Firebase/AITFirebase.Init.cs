// -----------------------------------------------------------------------
// <copyright file="AITFirebase.Init.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Init APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
    /// <summary>
    /// Firebase Init APIs
    /// </summary>
    public static partial class AITFirebase
    {
        /// <summary>Firebase를 초기화합니다. firebase-bridge.ts에 설정된 config을 사용합니다.</summary>
        [Preserve]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterVoidCallback(
                () => acs.SetResult(),
                error => acs.SetException(error)
            );
            __Firebase_initializeApp_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            Debug.Log($"[AITFirebase Mock] Initialize called");
            await Awaitable.NextFrameAsync();
#endif
        }
#else
        public static async Task Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterVoidCallback(
                () => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __Firebase_initializeApp_Internal(callbackId, "void");
            await tcs.Task;
#else
            Debug.Log($"[AITFirebase Mock] Initialize called");
            await Task.CompletedTask;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_initializeApp_Internal(string callbackId, string typeName);
#endif
    }
}
