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
    public enum IapProductListItemRenewalCycle
    {
        [EnumMember(Value = "WEEKLY")]
        WEEKLY,
        [EnumMember(Value = "MONTHLY")]
        MONTHLY,
        [EnumMember(Value = "YEARLY")]
        YEARLY
    }

    public enum CompletedOrRefundedOrdersResultOrderStatus
    {
        [EnumMember(Value = "COMPLETED")]
        COMPLETED,
        [EnumMember(Value = "REFUNDED")]
        REFUNDED
    }

    public enum DeclaredAgeRangeStatus
    {
        [EnumMember(Value = "INVALID_REQUEST")]
        INVALID_REQUEST,
        [EnumMember(Value = "NOT_AVAILABLE")]
        NOT_AVAILABLE,
        [EnumMember(Value = "SHARING")]
        SHARING,
        [EnumMember(Value = "DECLINED_SHARING")]
        DECLINED_SHARING,
        [EnumMember(Value = "UNSUPPORTED_OS")]
        UNSUPPORTED_OS,
        [EnumMember(Value = "FAILED")]
        FAILED
    }

    public enum LocationAccessLocation
    {
        [EnumMember(Value = "FINE")]
        FINE,
        [EnumMember(Value = "COARSE")]
        COARSE
    }

    [Serializable]
    [Preserve]
    public class AdMobLoadResult
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
        [Preserve]
        [JsonProperty("responseInfo")]
        public ResponseInfo ResponseInfo;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ResponseInfo
    {
        [Preserve]
        [JsonProperty("adNetworkInfoArray")]
        public AdNetworkResponseInfo[] AdNetworkInfoArray;
        [Preserve]
        [JsonProperty("loadedAdNetworkInfo")]
        public AdNetworkResponseInfo LoadedAdNetworkInfo;
        [Preserve]
        [JsonProperty("responseId")]
        public string ResponseId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AdNetworkResponseInfo
    {
        [Preserve]
        [JsonProperty("adSourceId")]
        public string AdSourceId;
        [Preserve]
        [JsonProperty("adSourceName")]
        public string AdSourceName;
        [Preserve]
        [JsonProperty("adSourceInstanceId")]
        public string AdSourceInstanceId;
        [Preserve]
        [JsonProperty("adSourceInstanceName")]
        public string AdSourceInstanceName;
        [Preserve]
        [JsonProperty("adNetworkClassName")]
        public string AdNetworkClassName;

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
    public class ShowAdMobEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public ShowAdMobEventData Data; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobEventData
    {
        [Preserve]
        [JsonProperty("unitType")]
        public string UnitType;
        [Preserve]
        [JsonProperty("unitAmount")]
        public double UnitAmount;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class Offer
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("offerId")]
        public string OfferId;
        [Preserve]
        [JsonProperty("period")]
        public string Period;
        [Preserve]
        [JsonProperty("displayAmount")]
        public string DisplayAmount; // optional

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
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("renewalCycle")]
        public IapProductListItemRenewalCycle? RenewalCycle; // optional
        [Preserve]
        [JsonProperty("offers")]
        public Offer[] Offers; // optional

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

    public enum AlbumItemType
    {
        [EnumMember(Value = "PHOTO")]
        PHOTO,
        [EnumMember(Value = "VIDEO")]
        VIDEO
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

    public enum PermissionAccess
    {
        [EnumMember(Value = "read")]
        Read,
        [EnumMember(Value = "write")]
        Write,
        [EnumMember(Value = "access")]
        Access
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

    public enum PermissionDialogResult
    {
        [EnumMember(Value = "allowed")]
        Allowed,
        [EnumMember(Value = "denied")]
        Denied
    }

    /// <summary>
    /// 웹 SDK가 노출하는 햅틱 타입이에요. 와이어(계약)는 string으로 열려 있어
    /// 새 타입이 추가돼도 하위호환이 깨지지 않아요.
    /// </summary>
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

    /// <summary>
    /// 웹 SDK가 노출하는 네트워크 상태 값이에요. 와이어(계약)는 string으로 열려 있어요.
    /// </summary>
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

    public enum NavigationBarTheme
    {
        [EnumMember(Value = "light")]
        Light,
        [EnumMember(Value = "dark")]
        Dark
    }

    public enum ConsentedUserDataKey
    {
        [EnumMember(Value = "USER_NAME")]
        USER_NAME,
        [EnumMember(Value = "USER_GENDER")]
        USER_GENDER,
        [EnumMember(Value = "USER_NATIONALITY")]
        USER_NATIONALITY,
        [EnumMember(Value = "USER_BIRTHDAY")]
        USER_BIRTHDAY,
        [EnumMember(Value = "USER_PHONE")]
        USER_PHONE,
        [EnumMember(Value = "USER_ADDRESS")]
        USER_ADDRESS,
        [EnumMember(Value = "USER_EMAIL")]
        USER_EMAIL,
        [EnumMember(Value = "USER_CONSUMPTION_HISTORY")]
        USER_CONSUMPTION_HISTORY,
        [EnumMember(Value = "USER_ADDRESS_BASE")]
        USER_ADDRESS_BASE,
        [EnumMember(Value = "USER_ADDRESS_DETAIL")]
        USER_ADDRESS_DETAIL
    }

    [Serializable]
    [Preserve]
    public class AdUserEarnedReward
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public AdUserEarnedRewardData Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AdUserEarnedRewardData
    {
        [Preserve]
        [JsonProperty("unitType")]
        public string UnitType;
        [Preserve]
        [JsonProperty("unitAmount")]
        public double UnitAmount;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AddAccessoryButtonOptions
    {
        [Preserve]
        [JsonProperty("id")]
        public string Id;
        [Preserve]
        [JsonProperty("title")]
        public string Title;
        [Preserve]
        [JsonProperty("icon")]
        public AddAccessoryButtonOptionsIcon Icon;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AddAccessoryButtonOptionsIcon
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AlbumItemResponse
    {
        [Preserve]
        [JsonProperty("id")]
        public string Id;
        [Preserve]
        [JsonProperty("dataUri")]
        public string DataUri;
        [Preserve]
        [JsonProperty("type")]
        public AlbumItemType Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AppsInTossGlobals
    {
        [Preserve]
        [JsonProperty("deploymentId")]
        public string DeploymentId;
        [Preserve]
        [JsonProperty("brandDisplayName")]
        public string BrandDisplayName;
        [Preserve]
        [JsonProperty("brandIcon")]
        public string BrandIcon;
        [Preserve]
        [JsonProperty("brandPrimaryColor")]
        public string BrandPrimaryColor;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CompletedOrRefundedOrdersResultOrder
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("sku")]
        public string Sku;
        [Preserve]
        [JsonProperty("status")]
        public CompletedOrRefundedOrdersResultOrderStatus Status;
        [Preserve]
        [JsonProperty("date")]
        public string Date;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CompletedOrRefundedOrdersResult
    {
        [Preserve]
        [JsonProperty("hasNext")]
        public bool HasNext;
        [Preserve]
        [JsonProperty("nextKey")]
        public string NextKey; // optional
        [Preserve]
        [JsonProperty("orders")]
        public CompletedOrRefundedOrdersResultOrder[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

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
    public class RewardFromContactsViralEventData
    {
        [Preserve]
        [JsonProperty("rewardAmount")]
        public double RewardAmount;
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CreateLinkParams
    {
        [Preserve]
        [JsonProperty("path")]
        public string Path;
        [Preserve]
        [JsonProperty("ogImageUrl")]
        public string OgImageUrl; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class DeclaredAgeRange
    {
        [Preserve]
        [JsonProperty("status")]
        [Obsolete("Use StatusTyped")]
        public string Status;

        /// <summary>
        /// <see cref="Status"/>를 <see cref="DeclaredAgeRangeStatus"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Status"/>에 그대로 남는다. 값을 쓰면 <see cref="Status"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public DeclaredAgeRangeStatus? StatusTyped
        {
            get => AITCompat.ParseEnum<DeclaredAgeRangeStatus>(Status);
            set => Status = AITCompat.ToWire(value);
        }
        [Preserve]
        [JsonProperty("lowerBound")]
        public double? LowerBound; // optional
        [Preserve]
        [JsonProperty("upperBound")]
        public double? UpperBound; // optional
        [Preserve]
        [JsonProperty("ageRangeDeclaration")]
        public string AgeRangeDeclaration; // optional
        [Preserve]
        [JsonProperty("activeParentalControls")]
        public string[] ActiveParentalControls;
        [Preserve]
        [JsonProperty("isEligibleForAgeFeatures")]
        public bool? IsEligibleForAgeFeatures; // optional
        [Preserve]
        [JsonProperty("errorCode")]
        public string ErrorCode; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantRewardParams
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
    public class IapCreateOneTimePurchaseOrderResult
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("displayName")]
        public string DisplayName;
        [Preserve]
        [JsonProperty("displayAmount")]
        public string DisplayAmount;
        [Preserve]
        [JsonProperty("amount")]
        public double Amount;
        [Preserve]
        [JsonProperty("currency")]
        public string Currency;
        [Preserve]
        [JsonProperty("fraction")]
        public double Fraction;
        [Preserve]
        [JsonProperty("miniAppIconUrl")]
        public string MiniAppIconUrl;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapSubscriptionInfoResponse
    {
        [Preserve]
        [JsonProperty("subscription")]
        public IapSubscriptionInfoResult Subscription;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    /// <summary>
    /// 비교를 허용하도록 넓혀요. `(string &amp; {})` 덕분에 자동완성은 유지돼요.
    /// </summary>
    [Serializable]
    [Preserve]
    public class IapSubscriptionInfoResult
    {
        [Preserve]
        [JsonProperty("status")]
        public string Status;
        [Preserve]
        [JsonProperty("catalogId")]
        public double CatalogId;
        [Preserve]
        [JsonProperty("expiresAt")]
        public string ExpiresAt;
        [Preserve]
        [JsonProperty("isAutoRenew")]
        public bool IsAutoRenew;
        [Preserve]
        [JsonProperty("gracePeriodExpiresAt")]
        public string GracePeriodExpiresAt;
        [Preserve]
        [JsonProperty("isAccessible")]
        public bool IsAccessible;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

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

    [Serializable]
    [Preserve]
    public class LoadAdMobEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public AdMobLoadResult Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class Location
    {
        [Preserve]
        [JsonProperty("accessLocation")]
        [Obsolete("Use AccessLocationTyped")]
        public string AccessLocation; // optional

        /// <summary>
        /// <see cref="AccessLocation"/>를 <see cref="LocationAccessLocation"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="AccessLocation"/>에 그대로 남는다. 값을 쓰면 <see cref="AccessLocation"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public LocationAccessLocation? AccessLocationTyped
        {
            get => AITCompat.ParseEnum<LocationAccessLocation>(AccessLocation);
            set => AccessLocation = AITCompat.ToWire(value);
        }
        [Preserve]
        [JsonProperty("timestamp")]
        public double Timestamp;
        [Preserve]
        [JsonProperty("coords")]
        public LocationCoords Coords;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
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
    public class OriginStorageDump
    {
        [Preserve]
        [JsonProperty("origin")]
        public string Origin;
        [Preserve]
        [JsonProperty("localStorage")]
        public Dictionary<string, object> LocalStorage;
        [Preserve]
        [JsonProperty("indexedDB")]
        public OriginStorageDumpIndexedDB[] IndexedDB;
        [Preserve]
        [JsonProperty("opfs")]
        public OriginStorageDumpOpfs Opfs;
        [Preserve]
        [JsonProperty("errors")]
        public OriginStorageDumpError[] Errors;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDBObjectStoreIndex
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        [Preserve]
        [JsonProperty("keyPath")]
        public string KeyPath;
        [Preserve]
        [JsonProperty("multiEntry")]
        public bool MultiEntry;
        [Preserve]
        [JsonProperty("unique")]
        public bool Unique;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDBObjectStoreRecord
    {
        [Preserve]
        [JsonProperty("key")]
        public object Key;
        [Preserve]
        [JsonProperty("primaryKey")]
        public object PrimaryKey;
        [Preserve]
        [JsonProperty("value")]
        public object Value;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDBObjectStore
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        [Preserve]
        [JsonProperty("keyPath")]
        public string KeyPath;
        [Preserve]
        [JsonProperty("autoIncrement")]
        public bool AutoIncrement;
        [Preserve]
        [JsonProperty("indexes")]
        public OriginStorageDumpIndexedDBObjectStoreIndex[] Indexes;
        [Preserve]
        [JsonProperty("records")]
        public OriginStorageDumpIndexedDBObjectStoreRecord[] Records;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDB
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        [Preserve]
        [JsonProperty("version")]
        public double Version;
        [Preserve]
        [JsonProperty("objectStores")]
        public OriginStorageDumpIndexedDBObjectStore[] ObjectStores;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpOpfsFile
    {
        [Preserve]
        [JsonProperty("path")]
        public string Path;
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("lastModified")]
        public double LastModified;
        [Preserve]
        [JsonProperty("data")]
        public byte[] Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpOpfs
    {
        [Preserve]
        [JsonProperty("directories")]
        public string[] Directories;
        [Preserve]
        [JsonProperty("files")]
        public OriginStorageDumpOpfsFile[] Files;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpError
    {
        [Preserve]
        [JsonProperty("storage")]
        public string Storage;
        [Preserve]
        [JsonProperty("message")]
        public string Message;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SetNavigationBarOptions
    {
        [Preserve]
        [JsonProperty("withBackButton")]
        public bool? WithBackButton; // optional
        [Preserve]
        [JsonProperty("withHomeButton")]
        public bool? WithHomeButton; // optional
        [Preserve]
        [JsonProperty("withTitle")]
        public bool? WithTitle; // optional
        [Preserve]
        [JsonProperty("transparentBackground")]
        public bool? TransparentBackground; // optional
        [Preserve]
        [JsonProperty("backgroundColor")]
        public string BackgroundColor; // optional
        [Preserve]
        [JsonProperty("theme")]
        public NavigationBarTheme Theme; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    /// <summary>
    /// 이전 SDK의 SafeAreaInsetsSubscribe 매개변수 묶음이다. 새 코드는
    /// <c>AIT.SafeAreaInsetsSubscribe(onEvent, onError)</c>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("AIT.SafeAreaInsetsSubscribe(Action<SafeAreaInsets> onEvent, Action<AITException> onError)를 사용하세요.")]
    public class SafeAreaInsetsSubscribe__0
    {
        [JsonIgnore]
        public System.Action<SafeAreaInsets> OnEvent;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    /// <summary>
    /// 이전 생성기가 잘못 만들어 공개한 빈 클래스다. 쓸 곳이 없다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("쓸 곳이 없는 빈 클래스예요.")]
    public class number
    {
    }
}
