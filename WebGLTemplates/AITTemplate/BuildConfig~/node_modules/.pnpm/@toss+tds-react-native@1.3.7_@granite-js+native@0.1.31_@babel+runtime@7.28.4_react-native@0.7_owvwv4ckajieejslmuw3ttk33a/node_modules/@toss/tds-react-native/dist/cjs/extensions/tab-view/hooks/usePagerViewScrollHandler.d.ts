import { Animated } from 'react-native';
export declare function useScrollHandler({ initialPosition }?: {
    initialPosition?: number;
}): {
    position: Animated.Value;
    offset: Animated.Value;
    scrollHandler: (...args: any[]) => void;
};
