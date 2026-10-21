// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Other.cs" company="Toss">
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
    public enum RequestNotificationAgreementOptionsOnEventParamType
    {
        [EnumMember(Value = "newAgreement")]
        NewAgreement,
        [EnumMember(Value = "alreadyAgreed")]
        AlreadyAgreed,
        [EnumMember(Value = "agreementRejected")]
        AgreementRejected
    }

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
    public class GrantPromotionRewardParams
    {
        [Preserve]
        [JsonProperty("params")]
        public GrantPromotionRewardParamsParams Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardParamsParams
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
    public class GrantPromotionRewardResult
    {
        [Preserve]
        [JsonProperty("key")]
        public string Key;
        [Preserve]
        [JsonProperty("code")]
        public string Code;
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
    public class RequestNotificationAgreementOptions
    {
        [Preserve]
        [JsonProperty("options")]
        public RequestNotificationAgreementOptionsOptions Options;
        [JsonIgnore]
        public System.Action<RequestNotificationAgreementOptionsOnEventParam> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class RequestNotificationAgreementOptionsOptions
    {
        [Preserve]
        [JsonProperty("templateCode")]
        public string TemplateCode;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class RequestNotificationAgreementOptionsOnEventParam
    {
        [Preserve]
        [JsonProperty("type")]
        public RequestNotificationAgreementOptionsOnEventParamType Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapProductListItem
    {
        [Preserve]
        [JsonProperty("sku")]
        public string Sku;
        [Preserve]
        [JsonProperty("displayAmount")]
        public string DisplayAmount;
        [Preserve]
        [JsonProperty("displayName")]
        public string DisplayName;
        [Preserve]
        [JsonProperty("iconUrl")]
        public string IconUrl;
        [Preserve]
        [JsonProperty("description")]
        public string Description;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    public enum PermissionStatus
    {
        [EnumMember(Value = "notDetermined")]
        NotDetermined,
        [EnumMember(Value = "denied")]
        Denied,
        [EnumMember(Value = "allowed")]
        Allowed
    }

    /// <summary>
    /// 사진 조회 결과를 나타내는 타입이에요.
    /// </summary>
    [Serializable]
    [Preserve]
    public class ImageResponse
    {
        /// <summary>가져온 사진의 고유 ID예요.</summary>
        [Preserve]
        [JsonProperty("id")]
        public string Id;
        /// <summary>사진의 데이터 URI예요. base64 옵션이 true인 경우 Base64 문자열로 반환돼요.</summary>
        [Preserve]
        [JsonProperty("dataUri")]
        public string DataUri;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    /// <summary>
    /// 연락처 정보를 나타내는 타입이에요.
    /// </summary>
    [Serializable]
    [Preserve]
    public class ContactEntity
    {
        /// <summary>연락처 이름이에요.</summary>
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        /// <summary>연락처 전화번호로, 문자열 형식이에요.</summary>
        [Preserve]
        [JsonProperty("phoneNumber")]
        public string PhoneNumber;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactResult
    {
        [Preserve]
        [JsonProperty("result")]
        public ContactEntity[] Result;
        [Preserve]
        [JsonProperty("nextOffset")]
        public double? NextOffset;
        [Preserve]
        [JsonProperty("done")]
        public bool Done;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    public enum HapticFeedbackType
    {
        [EnumMember(Value = "tickWeak")]
        TickWeak,
        [EnumMember(Value = "tap")]
        Tap,
        [EnumMember(Value = "tickMedium")]
        TickMedium,
        [EnumMember(Value = "softMedium")]
        SoftMedium,
        [EnumMember(Value = "basicWeak")]
        BasicWeak,
        [EnumMember(Value = "basicMedium")]
        BasicMedium,
        [EnumMember(Value = "success")]
        Success,
        [EnumMember(Value = "error")]
        Error,
        [EnumMember(Value = "wiggle")]
        Wiggle,
        [EnumMember(Value = "confetti")]
        Confetti
    }

    [Serializable]
    [Preserve]
    public class HapticFeedbackOptions
    {
        [Preserve]
        [JsonProperty("type")]
        public HapticFeedbackType Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
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

    public enum Accuracy
    {
        Lowest = 1,
        Low = 2,
        Balanced = 3,
        High = 4,
        Highest = 5,
        BestForNavigation = 6
    }

    [Serializable]
    [Preserve]
    public class LocationCoords
    {
        /// <summary>위도</summary>
        [Preserve]
        [JsonProperty("latitude")]
        public double Latitude;
        /// <summary>경도</summary>
        [Preserve]
        [JsonProperty("longitude")]
        public double Longitude;
        /// <summary>높이</summary>
        [Preserve]
        [JsonProperty("altitude")]
        public double Altitude;
        /// <summary>위치 정확도</summary>
        [Preserve]
        [JsonProperty("accuracy")]
        public double Accuracy;
        /// <summary>고도 정확도</summary>
        [Preserve]
        [JsonProperty("altitudeAccuracy")]
        public double AltitudeAccuracy;
        /// <summary>방향</summary>
        [Preserve]
        [JsonProperty("heading")]
        public double Heading;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GameCenterGameProfileResponse
    {
        [Preserve]
        [JsonProperty("statusCode")]
        public string StatusCode;
        [Preserve]
        [JsonProperty("nickname")]
        public string Nickname; // optional
        [Preserve]
        [JsonProperty("profileImageUri")]
        public string ProfileImageUri; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    public enum NetworkStatus
    {
        [EnumMember(Value = "OFFLINE")]
        OFFLINE,
        [EnumMember(Value = "WIFI")]
        WIFI,
        [EnumMember(Value = "2G")]
        _2G,
        [EnumMember(Value = "3G")]
        _3G,
        [EnumMember(Value = "4G")]
        _4G,
        [EnumMember(Value = "5G")]
        _5G,
        [EnumMember(Value = "WWAN")]
        WWAN,
        [EnumMember(Value = "UNKNOWN")]
        UNKNOWN
    }

    public enum PermissionAccess
    {
        [EnumMember(Value = "read")]
        Read,
        [EnumMember(Value = "write")]
        Write,
        [EnumMember(Value = "access")]
        Access
    }

    public enum PermissionName
    {
        [EnumMember(Value = "clipboard")]
        Clipboard,
        [EnumMember(Value = "contacts")]
        Contacts,
        [EnumMember(Value = "photos")]
        Photos,
        [EnumMember(Value = "geolocation")]
        Geolocation,
        [EnumMember(Value = "camera")]
        Camera,
        [EnumMember(Value = "microphone")]
        Microphone
    }

    [Serializable]
    [Preserve]
    public class RequestTossPayPaysBillingOptions
    {
        /// <summary>정기결제 래핑 토큰이에요.</summary>
        [Preserve]
        [JsonProperty("wrappedToken")]
        public string WrappedToken;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class RequestTossPayPaysBillingResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        [Preserve]
        [JsonProperty("success")]
        public bool Success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        [Preserve]
        [JsonProperty("reason")]
        public string Reason; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
