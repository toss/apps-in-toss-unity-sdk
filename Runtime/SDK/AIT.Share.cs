// -----------------------------------------------------------------------
// <copyright file="AIT.Share.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Share API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Share
    /// </summary>
    public static partial class AIT
    {
        public static void Share(object message, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __share_Internal(message, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] Share called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __share_Internal(object message, string callbackId, string typeName);
#endif
    }
}
