import type { Animated } from 'react-native';
export declare const parallelAnimation: (animations: Animated.CompositeAnimation[], config?: {
    stopTogether?: boolean;
}) => Animated.CompositeAnimation;
