// -----------------------------------------------------------------------
// APITestCatalog.cs - RuntimeAPITester / SDKAPIReflectionTests 공유 API 목록
// -----------------------------------------------------------------------
//
// SDK 3.0 커버리지 감사(2026-08)에서 RuntimeAPITester.cs와
// SDKAPIReflectionTests.cs에 동일한 39개 API 이름 목록이 두 곳에 중복
// 하드코딩되어 있던 것을 발견해 이 파일 하나로 단일화했다. API 이름을
// 추가/제거할 때는 이 배열만 수정하면 두 테스트가 함께 갱신된다.
//
// - RuntimeAPITester.TestAllSDKAPIs()는 각 이름에 대응하는 실제 호출
//   델리게이트(파라미터 더미값 포함)를 직접 구현하고, 마지막에
//   VerifyCatalogConsistency()로 이 목록과 실제 호출 목록 사이의 드리프트를
//   검출해 "API_Catalog_Consistency" 결과 항목으로 실패시킨다.
// - SDKAPIReflectionTests.AIT_API_Exists(string)는 이 배열을
//   [TestCaseSource]로 그대로 소비해 AIT 타입에 각 메서드가 존재하는지
//   검증한다.
//
// sdk_version_override 호환성 매트릭스(구 web-framework로 Runtime/SDK 재생성)를
// 지원해야 하므로, 이 배열의 버전 종속 항목은 RuntimeAPITester.TestAllSDKAPIs()의
// 호출부와 정확히 동일한 #if 가드(AIT_SDK_1_7_1_OR_LATER / AIT_SDK_3_0_OR_LATER)로
// 감싼다. 두 파일의 전처리 조건이 어긋나면 VerifyCatalogConsistency가 오탐 드리프트를
// 낸다.
//
// OnVisibilityChangedByTransparentServiceWeb은 web-framework 3.0.0에서
// 제거된 API라 이 목록에 포함하지 않는다 — 리플렉션 존재 여부에 따라
// 양쪽 테스트에서 조건부로만 다뤄진다(기존 동작 유지).
// -----------------------------------------------------------------------

namespace AppsInToss
{
    /// <summary>
    /// RuntimeAPITester(런타임 자동 호출)와 SDKAPIReflectionTests(EditMode 존재성 검증)가
    /// 함께 소비하는 SDK API 이름 목록의 단일 소스.
    /// </summary>
    public static class APITestCatalog
    {
        /// <summary>
        /// 이 하네스가 검증하는 모든 SDK API 이름. (3.0 풀빌드 기준 51개: 기존 39개 +
        /// SDK 3.0 신규 표면 12개. 버전 종속 항목은 #if로 감싸져 있어 sdk_version_override
        /// 매트릭스에서는 해당 버전에 실제로 존재하는 만큼만 포함된다.)
        /// </summary>
        public static readonly string[] AllAPINames = new string[]
        {
            // =====================================================================
            // 파라미터 없는 API들 (14개)
            // =====================================================================
            "GetDeviceId",
            "GetLocale",
            "GetNetworkStatus",
#if AIT_SDK_1_7_1_OR_LATER
            "GetOperationalEnvironment",
#endif
            "GetPlatformOS",
            "GetSchemeUri",
            "GetTossAppVersion",
            "AppLogin",
            "GetIsTossLoginIntegratedService",
            "GetClipboardText",
            "CloseView",
            "GetGameCenterGameProfile",
            "GetUserKeyForGame",
            "OpenGameCenterLeaderboard",

            // =====================================================================
            // 파라미터 있는 API들 (25개)
            // =====================================================================
            "SetClipboardText",
            "OpenURL",
            "GetTossShareLink",
            "Share",
            "FetchContacts",
            "EventLog",
            "GetPermission",
            "RequestPermission",
            "OpenPermissionDialog",
            "GetCurrentLocation",
            "GenerateHapticFeedback",
            "SetDeviceOrientation",
            "SetIosSwipeGestureEnabled",
#if AIT_SDK_1_7_1_OR_LATER
            "SetScreenAwakeMode",
            "SetSecureScreen",
#endif
            "CheckoutPayment",
            "FetchAlbumPhotos",
            "OpenCamera",
            "SaveBase64Data",
            "SubmitGameCenterLeaderBoardScore",
            "GrantPromotionRewardForGame",
            "GetGroupId",
            "AppsInTossSignTossCert",
            "StartUpdateLocation",
            "ContactsViral",

            // =====================================================================
            // SDK 3.0 신규 표면 (2026-08 감사로 편입, 12개)
            // 편입 기준: Task/Awaitable 반환 + 무인(unattended) 실행 안전.
            // Action(콜백 구독) 반환 API(SafeAreaInsetsSubscribe 등)는 이 하네스가
            // 다루지 않는다 — 다른 하네스(InteractiveAPITester 등)의 몫이다.
            // =====================================================================
#if AIT_SDK_3_0_OR_LATER
            "EnvGetDeploymentId",
            "GetAppsInTossGlobals",
            "IsMinVersionSupported",
            "GetServerTime",
            "StorageGetItem",
            "StorageSetItem",
            "StorageRemoveItem",
            "StorageClearItems",
            "PartnerAddAccessoryButton",
            "PartnerRemoveAccessoryButton",
            "FetchAlbumItems",
            "SafeAreaInsetsGet",
#endif
        };
    }
}
