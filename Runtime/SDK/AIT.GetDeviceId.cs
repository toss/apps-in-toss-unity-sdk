// -----------------------------------------------------------------------
// <copyright file="AIT.GetDeviceId.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetDeviceId API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetDeviceId
    /// </summary>
    public static partial class AIT
    {
        /// <returns>기기의 고유 식별자를 나타내는 문자열이에요.</returns>
        public static string GetDeviceId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __getDeviceId_Internal();
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetDeviceId called");
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string __getDeviceId_Internal();
#endif
    }
}
