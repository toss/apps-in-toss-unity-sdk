// -----------------------------------------------------------------------
// <copyright file="AITJsonCodec.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Generated JSON read/write code
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;

// [Obsolete] 타입·멤버도 와이어 형식은 그대로 읽고 쓴다.
#pragma warning disable 0612, 0618

namespace AppsInToss
{
    /// <summary>생성된 타입별 JSON 읽기 코드. 쓰기는 각 타입의 IAITJsonWritable 구현에 있다.</summary>
    internal static partial class AITJsonCodec
    {
        internal static object ReadBoxed(Type t, JsonValue j)
        {
            if (t == typeof(string)) return AITJson.Str(j);
            if (t == typeof(double)) return AITJson.Dbl(j);
            if (t == typeof(double?)) return AITJson.DblN(j);
            if (t == typeof(bool)) return AITJson.Bool(j);
            if (t == typeof(bool?)) return AITJson.BoolN(j);
            if (t == typeof(long)) return AITJson.Long(j);
            if (t == typeof(long?)) return AITJson.LongN(j);
            if (t == typeof(int)) return AITJson.Int(j);
            if (t == typeof(int?)) return AITJson.IntN(j);
            if (t == typeof(object)) return AITJson.Any(j);
            if (t == typeof(JsonValue)) return j;
            if (t == typeof(AITCallbackBagEvent)) return R_AITCallbackBagEvent(j);
            if (t == typeof(APIResponse)) return R_APIResponse(j);
            if (t == typeof(AITCore.VisibilityStateData)) return R_AITCore_VisibilityStateData(j);
            if (t == typeof(AITCore.CallbackData)) return R_AITCore_CallbackData(j);
            if (t == typeof(AITCore.NestedCallbackRequest)) return R_AITCore_NestedCallbackRequest(j);
            if (t == typeof(TdsNavigationAccessoryEventData)) return R_TdsNavigationAccessoryEventData(j);
            if (t == typeof(AppLoginResult)) return R_AppLoginResult(j);
            if (t == typeof(GetAnonymousKeyResult)) return R_GetAnonymousKeyResult(j);
            if (t == typeof(UserCreateAnonymousKeyAuthCodeResult)) return R_UserCreateAnonymousKeyAuthCodeResult(j);
            if (t == typeof(GetAnonymousKeySuccessResponse)) return R_GetAnonymousKeySuccessResponse(j);
            if (t == typeof(GetConsentedUserDataOptions)) return R_GetConsentedUserDataOptions(j);
            if (t == typeof(GetDeclaredAgeRangeParams)) return R_GetDeclaredAgeRangeParams(j);
            if (t == typeof(CheckoutPaymentParams)) return R_CheckoutPaymentParams(j);
            if (t == typeof(RequestTossPayPaysBillingParams)) return R_RequestTossPayPaysBillingParams(j);
            if (t == typeof(TossPayCheckoutPaymentOptions)) return R_TossPayCheckoutPaymentOptions(j);
            if (t == typeof(TossPayRequestTossPayPaysBillingOptions)) return R_TossPayRequestTossPayPaysBillingOptions(j);
            if (t == typeof(CheckoutPaymentOptions)) return R_CheckoutPaymentOptions(j);
            if (t == typeof(RequestTossPayPaysBillingOptions)) return R_RequestTossPayPaysBillingOptions(j);
            if (t == typeof(CheckoutPaymentResult)) return R_CheckoutPaymentResult(j);
            if (t == typeof(RequestTossPayPaysBillingResult)) return R_RequestTossPayPaysBillingResult(j);
            if (t == typeof(IapCreateOneTimePurchaseOrderOptions)) return R_IapCreateOneTimePurchaseOrderOptions(j);
            if (t == typeof(IapCreateOneTimePurchaseOrderOptionsOptions)) return R_IapCreateOneTimePurchaseOrderOptionsOptions(j);
            if (t == typeof(IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam)) return R_IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam(j);
            if (t == typeof(SuccessEvent)) return R_SuccessEvent(j);
            if (t == typeof(CreateSubscriptionPurchaseOrderOptions)) return R_CreateSubscriptionPurchaseOrderOptions(j);
            if (t == typeof(CreateSubscriptionPurchaseOrderOptionsOptions)) return R_CreateSubscriptionPurchaseOrderOptionsOptions(j);
            if (t == typeof(CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam)) return R_CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam(j);
            if (t == typeof(SubscriptionSuccessEvent)) return R_SubscriptionSuccessEvent(j);
            if (t == typeof(IAPGetCompletedOrRefundedOrdersParams)) return R_IAPGetCompletedOrRefundedOrdersParams(j);
            if (t == typeof(IAPCompleteProductGrantArgs_0)) return R_IAPCompleteProductGrantArgs_0(j);
            if (t == typeof(IAPCompleteProductGrantArgs_0Params)) return R_IAPCompleteProductGrantArgs_0Params(j);
            if (t == typeof(IAPGetSubscriptionInfoArgs_0)) return R_IAPGetSubscriptionInfoArgs_0(j);
            if (t == typeof(IAPGetSubscriptionInfoArgs_0Params)) return R_IAPGetSubscriptionInfoArgs_0Params(j);
            if (t == typeof(IAPGetPendingOrdersResult)) return R_IAPGetPendingOrdersResult(j);
            if (t == typeof(IAPGetProductItemListResult)) return R_IAPGetProductItemListResult(j);
            if (t == typeof(IAPGetPendingOrdersResultOrder)) return R_IAPGetPendingOrdersResultOrder(j);
            if (t == typeof(GetCurrentLocationOptions)) return R_GetCurrentLocationOptions(j);
            if (t == typeof(StartUpdateLocationEventParams)) return R_StartUpdateLocationEventParams(j);
            if (t == typeof(StartUpdateLocationOptions)) return R_StartUpdateLocationOptions(j);
            if (t == typeof(GetPermissionPermission)) return R_GetPermissionPermission(j);
            if (t == typeof(OpenPermissionDialogPermission)) return R_OpenPermissionDialogPermission(j);
            if (t == typeof(RequestPermissionPermission)) return R_RequestPermissionPermission(j);
            if (t == typeof(GetUserKeyForGameResult)) return R_GetUserKeyForGameResult(j);
            if (t == typeof(GameSetLeaderboardScoreParams)) return R_GameSetLeaderboardScoreParams(j);
            if (t == typeof(GrantPromotionRewardResult)) return R_GrantPromotionRewardResult(j);
            if (t == typeof(SubmitGameCenterLeaderBoardScoreParams)) return R_SubmitGameCenterLeaderBoardScoreParams(j);
            if (t == typeof(GetUserKeyForGameResponse)) return R_GetUserKeyForGameResponse(j);
            if (t == typeof(SubmitGameCenterLeaderBoardScoreResponse)) return R_SubmitGameCenterLeaderBoardScoreResponse(j);
            if (t == typeof(GrantPromotionRewardForGameParams)) return R_GrantPromotionRewardForGameParams(j);
            if (t == typeof(GrantPromotionRewardForGameParamsParams)) return R_GrantPromotionRewardForGameParamsParams(j);
            if (t == typeof(GrantPromotionRewardForGameSuccessResponse)) return R_GrantPromotionRewardForGameSuccessResponse(j);
            if (t == typeof(GrantPromotionRewardForGameErrorResponse)) return R_GrantPromotionRewardForGameErrorResponse(j);
            if (t == typeof(GrantPromotionRewardForGameErrorResult)) return R_GrantPromotionRewardForGameErrorResult(j);
            if (t == typeof(GrantPromotionRewardForGameResponse)) return R_GrantPromotionRewardForGameResponse(j);
            if (t == typeof(ShareSendMessageMessage)) return R_ShareSendMessageMessage(j);
            if (t == typeof(ViralGetCampaignOptions)) return R_ViralGetCampaignOptions(j);
            if (t == typeof(ViralGetCampaignResult)) return R_ViralGetCampaignResult(j);
            if (t == typeof(ViralGetCampaignResultDisplay)) return R_ViralGetCampaignResultDisplay(j);
            if (t == typeof(ViralGetCampaignResultRule)) return R_ViralGetCampaignResultRule(j);
            if (t == typeof(ViralShareOptions)) return R_ViralShareOptions(j);
            if (t == typeof(TrackViralParams)) return R_TrackViralParams(j);
            if (t == typeof(ShareMessage)) return R_ShareMessage(j);
            if (t == typeof(ContactsViralEventData)) return R_ContactsViralEventData(j);
            if (t == typeof(ContactsViralEvent)) return R_ContactsViralEvent(j);
            if (t == typeof(ContactsViralSuccessEventData)) return R_ContactsViralSuccessEventData(j);
            if (t == typeof(ContactsViralParamsOptions)) return R_ContactsViralParamsOptions(j);
            if (t == typeof(ContactsViralParams)) return R_ContactsViralParams(j);
            if (t == typeof(FetchAlbumPhotosOptions)) return R_FetchAlbumPhotosOptions(j);
            if (t == typeof(OpenCameraOptions)) return R_OpenCameraOptions(j);
            if (t == typeof(FetchAlbumItemsOptions)) return R_FetchAlbumItemsOptions(j);
            if (t == typeof(OpenPDFViewerParams)) return R_OpenPDFViewerParams(j);
            if (t == typeof(SaveBase64DataParams)) return R_SaveBase64DataParams(j);
            if (t == typeof(DeviceGetPhotosOptions)) return R_DeviceGetPhotosOptions(j);
            if (t == typeof(FetchContactsOptions)) return R_FetchContactsOptions(j);
            if (t == typeof(FetchContactsOptionsQuery)) return R_FetchContactsOptionsQuery(j);
            if (t == typeof(ContactResult)) return R_ContactResult(j);
            if (t == typeof(DeviceOpenCameraOptions)) return R_DeviceOpenCameraOptions(j);
            if (t == typeof(DeviceGetAlbumItemsOptions)) return R_DeviceGetAlbumItemsOptions(j);
            if (t == typeof(ScreenSetAwakeModeResult)) return R_ScreenSetAwakeModeResult(j);
            if (t == typeof(ScreenSetSecureResult)) return R_ScreenSetSecureResult(j);
            if (t == typeof(SetScreenAwakeModeResult)) return R_SetScreenAwakeModeResult(j);
            if (t == typeof(SetSecureScreenResult)) return R_SetSecureScreenResult(j);
            if (t == typeof(SetDeviceOrientationOptions)) return R_SetDeviceOrientationOptions(j);
            if (t == typeof(SetIosSwipeGestureEnabledOptions)) return R_SetIosSwipeGestureEnabledOptions(j);
            if (t == typeof(SetScreenAwakeModeOptions)) return R_SetScreenAwakeModeOptions(j);
            if (t == typeof(SetSecureScreenOptions)) return R_SetSecureScreenOptions(j);
            if (t == typeof(EventLogParams)) return R_EventLogParams(j);
            if (t == typeof(AppsInTossSignTossCertParams)) return R_AppsInTossSignTossCertParams(j);
            if (t == typeof(OnVisibilityChangedByTransparentServiceWebEventParams)) return R_OnVisibilityChangedByTransparentServiceWebEventParams(j);
            if (t == typeof(OnVisibilityChangedByTransparentServiceWebEventParamsOptions)) return R_OnVisibilityChangedByTransparentServiceWebEventParamsOptions(j);
            if (t == typeof(GoogleAdMobLoadAppsInTossAdMobArgs)) return R_GoogleAdMobLoadAppsInTossAdMobArgs(j);
            if (t == typeof(LoadAdMobOptions)) return R_LoadAdMobOptions(j);
            if (t == typeof(Error)) return R_Error(j);
            if (t == typeof(GoogleAdMobShowAppsInTossAdMobArgs)) return R_GoogleAdMobShowAppsInTossAdMobArgs(j);
            if (t == typeof(ShowAdMobOptions)) return R_ShowAdMobOptions(j);
            if (t == typeof(IsAdMobLoadedOptions)) return R_IsAdMobLoadedOptions(j);
            if (t == typeof(InitializeOptions)) return R_InitializeOptions(j);
            if (t == typeof(InitializeOptionsCallbacks)) return R_InitializeOptionsCallbacks(j);
            if (t == typeof(TossAdsAttachOptions)) return R_TossAdsAttachOptions(j);
            if (t == typeof(BannerSlotCallbacks)) return R_BannerSlotCallbacks(j);
            if (t == typeof(BannerSlotEventPayload)) return R_BannerSlotEventPayload(j);
            if (t == typeof(BannerSlotEventPayloadAdMetadata)) return R_BannerSlotEventPayloadAdMetadata(j);
            if (t == typeof(BannerSlotErrorPayload)) return R_BannerSlotErrorPayload(j);
            if (t == typeof(BannerSlotErrorPayloadError)) return R_BannerSlotErrorPayloadError(j);
            if (t == typeof(BannerSlotCallbacksOnNoFillParam)) return R_BannerSlotCallbacksOnNoFillParam(j);
            if (t == typeof(TossAdsAttachBannerOptions)) return R_TossAdsAttachBannerOptions(j);
            if (t == typeof(AttachBannerResult)) return R_AttachBannerResult(j);
            if (t == typeof(ShowFullScreenAdEvent)) return R_ShowFullScreenAdEvent(j);
            if (t == typeof(ShowFullScreenAdEventData)) return R_ShowFullScreenAdEventData(j);
            if (t == typeof(LoadFullScreenAdEvent)) return R_LoadFullScreenAdEvent(j);
            if (t == typeof(LoadFullScreenAdOptions)) return R_LoadFullScreenAdOptions(j);
            if (t == typeof(ShowFullScreenAdOptions)) return R_ShowFullScreenAdOptions(j);
            if (t == typeof(SafeAreaInsets)) return R_SafeAreaInsets(j);
            if (t == typeof(IsMinVersionSupportedMinVersions)) return R_IsMinVersionSupportedMinVersions(j);
            if (t == typeof(RewardFromContactsViralEvent)) return R_RewardFromContactsViralEvent(j);
            if (t == typeof(ContactsViralSuccessEvent)) return R_ContactsViralSuccessEvent(j);
            if (t == typeof(GrantPromotionRewardErrorResponse)) return R_GrantPromotionRewardErrorResponse(j);
            if (t == typeof(GrantPromotionRewardErrorResult)) return R_GrantPromotionRewardErrorResult(j);
            if (t == typeof(GrantPromotionRewardParams)) return R_GrantPromotionRewardParams(j);
            if (t == typeof(GrantPromotionRewardParamsParams)) return R_GrantPromotionRewardParamsParams(j);
            if (t == typeof(GrantPromotionRewardResponse)) return R_GrantPromotionRewardResponse(j);
            if (t == typeof(RequestNotificationAgreementOptions)) return R_RequestNotificationAgreementOptions(j);
            if (t == typeof(RequestNotificationAgreementOptionsOnEventParam)) return R_RequestNotificationAgreementOptionsOnEventParam(j);
            if (t == typeof(RequestNotificationAgreementOptionsOptions)) return R_RequestNotificationAgreementOptionsOptions(j);
            if (t == typeof(MigrationGetOriginStorageResult)) return R_MigrationGetOriginStorageResult(j);
            if (t == typeof(AdMobLoadResult)) return R_AdMobLoadResult(j);
            if (t == typeof(ResponseInfo)) return R_ResponseInfo(j);
            if (t == typeof(AdNetworkResponseInfo)) return R_AdNetworkResponseInfo(j);
            if (t == typeof(ShowAdMobEvent)) return R_ShowAdMobEvent(j);
            if (t == typeof(ShowAdMobEventData)) return R_ShowAdMobEventData(j);
            if (t == typeof(Offer)) return R_Offer(j);
            if (t == typeof(IapProductListItem)) return R_IapProductListItem(j);
            if (t == typeof(GameCenterGameProfileResponse)) return R_GameCenterGameProfileResponse(j);
            if (t == typeof(AdUserEarnedReward)) return R_AdUserEarnedReward(j);
            if (t == typeof(AdUserEarnedRewardData)) return R_AdUserEarnedRewardData(j);
            if (t == typeof(AddAccessoryButtonOptions)) return R_AddAccessoryButtonOptions(j);
            if (t == typeof(AddAccessoryButtonOptionsIcon)) return R_AddAccessoryButtonOptionsIcon(j);
            if (t == typeof(AlbumItemResponse)) return R_AlbumItemResponse(j);
            if (t == typeof(AppsInTossGlobals)) return R_AppsInTossGlobals(j);
            if (t == typeof(CompletedOrRefundedOrdersResultOrder)) return R_CompletedOrRefundedOrdersResultOrder(j);
            if (t == typeof(CompletedOrRefundedOrdersResult)) return R_CompletedOrRefundedOrdersResult(j);
            if (t == typeof(ContactEntity)) return R_ContactEntity(j);
            if (t == typeof(RewardFromContactsViralEventData)) return R_RewardFromContactsViralEventData(j);
            if (t == typeof(CreateLinkParams)) return R_CreateLinkParams(j);
            if (t == typeof(DeclaredAgeRange)) return R_DeclaredAgeRange(j);
            if (t == typeof(GrantRewardParams)) return R_GrantRewardParams(j);
            if (t == typeof(HapticFeedbackOptions)) return R_HapticFeedbackOptions(j);
            if (t == typeof(IapCreateOneTimePurchaseOrderResult)) return R_IapCreateOneTimePurchaseOrderResult(j);
            if (t == typeof(IapSubscriptionInfoResponse)) return R_IapSubscriptionInfoResponse(j);
            if (t == typeof(IapSubscriptionInfoResult)) return R_IapSubscriptionInfoResult(j);
            if (t == typeof(ImageResponse)) return R_ImageResponse(j);
            if (t == typeof(LoadAdMobEvent)) return R_LoadAdMobEvent(j);
            if (t == typeof(Location)) return R_Location(j);
            if (t == typeof(LocationCoords)) return R_LocationCoords(j);
            if (t == typeof(OriginStorageDump)) return R_OriginStorageDump(j);
            if (t == typeof(OriginStorageDumpIndexedDBObjectStoreIndex)) return R_OriginStorageDumpIndexedDBObjectStoreIndex(j);
            if (t == typeof(OriginStorageDumpIndexedDBObjectStoreRecord)) return R_OriginStorageDumpIndexedDBObjectStoreRecord(j);
            if (t == typeof(OriginStorageDumpIndexedDBObjectStore)) return R_OriginStorageDumpIndexedDBObjectStore(j);
            if (t == typeof(OriginStorageDumpIndexedDB)) return R_OriginStorageDumpIndexedDB(j);
            if (t == typeof(OriginStorageDumpOpfsFile)) return R_OriginStorageDumpOpfsFile(j);
            if (t == typeof(OriginStorageDumpOpfs)) return R_OriginStorageDumpOpfs(j);
            if (t == typeof(OriginStorageDumpError)) return R_OriginStorageDumpError(j);
            if (t == typeof(SetNavigationBarOptions)) return R_SetNavigationBarOptions(j);
            if (t == typeof(SafeAreaInsetsSubscribe__0)) return R_SafeAreaInsetsSubscribe__0(j);
            if (t == typeof(number)) return R_number(j);
            if (t == typeof(AppLoginResultReferrer) || t == typeof(AppLoginResultReferrer?)) return RE_AppLoginResultReferrer(j);
            if (t == typeof(GetConsentedUserDataErrorCode) || t == typeof(GetConsentedUserDataErrorCode?)) return RE_GetConsentedUserDataErrorCode(j);
            if (t == typeof(SubmitGameCenterLeaderBoardScoreResponseStatusCode) || t == typeof(SubmitGameCenterLeaderBoardScoreResponseStatusCode?)) return RE_SubmitGameCenterLeaderBoardScoreResponseStatusCode(j);
            if (t == typeof(ContactsViralEventDataCloseReason) || t == typeof(ContactsViralEventDataCloseReason?)) return RE_ContactsViralEventDataCloseReason(j);
            if (t == typeof(ContactsViralSuccessEventDataCloseReason) || t == typeof(ContactsViralSuccessEventDataCloseReason?)) return RE_ContactsViralSuccessEventDataCloseReason(j);
            if (t == typeof(ViralGetCampaignResultCampaignStatus) || t == typeof(ViralGetCampaignResultCampaignStatus?)) return RE_ViralGetCampaignResultCampaignStatus(j);
            if (t == typeof(ViralGetCampaignResultParticipationUnavailableReason) || t == typeof(ViralGetCampaignResultParticipationUnavailableReason?)) return RE_ViralGetCampaignResultParticipationUnavailableReason(j);
            if (t == typeof(ViralGetCampaignResultRuleStatus) || t == typeof(ViralGetCampaignResultRuleStatus?)) return RE_ViralGetCampaignResultRuleStatus(j);
            if (t == typeof(SetDeviceOrientationOptionsType) || t == typeof(SetDeviceOrientationOptionsType?)) return RE_SetDeviceOrientationOptionsType(j);
            if (t == typeof(EventLogParamsLog_type) || t == typeof(EventLogParamsLog_type?)) return RE_EventLogParamsLog_type(j);
            if (t == typeof(TossAdsAttachBannerOptionsTheme) || t == typeof(TossAdsAttachBannerOptionsTheme?)) return RE_TossAdsAttachBannerOptionsTheme(j);
            if (t == typeof(TossAdsAttachBannerOptionsTone) || t == typeof(TossAdsAttachBannerOptionsTone?)) return RE_TossAdsAttachBannerOptionsTone(j);
            if (t == typeof(TossAdsAttachBannerOptionsVariant) || t == typeof(TossAdsAttachBannerOptionsVariant?)) return RE_TossAdsAttachBannerOptionsVariant(j);
            if (t == typeof(RequestNotificationAgreementOptionsOnEventParamType) || t == typeof(RequestNotificationAgreementOptionsOnEventParamType?)) return RE_RequestNotificationAgreementOptionsOnEventParamType(j);
            if (t == typeof(IapProductListItemRenewalCycle) || t == typeof(IapProductListItemRenewalCycle?)) return RE_IapProductListItemRenewalCycle(j);
            if (t == typeof(CompletedOrRefundedOrdersResultOrderStatus) || t == typeof(CompletedOrRefundedOrdersResultOrderStatus?)) return RE_CompletedOrRefundedOrdersResultOrderStatus(j);
            if (t == typeof(DeclaredAgeRangeStatus) || t == typeof(DeclaredAgeRangeStatus?)) return RE_DeclaredAgeRangeStatus(j);
            if (t == typeof(LocationAccessLocation) || t == typeof(LocationAccessLocation?)) return RE_LocationAccessLocation(j);
            if (t == typeof(Accuracy) || t == typeof(Accuracy?)) return RE_Accuracy(j);
            if (t == typeof(AlbumItemType) || t == typeof(AlbumItemType?)) return RE_AlbumItemType(j);
            if (t == typeof(PermissionName) || t == typeof(PermissionName?)) return RE_PermissionName(j);
            if (t == typeof(PermissionAccess) || t == typeof(PermissionAccess?)) return RE_PermissionAccess(j);
            if (t == typeof(PermissionStatus) || t == typeof(PermissionStatus?)) return RE_PermissionStatus(j);
            if (t == typeof(PermissionDialogResult) || t == typeof(PermissionDialogResult?)) return RE_PermissionDialogResult(j);
            if (t == typeof(HapticFeedbackType) || t == typeof(HapticFeedbackType?)) return RE_HapticFeedbackType(j);
            if (t == typeof(NetworkStatus) || t == typeof(NetworkStatus?)) return RE_NetworkStatus(j);
            if (t == typeof(NavigationBarTheme) || t == typeof(NavigationBarTheme?)) return RE_NavigationBarTheme(j);
            if (t == typeof(ConsentedUserDataKey) || t == typeof(ConsentedUserDataKey?)) return RE_ConsentedUserDataKey(j);
            if (t.IsArray && t.GetArrayRank() == 1)
            {
                if (j == null || j.Kind != JsonValueKind.Array) return null;
                var elementType = t.GetElementType();
                var array = Array.CreateInstance(elementType, j.Count);
                for (int i = 0; i < j.Count; i++) array.SetValue(ReadBoxed(elementType, j._items[i]), i);
                return array;
            }
            throw new NotSupportedException("JSON: " + t.FullName + " is not an Apps in Toss SDK type");
        }

        internal static object ReadCallbackArg(Type t, JsonValue j)
        {
            if (t == typeof(string)) return AITJson.Str(j);
            if (t == typeof(double)) return AITJson.Dbl(j);
            if (t == typeof(double?)) return AITJson.DblN(j);
            if (t == typeof(bool)) return AITJson.Bool(j);
            if (t == typeof(bool?)) return AITJson.BoolN(j);
            if (t == typeof(long)) return AITJson.Long(j);
            if (t == typeof(long?)) return AITJson.LongN(j);
            if (t == typeof(int)) return AITJson.Int(j);
            if (t == typeof(int?)) return AITJson.IntN(j);
            if (t == typeof(object)) return AITJson.Any(j);
            if (t == typeof(JsonValue)) return j;
            if (t == typeof(IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam)) return R_IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam(j);
            if (t == typeof(SuccessEvent)) return R_SuccessEvent(j);
            if (t == typeof(CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam)) return R_CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam(j);
            if (t == typeof(SubscriptionSuccessEvent)) return R_SubscriptionSuccessEvent(j);
            if (t == typeof(ContactsViralEvent)) return R_ContactsViralEvent(j);
            if (t == typeof(Error)) return R_Error(j);
            if (t == typeof(BannerSlotEventPayload)) return R_BannerSlotEventPayload(j);
            if (t == typeof(BannerSlotErrorPayload)) return R_BannerSlotErrorPayload(j);
            if (t == typeof(BannerSlotCallbacksOnNoFillParam)) return R_BannerSlotCallbacksOnNoFillParam(j);
            if (t == typeof(SafeAreaInsets)) return R_SafeAreaInsets(j);
            if (t == typeof(RequestNotificationAgreementOptionsOnEventParam)) return R_RequestNotificationAgreementOptionsOnEventParam(j);
            if (t == typeof(ShowAdMobEvent)) return R_ShowAdMobEvent(j);
            if (t == typeof(LoadAdMobEvent)) return R_LoadAdMobEvent(j);
            if (t == typeof(Location)) return R_Location(j);
            throw new NotSupportedException("JSON: " + t.FullName + " is not an Apps in Toss SDK type");
        }

        private static readonly string[] EN_AppLoginResultReferrer = { "DEFAULT", "SANDBOX" };
        internal static AppLoginResultReferrer? RE_AppLoginResultReferrer(JsonValue j) => AITJson.Enum(j, EN_AppLoginResultReferrer, EN_AppLoginResultReferrer, null, out var v) ? (AppLoginResultReferrer)v : (AppLoginResultReferrer?)null;
        internal static void WE_AppLoginResultReferrer(AITJsonWriter w, AppLoginResultReferrer? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_AppLoginResultReferrer, null); else w.Null(); }

        private static readonly string[] EN_GetConsentedUserDataErrorCode = { "USER_DECLINED", "UNAVAILABLE", "TERMS_NOT_SET", "INVALID_REQUEST", "CANCELED", "CONSENTED_USER_DATA_AGREEMENT_FAILED", "CONSENTED_USER_DATA_INVALID_DATA" };
        internal static GetConsentedUserDataErrorCode? RE_GetConsentedUserDataErrorCode(JsonValue j) => AITJson.Enum(j, EN_GetConsentedUserDataErrorCode, EN_GetConsentedUserDataErrorCode, null, out var v) ? (GetConsentedUserDataErrorCode)v : (GetConsentedUserDataErrorCode?)null;
        internal static void WE_GetConsentedUserDataErrorCode(AITJsonWriter w, GetConsentedUserDataErrorCode? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_GetConsentedUserDataErrorCode, null); else w.Null(); }

        private static readonly string[] EN_SubmitGameCenterLeaderBoardScoreResponseStatusCode = { "PROFILE_NOT_FOUND", "SUCCESS", "LEADERBOARD_NOT_FOUND", "UNPARSABLE_SCORE" };
        internal static SubmitGameCenterLeaderBoardScoreResponseStatusCode? RE_SubmitGameCenterLeaderBoardScoreResponseStatusCode(JsonValue j) => AITJson.Enum(j, EN_SubmitGameCenterLeaderBoardScoreResponseStatusCode, EN_SubmitGameCenterLeaderBoardScoreResponseStatusCode, null, out var v) ? (SubmitGameCenterLeaderBoardScoreResponseStatusCode)v : (SubmitGameCenterLeaderBoardScoreResponseStatusCode?)null;
        internal static void WE_SubmitGameCenterLeaderBoardScoreResponseStatusCode(AITJsonWriter w, SubmitGameCenterLeaderBoardScoreResponseStatusCode? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_SubmitGameCenterLeaderBoardScoreResponseStatusCode, null); else w.Null(); }

        private static readonly string[] EN_ContactsViralEventDataCloseReason = { "ClickBackButton", "NoReward" };
        private static readonly string[] EW_ContactsViralEventDataCloseReason = { "clickBackButton", "noReward" };
        internal static ContactsViralEventDataCloseReason? RE_ContactsViralEventDataCloseReason(JsonValue j) => AITJson.Enum(j, EW_ContactsViralEventDataCloseReason, EN_ContactsViralEventDataCloseReason, null, out var v) ? (ContactsViralEventDataCloseReason)v : (ContactsViralEventDataCloseReason?)null;
        internal static void WE_ContactsViralEventDataCloseReason(AITJsonWriter w, ContactsViralEventDataCloseReason? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_ContactsViralEventDataCloseReason, null); else w.Null(); }

        private static readonly string[] EN_ContactsViralSuccessEventDataCloseReason = { "ClickBackButton", "NoReward" };
        private static readonly string[] EW_ContactsViralSuccessEventDataCloseReason = { "clickBackButton", "noReward" };
        internal static ContactsViralSuccessEventDataCloseReason? RE_ContactsViralSuccessEventDataCloseReason(JsonValue j) => AITJson.Enum(j, EW_ContactsViralSuccessEventDataCloseReason, EN_ContactsViralSuccessEventDataCloseReason, null, out var v) ? (ContactsViralSuccessEventDataCloseReason)v : (ContactsViralSuccessEventDataCloseReason?)null;
        internal static void WE_ContactsViralSuccessEventDataCloseReason(AITJsonWriter w, ContactsViralSuccessEventDataCloseReason? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_ContactsViralSuccessEventDataCloseReason, null); else w.Null(); }

        private static readonly string[] EN_ViralGetCampaignResultCampaignStatus = { "WAITING", "IN_PROGRESS", "ENDED" };
        internal static ViralGetCampaignResultCampaignStatus? RE_ViralGetCampaignResultCampaignStatus(JsonValue j) => AITJson.Enum(j, EN_ViralGetCampaignResultCampaignStatus, EN_ViralGetCampaignResultCampaignStatus, null, out var v) ? (ViralGetCampaignResultCampaignStatus)v : (ViralGetCampaignResultCampaignStatus?)null;
        internal static void WE_ViralGetCampaignResultCampaignStatus(AITJsonWriter w, ViralGetCampaignResultCampaignStatus? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_ViralGetCampaignResultCampaignStatus, null); else w.Null(); }

        private static readonly string[] EN_ViralGetCampaignResultParticipationUnavailableReason = { "ENDED", "INACTIVE", "NOT_STARTED", "ALL_RULES_ACHIEVED" };
        internal static ViralGetCampaignResultParticipationUnavailableReason? RE_ViralGetCampaignResultParticipationUnavailableReason(JsonValue j) => AITJson.Enum(j, EN_ViralGetCampaignResultParticipationUnavailableReason, EN_ViralGetCampaignResultParticipationUnavailableReason, null, out var v) ? (ViralGetCampaignResultParticipationUnavailableReason)v : (ViralGetCampaignResultParticipationUnavailableReason?)null;
        internal static void WE_ViralGetCampaignResultParticipationUnavailableReason(AITJsonWriter w, ViralGetCampaignResultParticipationUnavailableReason? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_ViralGetCampaignResultParticipationUnavailableReason, null); else w.Null(); }

        private static readonly string[] EN_ViralGetCampaignResultRuleStatus = { "EXPIRED", "IN_PROGRESS", "ACHIEVED" };
        internal static ViralGetCampaignResultRuleStatus? RE_ViralGetCampaignResultRuleStatus(JsonValue j) => AITJson.Enum(j, EN_ViralGetCampaignResultRuleStatus, EN_ViralGetCampaignResultRuleStatus, null, out var v) ? (ViralGetCampaignResultRuleStatus)v : (ViralGetCampaignResultRuleStatus?)null;
        internal static void WE_ViralGetCampaignResultRuleStatus(AITJsonWriter w, ViralGetCampaignResultRuleStatus? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_ViralGetCampaignResultRuleStatus, null); else w.Null(); }

        private static readonly string[] EN_SetDeviceOrientationOptionsType = { "Portrait", "Landscape" };
        private static readonly string[] EW_SetDeviceOrientationOptionsType = { "portrait", "landscape" };
        internal static SetDeviceOrientationOptionsType? RE_SetDeviceOrientationOptionsType(JsonValue j) => AITJson.Enum(j, EW_SetDeviceOrientationOptionsType, EN_SetDeviceOrientationOptionsType, null, out var v) ? (SetDeviceOrientationOptionsType)v : (SetDeviceOrientationOptionsType?)null;
        internal static void WE_SetDeviceOrientationOptionsType(AITJsonWriter w, SetDeviceOrientationOptionsType? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_SetDeviceOrientationOptionsType, null); else w.Null(); }

        private static readonly string[] EN_EventLogParamsLog_type = { "Impression", "Error", "Debug", "Info", "Warn", "Event", "Screen", "Click", "Popup" };
        private static readonly string[] EW_EventLogParamsLog_type = { "impression", "error", "debug", "info", "warn", "event", "screen", "click", "popup" };
        internal static EventLogParamsLog_type? RE_EventLogParamsLog_type(JsonValue j) => AITJson.Enum(j, EW_EventLogParamsLog_type, EN_EventLogParamsLog_type, null, out var v) ? (EventLogParamsLog_type)v : (EventLogParamsLog_type?)null;
        internal static void WE_EventLogParamsLog_type(AITJsonWriter w, EventLogParamsLog_type? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_EventLogParamsLog_type, null); else w.Null(); }

        private static readonly string[] EN_TossAdsAttachBannerOptionsTheme = { "Light", "Dark", "Auto" };
        private static readonly string[] EW_TossAdsAttachBannerOptionsTheme = { "light", "dark", "auto" };
        internal static TossAdsAttachBannerOptionsTheme? RE_TossAdsAttachBannerOptionsTheme(JsonValue j) => AITJson.Enum(j, EW_TossAdsAttachBannerOptionsTheme, EN_TossAdsAttachBannerOptionsTheme, null, out var v) ? (TossAdsAttachBannerOptionsTheme)v : (TossAdsAttachBannerOptionsTheme?)null;
        internal static void WE_TossAdsAttachBannerOptionsTheme(AITJsonWriter w, TossAdsAttachBannerOptionsTheme? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_TossAdsAttachBannerOptionsTheme, null); else w.Null(); }

        private static readonly string[] EN_TossAdsAttachBannerOptionsTone = { "BlackAndWhite", "Grey" };
        private static readonly string[] EW_TossAdsAttachBannerOptionsTone = { "blackAndWhite", "grey" };
        internal static TossAdsAttachBannerOptionsTone? RE_TossAdsAttachBannerOptionsTone(JsonValue j) => AITJson.Enum(j, EW_TossAdsAttachBannerOptionsTone, EN_TossAdsAttachBannerOptionsTone, null, out var v) ? (TossAdsAttachBannerOptionsTone)v : (TossAdsAttachBannerOptionsTone?)null;
        internal static void WE_TossAdsAttachBannerOptionsTone(AITJsonWriter w, TossAdsAttachBannerOptionsTone? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_TossAdsAttachBannerOptionsTone, null); else w.Null(); }

        private static readonly string[] EN_TossAdsAttachBannerOptionsVariant = { "Card", "Expanded" };
        private static readonly string[] EW_TossAdsAttachBannerOptionsVariant = { "card", "expanded" };
        internal static TossAdsAttachBannerOptionsVariant? RE_TossAdsAttachBannerOptionsVariant(JsonValue j) => AITJson.Enum(j, EW_TossAdsAttachBannerOptionsVariant, EN_TossAdsAttachBannerOptionsVariant, null, out var v) ? (TossAdsAttachBannerOptionsVariant)v : (TossAdsAttachBannerOptionsVariant?)null;
        internal static void WE_TossAdsAttachBannerOptionsVariant(AITJsonWriter w, TossAdsAttachBannerOptionsVariant? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_TossAdsAttachBannerOptionsVariant, null); else w.Null(); }

        private static readonly string[] EN_RequestNotificationAgreementOptionsOnEventParamType = { "NewAgreement", "AlreadyAgreed", "AgreementRejected" };
        private static readonly string[] EW_RequestNotificationAgreementOptionsOnEventParamType = { "newAgreement", "alreadyAgreed", "agreementRejected" };
        internal static RequestNotificationAgreementOptionsOnEventParamType? RE_RequestNotificationAgreementOptionsOnEventParamType(JsonValue j) => AITJson.Enum(j, EW_RequestNotificationAgreementOptionsOnEventParamType, EN_RequestNotificationAgreementOptionsOnEventParamType, null, out var v) ? (RequestNotificationAgreementOptionsOnEventParamType)v : (RequestNotificationAgreementOptionsOnEventParamType?)null;
        internal static void WE_RequestNotificationAgreementOptionsOnEventParamType(AITJsonWriter w, RequestNotificationAgreementOptionsOnEventParamType? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_RequestNotificationAgreementOptionsOnEventParamType, null); else w.Null(); }

        private static readonly string[] EN_IapProductListItemRenewalCycle = { "WEEKLY", "MONTHLY", "YEARLY" };
        internal static IapProductListItemRenewalCycle? RE_IapProductListItemRenewalCycle(JsonValue j) => AITJson.Enum(j, EN_IapProductListItemRenewalCycle, EN_IapProductListItemRenewalCycle, null, out var v) ? (IapProductListItemRenewalCycle)v : (IapProductListItemRenewalCycle?)null;
        internal static void WE_IapProductListItemRenewalCycle(AITJsonWriter w, IapProductListItemRenewalCycle? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_IapProductListItemRenewalCycle, null); else w.Null(); }

        private static readonly string[] EN_CompletedOrRefundedOrdersResultOrderStatus = { "COMPLETED", "REFUNDED" };
        internal static CompletedOrRefundedOrdersResultOrderStatus? RE_CompletedOrRefundedOrdersResultOrderStatus(JsonValue j) => AITJson.Enum(j, EN_CompletedOrRefundedOrdersResultOrderStatus, EN_CompletedOrRefundedOrdersResultOrderStatus, null, out var v) ? (CompletedOrRefundedOrdersResultOrderStatus)v : (CompletedOrRefundedOrdersResultOrderStatus?)null;
        internal static void WE_CompletedOrRefundedOrdersResultOrderStatus(AITJsonWriter w, CompletedOrRefundedOrdersResultOrderStatus? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_CompletedOrRefundedOrdersResultOrderStatus, null); else w.Null(); }

        private static readonly string[] EN_DeclaredAgeRangeStatus = { "INVALID_REQUEST", "NOT_AVAILABLE", "SHARING", "DECLINED_SHARING", "UNSUPPORTED_OS", "FAILED" };
        internal static DeclaredAgeRangeStatus? RE_DeclaredAgeRangeStatus(JsonValue j) => AITJson.Enum(j, EN_DeclaredAgeRangeStatus, EN_DeclaredAgeRangeStatus, null, out var v) ? (DeclaredAgeRangeStatus)v : (DeclaredAgeRangeStatus?)null;
        internal static void WE_DeclaredAgeRangeStatus(AITJsonWriter w, DeclaredAgeRangeStatus? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_DeclaredAgeRangeStatus, null); else w.Null(); }

        private static readonly string[] EN_LocationAccessLocation = { "FINE", "COARSE" };
        internal static LocationAccessLocation? RE_LocationAccessLocation(JsonValue j) => AITJson.Enum(j, EN_LocationAccessLocation, EN_LocationAccessLocation, null, out var v) ? (LocationAccessLocation)v : (LocationAccessLocation?)null;
        internal static void WE_LocationAccessLocation(AITJsonWriter w, LocationAccessLocation? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_LocationAccessLocation, null); else w.Null(); }

        private static readonly string[] EN_Accuracy = { "Lowest", "Low", "Balanced", "High", "Highest", "BestForNavigation" };
        private static readonly int[] EV_Accuracy = { 1, 2, 3, 4, 5, 6 };
        internal static Accuracy? RE_Accuracy(JsonValue j) => AITJson.Enum(j, EN_Accuracy, EN_Accuracy, EV_Accuracy, out var v) ? (Accuracy)v : (Accuracy?)null;
        internal static void WE_Accuracy(AITJsonWriter w, Accuracy? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, null, EV_Accuracy); else w.Null(); }

        private static readonly string[] EN_AlbumItemType = { "PHOTO", "VIDEO" };
        internal static AlbumItemType? RE_AlbumItemType(JsonValue j) => AITJson.Enum(j, EN_AlbumItemType, EN_AlbumItemType, null, out var v) ? (AlbumItemType)v : (AlbumItemType?)null;
        internal static void WE_AlbumItemType(AITJsonWriter w, AlbumItemType? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_AlbumItemType, null); else w.Null(); }

        private static readonly string[] EN_PermissionName = { "Clipboard", "Contacts", "Photos", "Geolocation", "Camera", "Microphone" };
        private static readonly string[] EW_PermissionName = { "clipboard", "contacts", "photos", "geolocation", "camera", "microphone" };
        internal static PermissionName? RE_PermissionName(JsonValue j) => AITJson.Enum(j, EW_PermissionName, EN_PermissionName, null, out var v) ? (PermissionName)v : (PermissionName?)null;
        internal static void WE_PermissionName(AITJsonWriter w, PermissionName? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_PermissionName, null); else w.Null(); }

        private static readonly string[] EN_PermissionAccess = { "Read", "Write", "Access" };
        private static readonly string[] EW_PermissionAccess = { "read", "write", "access" };
        internal static PermissionAccess? RE_PermissionAccess(JsonValue j) => AITJson.Enum(j, EW_PermissionAccess, EN_PermissionAccess, null, out var v) ? (PermissionAccess)v : (PermissionAccess?)null;
        internal static void WE_PermissionAccess(AITJsonWriter w, PermissionAccess? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_PermissionAccess, null); else w.Null(); }

        private static readonly string[] EN_PermissionStatus = { "NotDetermined", "Denied", "Allowed" };
        private static readonly string[] EW_PermissionStatus = { "notDetermined", "denied", "allowed" };
        internal static PermissionStatus? RE_PermissionStatus(JsonValue j) => AITJson.Enum(j, EW_PermissionStatus, EN_PermissionStatus, null, out var v) ? (PermissionStatus)v : (PermissionStatus?)null;
        internal static void WE_PermissionStatus(AITJsonWriter w, PermissionStatus? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_PermissionStatus, null); else w.Null(); }

        private static readonly string[] EN_PermissionDialogResult = { "Allowed", "Denied" };
        private static readonly string[] EW_PermissionDialogResult = { "allowed", "denied" };
        internal static PermissionDialogResult? RE_PermissionDialogResult(JsonValue j) => AITJson.Enum(j, EW_PermissionDialogResult, EN_PermissionDialogResult, null, out var v) ? (PermissionDialogResult)v : (PermissionDialogResult?)null;
        internal static void WE_PermissionDialogResult(AITJsonWriter w, PermissionDialogResult? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_PermissionDialogResult, null); else w.Null(); }

        private static readonly string[] EN_HapticFeedbackType = { "TickWeak", "Tap", "TickMedium", "SoftMedium", "BasicWeak", "BasicMedium", "Success", "Error", "Wiggle", "Confetti" };
        private static readonly string[] EW_HapticFeedbackType = { "tickWeak", "tap", "tickMedium", "softMedium", "basicWeak", "basicMedium", "success", "error", "wiggle", "confetti" };
        internal static HapticFeedbackType? RE_HapticFeedbackType(JsonValue j) => AITJson.Enum(j, EW_HapticFeedbackType, EN_HapticFeedbackType, null, out var v) ? (HapticFeedbackType)v : (HapticFeedbackType?)null;
        internal static void WE_HapticFeedbackType(AITJsonWriter w, HapticFeedbackType? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_HapticFeedbackType, null); else w.Null(); }

        private static readonly string[] EN_NetworkStatus = { "OFFLINE", "WIFI", "_2G", "_3G", "_4G", "_5G", "WWAN", "UNKNOWN" };
        private static readonly string[] EW_NetworkStatus = { "OFFLINE", "WIFI", "2G", "3G", "4G", "5G", "WWAN", "UNKNOWN" };
        internal static NetworkStatus? RE_NetworkStatus(JsonValue j) => AITJson.Enum(j, EW_NetworkStatus, EN_NetworkStatus, null, out var v) ? (NetworkStatus)v : (NetworkStatus?)null;
        internal static void WE_NetworkStatus(AITJsonWriter w, NetworkStatus? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_NetworkStatus, null); else w.Null(); }

        private static readonly string[] EN_NavigationBarTheme = { "Light", "Dark" };
        private static readonly string[] EW_NavigationBarTheme = { "light", "dark" };
        internal static NavigationBarTheme? RE_NavigationBarTheme(JsonValue j) => AITJson.Enum(j, EW_NavigationBarTheme, EN_NavigationBarTheme, null, out var v) ? (NavigationBarTheme)v : (NavigationBarTheme?)null;
        internal static void WE_NavigationBarTheme(AITJsonWriter w, NavigationBarTheme? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EW_NavigationBarTheme, null); else w.Null(); }

        private static readonly string[] EN_ConsentedUserDataKey = { "USER_NAME", "USER_GENDER", "USER_NATIONALITY", "USER_BIRTHDAY", "USER_PHONE", "USER_ADDRESS", "USER_EMAIL", "USER_CONSUMPTION_HISTORY", "USER_ADDRESS_BASE", "USER_ADDRESS_DETAIL" };
        internal static ConsentedUserDataKey? RE_ConsentedUserDataKey(JsonValue j) => AITJson.Enum(j, EN_ConsentedUserDataKey, EN_ConsentedUserDataKey, null, out var v) ? (ConsentedUserDataKey)v : (ConsentedUserDataKey?)null;
        internal static void WE_ConsentedUserDataKey(AITJsonWriter w, ConsentedUserDataKey? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, EN_ConsentedUserDataKey, null); else w.Null(); }
        internal static bool KE_ConsentedUserDataKey(string s, out ConsentedUserDataKey k) { bool ok = AITJson.EnumText(s, EN_ConsentedUserDataKey, EN_ConsentedUserDataKey, null, out var v); k = (ConsentedUserDataKey)v; return ok; }
        internal static string SE_ConsentedUserDataKey(ConsentedUserDataKey k) => AITJson.EnumWire((int)k, EN_ConsentedUserDataKey, null) ?? ((int)k).ToString(CultureInfo.InvariantCulture);

        private static readonly string[] N_AITCallbackBagEvent = { "event", "payload" };
        internal static AITCallbackBagEvent R_AITCallbackBagEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AITCallbackBagEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AITCallbackBagEvent))
                {
                    case 0: o.Event = AITJson.Str(v); break;
                    case 1: o.Payload = v; break;
                    default: break;
                }
            }
            return o;
        }

        private static readonly string[] N_APIResponse = { "success", "data", "error", "errorName" };
        internal static APIResponse R_APIResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new APIResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_APIResponse))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.success = AITJson.Bool(v); } break;
                    case 1: o.data = AITJson.Str(v); break;
                    case 2: o.error = AITJson.Str(v); break;
                    case 3: o.errorName = AITJson.Str(v); break;
                    default: break;
                }
            }
            return o;
        }

        private static readonly string[] N_AITCore_VisibilityStateData = { "isVisible" };
        internal static AITCore.VisibilityStateData R_AITCore_VisibilityStateData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AITCore.VisibilityStateData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AITCore_VisibilityStateData))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.isVisible = AITJson.Bool(v); } break;
                    default: break;
                }
            }
            return o;
        }

        private static readonly string[] N_AITCore_CallbackData = { "CallbackId", "TypeName", "Result" };
        internal static AITCore.CallbackData R_AITCore_CallbackData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AITCore.CallbackData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AITCore_CallbackData))
                {
                    case 0: o.CallbackId = AITJson.Str(v); break;
                    case 1: o.TypeName = AITJson.Str(v); break;
                    case 2: o.Result = AITJson.Str(v); break;
                    default: break;
                }
            }
            return o;
        }

        private static readonly string[] N_AITCore_NestedCallbackRequest = { "requestId", "callbackId", "callbackName", "data" };
        internal static AITCore.NestedCallbackRequest R_AITCore_NestedCallbackRequest(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AITCore.NestedCallbackRequest();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AITCore_NestedCallbackRequest))
                {
                    case 0: o.RequestId = AITJson.Str(v); break;
                    case 1: o.CallbackId = AITJson.Str(v); break;
                    case 2: o.CallbackName = AITJson.Str(v); break;
                    case 3: o.Data = AITJson.Str(v); break;
                    default: break;
                }
            }
            return o;
        }

        private static readonly string[] N_TdsNavigationAccessoryEventData = { "id" };
        internal static TdsNavigationAccessoryEventData R_TdsNavigationAccessoryEventData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new TdsNavigationAccessoryEventData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_TdsNavigationAccessoryEventData))
                {
                    case 0: o.Id = AITJson.Str(v); break;
                    default: break;
                }
            }
            return o;
        }

        private static readonly string[] N_AppLoginResult = { "authorizationCode", "referrer", "error" };
        internal static AppLoginResult R_AppLoginResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AppLoginResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AppLoginResult))
                {
                    case 0: o.AuthorizationCode = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.Referrer = AITJsonCodec.RE_AppLoginResultReferrer(v).GetValueOrDefault(); } break;
                    case 2: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetAnonymousKeyResult = { "_type", "_successJson", "_errorCode" };
        internal static GetAnonymousKeyResult R_GetAnonymousKeyResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetAnonymousKeyResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetAnonymousKeyResult))
                {
                    case 0: o._type = AITJson.Str(v); break;
                    case 1: o._successData = AITJsonCodec.R_GetAnonymousKeySuccessResponse(v); break;
                    case 2: o._errorCode = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_UserCreateAnonymousKeyAuthCodeResult = { "code", "expiresIn" };
        internal static UserCreateAnonymousKeyAuthCodeResult R_UserCreateAnonymousKeyAuthCodeResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new UserCreateAnonymousKeyAuthCodeResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_UserCreateAnonymousKeyAuthCodeResult))
                {
                    case 0: o.Code = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.ExpiresIn = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetAnonymousKeySuccessResponse = { "hash", "type" };
        internal static GetAnonymousKeySuccessResponse R_GetAnonymousKeySuccessResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetAnonymousKeySuccessResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetAnonymousKeySuccessResponse))
                {
                    case 0: o.Hash = AITJson.Str(v); break;
                    case 1: o.Type = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetConsentedUserDataOptions = { "consentedUserDataKey", "shouldRequestAgreementWhenUserDeclined" };
        internal static GetConsentedUserDataOptions R_GetConsentedUserDataOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetConsentedUserDataOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetConsentedUserDataOptions))
                {
                    case 0: o.ConsentedUserDataKey = AITJson.Str(v); break;
                    case 1: o.ShouldRequestAgreementWhenUserDeclined = AITJson.BoolN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetDeclaredAgeRangeParams = { "ageGates" };
        internal static GetDeclaredAgeRangeParams R_GetDeclaredAgeRangeParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetDeclaredAgeRangeParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetDeclaredAgeRangeParams))
                {
                    case 0: o.AgeGates = AITJson.Arr<double>(v, j1 => AITJson.Dbl(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CheckoutPaymentParams = { "payToken" };
        internal static CheckoutPaymentParams R_CheckoutPaymentParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CheckoutPaymentParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CheckoutPaymentParams))
                {
                    case 0: o.PayToken = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestTossPayPaysBillingParams = { "wrappedToken" };
        internal static RequestTossPayPaysBillingParams R_RequestTossPayPaysBillingParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestTossPayPaysBillingParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestTossPayPaysBillingParams))
                {
                    case 0: o.WrappedToken = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_TossPayCheckoutPaymentOptions = { "params" };
        internal static TossPayCheckoutPaymentOptions R_TossPayCheckoutPaymentOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new TossPayCheckoutPaymentOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_TossPayCheckoutPaymentOptions))
                {
                    case 0: o.Params = AITJsonCodec.R_CheckoutPaymentParams(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_TossPayRequestTossPayPaysBillingOptions = { "params" };
        internal static TossPayRequestTossPayPaysBillingOptions R_TossPayRequestTossPayPaysBillingOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new TossPayRequestTossPayPaysBillingOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_TossPayRequestTossPayPaysBillingOptions))
                {
                    case 0: o.Params = AITJsonCodec.R_RequestTossPayPaysBillingParams(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CheckoutPaymentOptions = { "params" };
        internal static CheckoutPaymentOptions R_CheckoutPaymentOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CheckoutPaymentOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CheckoutPaymentOptions))
                {
                    case 0: o.Params = AITJsonCodec.R_CheckoutPaymentParams(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestTossPayPaysBillingOptions = { "params" };
        internal static RequestTossPayPaysBillingOptions R_RequestTossPayPaysBillingOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestTossPayPaysBillingOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestTossPayPaysBillingOptions))
                {
                    case 0: o.Params = AITJsonCodec.R_RequestTossPayPaysBillingParams(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CheckoutPaymentResult = { "success", "reason" };
        internal static CheckoutPaymentResult R_CheckoutPaymentResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CheckoutPaymentResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CheckoutPaymentResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Success = AITJson.Bool(v); } break;
                    case 1: o.Reason = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestTossPayPaysBillingResult = { "success", "reason" };
        internal static RequestTossPayPaysBillingResult R_RequestTossPayPaysBillingResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestTossPayPaysBillingResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestTossPayPaysBillingResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Success = AITJson.Bool(v); } break;
                    case 1: o.Reason = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapCreateOneTimePurchaseOrderOptions = { "options" };
        internal static IapCreateOneTimePurchaseOrderOptions R_IapCreateOneTimePurchaseOrderOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapCreateOneTimePurchaseOrderOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapCreateOneTimePurchaseOrderOptions))
                {
                    case 0: o.Options = AITJsonCodec.R_IapCreateOneTimePurchaseOrderOptionsOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapCreateOneTimePurchaseOrderOptionsOptions = { "productId", "sku" };
        internal static IapCreateOneTimePurchaseOrderOptionsOptions R_IapCreateOneTimePurchaseOrderOptionsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapCreateOneTimePurchaseOrderOptionsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapCreateOneTimePurchaseOrderOptionsOptions))
                {
                    case 0: o.ProductId = AITJson.Str(v); break;
                    case 1: o.Sku = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam = { "orderId" };
        internal static IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam R_IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SuccessEvent = { "type", "data" };
        internal static SuccessEvent R_SuccessEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SuccessEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SuccessEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_IapCreateOneTimePurchaseOrderResult(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CreateSubscriptionPurchaseOrderOptions = { "options" };
        internal static CreateSubscriptionPurchaseOrderOptions R_CreateSubscriptionPurchaseOrderOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CreateSubscriptionPurchaseOrderOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CreateSubscriptionPurchaseOrderOptions))
                {
                    case 0: o.Options = AITJsonCodec.R_CreateSubscriptionPurchaseOrderOptionsOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CreateSubscriptionPurchaseOrderOptionsOptions = { "sku", "offerId" };
        internal static CreateSubscriptionPurchaseOrderOptionsOptions R_CreateSubscriptionPurchaseOrderOptionsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CreateSubscriptionPurchaseOrderOptionsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CreateSubscriptionPurchaseOrderOptionsOptions))
                {
                    case 0: o.Sku = AITJson.Str(v); break;
                    case 1: o.OfferId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam = { "orderId", "subscriptionId" };
        internal static CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam R_CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    case 1: o.SubscriptionId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SubscriptionSuccessEvent = { "type", "data" };
        internal static SubscriptionSuccessEvent R_SubscriptionSuccessEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SubscriptionSuccessEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SubscriptionSuccessEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_IapCreateOneTimePurchaseOrderResult(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPGetCompletedOrRefundedOrdersParams = { "key" };
        internal static IAPGetCompletedOrRefundedOrdersParams R_IAPGetCompletedOrRefundedOrdersParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPGetCompletedOrRefundedOrdersParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPGetCompletedOrRefundedOrdersParams))
                {
                    case 0: o.Key = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPCompleteProductGrantArgs_0 = { "params" };
        internal static IAPCompleteProductGrantArgs_0 R_IAPCompleteProductGrantArgs_0(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPCompleteProductGrantArgs_0();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPCompleteProductGrantArgs_0))
                {
                    case 0: o.Params = AITJsonCodec.R_IAPCompleteProductGrantArgs_0Params(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPCompleteProductGrantArgs_0Params = { "orderId" };
        internal static IAPCompleteProductGrantArgs_0Params R_IAPCompleteProductGrantArgs_0Params(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPCompleteProductGrantArgs_0Params();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPCompleteProductGrantArgs_0Params))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPGetSubscriptionInfoArgs_0 = { "params" };
        internal static IAPGetSubscriptionInfoArgs_0 R_IAPGetSubscriptionInfoArgs_0(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPGetSubscriptionInfoArgs_0();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPGetSubscriptionInfoArgs_0))
                {
                    case 0: o.Params = AITJsonCodec.R_IAPGetSubscriptionInfoArgs_0Params(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPGetSubscriptionInfoArgs_0Params = { "orderId" };
        internal static IAPGetSubscriptionInfoArgs_0Params R_IAPGetSubscriptionInfoArgs_0Params(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPGetSubscriptionInfoArgs_0Params();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPGetSubscriptionInfoArgs_0Params))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPGetPendingOrdersResult = { "orders", "error" };
        internal static IAPGetPendingOrdersResult R_IAPGetPendingOrdersResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPGetPendingOrdersResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPGetPendingOrdersResult))
                {
                    case 0: o.Orders = AITJson.Arr<IAPGetPendingOrdersResultOrder>(v, j1 => AITJsonCodec.R_IAPGetPendingOrdersResultOrder(j1)); break;
                    case 1: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPGetProductItemListResult = { "products", "error" };
        internal static IAPGetProductItemListResult R_IAPGetProductItemListResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPGetProductItemListResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPGetProductItemListResult))
                {
                    case 0: o.Products = AITJson.Arr<IapProductListItem>(v, j1 => AITJsonCodec.R_IapProductListItem(j1)); break;
                    case 1: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IAPGetPendingOrdersResultOrder = { "orderId", "sku", "paymentCompletedDate" };
        internal static IAPGetPendingOrdersResultOrder R_IAPGetPendingOrdersResultOrder(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IAPGetPendingOrdersResultOrder();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IAPGetPendingOrdersResultOrder))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    case 1: o.Sku = AITJson.Str(v); break;
                    case 2: o.PaymentCompletedDate = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetCurrentLocationOptions = { "accuracy" };
        internal static GetCurrentLocationOptions R_GetCurrentLocationOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetCurrentLocationOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetCurrentLocationOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Accuracy = AITJsonCodec.RE_Accuracy(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_StartUpdateLocationEventParams = { "options" };
        internal static StartUpdateLocationEventParams R_StartUpdateLocationEventParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new StartUpdateLocationEventParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_StartUpdateLocationEventParams))
                {
                    case 0: o.Options = AITJsonCodec.R_StartUpdateLocationOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_StartUpdateLocationOptions = { "accuracy", "timeInterval", "distanceInterval" };
        internal static StartUpdateLocationOptions R_StartUpdateLocationOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new StartUpdateLocationOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_StartUpdateLocationOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Accuracy = AITJsonCodec.RE_Accuracy(v).GetValueOrDefault(); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.TimeInterval = AITJson.Dbl(v); } break;
                    case 2: if (!AITJson.IsNull(v)) { o.DistanceInterval = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetPermissionPermission = { "name", "access" };
        internal static GetPermissionPermission R_GetPermissionPermission(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetPermissionPermission();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetPermissionPermission))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Name = AITJsonCodec.RE_PermissionName(v).GetValueOrDefault(); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.Access = AITJsonCodec.RE_PermissionAccess(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OpenPermissionDialogPermission = { "name", "access" };
        internal static OpenPermissionDialogPermission R_OpenPermissionDialogPermission(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OpenPermissionDialogPermission();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OpenPermissionDialogPermission))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Name = AITJsonCodec.RE_PermissionName(v).GetValueOrDefault(); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.Access = AITJsonCodec.RE_PermissionAccess(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestPermissionPermission = { "name", "access" };
        internal static RequestPermissionPermission R_RequestPermissionPermission(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestPermissionPermission();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestPermissionPermission))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Name = AITJsonCodec.RE_PermissionName(v).GetValueOrDefault(); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.Access = AITJsonCodec.RE_PermissionAccess(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetUserKeyForGameResult = { "_type", "_successJson", "_errorCode" };
        internal static GetUserKeyForGameResult R_GetUserKeyForGameResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetUserKeyForGameResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetUserKeyForGameResult))
                {
                    case 0: o._type = AITJson.Str(v); break;
                    case 1: o._successData = AITJsonCodec.R_GetAnonymousKeySuccessResponse(v); break;
                    case 2: o._errorCode = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GameSetLeaderboardScoreParams = { "score" };
        internal static GameSetLeaderboardScoreParams R_GameSetLeaderboardScoreParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GameSetLeaderboardScoreParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GameSetLeaderboardScoreParams))
                {
                    case 0: o.Score = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardResult = { "key", "errorCode", "message", "code" };
        internal static GrantPromotionRewardResult R_GrantPromotionRewardResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardResult))
                {
                    case 0: o.Key = AITJson.Str(v); break;
                    case 1: o.ErrorCode = AITJson.Str(v); break;
                    case 2: o.Message = AITJson.Str(v); break;
                    case 3: o.Code = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SubmitGameCenterLeaderBoardScoreParams = { "score" };
        internal static SubmitGameCenterLeaderBoardScoreParams R_SubmitGameCenterLeaderBoardScoreParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SubmitGameCenterLeaderBoardScoreParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SubmitGameCenterLeaderBoardScoreParams))
                {
                    case 0: o.Score = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GetUserKeyForGameResponse = { "hash", "type" };
        internal static GetUserKeyForGameResponse R_GetUserKeyForGameResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GetUserKeyForGameResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GetUserKeyForGameResponse))
                {
                    case 0: o.Hash = AITJson.Str(v); break;
                    case 1: o.Type = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SubmitGameCenterLeaderBoardScoreResponse = { "statusCode" };
        internal static SubmitGameCenterLeaderBoardScoreResponse R_SubmitGameCenterLeaderBoardScoreResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SubmitGameCenterLeaderBoardScoreResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SubmitGameCenterLeaderBoardScoreResponse))
                {
                    case 0: o.StatusCode = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardForGameParams = { "params" };
        internal static GrantPromotionRewardForGameParams R_GrantPromotionRewardForGameParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardForGameParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardForGameParams))
                {
                    case 0: o.Params = AITJsonCodec.R_GrantPromotionRewardForGameParamsParams(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardForGameParamsParams = { "promotionCode", "amount" };
        internal static GrantPromotionRewardForGameParamsParams R_GrantPromotionRewardForGameParamsParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardForGameParamsParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardForGameParamsParams))
                {
                    case 0: o.PromotionCode = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.Amount = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardForGameSuccessResponse = { "key" };
        internal static GrantPromotionRewardForGameSuccessResponse R_GrantPromotionRewardForGameSuccessResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardForGameSuccessResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardForGameSuccessResponse))
                {
                    case 0: o.Key = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardForGameErrorResponse = { "code" };
        internal static GrantPromotionRewardForGameErrorResponse R_GrantPromotionRewardForGameErrorResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardForGameErrorResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardForGameErrorResponse))
                {
                    case 0: o.Code = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardForGameErrorResult = { "errorCode", "message" };
        internal static GrantPromotionRewardForGameErrorResult R_GrantPromotionRewardForGameErrorResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardForGameErrorResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardForGameErrorResult))
                {
                    case 0: o.ErrorCode = AITJson.Str(v); break;
                    case 1: o.Message = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardForGameResponse = { "code", "key" };
        internal static GrantPromotionRewardForGameResponse R_GrantPromotionRewardForGameResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardForGameResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardForGameResponse))
                {
                    case 0: o.Code = AITJson.Str(v); break;
                    case 1: o.Key = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShareSendMessageMessage = { "message" };
        internal static ShareSendMessageMessage R_ShareSendMessageMessage(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShareSendMessageMessage();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShareSendMessageMessage))
                {
                    case 0: o.Message = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ViralGetCampaignOptions = { "campaignId" };
        internal static ViralGetCampaignOptions R_ViralGetCampaignOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ViralGetCampaignOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ViralGetCampaignOptions))
                {
                    case 0: o.CampaignId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ViralGetCampaignResult = { "campaignId", "benefitName", "display", "active", "campaignStatus", "participationAvailable", "participationUnavailableReason", "rules" };
        internal static ViralGetCampaignResult R_ViralGetCampaignResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ViralGetCampaignResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ViralGetCampaignResult))
                {
                    case 0: o.CampaignId = AITJson.Str(v); break;
                    case 1: o.BenefitName = AITJson.Str(v); break;
                    case 2: o.Display = AITJsonCodec.R_ViralGetCampaignResultDisplay(v); break;
                    case 3: if (!AITJson.IsNull(v)) { o.Active = AITJson.Bool(v); } break;
                    case 4: if (!AITJson.IsNull(v)) { o.CampaignStatus = AITJsonCodec.RE_ViralGetCampaignResultCampaignStatus(v).GetValueOrDefault(); } break;
                    case 5: if (!AITJson.IsNull(v)) { o.ParticipationAvailable = AITJson.Bool(v); } break;
                    case 6: if (!AITJson.IsNull(v)) { o.ParticipationUnavailableReason = AITJsonCodec.RE_ViralGetCampaignResultParticipationUnavailableReason(v).GetValueOrDefault(); } break;
                    case 7: o.Rules = AITJson.Arr<ViralGetCampaignResultRule>(v, j1 => AITJsonCodec.R_ViralGetCampaignResultRule(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ViralGetCampaignResultDisplay = { "title", "benefitText", "conditionText", "ctaText" };
        internal static ViralGetCampaignResultDisplay R_ViralGetCampaignResultDisplay(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ViralGetCampaignResultDisplay();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ViralGetCampaignResultDisplay))
                {
                    case 0: o.Title = AITJson.Str(v); break;
                    case 1: o.BenefitText = AITJson.Str(v); break;
                    case 2: o.ConditionText = AITJson.Str(v); break;
                    case 3: o.CtaText = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ViralGetCampaignResultRule = { "ruleId", "ruleKey", "revision", "title", "currentValue", "targetValue", "status", "achievedAt" };
        internal static ViralGetCampaignResultRule R_ViralGetCampaignResultRule(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ViralGetCampaignResultRule();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ViralGetCampaignResultRule))
                {
                    case 0: o.RuleId = AITJson.Str(v); break;
                    case 1: o.RuleKey = AITJson.Str(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.Revision = AITJson.Dbl(v); } break;
                    case 3: o.Title = AITJson.Str(v); break;
                    case 4: if (!AITJson.IsNull(v)) { o.CurrentValue = AITJson.Dbl(v); } break;
                    case 5: if (!AITJson.IsNull(v)) { o.TargetValue = AITJson.Dbl(v); } break;
                    case 6: if (!AITJson.IsNull(v)) { o.Status = AITJsonCodec.RE_ViralGetCampaignResultRuleStatus(v).GetValueOrDefault(); } break;
                    case 7: o.AchievedAt = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ViralShareOptions = { "campaignId", "title", "description" };
        internal static ViralShareOptions R_ViralShareOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ViralShareOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ViralShareOptions))
                {
                    case 0: o.CampaignId = AITJson.Str(v); break;
                    case 1: o.Title = AITJson.Str(v); break;
                    case 2: o.Description = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_TrackViralParams = { "eventName" };
        internal static TrackViralParams R_TrackViralParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new TrackViralParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_TrackViralParams))
                {
                    case 0: o.EventName = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShareMessage = { "message" };
        internal static ShareMessage R_ShareMessage(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShareMessage();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShareMessage))
                {
                    case 0: o.Message = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactsViralEventData = { "rewardAmount", "rewardUnit", "closeReason", "sentRewardAmount", "sendableRewardsCount", "sentRewardsCount" };
        internal static ContactsViralEventData R_ContactsViralEventData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactsViralEventData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactsViralEventData))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.RewardAmount = AITJson.Dbl(v); o.RewardAmountSpecified = true; } break;
                    case 1: o.RewardUnit = AITJson.Str(v); break;
                    case 2: o.CloseReason = AITJsonCodec.RE_ContactsViralEventDataCloseReason(v); break;
                    case 3: o.SentRewardAmount = AITJson.DblN(v); break;
                    case 4: o.SendableRewardsCount = AITJson.DblN(v); break;
                    case 5: o.SentRewardsCount = AITJson.DblN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactsViralEvent = { "type", "data" };
        internal static ContactsViralEvent R_ContactsViralEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactsViralEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactsViralEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_ContactsViralEventData(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactsViralSuccessEventData = { "closeReason", "sentRewardAmount", "sendableRewardsCount", "sentRewardsCount", "rewardUnit" };
        internal static ContactsViralSuccessEventData R_ContactsViralSuccessEventData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactsViralSuccessEventData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactsViralSuccessEventData))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.CloseReason = AITJsonCodec.RE_ContactsViralSuccessEventDataCloseReason(v).GetValueOrDefault(); } break;
                    case 1: o.SentRewardAmount = AITJson.DblN(v); break;
                    case 2: o.SendableRewardsCount = AITJson.DblN(v); break;
                    case 3: if (!AITJson.IsNull(v)) { o.SentRewardsCount = AITJson.Dbl(v); } break;
                    case 4: o.RewardUnit = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactsViralParamsOptions = { "moduleId" };
        internal static ContactsViralParamsOptions R_ContactsViralParamsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactsViralParamsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactsViralParamsOptions))
                {
                    case 0: o.ModuleId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactsViralParams = { "options" };
        internal static ContactsViralParams R_ContactsViralParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactsViralParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactsViralParams))
                {
                    case 0: o.Options = AITJsonCodec.R_ContactsViralParamsOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_FetchAlbumPhotosOptions = { "base64", "maxCount", "maxWidth" };
        internal static FetchAlbumPhotosOptions R_FetchAlbumPhotosOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new FetchAlbumPhotosOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_FetchAlbumPhotosOptions))
                {
                    case 0: o.Base64 = AITJson.BoolN(v); break;
                    case 1: o.MaxCount = AITJson.DblN(v); break;
                    case 2: o.MaxWidth = AITJson.DblN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OpenCameraOptions = { "base64", "maxWidth" };
        internal static OpenCameraOptions R_OpenCameraOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OpenCameraOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OpenCameraOptions))
                {
                    case 0: o.Base64 = AITJson.BoolN(v); break;
                    case 1: o.MaxWidth = AITJson.DblN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_FetchAlbumItemsOptions = { "types", "maxCount", "maxWidth", "base64" };
        internal static FetchAlbumItemsOptions R_FetchAlbumItemsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new FetchAlbumItemsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_FetchAlbumItemsOptions))
                {
                    case 0: o.Types = AITJson.Arr<AlbumItemType>(v, j1 => AITJsonCodec.RE_AlbumItemType(j1).GetValueOrDefault()); break;
                    case 1: o.MaxCount = AITJson.DblN(v); break;
                    case 2: o.MaxWidth = AITJson.DblN(v); break;
                    case 3: o.Base64 = AITJson.BoolN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OpenPDFViewerParams = { "data", "filename" };
        internal static OpenPDFViewerParams R_OpenPDFViewerParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OpenPDFViewerParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OpenPDFViewerParams))
                {
                    case 0: o.Data = AITJson.Str(v); break;
                    case 1: o.Filename = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SaveBase64DataParams = { "data", "fileName", "mimeType" };
        internal static SaveBase64DataParams R_SaveBase64DataParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SaveBase64DataParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SaveBase64DataParams))
                {
                    case 0: o.Data = AITJson.Str(v); break;
                    case 1: o.FileName = AITJson.Str(v); break;
                    case 2: o.MimeType = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_DeviceGetPhotosOptions = { "base64", "maxCount", "maxWidth" };
        internal static DeviceGetPhotosOptions R_DeviceGetPhotosOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new DeviceGetPhotosOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_DeviceGetPhotosOptions))
                {
                    case 0: o.Base64 = AITJson.BoolN(v); break;
                    case 1: o.MaxCount = AITJson.DblN(v); break;
                    case 2: o.MaxWidth = AITJson.DblN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_FetchContactsOptions = { "size", "offset", "query" };
        internal static FetchContactsOptions R_FetchContactsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new FetchContactsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_FetchContactsOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Size = AITJson.Dbl(v); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.Offset = AITJson.Dbl(v); } break;
                    case 2: o.Query = AITJsonCodec.R_FetchContactsOptionsQuery(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_FetchContactsOptionsQuery = { "contains" };
        internal static FetchContactsOptionsQuery R_FetchContactsOptionsQuery(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new FetchContactsOptionsQuery();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_FetchContactsOptionsQuery))
                {
                    case 0: o.Contains = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactResult = { "result", "nextOffset", "done" };
        internal static ContactResult R_ContactResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactResult))
                {
                    case 0: o.Result = AITJson.Arr<ContactEntity>(v, j1 => AITJsonCodec.R_ContactEntity(j1)); break;
                    case 1: o.NextOffset = AITJson.DblN(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.Done = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_DeviceOpenCameraOptions = { "base64", "maxWidth" };
        internal static DeviceOpenCameraOptions R_DeviceOpenCameraOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new DeviceOpenCameraOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_DeviceOpenCameraOptions))
                {
                    case 0: o.Base64 = AITJson.BoolN(v); break;
                    case 1: o.MaxWidth = AITJson.DblN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_DeviceGetAlbumItemsOptions = { "types", "maxCount", "maxWidth", "base64" };
        internal static DeviceGetAlbumItemsOptions R_DeviceGetAlbumItemsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new DeviceGetAlbumItemsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_DeviceGetAlbumItemsOptions))
                {
                    case 0: o.Types = AITJson.Arr<AlbumItemType>(v, j1 => AITJsonCodec.RE_AlbumItemType(j1).GetValueOrDefault()); break;
                    case 1: o.MaxCount = AITJson.DblN(v); break;
                    case 2: o.MaxWidth = AITJson.DblN(v); break;
                    case 3: o.Base64 = AITJson.BoolN(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ScreenSetAwakeModeResult = { "enabled" };
        internal static ScreenSetAwakeModeResult R_ScreenSetAwakeModeResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ScreenSetAwakeModeResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ScreenSetAwakeModeResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Enabled = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ScreenSetSecureResult = { "enabled" };
        internal static ScreenSetSecureResult R_ScreenSetSecureResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ScreenSetSecureResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ScreenSetSecureResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Enabled = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetScreenAwakeModeResult = { "enabled", "error" };
        internal static SetScreenAwakeModeResult R_SetScreenAwakeModeResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetScreenAwakeModeResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetScreenAwakeModeResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Enabled = AITJson.Bool(v); } break;
                    case 1: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetSecureScreenResult = { "enabled", "error" };
        internal static SetSecureScreenResult R_SetSecureScreenResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetSecureScreenResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetSecureScreenResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Enabled = AITJson.Bool(v); } break;
                    case 1: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetDeviceOrientationOptions = { "type" };
        internal static SetDeviceOrientationOptions R_SetDeviceOrientationOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetDeviceOrientationOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetDeviceOrientationOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Type = AITJsonCodec.RE_SetDeviceOrientationOptionsType(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetIosSwipeGestureEnabledOptions = { "isEnabled" };
        internal static SetIosSwipeGestureEnabledOptions R_SetIosSwipeGestureEnabledOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetIosSwipeGestureEnabledOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetIosSwipeGestureEnabledOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.IsEnabled = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetScreenAwakeModeOptions = { "enabled" };
        internal static SetScreenAwakeModeOptions R_SetScreenAwakeModeOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetScreenAwakeModeOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetScreenAwakeModeOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Enabled = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetSecureScreenOptions = { "enabled" };
        internal static SetSecureScreenOptions R_SetSecureScreenOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetSecureScreenOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetSecureScreenOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Enabled = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_EventLogParams = { "log_type", "log_name", "params" };
        internal static EventLogParams R_EventLogParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new EventLogParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_EventLogParams))
                {
                    case 0: o.Log_type = AITJson.Str(v); break;
                    case 1: o.Log_name = AITJson.Str(v); break;
                    case 2: o.Params = AITJson.Dict<string, object>(v, null, j1 => AITJson.Any(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AppsInTossSignTossCertParams = { "txId" };
        internal static AppsInTossSignTossCertParams R_AppsInTossSignTossCertParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AppsInTossSignTossCertParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AppsInTossSignTossCertParams))
                {
                    case 0: o.TxId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OnVisibilityChangedByTransparentServiceWebEventParams = { "options" };
        internal static OnVisibilityChangedByTransparentServiceWebEventParams R_OnVisibilityChangedByTransparentServiceWebEventParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OnVisibilityChangedByTransparentServiceWebEventParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OnVisibilityChangedByTransparentServiceWebEventParams))
                {
                    case 0: o.Options = AITJsonCodec.R_OnVisibilityChangedByTransparentServiceWebEventParamsOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OnVisibilityChangedByTransparentServiceWebEventParamsOptions = { "callbackId" };
        internal static OnVisibilityChangedByTransparentServiceWebEventParamsOptions R_OnVisibilityChangedByTransparentServiceWebEventParamsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OnVisibilityChangedByTransparentServiceWebEventParamsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OnVisibilityChangedByTransparentServiceWebEventParamsOptions))
                {
                    case 0: o.CallbackId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GoogleAdMobLoadAppsInTossAdMobArgs = { "options" };
        internal static GoogleAdMobLoadAppsInTossAdMobArgs R_GoogleAdMobLoadAppsInTossAdMobArgs(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GoogleAdMobLoadAppsInTossAdMobArgs();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GoogleAdMobLoadAppsInTossAdMobArgs))
                {
                    case 0: o.Options = AITJsonCodec.R_LoadAdMobOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_LoadAdMobOptions = { "adGroupId", "referrer" };
        internal static LoadAdMobOptions R_LoadAdMobOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new LoadAdMobOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_LoadAdMobOptions))
                {
                    case 0: o.AdGroupId = AITJson.Str(v); break;
                    case 1: o.Referrer = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_Error = { "name", "message", "stack", "cause" };
        internal static Error R_Error(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new Error();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_Error))
                {
                    case 0: o.Name = AITJson.Str(v); break;
                    case 1: o.Message = AITJson.Str(v); break;
                    case 2: o.Stack = AITJson.Str(v); break;
                    case 3: o.Cause = AITJson.Any(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GoogleAdMobShowAppsInTossAdMobArgs = { "options" };
        internal static GoogleAdMobShowAppsInTossAdMobArgs R_GoogleAdMobShowAppsInTossAdMobArgs(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GoogleAdMobShowAppsInTossAdMobArgs();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GoogleAdMobShowAppsInTossAdMobArgs))
                {
                    case 0: o.Options = AITJsonCodec.R_ShowAdMobOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShowAdMobOptions = { "adGroupId" };
        internal static ShowAdMobOptions R_ShowAdMobOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShowAdMobOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShowAdMobOptions))
                {
                    case 0: o.AdGroupId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IsAdMobLoadedOptions = { "adGroupId" };
        internal static IsAdMobLoadedOptions R_IsAdMobLoadedOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IsAdMobLoadedOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IsAdMobLoadedOptions))
                {
                    case 0: o.AdGroupId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_InitializeOptions = { "callbacks" };
        internal static InitializeOptions R_InitializeOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new InitializeOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_InitializeOptions))
                {
                    case 0: o.Callbacks = AITJsonCodec.R_InitializeOptionsCallbacks(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_InitializeOptionsCallbacks = {  };
        internal static InitializeOptionsCallbacks R_InitializeOptionsCallbacks(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new InitializeOptionsCallbacks();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_InitializeOptionsCallbacks))
                {
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_TossAdsAttachOptions = { "theme", "padding", "callbacks" };
        internal static TossAdsAttachOptions R_TossAdsAttachOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new TossAdsAttachOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_TossAdsAttachOptions))
                {
                    case 0: o.Theme = AITJson.Str(v); break;
                    case 1: o.Padding = AITJson.Str(v); break;
                    case 2: o.Callbacks = AITJsonCodec.R_BannerSlotCallbacks(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_BannerSlotCallbacks = {  };
        internal static BannerSlotCallbacks R_BannerSlotCallbacks(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new BannerSlotCallbacks();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_BannerSlotCallbacks))
                {
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_BannerSlotEventPayload = { "slotId", "adGroupId", "adMetadata" };
        internal static BannerSlotEventPayload R_BannerSlotEventPayload(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new BannerSlotEventPayload();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_BannerSlotEventPayload))
                {
                    case 0: o.SlotId = AITJson.Str(v); break;
                    case 1: o.AdGroupId = AITJson.Str(v); break;
                    case 2: o.AdMetadata = AITJsonCodec.R_BannerSlotEventPayloadAdMetadata(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_BannerSlotEventPayloadAdMetadata = { "creativeId", "requestId" };
        internal static BannerSlotEventPayloadAdMetadata R_BannerSlotEventPayloadAdMetadata(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new BannerSlotEventPayloadAdMetadata();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_BannerSlotEventPayloadAdMetadata))
                {
                    case 0: o.CreativeId = AITJson.Str(v); break;
                    case 1: o.RequestId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_BannerSlotErrorPayload = { "slotId", "adGroupId", "adMetadata", "error" };
        internal static BannerSlotErrorPayload R_BannerSlotErrorPayload(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new BannerSlotErrorPayload();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_BannerSlotErrorPayload))
                {
                    case 0: o.SlotId = AITJson.Str(v); break;
                    case 1: o.AdGroupId = AITJson.Str(v); break;
                    case 2: o.AdMetadata = AITJson.Dict<string, object>(v, null, j1 => AITJson.Any(j1)); break;
                    case 3: o.Error = AITJsonCodec.R_BannerSlotErrorPayloadError(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_BannerSlotErrorPayloadError = { "code", "message", "domain", "mediationId" };
        internal static BannerSlotErrorPayloadError R_BannerSlotErrorPayloadError(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new BannerSlotErrorPayloadError();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_BannerSlotErrorPayloadError))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Code = AITJson.Dbl(v); } break;
                    case 1: o.Message = AITJson.Str(v); break;
                    case 2: o.Domain = AITJson.Str(v); break;
                    case 3: o.MediationId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_BannerSlotCallbacksOnNoFillParam = { "slotId", "adGroupId", "adMetadata" };
        internal static BannerSlotCallbacksOnNoFillParam R_BannerSlotCallbacksOnNoFillParam(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new BannerSlotCallbacksOnNoFillParam();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_BannerSlotCallbacksOnNoFillParam))
                {
                    case 0: o.SlotId = AITJson.Str(v); break;
                    case 1: o.AdGroupId = AITJson.Str(v); break;
                    case 2: o.AdMetadata = AITJson.Dict<string, object>(v, null, j1 => AITJson.Any(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_TossAdsAttachBannerOptions = { "theme", "tone", "variant", "callbacks" };
        internal static TossAdsAttachBannerOptions R_TossAdsAttachBannerOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new TossAdsAttachBannerOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_TossAdsAttachBannerOptions))
                {
                    case 0: o.Theme = AITJson.Str(v); break;
                    case 1: o.Tone = AITJson.Str(v); break;
                    case 2: o.Variant = AITJson.Str(v); break;
                    case 3: o.Callbacks = AITJsonCodec.R_BannerSlotCallbacks(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AttachBannerResult = { "error" };
        internal static AttachBannerResult R_AttachBannerResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AttachBannerResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AttachBannerResult))
                {
                    case 0: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShowFullScreenAdEvent = { "type", "data" };
        internal static ShowFullScreenAdEvent R_ShowFullScreenAdEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShowFullScreenAdEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShowFullScreenAdEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_ShowFullScreenAdEventData(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShowFullScreenAdEventData = { "unitType", "unitAmount" };
        internal static ShowFullScreenAdEventData R_ShowFullScreenAdEventData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShowFullScreenAdEventData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShowFullScreenAdEventData))
                {
                    case 0: o.UnitType = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.UnitAmount = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_LoadFullScreenAdEvent = { "type" };
        internal static LoadFullScreenAdEvent R_LoadFullScreenAdEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new LoadFullScreenAdEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_LoadFullScreenAdEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_LoadFullScreenAdOptions = { "adGroupId" };
        internal static LoadFullScreenAdOptions R_LoadFullScreenAdOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new LoadFullScreenAdOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_LoadFullScreenAdOptions))
                {
                    case 0: o.AdGroupId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShowFullScreenAdOptions = { "adGroupId" };
        internal static ShowFullScreenAdOptions R_ShowFullScreenAdOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShowFullScreenAdOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShowFullScreenAdOptions))
                {
                    case 0: o.AdGroupId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SafeAreaInsets = { "top", "left", "right", "bottom", "error" };
        internal static SafeAreaInsets R_SafeAreaInsets(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SafeAreaInsets();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SafeAreaInsets))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Top = AITJson.Dbl(v); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.Left = AITJson.Dbl(v); } break;
                    case 2: if (!AITJson.IsNull(v)) { o.Right = AITJson.Dbl(v); } break;
                    case 3: if (!AITJson.IsNull(v)) { o.Bottom = AITJson.Dbl(v); } break;
                    case 4: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IsMinVersionSupportedMinVersions = { "android", "ios" };
        internal static IsMinVersionSupportedMinVersions R_IsMinVersionSupportedMinVersions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IsMinVersionSupportedMinVersions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IsMinVersionSupportedMinVersions))
                {
                    case 0: o.Android = AITJson.Str(v); break;
                    case 1: o.Ios = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RewardFromContactsViralEvent = { "type", "data" };
        internal static RewardFromContactsViralEvent R_RewardFromContactsViralEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RewardFromContactsViralEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RewardFromContactsViralEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_RewardFromContactsViralEventData(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactsViralSuccessEvent = { "type", "data" };
        internal static ContactsViralSuccessEvent R_ContactsViralSuccessEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactsViralSuccessEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactsViralSuccessEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_ContactsViralSuccessEventData(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardErrorResponse = { "code" };
        internal static GrantPromotionRewardErrorResponse R_GrantPromotionRewardErrorResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardErrorResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardErrorResponse))
                {
                    case 0: o.Code = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardErrorResult = { "errorCode", "message" };
        internal static GrantPromotionRewardErrorResult R_GrantPromotionRewardErrorResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardErrorResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardErrorResult))
                {
                    case 0: o.ErrorCode = AITJson.Str(v); break;
                    case 1: o.Message = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardParams = { "params" };
        internal static GrantPromotionRewardParams R_GrantPromotionRewardParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardParams))
                {
                    case 0: o.Params = AITJsonCodec.R_GrantPromotionRewardParamsParams(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardParamsParams = { "promotionCode", "amount" };
        internal static GrantPromotionRewardParamsParams R_GrantPromotionRewardParamsParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardParamsParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardParamsParams))
                {
                    case 0: o.PromotionCode = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.Amount = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantPromotionRewardResponse = { "key" };
        internal static GrantPromotionRewardResponse R_GrantPromotionRewardResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantPromotionRewardResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantPromotionRewardResponse))
                {
                    case 0: o.Key = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestNotificationAgreementOptions = { "options" };
        internal static RequestNotificationAgreementOptions R_RequestNotificationAgreementOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestNotificationAgreementOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestNotificationAgreementOptions))
                {
                    case 0: o.Options = AITJsonCodec.R_RequestNotificationAgreementOptionsOptions(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestNotificationAgreementOptionsOnEventParam = { "type" };
        internal static RequestNotificationAgreementOptionsOnEventParam R_RequestNotificationAgreementOptionsOnEventParam(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestNotificationAgreementOptionsOnEventParam();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestNotificationAgreementOptionsOnEventParam))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Type = AITJsonCodec.RE_RequestNotificationAgreementOptionsOnEventParamType(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RequestNotificationAgreementOptionsOptions = { "templateCode" };
        internal static RequestNotificationAgreementOptionsOptions R_RequestNotificationAgreementOptionsOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RequestNotificationAgreementOptionsOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RequestNotificationAgreementOptionsOptions))
                {
                    case 0: o.TemplateCode = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_MigrationGetOriginStorageResult = { "previous", "current" };
        internal static MigrationGetOriginStorageResult R_MigrationGetOriginStorageResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new MigrationGetOriginStorageResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_MigrationGetOriginStorageResult))
                {
                    case 0: o.Previous = AITJsonCodec.R_OriginStorageDump(v); break;
                    case 1: o.Current = AITJsonCodec.R_OriginStorageDump(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AdMobLoadResult = { "adGroupId", "adUnitId", "responseInfo" };
        internal static AdMobLoadResult R_AdMobLoadResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AdMobLoadResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AdMobLoadResult))
                {
                    case 0: o.AdGroupId = AITJson.Str(v); break;
                    case 1: o.AdUnitId = AITJson.Str(v); break;
                    case 2: o.ResponseInfo = AITJsonCodec.R_ResponseInfo(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ResponseInfo = { "adNetworkInfoArray", "loadedAdNetworkInfo", "responseId" };
        internal static ResponseInfo R_ResponseInfo(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ResponseInfo();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ResponseInfo))
                {
                    case 0: o.AdNetworkInfoArray = AITJson.Arr<AdNetworkResponseInfo>(v, j1 => AITJsonCodec.R_AdNetworkResponseInfo(j1)); break;
                    case 1: o.LoadedAdNetworkInfo = AITJsonCodec.R_AdNetworkResponseInfo(v); break;
                    case 2: o.ResponseId = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AdNetworkResponseInfo = { "adSourceId", "adSourceName", "adSourceInstanceId", "adSourceInstanceName", "adNetworkClassName" };
        internal static AdNetworkResponseInfo R_AdNetworkResponseInfo(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AdNetworkResponseInfo();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AdNetworkResponseInfo))
                {
                    case 0: o.AdSourceId = AITJson.Str(v); break;
                    case 1: o.AdSourceName = AITJson.Str(v); break;
                    case 2: o.AdSourceInstanceId = AITJson.Str(v); break;
                    case 3: o.AdSourceInstanceName = AITJson.Str(v); break;
                    case 4: o.AdNetworkClassName = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShowAdMobEvent = { "type", "data" };
        internal static ShowAdMobEvent R_ShowAdMobEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShowAdMobEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShowAdMobEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_ShowAdMobEventData(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ShowAdMobEventData = { "unitType", "unitAmount" };
        internal static ShowAdMobEventData R_ShowAdMobEventData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ShowAdMobEventData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ShowAdMobEventData))
                {
                    case 0: o.UnitType = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.UnitAmount = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_Offer = { "type", "offerId", "period", "displayAmount" };
        internal static Offer R_Offer(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new Offer();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_Offer))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.OfferId = AITJson.Str(v); break;
                    case 2: o.Period = AITJson.Str(v); break;
                    case 3: o.DisplayAmount = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapProductListItem = { "sku", "displayAmount", "displayName", "iconUrl", "description", "type", "renewalCycle", "offers" };
        internal static IapProductListItem R_IapProductListItem(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapProductListItem();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapProductListItem))
                {
                    case 0: o.Sku = AITJson.Str(v); break;
                    case 1: o.DisplayAmount = AITJson.Str(v); break;
                    case 2: o.DisplayName = AITJson.Str(v); break;
                    case 3: o.IconUrl = AITJson.Str(v); break;
                    case 4: o.Description = AITJson.Str(v); break;
                    case 5: o.Type = AITJson.Str(v); break;
                    case 6: o.RenewalCycle = AITJsonCodec.RE_IapProductListItemRenewalCycle(v); break;
                    case 7: o.Offers = AITJson.Arr<Offer>(v, j1 => AITJsonCodec.R_Offer(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GameCenterGameProfileResponse = { "statusCode", "nickname", "profileImageUri" };
        internal static GameCenterGameProfileResponse R_GameCenterGameProfileResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GameCenterGameProfileResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GameCenterGameProfileResponse))
                {
                    case 0: o.StatusCode = AITJson.Str(v); break;
                    case 1: o.Nickname = AITJson.Str(v); break;
                    case 2: o.ProfileImageUri = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AdUserEarnedReward = { "type", "data" };
        internal static AdUserEarnedReward R_AdUserEarnedReward(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AdUserEarnedReward();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AdUserEarnedReward))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_AdUserEarnedRewardData(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AdUserEarnedRewardData = { "unitType", "unitAmount" };
        internal static AdUserEarnedRewardData R_AdUserEarnedRewardData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AdUserEarnedRewardData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AdUserEarnedRewardData))
                {
                    case 0: o.UnitType = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.UnitAmount = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AddAccessoryButtonOptions = { "id", "title", "icon" };
        internal static AddAccessoryButtonOptions R_AddAccessoryButtonOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AddAccessoryButtonOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AddAccessoryButtonOptions))
                {
                    case 0: o.Id = AITJson.Str(v); break;
                    case 1: o.Title = AITJson.Str(v); break;
                    case 2: o.Icon = AITJsonCodec.R_AddAccessoryButtonOptionsIcon(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AddAccessoryButtonOptionsIcon = { "name" };
        internal static AddAccessoryButtonOptionsIcon R_AddAccessoryButtonOptionsIcon(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AddAccessoryButtonOptionsIcon();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AddAccessoryButtonOptionsIcon))
                {
                    case 0: o.Name = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AlbumItemResponse = { "id", "dataUri", "type" };
        internal static AlbumItemResponse R_AlbumItemResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AlbumItemResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AlbumItemResponse))
                {
                    case 0: o.Id = AITJson.Str(v); break;
                    case 1: o.DataUri = AITJson.Str(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.Type = AITJsonCodec.RE_AlbumItemType(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_AppsInTossGlobals = { "deploymentId", "brandDisplayName", "brandIcon", "brandPrimaryColor", "error" };
        internal static AppsInTossGlobals R_AppsInTossGlobals(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new AppsInTossGlobals();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_AppsInTossGlobals))
                {
                    case 0: o.DeploymentId = AITJson.Str(v); break;
                    case 1: o.BrandDisplayName = AITJson.Str(v); break;
                    case 2: o.BrandIcon = AITJson.Str(v); break;
                    case 3: o.BrandPrimaryColor = AITJson.Str(v); break;
                    case 4: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CompletedOrRefundedOrdersResultOrder = { "orderId", "sku", "status", "date" };
        internal static CompletedOrRefundedOrdersResultOrder R_CompletedOrRefundedOrdersResultOrder(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CompletedOrRefundedOrdersResultOrder();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CompletedOrRefundedOrdersResultOrder))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    case 1: o.Sku = AITJson.Str(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.Status = AITJsonCodec.RE_CompletedOrRefundedOrdersResultOrderStatus(v).GetValueOrDefault(); } break;
                    case 3: o.Date = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CompletedOrRefundedOrdersResult = { "hasNext", "nextKey", "orders", "error" };
        internal static CompletedOrRefundedOrdersResult R_CompletedOrRefundedOrdersResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CompletedOrRefundedOrdersResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CompletedOrRefundedOrdersResult))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.HasNext = AITJson.Bool(v); } break;
                    case 1: o.NextKey = AITJson.Str(v); break;
                    case 2: o.Orders = AITJson.Arr<CompletedOrRefundedOrdersResultOrder>(v, j1 => AITJsonCodec.R_CompletedOrRefundedOrdersResultOrder(j1)); break;
                    case 3: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ContactEntity = { "name", "phoneNumber" };
        internal static ContactEntity R_ContactEntity(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ContactEntity();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ContactEntity))
                {
                    case 0: o.Name = AITJson.Str(v); break;
                    case 1: o.PhoneNumber = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_RewardFromContactsViralEventData = { "rewardAmount", "rewardUnit" };
        internal static RewardFromContactsViralEventData R_RewardFromContactsViralEventData(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new RewardFromContactsViralEventData();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_RewardFromContactsViralEventData))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.RewardAmount = AITJson.Dbl(v); } break;
                    case 1: o.RewardUnit = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_CreateLinkParams = { "path", "ogImageUrl" };
        internal static CreateLinkParams R_CreateLinkParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new CreateLinkParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_CreateLinkParams))
                {
                    case 0: o.Path = AITJson.Str(v); break;
                    case 1: o.OgImageUrl = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_DeclaredAgeRange = { "status", "lowerBound", "upperBound", "ageRangeDeclaration", "activeParentalControls", "isEligibleForAgeFeatures", "errorCode" };
        internal static DeclaredAgeRange R_DeclaredAgeRange(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new DeclaredAgeRange();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_DeclaredAgeRange))
                {
                    case 0: o.Status = AITJson.Str(v); break;
                    case 1: o.LowerBound = AITJson.DblN(v); break;
                    case 2: o.UpperBound = AITJson.DblN(v); break;
                    case 3: o.AgeRangeDeclaration = AITJson.Str(v); break;
                    case 4: o.ActiveParentalControls = AITJson.Arr<string>(v, j1 => AITJson.Str(j1)); break;
                    case 5: o.IsEligibleForAgeFeatures = AITJson.BoolN(v); break;
                    case 6: o.ErrorCode = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_GrantRewardParams = { "promotionCode", "amount" };
        internal static GrantRewardParams R_GrantRewardParams(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new GrantRewardParams();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_GrantRewardParams))
                {
                    case 0: o.PromotionCode = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.Amount = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_HapticFeedbackOptions = { "type" };
        internal static HapticFeedbackOptions R_HapticFeedbackOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new HapticFeedbackOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_HapticFeedbackOptions))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Type = AITJsonCodec.RE_HapticFeedbackType(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapCreateOneTimePurchaseOrderResult = { "orderId", "displayName", "displayAmount", "amount", "currency", "fraction", "miniAppIconUrl" };
        internal static IapCreateOneTimePurchaseOrderResult R_IapCreateOneTimePurchaseOrderResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapCreateOneTimePurchaseOrderResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapCreateOneTimePurchaseOrderResult))
                {
                    case 0: o.OrderId = AITJson.Str(v); break;
                    case 1: o.DisplayName = AITJson.Str(v); break;
                    case 2: o.DisplayAmount = AITJson.Str(v); break;
                    case 3: if (!AITJson.IsNull(v)) { o.Amount = AITJson.Dbl(v); } break;
                    case 4: o.Currency = AITJson.Str(v); break;
                    case 5: if (!AITJson.IsNull(v)) { o.Fraction = AITJson.Dbl(v); } break;
                    case 6: o.MiniAppIconUrl = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapSubscriptionInfoResponse = { "subscription", "error" };
        internal static IapSubscriptionInfoResponse R_IapSubscriptionInfoResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapSubscriptionInfoResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapSubscriptionInfoResponse))
                {
                    case 0: o.Subscription = AITJsonCodec.R_IapSubscriptionInfoResult(v); break;
                    case 1: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_IapSubscriptionInfoResult = { "status", "catalogId", "expiresAt", "isAutoRenew", "gracePeriodExpiresAt", "isAccessible" };
        internal static IapSubscriptionInfoResult R_IapSubscriptionInfoResult(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new IapSubscriptionInfoResult();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_IapSubscriptionInfoResult))
                {
                    case 0: o.Status = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.CatalogId = AITJson.Dbl(v); } break;
                    case 2: o.ExpiresAt = AITJson.Str(v); break;
                    case 3: if (!AITJson.IsNull(v)) { o.IsAutoRenew = AITJson.Bool(v); } break;
                    case 4: o.GracePeriodExpiresAt = AITJson.Str(v); break;
                    case 5: if (!AITJson.IsNull(v)) { o.IsAccessible = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_ImageResponse = { "id", "dataUri" };
        internal static ImageResponse R_ImageResponse(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new ImageResponse();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_ImageResponse))
                {
                    case 0: o.Id = AITJson.Str(v); break;
                    case 1: o.DataUri = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_LoadAdMobEvent = { "type", "data" };
        internal static LoadAdMobEvent R_LoadAdMobEvent(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new LoadAdMobEvent();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_LoadAdMobEvent))
                {
                    case 0: o.Type = AITJson.Str(v); break;
                    case 1: o.Data = AITJsonCodec.R_AdMobLoadResult(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_Location = { "accessLocation", "timestamp", "coords", "error" };
        internal static Location R_Location(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new Location();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_Location))
                {
                    case 0: o.AccessLocation = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.Timestamp = AITJson.Dbl(v); } break;
                    case 2: o.Coords = AITJsonCodec.R_LocationCoords(v); break;
                    case 3: o.error = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_LocationCoords = { "latitude", "longitude", "altitude", "accuracy", "altitudeAccuracy", "heading" };
        internal static LocationCoords R_LocationCoords(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new LocationCoords();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_LocationCoords))
                {
                    case 0: if (!AITJson.IsNull(v)) { o.Latitude = AITJson.Dbl(v); } break;
                    case 1: if (!AITJson.IsNull(v)) { o.Longitude = AITJson.Dbl(v); } break;
                    case 2: if (!AITJson.IsNull(v)) { o.Altitude = AITJson.Dbl(v); } break;
                    case 3: if (!AITJson.IsNull(v)) { o.Accuracy = AITJson.Dbl(v); } break;
                    case 4: if (!AITJson.IsNull(v)) { o.AltitudeAccuracy = AITJson.Dbl(v); } break;
                    case 5: if (!AITJson.IsNull(v)) { o.Heading = AITJson.Dbl(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDump = { "origin", "localStorage", "indexedDB", "opfs", "errors" };
        internal static OriginStorageDump R_OriginStorageDump(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDump();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDump))
                {
                    case 0: o.Origin = AITJson.Str(v); break;
                    case 1: o.LocalStorage = AITJson.Dict<string, object>(v, null, j1 => AITJson.Any(j1)); break;
                    case 2: o.IndexedDB = AITJson.Arr<OriginStorageDumpIndexedDB>(v, j1 => AITJsonCodec.R_OriginStorageDumpIndexedDB(j1)); break;
                    case 3: o.Opfs = AITJsonCodec.R_OriginStorageDumpOpfs(v); break;
                    case 4: o.Errors = AITJson.Arr<OriginStorageDumpError>(v, j1 => AITJsonCodec.R_OriginStorageDumpError(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpIndexedDBObjectStoreIndex = { "name", "keyPath", "multiEntry", "unique" };
        internal static OriginStorageDumpIndexedDBObjectStoreIndex R_OriginStorageDumpIndexedDBObjectStoreIndex(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpIndexedDBObjectStoreIndex();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpIndexedDBObjectStoreIndex))
                {
                    case 0: o.Name = AITJson.Str(v); break;
                    case 1: o.KeyPath = AITJson.Any(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.MultiEntry = AITJson.Bool(v); } break;
                    case 3: if (!AITJson.IsNull(v)) { o.Unique = AITJson.Bool(v); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpIndexedDBObjectStoreRecord = { "key", "primaryKey", "value" };
        internal static OriginStorageDumpIndexedDBObjectStoreRecord R_OriginStorageDumpIndexedDBObjectStoreRecord(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpIndexedDBObjectStoreRecord();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpIndexedDBObjectStoreRecord))
                {
                    case 0: o.Key = AITJson.Any(v); break;
                    case 1: o.PrimaryKey = AITJson.Any(v); break;
                    case 2: o.Value = AITJson.Any(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpIndexedDBObjectStore = { "name", "keyPath", "autoIncrement", "indexes", "records" };
        internal static OriginStorageDumpIndexedDBObjectStore R_OriginStorageDumpIndexedDBObjectStore(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpIndexedDBObjectStore();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpIndexedDBObjectStore))
                {
                    case 0: o.Name = AITJson.Str(v); break;
                    case 1: o.KeyPath = AITJson.Any(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.AutoIncrement = AITJson.Bool(v); } break;
                    case 3: o.Indexes = AITJson.Arr<OriginStorageDumpIndexedDBObjectStoreIndex>(v, j1 => AITJsonCodec.R_OriginStorageDumpIndexedDBObjectStoreIndex(j1)); break;
                    case 4: o.Records = AITJson.Arr<OriginStorageDumpIndexedDBObjectStoreRecord>(v, j1 => AITJsonCodec.R_OriginStorageDumpIndexedDBObjectStoreRecord(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpIndexedDB = { "name", "version", "objectStores" };
        internal static OriginStorageDumpIndexedDB R_OriginStorageDumpIndexedDB(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpIndexedDB();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpIndexedDB))
                {
                    case 0: o.Name = AITJson.Str(v); break;
                    case 1: if (!AITJson.IsNull(v)) { o.Version = AITJson.Dbl(v); } break;
                    case 2: o.ObjectStores = AITJson.Arr<OriginStorageDumpIndexedDBObjectStore>(v, j1 => AITJsonCodec.R_OriginStorageDumpIndexedDBObjectStore(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpOpfsFile = { "path", "type", "lastModified", "data" };
        internal static OriginStorageDumpOpfsFile R_OriginStorageDumpOpfsFile(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpOpfsFile();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpOpfsFile))
                {
                    case 0: o.Path = AITJson.Str(v); break;
                    case 1: o.Type = AITJson.Str(v); break;
                    case 2: if (!AITJson.IsNull(v)) { o.LastModified = AITJson.Dbl(v); } break;
                    case 3: o.Data = AITJson.Bytes(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpOpfs = { "directories", "files" };
        internal static OriginStorageDumpOpfs R_OriginStorageDumpOpfs(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpOpfs();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpOpfs))
                {
                    case 0: o.Directories = AITJson.Arr<string>(v, j1 => AITJson.Str(j1)); break;
                    case 1: o.Files = AITJson.Arr<OriginStorageDumpOpfsFile>(v, j1 => AITJsonCodec.R_OriginStorageDumpOpfsFile(j1)); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_OriginStorageDumpError = { "storage", "message" };
        internal static OriginStorageDumpError R_OriginStorageDumpError(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new OriginStorageDumpError();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_OriginStorageDumpError))
                {
                    case 0: o.Storage = AITJson.Str(v); break;
                    case 1: o.Message = AITJson.Str(v); break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SetNavigationBarOptions = { "withBackButton", "withHomeButton", "withTitle", "transparentBackground", "backgroundColor", "theme" };
        internal static SetNavigationBarOptions R_SetNavigationBarOptions(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SetNavigationBarOptions();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SetNavigationBarOptions))
                {
                    case 0: o.WithBackButton = AITJson.BoolN(v); break;
                    case 1: o.WithHomeButton = AITJson.BoolN(v); break;
                    case 2: o.WithTitle = AITJson.BoolN(v); break;
                    case 3: o.TransparentBackground = AITJson.BoolN(v); break;
                    case 4: o.BackgroundColor = AITJson.Str(v); break;
                    case 5: if (!AITJson.IsNull(v)) { o.Theme = AITJsonCodec.RE_NavigationBarTheme(v).GetValueOrDefault(); } break;
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_SafeAreaInsetsSubscribe__0 = {  };
        internal static SafeAreaInsetsSubscribe__0 R_SafeAreaInsetsSubscribe__0(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new SafeAreaInsetsSubscribe__0();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_SafeAreaInsetsSubscribe__0))
                {
                    default: AITJson.Ext(ref o._extensionData, p); break;
                }
            }
            return o;
        }

        private static readonly string[] N_number = {  };
        internal static number R_number(JsonValue j)
        {
            if (j == null || j.Kind != JsonValueKind.Object) return null;
            var o = new number();
            foreach (var p in j._members)
            {
                var v = p.Value;
                switch (AITJson.Match(p.Key, N_number))
                {
                    default: break;
                }
            }
            return o;
        }
    }

    partial class AITCallbackBagEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("event"); w.Value(this.Event);
            w.Name("payload"); AITJson.Val(w, this.Payload);
            w.EndObject();
        }
    }

    partial class APIResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("success"); w.Value(this.success);
            w.Name("data"); w.Value(this.data);
            w.Name("error"); w.Value(this.error);
            w.Name("errorName"); w.Value(this.errorName);
            w.EndObject();
        }
    }

    partial class AITCore
    {
        partial class VisibilityStateData : IAITJsonWritable
        {
            void IAITJsonWritable.WriteJson(AITJsonWriter w)
            {
                w.BeginObject();
                w.Name("isVisible"); w.Value(this.isVisible);
                w.EndObject();
            }
        }
    }

    partial class AITCore
    {
        partial class CallbackData : IAITJsonWritable
        {
            void IAITJsonWritable.WriteJson(AITJsonWriter w)
            {
                w.BeginObject();
                w.Name("CallbackId"); w.Value(this.CallbackId);
                w.Name("TypeName"); w.Value(this.TypeName);
                w.Name("Result"); w.Value(this.Result);
                w.EndObject();
            }
        }
    }

    partial class AITCore
    {
        partial class NestedCallbackRequest : IAITJsonWritable
        {
            void IAITJsonWritable.WriteJson(AITJsonWriter w)
            {
                w.BeginObject();
                w.Name("requestId"); w.Value(this.RequestId);
                w.Name("callbackId"); w.Value(this.CallbackId);
                w.Name("callbackName"); w.Value(this.CallbackName);
                w.Name("data"); w.Value(this.Data);
                w.EndObject();
            }
        }
    }

    partial class TdsNavigationAccessoryEventData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("id"); w.Value(this.Id);
            w.EndObject();
        }
    }

    partial class AppLoginResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("authorizationCode"); w.Value(this.AuthorizationCode);
            w.Name("referrer"); AITJsonCodec.WE_AppLoginResultReferrer(w, this.Referrer);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetAnonymousKeyResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("_type"); w.Value(this._type);
            w.Name("_successJson"); AITJson.Obj(w, this._successData);
            w.Name("_errorCode"); w.Value(this._errorCode);
            w.Name("IsSuccess"); w.Value(this.IsSuccess);
            w.Name("IsError"); w.Value(this.IsError);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class UserCreateAnonymousKeyAuthCodeResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("code"); w.Value(this.Code);
            w.Name("expiresIn"); w.Value(this.ExpiresIn);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetAnonymousKeySuccessResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("hash"); w.Value(this.Hash);
            w.Name("type"); w.Value(this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetConsentedUserDataOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("consentedUserDataKey"); w.Value(this.ConsentedUserDataKey);
            w.Name("shouldRequestAgreementWhenUserDeclined"); w.Value(this.ShouldRequestAgreementWhenUserDeclined);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetDeclaredAgeRangeParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("ageGates"); AITJson.Arr<double>(w, this.AgeGates, (w1, x1) => w1.Value(x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CheckoutPaymentParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("payToken"); w.Value(this.PayToken);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestTossPayPaysBillingParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("wrappedToken"); w.Value(this.WrappedToken);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class TossPayCheckoutPaymentOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class TossPayRequestTossPayPaysBillingOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CheckoutPaymentOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestTossPayPaysBillingOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CheckoutPaymentResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("success"); w.Value(this.Success);
            w.Name("reason"); w.Value(this.Reason);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestTossPayPaysBillingResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("success"); w.Value(this.Success);
            w.Name("reason"); w.Value(this.Reason);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapCreateOneTimePurchaseOrderOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapCreateOneTimePurchaseOrderOptionsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("productId"); w.Value(this.ProductId);
            w.Name("sku"); w.Value(this.Sku);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SuccessEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CreateSubscriptionPurchaseOrderOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CreateSubscriptionPurchaseOrderOptionsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("sku"); w.Value(this.Sku);
            w.Name("offerId"); w.Value(this.OfferId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            w.Name("subscriptionId"); w.Value(this.SubscriptionId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SubscriptionSuccessEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPGetCompletedOrRefundedOrdersParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("key"); w.Value(this.Key);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPCompleteProductGrantArgs_0 : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPCompleteProductGrantArgs_0Params : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPGetSubscriptionInfoArgs_0 : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPGetSubscriptionInfoArgs_0Params : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPGetPendingOrdersResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orders"); AITJson.Arr<IAPGetPendingOrdersResultOrder>(w, this.Orders, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPGetProductItemListResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("products"); AITJson.Arr<IapProductListItem>(w, this.Products, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IAPGetPendingOrdersResultOrder : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            w.Name("sku"); w.Value(this.Sku);
            w.Name("paymentCompletedDate"); w.Value(this.PaymentCompletedDate);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetCurrentLocationOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("accuracy"); AITJsonCodec.WE_Accuracy(w, this.Accuracy);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class StartUpdateLocationEventParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class StartUpdateLocationOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("accuracy"); AITJsonCodec.WE_Accuracy(w, this.Accuracy);
            w.Name("timeInterval"); w.Value(this.TimeInterval);
            w.Name("distanceInterval"); w.Value(this.DistanceInterval);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetPermissionPermission : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); AITJsonCodec.WE_PermissionName(w, this.Name);
            w.Name("access"); AITJsonCodec.WE_PermissionAccess(w, this.Access);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OpenPermissionDialogPermission : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); AITJsonCodec.WE_PermissionName(w, this.Name);
            w.Name("access"); AITJsonCodec.WE_PermissionAccess(w, this.Access);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestPermissionPermission : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); AITJsonCodec.WE_PermissionName(w, this.Name);
            w.Name("access"); AITJsonCodec.WE_PermissionAccess(w, this.Access);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetUserKeyForGameResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("_type"); w.Value(this._type);
            w.Name("_successJson"); AITJson.Obj(w, this._successData);
            w.Name("_errorCode"); w.Value(this._errorCode);
            w.Name("IsSuccess"); w.Value(this.IsSuccess);
            w.Name("IsError"); w.Value(this.IsError);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GameSetLeaderboardScoreParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("score"); w.Value(this.Score);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("key"); w.Value(this.Key);
            w.Name("errorCode"); w.Value(this.ErrorCode);
            w.Name("message"); w.Value(this.Message);
            w.Name("code"); w.Value(this.Code);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SubmitGameCenterLeaderBoardScoreParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("score"); w.Value(this.Score);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GetUserKeyForGameResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("hash"); w.Value(this.Hash);
            w.Name("type"); w.Value(this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SubmitGameCenterLeaderBoardScoreResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("statusCode"); w.Value(this.StatusCode);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardForGameParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardForGameParamsParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("promotionCode"); w.Value(this.PromotionCode);
            w.Name("amount"); w.Value(this.Amount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardForGameSuccessResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("key"); w.Value(this.Key);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardForGameErrorResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("code"); w.Value(this.Code);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardForGameErrorResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("errorCode"); w.Value(this.ErrorCode);
            w.Name("message"); w.Value(this.Message);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardForGameResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("code"); w.Value(this.Code);
            w.Name("key"); w.Value(this.Key);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShareSendMessageMessage : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("message"); w.Value(this.Message);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ViralGetCampaignOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("campaignId"); w.Value(this.CampaignId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ViralGetCampaignResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("campaignId"); w.Value(this.CampaignId);
            w.Name("benefitName"); w.Value(this.BenefitName);
            w.Name("display"); AITJson.Obj(w, this.Display);
            w.Name("active"); w.Value(this.Active);
            w.Name("campaignStatus"); AITJsonCodec.WE_ViralGetCampaignResultCampaignStatus(w, this.CampaignStatus);
            w.Name("participationAvailable"); w.Value(this.ParticipationAvailable);
            w.Name("participationUnavailableReason"); AITJsonCodec.WE_ViralGetCampaignResultParticipationUnavailableReason(w, this.ParticipationUnavailableReason);
            w.Name("rules"); AITJson.Arr<ViralGetCampaignResultRule>(w, this.Rules, (w1, x1) => AITJson.Obj(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ViralGetCampaignResultDisplay : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("title"); w.Value(this.Title);
            w.Name("benefitText"); w.Value(this.BenefitText);
            w.Name("conditionText"); w.Value(this.ConditionText);
            w.Name("ctaText"); w.Value(this.CtaText);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ViralGetCampaignResultRule : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("ruleId"); w.Value(this.RuleId);
            w.Name("ruleKey"); w.Value(this.RuleKey);
            w.Name("revision"); w.Value(this.Revision);
            w.Name("title"); w.Value(this.Title);
            w.Name("currentValue"); w.Value(this.CurrentValue);
            w.Name("targetValue"); w.Value(this.TargetValue);
            w.Name("status"); AITJsonCodec.WE_ViralGetCampaignResultRuleStatus(w, this.Status);
            w.Name("achievedAt"); w.Value(this.AchievedAt);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ViralShareOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("campaignId"); w.Value(this.CampaignId);
            w.Name("title"); w.Value(this.Title);
            w.Name("description"); w.Value(this.Description);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class TrackViralParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("eventName"); w.Value(this.EventName);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShareMessage : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("message"); w.Value(this.Message);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactsViralEventData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            if (this.RewardAmountSpecified) { w.Name("rewardAmount"); w.Value(this.RewardAmount); }
            w.Name("rewardUnit"); w.Value(this.RewardUnit);
            w.Name("closeReason"); AITJsonCodec.WE_ContactsViralEventDataCloseReason(w, this.CloseReason);
            w.Name("sentRewardAmount"); w.Value(this.SentRewardAmount);
            w.Name("sendableRewardsCount"); w.Value(this.SendableRewardsCount);
            w.Name("sentRewardsCount"); w.Value(this.SentRewardsCount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactsViralEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactsViralSuccessEventData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("closeReason"); AITJsonCodec.WE_ContactsViralSuccessEventDataCloseReason(w, this.CloseReason);
            w.Name("sentRewardAmount"); w.Value(this.SentRewardAmount);
            w.Name("sendableRewardsCount"); w.Value(this.SendableRewardsCount);
            w.Name("sentRewardsCount"); w.Value(this.SentRewardsCount);
            w.Name("rewardUnit"); w.Value(this.RewardUnit);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactsViralParamsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("moduleId"); w.Value(this.ModuleId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactsViralParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class FetchAlbumPhotosOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("base64"); w.Value(this.Base64);
            w.Name("maxCount"); w.Value(this.MaxCount);
            w.Name("maxWidth"); w.Value(this.MaxWidth);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OpenCameraOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("base64"); w.Value(this.Base64);
            w.Name("maxWidth"); w.Value(this.MaxWidth);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class FetchAlbumItemsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("types"); AITJson.Arr<AlbumItemType>(w, this.Types, (w1, x1) => AITJsonCodec.WE_AlbumItemType(w1, x1));
            w.Name("maxCount"); w.Value(this.MaxCount);
            w.Name("maxWidth"); w.Value(this.MaxWidth);
            w.Name("base64"); w.Value(this.Base64);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OpenPDFViewerParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("data"); w.Value(this.Data);
            w.Name("filename"); w.Value(this.Filename);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SaveBase64DataParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("data"); w.Value(this.Data);
            w.Name("fileName"); w.Value(this.FileName);
            w.Name("mimeType"); w.Value(this.MimeType);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class DeviceGetPhotosOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("base64"); w.Value(this.Base64);
            w.Name("maxCount"); w.Value(this.MaxCount);
            w.Name("maxWidth"); w.Value(this.MaxWidth);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class FetchContactsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("size"); w.Value(this.Size);
            w.Name("offset"); w.Value(this.Offset);
            w.Name("query"); AITJson.Obj(w, this.Query);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class FetchContactsOptionsQuery : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("contains"); w.Value(this.Contains);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("result"); AITJson.Arr<ContactEntity>(w, this.Result, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("nextOffset"); w.Value(this.NextOffset);
            w.Name("done"); w.Value(this.Done);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class DeviceOpenCameraOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("base64"); w.Value(this.Base64);
            w.Name("maxWidth"); w.Value(this.MaxWidth);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class DeviceGetAlbumItemsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("types"); AITJson.Arr<AlbumItemType>(w, this.Types, (w1, x1) => AITJsonCodec.WE_AlbumItemType(w1, x1));
            w.Name("maxCount"); w.Value(this.MaxCount);
            w.Name("maxWidth"); w.Value(this.MaxWidth);
            w.Name("base64"); w.Value(this.Base64);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ScreenSetAwakeModeResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("enabled"); w.Value(this.Enabled);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ScreenSetSecureResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("enabled"); w.Value(this.Enabled);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetScreenAwakeModeResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("enabled"); w.Value(this.Enabled);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetSecureScreenResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("enabled"); w.Value(this.Enabled);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetDeviceOrientationOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); AITJsonCodec.WE_SetDeviceOrientationOptionsType(w, this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetIosSwipeGestureEnabledOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("isEnabled"); w.Value(this.IsEnabled);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetScreenAwakeModeOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("enabled"); w.Value(this.Enabled);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetSecureScreenOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("enabled"); w.Value(this.Enabled);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class EventLogParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("log_type"); w.Value(this.Log_type);
            w.Name("log_name"); w.Value(this.Log_name);
            w.Name("params"); AITJson.Dict<string, object>(w, this.Params, null, (w1, x1) => AITJson.Any(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AppsInTossSignTossCertParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("txId"); w.Value(this.TxId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OnVisibilityChangedByTransparentServiceWebEventParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OnVisibilityChangedByTransparentServiceWebEventParamsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("callbackId"); w.Value(this.CallbackId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GoogleAdMobLoadAppsInTossAdMobArgs : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class LoadAdMobOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            w.Name("referrer"); w.Value(this.Referrer);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class Error : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); w.Value(this.Name);
            w.Name("message"); w.Value(this.Message);
            w.Name("stack"); w.Value(this.Stack);
            w.Name("cause"); AITJson.Any(w, this.Cause);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GoogleAdMobShowAppsInTossAdMobArgs : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShowAdMobOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IsAdMobLoadedOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class InitializeOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("callbacks"); AITJson.Obj(w, this.Callbacks);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class InitializeOptionsCallbacks : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class TossAdsAttachOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("theme"); w.Value(this.Theme);
            w.Name("padding"); w.Value(this.Padding);
            w.Name("callbacks"); AITJson.Obj(w, this.Callbacks);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class BannerSlotCallbacks : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class BannerSlotEventPayload : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("slotId"); w.Value(this.SlotId);
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            w.Name("adMetadata"); AITJson.Obj(w, this.AdMetadata);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class BannerSlotEventPayloadAdMetadata : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("creativeId"); w.Value(this.CreativeId);
            w.Name("requestId"); w.Value(this.RequestId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class BannerSlotErrorPayload : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("slotId"); w.Value(this.SlotId);
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            w.Name("adMetadata"); AITJson.Dict<string, object>(w, this.AdMetadata, null, (w1, x1) => AITJson.Any(w1, x1));
            w.Name("error"); AITJson.Obj(w, this.Error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class BannerSlotErrorPayloadError : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("code"); w.Value(this.Code);
            w.Name("message"); w.Value(this.Message);
            w.Name("domain"); w.Value(this.Domain);
            w.Name("mediationId"); w.Value(this.MediationId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class BannerSlotCallbacksOnNoFillParam : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("slotId"); w.Value(this.SlotId);
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            w.Name("adMetadata"); AITJson.Dict<string, object>(w, this.AdMetadata, null, (w1, x1) => AITJson.Any(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class TossAdsAttachBannerOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("theme"); w.Value(this.Theme);
            w.Name("tone"); w.Value(this.Tone);
            w.Name("variant"); w.Value(this.Variant);
            w.Name("callbacks"); AITJson.Obj(w, this.Callbacks);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AttachBannerResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShowFullScreenAdEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShowFullScreenAdEventData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("unitType"); w.Value(this.UnitType);
            w.Name("unitAmount"); w.Value(this.UnitAmount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class LoadFullScreenAdEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class LoadFullScreenAdOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShowFullScreenAdOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SafeAreaInsets : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("top"); w.Value(this.Top);
            w.Name("left"); w.Value(this.Left);
            w.Name("right"); w.Value(this.Right);
            w.Name("bottom"); w.Value(this.Bottom);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IsMinVersionSupportedMinVersions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("android"); w.Value(this.Android);
            w.Name("ios"); w.Value(this.Ios);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RewardFromContactsViralEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactsViralSuccessEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardErrorResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("code"); w.Value(this.Code);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardErrorResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("errorCode"); w.Value(this.ErrorCode);
            w.Name("message"); w.Value(this.Message);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("params"); AITJson.Obj(w, this.Params);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardParamsParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("promotionCode"); w.Value(this.PromotionCode);
            w.Name("amount"); w.Value(this.Amount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantPromotionRewardResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("key"); w.Value(this.Key);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestNotificationAgreementOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("options"); AITJson.Obj(w, this.Options);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestNotificationAgreementOptionsOnEventParam : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); AITJsonCodec.WE_RequestNotificationAgreementOptionsOnEventParamType(w, this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RequestNotificationAgreementOptionsOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("templateCode"); w.Value(this.TemplateCode);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class MigrationGetOriginStorageResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("previous"); AITJson.Obj(w, this.Previous);
            w.Name("current"); AITJson.Obj(w, this.Current);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AdMobLoadResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adGroupId"); w.Value(this.AdGroupId);
            w.Name("adUnitId"); w.Value(this.AdUnitId);
            w.Name("responseInfo"); AITJson.Obj(w, this.ResponseInfo);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ResponseInfo : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adNetworkInfoArray"); AITJson.Arr<AdNetworkResponseInfo>(w, this.AdNetworkInfoArray, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("loadedAdNetworkInfo"); AITJson.Obj(w, this.LoadedAdNetworkInfo);
            w.Name("responseId"); w.Value(this.ResponseId);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AdNetworkResponseInfo : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("adSourceId"); w.Value(this.AdSourceId);
            w.Name("adSourceName"); w.Value(this.AdSourceName);
            w.Name("adSourceInstanceId"); w.Value(this.AdSourceInstanceId);
            w.Name("adSourceInstanceName"); w.Value(this.AdSourceInstanceName);
            w.Name("adNetworkClassName"); w.Value(this.AdNetworkClassName);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShowAdMobEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ShowAdMobEventData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("unitType"); w.Value(this.UnitType);
            w.Name("unitAmount"); w.Value(this.UnitAmount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class Offer : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("offerId"); w.Value(this.OfferId);
            w.Name("period"); w.Value(this.Period);
            w.Name("displayAmount"); w.Value(this.DisplayAmount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapProductListItem : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("sku"); w.Value(this.Sku);
            w.Name("displayAmount"); w.Value(this.DisplayAmount);
            w.Name("displayName"); w.Value(this.DisplayName);
            w.Name("iconUrl"); w.Value(this.IconUrl);
            w.Name("description"); w.Value(this.Description);
            w.Name("type"); w.Value(this.Type);
            w.Name("renewalCycle"); AITJsonCodec.WE_IapProductListItemRenewalCycle(w, this.RenewalCycle);
            w.Name("offers"); AITJson.Arr<Offer>(w, this.Offers, (w1, x1) => AITJson.Obj(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GameCenterGameProfileResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("statusCode"); w.Value(this.StatusCode);
            w.Name("nickname"); w.Value(this.Nickname);
            w.Name("profileImageUri"); w.Value(this.ProfileImageUri);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AdUserEarnedReward : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AdUserEarnedRewardData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("unitType"); w.Value(this.UnitType);
            w.Name("unitAmount"); w.Value(this.UnitAmount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AddAccessoryButtonOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("id"); w.Value(this.Id);
            w.Name("title"); w.Value(this.Title);
            w.Name("icon"); AITJson.Obj(w, this.Icon);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AddAccessoryButtonOptionsIcon : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); w.Value(this.Name);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AlbumItemResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("id"); w.Value(this.Id);
            w.Name("dataUri"); w.Value(this.DataUri);
            w.Name("type"); AITJsonCodec.WE_AlbumItemType(w, this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class AppsInTossGlobals : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("deploymentId"); w.Value(this.DeploymentId);
            w.Name("brandDisplayName"); w.Value(this.BrandDisplayName);
            w.Name("brandIcon"); w.Value(this.BrandIcon);
            w.Name("brandPrimaryColor"); w.Value(this.BrandPrimaryColor);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CompletedOrRefundedOrdersResultOrder : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            w.Name("sku"); w.Value(this.Sku);
            w.Name("status"); AITJsonCodec.WE_CompletedOrRefundedOrdersResultOrderStatus(w, this.Status);
            w.Name("date"); w.Value(this.Date);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CompletedOrRefundedOrdersResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("hasNext"); w.Value(this.HasNext);
            w.Name("nextKey"); w.Value(this.NextKey);
            w.Name("orders"); AITJson.Arr<CompletedOrRefundedOrdersResultOrder>(w, this.Orders, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ContactEntity : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); w.Value(this.Name);
            w.Name("phoneNumber"); w.Value(this.PhoneNumber);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class RewardFromContactsViralEventData : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("rewardAmount"); w.Value(this.RewardAmount);
            w.Name("rewardUnit"); w.Value(this.RewardUnit);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class CreateLinkParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("path"); w.Value(this.Path);
            w.Name("ogImageUrl"); w.Value(this.OgImageUrl);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class DeclaredAgeRange : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("status"); w.Value(this.Status);
            w.Name("lowerBound"); w.Value(this.LowerBound);
            w.Name("upperBound"); w.Value(this.UpperBound);
            w.Name("ageRangeDeclaration"); w.Value(this.AgeRangeDeclaration);
            w.Name("activeParentalControls"); AITJson.Arr<string>(w, this.ActiveParentalControls, (w1, x1) => w1.Value(x1));
            w.Name("isEligibleForAgeFeatures"); w.Value(this.IsEligibleForAgeFeatures);
            w.Name("errorCode"); w.Value(this.ErrorCode);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class GrantRewardParams : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("promotionCode"); w.Value(this.PromotionCode);
            w.Name("amount"); w.Value(this.Amount);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class HapticFeedbackOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); AITJsonCodec.WE_HapticFeedbackType(w, this.Type);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapCreateOneTimePurchaseOrderResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("orderId"); w.Value(this.OrderId);
            w.Name("displayName"); w.Value(this.DisplayName);
            w.Name("displayAmount"); w.Value(this.DisplayAmount);
            w.Name("amount"); w.Value(this.Amount);
            w.Name("currency"); w.Value(this.Currency);
            w.Name("fraction"); w.Value(this.Fraction);
            w.Name("miniAppIconUrl"); w.Value(this.MiniAppIconUrl);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapSubscriptionInfoResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("subscription"); AITJson.Obj(w, this.Subscription);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class IapSubscriptionInfoResult : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("status"); w.Value(this.Status);
            w.Name("catalogId"); w.Value(this.CatalogId);
            w.Name("expiresAt"); w.Value(this.ExpiresAt);
            w.Name("isAutoRenew"); w.Value(this.IsAutoRenew);
            w.Name("gracePeriodExpiresAt"); w.Value(this.GracePeriodExpiresAt);
            w.Name("isAccessible"); w.Value(this.IsAccessible);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class ImageResponse : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("id"); w.Value(this.Id);
            w.Name("dataUri"); w.Value(this.DataUri);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class LoadAdMobEvent : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("type"); w.Value(this.Type);
            w.Name("data"); AITJson.Obj(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class Location : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("accessLocation"); w.Value(this.AccessLocation);
            w.Name("timestamp"); w.Value(this.Timestamp);
            w.Name("coords"); AITJson.Obj(w, this.Coords);
            w.Name("error"); w.Value(this.error);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class LocationCoords : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("latitude"); w.Value(this.Latitude);
            w.Name("longitude"); w.Value(this.Longitude);
            w.Name("altitude"); w.Value(this.Altitude);
            w.Name("accuracy"); w.Value(this.Accuracy);
            w.Name("altitudeAccuracy"); w.Value(this.AltitudeAccuracy);
            w.Name("heading"); w.Value(this.Heading);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDump : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("origin"); w.Value(this.Origin);
            w.Name("localStorage"); AITJson.Dict<string, object>(w, this.LocalStorage, null, (w1, x1) => AITJson.Any(w1, x1));
            w.Name("indexedDB"); AITJson.Arr<OriginStorageDumpIndexedDB>(w, this.IndexedDB, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("opfs"); AITJson.Obj(w, this.Opfs);
            w.Name("errors"); AITJson.Arr<OriginStorageDumpError>(w, this.Errors, (w1, x1) => AITJson.Obj(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpIndexedDBObjectStoreIndex : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); w.Value(this.Name);
            w.Name("keyPath"); AITJson.Any(w, this.KeyPath);
            w.Name("multiEntry"); w.Value(this.MultiEntry);
            w.Name("unique"); w.Value(this.Unique);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpIndexedDBObjectStoreRecord : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("key"); AITJson.Any(w, this.Key);
            w.Name("primaryKey"); AITJson.Any(w, this.PrimaryKey);
            w.Name("value"); AITJson.Any(w, this.Value);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpIndexedDBObjectStore : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); w.Value(this.Name);
            w.Name("keyPath"); AITJson.Any(w, this.KeyPath);
            w.Name("autoIncrement"); w.Value(this.AutoIncrement);
            w.Name("indexes"); AITJson.Arr<OriginStorageDumpIndexedDBObjectStoreIndex>(w, this.Indexes, (w1, x1) => AITJson.Obj(w1, x1));
            w.Name("records"); AITJson.Arr<OriginStorageDumpIndexedDBObjectStoreRecord>(w, this.Records, (w1, x1) => AITJson.Obj(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpIndexedDB : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("name"); w.Value(this.Name);
            w.Name("version"); w.Value(this.Version);
            w.Name("objectStores"); AITJson.Arr<OriginStorageDumpIndexedDBObjectStore>(w, this.ObjectStores, (w1, x1) => AITJson.Obj(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpOpfsFile : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("path"); w.Value(this.Path);
            w.Name("type"); w.Value(this.Type);
            w.Name("lastModified"); w.Value(this.LastModified);
            w.Name("data"); AITJson.Bytes(w, this.Data);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpOpfs : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("directories"); AITJson.Arr<string>(w, this.Directories, (w1, x1) => w1.Value(x1));
            w.Name("files"); AITJson.Arr<OriginStorageDumpOpfsFile>(w, this.Files, (w1, x1) => AITJson.Obj(w1, x1));
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class OriginStorageDumpError : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("storage"); w.Value(this.Storage);
            w.Name("message"); w.Value(this.Message);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SetNavigationBarOptions : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.Name("withBackButton"); w.Value(this.WithBackButton);
            w.Name("withHomeButton"); w.Value(this.WithHomeButton);
            w.Name("withTitle"); w.Value(this.WithTitle);
            w.Name("transparentBackground"); w.Value(this.TransparentBackground);
            w.Name("backgroundColor"); w.Value(this.BackgroundColor);
            w.Name("theme"); AITJsonCodec.WE_NavigationBarTheme(w, this.Theme);
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class SafeAreaInsetsSubscribe__0 : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            AITJson.Ext(w, this._extensionData);
            w.EndObject();
        }
    }

    partial class number : IAITJsonWritable
    {
        void IAITJsonWritable.WriteJson(AITJsonWriter w)
        {
            w.BeginObject();
            w.EndObject();
        }
    }
}
