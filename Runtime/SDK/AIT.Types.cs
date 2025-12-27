// -----------------------------------------------------------------------
// <copyright file="AIT.Types.cs" company="Toss">
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
    public enum AppLoginResultReferrer
    {
        [EnumMember(Value = "DEFAULT")]
        DEFAULT,
        [EnumMember(Value = "SANDBOX")]
        SANDBOX
    }

    public enum ContactsViralSuccessEventDataCloseReason
    {
        [EnumMember(Value = "clickBackButton")]
        ClickBackButton,
        [EnumMember(Value = "noReward")]
        NoReward
    }

    public enum GetPermissionPermissionName
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
        Camera
    }

    public enum GetPermissionPermissionAccess
    {
        [EnumMember(Value = "read")]
        Read,
        [EnumMember(Value = "write")]
        Write,
        [EnumMember(Value = "access")]
        Access
    }

    public enum CompletedOrRefundedOrdersResultOrderStatus
    {
        [EnumMember(Value = "COMPLETED")]
        COMPLETED,
        [EnumMember(Value = "REFUNDED")]
        REFUNDED
    }

    public enum OpenPermissionDialogPermissionName
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
        Camera
    }

    public enum OpenPermissionDialogPermissionAccess
    {
        [EnumMember(Value = "read")]
        Read,
        [EnumMember(Value = "write")]
        Write,
        [EnumMember(Value = "access")]
        Access
    }

    public enum RequestPermissionPermissionName
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
        Camera
    }

    public enum RequestPermissionPermissionAccess
    {
        [EnumMember(Value = "read")]
        Read,
        [EnumMember(Value = "write")]
        Write,
        [EnumMember(Value = "access")]
        Access
    }

    public enum SetDeviceOrientationOptionsType
    {
        [EnumMember(Value = "portrait")]
        Portrait,
        [EnumMember(Value = "landscape")]
        Landscape
    }

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
    }


    [Serializable]
    [Preserve]
    public class AppLoginResult
    {
        [Preserve]
        [JsonProperty("authorizationCode")]
        public string AuthorizationCode;
        [Preserve]
        [JsonProperty("referrer")]
        public AppLoginResultReferrer Referrer;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public ContactsViralEventData Data;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralEventData
    {
        [Preserve]
        [JsonProperty("rewardAmount")]
        public double RewardAmount;
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit;
    }

    [Serializable]
    [Preserve]
    public class RewardFromContactsViralEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public RewardFromContactsViralEventData Data;
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
    }

    [Serializable]
    [Preserve]
    public class ContactsViralSuccessEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public ContactsViralSuccessEventData Data;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralSuccessEventData
    {
        [Preserve]
        [JsonProperty("closeReason")]
        public ContactsViralSuccessEventDataCloseReason CloseReason;
        [Preserve]
        [JsonProperty("sentRewardAmount")]
        public double? SentRewardAmount; // optional
        [Preserve]
        [JsonProperty("sendableRewardsCount")]
        public double? SendableRewardsCount; // optional
        [Preserve]
        [JsonProperty("sentRewardsCount")]
        public double SentRewardsCount;
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit; // optional
    }

    [Serializable]
    [Preserve]
    public class Location
    {
        [Preserve]
        [JsonProperty("accessLocation")]
        public string AccessLocation; // optional
        [Preserve]
        [JsonProperty("timestamp")]
        public double Timestamp;
        [Preserve]
        [JsonProperty("coords")]
        public LocationCoords Coords;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    [Preserve]
    public class GetPermissionPermission
    {
        [Preserve]
        [JsonProperty("name")]
        public GetPermissionPermissionName Name;
        [Preserve]
        [JsonProperty("access")]
        public GetPermissionPermissionAccess Access;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameOptions
    {
        [Preserve]
        [JsonProperty("params")]
        public GrantPromotionRewardForGameOptionsParams Params;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameOptionsParams
    {
        [Preserve]
        [JsonProperty("promotionCode")]
        public string PromotionCode;
        [Preserve]
        [JsonProperty("amount")]
        public double Amount;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameResult
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
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobLoadAdMobInterstitialAdArgs
    {
        [JsonIgnore]
        public System.Action<LoadAdMobInterstitialAdEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public LoadAdMobInterstitialAdOptions Options; // optional
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobInterstitialAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public InterstitialAd Data;
    }

    [Serializable]
    [Preserve]
    public class InterstitialAd
    {
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
        [Preserve]
        [JsonProperty("responseInfo")]
        public ResponseInfo ResponseInfo;
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
        public object LoadedAdNetworkInfo;
        [Preserve]
        [JsonProperty("responseId")]
        public string ResponseId;
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
    }

    [Serializable]
    [Preserve]
    public class Error
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        [Preserve]
        [JsonProperty("message")]
        public string Message;
        [Preserve]
        [JsonProperty("stack")]
        public string Stack; // optional
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobShowAdMobInterstitialAdArgs
    {
        [JsonIgnore]
        public System.Action<ShowAdMobInterstitialAdEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public ShowAdMobInterstitialAdOptions Options; // optional
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobInterstitialAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobLoadAdMobRewardedAdArgs
    {
        [JsonIgnore]
        public System.Action<LoadAdMobRewardedAdEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public LoadAdMobRewardedAdOptions Options; // optional
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobRewardedAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public RewardedAd Data;
    }

    [Serializable]
    [Preserve]
    public class RewardedAd
    {
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
        [Preserve]
        [JsonProperty("responseInfo")]
        public ResponseInfo ResponseInfo;
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobShowAdMobRewardedAdArgs
    {
        [JsonIgnore]
        public System.Action<ShowAdMobRewardedAdEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public ShowAdMobRewardedAdOptions Options; // optional
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobRewardedAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobLoadAppsInTossAdMobArgs
    {
        [JsonIgnore]
        public System.Action<LoadAdMobEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public LoadAdMobOptions Options; // optional
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
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobShowAppsInTossAdMobArgs
    {
        [JsonIgnore]
        public System.Action<ShowAdMobEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public ShowAdMobOptions Options; // optional
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
        public ShowAdMobEventData Data;
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
    }

    [Serializable]
    [Preserve]
    public class DataType
    {
        [Preserve]
        [JsonProperty("unitType")]
        public string UnitType;
        [Preserve]
        [JsonProperty("unitAmount")]
        public double UnitAmount;
    }

    [Serializable]
    [Preserve]
    public class IapCreateOneTimePurchaseOrderOptions
    {
        [Preserve]
        [JsonProperty("options")]
        public IapCreateOneTimePurchaseOrderOptionsOptions Options;
        [JsonIgnore]
        public System.Action<SuccessEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;
    }

    [Serializable]
    [Preserve]
    public class IapCreateOneTimePurchaseOrderOptionsOptions
    {
        [Preserve]
        [JsonProperty("productId")]
        public string ProductId;
        [Preserve]
        [JsonProperty("sku")]
        public string Sku; // optional
        [Preserve]
        [JsonProperty("processProductGrant")]
        public System.Func<object, object> ProcessProductGrant;
    }

    [Serializable]
    [Preserve]
    public class SuccessEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public IapCreateOneTimePurchaseOrderResult Data;
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
    }

    [Serializable]
    [Preserve]
    public class IAPGetProductItemListResult
    {
        [Preserve]
        [JsonProperty("products")]
        public IapProductListItem[] Products;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
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
    }

    [Serializable]
    [Preserve]
    public class IAPGetPendingOrdersResult
    {
        [Preserve]
        [JsonProperty("orders")]
        public IAPGetPendingOrdersResultOrder[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    [Preserve]
    public class IAPGetPendingOrdersResultOrder
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("sku")]
        public string Sku;
        [Preserve]
        [JsonProperty("paymentCompletedDate")]
        public string PaymentCompletedDate;
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
        public string error;
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
    }

    [Serializable]
    [Preserve]
    public class IAPCompleteProductGrantArgs_0
    {
        [Preserve]
        [JsonProperty("params")]
        public IAPCompleteProductGrantArgs_0Params Params;
    }

    [Serializable]
    [Preserve]
    public class IAPCompleteProductGrantArgs_0Params
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
    }

    [Serializable]
    [Preserve]
    public class SafeAreaInsetsGetResult
    {
        [Preserve]
        [JsonProperty("top")]
        public double Top;
        [Preserve]
        [JsonProperty("bottom")]
        public double Bottom;
        [Preserve]
        [JsonProperty("left")]
        public double Left;
        [Preserve]
        [JsonProperty("right")]
        public double Right;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    [Preserve]
    public class SafeAreaInsetsSubscribe__0
    {
        [JsonIgnore]
        public System.Action<SafeAreaInsets> OnEvent;
    }

    [Serializable]
    [Preserve]
    public class SafeAreaInsets
    {
        [Preserve]
        [JsonProperty("top")]
        public double Top;
        [Preserve]
        [JsonProperty("bottom")]
        public double Bottom;
        [Preserve]
        [JsonProperty("left")]
        public double Left;
        [Preserve]
        [JsonProperty("right")]
        public double Right;
    }

    [Serializable]
    [Preserve]
    public class InitializeOptions
    {
        [Preserve]
        [JsonProperty("callbacks")]
        public InitializeOptionsCallbacks Callbacks; // optional
    }

    [Serializable]
    [Preserve]
    public class InitializeOptionsCallbacks
    {
        [Preserve]
        [JsonProperty("onInitialized")]
        public object OnInitialized; // optional
        [Preserve]
        [JsonProperty("onInitializationFailed")]
        public object OnInitializationFailed; // optional
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
        public string error;
    }

    [Serializable]
    [Preserve]
    public class IsMinVersionSupportedMinVersions
    {
        [Preserve]
        [JsonProperty("android")]
        public string Android;
        [Preserve]
        [JsonProperty("ios")]
        public string Ios;
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
    }

    [Serializable]
    [Preserve]
    public class AddAccessoryButtonOptionsIcon
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
    }

    [Serializable]
    [Preserve]
    public class OnVisibilityChangedByTransparentServiceWebEventParams
    {
        [Preserve]
        [JsonProperty("options")]
        public OnVisibilityChangedByTransparentServiceWebEventParamsOptions Options;
        [JsonIgnore]
        public System.Action<bool> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;
    }

    [Serializable]
    [Preserve]
    public class OnVisibilityChangedByTransparentServiceWebEventParamsOptions
    {
        [Preserve]
        [JsonProperty("callbackId")]
        public string CallbackId;
    }

    [Serializable]
    [Preserve]
    public class OpenPermissionDialogPermission
    {
        [Preserve]
        [JsonProperty("name")]
        public OpenPermissionDialogPermissionName Name;
        [Preserve]
        [JsonProperty("access")]
        public OpenPermissionDialogPermissionAccess Access;
    }

    [Serializable]
    [Preserve]
    public class RequestPermissionPermission
    {
        [Preserve]
        [JsonProperty("name")]
        public RequestPermissionPermissionName Name;
        [Preserve]
        [JsonProperty("access")]
        public RequestPermissionPermissionAccess Access;
    }

    [Serializable]
    [Preserve]
    public class SetDeviceOrientationOptions
    {
        [Preserve]
        [JsonProperty("type")]
        public SetDeviceOrientationOptionsType Type;
    }

    [Serializable]
    [Preserve]
    public class SetIosSwipeGestureEnabledOptions
    {
        [Preserve]
        [JsonProperty("isEnabled")]
        public bool IsEnabled;
    }

    [Serializable]
    [Preserve]
    public class SetScreenAwakeModeOptions
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
    }

    [Serializable]
    [Preserve]
    public class SetScreenAwakeModeResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    [Preserve]
    public class SetSecureScreenOptions
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
    }

    [Serializable]
    [Preserve]
    public class SetSecureScreenResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    [Preserve]
    public class ShareMessage
    {
        [Preserve]
        [JsonProperty("message")]
        public string Message;
    }

    [Serializable]
    [Preserve]
    public class StartUpdateLocationEventParams
    {
        [JsonIgnore]
        public System.Action<Location> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;
        [Preserve]
        [JsonProperty("options")]
        public StartUpdateLocationOptions Options;
    }

    [Serializable]
    [Preserve]
    public class SubmitGameCenterLeaderBoardScoreParams
    {
        [Preserve]
        [JsonProperty("score")]
        public string Score;
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobInterstitialAdOptions
    {
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobInterstitialAdOptions
    {
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobRewardedAdOptions
    {
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobRewardedAdOptions
    {
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
    }

    [Serializable]
    [Preserve]
    public class TossAdsAttachOptions
    {
        [Preserve]
        [JsonProperty("theme")]
        public string Theme; // optional
        [Preserve]
        [JsonProperty("padding")]
        public string Padding; // optional
        [Preserve]
        [JsonProperty("callbacks")]
        public BannerSlotCallbacks Callbacks; // optional
    }

    [Serializable]
    [Preserve]
    public class BannerSlotCallbacks
    {
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdRendered; // optional
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdViewable; // optional
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdClicked; // optional
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdImpression; // optional
        [JsonIgnore]
        public System.Action<BannerSlotErrorPayload> OnAdFailedToRender; // optional
        [JsonIgnore]
        public System.Action<object> OnNoFill; // optional
    }

    [Serializable]
    [Preserve]
    public class BannerSlotEventPayload
    {
        [Preserve]
        [JsonProperty("slotId")]
        public string SlotId;
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adMetadata")]
        public object AdMetadata;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotErrorPayload
    {
        [Preserve]
        [JsonProperty("slotId")]
        public string SlotId;
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adMetadata")]
        public Dictionary<string, object> AdMetadata;
        [Preserve]
        [JsonProperty("error")]
        public object Error;
    }

    [Serializable]
    [Preserve]
    public class AppsInTossSignTossCertParams
    {
        [Preserve]
        [JsonProperty("txId")]
        public string TxId;
        [Preserve]
        [JsonProperty("skipConfirmDoc")]
        public bool? SkipConfirmDoc; // optional
    }

    [Serializable]
    [Preserve]
    public class CheckoutPaymentOptions
    {
        /// <summary>결제 토큰이에요.</summary>
        [Preserve]
        [JsonProperty("payToken")]
        public string PayToken;
    }

    [Serializable]
    [Preserve]
    public class CheckoutPaymentResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        [Preserve]
        [JsonProperty("success")]
        public bool Success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        [Preserve]
        [JsonProperty("reason")]
        public string Reason; // optional
    }

    [Serializable]
    [Preserve]
    public class ContactsViralParams
    {
        [Preserve]
        [JsonProperty("options")]
        public ContactsViralParamsOptions Options;
        [JsonIgnore]
        public System.Action<ContactsViralEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralParamsOptions
    {
        [Preserve]
        [JsonProperty("moduleId")]
        public string ModuleId;
    }

    [Serializable]
    [Preserve]
    public class EventLogParams
    {
        [Preserve]
        [JsonProperty("log_name")]
        public string Log_name;
        [Preserve]
        [JsonProperty("log_type")]
        public string Log_type;
        [Preserve]
        [JsonProperty("params")]
        public Dictionary<string, object> Params;
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
    /// 앨범 사진을 조회할 때 사용하는 옵션 타입이에요.
    /// </summary>
    [Serializable]
    [Preserve]
    public class FetchAlbumPhotosOptions
    {
        /// <summary>가져올 사진의 최대 개수를 설정해요. 숫자를 입력하고 기본값은 10이에요.</summary>
        [Preserve]
        [JsonProperty("maxCount")]
        public double? MaxCount; // optional
        /// <summary>사진의 최대 폭을 제한해요. 단위는 픽셀이고 기본값은 1024이에요.</summary>
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional
        /// <summary>이미지를 base64 형식으로 반환할지 설정해요. 기본값은 false예요.</summary>
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional
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
    }

    [Serializable]
    [Preserve]
    public class FetchContactsOptions
    {
        [Preserve]
        [JsonProperty("size")]
        public double Size;
        [Preserve]
        [JsonProperty("offset")]
        public double Offset;
        [Preserve]
        [JsonProperty("query")]
        public FetchContactsOptionsQuery Query; // optional
    }

    [Serializable]
    [Preserve]
    public class FetchContactsOptionsQuery
    {
        [Preserve]
        [JsonProperty("contains")]
        public string Contains; // optional
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
    public class GetCurrentLocationOptions
    {
        /// <summary>위치 정보를 가져올 정확도 수준이에요.</summary>
        [Preserve]
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;
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
        Camera
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
    }

    [Serializable]
    [Preserve]
    public class GetUserKeyForGameErrorResponse
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameSuccessResponse
    {
        [Preserve]
        [JsonProperty("key")]
        public string Key;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardForGameErrorResponse
    {
        [Preserve]
        [JsonProperty("code")]
        public string Code;
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
    }

    [Serializable]
    [Preserve]
    public class OpenCameraOptions
    {
        /// <summary>이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값: false.</summary>
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional
        /// <summary>이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값: 1024.</summary>
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional
    }

    [Serializable]
    [Preserve]
    public class SaveBase64DataParams
    {
        [Preserve]
        [JsonProperty("data")]
        public string Data;
        [Preserve]
        [JsonProperty("fileName")]
        public string FileName;
        [Preserve]
        [JsonProperty("mimeType")]
        public string MimeType;
    }

    [Serializable]
    [Preserve]
    public class StartUpdateLocationOptions
    {
        /// <summary>위치 정확도를 설정해요.</summary>
        [Preserve]
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;
        /// <summary>위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.</summary>
        [Preserve]
        [JsonProperty("timeInterval")]
        public double TimeInterval;
        /// <summary>위치 변경 거리를 미터(m) 단위로 설정해요.</summary>
        [Preserve]
        [JsonProperty("distanceInterval")]
        public double DistanceInterval;
    }

    [Serializable]
    [Preserve]
    public class SubmitGameCenterLeaderBoardScoreResponse
    {
        [Preserve]
        [JsonProperty("statusCode")]
        public string StatusCode;
    }

    [Serializable]
    [Preserve]
    public class LoadFullScreenAdOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
    }

    [Serializable]
    [Preserve]
    public class LoadFullScreenAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    [Preserve]
    public class ShowFullScreenAdOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
    }

    /// <summary>
    /// Data for userEarnedReward event
    /// </summary>
    [Serializable]
    [Preserve]
    public class ShowFullScreenAdEventData
    {
        [Preserve]
        [JsonProperty("unitType")]
        public string UnitType;
        [Preserve]
        [JsonProperty("unitAmount")]
        public double UnitAmount;
    }

    /// <summary>
    /// Full screen ad event (discriminated union)
    /// </summary>
    [Serializable]
    [Preserve]
    public class ShowFullScreenAdEvent
    {
        /// <summary>Event type: clicked, dismissed, failedToShow, impression, show, userEarnedReward, requested</summary>
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        /// <summary>Event data (only for userEarnedReward)</summary>
        [Preserve]
        [JsonProperty("data")]
        public ShowFullScreenAdEventData Data; // optional
    }
}
