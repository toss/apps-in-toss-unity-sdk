// -----------------------------------------------------------------------
// <copyright file="AIT.GameCenter.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GameCenter APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GameCenter
    /// </summary>
    public static partial class AIT
    {
        /// <returns>프로필 정보 또는 undefined를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GameCenterGameProfileResponse> GetGameCenterGameProfile()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __getGameCenterGameProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfile called");
            await Awaitable.NextFrameAsync();
            return default(GameCenterGameProfileResponse);
#endif
        }
#else
        public static async Task<GameCenterGameProfileResponse> GetGameCenterGameProfile()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getGameCenterGameProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfile called");
            await Task.CompletedTask;
            return default(GameCenterGameProfileResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGameCenterGameProfile_Internal(string callbackId, string typeName);
#endif
        /// <returns>사용자 키 조회 결과를 반환해요. GetUserKeyForGameSuccessResponse: 사용자 키 조회에 성공했어요. { type: 'HASH', hash: string } 형태로 반환돼요. 'INVALID_CATEGORY': 게임 카테고리가 아닌 미니앱에서 호출했어요. 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GetUserKeyForGameResult> GetUserKeyForGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GetUserKeyForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyForGameResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __getUserKeyForGame_Internal(callbackId, "GetUserKeyForGameResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            await Awaitable.NextFrameAsync();
            return default(GetUserKeyForGameResult);
#endif
        }
#else
        public static async Task<GetUserKeyForGameResult> GetUserKeyForGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetUserKeyForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyForGameResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getUserKeyForGame_Internal(callbackId, "GetUserKeyForGameResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            await Task.CompletedTask;
            return default(GetUserKeyForGameResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getUserKeyForGame_Internal(string callbackId, string typeName);
#endif
        /// <returns>포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. "40000": 게임이 아닌 미니앱에서 호출했을 때 "4100": 프로모션 정보를 찾을 수 없을 때 "4104": 프로모션이 중지되었을 때 "4105": 프로모션이 종료되었을 때 "4108": 프로모션이 승인되지 않았을 때 "4109": 프로모션이 실행중이 아닐 때 "4110": 리워드를 지급/회수할 수 없을 때 "4112": 프로모션 머니가 부족할 때 "4113": 이미 지급/회수된 내역일 때 "4114": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GrantPromotionRewardForGameResult> GrantPromotionRewardForGame(GrantPromotionRewardForGameOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GrantPromotionRewardForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardForGameResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __grantPromotionRewardForGame_Internal(AITJsonSettings.Serialize(options), callbackId, "GrantPromotionRewardForGameResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGame called");
            await Awaitable.NextFrameAsync();
            return default(GrantPromotionRewardForGameResult);
#endif
        }
#else
        public static async Task<GrantPromotionRewardForGameResult> GrantPromotionRewardForGame(GrantPromotionRewardForGameOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GrantPromotionRewardForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardForGameResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __grantPromotionRewardForGame_Internal(AITJsonSettings.Serialize(options), callbackId, "GrantPromotionRewardForGameResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGame called");
            await Task.CompletedTask;
            return default(GrantPromotionRewardForGameResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionRewardForGame_Internal(string options, string callbackId, string typeName);
#endif
        /// <returns>리더보드 웹뷰를 호출해요. 앱 버전이 낮으면 아무 동작도 하지 않고 undefined를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable OpenGameCenterLeaderboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __openGameCenterLeaderboard_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboard called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task OpenGameCenterLeaderboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __openGameCenterLeaderboard_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboard called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openGameCenterLeaderboard_Internal(string callbackId, string typeName);
#endif
        /// <returns>점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 undefined를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SubmitGameCenterLeaderBoardScoreResponse> SubmitGameCenterLeaderBoardScore(SubmitGameCenterLeaderBoardScoreParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SubmitGameCenterLeaderBoardScoreResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<SubmitGameCenterLeaderBoardScoreResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __submitGameCenterLeaderBoardScore_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScore called");
            await Awaitable.NextFrameAsync();
            return default(SubmitGameCenterLeaderBoardScoreResponse);
#endif
        }
#else
        public static async Task<SubmitGameCenterLeaderBoardScoreResponse> SubmitGameCenterLeaderBoardScore(SubmitGameCenterLeaderBoardScoreParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SubmitGameCenterLeaderBoardScoreResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<SubmitGameCenterLeaderBoardScoreResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __submitGameCenterLeaderBoardScore_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScore called");
            await Task.CompletedTask;
            return default(SubmitGameCenterLeaderBoardScoreResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __submitGameCenterLeaderBoardScore_Internal(string paramsParam, string callbackId, string typeName);
#endif
    }
}
