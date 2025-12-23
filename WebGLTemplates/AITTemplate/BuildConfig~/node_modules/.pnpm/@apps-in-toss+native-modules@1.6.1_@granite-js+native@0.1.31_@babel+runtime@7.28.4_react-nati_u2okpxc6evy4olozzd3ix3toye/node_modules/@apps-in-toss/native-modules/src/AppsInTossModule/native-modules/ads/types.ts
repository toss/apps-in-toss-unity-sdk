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
export type AdMobFullScreenEvent = AdClicked | AdDismissed | AdFailedToShow | AdImpression | AdShow;

type AdClicked = { type: 'clicked' };
type AdDismissed = { type: 'dismissed' };
type AdFailedToShow = { type: 'failedToShow' };
type AdImpression = { type: 'impression' };
type AdShow = { type: 'show' };

export type AdUserEarnedReward = { type: 'userEarnedReward'; data: { unitType: string; unitAmount: number } };

export interface AdMobHandlerParams<Options = void, Event = AdMobFullScreenEvent> {
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
export interface ResponseInfo {
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
export interface AdNetworkResponseInfo {
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
export interface InterstitialAd {
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
export interface RewardedAd {
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
export interface AdMobLoadResult {
  adGroupId: string;
  adUnitId: string;
  responseInfo: ResponseInfo;
}
