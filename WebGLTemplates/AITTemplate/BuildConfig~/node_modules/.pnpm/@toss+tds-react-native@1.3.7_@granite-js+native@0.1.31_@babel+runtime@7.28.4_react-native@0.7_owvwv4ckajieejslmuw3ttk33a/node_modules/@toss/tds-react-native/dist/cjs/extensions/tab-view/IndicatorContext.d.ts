import type { PagerViewOnPageScrollEventData, PageScrollStateChangedNativeEventData } from '@granite-js/native/react-native-pager-view';
import type { MutableRefObject } from 'react';
import type { NativeSyntheticEvent } from 'react-native';
import { Animated } from 'react-native';
export interface IndicatorContext {
    width: Animated.AnimatedInterpolation<number>;
    translateX: Animated.AnimatedInterpolation<number>;
    opacity: Animated.Value;
    isGestureSwiping?: MutableRefObject<boolean>;
    scrollHandler: (e: NativeSyntheticEvent<PagerViewOnPageScrollEventData>) => void;
    scrollStateChangedHandler: (e: NativeSyntheticEvent<PageScrollStateChangedNativeEventData>) => void;
}
export declare const IndicatorContext: import("react").Context<IndicatorContext>;
export declare function useIndicator(): IndicatorContext;
