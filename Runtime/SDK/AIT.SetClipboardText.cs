// -----------------------------------------------------------------------
// <copyright file="AIT.SetClipboardText.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SetClipboardText API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SetClipboardText
    /// </summary>
    public static partial class AIT
    {
        public static void SetClipboardText(string text, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __setClipboardText_Internal(text, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetClipboardText called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setClipboardText_Internal(string text, string callbackId, string typeName);
#endif
    }
}
