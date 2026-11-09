// -----------------------------------------------------------------------
// <copyright file="AIT.Types.User.cs" company="Toss">
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
    /// <summary>
    /// Result type for GetAnonymousKey (Discriminated Union)
    /// Success: GetAnonymousKeySuccessResponse | Error: ERROR
    /// </summary>
    [Serializable]
    [Preserve]
    public partial class GetAnonymousKeyResult
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
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }


    [Serializable]
    [Preserve]
    public partial class UserCreateAnonymousKeyAuthCodeResult
    {
        [Preserve]
        public string Code;
        [Preserve]
        public double ExpiresIn;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
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
    public partial class GetAnonymousKeySuccessResponse
    {
        [Preserve]
        public string Hash;
        [Preserve]
        public string Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GetConsentedUserDataOptions
    {
        /// <summary>동의 항목 키예요 (예: "cud_delivery").</summary>
        [Preserve]
        public string ConsentedUserDataKey;
        /// <summary>사용자가 이전에 거부했어도 약관 웹뷰를 다시 띄울지예요. 기본은 false예요.</summary>
        [Preserve]
        public bool? ShouldRequestAgreementWhenUserDeclined; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GetDeclaredAgeRangeParams
    {
        [Preserve]
        public double[] AgeGates;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
