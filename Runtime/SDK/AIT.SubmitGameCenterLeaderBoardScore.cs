// -----------------------------------------------------------------------
// <copyright file="AIT.SubmitGameCenterLeaderBoardScore.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SubmitGameCenterLeaderBoardScore API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SubmitGameCenterLeaderBoardScore
    /// </summary>
    public static partial class AIT
    {
        /// <returns>점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 undefined를 반환해요.</returns>
        public static void SubmitGameCenterLeaderBoardScore(object paramsParam, Action<SubmitGameCenterLeaderBoardScoreResponse> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __submitGameCenterLeaderBoardScore_Internal(paramsParam, callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScore called");
            var mockResult = default(SubmitGameCenterLeaderBoardScoreResponse);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __submitGameCenterLeaderBoardScore_Internal(object paramsParam, string callbackId, string typeName);
#endif
    }
}
