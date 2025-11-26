// -----------------------------------------------------------------------
// <copyright file="AIT.GrantPromotionRewardForGame.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GrantPromotionRewardForGame API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GrantPromotionRewardForGame
    /// </summary>
    public static partial class AIT
    {
        /// <returns>포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. "40000": 게임이 아닌 미니앱에서 호출했을 때 "4100": 프로모션 정보를 찾을 수 없을 때 "4104": 프로모션이 중지되었을 때 "4105": 프로모션이 종료되었을 때 "4108": 프로모션이 승인되지 않았을 때 "4109": 프로모션이 실행중이 아닐 때 "4110": 리워드를 지급/회수할 수 없을 때 "4112": 프로모션 머니가 부족할 때 "4113": 이미 지급/회수된 내역일 때 "4114": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요.</returns>
        public static Task<GrantPromotionRewardForGameResult> GrantPromotionRewardForGame(GrantPromotionRewardForGameOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GrantPromotionRewardForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardForGameResult>(result => tcs.SetResult(result));
            __grantPromotionRewardForGame_Internal(options, callbackId, "GrantPromotionRewardForGameResult");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGame called");
            return Task.FromResult(default(GrantPromotionRewardForGameResult));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionRewardForGame_Internal(GrantPromotionRewardForGameOptions options, string callbackId, string typeName);
#endif
    }
}
