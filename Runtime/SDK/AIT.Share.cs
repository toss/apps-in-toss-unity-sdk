// -----------------------------------------------------------------------
// <copyright file="AIT.Share.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Share APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Share
    /// </summary>
    public static partial class AIT
    {
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
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Share")]
        public static async Task<ContactResult> FetchContacts(FetchContactsOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ContactResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ContactResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchContacts_Internal(string options, string callbackId, string typeName);
#endif
        /// <param name="path">딥링크 경로예요. intoss://로 시작하는 문자열이어야 해요. (예: intoss://my-app, intoss://my-app/detail?id=123)</param>
        /// <param name="ogImageUrl">(선택) 공유 시 표시될 커스텀 OG 이미지 URL이에요. 최소 버전: Android 5.240.0, iOS 5.239.0</param>
        /// <returns>생성된 토스 공유 링크</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Share")]
        public static async Task<string> GetTossShareLink(string path, string ogImageUrl = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getTossShareLink_Internal(string path, string ogImageUrl, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Share")]
        public static async Task Share(ShareMessage message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __share_Internal(string message, string callbackId, string typeName);
#endif
    }
}
