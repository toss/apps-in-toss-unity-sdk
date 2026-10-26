Apps in Toss Unity SDK가 노출하는 C# API 표면이 `@apps-in-toss/web-framework` 버전에 따라 어떻게 달라지는지 정리한 자동 생성 리포트입니다. 최신 버전(v3.9.0)이 최상단에 오도록 정렬되어 있습니다.

## v3.x

### v3.8.0 → v3.9.0

API 총 134개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.IAPGetCompletedOrRefundedOrders`: `parameter added: params: GetCompletedOrRefundedOrdersParams?`

### v3.5.0 → v3.6.0

API 총 134개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.UserCreateAnonymousKeyAuthCode`

### v3.4.1 → v3.5.0

API 총 133개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.NavigationBarSetOptions`

### v3.4.0 → v3.4.1

API 총 132개 · 추가 0 · 변경 1 · 제거 0

**변경된 API**

- `AIT.PromotionOpenContactsInvite`: `isDeprecated: false → true`

### v3.3.0 → v3.4.0

API 총 132개 · 추가 1 · 변경 0 · 제거 0

**추가된 API**

- `AIT.ViralTrack`

### v3.2.0 → v3.3.0

API 총 131개 · 추가 2 · 변경 0 · 제거 0

**추가된 API**

- `AIT.ViralGetCampaign`
- `AIT.ViralShare`

### v3.1.1 → v3.2.0

API 총 129개 · 추가 3 · 변경 0 · 제거 0

**추가된 API**

- `AIT.GameSubscribeInfoOverlayHidden`
- `AIT.StorageGetItems`
- `AIT.StorageSetItems`

변경 없음: v3.7.0 → v3.8.0 · v3.6.0 → v3.7.0 · v3.1.0 → v3.1.1 · v3.0.5 → v3.1.0 · v3.0.4 → v3.0.5 · v3.0.3 → v3.0.4 · v3.0.2 → v3.0.3 · v3.0.1 → v3.0.2

## v3.9.0 API 카탈로그

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
- `AIT.ViralGetCampaign`
- `AIT.ViralShare`
- `AIT.ViralTrack`
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
- `AIT.StorageGetItems`
- `AIT.StorageRemoveItem`
- `AIT.StorageSetItem`
- `AIT.StorageSetItems`

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

### Other

- `AIT.GameSubscribeInfoOverlayHidden`
- `AIT.NavigationBarSetOptions`
- `AIT.UserCreateAnonymousKeyAuthCode`

### Notification

- `AIT.NotificationRequestAgreement`
- `AIT.RequestNotificationAgreement`

### Promotion

- `AIT.PromotionGrantReward`
- `AIT.GrantPromotionReward`

### Review

- `AIT.ReviewRequest`
- `AIT.RequestReview`
