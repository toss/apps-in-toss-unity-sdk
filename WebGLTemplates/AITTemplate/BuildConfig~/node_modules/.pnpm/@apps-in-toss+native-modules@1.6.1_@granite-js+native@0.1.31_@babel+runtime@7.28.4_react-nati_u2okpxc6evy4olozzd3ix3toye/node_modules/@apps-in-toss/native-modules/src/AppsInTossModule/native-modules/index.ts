// AdMob (deprecated)
import {
  loadAdMobInterstitialAd,
  loadAdMobRewardedAd,
  showAdMobInterstitialAd,
  showAdMobRewardedAd,
  type LoadAdMobInterstitialAdEvent,
  type LoadAdMobInterstitialAdOptions,
  type LoadAdMobRewardedAdEvent,
  type LoadAdMobRewardedAdOptions,
  type ShowAdMobInterstitialAdEvent,
  type ShowAdMobInterstitialAdOptions,
  type ShowAdMobRewardedAdEvent,
  type ShowAdMobRewardedAdOptions,
} from './ads/googleAdMob';
// AdMob V2
import {
  loadAppsInTossAdMob,
  showAppsInTossAdMob,
  type LoadAdMobParams,
  type LoadAdMobOptions,
  type LoadAdMobEvent,
  type ShowAdMobParams,
  type ShowAdMobEvent,
  type ShowAdMobOptions,
} from './ads/googleAdMobV2';
// TossPay
import { checkoutPayment, type CheckoutPaymentOptions, type CheckoutPaymentResult } from './checkoutPayment';

export { AppsInTossModuleInstance as INTERNAL__AppsInTossModule } from './AppsInTossModule';
export { AppsInTossModule } from './AppsInTossModule';

export * from './appLogin';
export * from './eventLog';

/** permission functions */
export * from './permissions/fetchAlbumPhotos/fetchAlbumPhotos';
export * from './permissions/fetchContacts/fetchContacts';
export * from './permissions/getClipboardText/getClipboardText';
export * from './permissions/getCurrentLocation/getCurrentLocation';
export * from './permissions/setClipboardText/setClipboardText';
export * from './permissions/openCamera/openCamera';

export * from './getDeviceId';
export * from './getOperationalEnvironment';
export * from './getTossAppVersion';
export * from './getTossShareLink';
export * from './iap';
export * from './isMinVersionSupported';
export * from './saveBase64Data';
export * from './setDeviceOrientation';
export * from './storage';
export * from './openGameCenterLeaderboard';
export * from './getGameCenterGameProfile';
export * from './submitGameCenterLeaderBoardScore';
export * from './getUserKeyForGame';
export * from './grantPromotionRewardForGame';
export * from './getIsTossLoginIntegratedService';
export * from '../native-event-emitter/contactsViral';
export * from './appsInTossSignTossCert';

export type {
  LoadAdMobInterstitialAdEvent,
  LoadAdMobInterstitialAdOptions,
  LoadAdMobRewardedAdEvent,
  LoadAdMobRewardedAdOptions,
  ShowAdMobInterstitialAdEvent,
  ShowAdMobInterstitialAdOptions,
  ShowAdMobRewardedAdEvent,
  CheckoutPaymentOptions,
  CheckoutPaymentResult,
  ShowAdMobRewardedAdOptions,
  LoadAdMobParams,
  LoadAdMobOptions,
  LoadAdMobEvent,
  ShowAdMobParams,
  ShowAdMobEvent,
  ShowAdMobOptions,
};

/**
 * @public
 * @category 토스페이
 * @name TossPay
 * @description 토스페이 결제 관련 함수를 모아둔 객체예요.
 * @property {typeof checkoutPayment} [checkoutPayment] 토스페이 결제를 인증하는 함수예요. 자세한 내용은 [checkoutPayment](/react-native/reference/native-modules/토스페이/checkoutPayment)를 참고하세요.
 */
export const TossPay = {
  checkoutPayment,
};

/**
 * @public
 * @category 광고
 * @name GoogleAdMob
 * @description Google AdMob 광고 관련 함수를 모아둔 객체예요.
 * @property {typeof loadAdMobInterstitialAd} [loadAdMobInterstitialAd] 전면 광고를 로드하는 함수예요. 자세한 내용은 [loadAdMobInterstitialAd](/react-native/reference/native-modules/광고/loadAdMobInterstitialAd.html)를 참고하세요.
 * @property {typeof showAdMobInterstitialAd} [showAdMobInterstitialAd] 로드한 전면 광고를 보여주는 함수예요. 자세한 내용은 [showAdMobInterstitialAd](/react-native/reference/native-modules/광고/showAdMobInterstitialAd.html)를 참고하세요.
 * @property {typeof loadAdMobRewardedAd} [loadAdMobRewardedAd] 보상형 광고를 로드하는 함수예요. 자세한 내용은 [loadAdMobRewardedAd](/react-native/reference/native-modules/광고/loadAdMobRewardedAd.html)를 참고하세요.
 * @property {typeof showAdMobRewardedAd} [showAdMobRewardedAd] 로드한 보상형 광고를 보여주는 함수예요. 자세한 내용은 [showAdMobRewardedAd](/react-native/reference/native-modules/광고/showAdMobRewardedAd.html)를 참고하세요.
 * @property {typeof loadAppsInTossAdMob} [loadAppsInTossAdMob] 광고를 로드하는 함수예요. 자세한 내용은 [loadAppsInTossAdMob](/react-native/reference/native-modules/광고/loadAppsInTossAdMob.html)를 참고하세요.
 * @property {typeof showAppsInTossAdMob} [showAppsInTossAdMob] 로드한 광고를 보여주는 함수예요. 자세한 내용은 [showAppsInTossAdMob](/react-native/reference/native-modules/광고/showAppsInTossAdMob.html)를 참고하세요.
 */
export const GoogleAdMob = {
  loadAdMobInterstitialAd,
  showAdMobInterstitialAd,
  loadAdMobRewardedAd,
  showAdMobRewardedAd,
  loadAppsInTossAdMob,
  showAppsInTossAdMob,
};
