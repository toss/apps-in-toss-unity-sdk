/**
 * API 카테고리 매핑
 * SDK 생성 시 API를 카테고리별로 그룹핑하여 partial class 파일 생성
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
};

/**
 * 카테고리 표시 순서 (UI에서 사용)
 */
export const CATEGORY_ORDER: string[] = [
  'Authentication',
  'Payment',
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
  'Other',
];

/**
 * API 이름으로 카테고리 찾기
 * @param apiName API 이름 (camelCase, 예: appLogin)
 * @returns 카테고리 이름 또는 'Other'
 */
export function getCategory(apiName: string): string {
  for (const [category, apis] of Object.entries(API_CATEGORIES)) {
    if (apis.includes(apiName)) {
      return category;
    }
  }
  return 'Other';
}

/**
 * API 목록을 카테고리별로 그룹핑
 * @param apiNames API 이름 목록
 * @returns 카테고리별 API 이름 Map
 */
export function groupByCategory(apiNames: string[]): Map<string, string[]> {
  const grouped = new Map<string, string[]>();

  for (const apiName of apiNames) {
    const category = getCategory(apiName);
    if (!grouped.has(category)) {
      grouped.set(category, []);
    }
    grouped.get(category)!.push(apiName);
  }

  // 카테고리 순서에 따라 정렬된 Map 반환
  const sorted = new Map<string, string[]>();
  for (const category of CATEGORY_ORDER) {
    if (grouped.has(category)) {
      sorted.set(category, grouped.get(category)!);
    }
  }

  // Other 카테고리 추가
  if (grouped.has('Other')) {
    sorted.set('Other', grouped.get('Other')!);
  }

  return sorted;
}
