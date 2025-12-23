import type { Animated } from 'react-native';
import type { AnimationController } from '../../types';
export declare function toAnimationController(animation: Animated.CompositeAnimation, { progress, endProgress, }: {
    progress: Animated.Value;
    endProgress: number;
}): AnimationController;
