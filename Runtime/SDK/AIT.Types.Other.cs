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
        [EnumMember(Value = "geolocation")]
        Geolocation,
        [EnumMember(Value = "contacts")]
        Contacts,
        [EnumMember(Value = "photos")]
        Photos,
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
        [EnumMember(Value = "USER_ADDRESS_BASE")]
        USER_ADDRESS_BASE,
        [EnumMember(Value = "USER_ADDRESS_DETAIL")]
        USER_ADDRESS_DETAIL,
        [EnumMember(Value = "USER_EMAIL")]
        USER_EMAIL,
        [EnumMember(Value = "USER_CONSUMPTION_HISTORY")]
        USER_CONSUMPTION_HISTORY
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

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactEntity
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
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
        public DeclaredAgeRangeStatus Status;
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
        [Preserve]
        [JsonProperty("id")]
        public string Id;
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
        public LocationAccessLocation? AccessLocation; // optional
        [Preserve]
        [JsonProperty("timestamp")]
        public double Timestamp;
        [Preserve]
        [JsonProperty("coords")]
        public LocationCoords Coords;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class LocationCoords
    {
        [Preserve]
        [JsonProperty("latitude")]
        public double Latitude;
        [Preserve]
        [JsonProperty("longitude")]
        public double Longitude;
        [Preserve]
        [JsonProperty("altitude")]
        public double Altitude;
        [Preserve]
        [JsonProperty("accuracy")]
        public double Accuracy;
        [Preserve]
        [JsonProperty("altitudeAccuracy")]
        public double AltitudeAccuracy;
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
        public object[] IndexedDB;
        [Preserve]
        [JsonProperty("opfs")]
        public OriginStorageDumpOpfs Opfs;
        [Preserve]
        [JsonProperty("errors")]
        public object[] Errors;

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
        public object[] Files;

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
}
