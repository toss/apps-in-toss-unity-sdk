// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Other.cs" company="Toss">
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
    public partial class AdMobLoadResult
    {
        [Preserve]
        public string AdGroupId;
        [Preserve]
        public string AdUnitId;
        [Preserve]
        public ResponseInfo ResponseInfo;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ResponseInfo
    {
        [Preserve]
        public AdNetworkResponseInfo[] AdNetworkInfoArray;
        [Preserve]
        public AdNetworkResponseInfo LoadedAdNetworkInfo;
        [Preserve]
        public string ResponseId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AdNetworkResponseInfo
    {
        [Preserve]
        public string AdSourceId;
        [Preserve]
        public string AdSourceName;
        [Preserve]
        public string AdSourceInstanceId;
        [Preserve]
        public string AdSourceInstanceName;
        [Preserve]
        public string AdNetworkClassName;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
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
    public partial class ShowAdMobEvent
    {
        [Preserve]
        public string Type;
        [Preserve]
        public ShowAdMobEventData Data; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ShowAdMobEventData
    {
        [Preserve]
        public string UnitType;
        [Preserve]
        public double UnitAmount;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class Offer
    {
        [Preserve]
        public string Type;
        [Preserve]
        public string OfferId;
        [Preserve]
        public string Period;
        [Preserve]
        public string DisplayAmount; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class IapProductListItem
    {
        [Preserve]
        public string Sku;
        [Preserve]
        public string DisplayAmount;
        [Preserve]
        public string DisplayName;
        [Preserve]
        public string IconUrl;
        [Preserve]
        public string Description;
        [Preserve]
        public string Type;
        [Preserve]
        public IapProductListItemRenewalCycle? RenewalCycle; // optional
        [Preserve]
        public Offer[] Offers; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GameCenterGameProfileResponse
    {
        [Preserve]
        public string StatusCode;
        [Preserve]
        public string Nickname; // optional
        [Preserve]
        public string ProfileImageUri; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
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
    public partial class AdUserEarnedReward
    {
        [Preserve]
        public string Type;
        [Preserve]
        public AdUserEarnedRewardData Data;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AdUserEarnedRewardData
    {
        [Preserve]
        public string UnitType;
        [Preserve]
        public double UnitAmount;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AddAccessoryButtonOptions
    {
        [Preserve]
        public string Id;
        [Preserve]
        public string Title;
        [Preserve]
        public AddAccessoryButtonOptionsIcon Icon;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AddAccessoryButtonOptionsIcon
    {
        [Preserve]
        public string Name;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AlbumItemResponse
    {
        [Preserve]
        public string Id;
        [Preserve]
        public string DataUri;
        [Preserve]
        public AlbumItemType Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AppsInTossGlobals
    {
        [Preserve]
        public string DeploymentId;
        [Preserve]
        public string BrandDisplayName;
        [Preserve]
        public string BrandIcon;
        [Preserve]
        public string BrandPrimaryColor;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class CompletedOrRefundedOrdersResultOrder
    {
        [Preserve]
        public string OrderId;
        [Preserve]
        public string Sku;
        [Preserve]
        public CompletedOrRefundedOrdersResultOrderStatus Status;
        [Preserve]
        public string Date;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class CompletedOrRefundedOrdersResult
    {
        [Preserve]
        public bool HasNext;
        [Preserve]
        public string NextKey; // optional
        [Preserve]
        public CompletedOrRefundedOrdersResultOrder[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ContactEntity
    {
        /// <summary>연락처 이름이에요.</summary>
        [Preserve]
        public string Name;
        /// <summary>연락처 전화번호로, 문자열 형식이에요.</summary>
        [Preserve]
        public string PhoneNumber;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class RewardFromContactsViralEventData
    {
        [Preserve]
        public double RewardAmount;
        [Preserve]
        public string RewardUnit;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class CreateLinkParams
    {
        [Preserve]
        public string Path;
        [Preserve]
        public string OgImageUrl; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class DeclaredAgeRange
    {
        [Preserve]
        [Obsolete("Use StatusTyped")]
        public string Status;

        /// <summary>
        /// <see cref="Status"/>를 <see cref="DeclaredAgeRangeStatus"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Status"/>에 그대로 남는다. 값을 쓰면 <see cref="Status"/>도 함께 바뀐다.
        /// </summary>
        public DeclaredAgeRangeStatus? StatusTyped
        {
            get => AITCompat.ParseEnum<DeclaredAgeRangeStatus>(Status);
            set => Status = AITCompat.ToWire(value);
        }
        [Preserve]
        public double? LowerBound; // optional
        [Preserve]
        public double? UpperBound; // optional
        [Preserve]
        public string AgeRangeDeclaration; // optional
        [Preserve]
        public string[] ActiveParentalControls;
        [Preserve]
        public bool? IsEligibleForAgeFeatures; // optional
        [Preserve]
        public string ErrorCode; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GrantRewardParams
    {
        [Preserve]
        public string PromotionCode;
        [Preserve]
        public double Amount;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class HapticFeedbackOptions
    {
        [Preserve]
        public HapticFeedbackType Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class IapCreateOneTimePurchaseOrderResult
    {
        [Preserve]
        public string OrderId;
        [Preserve]
        public string DisplayName;
        [Preserve]
        public string DisplayAmount;
        [Preserve]
        public double Amount;
        [Preserve]
        public string Currency;
        [Preserve]
        public double Fraction;
        [Preserve]
        public string MiniAppIconUrl;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class IapSubscriptionInfoResponse
    {
        [Preserve]
        public IapSubscriptionInfoResult Subscription;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    /// <summary>
    /// 비교를 허용하도록 넓혀요. `(string &amp; {})` 덕분에 자동완성은 유지돼요.
    /// </summary>
    [Serializable]
    [Preserve]
    public partial class IapSubscriptionInfoResult
    {
        [Preserve]
        public string Status;
        [Preserve]
        public double CatalogId;
        [Preserve]
        public string ExpiresAt;
        [Preserve]
        public bool IsAutoRenew;
        [Preserve]
        public string GracePeriodExpiresAt;
        [Preserve]
        public bool IsAccessible;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ImageResponse
    {
        /// <summary>가져온 사진의 고유 ID예요.</summary>
        [Preserve]
        public string Id;
        /// <summary>사진의 데이터 URI예요. base64 옵션이 true인 경우 Base64 문자열로 반환돼요.</summary>
        [Preserve]
        public string DataUri;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class LoadAdMobEvent
    {
        [Preserve]
        public string Type;
        [Preserve]
        public AdMobLoadResult Data;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class Location
    {
        [Preserve]
        [Obsolete("Use AccessLocationTyped")]
        public string AccessLocation; // optional

        /// <summary>
        /// <see cref="AccessLocation"/>를 <see cref="LocationAccessLocation"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="AccessLocation"/>에 그대로 남는다. 값을 쓰면 <see cref="AccessLocation"/>도 함께 바뀐다.
        /// </summary>
        public LocationAccessLocation? AccessLocationTyped
        {
            get => AITCompat.ParseEnum<LocationAccessLocation>(AccessLocation);
            set => AccessLocation = AITCompat.ToWire(value);
        }
        [Preserve]
        public double Timestamp;
        [Preserve]
        public LocationCoords Coords;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class LocationCoords
    {
        /// <summary>위도</summary>
        [Preserve]
        public double Latitude;
        /// <summary>경도</summary>
        [Preserve]
        public double Longitude;
        /// <summary>높이</summary>
        [Preserve]
        public double Altitude;
        /// <summary>위치 정확도</summary>
        [Preserve]
        public double Accuracy;
        /// <summary>고도 정확도</summary>
        [Preserve]
        public double AltitudeAccuracy;
        /// <summary>방향</summary>
        [Preserve]
        public double Heading;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDump
    {
        [Preserve]
        public string Origin;
        [Preserve]
        public Dictionary<string, object> LocalStorage;
        [Preserve]
        public OriginStorageDumpIndexedDB[] IndexedDB;
        [Preserve]
        public OriginStorageDumpOpfs Opfs;
        [Preserve]
        public OriginStorageDumpError[] Errors;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDBObjectStoreIndex
    {
        [Preserve]
        public string Name;
        /// <summary>인덱스의 keyPath예요. 문자열 또는 문자열 배열(JArray)로 와요.</summary>
        [Preserve]
        public object KeyPath;
        [Preserve]
        public bool MultiEntry;
        [Preserve]
        public bool Unique;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDBObjectStoreRecord
    {
        [Preserve]
        public object Key;
        [Preserve]
        public object PrimaryKey;
        [Preserve]
        public object Value;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDBObjectStore
    {
        [Preserve]
        public string Name;
        /// <summary>저장소의 keyPath예요. 문자열, 문자열 배열(JArray), null 중 하나로 와요.</summary>
        [Preserve]
        public object KeyPath;
        [Preserve]
        public bool AutoIncrement;
        [Preserve]
        public OriginStorageDumpIndexedDBObjectStoreIndex[] Indexes;
        [Preserve]
        public OriginStorageDumpIndexedDBObjectStoreRecord[] Records;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDB
    {
        [Preserve]
        public string Name;
        [Preserve]
        public double Version;
        [Preserve]
        public OriginStorageDumpIndexedDBObjectStore[] ObjectStores;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpOpfsFile
    {
        [Preserve]
        public string Path;
        [Preserve]
        public string Type;
        [Preserve]
        public double LastModified;
        [Preserve]
        public byte[] Data;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpOpfs
    {
        [Preserve]
        public string[] Directories;
        [Preserve]
        public OriginStorageDumpOpfsFile[] Files;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpError
    {
        [Preserve]
        public string Storage;
        [Preserve]
        public string Message;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SetNavigationBarOptions
    {
        [Preserve]
        public bool? WithBackButton; // optional
        [Preserve]
        public bool? WithHomeButton; // optional
        [Preserve]
        public bool? WithTitle; // optional
        [Preserve]
        public bool? TransparentBackground; // optional
        [Preserve]
        public string BackgroundColor; // optional
        [Preserve]
        public NavigationBarTheme Theme; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    /// <summary>
    /// 이전 SDK의 SafeAreaInsetsSubscribe 매개변수 묶음이다. 새 코드는
    /// <c>AIT.SafeAreaInsetsSubscribe(onEvent, onError)</c>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("AIT.SafeAreaInsetsSubscribe(Action<SafeAreaInsets> onEvent, Action<AITException> onError)를 사용하세요.")]
    public partial class SafeAreaInsetsSubscribe__0
    {
        public System.Action<SafeAreaInsets> OnEvent;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    /// <summary>
    /// 이전 생성기가 잘못 만들어 공개한 빈 클래스다. 쓸 곳이 없다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("쓸 곳이 없는 빈 클래스예요.")]
    public partial class number
    {
    }
}
