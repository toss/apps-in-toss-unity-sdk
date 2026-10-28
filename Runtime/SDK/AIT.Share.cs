// -----------------------------------------------------------------------
// <copyright file="AIT.Share.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Share APIs
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
    /// Apps in Toss Platform API - Share
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 지정한 경로를 토스 앱에서 열 수 있는 공유 링크를 만들어요.
        /// `path`는 `intoss://`로 시작하는 딥링크여야 해요. `ogImageUrl`은 토스앱 Android `5.240.0`, iOS `5.239.0` 이상에서만 동작해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> ShareCreateLink(CreateLinkParams options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ShareCreateLink"
            );
            __ShareCreateLink_Internal(AITJsonSettings.Serialize(options), callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ShareCreateLink called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> ShareCreateLink(CreateLinkParams options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ShareCreateLink"
            );
            __ShareCreateLink_Internal(AITJsonSettings.Serialize(options), callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ShareCreateLink called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ShareCreateLink_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 네이티브 공유 시트를 열어 메시지를 공유해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ShareSendMessage(ShareSendMessageMessage message, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ShareSendMessage"
            );
            __ShareSendMessage_Internal(AITJsonSettings.Serialize(message), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ShareSendMessage called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ShareSendMessage(ShareSendMessageMessage message, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ShareSendMessage"
            );
            __ShareSendMessage_Internal(AITJsonSettings.Serialize(message), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ShareSendMessage called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ShareSendMessage_Internal(string message, string callbackId, string typeName);
#endif
        /// <summary>
        /// 바이럴 캠페인 정보를 조회해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ViralGetCampaignResult> ViralGetCampaign(ViralGetCampaignOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ViralGetCampaignResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ViralGetCampaignResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ViralGetCampaign"
            );
            __ViralGetCampaign_Internal(AITJsonSettings.Serialize(options), callbackId, "ViralGetCampaignResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ViralGetCampaign called");
            await Awaitable.NextFrameAsync();
            return default(ViralGetCampaignResult);
#endif
        }
#else
        public static async Task<ViralGetCampaignResult> ViralGetCampaign(ViralGetCampaignOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ViralGetCampaignResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ViralGetCampaignResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ViralGetCampaign"
            );
            __ViralGetCampaign_Internal(AITJsonSettings.Serialize(options), callbackId, "ViralGetCampaignResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ViralGetCampaign called");
            await Task.CompletedTask;
            return default(ViralGetCampaignResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ViralGetCampaign_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 바이럴 캠페인을 카카오톡으로 공유해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ViralShare(ViralShareOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ViralShare"
            );
            __ViralShare_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ViralShare called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ViralShare(ViralShareOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ViralShare"
            );
            __ViralShare_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ViralShare called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ViralShare_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 바이럴 캠페인 보상 지급 시점을 기록해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ViralTrack(TrackViralParams options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "ViralTrack"
            );
            __ViralTrack_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ViralTrack called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task ViralTrack(TrackViralParams options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ViralTrack"
            );
            __ViralTrack_Internal(AITJsonSettings.Serialize(options), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ViralTrack called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __ViralTrack_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// 친구에게 공유하고 리워드를 받을 수 있는 기능을 제공해요.
        /// `options.moduleId`는 콘솔 공유 리워드 UUID예요. 반환되는 cleanup 함수로 구독을 해제해야 해요.
        /// 미지원 앱 버전이면 아무 동작 없이 빈 cleanup 함수를 반환해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Share")]
        public static Action ContactsViral(
            Action<ContactsViralEvent> onEvent,
            ContactsViralParamsOptions options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ContactsViralEvent>(
                onEvent,
                onError
            );
            __contactsViral_Internal(AITJsonSettings.Serialize(options), subscriptionId, "ContactsViralEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ContactsViral called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] ContactsViral cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __contactsViral_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `contactsViral`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> ContactsViralIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "ContactsViralIsSupported"
            );
            __contactsViralIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] ContactsViralIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> ContactsViralIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "ContactsViralIsSupported"
            );
            __contactsViralIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ContactsViralIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __contactsViralIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 연락처 목록을 페이지 단위로 가져와요. `offset`은 첫 호출에 `0`, 이후 `nextOffset`을 사용해요.
        /// 권한이 없으면 `FetchContactsPermissionError`가 발생해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetContacts")]
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<ContactResult> FetchContacts(FetchContactsOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<ContactResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ContactResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchContacts"
            );
            __fetchContacts_Internal(AITJsonSettings.Serialize(options), callbackId, "ContactResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchContacts called");
            await Awaitable.NextFrameAsync();
            return default(ContactResult);
#endif
        }
#else
        public static async Task<ContactResult> FetchContacts(FetchContactsOptions options, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ContactResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ContactResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchContacts"
            );
            __fetchContacts_Internal(AITJsonSettings.Serialize(options), callbackId, "ContactResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchContacts called");
            await Task.CompletedTask;
            return default(ContactResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchContacts_Internal(string options, string callbackId, string typeName);
#endif
        /// <summary>
        /// `fetchContacts`에 필요한 권한의 현재 상태를 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetContactsGetPermission")]
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<PermissionStatus> FetchContactsGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchContactsGetPermission"
            );
            __fetchContactsGetPermission_Internal(callbackId, "PermissionStatus");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchContactsGetPermission called");
            await Awaitable.NextFrameAsync();
            return default(PermissionStatus);
#endif
        }
#else
        public static async Task<PermissionStatus> FetchContactsGetPermission(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<PermissionStatus>();
            string callbackId = AITCore.Instance.RegisterCallback<PermissionStatus>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchContactsGetPermission"
            );
            __fetchContactsGetPermission_Internal(callbackId, "PermissionStatus");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchContactsGetPermission called");
            await Task.CompletedTask;
            return default(PermissionStatus);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchContactsGetPermission_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// `fetchContacts`에 필요한 권한 요청 다이얼로그를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use DeviceGetContactsOpenPermissionDialog")]
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> FetchContactsOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "FetchContactsOpenPermissionDialog"
            );
            __fetchContactsOpenPermissionDialog_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] FetchContactsOpenPermissionDialog called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> FetchContactsOpenPermissionDialog(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "FetchContactsOpenPermissionDialog"
            );
            __fetchContactsOpenPermissionDialog_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchContactsOpenPermissionDialog called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchContactsOpenPermissionDialog_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 지정한 경로를 토스 앱에서 열 수 있는 공유 링크를 만들어요.
        /// `path`는 `intoss://`로 시작하는 딥링크여야 해요. `ogImageUrl`은 토스앱 Android `5.240.0`, iOS `5.239.0` 이상에서만 동작해요.
        /// </summary>
        /// <param name="path">딥링크로 열고 싶은 경로예요. intoss://로 시작하는 문자열이어야 해요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>deep_link_value가 포함된 토스 공유 링크를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ShareCreateLink")]
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetTossShareLink(string path, string ogImageUrl = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetTossShareLink"
            );
            __getTossShareLink_Internal(path, ogImageUrl, callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetTossShareLink called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetTossShareLink(string path, string ogImageUrl = null, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetTossShareLink"
            );
            __getTossShareLink_Internal(path, ogImageUrl, callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetTossShareLink called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getTossShareLink_Internal(string path, string ogImageUrl, string callbackId, string typeName);
#endif
        /// <summary>
        /// 네이티브 공유 시트를 열어 메시지를 공유해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use ShareSendMessage")]
        [Preserve]
        [APICategory("Share")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable Share(ShareMessage message, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "Share"
            );
            __share_Internal(AITJsonSettings.Serialize(message), callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] Share called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task Share(ShareMessage message, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "Share"
            );
            __share_Internal(AITJsonSettings.Serialize(message), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] Share called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __share_Internal(string message, string callbackId, string typeName);
#endif
    }
}
