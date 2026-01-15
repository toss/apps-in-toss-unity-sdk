// -----------------------------------------------------------------------
// <copyright file="AIT.Visibility.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Visibility APIs
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
    /// Apps in Toss Platform API - Visibility
    /// </summary>
    public static partial class AIT
    {
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Visibility")]
        public static Action OnVisibilityChangedByTransparentServiceWeb(
            Action<bool> onEvent,
            object options,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<bool>(
                onEvent,
                onError
            );
            __onVisibilityChangedByTransparentServiceWeb_Internal(AITJsonSettings.Serialize(options), subscriptionId, "bool");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OnVisibilityChangedByTransparentServiceWeb called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] OnVisibilityChangedByTransparentServiceWeb cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __onVisibilityChangedByTransparentServiceWeb_Internal(string options, string subscriptionId, string typeName);
#endif
    }
}
