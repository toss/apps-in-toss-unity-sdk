/**
 * API 카테고리 매핑
 *
 * 카테고리는 C# 파일 단위(AIT.<Category>.cs, AIT.Types.<Category>.cs)이자 jslib 파일 단위
 * (AppsInToss-<Category>.jslib)다. 파서(bundle-parser.ts)가 API마다 한 번 정해서 api.category에
 * 넣고, 이후 단계는 api.category만 읽는다.
 *
 * 결정 규칙:
 * 1. 네임스페이스 멤버: NAMESPACE_CATEGORIES (네임스페이스 이름 기준)
 * 2. 최상위 함수: TOP_LEVEL_API_CATEGORIES (함수 이름 기준)
 * 3. 하위 멤버(isSupported 등): 기반 API의 카테고리
 * 4. 매핑이 없으면 기본 카테고리 'Other' (경고 출력)
 */

/** 최상위 함수 export의 카테고리 (키: 카테고리, 값: 번들 export 이름) */
export const TOP_LEVEL_API_CATEGORIES: Record<string, string[]> = {
  Authentication: ['appLogin', 'getIsTossLoginIntegratedService'],
  Payment: ['checkoutPayment', 'requestTossPayPaysBilling'],
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
    'getServerTime',
    'getGroupId',
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
  Media: ['openCamera', 'fetchAlbumPhotos', 'fetchAlbumItems', 'saveBase64Data', 'openPDFViewer'],
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
  Advertising: ['loadFullScreenAd', 'showFullScreenAd'],
  SafeArea: ['getSafeAreaInsets'],
  Environment: ['isMinVersionSupported', 'getAppsInTossGlobals'],
  User: ['getAnonymousKey', 'getConsentedUserData', 'getDeclaredAgeRange'],
  Promotion: ['grantPromotionReward'],
  Review: ['requestReview'],
  Notification: ['requestNotificationAgreement'],
};

/** 네임스페이스 export 이름 -> 카테고리 */
export const NAMESPACE_CATEGORIES: Record<string, string> = {
  Ads: 'Advertising',
  Analytics: 'Analytics',
  Clipboard: 'Clipboard',
  Device: 'Device',
  Environment: 'Environment',
  File: 'Media',
  Game: 'GameCenter',
  GoogleAdMob: 'Advertising',
  IAP: 'IAP',
  Migration: 'Migration',
  NavigationBar: 'Navigation',
  Notification: 'Notification',
  Promotion: 'Promotion',
  Review: 'Review',
  SafeArea: 'SafeArea',
  SafeAreaInsets: 'SafeArea',
  // 'Screen' 카테고리 파일(AIT.Screen.cs)은 브라우저 API용 수동 파일이라 Device에 둔다.
  Screen: 'Device',
  Share: 'Share',
  Storage: 'Storage',
  TossAds: 'Advertising',
  TossAuth: 'Authentication',
  TossPay: 'Payment',
  User: 'User',
  Viral: 'Share',
  appsInTossEvent: 'AppEvents',
  env: 'Environment',
  graniteEvent: 'AppEvents',
  partner: 'Partner',
  tdsEvent: 'AppEvents',
};

/** 기본 카테고리 (매핑이 없을 때 사용) */
export const DEFAULT_CATEGORY = 'Other';

/** 카테고리 표시 순서 (생성 파일 순서). 여기 없는 카테고리는 알파벳 순으로 뒤에 붙는다. */
export const CATEGORY_ORDER: string[] = [
  'Authentication',
  'User',
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
  'Analytics',
  'Certificate',
  'Visibility',
  'Storage',
  'Advertising',
  'SafeArea',
  'Partner',
  'AppEvents',
  'Environment',
  'Promotion',
  'Notification',
  'Review',
  'Migration',
  'Other',
];

/** 최상위 함수 export의 카테고리를 찾는다 */
export function getTopLevelCategory(exportName: string, warn: boolean = true): string {
  for (const [category, names] of Object.entries(TOP_LEVEL_API_CATEGORIES)) {
    if (names.includes(exportName)) return category;
  }
  if (warn) {
    console.warn(
      `⚠️  최상위 API '${exportName}'의 카테고리가 정의되지 않았습니다. '${DEFAULT_CATEGORY}'로 분류됩니다.\n` +
        `   categories.ts의 TOP_LEVEL_API_CATEGORIES에 추가를 검토해주세요.`,
    );
  }
  return DEFAULT_CATEGORY;
}

/**
 * C#으로 노출하지 않는 번들 export와 그 사유.
 * 사유는 기술적 제약이어야 한다. 여기에 없는 export는 모두 C# 진입점으로 옮겨진다.
 */
export const NOT_EXPOSED_EXPORTS: Record<string, string> = {
  createAsyncBridge:
    '고차 함수. 메서드 이름을 받아 제네릭 함수를 돌려주는 팩토리라 정적 C# 시그니처로 표현할 수 없다. 번들 내부 구현 도구이며 개별 API는 각자 노출된다',
  createConstantBridge:
    '고차 함수. 상수 이름을 받아 제네릭 getter를 돌려주는 팩토리라 정적 C# 시그니처로 표현할 수 없다',
  createEventBridge:
    '고차 함수. 이벤트 메서드 이름을 받아 구독 함수를 돌려주는 팩토리라 정적 C# 시그니처로 표현할 수 없다',
  StartUpdateLocationPermissionError:
    '오류 클래스의 별칭(typeof GetCurrentLocationPermissionError). 값으로 던져질 뿐 호출 대상이 없다',
};

/**
 * 파라미터 평탄화를 유지하는 최상위 콜백 API.
 * `{ options: { adGroupId }, onEvent, onError }`를 `(adGroupId, onEvent, onError)`로 푼다.
 * 이 두 API는 처음부터 이 C# 시그니처로 공개돼 있어 유지한다.
 */
export const FLATTENED_OPTIONS_CALLBACK_APIS: string[] = ['loadFullScreenAd', 'showFullScreenAd'];

/**
 * 생성기 측 deprecate override 목록
 * upstream d.ts에 @deprecated JSDoc이 없지만 SDK 차원에서 deprecate 처리할 API.
 * 키는 생성되는 C# API 이름(네임스페이스+PascalCase), 값은 [Obsolete] 메시지.
 */
export const DEPRECATED_API_OVERRIDES: Record<string, string> = {
  'TossAdsAttachBanner':
    'CSS 셀렉터 기반 attachBanner는 더 이상 권장되지 않습니다. AITBannerAdView 컴포넌트 또는 AITBannerAd.Show를 사용해주세요.',
};
