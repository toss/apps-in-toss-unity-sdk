// -----------------------------------------------------------------------
// <copyright file="AIT.Types.User.cs" company="Toss">
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
    /// Result type for GetAnonymousKey (Discriminated Union)
    /// Success: GetAnonymousKeySuccessResponse | Error: ERROR
    /// </summary>
    [Serializable]
    [Preserve]
    public class GetAnonymousKeyResult
    {
        [Preserve]
        [JsonProperty("_type")]
        public string _type;

        [Preserve]
        [JsonProperty("_successJson")]
        public GetAnonymousKeySuccessResponse _successData;

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
        public GetAnonymousKeyResult OnSuccess(Action<GetAnonymousKeySuccessResponse> action)
        {
            if (IsSuccess) action(GetSuccess());
            return this;
        }

        /// <summary>
        /// Fluent API: 에러 시 액션을 실행합니다.
        /// </summary>
        public GetAnonymousKeyResult OnError(Action<string> action)
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
    public class UserCreateAnonymousKeyAuthCodeResult
    {
        [Preserve]
        [JsonProperty("code")]
        public string Code;
        [Preserve]
        [JsonProperty("expiresIn")]
        public double ExpiresIn;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    public enum GetConsentedUserDataErrorCode
    {
        [EnumMember(Value = "USER_DECLINED")]
        USER_DECLINED,
        [EnumMember(Value = "UNAVAILABLE")]
        UNAVAILABLE,
        [EnumMember(Value = "TERMS_NOT_SET")]
        TERMS_NOT_SET,
        [EnumMember(Value = "INVALID_REQUEST")]
        INVALID_REQUEST,
        [EnumMember(Value = "CANCELED")]
        CANCELED,
        [EnumMember(Value = "CONSENTED_USER_DATA_AGREEMENT_FAILED")]
        CONSENTED_USER_DATA_AGREEMENT_FAILED,
        [EnumMember(Value = "CONSENTED_USER_DATA_INVALID_DATA")]
        CONSENTED_USER_DATA_INVALID_DATA
    }

    [Serializable]
    [Preserve]
    public class GetAnonymousKeySuccessResponse
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
    public class GetConsentedUserDataOptions
    {
        /// <summary>동의 항목 키예요 (예: "cud_delivery").</summary>
        [Preserve]
        [JsonProperty("consentedUserDataKey")]
        public string ConsentedUserDataKey;
        /// <summary>사용자가 이전에 거부했어도 약관 웹뷰를 다시 띄울지예요. 기본은 false예요.</summary>
        [Preserve]
        [JsonProperty("shouldRequestAgreementWhenUserDeclined")]
        public bool? ShouldRequestAgreementWhenUserDeclined; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GetDeclaredAgeRangeParams
    {
        [Preserve]
        [JsonProperty("ageGates")]
        public double[] AgeGates;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
