Apps in Toss Unity SDK가 노출하는 C# API 표면이 `@apps-in-toss/web-framework` 버전에 따라 어떻게 달라지는지 정리한 자동 생성 리포트입니다. 최신 버전(v3.9.0)이 최상단에 오도록 정렬되어 있습니다.

## v3.x

### v3.8.0 → v3.9.0

API 총 229개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.IAPGetCompletedOrRefundedOrders`: `parameter added: params: GetCompletedOrRefundedOrdersParams?`

### v3.5.0 → v3.6.0

API 총 229개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.UserCreateAnonymousKeyAuthCode`

### v3.4.1 → v3.5.0

API 총 228개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.NavigationBarSetOptions`

### v3.4.0 → v3.4.1

API 총 227개 · 추가 0 · 변경 2 · 제거 0

**변경된 API**

- `AIT.PromotionOpenContactsInvite`: `isDeprecated: false → true`
- `AIT.PromotionOpenContactsInviteIsSupported`: `isDeprecated: false → true`

### v3.3.0 → v3.4.0

API 총 227개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.ViralTrack`

### v3.2.0 → v3.3.0

API 총 226개 · 추가 2 · 변경 0 · 제거 0

**추가된 API**

- `AIT.ViralGetCampaign`
- `AIT.ViralShare`

### v3.1.1 → v3.2.0

API 총 224개 · 추가 5 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GameSubscribeInfoOverlayHidden`
- `AIT.StorageGetItems`
- `AIT.StorageGetItemsIsSupported`
- `AIT.StorageSetItems`
- `AIT.StorageSetItemsIsSupported`

### v3.0.5 → v3.1.0

API 총 219개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.MigrationGetOriginStorage`

변경 없음: v3.7.0 → v3.8.0 · v3.6.0 → v3.7.0 · v3.1.0 → v3.1.1 · v3.0.4 → v3.0.5 · v3.0.3 → v3.0.4 · v3.0.2 → v3.0.3 · v3.0.1 → v3.0.2

## v3.9.0 API 카탈로그

### Authentication

- `AIT.TossAuthIsIntegrated`
- `AIT.TossAuthIsIntegratedIsSupported`
- `AIT.TossAuthLogin`
- `AIT.TossAuthSign`
- `AIT.TossAuthSignIsSupported`
- `AIT.AppLogin`
- `AIT.GetIsTossLoginIntegratedService`
- `AIT.GetIsTossLoginIntegratedServiceIsSupported`

### User

- `AIT.UserCreateAnonymousKeyAuthCode`
- `AIT.UserGetAnonymousKey`
- `AIT.UserGetAnonymousKeyIsSupported`
- `AIT.UserGetConsentedData`
- `AIT.UserGetConsentedDataIsSupported`
- `AIT.UserGetDeclaredAgeRange`
- `AIT.UserGetDeclaredAgeRangeIsSupported`
- `AIT.GetAnonymousKey`
- `AIT.GetAnonymousKeyIsSupported`
- `AIT.GetConsentedUserData`
- `AIT.GetConsentedUserDataIsSupported`
- `AIT.GetDeclaredAgeRange`
- `AIT.GetDeclaredAgeRangeIsSupported`

### Payment

- `AIT.TossPayAuthorize`
- `AIT.TossPayAuthorizeSubscription`
- `AIT.TossPayAuthorizeSubscriptionIsSupported`
- `AIT.TossPayCheckoutPayment`
- `AIT.TossPayRequestTossPayPaysBilling`
- `AIT.TossPayRequestTossPayPaysBillingIsSupported`
- `AIT.CheckoutPayment`
- `AIT.RequestTossPayPaysBilling`
- `AIT.RequestTossPayPaysBillingIsSupported`

### IAP

- `AIT.IAPCompleteProductGrant`
- `AIT.IAPCompleteProductGrantIsSupported`
- `AIT.IAPCreateOneTimePurchaseOrder`
- `AIT.IAPCreateOneTimePurchaseOrderIsSupported`
- `AIT.IAPCreateSubscriptionPurchaseOrder`
- `AIT.IAPCreateSubscriptionPurchaseOrderIsSupported`
- `AIT.IAPGetCompletedOrRefundedOrders`
- `AIT.IAPGetCompletedOrRefundedOrdersIsSupported`
- `AIT.IAPGetPendingOrders`
- `AIT.IAPGetPendingOrdersIsSupported`
- `AIT.IAPGetProductItemList`
- `AIT.IAPGetProductItemListIsSupported`
- `AIT.IAPGetSubscriptionInfo`
- `AIT.IAPGetSubscriptionInfoIsSupported`

### SystemInfo

- `AIT.GetDeviceId`
- `AIT.GetGroupId`
- `AIT.GetLocale`
- `AIT.GetNetworkStatus`
- `AIT.GetOperationalEnvironment`
- `AIT.GetPlatformOS`
- `AIT.GetSchemeUri`
- `AIT.GetServerTime`
- `AIT.GetServerTimeIsSupported`
- `AIT.GetTossAppVersion`

### Location

- `AIT.GetCurrentLocation`
- `AIT.GetCurrentLocationGetPermission`
- `AIT.GetCurrentLocationOpenPermissionDialog`
- `AIT.StartUpdateLocation`
- `AIT.StartUpdateLocationGetPermission`
- `AIT.StartUpdateLocationOpenPermissionDialog`

### Permission

- `AIT.GetPermission`
- `AIT.OpenPermissionDialog`
- `AIT.RequestPermission`

### GameCenter

- `AIT.GameGetUserProfile`
- `AIT.GameGetUserProfileIsSupported`
- `AIT.GameOpenLeaderboard`
- `AIT.GameOpenLeaderboardIsSupported`
- `AIT.GameSetLeaderboardScore`
- `AIT.GameSetLeaderboardScoreIsSupported`
- `AIT.GameSubscribeInfoOverlayHidden`
- `AIT.GetGameCenterGameProfile`
- `AIT.GetGameCenterGameProfileIsSupported`
- `AIT.GetUserKeyForGame`
- `AIT.GetUserKeyForGameIsSupported`
- `AIT.GrantPromotionRewardForGame`
- `AIT.GrantPromotionRewardForGameIsSupported`
- `AIT.OpenGameCenterLeaderboard`
- `AIT.OpenGameCenterLeaderboardIsSupported`
- `AIT.SubmitGameCenterLeaderBoardScore`
- `AIT.SubmitGameCenterLeaderBoardScoreIsSupported`

### Share

- `AIT.ShareCreateLink`
- `AIT.ShareSendMessage`
- `AIT.ViralGetCampaign`
- `AIT.ViralShare`
- `AIT.ViralTrack`
- `AIT.ContactsViral`
- `AIT.ContactsViralIsSupported`
- `AIT.FetchContacts`
- `AIT.FetchContactsGetPermission`
- `AIT.FetchContactsOpenPermissionDialog`
- `AIT.GetTossShareLink`
- `AIT.Share`

### Media

- `AIT.FileOpenPDFViewer`
- `AIT.FileOpenPDFViewerIsSupported`
- `AIT.FileSaveBase64`
- `AIT.FileSaveBase64IsSupported`
- `AIT.FetchAlbumItems`
- `AIT.FetchAlbumItemsIsSupported`
- `AIT.FetchAlbumPhotos`
- `AIT.FetchAlbumPhotosGetPermission`
- `AIT.FetchAlbumPhotosOpenPermissionDialog`
- `AIT.OpenCamera`
- `AIT.OpenCameraGetPermission`
- `AIT.OpenCameraOpenPermissionDialog`
- `AIT.OpenPDFViewer`
- `AIT.OpenPDFViewerIsSupported`
- `AIT.SaveBase64Data`
- `AIT.SaveBase64DataIsSupported`

### Clipboard

- `AIT.ClipboardGetText`
- `AIT.ClipboardGetTextGetPermission`
- `AIT.ClipboardGetTextOpenPermissionDialog`
- `AIT.ClipboardSetText`
- `AIT.ClipboardSetTextGetPermission`
- `AIT.ClipboardSetTextOpenPermissionDialog`
- `AIT.GetClipboardText`
- `AIT.GetClipboardTextGetPermission`
- `AIT.GetClipboardTextOpenPermissionDialog`
- `AIT.SetClipboardText`
- `AIT.SetClipboardTextGetPermission`
- `AIT.SetClipboardTextOpenPermissionDialog`

### Device

- `AIT.DeviceGetAlbumItems`
- `AIT.DeviceGetAlbumItemsIsSupported`
- `AIT.DeviceGetContacts`
- `AIT.DeviceGetContactsGetPermission`
- `AIT.DeviceGetContactsOpenPermissionDialog`
- `AIT.DeviceGetLocation`
- `AIT.DeviceGetLocationGetPermission`
- `AIT.DeviceGetLocationOpenPermissionDialog`
- `AIT.DeviceGetPhotos`
- `AIT.DeviceGetPhotosGetPermission`
- `AIT.DeviceGetPhotosOpenPermissionDialog`
- `AIT.DeviceLocale`
- `AIT.DeviceOpenCamera`
- `AIT.DeviceOpenCameraGetPermission`
- `AIT.DeviceOpenCameraOpenPermissionDialog`
- `AIT.DeviceOpenURL`
- `AIT.DeviceOs`
- `AIT.DeviceSubscribeLocation`
- `AIT.DeviceSubscribeLocationGetPermission`
- `AIT.DeviceSubscribeLocationOpenPermissionDialog`
- `AIT.DeviceTriggerHaptic`
- `AIT.ScreenClose`
- `AIT.ScreenSetAwakeMode`
- `AIT.ScreenSetIosSwipeBack`
- `AIT.ScreenSetOrientation`
- `AIT.ScreenSetOrientationIsSupported`
- `AIT.ScreenSetSecure`
- `AIT.GenerateHapticFeedback`
- `AIT.SetDeviceOrientation`
- `AIT.SetDeviceOrientationIsSupported`
- `AIT.SetIosSwipeGestureEnabled`
- `AIT.SetScreenAwakeMode`
- `AIT.SetSecureScreen`

### Navigation

- `AIT.NavigationBarSetOptions`
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
- `AIT.AppsInTossSignTossCertIsSupported`

### Visibility

- `AIT.OnVisibilityChangedByTransparentServiceWeb`

### Storage

- `AIT.StorageClearItems`
- `AIT.StorageGetItem`
- `AIT.StorageGetItems`
- `AIT.StorageGetItemsIsSupported`
- `AIT.StorageRemoveItem`
- `AIT.StorageSetItem`
- `AIT.StorageSetItems`
- `AIT.StorageSetItemsIsSupported`

### Advertising

- `AIT.GoogleAdMobIsAppsInTossAdMobLoaded`
- `AIT.GoogleAdMobIsAppsInTossAdMobLoadedIsSupported`
- `AIT.GoogleAdMobLoadAppsInTossAdMob`
- `AIT.GoogleAdMobLoadAppsInTossAdMobIsSupported`
- `AIT.GoogleAdMobShowAppsInTossAdMob`
- `AIT.GoogleAdMobShowAppsInTossAdMobIsSupported`
- `AIT.TossAdsAttach`
- `AIT.TossAdsAttachBanner`
- `AIT.TossAdsAttachBannerIsSupported`
- `AIT.TossAdsAttachIsSupported`
- `AIT.TossAdsDestroy`
- `AIT.TossAdsDestroyAll`
- `AIT.TossAdsDestroyAllIsSupported`
- `AIT.TossAdsDestroyIsSupported`
- `AIT.TossAdsInitialize`
- `AIT.TossAdsInitializeIsSupported`
- `AIT.LoadFullScreenAd`
- `AIT.LoadFullScreenAdIsSupported`
- `AIT.ShowFullScreenAd`
- `AIT.ShowFullScreenAdIsSupported`

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

- `AIT.EnvironmentDeploymentId`
- `AIT.EnvironmentDeviceId`
- `AIT.EnvironmentEnvironment`
- `AIT.EnvironmentGetNetworkStatus`
- `AIT.EnvironmentGetServerTime`
- `AIT.EnvironmentGetServerTimeIsSupported`
- `AIT.EnvironmentGroupId`
- `AIT.EnvironmentInitialURL`
- `AIT.EnvironmentTossAppVersion`
- `AIT.EnvGetDeploymentId`
- `AIT.GetAppsInTossGlobals`
- `AIT.IsMinVersionSupported`

### Promotion

- `AIT.PromotionGrantReward`
- `AIT.PromotionGrantRewardIsSupported`
- `AIT.PromotionOpenContactsInvite`
- `AIT.PromotionOpenContactsInviteIsSupported`
- `AIT.GrantPromotionReward`
- `AIT.GrantPromotionRewardIsSupported`

### Notification

- `AIT.NotificationRequestAgreement`
- `AIT.NotificationRequestAgreementIsSupported`
- `AIT.RequestNotificationAgreement`
- `AIT.RequestNotificationAgreementIsSupported`

### Review

- `AIT.ReviewRequest`
- `AIT.ReviewRequestIsSupported`
- `AIT.RequestReview`
- `AIT.RequestReviewIsSupported`

### Migration

- `AIT.MigrationGetOriginStorage`
