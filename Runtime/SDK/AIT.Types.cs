// -----------------------------------------------------------------------
// <copyright file="AIT.Types.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Result type for GetUserKeyForGame (Discriminated Union)
    /// Success: GetUserKeyForGameSuccessResponse | Error: INVALID_CATEGORY,ERROR
    /// </summary>
    [Serializable]
    public class GetUserKeyForGameResult
    {
        public string _type;
        public string _successJson;
        public string _errorCode;

        /// <summary>성공 여부를 나타냅니다.</summary>
        public bool IsSuccess => _type == "success";

        /// <summary>에러 여부를 나타냅니다.</summary>
        public bool IsError => _type == "error";

        /// <summary>
        /// 성공 데이터를 가져옵니다.
        /// </summary>
        public GetUserKeyForGameSuccessResponse GetSuccess()
            => IsSuccess ? JsonUtility.FromJson<GetUserKeyForGameSuccessResponse>(_successJson) : null;

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
    public class AppLoginResult
    {
        [JsonProperty("authorizationCode")]
        public string AuthorizationCode;
        [JsonProperty("referrer")]
        public string Referrer;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class ContactsViralEventData
    {
        [JsonProperty("rewardAmount")]
        public double RewardAmount;
        [JsonProperty("rewardUnit")]
        public string RewardUnit;
    }

    [Serializable]
    public class ContactsViralEvent
    {
        [JsonProperty("type")]
        public string Type;
        [JsonProperty("data")]
        public ContactsViralEventData Data;
    }

    [Serializable]
    public class Location
    {
        [JsonProperty("accessLocation")]
        public string AccessLocation; // optional
        [JsonProperty("timestamp")]
        public double Timestamp;
        [JsonProperty("coords")]
        public LocationCoords Coords;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class GameCenterGameProfileResponse
    {
        [JsonProperty("statusCode")]
        public string StatusCode;
        [JsonProperty("nickname")]
        public string Nickname;
        [JsonProperty("profileImageUri")]
        public string ProfileImageUri;
    }

    [Serializable]
    public class GetPermissionPermission
    {
        [JsonProperty("name")]
        public string Name;
        [JsonProperty("access")]
        public PermissionAccess Access;
    }

    [Serializable]
    public class GrantPromotionRewardForGameOptionsParams
    {
        [JsonProperty("promotionCode")]
        public string PromotionCode;
        [JsonProperty("amount")]
        public double Amount;
    }

    [Serializable]
    public class GrantPromotionRewardForGameOptions
    {
        [JsonProperty("params")]
        public GrantPromotionRewardForGameOptionsParams Params;
    }

    [Serializable]
    public class GrantPromotionRewardForGameResult
    {
        [JsonProperty("key")]
        public string Key;
        [JsonProperty("code")]
        public string Code;
        [JsonProperty("errorCode")]
        public string ErrorCode;
        [JsonProperty("message")]
        public string Message;
    }

    [Serializable]
    public class IapCreateOneTimePurchaseOrderOptions
    {
        [JsonProperty("options")]
        public System.Action Options;
        [JsonProperty("onEvent")]
        public System.Action<SuccessEvent> OnEvent;
        [JsonProperty("onError")]
        public System.Action<object> OnError;
    }

    [Serializable]
    public class SuccessEvent
    {
        [JsonProperty("type")]
        public string Type;
        [JsonProperty("data")]
        public IapCreateOneTimePurchaseOrderResult Data;
    }

    [Serializable]
    public class IAPGetProductItemListResult
    {
        [JsonProperty("products")]
        public IapProductListItem[] Products;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class IAPGetPendingOrdersResult
    {
        [JsonProperty("orders")]
        public object[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class CompletedOrRefundedOrdersResult
    {
        [JsonProperty("hasNext")]
        public bool HasNext;
        [JsonProperty("nextKey")]
        public string NextKey; // optional
        [JsonProperty("orders")]
        public object[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class IAPCompleteProductGrantArgs_0Params
    {
        [JsonProperty("orderId")]
        public string OrderId;
    }

    [Serializable]
    public class IAPCompleteProductGrantArgs_0
    {
        [JsonProperty("params")]
        public IAPCompleteProductGrantArgs_0Params Params;
    }

    [Serializable]
    public class SafeAreaInsetsGetResult
    {
        [JsonProperty("top")]
        public double Top;
        [JsonProperty("bottom")]
        public double Bottom;
        [JsonProperty("left")]
        public double Left;
        [JsonProperty("right")]
        public double Right;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class AppsInTossGlobals
    {
        [JsonProperty("deploymentId")]
        public string DeploymentId;
        [JsonProperty("brandDisplayName")]
        public string BrandDisplayName;
        [JsonProperty("brandIcon")]
        public string BrandIcon;
        [JsonProperty("brandPrimaryColor")]
        public string BrandPrimaryColor;
        [JsonProperty("brandBridgeColorMode")]
        public string BrandBridgeColorMode;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class IsMinVersionSupportedMinVersions
    {
        [JsonProperty("android")]
        public string Android;
        [JsonProperty("ios")]
        public string Ios;
    }

    [Serializable]
    public class PartnerAddAccessoryButtonArgs_0Icon
    {
        [JsonProperty("name")]
        public string Name;
    }

    [Serializable]
    public class PartnerAddAccessoryButtonArgs_0
    {
        [JsonProperty("id")]
        public string Id;
        [JsonProperty("title")]
        public string Title;
        [JsonProperty("icon")]
        public PartnerAddAccessoryButtonArgs_0Icon Icon;
    }

    [Serializable]
    public class OpenPermissionDialogPermission
    {
        [JsonProperty("name")]
        public string Name;
        [JsonProperty("access")]
        public PermissionAccess Access;
    }

    [Serializable]
    public class RequestPermissionPermission
    {
        [JsonProperty("name")]
        public string Name;
        [JsonProperty("access")]
        public PermissionAccess Access;
    }

    [Serializable]
    public class SetDeviceOrientationOptions
    {
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    public class SetIosSwipeGestureEnabledOptions
    {
        [JsonProperty("isEnabled")]
        public bool IsEnabled;
    }

    [Serializable]
    public class SetScreenAwakeModeOptions
    {
        [JsonProperty("enabled")]
        public bool Enabled;
    }

    [Serializable]
    public class SetScreenAwakeModeResult
    {
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class SetSecureScreenOptions
    {
        [JsonProperty("enabled")]
        public bool Enabled;
    }

    [Serializable]
    public class SetSecureScreenResult
    {
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class ShareMessage
    {
        [JsonProperty("message")]
        public string Message;
    }

    [Serializable]
    public class StartUpdateLocationEventParams
    {
        [JsonProperty("onEvent")]
        public System.Action<Location> OnEvent;
        [JsonProperty("onError")]
        public System.Action<object> OnError;
        [JsonProperty("options")]
        public StartUpdateLocationOptions Options;
    }

    [Serializable]
    public class SubmitGameCenterLeaderBoardScoreParams
    {
        [JsonProperty("score")]
        public string Score;
    }

    [Serializable]
    public class AppsInTossSignTossCertParams
    {
        [JsonProperty("txId")]
        public string TxId;
    }

    [Serializable]
    public class CheckoutPaymentOptions
    {
        /// <summary>결제 토큰이에요.</summary>
        [JsonProperty("payToken")]
        public string PayToken;
    }

    [Serializable]
    public class CheckoutPaymentResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        [JsonProperty("success")]
        public bool Success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        [JsonProperty("reason")]
        public string Reason; // optional
    }

    [Serializable]
    public class ContactsViralParamsOptions
    {
        [JsonProperty("moduleId")]
        public string ModuleId;
    }

    [Serializable]
    public class ContactsViralParams
    {
        [JsonProperty("options")]
        public ContactsViralParamsOptions Options;
        [JsonProperty("onEvent")]
        public System.Action<ContactsViralEvent> OnEvent;
        [JsonProperty("onError")]
        public System.Action<object> OnError;
    }

    [Serializable]
    public class EventLogParams
    {
        [JsonProperty("log_name")]
        public string Log_name;
        [JsonProperty("log_type")]
        public string Log_type;
        [JsonProperty("params")]
        public Dictionary<string, object> Params;
    }

    /// <summary>
    /// 앨범 사진을 조회할 때 사용하는 옵션 타입이에요.
    /// </summary>
    [Serializable]
    public class FetchAlbumPhotosOptions
    {
        /// <summary>가져올 사진의 최대 개수를 설정해요. 숫자를 입력하고 기본값은 10이에요.</summary>
        [JsonProperty("maxCount")]
        public double MaxCount; // optional
        /// <summary>사진의 최대 폭을 제한해요. 단위는 픽셀이고 기본값은 1024이에요.</summary>
        [JsonProperty("maxWidth")]
        public double MaxWidth; // optional
        /// <summary>이미지를 base64 형식으로 반환할지 설정해요. 기본값은 false예요.</summary>
        [JsonProperty("base64")]
        public bool Base64; // optional
    }

    /// <summary>
    /// 사진 조회 결과를 나타내는 타입이에요.
    /// </summary>
    [Serializable]
    public class ImageResponse
    {
        /// <summary>가져온 사진의 고유 ID예요.</summary>
        [JsonProperty("id")]
        public string Id;
        /// <summary>사진의 데이터 URI예요. base64 옵션이 true인 경우 Base64 문자열로 반환돼요.</summary>
        [JsonProperty("dataUri")]
        public string DataUri;
    }

    [Serializable]
    public class FetchContactsOptionsQuery
    {
        [JsonProperty("contains")]
        public string Contains; // optional
    }

    [Serializable]
    public class FetchContactsOptions
    {
        [JsonProperty("size")]
        public double Size;
        [JsonProperty("offset")]
        public double Offset;
        [JsonProperty("query")]
        public FetchContactsOptionsQuery Query; // optional
    }

    /// <summary>
    /// 연락처 정보를 나타내는 타입이에요.
    /// </summary>
    [Serializable]
    public class ContactEntity
    {
        /// <summary>연락처 이름이에요.</summary>
        [JsonProperty("name")]
        public string Name;
        /// <summary>연락처 전화번호로, 문자열 형식이에요.</summary>
        [JsonProperty("phoneNumber")]
        public string PhoneNumber;
    }

    [Serializable]
    public class ContactResult
    {
        [JsonProperty("result")]
        public ContactEntity[] Result;
        [JsonProperty("nextOffset")]
        public double NextOffset;
        [JsonProperty("done")]
        public bool Done;
    }

    public enum HapticFeedbackType
    {
        TickWeak,
        Tap,
        TickMedium,
        SoftMedium,
        BasicWeak,
        BasicMedium,
        Success,
        Error,
        Wiggle,
        Confetti
    }

    [Serializable]
    public class HapticFeedbackOptions
    {
        [JsonProperty("type")]
        public HapticFeedbackType Type;
    }

    public enum Accuracy
    {
        Lowest,
        Low,
        Balanced,
        High,
        Highest,
        BestForNavigation
    }

    [Serializable]
    public class GetCurrentLocationOptions
    {
        /// <summary>위치 정보를 가져올 정확도 수준이에요.</summary>
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;
    }

    [Serializable]
    public class LocationCoords
    {
        /// <summary>위도</summary>
        [JsonProperty("latitude")]
        public double Latitude;
        /// <summary>경도</summary>
        [JsonProperty("longitude")]
        public double Longitude;
        /// <summary>높이</summary>
        [JsonProperty("altitude")]
        public double Altitude;
        /// <summary>위치 정확도</summary>
        [JsonProperty("accuracy")]
        public double Accuracy;
        /// <summary>고도 정확도</summary>
        [JsonProperty("altitudeAccuracy")]
        public double AltitudeAccuracy;
        /// <summary>방향</summary>
        [JsonProperty("heading")]
        public double Heading;
    }

    public enum NetworkStatus
    {
        OFFLINE,
        WIFI,
        _2G,
        _3G,
        _4G,
        _5G,
        WWAN,
        UNKNOWN
    }

    public enum PermissionAccess
    {
        Read,
        Write,
        Access
    }

    [Serializable]
    public class GetUserKeyForGameSuccessResponse
    {
        [JsonProperty("hash")]
        public string Hash;
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    public class GetUserKeyForGameErrorResponse
    {
        [JsonProperty("type")]
        public string Type;
    }

    [Serializable]
    public class GrantPromotionRewardForGameSuccessResponse
    {
        [JsonProperty("key")]
        public string Key;
    }

    [Serializable]
    public class GrantPromotionRewardForGameErrorResponse
    {
        [JsonProperty("code")]
        public string Code;
    }

    [Serializable]
    public class GrantPromotionRewardForGameErrorResult
    {
        [JsonProperty("errorCode")]
        public string ErrorCode;
        [JsonProperty("message")]
        public string Message;
    }

    [Serializable]
    public class OpenCameraOptions
    {
        /// <summary>이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값: false.</summary>
        [JsonProperty("base64")]
        public bool Base64; // optional
        /// <summary>이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값: 1024.</summary>
        [JsonProperty("maxWidth")]
        public double MaxWidth; // optional
    }

    [Serializable]
    public class SaveBase64DataParams
    {
        [JsonProperty("data")]
        public string Data;
        [JsonProperty("fileName")]
        public string FileName;
        [JsonProperty("mimeType")]
        public string MimeType;
    }

    [Serializable]
    public class StartUpdateLocationOptions
    {
        /// <summary>위치 정확도를 설정해요.</summary>
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;
        /// <summary>위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.</summary>
        [JsonProperty("timeInterval")]
        public double TimeInterval;
        /// <summary>위치 변경 거리를 미터(m) 단위로 설정해요.</summary>
        [JsonProperty("distanceInterval")]
        public double DistanceInterval;
    }

    [Serializable]
    public class SubmitGameCenterLeaderBoardScoreResponse
    {
        [JsonProperty("statusCode")]
        public string StatusCode;
    }
}
