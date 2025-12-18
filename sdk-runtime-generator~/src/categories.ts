/**
 * API 카테고리 매핑
 * SDK 생성 시 API를 카테고리별로 그룹핑하여 partial class 파일 생성
 *
 * ⚠️ 새 API 추가 시 이 목록에 추가 필요 (매핑에 없으면 생성 실패)
 */

export const API_CATEGORIES: Record<string, string[]> = {
  Authentication: ['appLogin', 'getIsTossLoginIntegratedService'],
  Payment: ['checkoutPayment'],
  Location: ['getCurrentLocation', 'startUpdateLocation'],
  Permission: ['getPermission', 'requestPermission', 'openPermissionDialog'],
  SystemInfo: [
    'getNetworkStatus',
    'getPlatformOS',
    'getTossAppVersion',
    'getOperationalEnvironment',
    'getSchemeUri',
    'getLocale',
    'getDeviceId',
  ],
  GameCenter: [
    'getGameCenterGameProfile',
    'openGameCenterLeaderboard',
    'submitGameCenterLeaderBoardScore',
    'getUserKeyForGame',
    'grantPromotionRewardForGame',
  ],
  Clipboard: ['getClipboardText', 'setClipboardText'],
  Share: ['share', 'getTossShareLink', 'contactsViral', 'fetchContacts'],
  Media: ['openCamera', 'fetchAlbumPhotos', 'saveBase64Data'],
  Device: [
    'generateHapticFeedback',
    'setDeviceOrientation',
    'setScreenAwakeMode',
    'setSecureScreen',
    'setIosSwipeGestureEnabled',
  ],
  Navigation: ['closeView', 'openURL'],
  Events: ['eventLog'],
  Certificate: ['appsInTossSignTossCert'],
  Visibility: ['onVisibilityChangedByTransparentServiceWeb'],

  // 네임스페이스 객체 API (index.d.ts에서 파싱)
  IAP: [
    'IAPCreateOneTimePurchaseOrder',
    'IAPGetProductItemList',
    'IAPGetPendingOrders',
    'IAPGetCompletedOrRefundedOrders',
    'IAPCompleteProductGrant',
  ],
  Storage: [
    'StorageGetItem',
    'StorageSetItem',
    'StorageRemoveItem',
    'StorageClearItems',
  ],
  Advertising: [
    'GoogleAdMobLoadAppsInTossAdMob',
    'GoogleAdMobShowAppsInTossAdMob',
    'GoogleAdMobLoadAdMobInterstitialAd',
    'GoogleAdMobShowAdMobInterstitialAd',
    'GoogleAdMobLoadAdMobRewardedAd',
    'GoogleAdMobShowAdMobRewardedAd',
    // TossAds (v1.6.0+)
    'TossAdsInitialize',
    'TossAdsAttach',
    'TossAdsDestroy',
    'TossAdsDestroyAll',
  ],
  SafeArea: [
    'SafeAreaInsetsGet',
    'SafeAreaInsetsSubscribe',
  ],
  Partner: [
    'partnerAddAccessoryButton',
    'partnerRemoveAccessoryButton',
  ],
  AppEvents: [
    'TdsEventSubscribeNavigationAccessoryEvent',
    'GraniteEventSubscribeBackEvent',
    'AppsInTossEventSubscribeEntryMessageExited',
  ],
  Environment: [
    'envGetDeploymentId',
    'isMinVersionSupported',
    'getAppsInTossGlobals',
  ],
};

/**
 * 카테고리 표시 순서 (UI에서 사용)
 */
export const CATEGORY_ORDER: string[] = [
  'Authentication',
  'Payment',
  'IAP',
  'SystemInfo',
  'Location',
  'Permission',
  'GameCenter',
  'Share',
  'Media',
  'Clipboard',
  'Device',
  'Navigation',
  'Events',
  'Certificate',
  'Visibility',
  'Storage',
  'Advertising',
  'SafeArea',
  'Partner',
  'AppEvents',
  'Environment',
  'Other',
];

/**
 * API 이름으로 카테고리 찾기
 * @param apiName API 이름 (camelCase, 예: appLogin) 또는 PascalCase (예: IAPGetProductItemList)
 * @returns 카테고리 이름
 * @throws 매핑에 없는 API인 경우 에러 발생
 */
export function getCategory(apiName: string): string {
  for (const [category, apis] of Object.entries(API_CATEGORIES)) {
    if (apis.includes(apiName)) {
      return category;
    }
  }
  // 매핑에 없는 API는 에러 발생 (명시적으로 카테고리 추가 필요)
  throw new Error(
    `API '${apiName}'의 카테고리가 정의되지 않았습니다.\n` +
    `categories.ts의 API_CATEGORIES에 추가해주세요.`
  );
}

/**
 * 모든 API가 카테고리에 매핑되어 있는지 검증
 * @param apiNames 검증할 API 이름 목록
 * @returns 검증 결과 (성공 여부, 누락된 API 목록)
 */
export function validateCategoryMappings(apiNames: string[]): {
  success: boolean;
  missingApis: string[];
} {
  const allMappedApis = new Set<string>();
  for (const apis of Object.values(API_CATEGORIES)) {
    apis.forEach(api => allMappedApis.add(api));
  }

  const missingApis = apiNames.filter(name => !allMappedApis.has(name));
  return {
    success: missingApis.length === 0,
    missingApis,
  };
}
