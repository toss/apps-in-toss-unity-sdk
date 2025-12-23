import { sequenceAnimation } from './sequence';
export * from './toAnimationController';
export declare const Animation: {
    parallel: (animations: import("react-native").Animated.CompositeAnimation[], config?: {
        stopTogether?: boolean;
    }) => import("react-native").Animated.CompositeAnimation;
    sequence: typeof sequenceAnimation;
};
