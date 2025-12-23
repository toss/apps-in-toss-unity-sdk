import { noop } from 'es-toolkit';
import type { AdMobFullScreenEvent, AdMobHandlerParams, InterstitialAd, RewardedAd } from './types';
import { INTERNAL__appBridgeHandler } from '../../native-event-emitter/internal/appBridge';
import { getOperationalEnvironment } from '../getOperationalEnvironment';
import { isMinVersionSupported } from '../isMinVersionSupported';

// MARK: Interstitial AD (load)

export interface LoadAdMobInterstitialAdOptions {
  /**
   * 광고 단위 ID
   */
  adUnitId: string;
}

/**
 * @public
 * @category 광고
 * @name LoadAdMobInterstitialAdEvent
 * @description 전면 광고를 불러오는 함수에서 발생하는 이벤트 타입이에요. `loaded` 이벤트가 발생하면 광고를 성공적으로 불러온 거예요. 이때 [InterstitialAd](/react-native/reference/native-modules/광고/InterstitialAd.html) 객체가 함께 반환돼요.
 */
export type LoadAdMobInterstitialAdEvent =
  | AdMobFullScreenEvent
  | {
      type: 'loaded';
      data: InterstitialAd;
    };

/**
 * @public
 * @category 광고
 * @name LoadAdMobInterstitialAdParams
 * @description 전면 광고를 불러오는 함수에 필요한 옵션 객체예요.
 */
export type LoadAdMobInterstitialAdParams = AdMobHandlerParams<
  LoadAdMobInterstitialAdOptions,
  LoadAdMobInterstitialAdEvent
>;

/**
 * @public
 * @category 광고
 * @name loadAdMobInterstitialAd
 * @deprecated 이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요.
 *
 * @example
 * ### 버튼 눌러 불러온 광고 보여주기 (loadAppsInTossAdMob로 변경 예제)
 * ```tsx
 * import { GoogleAdMob } from '@apps-in-toss/framework';
 * import { useFocusEffect } from '@granite-js/native/@react-navigation/native';
 * import { useNavigation } from '@granite-js/react-native';
 * import { useCallback, useState } from 'react';
 * import { Button, Text, View } from 'react-native';
 *
 * const AD_GROUP_ID = '<AD_GROUP_ID>';
 *
 * export function GoogleAdmobExample() {
 *   const [adLoadStatus, setAdLoadStatus] = useState<'not_loaded' | 'loaded' | 'failed'>('not_loaded');
 *   const navigation = useNavigation();
 *
 *   const loadAd = useCallback(() => {
 *     if (GoogleAdMob.loadAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }

 *     const cleanup = GoogleAdMob.loadAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'loaded':
 *             console.log('광고 로드 성공', event.data);
 *             setAdLoadStatus('loaded');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 불러오기 실패', error);
 *       },
 *     });
 *
 *     return cleanup;
 *   }, [navigation]);
 *
 *   const showAd = useCallback(() => {
 *     if (GoogleAdMob.showAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }
 *
 *     GoogleAdMob.showAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'requested':
 *             console.log('광고 보여주기 요청 완료');
 *             break;
 * 
 *           case 'clicked':
 *             console.log('광고 클릭');
 *             break;
 *
 *           case 'dismissed':
 *             console.log('광고 닫힘');
 *             navigation.navigate('/examples/google-admob-interstitial-ad-landing');
 *             break;
 *
 *           case 'failedToShow':
 *             console.log('광고 보여주기 실패');
 *             break;
 *
 *           case 'impression':
 *             console.log('광고 노출');
 *             break;
 * 
 *           case 'userEarnedReward':
 *             console.log('광고 보상 획득 unitType:', event.data.unitType);
 *             console.log('광고 보상 획득 unitAmount:', event.data.unitAmount);
 *             break;
 *
 *           case 'show':
 *             console.log('광고 컨텐츠 보여졌음');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 보여주기 실패', error);
 *       },
 *     });
 *   }, []);
 *
 *   useFocusEffect(loadAd);
 *
 *   return (
 *     <View>
 *       <Text>
 *         {adLoadStatus === 'not_loaded' && '광고 로드 하지 않음 '}
 *         {adLoadStatus === 'loaded' && '광고 로드 완료'}
 *         {adLoadStatus === 'failed' && '광고 로드 실패'}
 *       </Text>
 *
 *       <Button title="Show Ad" onPress={showAd} disabled={adLoadStatus !== 'loaded'} />
 *     </View>
 *   );
 * }
 * ```
 */
export function loadAdMobInterstitialAd(params: LoadAdMobInterstitialAdParams) {
  if (!loadAdMobInterstitialAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return noop as () => void;
  }

  const { onEvent, onError, options } = params;

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod('loadAdMobInterstitialAd', options, {
    onAdClicked: () => {
      onEvent({ type: 'clicked' });
    },
    onAdDismissed: () => {
      onEvent({ type: 'dismissed' });
    },
    onAdFailedToShow: () => {
      onEvent({ type: 'failedToShow' });
    },
    onAdImpression: () => {
      onEvent({ type: 'impression' });
    },
    onAdShow: () => {
      onEvent({ type: 'show' });
    },
    onSuccess: (result: InterstitialAd) => onEvent({ type: 'loaded', data: result }),
    onError,
  });

  return unregisterCallbacks;
}

// MARK: Interstitial AD (show)

export interface ShowAdMobInterstitialAdOptions {
  /**
   * 광고 단위 ID
   */
  adUnitId: string;
}

/**
 * @public
 * @category 광고
 * @name ShowAdMobInterstitialAdEvent
 * @description 전면 광고를 보여주는 함수에서 발생하는 이벤트 타입이에요. `requested` 이벤트가 발생하면 광고 노출 요청이 Google AdMob에 성공적으로 전달된 거예요.
 */
export type ShowAdMobInterstitialAdEvent = { type: 'requested' };

/**
 * @public
 * @category 광고
 * @name ShowAdMobInterstitialAdParams
 * @description 불러온 전면 광고를 보여주는 함수에 필요한 옵션 객체예요.
 */
export type ShowAdMobInterstitialAdParams = AdMobHandlerParams<
  ShowAdMobInterstitialAdOptions,
  ShowAdMobInterstitialAdEvent
>;

/**
 * @public
 * @category 광고
 * @name showAdMobInterstitialAd
 * @deprecated 이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.showAppsInTossAdMob}를 사용해주세요.
 *
 * @example
 * ### 버튼 눌러 불러온 광고 보여주기 (showAppsInTossAdMob로 변경 예제)
 * ```tsx
 * import { GoogleAdMob } from '@apps-in-toss/framework';
 * import { useFocusEffect } from '@granite-js/native/@react-navigation/native';
 * import { useNavigation } from '@granite-js/react-native';
 * import { useCallback, useState } from 'react';
 * import { Button, Text, View } from 'react-native';
 *
 * const AD_GROUP_ID = '<AD_GROUP_ID>';
 *
 * export function GoogleAdmobExample() {
 *   const [adLoadStatus, setAdLoadStatus] = useState<'not_loaded' | 'loaded' | 'failed'>('not_loaded');
 *   const navigation = useNavigation();
 *
 *   const loadAd = useCallback(() => {
 *     if (GoogleAdMob.loadAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }

 *     const cleanup = GoogleAdMob.loadAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'loaded':
 *             console.log('광고 로드 성공', event.data);
 *             setAdLoadStatus('loaded');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 불러오기 실패', error);
 *       },
 *     });
 *
 *     return cleanup;
 *   }, [navigation]);
 *
 *   const showAd = useCallback(() => {
 *     if (GoogleAdMob.showAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }
 *
 *     GoogleAdMob.showAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'requested':
 *             console.log('광고 보여주기 요청 완료');
 *             break;
 * 
 *           case 'clicked':
 *             console.log('광고 클릭');
 *             break;
 *
 *           case 'dismissed':
 *             console.log('광고 닫힘');
 *             navigation.navigate('/examples/google-admob-interstitial-ad-landing');
 *             break;
 *
 *           case 'failedToShow':
 *             console.log('광고 보여주기 실패');
 *             break;
 *
 *           case 'impression':
 *             console.log('광고 노출');
 *             break;
 * 
 *           case 'userEarnedReward':
 *             console.log('광고 보상 획득 unitType:', event.data.unitType);
 *             console.log('광고 보상 획득 unitAmount:', event.data.unitAmount);
 *             break;
 *
 *           case 'show':
 *             console.log('광고 컨텐츠 보여졌음');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 보여주기 실패', error);
 *       },
 *     });
 *   }, []);
 *
 *   useFocusEffect(loadAd);
 *
 *   return (
 *     <View>
 *       <Text>
 *         {adLoadStatus === 'not_loaded' && '광고 로드 하지 않음 '}
 *         {adLoadStatus === 'loaded' && '광고 로드 완료'}
 *         {adLoadStatus === 'failed' && '광고 로드 실패'}
 *       </Text>
 *
 *       <Button title="Show Ad" onPress={showAd} disabled={adLoadStatus !== 'loaded'} />
 *     </View>
 *   );
 * }
 * ```
 */
export function showAdMobInterstitialAd(params: ShowAdMobInterstitialAdParams) {
  if (!showAdMobInterstitialAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return noop as () => void;
  }

  const { onEvent, onError, options } = params;

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod('showAdMobInterstitialAd', options, {
    onSuccess: () => onEvent({ type: 'requested' }),
    onError,
  });

  return unregisterCallbacks;
}

// MARK: Rewarded AD (load)

export interface LoadAdMobRewardedAdOptions {
  /**
   * 광고 단위 ID
   */
  adUnitId: string;
}

/**
 * @public
 * @category 광고
 * @name LoadAdMobRewardedAdEvent
 * @description 보상형 광고를 불러오는 함수에서 발생하는 이벤트 타입이에요. `loaded` 이벤트가 발생하면 광고를 성공적으로 불러온 거예요. 이때 [RewardedAd](/react-native/reference/native-modules/광고/RewardedAd.html) 객체가 함께 반환돼요. `userEarnedReward` 이벤트는 사용자가 광고를 끝까지 시청해, 보상 조건을 충족했을 때 발생해요.
 */
export type LoadAdMobRewardedAdEvent =
  | AdMobFullScreenEvent
  | { type: 'loaded'; data: RewardedAd }
  | { type: 'userEarnedReward' };

/**
 * @public
 * @category 광고
 * @name LoadAdMobRewardedAdParams
 * @description 보상형 광고를 불러오는 함수에 필요한 옵션 객체예요.
 */
export type LoadAdMobRewardedAdParams = AdMobHandlerParams<LoadAdMobRewardedAdOptions, LoadAdMobRewardedAdEvent>;

/**
 * @public
 * @category 광고
 * @name loadAdMobRewardedAd
 * @deprecated 이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.loadAppsInTossAdMob}를 사용해주세요.
 *
 * @example
 * ### 버튼 눌러 불러온 광고 보여주기 (loadAppsInTossAdMob로 변경 예제)
 * ```tsx
 * import { GoogleAdMob } from '@apps-in-toss/framework';
 * import { useFocusEffect } from '@react-native-bedrock/native/@react-navigation/native';
 * import { useCallback, useState } from 'react';
 * import { Button, Text, View } from 'react-native';
 * import { useNavigation } from 'react-native-bedrock';
 *
 * const AD_GROUP_ID = '<AD_GROUP_ID>';
 *
 * export function GoogleAdmobExample() {
 *   const [adLoadStatus, setAdLoadStatus] = useState<'not_loaded' | 'loaded' | 'failed'>('not_loaded');
 *   const navigation = useNavigation();
 *
 *   const loadAd = useCallback(() => {
 *     if (GoogleAdMob.loadAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }

 *     const cleanup = GoogleAdMob.loadAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'loaded':
 *             console.log('광고 로드 성공', event.data);
 *             setAdLoadStatus('loaded');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 불러오기 실패', error);
 *       },
 *     });
 *
 *     return cleanup;
 *   }, [navigation]);
 *
 *   const showAd = useCallback(() => {
 *     if (GoogleAdMob.showAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }
 *
 *     GoogleAdMob.showAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'requested':
 *             console.log('광고 보여주기 요청 완료');
 *             break;
 * 
 *           case 'clicked':
 *             console.log('광고 클릭');
 *             break;
 *
 *           case 'dismissed':
 *             console.log('광고 닫힘');
 *             navigation.navigate('/examples/google-admob-interstitial-ad-landing');
 *             break;
 *
 *           case 'failedToShow':
 *             console.log('광고 보여주기 실패');
 *             break;
 *
 *           case 'impression':
 *             console.log('광고 노출');
 *             break;
 * 
 *           case 'userEarnedReward':
 *             console.log('광고 보상 획득 unitType:', event.data.unitType);
 *             console.log('광고 보상 획득 unitAmount:', event.data.unitAmount);
 *             break;
 *
 *           case 'show':
 *             console.log('광고 컨텐츠 보여졌음');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 보여주기 실패', error);
 *       },
 *     });
 *   }, []);
 *
 *   useFocusEffect(loadAd);
 *
 *   return (
 *     <View>
 *       <Text>
 *         {adLoadStatus === 'not_loaded' && '광고 로드 하지 않음 '}
 *         {adLoadStatus === 'loaded' && '광고 로드 완료'}
 *         {adLoadStatus === 'failed' && '광고 로드 실패'}
 *       </Text>
 *
 *       <Button title="Show Ad" onPress={showAd} disabled={adLoadStatus !== 'loaded'} />
 *     </View>
 *   );
 * }
 * ```
 */
export function loadAdMobRewardedAd(params: LoadAdMobRewardedAdParams) {
  if (!loadAdMobRewardedAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return noop as () => void;
  }

  const { onEvent, onError, options } = params;

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod('loadAdMobRewardedAd', options, {
    onAdClicked: () => {
      onEvent({ type: 'clicked' });
    },
    onAdDismissed: () => {
      onEvent({ type: 'dismissed' });
    },
    onAdFailedToShow: () => {
      onEvent({ type: 'failedToShow' });
    },
    onAdImpression: () => {
      onEvent({ type: 'impression' });
    },
    onAdShow: () => {
      onEvent({ type: 'show' });
    },
    onUserEarnedReward: () => {
      onEvent({ type: 'userEarnedReward' });
    },
    onSuccess: (result: RewardedAd) => onEvent({ type: 'loaded', data: result }),
    onError,
  });

  return unregisterCallbacks;
}

// MARK: Rewarded AD (show)

export interface ShowAdMobRewardedAdOptions {
  /**
   * 광고 단위 ID
   */
  adUnitId: string;
}

/**
 * @public
 * @category 광고
 * @name ShowAdMobRewardedAdEvent
 * @description 보상형 광고를 보여주는 함수에서 발생하는 이벤트 타입이에요. `requested` 이벤트가 발생하면 광고 노출 요청이 Google AdMob에 성공적으로 전달된 거예요.
 */
export type ShowAdMobRewardedAdEvent = { type: 'requested' };

/**
 * @public
 * @category 광고
 * @name ShowAdMobRewardedAdParams
 * @description 불러온 보상형 광고를 보여주는 함수에 필요한 옵션 객체예요.
 */
export type ShowAdMobRewardedAdParams = AdMobHandlerParams<ShowAdMobRewardedAdOptions, ShowAdMobRewardedAdEvent>;

/**
 * @public
 * @category 광고
 * @name showAdMobRewardedAd
 * @deprecated 이 함수는 더 이상 사용되지 않습니다. 대신 {@link GoogleAdMob.showAppsInTossAdMob}를 사용해주세요.
 *
 * @example
 * ### 버튼 눌러 불러온 광고 보여주기 (showAppsInTossAdMob로 변경 예제)
 * ```tsx
 * import { GoogleAdMob } from '@apps-in-toss/framework';
 * import { useFocusEffect } from '@react-native-bedrock/native/@react-navigation/native';
 * import { useCallback, useState } from 'react';
 * import { Button, Text, View } from 'react-native';
 * import { useNavigation } from 'react-native-bedrock';
 *
 * const AD_GROUP_ID = '<AD_GROUP_ID>';
 *
 * export function GoogleAdmobExample() {
 *   const [adLoadStatus, setAdLoadStatus] = useState<'not_loaded' | 'loaded' | 'failed'>('not_loaded');
 *   const navigation = useNavigation();
 *
 *   const loadAd = useCallback(() => {
 *     if (GoogleAdMob.loadAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }

 *     const cleanup = GoogleAdMob.loadAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'loaded':
 *             console.log('광고 로드 성공', event.data);
 *             setAdLoadStatus('loaded');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 불러오기 실패', error);
 *       },
 *     });
 *
 *     return cleanup;
 *   }, [navigation]);
 *
 *   const showAd = useCallback(() => {
 *     if (GoogleAdMob.showAppsInTossAdMob.isSupported() !== true) {
 *       return;
 *     }
 *
 *     GoogleAdMob.showAppsInTossAdMob({
 *       options: {
 *         adGroupId: AD_GROUP_ID,
 *       },
 *       onEvent: (event) => {
 *         switch (event.type) {
 *           case 'requested':
 *             console.log('광고 보여주기 요청 완료');
 *             break;
 * 
 *           case 'clicked':
 *             console.log('광고 클릭');
 *             break;
 *
 *           case 'dismissed':
 *             console.log('광고 닫힘');
 *             navigation.navigate('/examples/google-admob-interstitial-ad-landing');
 *             break;
 *
 *           case 'failedToShow':
 *             console.log('광고 보여주기 실패');
 *             break;
 *
 *           case 'impression':
 *             console.log('광고 노출');
 *             break;
 * 
 *           case 'userEarnedReward':
 *             console.log('광고 보상 획득 unitType:', event.data.unitType);
 *             console.log('광고 보상 획득 unitAmount:', event.data.unitAmount);
 *             break;
 *
 *           case 'show':
 *             console.log('광고 컨텐츠 보여졌음');
 *             break;
 *         }
 *       },
 *       onError: (error) => {
 *         console.error('광고 보여주기 실패', error);
 *       },
 *     });
 *   }, []);
 *
 *   useFocusEffect(loadAd);
 *
 *   return (
 *     <View>
 *       <Text>
 *         {adLoadStatus === 'not_loaded' && '광고 로드 하지 않음 '}
 *         {adLoadStatus === 'loaded' && '광고 로드 완료'}
 *         {adLoadStatus === 'failed' && '광고 로드 실패'}
 *       </Text>
 *
 *       <Button title="Show Ad" onPress={showAd} disabled={adLoadStatus !== 'loaded'} />
 *     </View>
 *   );
 * }
 * ```
 */
export function showAdMobRewardedAd(params: ShowAdMobRewardedAdParams) {
  if (!showAdMobRewardedAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return noop as () => void;
  }

  const { onEvent, onError, options } = params;

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod('showAdMobRewardedAd', options, {
    onSuccess: () => onEvent({ type: 'requested' }),
    onError,
  });

  return unregisterCallbacks;
}

// MARK: - isSupported

const ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION = '5.209.0';
const IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION = '5.209.0';
const UNSUPPORTED_ERROR_MESSAGE = 'This feature is not supported in the current environment';
const ENVIRONMENT = getOperationalEnvironment();

function createIsSupported() {
  return () => {
    if (ENVIRONMENT !== 'toss') {
      return false;
    }

    return isMinVersionSupported({
      android: ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION,
      ios: IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION,
    });
  };
}

loadAdMobInterstitialAd.isSupported = createIsSupported();
loadAdMobRewardedAd.isSupported = createIsSupported();
showAdMobInterstitialAd.isSupported = createIsSupported();
showAdMobRewardedAd.isSupported = createIsSupported();
