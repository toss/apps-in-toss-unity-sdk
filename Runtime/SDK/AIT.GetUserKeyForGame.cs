// -----------------------------------------------------------------------
// <copyright file="AIT.GetUserKeyForGame.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetUserKeyForGame API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetUserKeyForGame
    /// </summary>
    public static partial class AIT
    {
        /// <returns>사용자 키 조회 결과를 반환해요. GetUserKeyForGameSuccessResponse: 사용자 키 조회에 성공했어요. { type: 'HASH', hash: string } 형태로 반환돼요. 'INVALID_CATEGORY': 게임 카테고리가 아닌 미니앱에서 호출했어요. 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요.</returns>
        public static Task<GetUserKeyForGameResult> GetUserKeyForGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetUserKeyForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyForGameResult>(result => tcs.SetResult(result));
            __getUserKeyForGame_Internal(callbackId, "GetUserKeyForGameResult");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            return Task.FromResult(default(GetUserKeyForGameResult));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getUserKeyForGame_Internal(string callbackId, string typeName);
#endif
    }
}
