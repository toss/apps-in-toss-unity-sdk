// -----------------------------------------------------------------------
// <copyright file="AIT.GetSchemeUri.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetSchemeUri API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetSchemeUri
    /// </summary>
    public static partial class AIT
    {
        /// <returns>처음에 화면에 진입한 스킴 값을 반환해요.</returns>
        public static string GetSchemeUri()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __getSchemeUri_Internal();
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetSchemeUri called");
            return "";
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string __getSchemeUri_Internal();
#endif
    }
}
