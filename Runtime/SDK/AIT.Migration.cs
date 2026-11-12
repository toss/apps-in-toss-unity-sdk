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
        /// <summary>
        /// 이전 origin과 현재 origin의 localStorage, IndexedDB, OPFS 덤프를 가져와요. SDK 3.0.x origin에 저장된 데이터를 3.1.0 이상으로 옮길 때 사용해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>previous(이전 origin)와 current(현재 origin) 덤프를 반환해요. 읽지 못한 저장소는 각 덤프의 Errors에 담겨요. IndexedDB 레코드의 바이너리 값은 base64 문자열로 와요.</returns>
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
