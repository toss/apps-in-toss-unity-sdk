/** @description iOS 의 노치디자인 하단 SafeArea 값 */
export declare const IOS_NOTCH_SAFE_AREA_BOTTOM = 34;
/**
 * @name useSafeAreaBottom
 * @description SafeAreaBottom 값을 가져옵니다.
 * - iOS 에서 최대 SafeArea Bottom 값을 기본 노치 SafeArea Bottom 값으로 지정합니다.
 * - MainTabBar 가 열리고 닫힐 때 SafeArea 값이 변경되어 LayoutShift 가 발생합니다.
 * - MainTabBar 가 있는 경우 SafeArea = 82px = 58px + 24px
 * - MainTabBar 가 없는 경우 SafeArea = 34px (기본 노치 값, 노치값이 없다면 0px)
 */
export declare function useSafeAreaBottom(): number;
