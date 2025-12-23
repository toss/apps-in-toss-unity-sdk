import type { Animated, TextStyle, ViewStyle } from 'react-native';
import type { Motion } from '../types';
export declare function createInterpolationStyle(progress: Animated.Value, { inputRange, motion, }: {
    inputRange: [number, number];
    motion: Motion;
}): Animated.WithAnimatedObject<ViewStyle | TextStyle>;
