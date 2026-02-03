// -----------------------------------------------------------------------
// <copyright file="AIT.Authentication.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Authentication APIs
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
    /// Apps in Toss Platform API - Authentication
    /// </summary>
    public static partial class AIT
    {
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<AppLoginResult> AppLogin()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
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
        public static async Task<AppLoginResult> AppLogin()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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
        /// <returns>토스 로그인이 연동된 유저인지 여부를 반환해요. true: 토스 로그인이 연동된 유저에요. false: 토스 로그인이 연동되지 않은 유저에요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Authentication")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool?> GetIsTossLoginIntegratedService()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool?>();
            string callbackId = AITCore.Instance.RegisterCallback<bool?>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
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
        public static async Task<bool?> GetIsTossLoginIntegratedService()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool?>();
            string callbackId = AITCore.Instance.RegisterCallback<bool?>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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
    }
}
