// -----------------------------------------------------------------------
// <copyright file="AITFirebase.Analytics.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Analytics APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
    /// <summary>
    /// Firebase Analytics APIs
    /// </summary>
    public static partial class AITFirebase
    {
        /// <summary>Analytics 이벤트를 기록합니다.</summary>
        /// <param name="eventName">이벤트 이름</param>
        /// <param name="eventParams">이벤트 파라미터 (JSON 문자열)</param>
        [Preserve]
        public static void LogEvent(string eventName, string eventParams = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            __Firebase_logEvent_Internal(eventName, eventParams);
#else
            Debug.Log($"[AITFirebase Mock] LogEvent called");
#endif
        }

        /// <summary>Analytics 사용자 ID를 설정합니다.</summary>
        /// <param name="userId">사용자 ID</param>
        [Preserve]
        public static void SetUserId(string userId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            __Firebase_setUserId_Internal(userId);
#else
            Debug.Log($"[AITFirebase Mock] SetUserId called");
#endif
        }

        /// <summary>Analytics 사용자 속성을 설정합니다.</summary>
        /// <param name="properties">사용자 속성 (JSON 문자열)</param>
        [Preserve]
        public static void SetUserProperties(string properties)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            __Firebase_setUserProperties_Internal(properties);
#else
            Debug.Log($"[AITFirebase Mock] SetUserProperties called");
#endif
        }

        /// <summary>Analytics 데이터 수집을 활성화/비활성화합니다.</summary>
        /// <param name="enabled">수집 활성화 여부</param>
        [Preserve]
        public static void SetAnalyticsCollectionEnabled(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            __Firebase_setAnalyticsCollectionEnabled_Internal(enabled);
#else
            Debug.Log($"[AITFirebase Mock] SetAnalyticsCollectionEnabled called");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_logEvent_Internal(string eventName, string eventParams);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_setUserId_Internal(string userId);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_setUserProperties_Internal(string properties);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_setAnalyticsCollectionEnabled_Internal(bool enabled);
#endif
    }
}
