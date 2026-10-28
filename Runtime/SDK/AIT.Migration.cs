// -----------------------------------------------------------------------
// <copyright file="AIT.Migration.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Migration APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Migration
    /// </summary>
    public static partial class AIT
    {
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Migration")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<MigrationGetOriginStorageResult> MigrationGetOriginStorage(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<MigrationGetOriginStorageResult>();
            string callbackId = AITCore.Instance.RegisterCallback<MigrationGetOriginStorageResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "MigrationGetOriginStorage"
            );
            __MigrationGetOriginStorage_Internal(callbackId, "MigrationGetOriginStorageResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] MigrationGetOriginStorage called");
            await Awaitable.NextFrameAsync();
            return default(MigrationGetOriginStorageResult);
#endif
        }
#else
        public static async Task<MigrationGetOriginStorageResult> MigrationGetOriginStorage(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<MigrationGetOriginStorageResult>();
            string callbackId = AITCore.Instance.RegisterCallback<MigrationGetOriginStorageResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "MigrationGetOriginStorage"
            );
            __MigrationGetOriginStorage_Internal(callbackId, "MigrationGetOriginStorageResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] MigrationGetOriginStorage called");
            await Task.CompletedTask;
            return default(MigrationGetOriginStorageResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __MigrationGetOriginStorage_Internal(string callbackId, string typeName);
#endif
    }
}
