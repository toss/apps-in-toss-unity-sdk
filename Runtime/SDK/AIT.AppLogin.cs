// -----------------------------------------------------------------------
// <copyright file="AIT.AppLogin.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - AppLogin API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - AppLogin
    /// </summary>
    public static partial class AIT
    {
        public static void AppLogin(Action<object> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __appLogin_Internal(callbackId, "object");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] AppLogin called");
            var mockResult = default(object);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __appLogin_Internal(string callbackId, string typeName);
#endif
    }
}
