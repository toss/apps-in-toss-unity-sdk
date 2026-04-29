// -----------------------------------------------------------------------
// <copyright file="AIT.Other.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Other APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - Other
    /// </summary>
    public static partial class AIT
    {
        /// <returns>그룹 ID</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetGroupId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __getGroupId_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetGroupId called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetGroupId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getGroupId_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGroupId called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGroupId_Internal(string callbackId, string typeName);
#endif
        /// <returns>사용자 키 조회 결과를 반환해요. GetUserKeySuccessResponse: 사용자 키 조회에 성공했어요. { type: 'HASH', hash: string } 형태로 반환돼요. 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GetUserKeyResult> GetUserKey()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GetUserKeyResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __getUserKey_Internal(callbackId, "GetUserKeyResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKey called");
            await Awaitable.NextFrameAsync();
            return default(GetUserKeyResult);
#endif
        }
#else
        public static async Task<GetUserKeyResult> GetUserKey()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetUserKeyResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __getUserKey_Internal(callbackId, "GetUserKeyResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKey called");
            await Task.CompletedTask;
            return default(GetUserKeyResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getUserKey_Internal(string callbackId, string typeName);
#endif
        /// <param name="paramsParam">포인트를 지급하기 위해 필요한 정보예요.</param>
        /// <returns>포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. "4100": 프로모션 정보를 찾을 수 없을 때 "4104": 프로모션이 중지되었을 때 "4105": 프로모션이 종료되었을 때 "4108": 프로모션이 승인되지 않았을 때 "4109": 프로모션이 실행중이 아닐 때 "4110": 리워드를 지급/회수할 수 없을 때 "4112": 프로모션 머니가 부족할 때 "4113": 이미 지급/회수된 내역일 때 "4114": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GrantPromotionRewardResult> GrantPromotionReward(GrantPromotionRewardParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GrantPromotionRewardResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __grantPromotionReward_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionReward called");
            await Awaitable.NextFrameAsync();
            return default(GrantPromotionRewardResult);
#endif
        }
#else
        public static async Task<GrantPromotionRewardResult> GrantPromotionReward(GrantPromotionRewardParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GrantPromotionRewardResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __grantPromotionReward_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionReward called");
            await Task.CompletedTask;
            return default(GrantPromotionRewardResult);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionReward_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        [Preserve]
        [APICategory("Other")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable RequestReview()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error)
            );
            __requestReview_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] RequestReview called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task RequestReview()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __requestReview_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] RequestReview called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __requestReview_Internal(string callbackId, string typeName);
#endif
    }
}
