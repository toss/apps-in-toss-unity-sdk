Apps in Toss Unity SDK가 노출하는 C# API 표면이 `@apps-in-toss/web-framework` 버전에 따라 어떻게 달라지는지 정리한 자동 생성 리포트입니다. 최신 버전(v3.0.3)이 최상단에 오도록 정렬되어 있습니다.

다음 버전은 패키지 리네임 등으로 sibling 탐색에 실패해 pnpm store 폴백으로 API 표면을 근사함: v3.0.1, v3.0.2, v3.0.3

## v3.x

변경 없음: v3.0.2 → v3.0.3 · v3.0.1 → v3.0.2 · v2.10.8 → v3.0.1

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

## v3.0.3 API 카탈로그

### Authentication

- `AIT.AppLogin`
- `AIT.GetIsTossLoginIntegratedService`

### Payment

- `AIT.CheckoutPayment`

### IAP

- `AIT.IAPCompleteProductGrant`
- `AIT.IAPCreateOneTimePurchaseOrder`
- `AIT.IAPCreateSubscriptionPurchaseOrder`
- `AIT.IAPGetCompletedOrRefundedOrders`
- `AIT.IAPGetPendingOrders`
- `AIT.IAPGetProductItemList`
- `AIT.IAPGetSubscriptionInfo`

### SystemInfo

- `AIT.GetDeviceId`
- `AIT.GetLocale`
- `AIT.GetNetworkStatus`
- `AIT.GetOperationalEnvironment`
- `AIT.GetPlatformOS`
- `AIT.GetSchemeUri`
- `AIT.GetServerTime`
- `AIT.GetTossAppVersion`

### Location

- `AIT.GetCurrentLocation`
- `AIT.StartUpdateLocation`

### Permission

- `AIT.GetPermission`
- `AIT.OpenPermissionDialog`
- `AIT.RequestPermission`

### GameCenter

- `AIT.GetGameCenterGameProfile`
- `AIT.GetUserKeyForGame`
- `AIT.GrantPromotionRewardForGame`
- `AIT.OpenGameCenterLeaderboard`
- `AIT.SubmitGameCenterLeaderBoardScore`

### Share

- `AIT.ContactsViral`
- `AIT.FetchContacts`
- `AIT.GetTossShareLink`
- `AIT.Share`

### Media

- `AIT.FetchAlbumItems`
- `AIT.FetchAlbumPhotos`
- `AIT.OpenCamera`
- `AIT.SaveBase64Data`

### Clipboard

- `AIT.GetClipboardText`
- `AIT.SetClipboardText`

### Device

- `AIT.GenerateHapticFeedback`
- `AIT.SetDeviceOrientation`
- `AIT.SetIosSwipeGestureEnabled`
- `AIT.SetScreenAwakeMode`
- `AIT.SetSecureScreen`

### Navigation

- `AIT.CloseView`
- `AIT.OpenURL`

### Events

- `AIT.EventLog`

### Analytics

- `AIT.AnalyticsClick`
- `AIT.AnalyticsImpression`
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

- `AIT.SafeAreaInsetsGet`
- `AIT.SafeAreaInsetsSubscribe`

### AppEvents

- `AIT.GraniteEventSubscribeBackEvent`
- `AIT.GraniteEventSubscribeHomeEvent`
- `AIT.TdsEventSubscribeNavigationAccessoryEvent`

### Environment

- `AIT.GetAppsInTossGlobals`
- `AIT.IsMinVersionSupported`

### Other

- `AIT.EnvGetDeploymentId`
- `AIT.GetAnonymousKey`
- `AIT.GetConsentedUserData`
- `AIT.GetDeclaredAgeRange`
- `AIT.GetGroupId`
- `AIT.GrantPromotionReward`
- `AIT.OpenPDFViewer`
- `AIT.PartnerAddAccessoryButton`
- `AIT.PartnerRemoveAccessoryButton`
- `AIT.RequestNotificationAgreement`
- `AIT.RequestReview`
- `AIT.RequestTossPayPaysBilling`
