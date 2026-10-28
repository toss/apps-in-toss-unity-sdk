// -----------------------------------------------------------------------
// <copyright file="AIT.Authentication.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Authentication APIs
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
    /// Apps in Toss Platform API - Authentication
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 토스 인증으로 로그인해요. 로그인이 완료되면 다시 토스 앱으로 이동해요.
        /// 반환된 `authorizationCode`와 `referrer`는 서버로 전달해야 해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AppLoginResult> TossAuthLogin(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAuthLogin"
            );
            __TossAuthLogin_Internal(callbackId, "AppLoginResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthLogin called");
            await Awaitable.NextFrameAsync();
            return default(AppLoginResult);
#endif
        }
#else
        public static async Task<AppLoginResult> TossAuthLogin(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAuthLogin"
            );
            __TossAuthLogin_Internal(callbackId, "AppLoginResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthLogin called");
            await Task.CompletedTask;
            return default(AppLoginResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAuthLogin_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 유저가 토스 로그인을 연동했는지 여부를 확인해요.
        /// 토스 로그인을 쓰지 않는 미니앱에서 호출하면 설정 오류로 throw될 수 있어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>결과 값 또는 null</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool?> TossAuthIsIntegrated(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool?>();
            string callbackId = AITCore.Instance.RegisterCallback<bool?>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAuthIsIntegrated"
            );
            __TossAuthIsIntegrated_Internal(callbackId, "bool?");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthIsIntegrated called");
            await Awaitable.NextFrameAsync();
            return null;
#endif
        }
#else
        public static async Task<bool?> TossAuthIsIntegrated(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool?>();
            string callbackId = AITCore.Instance.RegisterCallback<bool?>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAuthIsIntegrated"
            );
            __TossAuthIsIntegrated_Internal(callbackId, "bool?");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthIsIntegrated called");
            await Task.CompletedTask;
            return null;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAuthIsIntegrated_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAuth.isIntegrated`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAuthIsIntegratedIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAuthIsIntegratedIsSupported"
            );
            __TossAuthIsIntegratedIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthIsIntegratedIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAuthIsIntegratedIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAuthIsIntegratedIsSupported"
            );
            __TossAuthIsIntegratedIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthIsIntegratedIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAuthIsIntegratedIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 토스 인증서를 사용해 서명해요. 본인확인·간편인증·전자서명의 `txId`가 필요해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable TossAuthSign(AppsInTossSignTossCertParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "TossAuthSign"
            );
            __TossAuthSign_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthSign called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task TossAuthSign(AppsInTossSignTossCertParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAuthSign"
            );
            __TossAuthSign_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthSign called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAuthSign_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `TossAuth.sign`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> TossAuthSignIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "TossAuthSignIsSupported"
            );
            __TossAuthSignIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthSignIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> TossAuthSignIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "TossAuthSignIsSupported"
            );
            __TossAuthSignIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TossAuthSignIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TossAuthSignIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 토스 인증으로 로그인해요. 로그인이 완료되면 다시 토스 앱으로 이동해요.
        /// 반환된 `authorizationCode`와 `referrer`는 서버로 전달해야 해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossAuthLogin")]
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AppLoginResult> AppLogin(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "AppLogin"
            );
            __appLogin_Internal(callbackId, "AppLoginResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] AppLogin called");
            await Awaitable.NextFrameAsync();
            return default(AppLoginResult);
#endif
        }
#else
        public static async Task<AppLoginResult> AppLogin(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "AppLogin"
            );
            __appLogin_Internal(callbackId, "AppLoginResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] AppLogin called");
            await Task.CompletedTask;
            return default(AppLoginResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __appLogin_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 유저가 토스 로그인을 연동했는지 여부를 확인해요.
        /// 토스 로그인을 쓰지 않는 미니앱에서 호출하면 설정 오류로 throw될 수 있어요.
        /// 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>토스 로그인이 연동된 유저인지 여부를 반환해요. true: 토스 로그인이 연동된 유저에요. false: 토스 로그인이 연동되지 않은 유저에요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossAuthIsIntegrated")]
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool?> GetIsTossLoginIntegratedService(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool?>();
            string callbackId = AITCore.Instance.RegisterCallback<bool?>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetIsTossLoginIntegratedService"
            );
            __getIsTossLoginIntegratedService_Internal(callbackId, "bool?");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetIsTossLoginIntegratedService called");
            await Awaitable.NextFrameAsync();
            return null;
#endif
        }
#else
        public static async Task<bool?> GetIsTossLoginIntegratedService(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool?>();
            string callbackId = AITCore.Instance.RegisterCallback<bool?>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetIsTossLoginIntegratedService"
            );
            __getIsTossLoginIntegratedService_Internal(callbackId, "bool?");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetIsTossLoginIntegratedService called");
            await Task.CompletedTask;
            return null;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getIsTossLoginIntegratedService_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `getIsTossLoginIntegratedService`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use TossAuthIsIntegratedIsSupported")]
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GetIsTossLoginIntegratedServiceIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetIsTossLoginIntegratedServiceIsSupported"
            );
            __getIsTossLoginIntegratedServiceIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetIsTossLoginIntegratedServiceIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GetIsTossLoginIntegratedServiceIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetIsTossLoginIntegratedServiceIsSupported"
            );
            __getIsTossLoginIntegratedServiceIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetIsTossLoginIntegratedServiceIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getIsTossLoginIntegratedServiceIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
