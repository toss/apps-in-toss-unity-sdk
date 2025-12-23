import type { Animated } from 'react-native';
import type { MotionInput } from '../types';
export declare function createMotionAnimation({ progress, toValue, motion, }: {
    progress: Animated.Value;
    toValue: number;
    motion: MotionInput;
}): Animated.CompositeAnimation;
