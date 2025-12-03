/**
 * Unity Bridge for Apps in Toss
 *
 * @apps-in-toss/web-framework의 모든 함수를 window.AppsInToss에 노출합니다.
 * Unity jslib에서 window.AppsInToss.functionName()으로 호출할 수 있습니다.
 */

import {
  // 로그인
  appLogin,

  // 기기 정보
  getDeviceId,
  getPlatformOS,
  getTossAppVersion,
  getOperationalEnvironment,
  getLocale,
  getSchemeUri,
  getNetworkStatus,

  // 권한
  getPermission,
  requestPermission,
  openPermissionDialog,

  // UI/UX
  closeView,
  generateHapticFeedback,
  setIosSwipeGestureEnabled,
  setSecureScreen,
  setScreenAwakeMode,
  setDeviceOrientation,

  // 클립보드
  setClipboardText,
  getClipboardText,

  // 공유
  share,
  getTossShareLink,

  // URL/외부 앱
  openURL,

  // 위치
  getCurrentLocation,
  startUpdateLocation,

  // 카메라/사진
  openCamera,
  fetchAlbumPhotos,

  // 연락처
  fetchContacts,
  contactsViral,

  // 저장
  saveBase64Data,

  // 결제
  checkoutPayment,

  // 게임센터
  getGameCenterGameProfile,
  openGameCenterLeaderboard,
  submitGameCenterLeaderBoardScore,
  getUserKeyForGame,
  grantPromotionRewardForGame,

  // 토스 인증
  appsInTossSignTossCert,
  getIsTossLoginIntegratedService,

  // 이벤트 로깅
  eventLog,

  // 가시성
  onVisibilityChangedByTransparentServiceWeb,
} from '@apps-in-toss/web-framework';

// window.AppsInToss 타입 정의
declare global {
  interface Window {
    AppsInToss: typeof AppsInTossBridge;
  }
}

// 모든 API를 하나의 객체로 묶음
const AppsInTossBridge = {
  // 로그인
  appLogin,

  // 기기 정보
  getDeviceId,
  getPlatformOS,
  getTossAppVersion,
  getOperationalEnvironment,
  getLocale,
  getSchemeUri,
  getNetworkStatus,

  // 권한
  getPermission,
  requestPermission,
  openPermissionDialog,

  // UI/UX
  closeView,
  generateHapticFeedback,
  setIosSwipeGestureEnabled,
  setSecureScreen,
  setScreenAwakeMode,
  setDeviceOrientation,

  // 클립보드
  setClipboardText,
  getClipboardText,

  // 공유
  share,
  getTossShareLink,

  // URL/외부 앱
  openURL,

  // 위치
  getCurrentLocation,
  startUpdateLocation,

  // 카메라/사진
  openCamera,
  fetchAlbumPhotos,

  // 연락처
  fetchContacts,
  contactsViral,

  // 저장
  saveBase64Data,

  // 결제
  checkoutPayment,

  // 게임센터
  getGameCenterGameProfile,
  openGameCenterLeaderboard,
  submitGameCenterLeaderBoardScore,
  getUserKeyForGame,
  grantPromotionRewardForGame,

  // 토스 인증
  appsInTossSignTossCert,
  getIsTossLoginIntegratedService,

  // 이벤트 로깅
  eventLog,

  // 가시성
  onVisibilityChangedByTransparentServiceWeb,
};

// window.AppsInToss에 노출
window.AppsInToss = AppsInTossBridge;

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(AppsInTossBridge).length, 'functions');
console.log('[Unity Bridge] Available functions:', Object.keys(AppsInTossBridge).join(', '));

export default AppsInTossBridge;
