// -----------------------------------------------------------------------
// <copyright file="AIT.GetPlatformOS.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetPlatformOS API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetPlatformOS
    /// </summary>
    public static partial class AIT
    {
        /// <returns>현재 실행 중인 플랫폼</returns>
        public static string GetPlatformOS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __getPlatformOS_Internal();
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetPlatformOS called");
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string __getPlatformOS_Internal();
#endif
    }
}
