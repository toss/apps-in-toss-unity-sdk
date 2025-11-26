// -----------------------------------------------------------------------
// <copyright file="AIT.GetOperationalEnvironment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetOperationalEnvironment API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetOperationalEnvironment
    /// </summary>
    public static partial class AIT
    {
        /// <returns>현재 운영 환경을 나타내는 문자열이에요. 'toss': 토스 앱에서 실행 중이에요. 'sandbox': 샌드박스 환경에서 실행 중이에요.</returns>
        public static string GetOperationalEnvironment()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __getOperationalEnvironment_Internal();
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetOperationalEnvironment called");
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string __getOperationalEnvironment_Internal();
#endif
    }
}
