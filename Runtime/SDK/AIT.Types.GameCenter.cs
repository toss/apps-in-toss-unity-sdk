// -----------------------------------------------------------------------
// <copyright file="AIT.Types.GameCenter.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Result type for GetUserKeyForGame (Discriminated Union)
    /// Success: GetUserKeyForGameSuccessResponse | Error: INVALID_CATEGORY,ERROR
    /// </summary>
    [Serializable]
    [Preserve]
    public class GetUserKeyForGameResult
    {
        [Preserve]
        [JsonProperty("_type")]
        public string _type;

        [Preserve]
        [JsonProperty("_successJson")]
        public GetUserKeyForGameSuccessResponse _successData;

        [Preserve]
        [JsonProperty("_errorCode")]
        public string _errorCode;

        /// <summary>성공 여부를 나타냅니다.</summary>
        public bool IsSuccess => _type == "success";

        /// <summary>에러 여부를 나타냅니다.</summary>
        public bool IsError => _type == "error";

        /// <summary>
        /// 성공 데이터를 가져옵니다.
        /// </summary>
        public GetUserKeyForGameSuccessResponse GetSuccess()
            => IsSuccess ? _successData : null;

        /// <summary>
        /// 에러 코드를 가져옵니다.
        /// Possible values: "INVALID_CATEGORY", "ERROR"
        /// </summary>
        public string GetErrorCode()
            => IsError ? _errorCode : null;

        /// <summary>
        /// Pattern matching을 사용하여 결과를 처리합니다.
        /// </summary>
        public void Match(
            Action<GetUserKeyForGameSuccessResponse> onSuccess,
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
            Func<GetUserKeyForGameSuccessResponse, T> onSuccess,
            Func<string, T> onError
        )
        {
            return IsSuccess ? onSuccess(GetSuccess()) : onError(GetErrorCode());
        }

        /// <summary>
        /// Fluent API: 성공 시 액션을 실행합니다.
        /// </summary>
        public GetUserKeyForGameResult OnSuccess(Action<GetUserKeyForGameSuccessResponse> action)
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
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }


    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameParams
    {
        [Preserve]
        [JsonProperty("params")]
        public GrantPromotionRewardForGameParamsParams Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameParamsParams
    {
        [Preserve]
        [JsonProperty("promotionCode")]
        public string PromotionCode;
        [Preserve]
        [JsonProperty("amount")]
        public double Amount;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SubmitGameCenterLeaderBoardScoreParams
    {
        [Preserve]
        [JsonProperty("score")]
        public string Score;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GetUserKeyForGameResponse
    {
        [Preserve]
        [JsonProperty("hash")]
        public string Hash; // optional
        [Preserve]
        [JsonProperty("type")]
        public string Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GetUserKeyForGameSuccessResponse
    {
        [Preserve]
        [JsonProperty("hash")]
        public string Hash;
        [Preserve]
        [JsonProperty("type")]
        public string Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GetUserKeyForGameErrorResponse
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameSuccessResponse
    {
        [Preserve]
        [JsonProperty("key")]
        public string Key;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameErrorResponse
    {
        [Preserve]
        [JsonProperty("code")]
        public string Code;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameErrorResult
    {
        [Preserve]
        [JsonProperty("errorCode")]
        public string ErrorCode;
        [Preserve]
        [JsonProperty("message")]
        public string Message;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameResponse
    {
        [Preserve]
        [JsonProperty("key")]
        public string Key; // optional
        [Preserve]
        [JsonProperty("code")]
        public string Code; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SubmitGameCenterLeaderBoardScoreResponse
    {
        [Preserve]
        [JsonProperty("statusCode")]
        public string StatusCode;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
