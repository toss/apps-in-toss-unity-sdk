// -----------------------------------------------------------------------
// <copyright file="AIT.Partner.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Partner APIs
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
    /// Apps in Toss Platform API - Partner
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 상단 네비게이션의 악세서리 버튼을 추가해요. callback에 대한 정의는 `tdsEvent.addEventListener("navigationAccessoryEvent", callback)`를 참고해주세요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Partner")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> partnerAddAccessoryButton(AddAccessoryButtonOptions args_0)
#else
        public static async Task<bool> partnerAddAccessoryButton(AddAccessoryButtonOptions args_0)
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __partnerAddAccessoryButton_Internal(AITJsonSettings.Serialize(args_0), callbackId, "void");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] partnerAddAccessoryButton called");
            await Task.CompletedTask;
            return true; // test return value
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __partnerAddAccessoryButton_Internal(string args_0, string callbackId, string typeName);
#endif
        /// <summary>
        /// 상단 네비게이션의 악세서리 버튼을 추가해요. callback에 대한 정의는 `tdsEvent.addEventListener("navigationAccessoryEvent", callback)`를 참고해주세요.
        /// </summary>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Partner")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> partnerRemoveAccessoryButton()
#else
        public static async Task<bool> partnerRemoveAccessoryButton()
#endif
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
            var tcs = new AwaitableCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>();
#endif
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(true),
                error => tcs.TrySetException(error)
            );
            __partnerRemoveAccessoryButton_Internal(callbackId, "void");
#if UNITY_6000_0_OR_NEWER
            return await tcs.Awaitable;
#else
            return await tcs.Task;
#endif
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] partnerRemoveAccessoryButton called");
            await Task.CompletedTask;
            return true; // test return value
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __partnerRemoveAccessoryButton_Internal(string callbackId, string typeName);
#endif
    }
}
