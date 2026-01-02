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
        /// <param name="paramsParam">연락처 공유 기능을 실행할 때 사용하는 파라미터예요. 옵션 설정과 이벤트 핸들러를 포함해요. 자세한 내용은 [ContactsViralParams](/bedrock/reference/native-modules/친구초대/ContactsViralParams.html) 문서를 참고하세요.</param>
        /// <returns>앱브릿지 cleanup 함수를 반환해요. 공유 기능이 끝나면 반드시 이 함수를 호출해서 리소스를 해제해야 해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Share")]
        public static async Task<System.Action> ContactsViral(ContactsViralParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<System.Action>();
            string callbackId = AITCore.Instance.RegisterCallback<System.Action>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __contactsViral_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "System.Action");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ContactsViral called");
            await Task.CompletedTask;
            return default(System.Action);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __contactsViral_Internal(string paramsParam, string callbackId, string typeName);
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
