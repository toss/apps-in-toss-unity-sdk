import type { PageScrollStateChangedNativeEventData } from '@granite-js/native/react-native-pager-view';
import type { NativeSyntheticEvent } from 'react-native';
export declare function useScrollStateChangedHandler(): {
    isGestureSwipingRef: import("react").MutableRefObject<boolean>;
    scrollStateChangedHandler: (e: NativeSyntheticEvent<PageScrollStateChangedNativeEventData>) => void;
};
