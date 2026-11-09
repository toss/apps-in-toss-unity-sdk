// -----------------------------------------------------------------------
// <copyright file="AIT.Types.GameCenter.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    public enum SubmitGameCenterLeaderBoardScoreResponseStatusCode
    {
        [EnumMember(Value = "PROFILE_NOT_FOUND")]
        PROFILE_NOT_FOUND,
        [EnumMember(Value = "SUCCESS")]
        SUCCESS,
        [EnumMember(Value = "LEADERBOARD_NOT_FOUND")]
        LEADERBOARD_NOT_FOUND,
        [EnumMember(Value = "UNPARSABLE_SCORE")]
        UNPARSABLE_SCORE
    }

    /// <summary>
    /// Result type for GetUserKeyForGame (Discriminated Union)
    /// Success: GetAnonymousKeySuccessResponse | Error: ERROR
    /// </summary>
    [Serializable]
    [Preserve]
    public partial class GetUserKeyForGameResult
    {
        [Preserve]
        public string _type;

        [Preserve]
        public GetAnonymousKeySuccessResponse _successData;

        [Preserve]
        public string _errorCode;

        /// <summary>성공 여부를 나타냅니다.</summary>
        public bool IsSuccess => _type == "success";

        /// <summary>에러 여부를 나타냅니다.</summary>
        public bool IsError => _type == "error";

        /// <summary>
        /// 성공 데이터를 가져옵니다.
        /// </summary>
        public GetAnonymousKeySuccessResponse GetSuccess()
            => IsSuccess ? _successData : null;

        /// <summary>
        /// 에러 코드를 가져옵니다.
        /// Possible values: "ERROR"
        /// </summary>
        public string GetErrorCode()
            => IsError ? _errorCode : null;

        /// <summary>
        /// Pattern matching을 사용하여 결과를 처리합니다.
        /// </summary>
        public void Match(
            Action<GetAnonymousKeySuccessResponse> onSuccess,
            Action<string> onError
        )
        {
            if (IsSuccess)
                onSuccess(GetSuccess());
            else
                onError(GetErrorCode());
        }

        /// <summary>
        /// Pattern matching을 사용하여 결과를 처리하고 값을 반환합니다.
        /// </summary>
        public T Match<T>(
            Func<GetAnonymousKeySuccessResponse, T> onSuccess,
            Func<string, T> onError
        )
        {
            return IsSuccess ? onSuccess(GetSuccess()) : onError(GetErrorCode());
        }

        /// <summary>
        /// Fluent API: 성공 시 액션을 실행합니다.
        /// </summary>
        public GetUserKeyForGameResult OnSuccess(Action<GetAnonymousKeySuccessResponse> action)
        {
            if (IsSuccess) action(GetSuccess());
            return this;
        }

        /// <summary>
        /// Fluent API: 에러 시 액션을 실행합니다.
        /// </summary>
        public GetUserKeyForGameResult OnError(Action<string> action)
        {
            if (IsError) action(GetErrorCode());
            return this;
        }

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }


    [Serializable]
    [Preserve]
    public partial class GameSetLeaderboardScoreParams
    {
        [Preserve]
        public string Score;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GrantPromotionRewardResult
    {
        [Preserve]
        public string Key;
        [Preserve]
        public string ErrorCode;
        [Preserve]
        public string Message;
        [Preserve]
        [Obsolete("오류 코드는 ErrorCode로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string Code;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SubmitGameCenterLeaderBoardScoreParams
    {
        [Preserve]
        public string Score;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GetUserKeyForGameResponse
    {
        [Preserve]
        public string Hash; // optional
        [Preserve]
        public string Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SubmitGameCenterLeaderBoardScoreResponse
    {
        [Preserve]
        [Obsolete("Use StatusCodeTyped")]
        public string StatusCode;

        /// <summary>
        /// <see cref="StatusCode"/>를 <see cref="SubmitGameCenterLeaderBoardScoreResponseStatusCode"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="StatusCode"/>에 그대로 남는다. 값을 쓰면 <see cref="StatusCode"/>도 함께 바뀐다.
        /// </summary>
        public SubmitGameCenterLeaderBoardScoreResponseStatusCode? StatusCodeTyped
        {
            get => AITCompat.ParseEnum<SubmitGameCenterLeaderBoardScoreResponseStatusCode>(StatusCode);
            set => StatusCode = AITCompat.ToWire(value);
        }

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="GrantPromotionRewardParams"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("GrantPromotionRewardParams를 사용하세요.")]
    public partial class GrantPromotionRewardForGameParams : GrantPromotionRewardParams
    {
        [Preserve]
        public new GrantPromotionRewardForGameParamsParams Params;
    }

    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="GrantPromotionRewardParamsParams"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("GrantPromotionRewardParamsParams를 사용하세요.")]
    public partial class GrantPromotionRewardForGameParamsParams : GrantPromotionRewardParamsParams
    {
    }

    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="GrantPromotionRewardResponse"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("GrantPromotionRewardResponse를 사용하세요.")]
    public partial class GrantPromotionRewardForGameSuccessResponse : GrantPromotionRewardResponse
    {
    }

    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="GrantPromotionRewardErrorResponse"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("GrantPromotionRewardErrorResponse를 사용하세요.")]
    public partial class GrantPromotionRewardForGameErrorResponse : GrantPromotionRewardErrorResponse
    {
    }

    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="GrantPromotionRewardErrorResult"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("GrantPromotionRewardErrorResult를 사용하세요.")]
    public partial class GrantPromotionRewardForGameErrorResult : GrantPromotionRewardErrorResult
    {
    }

    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="GrantPromotionRewardResponse"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("GrantPromotionRewardResponse를 사용하세요.")]
    public partial class GrantPromotionRewardForGameResponse : GrantPromotionRewardResponse
    {
        [Preserve]
        public string Code; // optional
    }
}
