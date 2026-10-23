// -----------------------------------------------------------------------
// <copyright file="AIT.VisibilityHelper.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Visibility Helper
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// 브라우저 탭/앱 가시성 상태 관리 helper
    /// </summary>
    /// <remarks>
    /// 브라우저의 document.visibilitychange 이벤트를 Unity에서 쉽게 사용할 수 있도록 합니다.
    ///
    /// 사용 예시:
    /// <code>
    /// AITVisibilityHelper.OnVisibilityChanged += (isVisible) => {
    ///     if (isVisible) {
    ///         audioSource.UnPause();
    ///     } else {
    ///         audioSource.Pause();
    ///     }
    /// };
    /// </code>
    /// </remarks>
    [Preserve]
    public static class AITVisibilityHelper
    {
        private static bool _initialized;

        /// <summary>
        /// 가시성 상태 변경 이벤트
        /// </summary>
        public static event Action<bool> OnVisibilityChanged;

        /// <summary>
        /// 현재 가시성 상태
        /// </summary>
        public static bool IsVisible
        {
            get
            {
                EnsureInitialized();
#if UNITY_WEBGL && !UNITY_EDITOR
                return __AITVisibilityHelper_GetIsVisible_Internal() != 0;
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// 초기화 (AITCore.OnVisibilityStateChangedInternal 이벤트 구독)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            AITCore.OnVisibilityStateChangedInternal += HandleVisibilityChanged;
        }

        private static void HandleVisibilityChanged(bool isVisible)
        {
            Debug.Log($"[AITVisibilityHelper] Visibility changed: {isVisible}");
            OnVisibilityChanged?.Invoke(isVisible);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int __AITVisibilityHelper_GetIsVisible_Internal();
#endif
    }
}
