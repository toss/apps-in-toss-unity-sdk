// -----------------------------------------------------------------------
// <copyright file="AIT.GenerateHapticFeedback.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GenerateHapticFeedback API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GenerateHapticFeedback
    /// </summary>
    public static partial class AIT
    {
        public static void GenerateHapticFeedback(HapticFeedbackOptions options, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __generateHapticFeedback_Internal(options, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GenerateHapticFeedback called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __generateHapticFeedback_Internal(HapticFeedbackOptions options, string callbackId, string typeName);
#endif
    }
}
