// -----------------------------------------------------------------------
// <copyright file="AIT.Events.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Events APIs
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
    /// Apps in Toss Platform API - Events
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 앱에서 발생하는 이벤트를 기록해요.
        /// 
        /// `params`에는 `anonymous_key`가 기본 파라미터로 자동 포함되고, `undefined`, `null` 값은
        /// 제외된 뒤 나머지는 string으로 정규화돼요. sandbox 환경에서는 콘솔에만 출력되고
        /// 전송되지 않아요. 미지원 앱 버전에서는 에러 없이 조용히 무시돼요.
        /// </summary>
        /// <param name="paramsParam">로그 기록에 필요한 매개변수 객체예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>로그 기록이 완료되면 해결되는 Promise예요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use AnalyticsLog")]
        [Preserve]
        [APICategory("Events")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable EventLog(EventLogParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "EventLog"
            );
            __eventLog_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] EventLog called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task EventLog(EventLogParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "EventLog"
            );
            __eventLog_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] EventLog called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __eventLog_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
