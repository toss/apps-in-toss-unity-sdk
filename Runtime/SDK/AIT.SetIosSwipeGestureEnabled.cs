// -----------------------------------------------------------------------
// <copyright file="AIT.SetIosSwipeGestureEnabled.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SetIosSwipeGestureEnabled API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SetIosSwipeGestureEnabled
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요.</param>
        public static void SetIosSwipeGestureEnabled(object options, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __setIosSwipeGestureEnabled_Internal(options, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetIosSwipeGestureEnabled called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setIosSwipeGestureEnabled_Internal(object options, string callbackId, string typeName);
#endif
    }
}
