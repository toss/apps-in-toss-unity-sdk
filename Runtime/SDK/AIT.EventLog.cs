// -----------------------------------------------------------------------
// <copyright file="AIT.EventLog.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - EventLog API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - EventLog
    /// </summary>
    public static partial class AIT
    {
        /// <param name="paramsParam">로그 기록에 필요한 매개변수 객체예요.</param>
        /// <returns>로그 기록이 완료되면 해결되는 Promise예요.</returns>
        public static void EventLog(EventLogParams paramsParam, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __eventLog_Internal(paramsParam, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] EventLog called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __eventLog_Internal(EventLogParams paramsParam, string callbackId, string typeName);
#endif
    }
}
