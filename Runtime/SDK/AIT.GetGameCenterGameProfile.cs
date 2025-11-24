// -----------------------------------------------------------------------
// <copyright file="AIT.GetGameCenterGameProfile.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetGameCenterGameProfile API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetGameCenterGameProfile
    /// </summary>
    public static partial class AIT
    {
        /// <returns>프로필 정보 또는 undefined를 반환해요.</returns>
        public static void GetGameCenterGameProfile(Action<GameCenterGameProfileResponse> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __getGameCenterGameProfile_Internal(callbackId, "GameCenterGameProfileResponse");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfile called");
            var mockResult = default(GameCenterGameProfileResponse);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGameCenterGameProfile_Internal(string callbackId, string typeName);
#endif
    }
}
