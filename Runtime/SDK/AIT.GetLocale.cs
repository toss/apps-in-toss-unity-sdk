// -----------------------------------------------------------------------
// <copyright file="AIT.GetLocale.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetLocale API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetLocale
    /// </summary>
    public static partial class AIT
    {
        /// <returns>사용자의 로케일 정보를 반환해요.</returns>
        public static string GetLocale()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __getLocale_Internal();
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetLocale called");
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string __getLocale_Internal();
#endif
    }
}
