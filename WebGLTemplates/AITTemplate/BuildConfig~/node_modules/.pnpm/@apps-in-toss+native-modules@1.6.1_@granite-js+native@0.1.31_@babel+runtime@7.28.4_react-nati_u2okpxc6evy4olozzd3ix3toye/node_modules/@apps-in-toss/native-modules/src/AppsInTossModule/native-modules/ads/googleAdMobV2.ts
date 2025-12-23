import { noop } from 'es-toolkit';
import type { AdMobFullScreenEvent, AdMobHandlerParams, AdMobLoadResult, AdUserEarnedReward } from './types';
import { getReferrer } from '../../../utils/getReferrer';
import { INTERNAL__appBridgeHandler } from '../../native-event-emitter/internal/appBridge';
import { getOperationalEnvironment } from '../getOperationalEnvironment';
import { isMinVersionSupported } from '../isMinVersionSupported';

// MARK: AdMob Load

export interface LoadAdMobOptions {
  /**
   * 광고 그룹 단위 ID
   */
  adGroupId: string;
}

/**
 * @public
 * @category 광고
 * @name LoadAdMobEvent
 * @description 광고를 불러오는 함수에서 발생하는 이벤트 타입이에요. `loaded` 이벤트가 발생하면 광고를 성공적으로 불러온 거예요. 이때 [AdMobLoadResult](/react-native/reference/native-modules/광고/AdMobLoadResult.html) 객체가 함께 반환돼요.
 */
export type LoadAdMobEvent = {
  type: 'loaded';
  data: AdMobLoadResult;
};

/**
 * @public
 * @category 광고
 * @name LoadAdMobParams
 * @description 광고를 불러오는 함수에 필요한 옵션 객체예요.
 */
export type LoadAdMobParams = AdMobHandlerParams<LoadAdMobOptions, LoadAdMobEvent>;

/**
 * @public
 * @category 광고
 * @name loadAppsInTossAdMob
 * @description 광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요.
 * @param {LoadAdMobParams} params 광고를 불러올 때 사용할 설정 값이에요. 광고 그룹 ID와 광고의 동작에 대한 콜백을 설정할 수 있어요.
 * @param {LoadAdMobOptions} params.options 광고를 불러올 때 전달할 옵션 객체예요.
 * @param {string} params.options.adGroupId 광고 그룹 단위 ID예요. 콘솔에서 발급받은 ID를 입력해요.
 * @param {(event: LoadAdMobEvent) => void} [params.onEvent] 광고 관련 이벤트가 발생했을 때 호출돼요. (예시: 광고가 닫히거나 클릭됐을 때). 자세한 이벤트 타입은 [LoadAdMobEvent](/react-native/reference/native-modules/광고/LoadAdMobEvent.html)를 참고하세요.
 * @param {(reason: unknown) => void} [params.onError] 광고를 불러오지 못했을 때 호출돼요. (예시: 네트워크 오류나 지원하지 않는 환경일 때)
 * @property {() => boolean} [isSupported] 현재 실행 중인 앱(예: 토스 앱, 개발용 샌드박스 앱 등)에서 Google AdMob 광고 기능을 지원하는지 확인하는 함수예요. 기능을 사용하기 전에 지원 여부를 확인해야 해요.
 *
 * @example
 * ### 버튼 눌러 불러온 광고 보여주기
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
export function loadAppsInTossAdMob(params: LoadAdMobParams) {
  if (!loadAppsInTossAdMob.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return noop;
  }

  const { onEvent, onError, options } = params;
  const referrer = getReferrer();

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    'loadAppsInTossAdmob',
    { ...options, referrer },
    {
      onSuccess: (result) => onEvent({ type: 'loaded', data: result }),
      onError,
    }
  );

  return unregisterCallbacks;
}

// MARK: AdMob Show

export interface ShowAdMobOptions {
  /**
   * 광고 그룹 단위 ID
   */
  adGroupId: string;
}

/**
 * @public
 * @category 광고
 * @name ShowAdMobEvent
 * @description 광고를 보여주는 함수에서 발생하는 이벤트 타입이에요. `requested` 이벤트가 발생하면 광고 노출 요청이 Google AdMob에 성공적으로 전달된 거예요.
 */
export type ShowAdMobEvent = AdMobFullScreenEvent | AdUserEarnedReward | { type: 'requested' };

/**
 * @public
 * @category 광고
 * @name ShowAdMobParams
 * @description 불러온 광고를 보여주는 함수에 필요한 옵션 객체예요.
 */
export type ShowAdMobParams = AdMobHandlerParams<ShowAdMobOptions, ShowAdMobEvent>;

/**
 * @public
 * @category 광고
 * @name showAppsInTossAdMob
 * @description 광고를 사용자에게 노출해요. 이 함수는 `loadAppsInTossAdMob` 로 미리 불러온 광고를 실제로 사용자에게 노출해요.
 * @param {ShowAdMobParams} params 광고를 보여주기 위해 사용할 설정 값이에요. 광고 그룹 ID와과 광고의 동작에 대한 콜백을 설정할 수 있어요.
 * @param {ShowAdMobOptions} params.options 광고를 보여줄 때 전달할 옵션 객체예요.
 * @param {string} params.options.adUnitId 광고 그룹 단위 ID예요. `loadAppsInTossAdMob` 로 불러온 광고용 그룹 ID를 입력해요.
 * @param {(event: ShowAdMobEvent) => void} [params.onEvent] 광고 관련 이벤트가 발생했을 때 호출돼요. (예시: 광고 노출을 요청했을 때). 자세한 이벤트 타입은 [ShowAdMobEvent](/react-native/reference/native-modules/광고/ShowAdMobEvent.html)를 참고하세요.
 * @param {(reason: unknown) => void} [params.onError] 광고를 노출하지 못했을 때 호출돼요. (예시: 네트워크 오류나 지원하지 않는 환경일 때)
 * @property {() => boolean} [isSupported] 현재 실행 중인 앱(예: 토스 앱, 개발용 샌드박스 앱 등)에서 Google AdMob 광고 기능을 지원하는지 확인하는 함수예요. 기능을 사용하기 전에 지원 여부를 확인해야 해요.
 *
 * @example
 * ### 버튼 눌러 불러온 광고 보여주기
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
export function showAppsInTossAdMob(params: ShowAdMobParams) {
  if (!showAppsInTossAdMob.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return noop;
  }

  const { onEvent, onError, options } = params;
  const referrer = getReferrer();

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    'showAppsInTossAdmob',
    { ...options, referrer },
    {
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
      onUserEarnedReward: (data: { unitType: string; unitAmount: number }) => {
        onEvent({ type: 'userEarnedReward', data });
      },
      onSuccess: () => onEvent({ type: 'requested' }),
      onError,
    }
  );

  return unregisterCallbacks;
}

// MARK: - isSupported

const ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION = '5.227.0';
const IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION = '5.227.0';
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

loadAppsInTossAdMob.isSupported = createIsSupported();
showAppsInTossAdMob.isSupported = createIsSupported();
