// -----------------------------------------------------------------------
// <copyright file="AIT.Advertising.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Advertising APIs
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
    /// Apps in Toss Platform API - Advertising
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobLoadAppsInTossAdMob(
            Action<LoadAdMobEvent> onEvent,
            LoadAdMobOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<LoadAdMobEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobLoadAppsInTossAdMob_Internal(AITJsonSettings.Serialize(options), subscriptionId, "LoadAdMobEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMob called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobLoadAppsInTossAdMob cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobLoadAppsInTossAdMob_Internal(string options, string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 광고를 사용자에게 노출해요. 이 함수는 `loadAppsInTossAdMob` 로 미리 불러온 광고를 실제로 사용자에게 노출해요.
        /// </summary>
        /// <param name="onEvent">이벤트 콜백</param>
        /// <param name="onError">에러 콜백</param>
        /// <returns>구독 취소를 위한 Action</returns>
        [Preserve]
        [APICategory("Advertising")]
        public static Action GoogleAdMobShowAppsInTossAdMob(
            Action<ShowAdMobEvent> onEvent,
            ShowAdMobOptions options = null,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<ShowAdMobEvent>(
                onEvent,
                onError
            );
            __GoogleAdMobShowAppsInTossAdMob_Internal(AITJsonSettings.Serialize(options), subscriptionId, "ShowAdMobEvent");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMob called");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GoogleAdMobShowAppsInTossAdMob cancelled");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GoogleAdMobShowAppsInTossAdMob_Internal(string options, string subscriptionId, string typeName);
#endif
    }
}
