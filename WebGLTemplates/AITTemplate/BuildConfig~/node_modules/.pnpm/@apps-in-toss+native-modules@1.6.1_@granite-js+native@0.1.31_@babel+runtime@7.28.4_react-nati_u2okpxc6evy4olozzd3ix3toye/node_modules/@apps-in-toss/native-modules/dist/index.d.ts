import * as _apps_in_toss_types from '@apps-in-toss/types';
import { Location, StartUpdateLocationEventParams, Accuracy, CompatiblePlaceholderArgument, SetClipboardTextOptions, FetchContactsOptions, ContactResult, FetchAlbumPhotosOptions, ImageResponse, GetCurrentLocationOptions, OpenCameraOptions, PermissionName, PermissionAccess, PermissionStatus, FetchAlbumPhotos, FetchContacts, GetClipboardText, GetCurrentLocation, SetClipboardText, OpenCamera } from '@apps-in-toss/types';
export { StartUpdateLocationPermissionError } from '@apps-in-toss/types';
import { GraniteEventDefinition, GraniteEvent } from '@granite-js/react-native';
import { EmitterSubscription, TurboModule } from 'react-native';

interface EventEmitterSchema<K extends string, P extends unknown[]> {
    name: K;
    params: P;
}

/**
 * @name UpdateLocationEventEmitter
 * @kind typedef
 * @description
 * 디바이스의 위치 정보 변경을 감지해요
 */
type UpdateLocationEventEmitter = EventEmitterSchema<'updateLocation', [Location]>;
/**
 * @public
 * @category 위치 정보
 * @name startUpdateLocation
 * @description  디바이스의 위치 정보를 지속적으로 감지하고, 위치가 변경되면 콜백을 실행하는 함수예요. 콜백 함수를 등록하면 위치가 변경될 때마다 자동으로 호출돼요.
 * 실시간 위치 추적이 필요한 기능을 구현할 때 사용할 수 있어요. 예를 들어 지도 앱에서 사용자의 현재 위치를 실시간으로 업데이트할 때, 운동 앱에서 사용자의 이동 거리를 기록할 때 등이에요.
 * 위치 업데이트 주기와 정확도를 조정해 배터리 소모를 최소화하면서도 필요한 정보를 얻을 수 있어요.
 *
 *
 * @param {(error: unknown) => void} onError 위치 정보 감지에 실패했을 때 호출되는 콜백 함수예요.
 * @param {(location: Location) => void} onEvent 위치 정보가 변경될 때 호출되는 콜백 함수예요. 자세한 내용은 [Location](/react-native/reference/native-modules/Types/Location.html)을 참고해주세요.
 * @param {StartUpdateLocationOptions} options - 위치 정보 감지에 필요한 설정 객체에요.
 * @param {number} [options.accuracy] 위치 정확도를 설정해요.
 * @param {number} [options.timeInterval] 위치 정보를 업데이트하는 최소 주기로, 단위는 밀리초(ms)예요. 이 값은 위치 업데이트가 발생하는 가장 짧은 간격을 설정하지만, 시스템이나 환경의 영향을 받아 지정한 주기보다 더 긴 간격으로 업데이트될 수 있어요.
 * @param {number} [options.distanceInterval] 위치 변경 거리를 미터(m) 단위로 설정해요.
 *
 * @property [openPermissionDialog] - 위치 정보 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 위치 정보 권한의 현재 상태를 반환해요. `allowed`는 사용자가 위치 정보 권한을 허용한 상태예요. `denied`는 사용자가 위치 정보 권한을 거부한 상태예요. `notDetermined`는 위치 정보 권한 요청을 한 번도 하지 않은 상태예요.
 *
 * @signature
 * ```typescript
 * function startUpdateLocation(options: {
 *   onError: (error: unknown) => void;
 *   onEvent: (location: Location) => void;
 *   options: StartUpdateLocationOptions;
 * }): () => void;
 * ```
 *
 * @example
 * ### 위치 정보 변경 감지하기
 *
 * 위치 정보가 변경되는것을 감지하는 예제예요. "위치 정보 변경 감지하기"를 눌러서 감지할 수 있어요.
 *
 * "권한 확인하기"버튼을 눌러서 현재 위치 정보 변경 감지 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`StartUpdateLocationPermissionError`](/react-native/reference/types/권한/StartUpdateLocationPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 위치 정보 변경 감지 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { Accuracy, Location, startUpdateLocation, StartUpdateLocationPermissionError } from '@apps-in-toss/framework';
 * import { useCallback, useState } from 'react';
 * import { Alert, Button, Text, View } from 'react-native';
 *
 * // 위치 정보 변경 감지하기
 * function LocationWatcher() {
 *   const [location, setLocation] = useState<Location | null>(null);
 *
 *   const handlePress = useCallback(() => {
 *     startUpdateLocation({
 *       options: {
 *         accuracy: Accuracy.Balanced,
 *         timeInterval: 3000,
 *         distanceInterval: 10,
 *       },
 *       onEvent: (location) => {
 *         setLocation(location);
 *       },
 *       onError: (error) => {
 *         if (error instanceof StartUpdateLocationPermissionError) {
 *           // 위치 정보 변경 감지 권한 없음
 *         }
 *         console.error('위치 정보를 가져오는데 실패했어요:', error);
 *       },
 *     });
 *   }, []);
 *
 *   return (
 *     <View>
 *       {location != null && (
 *         <>
 *           <Text>위도: {location.coords.latitude}</Text>
 *           <Text>경도: {location.coords.longitude}</Text>
 *           <Text>위치 정확도: {location.coords.accuracy}m</Text>
 *           <Text>높이: {location.coords.altitude}m</Text>
 *           <Text>고도 정확도: {location.coords.altitudeAccuracy}m</Text>
 *           <Text>방향: {location.coords.heading}°</Text>
 *         </>
 *       )}
 *
 *       <Button title="위치 정보 변경 감지하기" onPress={handlePress} />
 *
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await startUpdateLocation.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await startUpdateLocation.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
declare function startUpdateLocation(eventParams: StartUpdateLocationEventParams): () => void;
declare namespace startUpdateLocation {
    var openPermissionDialog: _apps_in_toss_types.PermissionDialogFunction;
    var getPermission: _apps_in_toss_types.GetPermissionFunction;
}

/**
 * @public
 * @category 친구초대
 * @name RewardFromContactsViralEvent
 * @description 친구에게 공유하기를 완료했을 때 지급할 리워드 정보를 담는 타입이에요. 이 타입을 사용하면 공유가 완료됐을 때 지급할 리워드 정보를 확인할 수 있어요.
 * @property {'sendViral'} type - 이벤트의 타입이에요. `'sendViral'`은 사용자가 친구에게 공유를 완료했을 때 돌아와요.
 * @property {Object} data - 지급할 리워드 관련 정보를 담고 있어요.
 * @property {number} data.rewardAmount - 지급할 리워드 수량이에요. 앱인토스 콘솔에서 설정한 수량 및 금액 값이에요.
 * @property {string} data.rewardUnit - 리워드의 단위예요. 앱인토스 콘솔에 설정된 리워드 이름인 '하트', '보석' 등이 리워드 단위예요.
 */
type RewardFromContactsViralEvent = {
    type: 'sendViral';
    data: {
        rewardAmount: number;
        rewardUnit: string;
    };
};
/**
 * @public
 * @category 친구초대
 * @name ContactsViralSuccessEvent
 * @description 연락처 공유 모듈이 정상적으로 종료됐을 때 전달되는 이벤트 객체예요. 종료 이유와 함께 리워드 상태 및 남은 친구 수 등 관련 정보를 제공해요.
 * @property {'close'} type - 이벤트의 타입이에요. `'close'`는 공유 모듈이 종료됐을 때 돌아와요.
 * @property {Object} data - 모듈 종료와 관련된 세부 정보를 담고 있어요.
 * @property {'clickBackButton' | 'noReward'} data.closeReason - 모듈이 종료된 이유예요. `'clickBackButton'`은 사용자가 뒤로 가기 버튼을 눌러 종료한 경우이고, `'noReward'`는 받을 수 있는 리워드가 없어서 종료된 경우예요.
 * @property {number} data.sentRewardAmount - 사용자가 받은 전체 리워드 수량이에요.
 * @property {number} data.sendableRewardsCount - 아직 공유할 수 있는 친구 수예요.
 * @property {number} data.sentRewardsCount - 사용자가 공유를 완료한 친구 수예요.
 * @property {string} data.rewardUnit - 리워드의 단위예요. 앱인토스 콘솔에 설정된 리워드 이름인 '하트', '보석' 등이 리워드 단위예요.
 */
type ContactsViralSuccessEvent = {
    type: 'close';
    data: {
        closeReason: 'clickBackButton' | 'noReward';
        sentRewardAmount?: number;
        sendableRewardsCount?: number;
        sentRewardsCount: number;
        rewardUnit?: string;
    };
};
type ContactsViralEvent = RewardFromContactsViralEvent | ContactsViralSuccessEvent;
/**
 * @public
 * @category 친구초대
 * @name ContactsViralOption
 * @description [연락처 공유 기능](/react-native/reference/native-modules/친구초대/contactsViral.html)을 사용할 때 필요한 옵션이에요.
 * @property {string} moduleId - 공유 리워드를 구분하는 UUID 형식의 고유 ID예요. 앱인토스 콘솔의 미니앱 > 공유 리워드 메뉴에서 확인할 수 있어요.
 */
type ContactsViralOption = {
    moduleId: string;
};
/**
 * @public
 * @category 친구초대
 * @name ContactsViralParams
 * @description `ContactsViralParams`는 연락처 공유 기능을 사용할 때 전달해야 하는 파라미터 타입이에요. 옵션을 설정하고, 이벤트 및 에러 처리 콜백을 지정할 수 있어요.
 * @property {ContactsViralOption} options - 공유 기능에 사용할 옵션 객체예요.
 * @property {(event: ContactsViralEvent) => void} onEvent - 공유 이벤트가 발생했을 때 실행되는 함수예요. [`RewardFromContactsViralEvent`](/bedrock/reference/native-modules/친구초대/RewardFromContactsViralEvent.html) 또는 [`ContactsViralSuccessEvent`](/react-native/reference/native-modules/친구초대/ContactsViralSuccessEvent.html) 타입의 이벤트 객체가 전달돼요.
 * @property {(error: unknown) => void} onError - 예기치 않은 에러가 발생했을 때 실행되는 함수예요.
 */
interface ContactsViralParams {
    options: ContactsViralOption;
    onEvent: (event: ContactsViralEvent) => void;
    onError: (error: unknown) => void;
}
/**
 * @public
 * @category 친구초대
 * @name contactsViral
 * @description 친구에게 공유하고 리워드를 받을 수 있는 기능을 제공해요. 사용자가 친구에게 공유를 완료하면 앱브릿지가 이벤트를 통해 리워드 정보를 전달해요.
 * @param {ContactsViralParams} params - 연락처 공유 기능을 실행할 때 사용하는 파라미터예요. 옵션 설정과 이벤트 핸들러를 포함해요. 자세한 내용은 [ContactsViralParams](/bedrock/reference/native-modules/친구초대/ContactsViralParams.html) 문서를 참고하세요.
 * @returns {() => void} 앱브릿지 cleanup 함수를 반환해요. 공유 기능이 끝나면 반드시 이 함수를 호출해서 리소스를 해제해야 해요.
 *
 * @example
 * ### 친구에게 공유하고 리워드 받기
 *
 * ```tsx
 * import { useCallback } from 'react';
 * import { Button } from 'react-native';
 * import { contactsViral } from '@apps-in-toss/framework';
 *
 * function ContactsViralButton({ moduleId }: { moduleId: string }) {
 *   const handleContactsViral = useCallback(() => {
 *     try {
 *       const cleanup = contactsViral({
 *         options: { moduleId: moduleId.trim() },
 *         onEvent: (event) => {
 *           if (event.type === 'sendViral') {
 *             console.log('리워드 지급:', event.data.rewardAmount, event.data.rewardUnit);
 *           } else if (event.type === 'close') {
 *             console.log('모듈 종료:', event.data.closeReason);
 *           }
 *         },
 *         onError: (error) => {
 *           console.error('에러 발생:', error);
 *         },
 *       });
 *
 *       return cleanup;
 *     } catch (error) {
 *       console.error('실행 중 에러:', error);
 *     }
 *   }, [moduleId]);
 *
 *   return <Button title="친구에게 공유하고 리워드 받기" onPress={handleContactsViral} />;
 * }
 * ```
 */
declare function contactsViral(params: ContactsViralParams): () => void;

interface StartUpdateLocationOptions {
    /**
     * 위치 정확도를 설정해요.
     */
    accuracy: Accuracy;
    /**
     * 위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.
     */
    timeInterval: number;
    /**
     * 위치 변경 거리를 미터(m) 단위로 설정해요.
     */
    distanceInterval: number;
}
declare class UpdateLocationEvent extends GraniteEventDefinition<StartUpdateLocationOptions, Location> {
    name: "updateLocationEvent";
    subscriptionCount: number;
    ref: {
        remove: () => void;
    };
    remove(): void;
    listener(options: StartUpdateLocationOptions, onEvent: (response: Location) => void, onError: (error: unknown) => void): void;
}

interface AppBridgeCallbackResult {
    name: string;
    params?: any;
}
declare class AppBridgeCallbackEvent extends GraniteEventDefinition<void, AppBridgeCallbackResult> {
    private static INTERNAL__appBridgeSubscription?;
    name: "appBridgeCallbackEvent";
    constructor();
    remove(): void;
    listener(): void;
    private registerAppBridgeCallbackEventListener;
    private ensureInvokeAppBridgeCallback;
}

interface VisibilityChangedByTransparentServiceWebOptions {
    callbackId: string;
}
declare class VisibilityChangedByTransparentServiceWebEvent extends GraniteEventDefinition<VisibilityChangedByTransparentServiceWebOptions, boolean> {
    name: "onVisibilityChangedByTransparentServiceWeb";
    subscription: EmitterSubscription | null;
    remove(): void;
    listener(options: VisibilityChangedByTransparentServiceWebOptions, onEvent: (isVisible: boolean) => void, onError: (error: unknown) => void): void;
    private isVisibilityChangedByTransparentServiceWebResult;
}

declare const appsInTossEvent: GraniteEvent<UpdateLocationEvent | AppBridgeCallbackEvent | VisibilityChangedByTransparentServiceWebEvent>;

declare function onVisibilityChangedByTransparentServiceWeb(eventParams: {
    options: {
        callbackId: string;
    };
    onEvent: (isVisible: boolean) => void;
    onError: (error: unknown) => void;
}): () => void;

interface AppBridgeCompatCallbacks<Result> {
    onSuccess: (result: Result) => void;
    onError: (reason: unknown) => void;
}
type AppBridgeCallback = (...args: any[]) => void;
declare function invokeAppBridgeCallback(id: string, ...args: any[]): boolean;
declare function invokeAppBridgeMethod<Result = any, Params = any>(methodName: string, params: Params, callbacks: AppBridgeCompatCallbacks<Result> & Record<string, AppBridgeCallback>): () => void;
declare function registerCallback(callback: AppBridgeCallback, name?: string): string;
declare function unregisterCallback(id: string): void;
declare function getCallbackIds(): string[];
declare const INTERNAL__appBridgeHandler: {
    invokeAppBridgeCallback: typeof invokeAppBridgeCallback;
    invokeAppBridgeMethod: typeof invokeAppBridgeMethod;
    registerCallback: typeof registerCallback;
    unregisterCallback: typeof unregisterCallback;
    getCallbackIds: typeof getCallbackIds;
};

/**
 * @public
 * @category 광고
 * @name AdMobFullScreenEvent
 * @description 앱 화면을 덮는 광고(예: 전면 광고, 보상형 광고)를 사용하는 경우에 발생하는 이벤트 타입이에요.
 * @example
 * ### 광고 이벤트 처리하기
 * ```ts
 * function handleEvent(event: AdMobFullScreenEvent) {
 *   switch (event.type) {
 *     case 'clicked':
 *       console.log('광고가 클릭됐어요.');
 *       break;
 *
 *     case 'dismissed':
 *       console.log('광고가 닫혔어요.');
 *       break;
 *
 *     case 'failedToShow':
 *       console.log('광고가 보여지지 않았어요.');
 *       break;
 *
 *     case 'impression':
 *       console.log('광고가 노출됐어요.');
 *       break;
 *
 *     case 'show':
 *       console.log('광고가 보여졌어요.');
 *       break;
 *   }
 * }
 * ```
 */
type AdMobFullScreenEvent = AdClicked | AdDismissed | AdFailedToShow | AdImpression | AdShow;
type AdClicked = {
    type: 'clicked';
};
type AdDismissed = {
    type: 'dismissed';
};
type AdFailedToShow = {
    type: 'failedToShow';
};
type AdImpression = {
    type: 'impression';
};
type AdShow = {
    type: 'show';
};
type AdUserEarnedReward = {
    type: 'userEarnedReward';
    data: {
        unitType: string;
        unitAmount: number;
    };
};
interface AdMobHandlerParams<Options = void, Event = AdMobFullScreenEvent> {
    options: Options;
    onEvent: (event: Event) => void;
    onError: (error: unknown) => void;
}
/**
 * @public
 * @category 광고
 * @name ResponseInfo
 * @description 광고 로드 응답 정보를 담고 있는 객체예요.
 * @property {Array<AdNetworkResponseInfo>} adNetworkInfoArray 광고 네트워크 응답 정보 배열예요.
 * @property {AdNetworkResponseInfo | null} loadedAdNetworkInfo 로드된 광고 네트워크 응답 정보예요.
 * @property {string | null} responseId 광고 응답 ID예요.
 */
interface ResponseInfo {
    adNetworkInfoArray: Array<AdNetworkResponseInfo>;
    loadedAdNetworkInfo: AdNetworkResponseInfo | null;
    responseId: string | null;
}
/**
 * @public
 * @category 광고
 * @name AdNetworkResponseInfo
 * @description 광고 네트워크 응답 정보를 담고 있는 객체예요.
 * @property {string} adSourceId 광고 소스 ID예요.
 * @property {string} adSourceName 광고 소스 이름이예요.
 * @property {string} adSourceInstanceId 광고 소스 인스턴스 ID예요.
 * @property {string} adSourceInstanceName 광고 소스 인스턴스 이름이예요.
 * @property {string | null} adNetworkClassName 광고 네트워크 클래스 이름이예요.
 */
interface AdNetworkResponseInfo {
    adSourceId: string;
    adSourceName: string;
    adSourceInstanceId: string;
    adSourceInstanceName: string;
    adNetworkClassName: string | null;
}
/**
 * @public
 * @category 광고
 * @name InterstitialAd
 * @description 전면 광고의 ID와 응답 정보를 담고 있는 객체예요. 광고를 로드한 뒤, 관련 정보를 확인할 때 유용해요.
 * @property {string} adUnitId 광고 ID예요.
 * @property {ResponseInfo} responseInfo 광고 로드 응답 정보예요. 자세한 내용은 [ResponseInfo](/react-native/reference/native-modules/광고/ResponseInfo.html)를 참고하세요.
 */
interface InterstitialAd {
    adUnitId: string;
    responseInfo: ResponseInfo;
}
/**
 * @public
 * @category 광고
 * @name RewardedAd
 * @description 보상형 광고의 ID와 응답 정보를 담고 있는 객체예요. 광고를 로드한 뒤, 관련 정보를 확인할 때 유용해요.
 * @property {string} adUnitId 광고 ID예요.
 * @property {ResponseInfo} responseInfo 광고 로드 응답 정보예요. 자세한 내용은 [ResponseInfo](/react-native/reference/native-modules/광고/ResponseInfo.html)를 참고하세요.
 */
interface RewardedAd {
    adUnitId: string;
    responseInfo: ResponseInfo;
}
/**
 * @public
 * @category 광고
 * @name AdMobLoadResult
 * @description 광고의 ID와 응답 정보를 담고 있는 객체예요. 광고를 로드한 뒤, 관련 정보를 확인할 때 유용해요.
 * @property {string} adGroupId 광고 그룹 ID예요.
 * @property {string} adUnitId 광고 ID예요.
 * @property {ResponseInfo} responseInfo 광고 로드 응답 정보예요. 자세한 내용은 [ResponseInfo](/react-native/reference/native-modules/광고/ResponseInfo.html)를 참고하세요.
 */
interface AdMobLoadResult {
    adGroupId: string;
    adUnitId: string;
    responseInfo: ResponseInfo;
}

interface LoadAdMobInterstitialAdOptions {
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
type LoadAdMobInterstitialAdEvent = AdMobFullScreenEvent | {
    type: 'loaded';
    data: InterstitialAd;
};
/**
 * @public
 * @category 광고
 * @name LoadAdMobInterstitialAdParams
 * @description 전면 광고를 불러오는 함수에 필요한 옵션 객체예요.
 */
type LoadAdMobInterstitialAdParams = AdMobHandlerParams<LoadAdMobInterstitialAdOptions, LoadAdMobInterstitialAdEvent>;
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
declare function loadAdMobInterstitialAd(params: LoadAdMobInterstitialAdParams): () => void;
declare namespace loadAdMobInterstitialAd {
    var isSupported: () => boolean;
}
interface ShowAdMobInterstitialAdOptions {
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
type ShowAdMobInterstitialAdEvent = {
    type: 'requested';
};
/**
 * @public
 * @category 광고
 * @name ShowAdMobInterstitialAdParams
 * @description 불러온 전면 광고를 보여주는 함수에 필요한 옵션 객체예요.
 */
type ShowAdMobInterstitialAdParams = AdMobHandlerParams<ShowAdMobInterstitialAdOptions, ShowAdMobInterstitialAdEvent>;
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
declare function showAdMobInterstitialAd(params: ShowAdMobInterstitialAdParams): () => void;
declare namespace showAdMobInterstitialAd {
    var isSupported: () => boolean;
}
interface LoadAdMobRewardedAdOptions {
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
type LoadAdMobRewardedAdEvent = AdMobFullScreenEvent | {
    type: 'loaded';
    data: RewardedAd;
} | {
    type: 'userEarnedReward';
};
/**
 * @public
 * @category 광고
 * @name LoadAdMobRewardedAdParams
 * @description 보상형 광고를 불러오는 함수에 필요한 옵션 객체예요.
 */
type LoadAdMobRewardedAdParams = AdMobHandlerParams<LoadAdMobRewardedAdOptions, LoadAdMobRewardedAdEvent>;
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
declare function loadAdMobRewardedAd(params: LoadAdMobRewardedAdParams): () => void;
declare namespace loadAdMobRewardedAd {
    var isSupported: () => boolean;
}
interface ShowAdMobRewardedAdOptions {
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
type ShowAdMobRewardedAdEvent = {
    type: 'requested';
};
/**
 * @public
 * @category 광고
 * @name ShowAdMobRewardedAdParams
 * @description 불러온 보상형 광고를 보여주는 함수에 필요한 옵션 객체예요.
 */
type ShowAdMobRewardedAdParams = AdMobHandlerParams<ShowAdMobRewardedAdOptions, ShowAdMobRewardedAdEvent>;
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
declare function showAdMobRewardedAd(params: ShowAdMobRewardedAdParams): () => void;
declare namespace showAdMobRewardedAd {
    var isSupported: () => boolean;
}

interface LoadAdMobOptions {
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
type LoadAdMobEvent = {
    type: 'loaded';
    data: AdMobLoadResult;
};
/**
 * @public
 * @category 광고
 * @name LoadAdMobParams
 * @description 광고를 불러오는 함수에 필요한 옵션 객체예요.
 */
type LoadAdMobParams = AdMobHandlerParams<LoadAdMobOptions, LoadAdMobEvent>;
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
declare function loadAppsInTossAdMob(params: LoadAdMobParams): () => void;
declare namespace loadAppsInTossAdMob {
    var isSupported: () => boolean;
}
interface ShowAdMobOptions {
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
type ShowAdMobEvent = AdMobFullScreenEvent | AdUserEarnedReward | {
    type: 'requested';
};
/**
 * @public
 * @category 광고
 * @name ShowAdMobParams
 * @description 불러온 광고를 보여주는 함수에 필요한 옵션 객체예요.
 */
type ShowAdMobParams = AdMobHandlerParams<ShowAdMobOptions, ShowAdMobEvent>;
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
declare function showAppsInTossAdMob(params: ShowAdMobParams): () => void;
declare namespace showAppsInTossAdMob {
    var isSupported: () => boolean;
}

/**
 * @public
 * @category 토스페이
 * @name CheckoutPaymentOptions
 * @description 토스페이 결제창을 띄울 때 필요한 옵션이에요.
 * @property {string} payToken 결제 토큰이에요.
 */
interface CheckoutPaymentOptions {
    /**
     * 결제 토큰이에요.
     */
    payToken: string;
}
/**
 * @public
 * @category 토스페이
 * @name CheckoutPaymentResult
 * @description 토스페이 결제창에서 사용자가 인증에 성공했는지 여부예요.
 * @property {boolean} success 인증이 성공했는지 여부예요.
 * @property {string} [reason] 인증이 실패했을 경우의 이유예요.
 */
interface CheckoutPaymentResult {
    /**
     * 인증이 성공했는지 여부예요.
     */
    success: boolean;
    /**
     * 인증이 실패했을 경우의 이유예요.
     */
    reason?: string;
}
/**
 * @public
 * @category 토스페이
 * @name checkoutPayment
 * @description 토스페이 결제창을 띄우고, 사용자 인증을 수행해요. 인증이 완료되면 성공 여부를 반환해요.
 *
 * 이 함수는 결제창을 통해 사용자 인증만 해요. 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요.
 *
 * @param {CheckoutPaymentOptions} options 결제창을 띄울 때 필요한 옵션이에요.
 * @returns {Promise<CheckoutPaymentResult>} 인증 성공 여부를 포함한 결과를 반환해요.
 *
 * @example
 *
 * ### 토스페이 결제창 띄우고 인증 처리하기
 *
 * ```tsx
 * import { TossPay } from '@apps-in-toss/framework';
 *
 * async function handlePayment() {
 *   try {
 *     // 실제 구현 시 결제 생성 역할을 하는 API 엔드포인트로 대체해주세요.
 *     const { payToken } = await fetch('/my-api/payment/create').then(res => res.json());
 *
 *     const { success, reason } = await TossPay.checkoutPayment({ payToken });
 *
 *     if (success) {
 *       // 실제 구현 시 결제를 실행하는 API 엔드포인트로 대체해주세요.
 *       await fetch('/my-api/payment/execute', {
 *         method: 'POST',
 *         body: JSON.stringify({ payToken }),
 *         headers: { 'Content-Type': 'application/json' },
 *       });
 *     } else {
 *       console.log('인증 실패:', reason);
 *     }
 *   } catch (error) {
 *     console.error('결제 인증 중 오류가 발생했어요:', error);
 *   }
 * }
 * ```
 */
declare function checkoutPayment(options: CheckoutPaymentOptions): Promise<CheckoutPaymentResult>;

interface AppsInTossSignTossCertParams {
    txId: string;
    skipConfirmDoc?: boolean;
}
/**
 * @public
 * @category 토스인증
 * @name appsInTossSignTossCert
 * @description 토스 인증서를 사용해 서명하는 기능을 제공해요. 이 함수를 사용하면 앱인토스에서 제공하는 인증서를 활용해 서명을 할 수 있어요.
 *
 * @param {AppsInTossSignTossCertParams} params - 서명에 필요한 파라미터를 포함하는 객체예요.
 * @param {string} params.txId - 토스인증서를 사용한 본인확인이나 간편인증, 전자서명에서 사용하는 Transaction Id예요.
 * @param {boolean} params.skipConfirmDoc - (선택) 토스 원터치 인증방식을 사용하기 위한 옵션이예요. true 설정 시 최소 버전: Android 5.236.0, iOS 5.236.0 (default: false)
 *
 * @example
 * ```tsx
 * import { appsInTossSignTossCert } from '@apps-in-toss/framework';
 *
 *  // 서명에 필요한 파라미터를 정의해요.
 *  const params = {
 *    txId: "f2e1a6df..."
 *  };
 *
 * appsInTossSignTossCert(params)
 *   .then(() => {
 *     console.log('서명 작업이 성공적으로 완료되었어요.');
 *   })
 *   .catch((error) => {
 *     console.error('서명 작업 중 에러가 발생했어요:', error);
 *   });
 * ```
 */
declare function appsInTossSignTossCert(params: AppsInTossSignTossCertParams): Promise<void>;

/**
 * @category 게임센터
 * @name GameCenterGameProfileResponse
 * @description 토스게임센터 프로필을 가져온 결과 타입이에요.
 * 앱에 프로필이 없는 경우, `statusCode`가 `'PROFILE_NOT_FOUND'`이고 다른 정보는 없어요.
 * 프로필이 있는 경우 `statusCode`가 `'SUCCESS'`이고, 닉네임과 프로필 이미지 주소가 함께 제공돼요.
 * @property {string} statusCode 프로필 조회 결과 상태예요. `'SUCCESS'` 또는 `'PROFILE_NOT_FOUND'` 중 하나예요.
 * @property {string} [nickname] 프로필 닉네임이에요. `statusCode`가 `'SUCCESS'`일 때만 있어요.
 * @property {string} [profileImageUri] 프로필 이미지 URL이에요. `statusCode`가 `'SUCCESS'`일 때만 있어요.
 */
type GameCenterGameProfileResponse = {
    statusCode: 'PROFILE_NOT_FOUND';
} | {
    statusCode: 'SUCCESS';
    nickname: string;
    profileImageUri: string;
};
/**
 * @category 게임센터
 * @name getGameCenterGameProfile
 * @description 토스게임센터 프로필 정보를 가져와요.
 * 사용자가 프로필을 만들지 않았다면 `statusCode`가 `'PROFILE_NOT_FOUND'`인 응답이 반환돼요.
 * 앱 버전이 최소 지원 버전(안드로이드 5.221.0, iOS 5.221.0)보다 낮으면 `undefined`를 반환해요.
 * @returns {Promise<GameCenterGameProfileResponse | undefined>} 프로필 정보 또는 `undefined`를 반환해요.
 *
 * @example
 * ### 게임센터 프로필 가져오기
 * ```tsx
 * import { getGameCenterGameProfile } from './getGameCenterGameProfile';
 * import { useState } from 'react';
 * import { View, Button } from 'react-native';
 *
 * function GameProfile() {
 *   const [profile, setProfile] = useState<GameCenterGameProfileResponse | null>(null);
 *
 *   const handlePress = async () => {
 *     try {
 *       const result = await getGameCenterGameProfile();
 *       if (result) {
 *         setProfile(result);
 *       }
 *     } catch (error) {
 *       console.error('게임센터 프로필 가져오기에 실패했어요.', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       <Button title="게임센터 프로필 가져오기" onPress={handlePress} />
 *     </View>
 *   );
 * }
 * ```
 */
declare function getGameCenterGameProfile(): Promise<GameCenterGameProfileResponse | undefined>;

interface GetUserKeyForGameSuccessResponse {
    hash: string;
    type: 'HASH';
}
interface GetUserKeyForGameErrorResponse {
    type: 'NOT_AVAILABLE';
}
type GetUserKeyForGameResponse = GetUserKeyForGameSuccessResponse | GetUserKeyForGameErrorResponse;
/**
 * @public
 * @category 게임
 * @name getUserKeyForGame
 * @description
 * 게임 카테고리 미니앱에서 사용자의 고유 키를 가져와요. 이 키를 사용해서 사용자를 식별하고 게임 데이터를 관리할 수 있어요.
 * 게임 카테고리가 아닌 미니앱에서 호출하면 `'INVALID_CATEGORY'`를 반환해요.
 * @returns {Promise<GetUserKeyForGameSuccessResponse | 'INVALID_CATEGORY' | 'ERROR' | undefined>}
 * 사용자 키 조회 결과를 반환해요.
 * - `GetUserKeyForGameSuccessResponse`: 사용자 키 조회에 성공했어요. `{ type: 'HASH', hash: string }` 형태로 반환돼요.
 * - `'INVALID_CATEGORY'`: 게임 카테고리가 아닌 미니앱에서 호출했어요.
 * - `'ERROR'`: 알 수 없는 오류가 발생했어요.
 * - `undefined`: 앱 버전이 최소 지원 버전보다 낮아요.
 *
 * @example
 * ```tsx
 * // react-native
 * import { Button } from 'react-native';
 * import { getUserKeyForGame } from '@apps-in-toss/framework';
 *
 * function GameUserKeyButton() {
 *   async function handlePress() {
 *       const result = await getUserKeyForGame();
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'INVALID_CATEGORY') {
 *         console.error('게임 카테고리가 아닌 미니앱이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('사용자 키 조회 중 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if (result.type === 'HASH') {
 *         console.log('사용자 키:', result.hash);
 *         // 여기에서 사용자 키를 사용해 게임 데이터를 관리할 수 있어요.
 *       }
 *   }
 *
 *   return (
 *     <Button onPress={handlePress} title="유저 키 가져오기" />
 *   );
 * }
 * ```
 *
 * @example
 * ```tsx
 * // webview
 * import { getUserKeyForGame } from '@apps-in-toss/web-framework';
 *
 * function GameUserKeyButton() {
 *   async function handleClick() {
 *       const result = await getUserKeyForGame();
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'INVALID_CATEGORY') {
 *         console.error('게임 카테고리가 아닌 미니앱이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('사용자 키 조회 중 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if (result.type === 'HASH') {
 *         console.log('사용자 키:', result.hash);
 *         // 여기에서 사용자 키를 사용해 게임 데이터를 관리할 수 있어요.
 *       }
 *   }
 *
 *   return (
 *     <button onClick={handleClick}>유저 키 가져오기</button>
 *   );
 * }
 * ```
 */
declare function getUserKeyForGame(): Promise<GetUserKeyForGameSuccessResponse | 'INVALID_CATEGORY' | 'ERROR' | undefined>;

interface GrantPromotionRewardForGameSuccessResponse {
    key: string;
}
interface GrantPromotionRewardForGameErrorResponse {
    code: string;
    [key: string]: any;
}
interface GrantPromotionRewardForGameErrorResult {
    errorCode: string;
    message: string;
}
type GrantPromotionRewardForGameResponse = GrantPromotionRewardForGameSuccessResponse | GrantPromotionRewardForGameErrorResponse;
type GrantPromotionRewardForGameResult = GrantPromotionRewardForGameResponse | GrantPromotionRewardForGameErrorResult | 'ERROR' | undefined;
/**
 * @public
 * @category 게임
 * @name grantPromotionRewardForGame
 * @description
 * 이 함수를 사용하면 게임 카테고리 미니앱에서 프로모션 코드를 사용해서 유저에게 리워드를 지급할 수 있어요.
 * 게임 카테고리가 아닌 미니앱에서 호출할 수 없어요.
 * @param {{ params: { promotionCode: string; amount: number } }} params - 포인트를 지급하기 위해 필요한 정보예요.
 * @param {string} params.promotionCode - 프로모션 코드예요.
 * @param {number} params.amount - 지급할 포인트 금액이에요.
 * @returns {Promise<{ key: string } | { errorCode: string; message: string } | 'ERROR' | undefined>}
 * 포인트 지급 결과를 반환해요.
 * - `{ key: string }`: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요.
 * - `{ errorCode: string, message: string }`: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요.
 *   - `"40000"`: 게임이 아닌 미니앱에서 호출했을 때
 *   - `"4100"`: 프로모션 정보를 찾을 수 없을 때
 *   - `"4104"`: 프로모션이 중지되었을 때
 *   - `"4105"`: 프로모션이 종료되었을 때
 *   - `"4108"`: 프로모션이 승인되지 않았을 때
 *   - `"4109"`: 프로모션이 실행중이 아닐 때
 *   - `"4110"`: 리워드를 지급/회수할 수 없을 때
 *   - `"4112"`: 프로모션 머니가 부족할 때
 *   - `"4113"`: 이미 지급/회수된 내역일 때
 *   - `"4114"`: 프로모션에 설정된 1회 지급 금액을 초과할 때
 * - `'ERROR'`: 알 수 없는 오류가 발생했어요.
 * - `undefined`: 앱 버전이 최소 지원 버전보다 낮아요.
 * @example
 * ```tsx
 * // react-native
 * import { Button } from 'react-native';
 * import { grantPromotionRewardForGame } from '@apps-in-toss/framework';
 *
 * function GrantRewardButton() {
 *   async function handlePress() {
 *       const result = await grantPromotionRewardForGame({
 *         params: {
 *           promotionCode: 'GAME_EVENT_2024',
 *           amount: 1000,
 *         },
 *       });
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('포인트 지급 중 알 수 없는 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if ('key' in result) {
 *         console.log('포인트 지급 성공!', result.key);
 *       } else if ('errorCode' in result) {
 *         console.error('포인트 지급 실패:', result.errorCode, result.message);
 *       }
 *   }
 *
 *   return <Button onPress={handlePress} title="포인트 지급하기" />;
 * }
 * ```
 *
 * @example
 * ```tsx
 * // webview
 * import { grantPromotionRewardForGame } from '@apps-in-toss/web-framework';
 *
 * function GrantRewardButton() {
 *   async function handleClick() {
 *       const result = await grantPromotionRewardForGame({
 *         params: {
 *           promotionCode: 'GAME_EVENT_2024',
 *           amount: 1000,
 *         },
 *       });
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('포인트 지급 중 알 수 없는 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if ('key' in result) {
 *         console.log('포인트 지급 성공!', result.key);
 *       } else if ('errorCode' in result) {
 *         console.error('포인트 지급 실패:', result.errorCode, result.message);
 *       }
 *   }
 *
 *   return (
 *     <button onClick={handleClick}>포인트 지급하기</button>
 *   );
 * }
 * ```
 */
declare function grantPromotionRewardForGame({ params, }: {
    params: {
        promotionCode: string;
        amount: number;
    };
}): Promise<GrantPromotionRewardForGameResult>;

type Sku = {
    /**
     * @deprecated `productId`는 더 이상 사용하지 않아요. 대신 `sku`를 사용해요.
     */
    productId: string;
    sku?: string;
} | {
    productId?: never;
    sku: string;
};
/**
 * @public
 * @category 인앱결제
 * @name IapCreateOneTimePurchaseOrderResult
 * @description 인앱결제 1건이 완료되면 결제 세부 정보와 상품 정보를 담아 반환해요. 반환된 정보로 결제한 상품의 정보를 화면에 표시할 수 있어요.
 * @property {string} orderId - 결제 주문 ID이에요. 결제 완료 후 [결제 상태를 조회](https://developers-apps-in-toss.toss.im/api/getIapOrderStatus.html)할 때 사용해요.
 * @property {string} displayName - 화면에 표시할 상품 이름이에요.
 * @property {string} displayAmount - 통화 단위가 포함된 가격 정보예요. 예를 들어 `1,000원`으로 가격과 통화가 함께 표시돼요.
 * @property {number} amount - 상품 가격 숫자 값이에요. 화폐 단위와 쉼표를 제외한 순수 숫자예요. 예를 들어 `1000`으로 표시돼요.
 * @property {string} currency - [ISO 4217 표준](https://ko.wikipedia.org/wiki/ISO_4217)에 따른 상품 가격 통화 단위예요. 예를 들어 원화는 `KRW`, 달러는 `USD`로 표시돼요.
 * @property {number} fraction - 가격을 표시할 때 소수점 아래 몇 자리까지 보여줄지 정하는 값이에요. 예를 들어 달러는 소수점 둘째 자리까지 보여줘서 `2`, 원화는 소수점이 필요 없어서 `0`이에요
 * @property {string | null} miniAppIconUrl - 미니앱 아이콘 이미지의 URL이에요. 아이콘은 앱인토스 콘솔에서 설정한 이미지예요. 콘솔에서 아이콘을 등록하지 않았다면 `null`로 반환돼요.
 */
interface IapCreateOneTimePurchaseOrderResult {
    orderId: string;
    displayName: string;
    displayAmount: string;
    amount: number;
    currency: string;
    fraction: number;
    miniAppIconUrl: string | null;
}
interface SuccessEvent {
    type: 'success';
    data: IapCreateOneTimePurchaseOrderResult;
}
interface PurchasedEvent {
    type: 'purchased';
    data: {
        orderId: string;
    };
}
/**
 * @public
 * @category 인앱결제
 * @name IapCreateOneTimePurchaseOrderOptions
 * @property {Sku} options - 결제할 상품의 정보예요.
 * @property {string} options.sku - 주문할 상품의 고유 ID예요.
 * @property {(params: { orderId: string }) => boolean | Promise<boolean>} processProductGrant - 주문이 만들어진 뒤 실제로 상품을 지급할 때 호출해요. `orderId`를 받아서 지급 성공 여부를 `true` 또는 `Promise<true>`로 반환해요. 지급에 실패하면 `false`를 반환해요.
 * @property {(event: SuccessEvent) => void | Promise<void>} onEvent - 결제가 성공했을 때 호출해요. `event.type`이 `'success'`이고, `event.data`에 `IapCreateOneTimePurchaseOrderResult`가 들어 있어요.
 * @property {(error: unknown) => void | Promise<void>} onError - 결제 과정에서 에러가 발생했을 때 호출해요. 에러 객체를 받아서 로깅하거나 복구 절차를 실행할 수 있어요.
 */
interface IapCreateOneTimePurchaseOrderOptions {
    options: Sku & {
        processProductGrant: (params: {
            orderId: string;
        }) => boolean | Promise<boolean>;
    };
    onEvent: (event: SuccessEvent) => void | Promise<void>;
    onError: (error: unknown) => void | Promise<void>;
}
interface IapRequestOneTimePurchaseOptions {
    options: Sku;
    onEvent: (event: PurchasedEvent | SuccessEvent) => void | Promise<void>;
    onError: (error: unknown) => void | Promise<void>;
}
declare function iapCreateOneTimePurchaseOrder(params: Sku): Promise<IapCreateOneTimePurchaseOrderResult>;
declare function processProductGrant(params: {
    orderId: string;
    isProductGranted: boolean;
}): Promise<void>;
declare function requestOneTimePurchase(params: IapRequestOneTimePurchaseOptions): () => void;
/**
 * @public
 * @category 인앱결제
 * @name createOneTimePurchaseOrder
 * @description
 * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
 * @param {IapCreateOneTimePurchaseOrderOptions} params - 인앱결제를 생성할 때 필요한 정보예요.
 * @returns {() => void} 앱브릿지 cleanup 함수를 반환해요. 인앱결제 기능이 끝나면 반드시 이 함수를 호출해서 리소스를 해제해야 해요.
 *
 * @throw {code: "INVALID_PRODUCT_ID"} - 유효하지 않은 상품 ID이거나, 해당 상품이 존재하지 않을 때 발생해요.
 * @throw {code: "PAYMENT_PENDING"} - 사용자가 요청한 결제가 아직 승인을 기다리고 있을 때 발생해요.
 * @throw {code: "NETWORK_ERROR"} - 서버 내부 문제로 요청을 처리할 수 없을 때 발생해요.
 * @throw {code: "INVALID_USER_ENVIRONMENT"} - 특정 기기, 계정 또는 설정 환경에서 구매할 수 없는 상품일 때 발생해요.
 * @throw {code: "ITEM_ALREADY_OWNED"} - 사용자가 이미 구매한 상품을 다시 구매하려고 할 때 발생해요.
 * @throw {code: "APP_MARKET_VERIFICATION_FAILED"} - 사용자가 결제를 완료했지만, 앱스토어에서 사용자 정보 검증에 실패했을 때 발생해요. 사용자가 앱스토어에 문의해서 환불을 요청해야해요.
 * @throw {code: "TOSS_SERVER_VERIFICATION_FAILED"} - 사용자가 결제를 완료했지만, 서버 전송에 실패해서 결제 정보를 저장할 수 없을 때 발생해요.
 * @throw {code: "INTERNAL_ERROR"} - 서버 내부 문제로 요청을 처리할 수 없을 때 발생해요.
 * @throw {code: "KOREAN_ACCOUNT_ONLY"} - iOS 환경에서 사용자의 계정이 한국 계정이 아닐 때 발생해요.
 * @throw {code: "USER_CANCELED"} - 사용자가 결제를 완료하지 않고 주문서 페이지를 이탈했을 때 발생해요.
 * @throw {code: "PRODUCT_NOT_GRANTED_BY_PARTNER"} - 파트너사의 상품 지급이 실패했을 때 발생해요.
 *
 * @example
 * ### 특정 인앱결제 주문서 페이지로 이동하기
 *
 * ```tsx
 * import { IAP } from "@apps-in-toss/web-framework";
 * import { Button } from "@toss/tds-react-native";
 * import { useCallback } from "react";
 *
 * interface Props {
 *   sku: string;
 * }
 *
 * function IapCreateOneTimePurchaseOrderButton({ sku }: Props) {
 *   const handleClick = useCallback(async () => {
 *
 *     const cleanup = await IAP.createOneTimePurchaseOrder({
 *       options: {
 *        sku,
 *        processProductGrant: ({ orderId }) => {
 *         // 상품 지급 로직 작성
 *          return true; // 상품 지급 여부
 *        }
 *        },
 *       onEvent: (event) => {
 *         console.log(event);
 *       },
 *       onError: (error) => {
 *         console.error(error);
 *       },
 *     });
 *
 *     return cleanup;
 *   }, []);
 *
 *   return <Button onClick={handleClick}>구매하기</Button>;
 * }
 * ```
 */
declare function createOneTimePurchaseOrder(params: IapCreateOneTimePurchaseOrderOptions): () => void;
/**
 * @public
 * @category 인앱결제
 * @name IapProductListItem
 * @description 인앱결제로 구매할 수 있는 상품 하나의 정보를 담은 객체예요. 상품 목록을 화면에 표시할 때 사용해요.
 * @property {string} sku - 상품의 고유 ID예요.
 * @property {string} displayName - 화면에 표시할 상품 이름이에요. 상품 이름은 앱인토스 콘솔에서 설정한 값이에요.
 * @property {string} displayAmount - 통화 단위가 포함된 가격 정보예요. 예를 들어 `1,000원`으로 가격과 통화가 함께 표시돼요.
 * @property {string} iconUrl - 상품 아이콘 이미지의 URL이에요. 아이콘은 앱인토스 콘솔에서 설정한 이미지예요.
 * @property {string} description - 상품에 대한 설명이에요. 설명은 앱인토스 콘솔에서 설정한 값이에요.
 */
interface IapProductListItem {
    sku: string;
    displayAmount: string;
    displayName: string;
    iconUrl: string;
    description: string;
}
/**
 * @public
 * @category 인앱결제
 * @name getProductItemList
 * @description 인앱결제로 구매할 수 있는 상품 목록을 가져와요. 상품 목록 화면에 진입할 때 호출해요.
 * @returns {Promise<{ products: IapProductListItem[] } | undefined>} 상품 목록을 포함한 객체를 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.219.0, iOS 5.219.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ### 구매 가능한 인앱결제 상품목록 가져오기
 *
 * ```tsx
 * import { IAP, IapProductListItem } from "@apps-in-toss/framework";
 * import { Button, List, ListRow } from "@toss/tds-react-native";
 * import { useEffect, useState } from "react";
 *
 * function IapProductList() {
 *   const [products, setProducts] = useState<IapProductListItem[]>([]);
 *
 *   async function buyIapProduct(productId: string) {
 *     try {
 *       await IAP.createOneTimePurchaseOrder({
 *         productId,
 *       });
 *
 *       console.error("인앱결제에 성공했어요");
 *     } catch (error) {
 *       console.error("인앱결제에 실패했어요:", error);
 *     }
 *   }
 *
 *   useEffect(() => {
 *     async function fetchProducts() {
 *       try {
 *         const response = await IAP.getProductItemList();
 *         setProducts(response?.products ?? []);
 *       } catch (error) {
 *         console.error("상품 목록을 가져오는 데 실패했어요:", error);
 *       }
 *     }
 *
 *     fetchProducts();
 *   }, []);
 *
 *   return (
 *     <List>
 *       {products.map((product) => (
 *         <ListRow
 *           key={product.sku}
 *           left={
 *             <ListRow.Image type="square" source={{ uri: product.iconUrl }} />
 *           }
 *           right={
 *             <Button size="medium" onPress={() => buyIapProduct(product.sku)}>
 *               구매하기
 *             </Button>
 *           }
 *           contents={
 *             <ListRow.Texts
 *               type="3RowTypeA"
 *               top={product.displayName}
 *               middle={product.description}
 *               bottom={product.displayAmount}
 *             />
 *           }
 *         />
 *       ))}
 *     </List>
 *   );
 * }
 * ```
 */
declare function getProductItemList(): Promise<{
    products: IapProductListItem[];
} | undefined>;
/**
 * @public
 * @category 인앱결제
 * @name getPendingOrders
 * @description 대기 중인 주문 목록을 가져와요. 이 함수를 사용하면 결제가 아직 완료되지 않은 주문 정보를 확인할 수 있어요.
 * @returns {Promise<{ orders: { orderId: string; sku: string; paymentCompletedDate: string; }[]}>} 대기 중인 주문의 배열을 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.234.0, iOS 5.231.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ### 대기 중인 주문 목록 가져오기
 * ```typescript
 * import { IAP } from '@apps-in-toss/framework';
 *
 * async function fetchOrders() {
 *   try {
 *     const pendingOrders = await IAP.getPendingOrders();
 *     return pendingOrders;
 *   } catch (error) {
 *     console.error(error);
 *   }
 * }
 * ```
 */
declare function getPendingOrders(): Promise<{
    orders: {
        orderId: string;
        sku: string;
        paymentCompletedDate: string;
    }[];
} | undefined>;
/**
 * @public
 * @category 인앱결제
 * @name CompletedOrRefundedOrdersResult
 * @description 인앱결제로 구매하거나 환불한 주문 목록을 나타내는 객체예요. 페이지네이션 정보를 포함해요.
 * @property {boolean} hasNext 다음 페이지가 있는지 여부예요. `true`면 더 많은 주문이 남아 있어요.
 * @property {string | null} [nextKey] 다음 주문 목록을 조회할 때 사용할 키예요. 마지막 페이지라면 `null`이거나 생략될 수 있어요.
 * @property {Array} orders 주문 정보를 담은 배열이에요. 각 요소는 하나의 주문을 나타내요.
 * @property {string} orders[].orderId 주문의 고유 ID예요.
 * @property {string} orders[].sku 주문 상품의 고유 ID예요.
 * @property {'COMPLETED' | 'REFUNDED'} orders[].status 주문의 상태예요. 'COMPLETED'는 주문이 완료된 상태, 'REFUNDED'는 환불된 상태를 의미해요.
 * @property {string} orders[].date 주문의 날짜 정보예요. ISO 8601 형식(YYYY-MM-DDTHH:mm:ss)을 사용해요. 예를 들어 "2025-09-22T00:00:00" 형식으로 제공돼요. 주문 상태가 `COMPLETED`라면 주문한 날짜를, `REFUNDED`라면 환불한 날짜를 나타내요.
 */
interface CompletedOrRefundedOrdersResult {
    hasNext: boolean;
    nextKey?: string | null;
    orders: {
        orderId: string;
        sku: string;
        status: 'COMPLETED' | 'REFUNDED';
        date: string;
    }[];
}
/**
 * @public
 * @category 인앱결제
 * @name getCompletedOrRefundedOrders
 * @description 인앱결제로 구매하거나 환불한 주문 목록을 가져와요.
 * @returns {Promise<CompletedOrRefundedOrdersResult>} 페이지네이션을 포함한 주문 목록 객체를 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.231.0, iOS 5.231.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ```typescript
 * import { IAP } from "@apps-in-toss/framework";
 *
 * async function fetchOrders() {
 *   try {
 *     const response =  await IAP.getCompletedOrRefundedOrders();
 *     return response;
 *   } catch (error) {
 *     console.error(error);
 *   }
 * }
 * ```
 */
declare function getCompletedOrRefundedOrders(params?: {
    key?: string | null;
}): Promise<CompletedOrRefundedOrdersResult | undefined>;
/**
 * @public
 * @category 인앱결제
 * @name completeProductGrant
 * @description 상품 지급 처리를 완료했다는 메시지를 앱에 전달해요. 이 함수를 사용하면 결제가 완료된 주문의 상품 지급이 정상적으로 완료되었음을 알릴 수 있어요.
 * @param {{ params: { orderId: string } }} params 결제가 완료된 주문 정보를 담은 객체예요.
 * @param {string} params.orderId 주문의 고유 ID예요. 상품 지급을 완료할 주문을 지정할 때 사용해요.
 * @returns {Promise<boolean>} 상품 지급이 완료됐는지 여부를 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.233.0, iOS 5.233.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ### 결제를 성공한 뒤 상품을 지급하는 예시
 * ```typescript
 * import { IAP } from '@apps-in-toss/framework';
 *
 * async function handleGrantProduct(orderId: string) {
 *   try {
 *     await IAP.completeProductGrant({ params: { orderId } });
 *   } catch (error) {
 *     console.error(error);
 *   }
 * }
 * ```
 */
declare function completeProductGrant(params: {
    params: {
        orderId: string;
    };
}): Promise<boolean | undefined>;
/**
 * @public
 * @category 인앱결제
 * @name IAP
 * @description 인앱결제 관련 기능을 모은 객체예요. 단건 인앱결제 주문서 이동과 상품 목록 조회 기능을 제공해요.
 * @property {typeof createOneTimePurchaseOrder} [createOneTimePurchaseOrder] 특정 인앱결제 주문서 페이지로 이동해요. 자세한 내용은 [createOneTimePurchaseOrder](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/createOneTimePurchaseOrder.html) 문서를 참고하세요.
 * @property {typeof getProductItemList} [getProductItemList] 인앱결제로 구매할 수 있는 상품 목록을 가져와요. 자세한 내용은 [getProductItemList](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/getProductItemList.html) 문서를 참고하세요.
 * @property {typeof getPendingOrders} [getPendingOrders] 대기 중인 주문 목록을 가져와요. 자세한 내용은 [getPendingOrders](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/getPendingOrders.htm) 문서를 참고하세요.
 * @property {typeof getCompletedOrRefundedOrders} [getCompletedOrRefundedOrders] 인앱결제로 구매하거나 환불한 주문 목록을 가져와요. 자세한 내용은 [getCompletedOrRefundedOrders](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/getCompletedOrRefundedOrders.html) 문서를 참고하세요.
 * @property {typeof completeProductGrant} [completeProductGrant] 상품 지급 처리를 완료했다는 메시지를 앱에 전달해요. 자세한 내용은 [completeProductGrant](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/completeProductGrant.html) 문서를 참고하세요.
 */
declare const IAP: {
    createOneTimePurchaseOrder: typeof createOneTimePurchaseOrder;
    getProductItemList: typeof getProductItemList;
    getPendingOrders: typeof getPendingOrders;
    getCompletedOrRefundedOrders: typeof getCompletedOrRefundedOrders;
    completeProductGrant: typeof completeProductGrant;
};

interface SaveBase64DataParams {
    data: string;
    fileName: string;
    mimeType: string;
}
/**
 * @public
 * @category 데이터
 * @name saveBase64Data
 * @description 문자열로 인코딩된 Base64 데이터를 지정한 파일 이름과 MIME 타입으로 사용자 기기에 저장해요. 이미지, 텍스트, PDF 등 다양한 형식의 데이터를 저장할 수 있어요.
 * @param {SaveBase64DataParams} params - 저장할 데이터와 파일 정보를 담은 객체예요.
 * @param {string} params.data - Base64 형식으로 인코딩된 데이터 문자열이에요.
 * @param {string} params.fileName - 저장할 파일 이름이에요. 확장자도 같이 붙여줘야해요. 예를 들어, 'example.png'로 저장할 수 있어요.
 * @param {string} params.mimeType - 저장할 파일의 MIME 타입이에요. 예를 들어 'image/png' 로 지정하면 이미지, 'application/pdf'는 PDF 파일이에요. 자세한 내용은 [MIME 문서](https://developer.mozilla.org/ko/docs/Web/HTTP/Guides/MIME_types)를 참고해주세요.
 *
 * @example
 * ### Base64 이미지 데이터를 사용자 기기에 저장하기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { saveBase64Data } from '@apps-in-toss/framework';
 *
 * // '저장' 버튼을 누르면 이미지가 사용자 기기에 저장돼요.
 * function SaveButton() {
 *   const handleSave = async () => {
 *     try {
 *       await saveBase64Data({
 *         data: 'iVBORw0KGgo...',
 *         fileName: 'some-photo.png',
 *         mimeType: 'image/png',
 *       });
 *     } catch (error) {
 *       console.error('데이터 저장에 실패했어요:', error);
 *     }
 *   };
 *
 *   return <Button title="저장" onPress={handleSave} />;
 * }
 * ```
 */
declare function saveBase64Data(params: SaveBase64DataParams): Promise<void>;

/**
 * @public
 * @category 게임센터
 * @name SubmitGameCenterLeaderBoardScoreResponse
 * @description
 * 토스게임센터 리더보드에 점수를 제출한 결과 정보를 담아서 반환해요. 반환된 정보를 사용해서 점수 제출 결과에 따라 적절한 에러 처리를 할 수 있어요.
 * @property {'SUCCESS' | 'LEADERBOARD_NOT_FOUND' | 'PROFILE_NOT_FOUND' | 'UNPARSABLE_SCORE'} statusCode
 * 점수 제출 결과를 나타내는 상태 코드예요.
 * - `'SUCCESS'`: 점수 제출이 성공했어요.
 * - `'LEADERBOARD_NOT_FOUND'`: `gameId`에 해당하는 리더보드를 찾을 수 없어요.
 * - `'PROFILE_NOT_FOUND'`: 사용자의 프로필이 없어요.
 * - `'UNPARSABLE_SCORE'`: 점수를 해석할 수 없어요. 점수는 실수(float) 형태의 문자열로 전달해야 해요.
 */
interface SubmitGameCenterLeaderBoardScoreResponse {
    statusCode: 'SUCCESS' | 'LEADERBOARD_NOT_FOUND' | 'PROFILE_NOT_FOUND' | 'UNPARSABLE_SCORE';
}
/**
 * @public
 * @category 게임센터
 * @name submitGameCenterLeaderBoardScore
 * @description
 * 사용자의 게임 점수를 토스게임센터 리더보드에 제출해요. 이 기능으로 사용자의 점수를 공식 리더보드에 기록하고 다른 사용자와 비교할 수 있어요.
 * @param {string} params.score
 * 제출할 게임 점수예요. 실수 형태의 숫자를 문자열로 전달해야 해요. 예를들어 `"123.45"` 또는 `"9999"` 예요.
 * @returns {Promise<SubmitGameCenterLeaderBoardScoreResponse | undefined>}
 * 점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
 *
 * @example
 * ### 게임 점수를 토스게임센터 리더보드에 제출하기
 * ```tsx
 * import { Button } from 'react-native';
 * import { submitGameCenterLeaderBoardScore } from '@apps-in-toss/framework';
 *
 * function GameCenterLeaderBoardScoreSubmitButton() {
 *   async function handlePress() {
 *     try {
 *       const result = await submitGameCenterLeaderBoardScore({ score: '123.45' });
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result.statusCode === 'SUCCESS') {
 *         console.log('점수 제출 성공!');
 *       } else {
 *         console.error('점수 제출 실패:', result.statusCode);
 *       }
 *     } catch (error) {
 *       console.error('점수 제출 중 오류가 발생했어요.', error);
 *     }
 *   }
 *
 *   return (
 *     <Button onPress={handlePress}>점수 제출하기</Button>
 *   );
 * }
 * ```
 */
declare function submitGameCenterLeaderBoardScore(params: {
    score: string;
}): Promise<SubmitGameCenterLeaderBoardScoreResponse | undefined>;

/**
 * TurboModule 타입 별칭 사용하는 이유?
 * React Native Codegen 에 의해 코드젠 되는 것이 아니라 추후 내부 모듈 체계에 의해 처리될 것이기 때문에 RN Codegen에 본 파일을 코드젠 하지 않도록 함
 * (코드젠 내부에서 "extends TurboModule" 문자열을 찾기 때문에 패턴에 매칭되지 않도록 함)
 */
interface Spec extends TurboModule {
    groupId: string;
    operationalEnvironment: 'sandbox' | 'toss';
    tossAppVersion: string;
    deviceId: string;
    getClipboardText: (arg: CompatiblePlaceholderArgument) => Promise<string>;
    setClipboardText: (option: SetClipboardTextOptions) => Promise<void>;
    fetchContacts: (option: FetchContactsOptions) => Promise<ContactResult>;
    fetchAlbumPhotos: (options: FetchAlbumPhotosOptions) => Promise<ImageResponse[]>;
    getCurrentLocation: (options: GetCurrentLocationOptions) => Promise<Location>;
    openCamera: (options: OpenCameraOptions) => Promise<ImageResponse>;
    getWebBundleURL: (arg: CompatiblePlaceholderArgument) => {
        url: string;
    };
    getPermission: (permission: {
        name: PermissionName;
        access: PermissionAccess;
    }) => Promise<PermissionStatus>;
    openPermissionDialog: (permission: {
        name: PermissionName;
        access: PermissionAccess;
    }) => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
    appLogin: (arg: CompatiblePlaceholderArgument) => Promise<{
        authorizationCode: string;
        referrer: 'DEFAULT' | 'SANDBOX';
    }>;
    checkoutPayment: (options: {
        params: CheckoutPaymentOptions;
    }) => Promise<CheckoutPaymentResult>;
    /** Storage */
    getStorageItem: (params: {
        key: string;
    }) => Promise<string | null>;
    setStorageItem: (params: {
        key: string;
        value: string;
    }) => Promise<void>;
    removeStorageItem: (params: {
        key: string;
    }) => Promise<void>;
    clearStorage: (arg: CompatiblePlaceholderArgument) => Promise<void>;
    eventLog: (params: {
        log_name: string;
        log_type: 'debug' | 'info' | 'warn' | 'error' | 'event' | 'screen' | 'impression' | 'click';
        params: Record<string, string>;
    }) => Promise<void>;
    getTossShareLink: (params: object) => Promise<{
        shareLink: string;
    }>;
    setDeviceOrientation: (options: {
        type: 'portrait' | 'landscape';
    }) => Promise<void>;
    saveBase64Data: (params: SaveBase64DataParams) => Promise<void>;
    /** IAP */
    iapGetProductItemList: (arg: CompatiblePlaceholderArgument) => Promise<{
        products: IapProductListItem[];
    }>;
    /** @deprecated `requestOneTimePurchase`를 사용해주세요. */
    iapCreateOneTimePurchaseOrder: (params: {
        productId: string;
    }) => Promise<IapCreateOneTimePurchaseOrderResult>;
    requestOneTimePurchase: (params: {
        sku: string;
    }, fallbacks: {
        onPurchased: (params: {
            orderId: string;
        }) => void;
    }) => () => void;
    processProductGrant: (params: {
        orderId: string;
        isProductGranted: boolean;
    }) => Promise<void>;
    getPendingOrders: (params: CompatiblePlaceholderArgument) => Promise<{
        orders: {
            orderId: string;
            sku: string;
            paymentCompletedDate: string;
        }[];
    }>;
    getCompletedOrRefundedOrders: (params: {
        key?: string | null;
    }) => Promise<CompletedOrRefundedOrdersResult>;
    completeProductGrant: (params: {
        params: {
            orderId: string;
        };
    }) => Promise<boolean>;
    getGameCenterGameProfile: (params: CompatiblePlaceholderArgument) => Promise<GameCenterGameProfileResponse>;
    getUserKeyForGame: (params: CompatiblePlaceholderArgument) => Promise<GetUserKeyForGameResponse>;
    getIsTossLoginIntegratedService: (params: CompatiblePlaceholderArgument) => Promise<boolean>;
    grantPromotionRewardForGame: (params: {
        params: {
            promotionCode: string;
            amount: number;
        };
    }) => Promise<GrantPromotionRewardForGameResponse>;
    submitGameCenterLeaderBoardScore: (params: {
        score: string;
    }) => Promise<SubmitGameCenterLeaderBoardScoreResponse>;
    contactsViral: (params: ContactsViralParams) => () => void;
    /** 토스인증 */
    appsInTossSignTossCert: (params: {
        params: AppsInTossSignTossCertParams;
    }) => void;
}
declare const AppsInTossModuleInstance: any;
declare const AppsInTossModule: Spec;

/**
 * @public
 * @category 로그인
 * @name appLogin
 * @description 토스 인증으로 로그인해요. 로그인이 완료되면 다시 토스 앱으로 이동해요.
 * @example
 *
 * ### 토스 인증을 통해 로그인을 하는 예제
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { appLogin } from '@apps-in-toss/framework';
 *
 * function Page() {
 *   const handleLogin = async () => {
 *     const { authorizationCode, referrer } = await appLogin();
 *
 *     // 획득한 인가 코드(`authorizationCode`)와 `referrer`를 서버로 전달해요.
 *   }
 *
 *   return <Button title="로그인" onPress={handleLogin} />;
 * }
 * ```
 */
declare function appLogin(): Promise<{
    authorizationCode: string;
    referrer: 'DEFAULT' | 'SANDBOX';
}>;

type Primitive = string | number | boolean | null | undefined | symbol;

interface EventLogParams {
    log_name: string;
    log_type: 'debug' | 'info' | 'warn' | 'error' | 'event' | 'screen' | 'impression' | 'click';
    params: Record<string, Primitive>;
}
/**
 * @category 로깅
 * @kind function
 * @name eventLog
 * @description
 * 이벤트 로그를 기록하는 함수예요.
 *
 * 이 함수는 앱 내에서 발생하는 다양한 이벤트를 로깅하는 데 사용돼요. 디버깅, 정보 제공, 경고, 오류 등 다양한 유형의 로그를 기록할 수 있어요. 샌드박스 환경에서는 콘솔에 로그가 출력되고, 실제 환경에서는 로그 시스템에 기록돼요.
 *
 * @param {Object} params 로그 기록에 필요한 매개변수 객체예요.
 * @param {string} params.log_name 로그의 이름이에요.
 * @param {'debug' | 'info' | 'warn' | 'error' | 'event' | 'screen' | 'impression' | 'click'} params.log_type 로그의 유형이에요.
 * @param {Record<string, Primitive>} params.params 로그에 포함할 추가 매개변수 객체예요.
 *
 * @returns {Promise<void>} 로그 기록이 완료되면 해결되는 Promise예요.
 *
 * @example
 * ### 이벤트 로그 기록하기
 *
 * ```tsx
 * import { eventLog } from '@apps-in-toss/framework';
 *
 * function logUserAction() {
 *   eventLog({
 *     log_name: 'user_action',
 *     log_type: 'info',
 *     params: {
 *       action: 'button_click',
 *       screen: 'main',
 *       userId: 12345
 *     }
 *   });
 * }
 * ```
 */
declare function eventLog(params: EventLogParams): Promise<void>;

/**
 * @public
 * @category 사진
 * @name fetchAlbumPhotos
 * @description
 * 사용자의 앨범에서 사진 목록을 불러오는 함수예요.
 * 최대 개수와 해상도를 설정할 수 있고 갤러리 미리보기, 이미지 선택 기능 등에 활용할 수 있어요.
 *
 * @param options - 조회 옵션을 담은 객체예요.
 * @param {number} [options.maxCount=10] 가져올 사진의 최대 개수를 설정해요. 숫자로 입력하며 기본값은 10이에요.
 * @param {number} [options.maxWidth=1024] 사진의 최대 폭을 제한해요. 단위는 픽셀이며 기본값은 `1024`이에요.
 * @param {boolean} [options.base64=false] 이미지를 base64 형식으로 반환할지 설정해요. 기본값은 `false`예요.
 * @property [openPermissionDialog] - 사진첩 읽기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 사진첩 읽기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 사진첩 읽기 권한을 허용한 상태예요. `denied`는 사용자가 사진첩 읽기 권한을 거부한 상태예요. `notDetermined`는 사진첩 읽기 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<ImageResponse[]>}
 * 앨범 사진의 고유 ID와 데이터 URI를 포함한 배열을 반환해요.
 *
 * @signature
 * ```typescript
 * function fetchAlbumPhotos(options: {
 *   maxCount: number;
 *   maxWidth: number;
 *   base64: boolean;
 * }): Promise<ImageResponse[]>;
 * ```
 *
 * @example
 * ### 사진의 최대 폭을 360px로 제한하여 가져오기
 *
 * 사진을 가져오는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 사진첩 읽기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`FetchAlbumPhotosPermissionError`](/react-native/reference/types/권한/FetchAlbumPhotosPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 사진첩 읽기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { fetchAlbumPhotos, FetchAlbumPhotosPermissionError, ImageResponse } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Image, View } from 'react-native';
 *
 * const base64 = true;
 *
 * // 앨범 사진 목록을 가져와 화면에 표시하는 컴포넌트
 * function AlbumPhotoList() {
 *   const [albumPhotos, setAlbumPhotos] = useState<ImageResponse[]>([]);
 *
 *   const handlePress = async () => {
 *     try {
 *       const response = await fetchAlbumPhotos({
 *         base64,
 *         maxWidth: 360,
 *       });
 *       setAlbumPhotos((prev) => [...prev, ...response]);
 *     } catch (error) {
 *       if (error instanceof FetchAlbumPhotosPermissionError) {
 *         // 앨범 읽기 권한 없음
 *       }
 *       console.error('앨범을 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       {albumPhotos.map((image) => {
 *         // base64 형식으로 반환된 이미지를 표시하려면 데이터 URL 스키마 Prefix를 붙여야해요.
 *         const imageUri = base64 ? 'data:image/jpeg;base64,' + image.dataUri : image.dataUri;
 *
 *         return <Image source={{ uri: imageUri }} key={image.id} />;
 *       })}
 *       <Button title="앨범 가져오기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await fetchAlbumPhotos.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await fetchAlbumPhotos.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
declare const fetchAlbumPhotos: _apps_in_toss_types.PermissionFunctionWithDialog<FetchAlbumPhotos>;

/**
 * @public
 * @category 연락처
 * @name fetchContacts
 * @description 사용자의 연락처 목록을 페이지 단위로 가져오는 함수예요.
 * @param options - 연락처를 가져올 때 지정하는 옵션 객체예요.
 * @param options.size - 한 번에 가져올 연락처 개수예요. 예를 들어, 10을 전달하면 최대 10개의 연락처를 가져와요.
 * @param options.offset - 가져올 연락처의 시작 지점이에요. 처음 호출할 때는 `0`을 전달해야 해요. 이후에는 이전 호출에서 반환된 `nextOffset` 값을 사용해요.
 * @param options.query - 추가적인 필터링 옵션이에요.
 * @param options.query.contains - 이름에 특정 문자열이 포함된 연락처만 가져오고 싶을 때 사용해요. 이 값을 전달하지 않으면 모든 연락처를 가져와요.
 * @property [openPermissionDialog] - 연락처 읽기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 연락처 읽기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 연락처 읽기 권한을 허용한 상태예요. `denied`는 사용자가 연락처 읽기 권한을 거부한 상태예요. `notDetermined`는 연락처 읽기 권한 요청을 한 번도 하지 않은 상태예요.
 *
 * @returns {Promise<ContactResult>}
 * 연락처 목록과 페이지네이션 정보를 포함한 객체를 반환해요.
 * - `result`: 가져온 연락처 목록이에요.
 * - `nextOffset`: 다음 호출에 사용할 오프셋 값이에요. 더 가져올 연락처가 없으면 `null`이에요.
 * - `done`: 모든 연락처를 다 가져왔는지 여부를 나타내요. 모두 가져왔다면 `true`예요.
 *
 * @signature
 * ```typescript
 * function fetchContacts(options: {
 *   size: number;
 *   offset: number;
 *   query?: {
 *     contains?: string;
 *   };
 * }): Promise<ContactResult>;
 * ```
 *
 * @example
 * ### 특정 문자열이 포함된 연락처 목록 가져오기
 *
 * 연락처 목록을 가져오는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 연락처 읽기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`FetchContactsPermissionError`](/react-native/reference/types/권한/FetchContactsPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 연락처 읽기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { ContactEntity, fetchContacts, FetchContactsPermissionError } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Text, View } from 'react-native';
 *
 * // 특정 문자열을 포함한 연락처 목록을 가져와 화면에 표시하는 컴포넌트
 * function ContactsList() {
 *   const [contacts, setContacts] = useState<{
 *     result: ContactEntity[];
 *     nextOffset: number | null;
 *     done: boolean;
 *   }>({
 *     result: [],
 *     nextOffset: null,
 *     done: false,
 *   });
 *
 *   const handlePress = async () => {
 *     try {
 *       if (contacts.done) {
 *         console.log('모든 연락처를 가져왔어요.');
 *         return;
 *       }
 *
 *       const response = await fetchContacts({
 *         size: 10,
 *         offset: contacts.nextOffset ?? 0,
 *         query: { contains: '김' },
 *       });
 *       setContacts((prev) => ({
 *         result: [...prev.result, ...response.result],
 *         nextOffset: response.nextOffset,
 *         done: response.done,
 *       }));
 *     } catch (error) {
 *       if (error instanceof FetchContactsPermissionError) {
 *         // 연락처 읽기 권한 없음
 *       }
 *       console.error('연락처를 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       {contacts.result.map((contact, index) => (
 *         <Text key={index}>
 *           {contact.name}: {contact.phoneNumber}
 *         </Text>
 *       ))}
 *       <Button
 *         title={contacts.done ? '모든 연락처를 가져왔어요.' : '다음 연락처 가져오기'}
 *         disabled={contacts.done}
 *         onPress={handlePress}
 *       />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await fetchContacts.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await fetchContacts.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
declare const fetchContacts: _apps_in_toss_types.PermissionFunctionWithDialog<FetchContacts>;

/**
 * @public
 * @category 클립보드
 * @name getClipboardText
 * @description 클립보드에 저장된 텍스트를 가져오는 함수예요. 복사된 텍스트를 읽어서 다른 작업에 활용할 수 있어요.
 * @property [openPermissionDialog] - 클립보드 읽기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 클립보드 읽기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 클립보드 읽기 권한을 허용한 상태예요. `denied`는 사용자가 클립보드 읽기 권한을 거부한 상태예요. `notDetermined`는 클립보드 읽기 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<string>} - 클립보드에 저장된 텍스트를 반환해요. 클립보드에 텍스트가 없으면 빈 문자열을 반환해요.
 *
 * @signature
 * ```typescript
 * function getClipboardText(): Promise<string>;
 * ```
 *
 * @example
 * ### 클립보드의 텍스트 가져오기
 *
 * 클립보드의 텍스트를 가져오는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 클립보드 읽기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`GetClipboardTextPermissionError`](/react-native/reference/types/권한/GetClipboardTextPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 클립보드 읽기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import {
 *   getClipboardText,
 *   GetClipboardTextPermissionError,
 *   SetClipboardTextPermissionError,
 * } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Text, View } from 'react-native'; *

 * // '붙여넣기' 버튼을 누르면 클립보드에 저장된 텍스트를 가져와 화면에 표시해요.
 * function PasteButton() {
 *   const [text, setText] = useState(''); *

 *   const handlePress = async () => {
 *     try {
 *       const clipboardText = await getClipboardText();
 *       setText(clipboardText || '클립보드에 텍스트가 없어요.');
 *     } catch (error) {
 *       if (error instanceof GetClipboardTextPermissionError) {
 *         // 클립보드 읽기 권한 없음
 *       } *

 *       if (error instanceof SetClipboardTextPermissionError) {
 *         // 클립보드 쓰기 권한 없음
 *       }
 *     }
 *   }; *

 *   return (
 *     <View>
 *       <Text>{text}</Text>
 *       <Button title="붙여넣기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await getClipboardText.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await getClipboardText.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
declare const getClipboardText: _apps_in_toss_types.PermissionFunctionWithDialog<GetClipboardText>;

/**
 * @public
 * @category 위치 정보
 * @name getCurrentLocation
 * @description 디바이스의 현재 위치 정보를 가져오는 함수예요.
 * 위치 기반 서비스를 구현할 때 사용되고, 한 번만 호출되어 현재 위치를 즉시 반환해요.
 * 예를 들어 지도 앱에서 사용자의 현재 위치를 한 번만 가져올 때, 날씨 앱에서 사용자의 위치를 기반으로 기상 정보를 제공할 때, 매장 찾기 기능에서 사용자의 위치를 기준으로 가까운 매장을 검색할 때 사용하면 유용해요.
 *
 * @param {GetCurrentLocationOptions} options 위치 정보를 가져올 때 사용하는 옵션 객체예요.
 * @param {Accuracy} [options.accuracy] 위치 정보의 정확도 수준이에요. 정확도는 `Accuracy` 타입으로 설정돼요.
 * @property [openPermissionDialog] - 위치 정보 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 위치 정보 권한의 현재 상태를 반환해요. `allowed`는 사용자가 위치 정보 권한을 허용한 상태예요. `denied`는 사용자가 위치 정보 권한을 거부한 상태예요. `notDetermined`는 위치 정보 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<Location>} 디바이스의 위치 정보가 담긴 객체를 반환해요. 자세한 내용은 [Location](/react-native/reference/native-modules/Types/Location.html)을 참고해주세요.
 *
 * @signature
 * ```typescript
 * function getCurrentLocation(options: {
 *   accuracy: Accuracy;
 * }): Promise<Location>;
 * ```
 *
 * @example
 * ### 디바이스의 현재 위치 정보 가져오기
 *
 * "권한 확인하기"버튼을 눌러서 현재 위치정보 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`GetCurrentLocationPermissionError`](/react-native/reference/types/권한/GetCurrentLocationPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 위치정보 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { Accuracy, getCurrentLocation, Location } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Text, View } from 'react-native';
 *
 * // 현재 위치 정보를 가져와 화면에 표시하는 컴포넌트
 * function CurrentPosition() {
 *   const [position, setPosition] = useState<Location | null>(null);
 *
 *   const handlePress = async () => {
 *     try {
 *       const response = await getCurrentLocation({ accuracy: Accuracy.Balanced });
 *       setPosition(response);
 *     } catch (error) {
 *       console.error('위치 정보를 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       {position ? (
 *         <Text>
 *           위치: {position.coords.latitude}, {position.coords.longitude}
 *         </Text>
 *       ) : (
 *         <Text>위치 정보를 아직 가져오지 않았어요</Text>
 *       )}
 *       <Button title="현재 위치 정보 가져오기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           Alert.alert(await getCurrentLocation.getPermission());
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           Alert.alert(await getCurrentLocation.openPermissionDialog());
 *         }}
 *       />
 *     </View>
 *   );
 * }
 *
 * ```
 */
declare const getCurrentLocation: _apps_in_toss_types.PermissionFunctionWithDialog<GetCurrentLocation>;

/**
 * @public
 * @category 클립보드
 * @name setClipboardText
 * @description 텍스트를 클립보드에 복사해서 사용자가 다른 곳에 붙여 넣기 할 수 있어요.
 * @param {Promise<void>} text - 클립보드에 복사할 텍스트예요. 문자열 형식으로 입력해요.
 * @property [openPermissionDialog] - 클립보드 쓰기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 클립보드 쓰기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 클립보드 쓰기 권한을 허용한 상태예요. `denied`는 사용자가 클립보드 쓰기 권한을 거부한 상태예요. `notDetermined`는 클립보드 쓰기 권한 요청을 한 번도 하지 않은 상태예요.
 *
 * @signature
 * ```typescript
 * function setClipboardText(text: string): Promise<void>;
 * ```
 *
 * @example
 * ### 텍스트를 클립보드에 복사하기
 *
 * 텍스트를 클립보드에 복사하는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 클립보드 쓰기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`SetClipboardTextPermissionError`](/react-native/reference/types/권한/SetClipboardTextPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 클립보드 쓰기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { setClipboardText, SetClipboardTextPermissionError } from '@apps-in-toss/framework';
 * import { Alert, Button } from 'react-native';
 *
 * // '복사' 버튼을 누르면 "복사할 텍스트"가 클립보드에 복사돼요.
 * function CopyButton() {
 *   const handleCopy = async () => {
 *     try {
 *       await setClipboardText('복사할 텍스트');
 *       console.log('텍스트가 복사됐어요!');
 *     } catch (error) {
 *       if (error instanceof SetClipboardTextPermissionError) {
 *         // 텍스트 쓰기 권한 거부됨
 *       }
 *     }
 *   };
 *
 *   return (
 *     <>
 *       <Button title="복사" onPress={handleCopy} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await setClipboardText.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await setClipboardText.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </>
 *   );
 * }
 * ```
 */
declare const setClipboardText: _apps_in_toss_types.PermissionFunctionWithDialog<SetClipboardText>;

/**
 * @public
 * @category 카메라
 * @name openCamera
 * @description 카메라를 실행해서 촬영된 이미지를 반환하는 함수예요.
 * @param {OpenCameraOptions} options - 카메라 실행 시 사용되는 옵션 객체예요.
 * @param {boolean} [options.base64=false] - 이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값은 `false`예요. `true`로 설정하면 `dataUri` 대신 Base64 인코딩된 문자열을 반환해요.
 * @param {number} [options.maxWidth=1024] - 이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값은 `1024`예요.
 * @property [openPermissionDialog] - 카메라 접근 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 카메라 접근 권한의 현재 상태를 반환해요. `allowed`는 사용자가 카메라 접근 권한을 허용한 상태예요. `denied`는 사용자가 카메라 접근 권한을 거부한 상태예요. `notDetermined`는 카메라 접근 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<ImageResponse>}
 * 촬영된 이미지 정보를 포함한 객체를 반환해요. 반환 객체의 구성은 다음과 같아요:
 * - `id`: 이미지의 고유 식별자예요.
 * - `dataUri`: 이미지 데이터를 표현하는 데이터 URI예요.
 *
 * @signature
 * ```typescript
 * function openCamera(options: {
 *   base64: boolean;
 *   maxWidth: number;
 * }): Promise<ImageResponse>;
 * ```
 *
 * @example
 * ### 카메라 실행 후 촬영된 사진 가져오기
 *
 * 카메라로 사진을 찍고 결과를 가져오는 예제예요.
 * 이 과정에서 현재 카메라 권한 상태를 확인할 수 있고, 권한이 없으면 권한을 요청해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`OpenCameraPermissionError`](/react-native/reference/types/권한/OpenCameraPermissionError)를 반환해요.
 *
 * ```tsx
 * import { ImageResponse, openCamera, OpenCameraPermissionError } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Image, Text, View } from 'react-native';
 *
 * const base64 = true;
 *
 * // 카메라를 실행하고 촬영된 이미지를 화면에 표시하는 컴포넌트
 * function Camera() {
 *   const [image, setImage] = useState<ImageResponse | null>(null);
 *
 *   const handlePress = async () => {
 *     try {
 *       const response = await openCamera({ base64 });
 *       setImage(response);
 *     } catch (error) {
 *       if (error instanceof OpenCameraPermissionError) {
 *         console.log('권한 에러');
 *       }
 *
 *       console.error('사진을 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   // base64 형식으로 반환된 이미지를 표시하려면 데이터 URL 스키마 Prefix를 붙여야해요.
 *   const imageUri = base64 ? 'data:image/jpeg;base64,' + image?.dataUri : image?.dataUri;
 *
 *   return (
 *     <View>
 *       {image ? <Image source={{ uri: imageUri }} style={{ width: 200, height: 200 }} /> : <Text>사진이 없어요</Text>}
 *       <Button title="사진 촬영하기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await openCamera.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const currentPermission = await openCamera.openPermissionDialog();
 *           Alert.alert(currentPermission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
declare const openCamera: _apps_in_toss_types.PermissionFunctionWithDialog<OpenCamera>;

/**
 * @public
 * @category 환경 확인
 * @kind function
 * @name getDeviceId
 * @description
 * 사용 중인 기기의 고유 식별자를 문자열로 반환해요.
 *
 * 이 함수는 현재 사용 중인 기기의 고유 식별자를 문자열로 반환해요. 기기별로 설정이나 데이터를 저장하거나 사용자의 기기를 식별해서 로그를 기록하고 분석하는 데 사용할 수 있어요. 같은 사용자의 여러 기기를 구분하는 데도 유용해요.
 *
 * @returns {string} 기기의 고유 식별자를 나타내는 문자열이에요.
 *
 * @example
 * ### 기기 고유 식별자 가져오기
 *
 * ```tsx
 * import { getDeviceId } from '@apps-in-toss/framework';
 * import { Text } from 'react-native';
 *
 * function MyPage() {
 *   const id = getDeviceId();
 *
 *   return (
 *     <Text>사용자의 기기 고유 식별자: {id}</Text>
 *   );
 * }
 * ```
 */
declare function getDeviceId(): string;

/**
 * @public
 * @category 환경 확인
 * @kind function
 * @name getOperationalEnvironment
 * @description
 * 현재 실행 중인 앱의 운영 환경을 확인해요.
 * 토스 앱에서 실행 중이라면 `'toss'`, 샌드박스 환경에서 실행 중이라면 `'sandbox'`를 반환해요.
 *
 * 운영 환경은 앱이 실행되는 컨텍스트를 의미하며, 특정 기능의 사용 가능 여부를 판단하는 데 활용할 수 있어요.
 *
 * @returns {'toss' | 'sandbox'}
 * 현재 운영 환경을 나타내는 문자열이에요.
 * - `'toss'`: 토스 앱에서 실행 중이에요.
 * - `'sandbox'`: 샌드박스 환경에서 실행 중이에요.
 *
 * @example
 * ### 현재 운영 환경 확인하기
 *
 * ```tsx
 * import { getOperationalEnvironment } from '@apps-in-toss/framework';
 * import { Text } from 'react-native';
 *
 * function EnvironmentInfo() {
 *   const environment = getOperationalEnvironment();
 *
 *   return (
 *     <Text>현재 운영 환경: {environment}</Text>
 *   );
 * }
 * ```
 */
declare function getOperationalEnvironment(): 'toss' | 'sandbox';

/**
 * @public
 * @category 환경 확인
 * @name getTossAppVersion
 * @description 토스 앱 버전을 가져옵니다. 예를 들어, `5.206.0`과 같은 형태로 반환돼요. 토스 앱 버전을 로그로 남기거나, 특정 기능이 특정 버전 이상에서만 실행될 때 사용돼요.
 * @signature
 * ```typescript
 * function getTossAppVersion(): string
 * ```
 *
 * @returns {string} 토스 앱 버전
 *
 * @example
 *
 *
 * ### 토스 앱 버전 확인하기
 *
 * ```tsx
 * import { getTossAppVersion } from '@apps-in-toss/framework';
 * import { Text } from 'react-native';
 *
 * export function TossAppVersionPage() {
 *  return (
 *   <Text>{getTossAppVersion()}</Text>
 *  )
 * }
 * ```
 */
declare function getTossAppVersion(): string;

/**
 * @public
 * @category 공유
 * @kind function
 * @name getTossShareLink
 * @description
 * 사용자가 지정한 경로를 토스 앱에서 열 수 있는 공유 링크를 생성해요.
 *
 * 생성된 링크를 다른 사람과 공유하면:
 * - 토스 앱이 설치되어 있으면: 토스 앱이 실행되면서 지정한 경로로 이동해요.
 * - 토스 앱이 없으면: iOS는 앱스토어로, Android는 플레이스토어로 이동해요.
 *
 * @param path - 딥링크 경로예요. `intoss://`로 시작하는 문자열이어야 해요. (예: `intoss://my-app`, `intoss://my-app/detail?id=123`)
 * @param ogImageUrl - (선택) 공유 시 표시될 커스텀 OG 이미지 URL이에요. 최소 버전: Android 5.240.0, iOS 5.239.0
 * @returns {Promise<string>} 생성된 토스 공유 링크
 *
 * @example
 * ```tsx
 * import { share } from '@granite-js/react-native';
 * import { getTossShareLink } from '@apps-in-toss/framework';
 *
 * // 기본 사용법
 * const tossLink = await getTossShareLink('intoss://my-app');
 * await share({ message: tossLink });
 *
 * // 커스텀 OG 이미지와 함께 사용
 * const linkWithImage = await getTossShareLink(
 *   'intoss://my-app/event',
 *   'https://example.com/og-image.png'
 * );
 * await share({ message: linkWithImage });
 * ```
 */
declare function getTossShareLink(path: string, ogImageUrl?: string): Promise<string>;

/**
 * @public
 * @category 환경 확인
 * @kind function
 * @name isMinVersionSupported
 * @description
 * 현재 토스 앱 버전이 지정한 최소 버전 이상인지 확인해요.
 *
 * 이 함수는 현재 실행 중인 토스 앱의 버전이 파라미터로 전달된 최소 버전 요구사항을 충족하는지 확인해요. 특정 기능이 최신 버전에서만 동작할 때, 사용자에게 앱 업데이트를 안내할 수 있어요.
 *
 * @param {Object} minVersions 플랫폼별 최소 버전 요구사항을 지정하는 객체예요.
 * @param {(`${number}.${number}.${number}` | 'always' | 'never')} minVersions.android 안드로이드 플랫폼의 최소 버전 요구사항이에요.
 * @param {(`${number}.${number}.${number}` | 'always' | 'never')} minVersions.ios iOS 플랫폼의 최소 버전 요구사항이에요.
 * @returns {boolean} 현재 앱 버전이 최소 버전 이상이면 true, 그렇지 않으면 false를 반환해요.
 *
 * @example
 * ### 앱 버전 확인하기
 *
 * ```tsx
 * import { isMinVersionSupported } from '@apps-in-toss/framework';
 * import { Text, View } from 'react-native';
 *
 * function VersionCheck() {
 *   const isSupported = isMinVersionSupported({
 *     android: '1.2.0',
 *     ios: '1.3.0'
 *   });
 *
 *   return (
 *     <View>
 *       {!isSupported && (
 *         <Text>최신 버전으로 업데이트가 필요해요.</Text>
 *       )}
 *     </View>
 *   );
 * }
 * ```
 */
declare function isMinVersionSupported(minVersions: {
    android: `${number}.${number}.${number}` | 'always' | 'never';
    ios: `${number}.${number}.${number}` | 'always' | 'never';
}): boolean;

/**
 * @public
 * @category 화면 제어
 * @kind function
 * @name setDeviceOrientation
 * @description
 * `setDeviceOrientation` 함수는 기기의 화면 방향을 설정하는 기능을 제공해요.
 * 이 기능은 특정 화면에서 가로 모드나 세로 모드를 강제로 지정해야 할 때 유용해요.
 *
 * `type` 옵션을 통해 원하는 화면 방향을 지정할 수 있어요. 특히, 이 함수는 앱 전체에 영향을 미치므로
 * 특정 화면에서만 사용하려면 화면을 벗어날 때 이전 상태로 복구하는 추가 작업이 필요해요.
 *
 * 예를 들어, 동영상 감상 화면에서는 가로 모드를 강제하고, 화면을 떠날 때 설정을 복구해서
 * 다른 화면들의 방향 설정에 영향을 주지 않도록 할 수 있어요.
 *
 * @param {object} options 화면 방향 설정 값이에요.
 * @param {string} options.type 화면 방향을 지정하는 옵션이에요.
 * 'portrait' | 'landscape' 중 하나를 선택할 수 있어요.
 *
 * @returns {Promise<void>} 화면 방향 설정이 완료되면 해결되는 Promise를 반환해요.
 *
 * @example
 * ### 화면 방향 설정하기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { setDeviceOrientation } from '@apps-in-toss/framework';
 *
 * function SetDeviceOrientation() {
 *   return (
 *     <Button
 *       title="가로 모드로 변경"
 *       onPress={() => {
 *         setDeviceOrientation({ type: 'landscape' });
 *       }}
 *     />
 *   );
 * }
 * ```
 *
 * ### 화면 방향 복구하기
 * 특정 화면을 벗어날 때 이전 상태로 복구하려면 다음과 같이 `useEffect`를 사용하세요.
 *
 * ```tsx
 * import { useEffect } from 'react';
 * import { setDeviceOrientation } from '@apps-in-toss/framework';
 *
 * function VideoScreen() {
 *   useEffect(() => {
 *     setDeviceOrientation({ type: 'landscape' });
 *
 *     return () => {
 *       setDeviceOrientation({ type: 'portrait' }); // 설정을 이전 상태로 복구해요.
 *     };
 *   }, []);
 *
 *   return <Text>동영상을 감상하는 화면</Text>;
 * }
 * ```
 */
declare function setDeviceOrientation(options: {
    type: 'portrait' | 'landscape';
}): Promise<void>;

/**
 * @public
 * @category 저장소
 * @name getItem
 * @description 모바일 앱의 로컬 저장소에서 문자열 데이터를 가져와요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
 * @param {string} key - 가져올 아이템의 키를 입력해요.
 * @returns {Promise<string | null>} 지정한 키에 저장된 문자열 값을 반환해요. 값이 없으면 `null`을 반환해요.
 * @example
 *
 * ### `my-key`에 저장된 아이템 가져오기
 * ```ts
 * const value = await Storage.getItem('my-key');
 * console.log(value); // 'value'
 * ```
 */
declare function getItem(key: string): Promise<string | null>;
/**
 * @public
 * @category 저장소
 * @name setItem
 * @description 모바일 앱의 로컬 저장소에 문자열 데이터를 저장해요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
 * @param {string} key - 저장할 아이템의 키를 입력해요.
 * @param {string} value - 저장할 아이템의 값을 입력해요.
 * @returns {Promise<void>} 아이템을 성공적으로 저장하면 아무 값도 반환하지 않아요.
 * @example
 *
 * ### `my-key`에 아이템 저장하기
 * ```ts
 * import { Storage } from '@apps-in-toss/framework';
 *
 * await Storage.setItem('my-key', 'value');
 * ```
 */
declare function setItem(key: string, value: string): Promise<void>;
/**
 * @public
 * @category 저장소
 * @name removeItem
 * @description 모바일 앱의 로컬 저장소에서 특정 키에 해당하는 아이템을 삭제해요.
 * @param {string} key - 삭제할 아이템의 키를 입력해요.
 * @returns {Promise<void>} 아이템을 삭제하면 아무 값도 반환하지 않아요.
 * @example
 *
 * ### `my-key`에 저장된 아이템 삭제하기
 * ```ts
 * import { Storage } from '@apps-in-toss/framework';
 *
 * await Storage.removeItem('my-key');
 * ```
 */
declare function removeItem(key: string): Promise<void>;
/**
 * @public
 * @category 저장소
 * @name clearItems
 * @description 모바일 앱의 로컬 저장소의 모든 아이템을 삭제해요.
 * @returns {Promise<void>} 아이템을 삭제하면 아무 값도 반환하지 않고 저장소가 초기화돼요.
 * @example
 *
 * ### 저장소 초기화하기
 * ```ts
 * import { Storage } from '@apps-in-toss/framework';
 *
 * await Storage.clearItems();
 * ```
 */
declare function clearItems(): Promise<void>;
/**
 * @public
 * @category 저장소
 * @name Storage
 * @description 네이티브의 저장소를 사용해요.
 * @property {typeof getItem} [getItem] 모바일 앱의 로컬 저장소에서 아이템을 가져오는 함수예요. 자세한 내용은 [getItem](/react-native/reference/native-modules/저장소/getItem.html)을 참고하세요.
 * @property {typeof setItem} [setItem] 모바일 앱의 로컬 저장소에 아이템을 저장하는 함수예요. 자세한 내용은 [setItem](/react-native/reference/native-modules/저장소/setItem.html)을 참고하셰요.
 * @property {typeof removeItem} [removeItem] 모바일 앱의 로컬 저장소에서 아이템을 삭제하는 함수예요. 자세한 내용은 [removeItem](/react-native/reference/native-modules/저장소/removeItem.html)을 참고하세요.
 * @property {typeof clearItems} [clearItems] 모바일 앱의 로컬 저장소를 초기화하는 함수예요. 자세한 내용은 [clearItems](/react-native/reference/native-modules/저장소/clearItems.html)을 참고하세요.
 */
declare const Storage: {
    getItem: typeof getItem;
    setItem: typeof setItem;
    removeItem: typeof removeItem;
    clearItems: typeof clearItems;
};

/**
 * @public
 * @category 게임센터
 * @name openGameCenterLeaderboard
 * @description 게임센터 리더보드 웹뷰를 열어요.
 * 앱 버전이 최소 지원 버전(`5.221.0`)보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
 * 게임센터를 사용하는 사용자는 반드시 최소 지원 버전 이상이어야 게임을 실행할 수 있어요.
 * @returns 리더보드 웹뷰를 호출해요. 앱 버전이 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
 *
 * @example
 * import { Button } from 'react-native';
 * import { openGameCenterLeaderboard } from '@apps-in-toss/framework';
 *
 * // '리더보드' 버튼을 누르면 게임센터 리더보드 웹뷰가 열려요.
 * function LeaderboardButton() {
 *   const onPress = () => {
 *     openGameCenterLeaderboard();
 *   };
 *
 *   return <Button title="리더보드 웹뷰 호출" onPress={onPress} />;
 * }
 */
declare function openGameCenterLeaderboard(): Promise<void>;

/**
 * @public
 * @category 토스 로그인
 * @name getIsTossLoginIntegratedService
 * @description
 * 유저가 토스 로그인을 연동했는지 여부를 확인해요.
 * 기존 토스 로그인 사용자를 찾아서 게임 식별자로 전환하는 마이그레이션을 할 때 사용할 수 있어요.
 * @returns {Promise<boolean | undefined>}
 * 토스 로그인이 연동된 유저인지 여부를 반환해요.
 * - `true`: 토스 로그인이 연동된 유저에요.
 * - `false`: 토스 로그인이 연동되지 않은 유저에요.
 * - `undefined`: 앱 버전이 최소 지원 버전보다 낮아요.
 * @throw {message: "oauth2ClientId 설정이 필요합니다."} - 토스 로그인을 사용하지 않는 미니앱에서 호출했을 때 발생해요.
 * @example
 * ```tsx
 * // react-native
 * import { Button } from 'react-native';
 * import { getIsTossLoginIntegratedService } from '@apps-in-toss/framework';
 *
 * function GetIsTossLoginIntegratedServiceButton() {
 *   async function handlePress() {
 *     try {
 *       const result = await getIsTossLoginIntegratedService();
 *
 *       if (result === undefined) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *       if (result === true) {
 *         console.log('토스 로그인이 연동된 유저에요.');
 *         // 여기에서 토스 로그인 연동 유저에 대한 처리를 할 수 있어요.
 *       }
 *       if (result === false) {
 *         console.log('토스 로그인이 연동되지 않은 유저에요.');
 *         // 여기에서 토스 로그인 연동 유저가 아닌 경우에 대한 처리를 할 수 있어요.
 *       }
 *     } catch (error) {
 *       console.error(error);
 *     }
 *   }
 *
 *   return (
 *     <Button onPress={handlePress} title="토스 로그인 통합 서비스 여부 확인" />
 *   );
 * }
 * ```
 */
declare function getIsTossLoginIntegratedService(): Promise<boolean | undefined>;

/**
 * @public
 * @category 토스페이
 * @name TossPay
 * @description 토스페이 결제 관련 함수를 모아둔 객체예요.
 * @property {typeof checkoutPayment} [checkoutPayment] 토스페이 결제를 인증하는 함수예요. 자세한 내용은 [checkoutPayment](/react-native/reference/native-modules/토스페이/checkoutPayment)를 참고하세요.
 */
declare const TossPay: {
    checkoutPayment: typeof checkoutPayment;
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
declare const GoogleAdMob: {
    loadAdMobInterstitialAd: typeof loadAdMobInterstitialAd;
    showAdMobInterstitialAd: typeof showAdMobInterstitialAd;
    loadAdMobRewardedAd: typeof loadAdMobRewardedAd;
    showAdMobRewardedAd: typeof showAdMobRewardedAd;
    loadAppsInTossAdMob: typeof loadAppsInTossAdMob;
    showAppsInTossAdMob: typeof showAppsInTossAdMob;
};

/**
 * @public
 * @category 화면 제어
 * @kind function
 * @name closeView
 * @description 현재 화면을 닫는 함수에요. 예를 들어, "닫기" 버튼을 눌러서 서비스를 종료할 때 사용할 수 있어요.
 * @returns {Promise<void>}
 *
 * @example
 * ### 닫기 버튼을 눌러 화면 닫기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { closeView } from '@apps-in-toss/native-modules';
 *
 * function CloseButton() {
 *  return <Button title="닫기" onPress={closeView} />;
 * }
 * ```
 */
declare function closeView(): Promise<void>;

/**
 * @public
 * @category 언어
 * @kind function
 * @name getLocale
 * @description
 * 사용자의 로케일(locale) 정보를 반환해요. 네이티브 모듈에서 로케일 정보를 가져올 수 없을 때는 기본값으로 'ko-KR'을 반환합니다. 앱의 현지화 및 언어 설정과 관련된 기능을 구현할 때 사용하세요.
 *
 * @returns {string} 사용자의 로케일 정보를 반환해요.
 *
 * @example
 * ### 현재 사용자의 로케일 정보 가져오기
 *
 * ```tsx
 * import { getLocale } from '@apps-in-toss/native-modules';
 * import { Text } from 'react-native';
 *
 * function MyPage() {
 *  const locale = getLocale();
 *
 *  return (
 *    <Text>사용자의 로케일 정보: {locale}</Text>
 *  )
 * }
 *
 * ```
 */
declare function getLocale(): string;

/**
 * @public
 * @name getSchemeUri
 * @category 환경 확인
 * @kind function
 * @description 처음에 화면에 진입한 스킴 값이에요. 페이지 이동으로 인한 URI 변경은 반영되지 않아요.
 * @returns {string} 처음에 화면에 진입한 스킴 값을 반환해요.
 *
 * @example
 * ### 처음 진입한 스킴 값 가져오기
 *
 * ```tsx
 * import { getSchemeUri } from '@apps-in-toss/native-modules';
 * import { Text } from 'react-native';
 *
 * function MyPage() {
 *  const schemeUri = getSchemeUri();
 *
 *  return <Text>처음에 화면에 진입한 스킴 값: {schemeUri}</Text>
 * }
 * ```
 */
declare function getSchemeUri(): string;

type HapticFeedbackType = 'tickWeak' | 'tap' | 'tickMedium' | 'softMedium' | 'basicWeak' | 'basicMedium' | 'success' | 'error' | 'wiggle' | 'confetti';
/**
 * @public
 * @category 인터렉션
 * @name HapticFeedbackOptions
 * @description
 * generateHapticFeedback 함수에 전달할 햅틱진동의 타입을 나타내요. 진동타입의 종류는 다음과 같아요.
 * ```typescript
 * type HapticFeedbackType =
 * | "tickWeak"
 * | "tap"
 * | "tickMedium"
 * | "softMedium"
 * | "basicWeak"
 * | "basicMedium"
 * | "success"
 * | "error"
 * | "wiggle"
 * | "confetti";
 * ```
 * @typedef { type: HapticFeedbackType } HapticFeedbackOptions
 * @typedef { "tickWeak" | "tap" | "tickMedium" | "softMedium" | "basicWeak" | "basicMedium" | "success" | "error" | "wiggle" | "confetti" } HapticFeedbackType
 *
 */
interface HapticFeedbackOptions {
    type: HapticFeedbackType;
}

/**
 * @public
 * @category 인터렉션
 * @name generateHapticFeedback
 * @description 디바이스에 햅틱 진동을 일으키는 함수예요. 예를 들어, 버튼 터치나 화면전환에 드라마틱한 효과를 주고 싶을 때 사용할 수 있어요. [HapticFeedbackOptions](/react-native/reference/native-modules/인터렉션/HapticFeedbackOptions.html)에서 진동타입을 확인해 보세요.
 * @returns {void}
 *
 * @example
 * ### 버튼을 눌러 햅틱 일으키기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { generateHapticFeedback } from '@apps-in-toss/native-modules';
 *
 * function GenerateHapticFeedback() {
 *  return <Button title="햅틱" onPress={() => { generateHapticFeedback( { type: "tickWeak"}) }} />;
 * }
 * ```
 */
declare function generateHapticFeedback(options: HapticFeedbackOptions): Promise<void>;

/**
 * @public
 * @category 공유
 * @kind function
 * @name share
 * @description
 * 사용자가 콘텐츠를 다른 사람과 공유할 수 있도록 네이티브 공유 시트를 표시해요.
 * `options.message` 속성에 공유할 메시지를 전달하면, 사용자가 선택할 수 있는 앱 목록이 표시돼요.
 * 예를 들어, 사용자가 텍스트 메시지를 공유하거나 메모 앱에 저장하려고 할 때 유용해요.
 * @param {object} options - 공유할 메시지를 담은 객체예요.
 * @param {string} options.message - 공유할 텍스트 문자열이에요. 예를 들어, "안녕하세요! 이 내용을 공유합니다."
 *
 * @example
 * ### 공유하기 기능 구현하기
 *
 * 아래는 버튼을 클릭하면 메시지를 공유하는 간단한 예제예요.
 *
 * ```tsx
 * import { share } from '@apps-in-toss/native-modules';
 * import { Button } from 'react-native';
 *
 * function MyPage() {
 *   return (
 *     <Button
 *       title="공유"
 *       onPress={() => share({ message: '공유할 메시지입니다.' })}
 *     />
 *   );
 * }
 * ```
 */
declare function share(message: {
    message: string;
}): Promise<void>;

/**
 * @public
 * @name setSecureScreen
 * @category 화면 제어
 * @kind function
 * @description
 * 화면 캡쳐를 차단해서 민감한 정보가 유출되지 않도록 보호하거나, 필요할 경우 캡쳐를 허용하도록 설정해요. 예를 들어 보안이 중요한 화면에서 사용할 수 있어요.
 *
 * @param {object} options 화면 캡쳐 설정 옵션이에요.
 * @param {boolean} options.enabled 화면 캡쳐를 차단할지 여부를 설정해요. `true`면 캡쳐를 차단하고, `false`면 허용해요.
 * @returns {enabled: boolean} 현재 설정된 캡쳐 차단 상태를 반환해요.
 *
 * @example
 * ### 캡쳐 허용 상태 변경하기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { setSecureScreen } from '@apps-in-toss/native-modules';
 *
 * function SetSecureScreen() {
 *  return <Button title="캡쳐 막기" onPress={async () => {
 *    await setSecureScreen({ enabled: true });
 *  }} />;
 * }
 * ```
 */
declare function setSecureScreen(options: {
    enabled: boolean;
}): Promise<{
    enabled: boolean;
}>;

/**
 * @public
 * @category 화면 제어
 * @kind function
 * @name setScreenAwakeMode
 * @description
 * `setScreenAwakeMode` 함수는 화면이 항상 켜져 있도록 설정하거나 해제하는 기능을 제공해요.
 * 이 기능은 웹툰, 동영상, 문서 읽기 등 화면을 지속해서 켜두어야 하는 상황에서 유용해요.
 *
 * `enabled` 옵션을 `true`로 설정하면 화면이 꺼지지 않게 유지하고,  `false`로 설정하면 기본 화면 보호기 시간에 따라 화면이 꺼져요.  특히, 이 함수는 앱 전체에 영향을 미치므로 특정 화면에서만 사용하려면 화면을 벗어날 때 이전 상태로 복구하는 추가 작업이 필요해요.
 *
 * 예를 들어, 미디어 콘텐츠 감상 화면에서는 항상 켜짐 모드를 활성화하고, 화면을 떠날 때 설정을 복구해서 불필요한 배터리 소모를 방지할 수 있어요.
 *
 * 다만, 앱에서 벗어나는 상황에서는 항상 켜짐 모드가 비활성화될 수 있으니 주의해야 해요.
 *
 * @param {object} options 화면 항상 켜짐 모드의 설정 값이에요.
 * @param {boolean} options.enabled 화면 항상 켜짐 모드를 켜거나 끄는 옵션이에요.
 * `true`로 설정하면 화면이 항상 켜지고, `false`로 설정하면 화면 보호기 시간에 따라 꺼져요.
 *
 * @returns {object} 현재 화면 항상 켜짐 모드의 설정 상태를 반환해요.
 * @returns {boolean} enabled 화면 항상 켜짐 모드가 켜져 있는지 여부를 나타내는 값이에요.
 *
 * @example
 * ### 화면 항상 켜짐 모드 설정하기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { setScreenAwakeMode } from '@apps-in-toss/native-modules';
 *
 * function SetScreenAwakeMode() {
 *   return (
 *     <Button
 *       title="화면 항상 켜기"
 *       onPress={() => {
 *         setScreenAwakeMode({ enabled: true });
 *       }}
 *     />
 *   );
 * }
 * ```
 *
 * ### 화면 항상 켜짐 모드 복구하기
 * 특정 화면을 벗어날 때 이전 상태로 복구하려면 다음과 같이 `useEffect`를 사용하세요.
 *
 * ```tsx
 * import { useEffect } from 'react';
 * import { setScreenAwakeMode, cleanUp } from '@apps-in-toss/native-modules';
 *
 * function MediaScreen() {
 *   useEffect(() => {
 *     setScreenAwakeMode({ enabled: true });
 *
 *     return () => {
 *       setScreenAwakeMode({ enabled: false }); // 설정을 이전 상태로 복구해요.
 *     };
 *   }, []);
 *
 *   return <Text>미디어 콘텐츠를 감상하는 화면</Text>;
 * }
 * ```
 */
declare function setScreenAwakeMode(options: {
    enabled: boolean;
}): Promise<{
    enabled: boolean;
}>;

type NetworkStatus = 'OFFLINE' | 'WIFI' | '2G' | '3G' | '4G' | '5G' | 'WWAN' | 'UNKNOWN';

/**
 * @public
 * @category 네트워크
 * @kind function
 * @name getNetworkStatus
 * @description
 * 디바이스의 현재 네트워크 연결 상태를 가져오는 함수예요.
 * 반환 값은 `NetworkStatus` 타입으로, 인터넷 연결 여부와 연결 유형(Wi-Fi, 모바일 데이터 등)을 나타내요. 값은 다음 중 하나예요.
 *
 * - `OFFLINE`: 인터넷에 연결되지 않은 상태예요.
 * - `WIFI`: Wi-Fi에 연결된 상태예요.
 * - `2G`: 2G 네트워크에 연결된 상태예요.
 * - `3G`: 3G 네트워크에 연결된 상태예요.
 * - `4G`: 4G 네트워크에 연결된 상태예요.
 * - `5G`: 5G 네트워크에 연결된 상태예요.
 * - `WWAN`: 인터넷은 연결되었지만, 연결 유형(Wi-Fi, 2G~5G)을 알 수 없는 상태예요. 이 상태는 iOS에서만 확인할 수 있어요.
 * - `UNKNOWN`: 인터넷 연결 상태를 알 수 없는 상태예요. 이 상태는 안드로이드에서만 확인할 수 있어요.
 *
 * @returns {Promise<NetworkStatus>} 네트워크 상태를 반환해요.
 *
 * @example
 * ### 현재 네트워크 상태 가져오기
 *
 * 네트워크 연결 상태를 가져와 화면에 표시하는 예제예요.
 *
 * ```tsx
 * import { useState, useEffect } from 'react';
 * import { Text, View } from 'react-native';
 * import { getNetworkStatus, NetworkStatus } from '@apps-in-toss/native-modules';
 *
 * function GetNetworkStatus() {
 *   const [status, setStatus] = useState<NetworkStatus | ''>('');
 *
 *   useEffect(() => {
 *     async function fetchStatus() {
 *       const networkStatus = await getNetworkStatus();
 *       setStatus(networkStatus);
 *     }
 *
 *     fetchStatus();
 *   }, []);
 *
 *   return (
 *     <View>
 *       <Text>현재 네트워크 상태: {status}</Text>
 *     </View>
 *   );
 * }
 * ```
 */
declare function getNetworkStatus(): Promise<NetworkStatus>;

/**
 * @public
 * @category 화면 제어
 * @name setIosSwipeGestureEnabled
 * @description
 * `setIosSwipeGestureEnabled` 함수는 iOS에서 화면을 스와이프하여 뒤로가기 기능을 활성화하거나 비활성화할 수 있어요.
 *
 * @param {object} options 스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요.
 * @param {boolean} options.isEnabled 화면을 스와이프하여 뒤로가기 기능을 활성화하거나 비활성화할 수 있어요. `true`를 설정하면 스와이프로 뒤로갈 수 있고, `false`를 설정하면 스와이프 뒤로가기 기능이 비활성화돼요.
 *
 * @returns {void}
 *
 * @example
 *
 * ### iOS에서 화면 스와이프로 뒤로가기 기능을 활성화하거나 비활성화하기
 *
 * **스와이프 끄기** 버튼을 눌러 화면 스와이프로 뒤로가기 기능을 비활성화하거나, **스와이프 켜기** 버튼을 눌러 화면 스와이프로 뒤로가기 기능을 활성화할 수 있어요.
 *
 *
 * ```tsx
 * import { setIosSwipeGestureEnabled } from '@apps-in-toss/native-modules';
 * import { Button } from 'react-native';
 *
 * function Page() {
 *  return (
 *    <>
 *     <Button title="스와이프 끄기" onPress={() => setIosSwipeGestureEnabled({ isEnabled: false })} />
 *     <Button title="스와이프 켜기" onPress={() => setIosSwipeGestureEnabled({ isEnabled: true })} />
 *    </>
 *  );
 * }
 * ```
 *
 */
declare function setIosSwipeGestureEnabled(options: {
    isEnabled: boolean;
}): Promise<void>;

/**
 * @public
 * @kind function
 * @category 화면 이동
 *
 * @name openURL
 * @signature
 * ```typescript
 * function openURL(url: string): Promise<any>;
 * ```
 *
 * @description
 * 지정된 URL을 기기의 기본 브라우저나 관련 앱에서 열어요.
 * 이 함수는 `react-native`의 [`Linking.openURL`](https://reactnative.dev/docs/0.72/linking#openurl) 메서드를 사용하여 URL을 열어요.
 *
 * @param {string} url 열고자 하는 URL 주소
 * @returns {Promise<any>} URL이 성공적으로 열렸을 때 해결되는 Promise
 *
 * @example
 *
 * ### 외부 URL 열기
 *
 * ```tsx
 * import { openURL } from '@apps-in-toss/native-modules';
 * import { Button } from 'react-native';
 *
 * function Page() {
 *   const handlePress = () => {
 *     openURL('https://google.com');
 *   };
 *
 *   return <Button title="구글 웹사이트 열기" onPress={handlePress} />;
 * }
 * ```
 */
declare function openURL(url: string): Promise<any>;

/**
 * @public
 * @kind function
 * @category 환경 확인
 *
 * @name getPlatformOS
 * @signature
 * ```typescript
 * function getPlatformOS(): 'ios' | 'android';
 * ```
 *
 * @description
 * 현재 실행 중인 플랫폼을 확인하는 함수예요.
 * 이 함수는 `react-native`의 [`Platform.OS`](https://reactnative.dev/docs/0.72/platform#os) 값을 기반으로 동작하며, `ios` 또는 `android` 중 하나의 문자열을 반환해요.
 *
 * @returns {'ios' | 'android'} 현재 실행 중인 플랫폼
 *
 * @example
 *
 * ### 현재 실행중인 OS 플랫폼 확인하기
 *
 * ```tsx
 * import { getPlatformOS } from '@apps-in-toss/native-modules';
 * import { Text } 'react-native';
 *
 * function Page() {
 *   const platform = getPlatformOS();
 *
 *   return <Text>현재 플랫폼: {platform}</Text>;
 * }
 * ```
 */
declare function getPlatformOS(): 'ios' | 'android';

interface BedrockModule {
    closeView: () => void;
    generateHapticFeedback: (options: HapticFeedbackOptions) => Promise<void>;
    share: (message: {
        message: string;
    }) => void;
    setSecureScreen: (options: {
        enabled: boolean;
    }) => Promise<{
        enabled: boolean;
    }>;
    setScreenAwakeMode: (options: {
        enabled: boolean;
    }) => Promise<{
        enabled: boolean;
    }>;
    getNetworkStatus: () => Promise<NetworkStatus>;
    setIosSwipeGestureEnabled: ({ isEnabled }: {
        isEnabled: boolean;
    }) => Promise<void>;
    deviceId: string;
    DeviceInfo: {
        locale: string;
    };
    schemeUri: string;
}
declare const BedrockModule: BedrockModule;

interface BedrockCoreModule {
    addListener: (eventType: string) => void;
    removeListeners: (count: number) => void;
}
declare const BedrockCoreModule: BedrockCoreModule;

declare function tossCoreEventLog(params: {
    log_name: string;
    log_type: string;
    params: Record<string, unknown>;
}): void;

declare const INTERNAL__module: {
    tossCoreEventLog: typeof tossCoreEventLog;
};

export { AppsInTossModule, type AppsInTossSignTossCertParams, BedrockCoreModule, BedrockModule, type CheckoutPaymentOptions, type CheckoutPaymentResult, type CompletedOrRefundedOrdersResult, type ContactsViralParams, type EventLogParams, type GameCenterGameProfileResponse, type GetUserKeyForGameErrorResponse, type GetUserKeyForGameResponse, type GetUserKeyForGameSuccessResponse, GoogleAdMob, type GrantPromotionRewardForGameErrorResponse, type GrantPromotionRewardForGameErrorResult, type GrantPromotionRewardForGameResponse, type GrantPromotionRewardForGameSuccessResponse, type HapticFeedbackType, IAP, AppsInTossModuleInstance as INTERNAL__AppsInTossModule, INTERNAL__appBridgeHandler, INTERNAL__module, type IapCreateOneTimePurchaseOrderOptions, type IapCreateOneTimePurchaseOrderResult, type IapProductListItem, type LoadAdMobEvent, type LoadAdMobInterstitialAdEvent, type LoadAdMobInterstitialAdOptions, type LoadAdMobOptions, type LoadAdMobParams, type LoadAdMobRewardedAdEvent, type LoadAdMobRewardedAdOptions, type NetworkStatus, type Primitive, type SaveBase64DataParams, type ShowAdMobEvent, type ShowAdMobInterstitialAdEvent, type ShowAdMobInterstitialAdOptions, type ShowAdMobOptions, type ShowAdMobParams, type ShowAdMobRewardedAdEvent, type ShowAdMobRewardedAdOptions, Storage, type SubmitGameCenterLeaderBoardScoreResponse, TossPay, type UpdateLocationEventEmitter, appLogin, appsInTossEvent, appsInTossSignTossCert, closeView, contactsViral, eventLog, fetchAlbumPhotos, fetchContacts, generateHapticFeedback, getClipboardText, getCurrentLocation, getDeviceId, getGameCenterGameProfile, getIsTossLoginIntegratedService, getLocale, getNetworkStatus, getOperationalEnvironment, getPlatformOS, getSchemeUri, getTossAppVersion, getTossShareLink, getUserKeyForGame, grantPromotionRewardForGame, iapCreateOneTimePurchaseOrder, isMinVersionSupported, onVisibilityChangedByTransparentServiceWeb, openCamera, openGameCenterLeaderboard, openURL, processProductGrant, requestOneTimePurchase, saveBase64Data, setClipboardText, setDeviceOrientation, setIosSwipeGestureEnabled, setScreenAwakeMode, setSecureScreen, share, startUpdateLocation, submitGameCenterLeaderBoardScore };
