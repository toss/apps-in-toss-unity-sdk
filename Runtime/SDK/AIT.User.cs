// -----------------------------------------------------------------------
// <copyright file="AIT.User.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - User APIs
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
    /// Apps in Toss Platform API - User
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 익명 사용자 키를 받기 위한 일회용 인증 코드를 발급해요.
        /// 반환된 `code`를 파트너사 서버로 보내 교환하면 `hash`를 받을 수 있어요.
        /// 코드는 일회용이며 `expiresIn`(초) 안에 써야 해요.
        /// 교환으로 받은 `hash`는 `User.getAnonymousKey`의 `hash`와 같은 값이에요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<UserCreateAnonymousKeyAuthCodeResult> UserCreateAnonymousKeyAuthCode(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<UserCreateAnonymousKeyAuthCodeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<UserCreateAnonymousKeyAuthCodeResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserCreateAnonymousKeyAuthCode"
            );
            __UserCreateAnonymousKeyAuthCode_Internal(callbackId, "UserCreateAnonymousKeyAuthCodeResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserCreateAnonymousKeyAuthCode called");
            await Awaitable.NextFrameAsync();
            return default(UserCreateAnonymousKeyAuthCodeResult);
#endif
        }
#else
        public static async Task<UserCreateAnonymousKeyAuthCodeResult> UserCreateAnonymousKeyAuthCode(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<UserCreateAnonymousKeyAuthCodeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<UserCreateAnonymousKeyAuthCodeResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserCreateAnonymousKeyAuthCode"
            );
            __UserCreateAnonymousKeyAuthCode_Internal(callbackId, "UserCreateAnonymousKeyAuthCodeResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserCreateAnonymousKeyAuthCode called");
            await Task.CompletedTask;
            return default(UserCreateAnonymousKeyAuthCodeResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserCreateAnonymousKeyAuthCode_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 미니앱에서 사용자의 고유 키를 가져와요. 성공 시 `{ type: 'HASH', hash }`를 반환해요.
        /// 앱 버전이 부족하면 `UNSUPPORTED_APP_VERSION`, 알 수 없는 오류면 `UNKNOWN_ERROR`를 throw해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GetAnonymousKeySuccessResponse> UserGetAnonymousKey(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GetAnonymousKeySuccessResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GetAnonymousKeySuccessResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserGetAnonymousKey"
            );
            __UserGetAnonymousKey_Internal(callbackId, "GetAnonymousKeySuccessResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserGetAnonymousKey called");
            await Awaitable.NextFrameAsync();
            return default(GetAnonymousKeySuccessResponse);
#endif
        }
#else
        public static async Task<GetAnonymousKeySuccessResponse> UserGetAnonymousKey(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetAnonymousKeySuccessResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GetAnonymousKeySuccessResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserGetAnonymousKey"
            );
            __UserGetAnonymousKey_Internal(callbackId, "GetAnonymousKeySuccessResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserGetAnonymousKey called");
            await Task.CompletedTask;
            return default(GetAnonymousKeySuccessResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserGetAnonymousKey_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `User.getAnonymousKey`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> UserGetAnonymousKeyIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserGetAnonymousKeyIsSupported"
            );
            __UserGetAnonymousKeyIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserGetAnonymousKeyIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> UserGetAnonymousKeyIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserGetAnonymousKeyIsSupported"
            );
            __UserGetAnonymousKeyIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserGetAnonymousKeyIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserGetAnonymousKeyIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 사용자 동의 기반 데이터를 요청해요. `consentedUserDataKey`가 필요하고, 동의가 필요하면 약관 웹뷰를 띄워요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>결과 값 또는 null</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<Dictionary<ConsentedUserDataKey, string>> UserGetConsentedData(GetConsentedUserDataOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<Dictionary<ConsentedUserDataKey, string>>();
            string callbackId = AITCore.Instance.RegisterCallback<Dictionary<ConsentedUserDataKey, string>>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserGetConsentedData"
            );
            __UserGetConsentedData_Internal(AITJsonSettings.Serialize(options), callbackId, "Dictionary<ConsentedUserDataKey, string>");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserGetConsentedData called");
            await Awaitable.NextFrameAsync();
            return default(Dictionary<ConsentedUserDataKey, string>);
#endif
        }
#else
        public static async Task<Dictionary<ConsentedUserDataKey, string>> UserGetConsentedData(GetConsentedUserDataOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Dictionary<ConsentedUserDataKey, string>>();
            string callbackId = AITCore.Instance.RegisterCallback<Dictionary<ConsentedUserDataKey, string>>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserGetConsentedData"
            );
            __UserGetConsentedData_Internal(AITJsonSettings.Serialize(options), callbackId, "Dictionary<ConsentedUserDataKey, string>");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserGetConsentedData called");
            await Task.CompletedTask;
            return default(Dictionary<ConsentedUserDataKey, string>);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserGetConsentedData_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `User.getConsentedData`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> UserGetConsentedDataIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserGetConsentedDataIsSupported"
            );
            __UserGetConsentedDataIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserGetConsentedDataIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> UserGetConsentedDataIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserGetConsentedDataIsSupported"
            );
            __UserGetConsentedDataIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserGetConsentedDataIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserGetConsentedDataIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// iOS에서 선언된 연령 범위를 조회해요. iOS 26 이상과 토스앱 5.266.0 이상이 필요하고, `{ ageGates: number[] }`를 넘기세요.
        /// OS 버전이 부족하면 `UNSUPPORTED_OS_VERSION`, 토스앱 버전이 부족하면 `UNSUPPORTED_APP_VERSION` 에러를 던져요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<DeclaredAgeRange> UserGetDeclaredAgeRange(GetDeclaredAgeRangeParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<DeclaredAgeRange>();
            string callbackId = AITCore.Instance.RegisterCallback<DeclaredAgeRange>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserGetDeclaredAgeRange"
            );
            __UserGetDeclaredAgeRange_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "DeclaredAgeRange");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserGetDeclaredAgeRange called");
            await Awaitable.NextFrameAsync();
            return default(DeclaredAgeRange);
#endif
        }
#else
        public static async Task<DeclaredAgeRange> UserGetDeclaredAgeRange(GetDeclaredAgeRangeParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<DeclaredAgeRange>();
            string callbackId = AITCore.Instance.RegisterCallback<DeclaredAgeRange>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserGetDeclaredAgeRange"
            );
            __UserGetDeclaredAgeRange_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "DeclaredAgeRange");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserGetDeclaredAgeRange called");
            await Task.CompletedTask;
            return default(DeclaredAgeRange);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserGetDeclaredAgeRange_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `User.getDeclaredAgeRange`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> UserGetDeclaredAgeRangeIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "UserGetDeclaredAgeRangeIsSupported"
            );
            __UserGetDeclaredAgeRangeIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] UserGetDeclaredAgeRangeIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> UserGetDeclaredAgeRangeIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "UserGetDeclaredAgeRangeIsSupported"
            );
            __UserGetDeclaredAgeRangeIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] UserGetDeclaredAgeRangeIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __UserGetDeclaredAgeRangeIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 미니앱에서 사용자의 고유 키를 가져와요. 성공 시 `{ type: 'HASH', hash }`를 반환해요.
        /// 알 수 없는 오류가 발생하면 `'ERROR'`를, 앱 버전이 최소 지원 버전보다 낮으면 `undefined`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>사용자 키 조회 결과를 반환해요. GetAnonymousKeySuccessResponse: 사용자 키 조회에 성공했어요. { type: 'HASH', hash: string } 형태로 반환돼요. 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetAnonymousKey")]
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GetAnonymousKeyResult> GetAnonymousKey(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GetAnonymousKeyResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetAnonymousKeyResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetAnonymousKey"
            );
            __getAnonymousKey_Internal(callbackId, "GetAnonymousKeyResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetAnonymousKey called");
            await Awaitable.NextFrameAsync();
            return default(GetAnonymousKeyResult);
#endif
        }
#else
        public static async Task<GetAnonymousKeyResult> GetAnonymousKey(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetAnonymousKeyResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetAnonymousKeyResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetAnonymousKey"
            );
            __getAnonymousKey_Internal(callbackId, "GetAnonymousKeyResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetAnonymousKey called");
            await Task.CompletedTask;
            return default(GetAnonymousKeyResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getAnonymousKey_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `getAnonymousKey`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetAnonymousKeyIsSupported")]
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GetAnonymousKeyIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetAnonymousKeyIsSupported"
            );
            __getAnonymousKeyIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetAnonymousKeyIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GetAnonymousKeyIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetAnonymousKeyIsSupported"
            );
            __getAnonymousKeyIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetAnonymousKeyIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getAnonymousKeyIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 사용자 동의 기반 데이터를 요청해요. `consentedUserDataKey`가 필요하고, 동의가 필요하면 약관 웹뷰를 띄워요.
        /// 미지원 토스 앱 버전에서는 undefined를 반환해요.
        /// </summary>
        /// <param name="options">동의 데이터 요청 옵션이에요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>서버에서 제공하는 사용자 데이터를 반환해요. 토스앱 버전이 5.264.0 미만이면 undefined를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetConsentedData")]
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<Dictionary<ConsentedUserDataKey, string>> GetConsentedUserData(GetConsentedUserDataOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<Dictionary<ConsentedUserDataKey, string>>();
            string callbackId = AITCore.Instance.RegisterCallback<Dictionary<ConsentedUserDataKey, string>>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetConsentedUserData"
            );
            __getConsentedUserData_Internal(AITJsonSettings.Serialize(options), callbackId, "Dictionary<ConsentedUserDataKey, string>");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetConsentedUserData called");
            await Awaitable.NextFrameAsync();
            return default(Dictionary<ConsentedUserDataKey, string>);
#endif
        }
#else
        public static async Task<Dictionary<ConsentedUserDataKey, string>> GetConsentedUserData(GetConsentedUserDataOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Dictionary<ConsentedUserDataKey, string>>();
            string callbackId = AITCore.Instance.RegisterCallback<Dictionary<ConsentedUserDataKey, string>>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetConsentedUserData"
            );
            __getConsentedUserData_Internal(AITJsonSettings.Serialize(options), callbackId, "Dictionary<ConsentedUserDataKey, string>");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetConsentedUserData called");
            await Task.CompletedTask;
            return default(Dictionary<ConsentedUserDataKey, string>);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getConsentedUserData_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `getConsentedUserData`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetConsentedDataIsSupported")]
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GetConsentedUserDataIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetConsentedUserDataIsSupported"
            );
            __getConsentedUserDataIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetConsentedUserDataIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GetConsentedUserDataIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetConsentedUserDataIsSupported"
            );
            __getConsentedUserDataIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetConsentedUserDataIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getConsentedUserDataIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// iOS에서 선언된 연령 범위를 조회해요. iOS 26 이상과 토스앱 5.266.0 이상이 필요하고, `{ ageGates: number[] }`를 넘기세요.
        /// `isSupported()`로 지원 여부를 확인할 수 있어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetDeclaredAgeRange")]
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<DeclaredAgeRange> GetDeclaredAgeRange(GetDeclaredAgeRangeParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<DeclaredAgeRange>();
            string callbackId = AITCore.Instance.RegisterCallback<DeclaredAgeRange>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetDeclaredAgeRange"
            );
            __getDeclaredAgeRange_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "DeclaredAgeRange");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetDeclaredAgeRange called");
            await Awaitable.NextFrameAsync();
            return default(DeclaredAgeRange);
#endif
        }
#else
        public static async Task<DeclaredAgeRange> GetDeclaredAgeRange(GetDeclaredAgeRangeParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<DeclaredAgeRange>();
            string callbackId = AITCore.Instance.RegisterCallback<DeclaredAgeRange>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetDeclaredAgeRange"
            );
            __getDeclaredAgeRange_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "DeclaredAgeRange");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetDeclaredAgeRange called");
            await Task.CompletedTask;
            return default(DeclaredAgeRange);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getDeclaredAgeRange_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `getDeclaredAgeRange`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetDeclaredAgeRangeIsSupported")]
        [Preserve]
        [APICategory("User")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GetDeclaredAgeRangeIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetDeclaredAgeRangeIsSupported"
            );
            __getDeclaredAgeRangeIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetDeclaredAgeRangeIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GetDeclaredAgeRangeIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetDeclaredAgeRangeIsSupported"
            );
            __getDeclaredAgeRangeIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetDeclaredAgeRangeIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getDeclaredAgeRangeIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
