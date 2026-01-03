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
        public static async Task PartnerAddAccessoryButton(AddAccessoryButtonOptions args_0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __partnerAddAccessoryButton_Internal(AITJsonSettings.Serialize(args_0), callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] PartnerAddAccessoryButton called");
            await Task.CompletedTask;
            // void return - nothing to return
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
        public static async Task PartnerRemoveAccessoryButton()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __partnerRemoveAccessoryButton_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] PartnerRemoveAccessoryButton called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __partnerRemoveAccessoryButton_Internal(string callbackId, string typeName);
#endif
    }
}
