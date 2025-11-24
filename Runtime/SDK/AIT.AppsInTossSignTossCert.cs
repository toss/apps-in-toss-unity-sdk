// -----------------------------------------------------------------------
// <copyright file="AIT.AppsInTossSignTossCert.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - AppsInTossSignTossCert API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - AppsInTossSignTossCert
    /// </summary>
    public static partial class AIT
    {
        /// <param name="paramsParam">서명에 필요한 파라미터를 포함하는 객체예요.</param>
        public static void AppsInTossSignTossCert(AppsInTossSignTossCertParams paramsParam, Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __appsInTossSignTossCert_Internal(paramsParam, callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] AppsInTossSignTossCert called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __appsInTossSignTossCert_Internal(AppsInTossSignTossCertParams paramsParam, string callbackId, string typeName);
#endif
    }
}
