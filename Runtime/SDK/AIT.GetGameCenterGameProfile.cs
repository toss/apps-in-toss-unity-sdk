// -----------------------------------------------------------------------
// <copyright file="AIT.GetGameCenterGameProfile.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetGameCenterGameProfile API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetGameCenterGameProfile
    /// </summary>
    public static partial class AIT
    {
        /// <returns>프로필 정보 또는 undefined를 반환해요.</returns>
        public static Task<GameCenterGameProfileResponse> GetGameCenterGameProfile()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(result => tcs.SetResult(result));
            __getGameCenterGameProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfile called");
            return Task.FromResult(default(GameCenterGameProfileResponse));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGameCenterGameProfile_Internal(string callbackId, string typeName);
#endif
    }
}
