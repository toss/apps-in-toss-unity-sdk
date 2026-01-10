/**
 * WebGL 수동 API 생성기
 *
 * @apps-in-toss/web-framework에 포함되지 않은 브라우저 API를 위한
 * 수동 C# 및 jslib 코드를 생성합니다.
 */

/**
 * AIT.WebGL.cs 파일 내용 생성
 *
 * devicePixelRatio 등 WebGL 환경 전용 API를 제공합니다.
 */
export function generateWebGLManualCs(): string {
  return `// -----------------------------------------------------------------------
// <copyright file="AIT.WebGL.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - WebGL Manual APIs
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - WebGL
    /// WebGL 환경 전용 유틸리티 API
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
        [APICategory("WebGL")]
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
`;
}

/**
 * AppsInToss-WebGL.jslib 파일 내용 생성
 */
export function generateWebGLManualJslib(): string {
  return `/**
 * AppsInToss-WebGL.jslib
 *
 * WebGL 환경 전용 수동 API (브라우저 API)
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __GetDevicePixelRatio_Internal: function() {
        // Unity config에서 설정된 devicePixelRatio 사용
        // index.html에서 config.devicePixelRatio = getOptimalDevicePixelRatio()로 설정됨
        // unityInstance.Module.devicePixelRatio에 저장됨
        var dpr = 1;

        // 1. Unity Instance에서 설정된 값 확인
        if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.Module) {
            dpr = unityInstance.Module.devicePixelRatio || window.devicePixelRatio || 1;
        }
        // 2. 글로벌 unityConfig에서 확인 (초기화 전)
        else if (typeof unityConfig !== 'undefined' && unityConfig) {
            dpr = unityConfig.devicePixelRatio || window.devicePixelRatio || 1;
        }
        // 3. 브라우저 기본값 사용
        else {
            dpr = window.devicePixelRatio || 1;
        }

        console.log('[AIT jslib] GetDevicePixelRatio:', dpr);
        return dpr;
    },
});
`;
}
