// -----------------------------------------------------------------------
// <copyright file="AIT.GetClipboardText.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetClipboardText API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetClipboardText
    /// </summary>
    public static partial class AIT
    {
        public static void GetClipboardText(Action<string> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __getClipboardText_Internal(callbackId, "string");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardText called");
            var mockResult = "";
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getClipboardText_Internal(string callbackId, string typeName);
#endif
    }
}
