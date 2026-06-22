/**
 * API 카테고리 매핑
 * SDK 생성 시 API를 카테고리별로 그룹핑하여 partial class 파일 생성
 *
 * 카테고리 결정 우선순위:
 * 1. 명시적 매핑 (API_CATEGORIES) - 가장 정확
 * 2. 네임스페이스 접두사 패턴 (CATEGORY_INFERENCE_RULES)
 * 3. 의미 기반 키워드 패턴 (CATEGORY_INFERENCE_RULES)
 * 4. 기본 카테고리 'Other' (빌드 실패 없이 경고만 출력)
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
    'GoogleAdMobIsAppsInTossAdMobLoaded', // v1.8.0+
    // TossAds (v1.6.0+)
    'TossAdsInitialize',
    'TossAdsAttach',
    'TossAdsDestroy',
    'TossAdsDestroyAll',
    // Full Screen Ads (top-level exports)
    'loadFullScreenAd',
    'showFullScreenAd',
  ],
  SafeArea: [
    'SafeAreaInsetsGet',
    'SafeAreaInsetsSubscribe',
  ],
  Partner: [
    'PartnerAddAccessoryButton',
    'PartnerRemoveAccessoryButton',
  ],
  AppEvents: [
    'TdsEventSubscribeNavigationAccessoryEvent',
    'GraniteEventSubscribeBackEvent',
    // 'AppsInTossEventSubscribeEntryMessageExited', // v1.8.0에서 제거됨 (AppsInTossEvent = {})
  ],
  Environment: [
    'EnvGetDeploymentId',
    'isMinVersionSupported',
    'getAppsInTossGlobals',
  ],
  Analytics: [
    'AnalyticsScreen',
    'AnalyticsImpression',
    'AnalyticsClick',
  ],
};

/**
 * 카테고리 추론 규칙 인터페이스
 */
export interface CategoryInferenceRule {
  pattern: RegExp;
  category: string;
}

/**
 * 자동 카테고리 추론 규칙
 * API 이름의 패턴을 기반으로 카테고리를 추론
 * 순서대로 검사하여 첫 번째 매치되는 규칙 적용
 */
export const CATEGORY_INFERENCE_RULES: CategoryInferenceRule[] = [
  // 네임스페이스 접두사 (PascalCase)
  { pattern: /^IAP[A-Z]/, category: 'IAP' },
  { pattern: /^Storage[A-Z]/, category: 'Storage' },
  { pattern: /^GoogleAdMob/, category: 'Advertising' },
  { pattern: /^TossAds/, category: 'Advertising' },
  { pattern: /^SafeArea/, category: 'SafeArea' },
  { pattern: /^Partner/, category: 'Partner' },
  { pattern: /^Env[A-Z]/, category: 'Environment' },
  { pattern: /^Analytics[A-Z]/, category: 'Analytics' },
  { pattern: /^(Tds|Granite|AppsInToss)Event/, category: 'AppEvents' },

  // 의미 기반 패턴
  { pattern: /^(get|set)Clipboard/, category: 'Clipboard' },
  { pattern: /Location$|^(get|start|stop).*Location/, category: 'Location' },
  { pattern: /Permission/, category: 'Permission' },
  { pattern: /^(get|set)Device|Haptic|Orientation|ScreenAwake|SecureScreen|SwipeGesture/, category: 'Device' },
  { pattern: /GameCenter|Leaderboard|ForGame/, category: 'GameCenter' },
  { pattern: /^share$|Share|Viral|Contacts/, category: 'Share' },
  { pattern: /^(open)?Camera|Album|Photo|saveBase64/, category: 'Media' },
  { pattern: /^(close|open)View|openURL/, category: 'Navigation' },
  { pattern: /^eventLog$/, category: 'Events' },
  { pattern: /TossCert|Certificate/, category: 'Certificate' },
  { pattern: /Visibility/, category: 'Visibility' },
  { pattern: /^(app)?Login|LoginIntegrated/, category: 'Authentication' },
  { pattern: /^checkout|Payment/, category: 'Payment' },
  { pattern: /FullScreenAd/, category: 'Advertising' },

  // SystemInfo 패턴 (getServerTime 등 새 API 자동 분류)
  { pattern: /^get(DeviceId|Locale|NetworkStatus|PlatformOS|TossAppVersion|OperationalEnvironment|SchemeUri|ServerTime)$/, category: 'SystemInfo' },
];

/**
 * 기본 카테고리 (추론 실패 시 사용)
 */
export const DEFAULT_CATEGORY = 'Other';

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
  'Analytics',
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
 *
 * 카테고리 결정 우선순위:
 * 1. 명시적 매핑 (API_CATEGORIES) - 가장 정확
 * 2. 추론 규칙 (CATEGORY_INFERENCE_RULES) - 패턴 기반
 * 3. 기본 카테고리 (DEFAULT_CATEGORY) - 'Other'
 *
 * @param apiName API 이름 (camelCase, 예: appLogin) 또는 PascalCase (예: IAPGetProductItemList)
 * @param warn 매핑 누락 시 경고 출력 여부 (기본 true). 타입 분류처럼 동일 API에 대해
 *   getCategory가 두 번 호출되는 경로에서는 false로 넘겨 중복 경고를 막는다.
 * @returns 카테고리 이름
 */
export function getCategory(apiName: string, warn: boolean = true): string {
  // 1. 명시적 매핑 확인 (가장 우선)
  for (const [category, apis] of Object.entries(API_CATEGORIES)) {
    if (apis.includes(apiName)) {
      return category;
    }
  }

  // 2. 추론 규칙 확인
  for (const rule of CATEGORY_INFERENCE_RULES) {
    if (rule.pattern.test(apiName)) {
      return rule.category;
    }
  }

  // 3. 기본 카테고리 반환 (경고 출력)
  if (warn) {
    console.warn(
      `⚠️  API '${apiName}'의 카테고리가 정의되지 않았습니다. '${DEFAULT_CATEGORY}'로 분류됩니다.\n` +
      `   categories.ts의 API_CATEGORIES 또는 CATEGORY_INFERENCE_RULES에 추가를 검토해주세요.`
    );
  }
  return DEFAULT_CATEGORY;
}

/**
 * API 객체의 카테고리를 결정한다 (생성기 전역 단일 규약).
 * - 이벤트 구독 API: 파서가 설정한 api.category 사용
 * - 네임스페이스 API: 전체 이름(pascalName)으로 룩업 (예: IAPGetProductItemList)
 * - 일반 API: 원본 이름(originalName)으로 룩업 (예: appLogin)
 *
 * 파라미터는 ParsedAPI를 구조적으로만 받아 categories.ts ↔ types.ts 순환 import를 피한다.
 * @param warn getCategory 매핑 누락 경고 출력 여부 (동일 API에 대해 중복 호출되는 경로는 false).
 */
export function resolveApiCategory(
  api: { isEventSubscription?: boolean; category: string; namespace?: string; pascalName: string; originalName: string },
  warn: boolean = true
): string {
  if (api.isEventSubscription) {
    return api.category;
  }
  if (api.namespace) {
    return getCategory(api.pascalName, warn);
  }
  return getCategory(api.originalName, warn);
}

/**
 * @apps-in-toss/framework 패키지에서 직접 파싱해야 하는 API 목록
 * 이 API들은 web-framework에서 re-export되지 않음
 */
export const FRAMEWORK_APIS: string[] = [
  'loadFullScreenAd',
  'showFullScreenAd',
];

/**
 * SDK 생성에서 제외할 API 목록
 * 이 API들은 파싱되더라도 SDK에 포함되지 않음
 */
export const EXCLUDED_APIS: string[] = [
  // deprecated GoogleAdMob 인앱 광고 API (2026-01 제거)
  'GoogleAdMobLoadAdMobInterstitialAd',
  'GoogleAdMobShowAdMobInterstitialAd',
  'GoogleAdMobLoadAdMobRewardedAd',
  'GoogleAdMobShowAdMobRewardedAd',
  // v1.8.0에서 제거된 API (AppsInTossEvent = {})
  'AppsInTossEventSubscribeEntryMessageExited',
];

/**
 * 생성기 측 deprecate override 목록
 * upstream d.ts에 @deprecated JSDoc이 없지만 SDK 차원에서 deprecate 처리할 API.
 * 키는 생성되는 C# API 이름(네임스페이스+PascalCase), 값은 [Obsolete] 메시지.
 */
export const DEPRECATED_API_OVERRIDES: Record<string, string> = {
  'TossAdsAttachBanner':
    'CSS 셀렉터 기반 attachBanner는 더 이상 권장되지 않습니다. AITBannerAdView 컴포넌트 또는 AITBannerAd.Show를 사용해주세요.',
};

/**
 * 모든 API가 카테고리에 매핑되어 있는지 검증
 * @param apiNames 검증할 API 이름 목록
 * @returns 검증 결과 (성공 여부, 누락된 API 목록, 추론된 API 목록)
 */
export function validateCategoryMappings(apiNames: string[]): {
  success: boolean;
  missingApis: string[];
  inferredApis: Array<{ name: string; category: string; rule: 'pattern' | 'default' }>;
} {
  const allMappedApis = new Set<string>();
  for (const apis of Object.values(API_CATEGORIES)) {
    apis.forEach(api => allMappedApis.add(api));
  }

  const missingApis: string[] = [];
  const inferredApis: Array<{ name: string; category: string; rule: 'pattern' | 'default' }> = [];

  for (const name of apiNames) {
    if (allMappedApis.has(name)) {
      continue; // 명시적 매핑 있음
    }

    // 추론 규칙으로 분류 시도
    let inferredCategory: string | null = null;
    for (const rule of CATEGORY_INFERENCE_RULES) {
      if (rule.pattern.test(name)) {
        inferredCategory = rule.category;
        break;
      }
    }

    if (inferredCategory) {
      inferredApis.push({ name, category: inferredCategory, rule: 'pattern' });
    } else {
      missingApis.push(name);
      inferredApis.push({ name, category: DEFAULT_CATEGORY, rule: 'default' });
    }
  }

  return {
    success: missingApis.length === 0,
    missingApis,
    inferredApis,
  };
}
