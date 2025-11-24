// -----------------------------------------------------------------------
// <copyright file="AIT.GetTossAppVersion.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetTossAppVersion API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetTossAppVersion
    /// </summary>
    public static partial class AIT
    {
        /// <returns>토스 앱 버전</returns>
        public static string GetTossAppVersion()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __getTossAppVersion_Internal();
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetTossAppVersion called");
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string __getTossAppVersion_Internal();
#endif
    }
}
