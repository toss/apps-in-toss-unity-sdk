// -----------------------------------------------------------------------
// <copyright file="AIT.OpenGameCenterLeaderboard.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OpenGameCenterLeaderboard API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OpenGameCenterLeaderboard
    /// </summary>
    public static partial class AIT
    {
        /// <returns>리더보드 웹뷰를 호출해요. 앱 버전이 낮으면 아무 동작도 하지 않고 undefined를 반환해요.</returns>
        public static void OpenGameCenterLeaderboard(Action callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __openGameCenterLeaderboard_Internal(callbackId, "void");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboard called");
            callback?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openGameCenterLeaderboard_Internal(string callbackId, string typeName);
#endif
    }
}
