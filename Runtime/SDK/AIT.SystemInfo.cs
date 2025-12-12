// -----------------------------------------------------------------------
// <copyright file="AIT.SystemInfo.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SystemInfo APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SystemInfo
    /// </summary>
    public static partial class AIT
    {
        /// <returns>기기의 고유 식별자를 나타내는 문자열이에요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<string> GetDeviceId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getDeviceId_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetDeviceId called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getDeviceId_Internal(string callbackId, string typeName);
#endif
        /// <returns>사용자의 로케일 정보를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<string> GetLocale()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getLocale_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetLocale called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getLocale_Internal(string callbackId, string typeName);
#endif
        /// <returns>네트워크 상태를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<NetworkStatus> GetNetworkStatus()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<NetworkStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<NetworkStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getNetworkStatus_Internal(callbackId, "NetworkStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetNetworkStatus called");
            await Task.CompletedTask;
            return default(NetworkStatus);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getNetworkStatus_Internal(string callbackId, string typeName);
#endif
        /// <returns>현재 운영 환경을 나타내는 문자열이에요. 'toss': 토스 앱에서 실행 중이에요. 'sandbox': 샌드박스 환경에서 실행 중이에요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<string> GetOperationalEnvironment()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getOperationalEnvironment_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetOperationalEnvironment called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getOperationalEnvironment_Internal(string callbackId, string typeName);
#endif
        /// <returns>현재 실행 중인 플랫폼</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<string> GetPlatformOS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getPlatformOS_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetPlatformOS called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getPlatformOS_Internal(string callbackId, string typeName);
#endif
        /// <returns>처음에 화면에 진입한 스킴 값을 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<string> GetSchemeUri()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getSchemeUri_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetSchemeUri called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getSchemeUri_Internal(string callbackId, string typeName);
#endif
        /// <returns>토스 앱 버전</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("SystemInfo")]
        public static async Task<string> GetTossAppVersion()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getTossAppVersion_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetTossAppVersion called");
            await Task.CompletedTask;
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getTossAppVersion_Internal(string callbackId, string typeName);
#endif
    }
}
