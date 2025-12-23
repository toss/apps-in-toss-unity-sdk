import type * as ReactNative from 'react-native';
import { PagerView } from './PagerView';
export default PagerView;
import type { OnPageScrollEventData as PagerViewOnPageScrollEventData, OnPageSelectedEventData as PagerViewOnPageSelectedEventData, OnPageScrollStateChangedEventData as PageScrollStateChangedNativeEventData, NativeProps } from './PagerViewNativeComponent';
export type { PagerViewOnPageScrollEventData, PagerViewOnPageSelectedEventData, PageScrollStateChangedNativeEventData, NativeProps as PagerViewProps, };
export declare type PagerViewOnPageScrollEvent = ReactNative.NativeSyntheticEvent<PagerViewOnPageScrollEventData>;
export declare type PagerViewOnPageSelectedEvent = ReactNative.NativeSyntheticEvent<PagerViewOnPageSelectedEventData>;
export declare type PageScrollStateChangedNativeEvent = ReactNative.NativeSyntheticEvent<PageScrollStateChangedNativeEventData>;
