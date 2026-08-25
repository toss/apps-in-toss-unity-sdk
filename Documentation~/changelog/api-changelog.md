Apps in Toss Unity SDK가 노출하는 C# API 표면이 `@apps-in-toss/web-framework` 버전에 따라 어떻게 달라지는지 정리한 자동 생성 리포트입니다. 최신 버전(v3.1.0)이 최상단에 오도록 정렬되어 있습니다.

## v3.x

### v2.10.8 → v3.0.1

API 총 126개 · 추가 42 · 변경 64 · 제거 0

**추가된 API**

- `AIT.AnalyticsLog`
- `AIT.ClipboardGetText`
- `AIT.ClipboardSetText`
- `AIT.DeviceGetAlbumItems`
- `AIT.DeviceGetContacts`
- `AIT.DeviceGetLocation`
- `AIT.DeviceGetPhotos`
- `AIT.DeviceOpenCamera`
- `AIT.DeviceOpenURL`
- `AIT.DeviceSubscribeLocation`
- `AIT.DeviceTriggerHaptic`
- `AIT.EnvironmentGetNetworkStatus`
- `AIT.EnvironmentGetServerTime`
- `AIT.FileOpenPDFViewer`
- `AIT.FileSaveBase64`
- `AIT.GameGetUserProfile`
- `AIT.GameOpenLeaderboard`
- `AIT.GameSetLeaderboardScore`
- `AIT.NotificationRequestAgreement`
- `AIT.PromotionGrantReward`
- `AIT.PromotionOpenContactsInvite`
- `AIT.ReviewRequest`
- `AIT.SafeAreaGet`
- `AIT.SafeAreaSubscribe`
- `AIT.ScreenClose`
- `AIT.ScreenSetAwakeMode`
- `AIT.ScreenSetIosSwipeBack`
- `AIT.ScreenSetOrientation`
- `AIT.ScreenSetSecure`
- `AIT.ShareCreateLink`
- `AIT.ShareSendMessage`
- `AIT.TossAuthIsIntegrated`
- `AIT.TossAuthLogin`
- `AIT.TossAuthSign`
- `AIT.TossPayAuthorize`
- `AIT.TossPayAuthorizeSubscription`
- `AIT.TossPayCheckoutPayment`
- `AIT.TossPayRequestTossPayPaysBilling`
- `AIT.UserGetAnonymousKey`
- `AIT.UserGetConsentedData`
- `AIT.UserGetDeclaredAgeRange`
- `AIT.GetSafeAreaInsets`

**변경된 API**

- `AIT.AnalyticsClick`: `params: object → LoggerParams`
- `AIT.AnalyticsImpression`: `params: object → LoggerParams`
- `AIT.AnalyticsScreen`: `params: object → LoggerParams`
- `AIT.AppLogin`: `return: object → AppLoginResponse; isDeprecated: false → true`
- `AIT.AppsInTossSignTossCert`: `isDeprecated: false → true`
- `AIT.CheckoutPayment`: `isDeprecated: false → true`
- `AIT.CloseView`: `isDeprecated: false → true`
- `AIT.ContactsViral`: `isDeprecated: false → true`
- `AIT.EnvGetDeploymentId`: `isDeprecated: false → true`
- `AIT.EventLog`: `isDeprecated: false → true`
- `AIT.FetchAlbumItems`: `isDeprecated: false → true`
- `AIT.FetchAlbumPhotos`: `options: FetchAlbumPhotosOptions → FetchAlbumPhotosParams; isDeprecated: false → true`
- `AIT.FetchContacts`: `options: FetchContactsOptions → FetchContactsParams; return: ContactResult → FetchContactsResult; isDeprecated: false → true`
- `AIT.GenerateHapticFeedback`: `isDeprecated: false → true`
- `AIT.GetAnonymousKey`: `return: GetAnonymousKeySuccessResponseERRORundefined → GetAnonymousKeyResponseERRORundefined; isDeprecated: false → true`
- `AIT.GetClipboardText`: `isDeprecated: false → true`
- `AIT.GetConsentedUserData`: `isDeprecated: false → true`
- `AIT.GetCurrentLocation`: `isDeprecated: false → true`
- `AIT.GetDeclaredAgeRange`: `params: object → GetDeclaredAgeRangeParams; isDeprecated: false → true`
- `AIT.GetDeviceId`: `isDeprecated: false → true`
- `AIT.GetGameCenterGameProfile`: `isDeprecated: false → true`
- `AIT.GetGroupId`: `isDeprecated: false → true`
- `AIT.GetIsTossLoginIntegratedService`: `isDeprecated: false → true`
- `AIT.GetLocale`: `isDeprecated: false → true`
- `AIT.GetNetworkStatus`: `isDeprecated: false → true`
- `AIT.GetOperationalEnvironment`: `isDeprecated: false → true`
- `AIT.GetPlatformOS`: `isDeprecated: false → true`
- `AIT.GetSchemeUri`: `isDeprecated: false → true`
- `AIT.GetServerTime`: `isDeprecated: false → true`
- `AIT.GetTossAppVersion`: `isDeprecated: false → true`
- `AIT.GetTossShareLink`: `isDeprecated: false → true`
- `AIT.GetUserKeyForGame`: `return: string → GetAnonymousKeyResponseERRORundefined; isDeprecated: false → true`
- `AIT.GoogleAdMobIsAppsInTossAdMobLoaded`: `parameter added: options: GetCachedStatusAppsInTossAdmobParams; parameter removed: args_0`
- `AIT.GoogleAdMobLoadAppsInTossAdMob`: `parameter added: params: object; parameter removed: args`
- `AIT.GoogleAdMobShowAppsInTossAdMob`: `parameter added: params: object; parameter removed: args`
- `AIT.GrantPromotionReward`: `params: object → GrantPromotionRewardParams; isDeprecated: false → true`
- `AIT.GrantPromotionRewardForGame`: `params: object → GrantPromotionRewardParams; return: GrantPromotionRewardResult → GrantPromotionRewardResponseERRORerrorCode:stringmessage:stringundefined; isDeprecated: false → true`
- `AIT.IAPCompleteProductGrant`: `parameter added: args: object; parameter removed: args_0`
- `AIT.IAPGetPendingOrders`: `return: object → GetPendingOrdersResult`
- `AIT.IAPGetProductItemList`: `return: object → IapGetProductItemListResult`
- `AIT.IAPGetSubscriptionInfo`: `parameter added: args: object; parameter removed: args_0`
- `AIT.OpenCamera`: `options: OpenCameraOptions → OpenCameraParams; isDeprecated: false → true`
- `AIT.OpenGameCenterLeaderboard`: `isDeprecated: false → true`
- `AIT.OpenPDFViewer`: `isDeprecated: false → true`
- `AIT.OpenPermissionDialog`: `return: string → PermissionDialogResult`
- `AIT.OpenURL`: `isDeprecated: false → true`
- `AIT.PartnerAddAccessoryButton`: `parameter added: params: AddAccessoryButtonParams; parameter removed: args_0`
- `AIT.RequestNotificationAgreement`: `isDeprecated: false → true`
- `AIT.RequestReview`: `isDeprecated: false → true`
- `AIT.RequestTossPayPaysBilling`: `isDeprecated: false → true`
- `AIT.SafeAreaInsetsSubscribe`: `parameter added: args: object; parameter removed: __0`
- `AIT.SaveBase64Data`: `isDeprecated: false → true`
- `AIT.SetClipboardText`: `isDeprecated: false → true`
- `AIT.SetDeviceOrientation`: `options: object → SetDeviceOrientationOptions; isDeprecated: false → true`
- `AIT.SetIosSwipeGestureEnabled`: `options: object → SetIosSwipeGestureEnabledOptions; isDeprecated: false → true`
- `AIT.SetScreenAwakeMode`: `options: object → SetScreenAwakeModeOptions; isDeprecated: false → true`
- `AIT.SetSecureScreen`: `options: object → SetSecureScreenOptions; isDeprecated: false → true`
- `AIT.Share`: `isDeprecated: false → true`
- `AIT.StartUpdateLocation`: `isDeprecated: false → true`
- `AIT.StorageClearItems`: `parameter removed: args_0`
- `AIT.StorageGetItem`: `parameter added: key: string; parameter removed: args_0`
- `AIT.StorageRemoveItem`: `parameter added: key: string; parameter removed: args_0`
- `AIT.StorageSetItem`: `parameter added: key: string; parameter added: value: string; parameter removed: args_0; parameter removed: args_1`
- `AIT.SubmitGameCenterLeaderBoardScore`: `isDeprecated: false → true`

변경 없음: v3.0.5 → v3.1.0 · v3.0.4 → v3.0.5 · v3.0.3 → v3.0.4 · v3.0.2 → v3.0.3 · v3.0.1 → v3.0.2

## v2.x

### v2.10.0 → v2.10.1

API 총 84개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GetDeclaredAgeRange`

### v2.6.2 → v2.7.0

API 총 83개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GetConsentedUserData`

### v2.5.2 → v2.6.0

API 총 82개 · 추가 2 · 변경 0 · 제거 0

**추가된 API**

- `AIT.FetchAlbumItems`
- `AIT.OpenPDFViewer`

### v2.4.7 → v2.5.0

API 총 80개 · 추가 2 · 변경 0 · 제거 0

**추가된 API**

- `AIT.RequestNotificationAgreement`
- `AIT.RequestTossPayPaysBilling`

### v2.4.6 → v2.4.7

API 총 78개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.TossAdsAttach`: `isDeprecated: false → true`

### v2.4.4 → v2.4.5

API 총 78개 · 추가 1 · 변경 0 · 제거 1

**추가된 API**

- `AIT.GetAnonymousKey`

**제거된 API**

- `AIT.GetUserKey`

### v2.4.3 → v2.4.4

API 총 78개 · 추가 1 · 변경 1 · 제거 0

**추가된 API**

- `AIT.GetUserKey`

**변경된 API**

- `AIT.GetUserKeyForGame`: `return: GetUserKeyForGameSuccessResponseINVALID_CATEGORYERRORundefined → string`

### v2.3.0 → v2.4.0

API 총 77개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.RequestReview`

### v2.1.1 → v2.2.0

API 총 76개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.IAPGetSubscriptionInfo`

### v2.0.6 → v2.0.9

API 총 75개 · 추가 1 · 변경 1 · 제거 0

**추가된 API**

- `AIT.GrantPromotionReward`

**변경된 API**

- `AIT.GrantPromotionRewardForGame`: `return: GrantPromotionRewardForGameResult → GrantPromotionRewardResult`

### v2.0.1 → v2.0.2

API 총 74개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.GetTossShareLink`: `parameter added: ogImageUrl: string?`

### v1.14.1 → v2.0.0

API 총 74개 · 추가 1 · 변경 3 · 제거 0

**추가된 API**

- `AIT.GetGroupId`

**변경된 API**

- `AIT.CheckoutPayment`: `options: CheckoutPaymentOptions → object`
- `AIT.GetTossShareLink`: `parameter removed: ogImageUrl`
- `AIT.GrantPromotionRewardForGame`: `parameter added: params: object; parameter removed: options`

변경 없음: v2.10.7 → v2.10.8 · v2.10.6 → v2.10.7 · v2.10.5 → v2.10.6 · v2.10.4 → v2.10.5 · v2.10.3 → v2.10.4 · v2.10.2 → v2.10.3 · v2.10.1 → v2.10.2 · v2.9.3 → v2.10.0 · v2.9.2 → v2.9.3 · v2.9.1 → v2.9.2 · v2.9.0 → v2.9.1 · v2.8.0 → v2.9.0 · v2.7.1 → v2.8.0 · v2.7.0 → v2.7.1 · v2.6.1 → v2.6.2 · v2.6.0 → v2.6.1 · v2.5.1 → v2.5.2 · v2.5.0 → v2.5.1 · v2.4.5 → v2.4.6 · v2.4.2 → v2.4.3 · v2.4.1 → v2.4.2 · v2.4.0 → v2.4.1 · v2.2.0 → v2.3.0 · v2.1.0 → v2.1.1 · v2.0.9 → v2.1.0 · v2.0.5 → v2.0.6 · v2.0.4 → v2.0.5 · v2.0.3 → v2.0.4 · v2.0.2 → v2.0.3 · v2.0.0 → v2.0.1

## v1.x

### v1.13.0 → v1.14.0

API 총 73개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GraniteEventSubscribeHomeEvent`

### v1.11.2 → v1.12.0

API 총 72개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.IAPCreateSubscriptionPurchaseOrder`

### v1.10.1 → v1.11.0

API 총 71개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.TossAdsAttachBanner`

### v1.9.4 → v1.10.0

API 총 70개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.GoogleAdMobIsAppsInTossAdMobLoaded`: `args_0: object → IsAdMobLoadedOptions`

### v1.8.1 → v1.9.0

API 총 70개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GetServerTime`

### v1.7.1 → v1.8.0

API 총 69개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GoogleAdMobIsAppsInTossAdMobLoaded`

### v1.7.0 → v1.7.1

API 총 68개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.SafeAreaInsetsGet`: `return: object → SafeAreaInsets`

### v1.5.3 → v1.6.0

API 총 68개 · 추가 6 · 변경 0 · 제거 0

**추가된 API**

- `AIT.TossAdsAttach`
- `AIT.TossAdsDestroy`
- `AIT.TossAdsDestroyAll`
- `AIT.TossAdsInitialize`
- `AIT.LoadFullScreenAd`
- `AIT.ShowFullScreenAd`

### v1.5.2 → v1.5.3

API 총 62개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.GetTossShareLink`: `parameter added: ogImageUrl: string?`

### v1.5.0 → v1.5.2

API 총 62개 · 추가 0 · 변경 3 · 제거 0

**변경된 API**

- `AIT.AnalyticsClick`: `params: Dictionary<string, object> → object`
- `AIT.AnalyticsImpression`: `params: Dictionary<string, object> → object`
- `AIT.AnalyticsScreen`: `params: Dictionary<string, object> → object`

변경 없음: v1.14.0 → v1.14.1 · v1.12.0 → v1.13.0 · v1.11.1 → v1.11.2 · v1.11.0 → v1.11.1 · v1.10.0 → v1.10.1 · v1.9.3 → v1.9.4 · v1.9.2 → v1.9.3 · v1.9.1 → v1.9.2 · v1.9.0 → v1.9.1 · v1.8.0 → v1.8.1 · v1.6.2 → v1.7.0 · v1.6.1 → v1.6.2 · v1.6.0 → v1.6.1

## v3.1.0 API 카탈로그

### Authentication

- `AIT.TossAuthIsIntegrated`
- `AIT.TossAuthLogin`
- `AIT.TossAuthSign`
- `AIT.AppLogin`
- `AIT.GetIsTossLoginIntegratedService`

### Payment

- `AIT.TossPayAuthorize`
- `AIT.TossPayAuthorizeSubscription`
- `AIT.TossPayCheckoutPayment`
- `AIT.TossPayRequestTossPayPaysBilling`
- `AIT.CheckoutPayment`
- `AIT.RequestTossPayPaysBilling`

### IAP

- `AIT.IAPCompleteProductGrant`
- `AIT.IAPCreateOneTimePurchaseOrder`
- `AIT.IAPCreateSubscriptionPurchaseOrder`
- `AIT.IAPGetCompletedOrRefundedOrders`
- `AIT.IAPGetPendingOrders`
- `AIT.IAPGetProductItemList`
- `AIT.IAPGetSubscriptionInfo`

### SystemInfo

- `AIT.EnvironmentGetNetworkStatus`
- `AIT.EnvironmentGetServerTime`
- `AIT.UserGetAnonymousKey`
- `AIT.UserGetConsentedData`
- `AIT.UserGetDeclaredAgeRange`
- `AIT.GetAnonymousKey`
- `AIT.GetConsentedUserData`
- `AIT.GetDeclaredAgeRange`
- `AIT.GetDeviceId`
- `AIT.GetGroupId`
- `AIT.GetLocale`
- `AIT.GetNetworkStatus`
- `AIT.GetOperationalEnvironment`
- `AIT.GetPlatformOS`
- `AIT.GetSchemeUri`
- `AIT.GetServerTime`
- `AIT.GetTossAppVersion`

### Location

- `AIT.DeviceGetLocation`
- `AIT.DeviceSubscribeLocation`
- `AIT.GetCurrentLocation`
- `AIT.StartUpdateLocation`

### Permission

- `AIT.GetPermission`
- `AIT.OpenPermissionDialog`
- `AIT.RequestPermission`

### GameCenter

- `AIT.GameGetUserProfile`
- `AIT.GameOpenLeaderboard`
- `AIT.GameSetLeaderboardScore`
- `AIT.GetGameCenterGameProfile`
- `AIT.GetUserKeyForGame`
- `AIT.GrantPromotionRewardForGame`
- `AIT.OpenGameCenterLeaderboard`
- `AIT.SubmitGameCenterLeaderBoardScore`

### Share

- `AIT.DeviceGetContacts`
- `AIT.PromotionOpenContactsInvite`
- `AIT.ShareCreateLink`
- `AIT.ShareSendMessage`
- `AIT.ContactsViral`
- `AIT.FetchContacts`
- `AIT.GetTossShareLink`
- `AIT.Share`

### Media

- `AIT.DeviceGetAlbumItems`
- `AIT.DeviceGetPhotos`
- `AIT.DeviceOpenCamera`
- `AIT.FileOpenPDFViewer`
- `AIT.FileSaveBase64`
- `AIT.FetchAlbumItems`
- `AIT.FetchAlbumPhotos`
- `AIT.OpenCamera`
- `AIT.OpenPDFViewer`
- `AIT.SaveBase64Data`

### Clipboard

- `AIT.ClipboardGetText`
- `AIT.ClipboardSetText`
- `AIT.GetClipboardText`
- `AIT.SetClipboardText`

### Device

- `AIT.DeviceTriggerHaptic`
- `AIT.ScreenSetAwakeMode`
- `AIT.ScreenSetIosSwipeBack`
- `AIT.ScreenSetOrientation`
- `AIT.ScreenSetSecure`
- `AIT.GenerateHapticFeedback`
- `AIT.SetDeviceOrientation`
- `AIT.SetIosSwipeGestureEnabled`
- `AIT.SetScreenAwakeMode`
- `AIT.SetSecureScreen`

### Navigation

- `AIT.DeviceOpenURL`
- `AIT.ScreenClose`
- `AIT.CloseView`
- `AIT.OpenURL`

### Events

- `AIT.EventLog`

### Analytics

- `AIT.AnalyticsClick`
- `AIT.AnalyticsImpression`
- `AIT.AnalyticsLog`
- `AIT.AnalyticsScreen`

### Certificate

- `AIT.AppsInTossSignTossCert`

### Visibility

- `AIT.OnVisibilityChangedByTransparentServiceWeb`

### Storage

- `AIT.StorageClearItems`
- `AIT.StorageGetItem`
- `AIT.StorageRemoveItem`
- `AIT.StorageSetItem`

### Advertising

- `AIT.GoogleAdMobIsAppsInTossAdMobLoaded`
- `AIT.GoogleAdMobLoadAppsInTossAdMob`
- `AIT.GoogleAdMobShowAppsInTossAdMob`
- `AIT.TossAdsAttach`
- `AIT.TossAdsAttachBanner`
- `AIT.TossAdsDestroy`
- `AIT.TossAdsDestroyAll`
- `AIT.TossAdsInitialize`
- `AIT.LoadFullScreenAd`
- `AIT.ShowFullScreenAd`

### SafeArea

- `AIT.SafeAreaGet`
- `AIT.SafeAreaInsetsGet`
- `AIT.SafeAreaInsetsSubscribe`
- `AIT.SafeAreaSubscribe`
- `AIT.GetSafeAreaInsets`

### Partner

- `AIT.PartnerAddAccessoryButton`
- `AIT.PartnerRemoveAccessoryButton`

### AppEvents

- `AIT.GraniteEventSubscribeBackEvent`
- `AIT.GraniteEventSubscribeHomeEvent`
- `AIT.TdsEventSubscribeNavigationAccessoryEvent`

### Environment

- `AIT.EnvGetDeploymentId`
- `AIT.GetAppsInTossGlobals`
- `AIT.IsMinVersionSupported`

### Notification

- `AIT.NotificationRequestAgreement`
- `AIT.RequestNotificationAgreement`

### Promotion

- `AIT.PromotionGrantReward`
- `AIT.GrantPromotionReward`

### Review

- `AIT.ReviewRequest`
- `AIT.RequestReview`
