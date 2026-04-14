// -----------------------------------------------------------------------
// <copyright file="AIT.Screen.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Screen APIs
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Screen
    /// 화면 관련 유틸리티 API
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 현재 WebGL 캔버스에 적용된 devicePixelRatio를 반환합니다.
        /// SafeAreaInsets 값을 Unity Screen 좌표로 변환할 때 사용합니다.
        /// </summary>
        /// <remarks>
        /// SafeAreaInsets API는 CSS 픽셀 단위로 값을 반환합니다.
        /// Unity의 Screen.width/Screen.height는 device 픽셀 단위입니다.
        /// Safe area 값을 Unity 좌표로 변환하려면 이 값을 곱해야 합니다.
        ///
        /// 예시:
        /// <code>
        /// var insets = await AIT.SafeAreaInsetsGet();
        /// var dpr = AIT.GetDevicePixelRatio();
        /// float topInDevicePixels = (float)(insets.Top * dpr);
        /// </code>
        /// </remarks>
        /// <returns>devicePixelRatio 값 (Unity Editor에서는 항상 1.0)</returns>
        [Preserve]
        [APICategory("Screen")]
        public static double GetDevicePixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __GetDevicePixelRatio_Internal();
#else
            // Unity Editor에서는 1.0 반환
            UnityEngine.Debug.Log("[AIT Mock] GetDevicePixelRatio called, returning 1.0");
            return 1.0;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern double __GetDevicePixelRatio_Internal();
#endif
    }
}
