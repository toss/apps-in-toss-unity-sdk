// -----------------------------------------------------------------------
// <copyright file="AIT.AppEvents.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - AppEvents APIs
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
    /// Apps in Toss Platform API - AppEvents
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// graniteEvent.backEvent 이벤트를 구독합니다.
        /// </summary>
        /// <param name="onEvent">Event callback when the event is triggered</param>
        /// <param name="onError">Error callback (optional)</param>
        /// <returns>Action to call for unsubscribing from the event</returns>
        [Preserve]
        [APICategory("AppEvents")]
        public static Action GraniteEventSubscribeBackEvent(
            Action onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterVoidSubscriptionCallback(
                onEvent,
                onError
            );
            __GraniteEventSubscribeBackEvent_Internal(subscriptionId, "void");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GraniteEventSubscribeBackEvent subscribed");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GraniteEventSubscribeBackEvent unsubscribed");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GraniteEventSubscribeBackEvent_Internal(string subscriptionId, string typeName);
#endif
        /// <summary>
        /// tdsEvent.navigationAccessoryEvent 이벤트를 구독합니다.
        /// </summary>
        /// <param name="onEvent">Event callback when the event is triggered</param>
        /// <param name="onError">Error callback (optional)</param>
        /// <returns>Action to call for unsubscribing from the event</returns>
        [Preserve]
        [APICategory("AppEvents")]
        public static Action TdsEventSubscribeNavigationAccessoryEvent(
            Action<TdsNavigationAccessoryEventData> onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<TdsNavigationAccessoryEventData>(
                onEvent,
                onError
            );
            __TdsEventSubscribeNavigationAccessoryEvent_Internal(subscriptionId, "TdsNavigationAccessoryEventData");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] TdsEventSubscribeNavigationAccessoryEvent subscribed");
            return () => UnityEngine.Debug.Log($"[AIT Mock] TdsEventSubscribeNavigationAccessoryEvent unsubscribed");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __TdsEventSubscribeNavigationAccessoryEvent_Internal(string subscriptionId, string typeName);
#endif
    }
}
