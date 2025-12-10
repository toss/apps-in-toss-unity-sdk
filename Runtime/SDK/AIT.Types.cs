// -----------------------------------------------------------------------
// <copyright file="AIT.Types.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
        public string authorizationCode;
        public string referrer;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class ContactsViralEventData
    {
        public double rewardAmount;
        public string rewardUnit;
    }

    [Serializable]
    public class ContactsViralEvent
    {
        public string type;
        public ContactsViralEventData data;
    }

    [Serializable]
    public class Location
    {
        public string accessLocation; // optional
        public double timestamp;
        public LocationCoords coords;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class GameCenterGameProfileResponse
    {
        public string statusCode;
        public string nickname;
        public string profileImageUri;
    }

    [Serializable]
    public class GetPermissionPermission
    {
        public string name;
        public PermissionAccess access;
    }

    [Serializable]
    public class GrantPromotionRewardForGameOptionsParams
    {
        public string promotionCode;
        public double amount;
    }

    [Serializable]
    public class GrantPromotionRewardForGameOptions
    {
        public GrantPromotionRewardForGameOptionsParams params;
    }

    [Serializable]
    public class GrantPromotionRewardForGameResult
    {
        public string key;
        public string code;
        public string errorCode;
        public string message;
    }

    [Serializable]
    public class OpenPermissionDialogPermission
    {
        public string name;
        public PermissionAccess access;
    }

    [Serializable]
    public class RequestPermissionPermission
    {
        public string name;
        public PermissionAccess access;
    }

    [Serializable]
    public class SetDeviceOrientationOptions
    {
        public string type;
    }

    [Serializable]
    public class SetIosSwipeGestureEnabledOptions
    {
        public bool isEnabled;
    }

    [Serializable]
    public class SetScreenAwakeModeOptions
    {
        public bool enabled;
    }

    [Serializable]
    public class SetScreenAwakeModeResult
    {
        public bool enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class SetSecureScreenOptions
    {
        public bool enabled;
    }

    [Serializable]
    public class SetSecureScreenResult
    {
        public bool enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;
    }

    [Serializable]
    public class ShareMessage
    {
        public string message;
    }

    [Serializable]
    public class StartUpdateLocationEventParams
    {
        public System.Action<Location> onEvent;
        public System.Action<object> onError;
        public StartUpdateLocationOptions options;
    }

    [Serializable]
    public class SubmitGameCenterLeaderBoardScoreParams
    {
        public string score;
    }

    [Serializable]
    public class AppsInTossSignTossCertParams
    {
        public string txId;
    }

    [Serializable]
    public class CheckoutPaymentOptions
    {
        /// <summary>결제 토큰이에요.</summary>
        public string payToken;
    }

    [Serializable]
    public class CheckoutPaymentResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        public bool success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        public string reason; // optional
    }

    [Serializable]
    public class ContactsViralParamsOptions
    {
        public string moduleId;
    }

    [Serializable]
    public class ContactsViralParams
    {
        public ContactsViralParamsOptions options;
        public System.Action<ContactsViralEvent> onEvent;
        public System.Action<object> onError;
    }

    [Serializable]
    public class EventLogParams
    {
        public string log_name;
        public string log_type;
        public Dictionary<string, object> params;
    }

    /// <summary>
    /// 앨범 사진을 조회할 때 사용하는 옵션 타입이에요.
    /// </summary>
    [Serializable]
    public class FetchAlbumPhotosOptions
    {
        /// <summary>가져올 사진의 최대 개수를 설정해요. 숫자를 입력하고 기본값은 10이에요.</summary>
        public double maxCount; // optional
        /// <summary>사진의 최대 폭을 제한해요. 단위는 픽셀이고 기본값은 1024이에요.</summary>
        public double maxWidth; // optional
        /// <summary>이미지를 base64 형식으로 반환할지 설정해요. 기본값은 false예요.</summary>
        public bool base64; // optional
    }

    /// <summary>
    /// 사진 조회 결과를 나타내는 타입이에요.
    /// </summary>
    [Serializable]
    public class ImageResponse
    {
        /// <summary>가져온 사진의 고유 ID예요.</summary>
        public string id;
        /// <summary>사진의 데이터 URI예요. base64 옵션이 true인 경우 Base64 문자열로 반환돼요.</summary>
        public string dataUri;
    }

    [Serializable]
    public class FetchContactsOptionsQuery
    {
        public string contains; // optional
    }

    [Serializable]
    public class FetchContactsOptions
    {
        public double size;
        public double offset;
        public FetchContactsOptionsQuery query; // optional
    }

    /// <summary>
    /// 연락처 정보를 나타내는 타입이에요.
    /// </summary>
    [Serializable]
    public class ContactEntity
    {
        /// <summary>연락처 이름이에요.</summary>
        public string name;
        /// <summary>연락처 전화번호로, 문자열 형식이에요.</summary>
        public string phoneNumber;
    }

    [Serializable]
    public class ContactResult
    {
        public ContactEntity[] result;
        public double nextOffset;
        public bool done;
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
        public HapticFeedbackType type;
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
        public Accuracy accuracy;
    }

    [Serializable]
    public class LocationCoords
    {
        /// <summary>위도</summary>
        public double latitude;
        /// <summary>경도</summary>
        public double longitude;
        /// <summary>높이</summary>
        public double altitude;
        /// <summary>위치 정확도</summary>
        public double accuracy;
        /// <summary>고도 정확도</summary>
        public double altitudeAccuracy;
        /// <summary>방향</summary>
        public double heading;
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
        public string hash;
        public string type;
    }

    [Serializable]
    public class GetUserKeyForGameErrorResponse
    {
        public string type;
    }

    [Serializable]
    public class GrantPromotionRewardForGameSuccessResponse
    {
        public string key;
    }

    [Serializable]
    public class GrantPromotionRewardForGameErrorResponse
    {
        public string code;
    }

    [Serializable]
    public class GrantPromotionRewardForGameErrorResult
    {
        public string errorCode;
        public string message;
    }

    [Serializable]
    public class OpenCameraOptions
    {
        /// <summary>이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값: false.</summary>
        public bool base64; // optional
        /// <summary>이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값: 1024.</summary>
        public double maxWidth; // optional
    }

    [Serializable]
    public class SaveBase64DataParams
    {
        public string data;
        public string fileName;
        public string mimeType;
    }

    [Serializable]
    public class StartUpdateLocationOptions
    {
        /// <summary>위치 정확도를 설정해요.</summary>
        public Accuracy accuracy;
        /// <summary>위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.</summary>
        public double timeInterval;
        /// <summary>위치 변경 거리를 미터(m) 단위로 설정해요.</summary>
        public double distanceInterval;
    }

    [Serializable]
    public class SubmitGameCenterLeaderBoardScoreResponse
    {
        public string statusCode;
    }
}
