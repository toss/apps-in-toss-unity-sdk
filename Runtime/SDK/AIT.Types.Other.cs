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
}
