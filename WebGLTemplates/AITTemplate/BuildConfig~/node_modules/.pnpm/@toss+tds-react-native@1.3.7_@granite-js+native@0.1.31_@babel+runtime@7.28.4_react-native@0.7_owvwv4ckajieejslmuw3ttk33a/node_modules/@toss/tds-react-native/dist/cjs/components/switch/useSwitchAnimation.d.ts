import type { ViewStyle } from 'react-native';
import { Animated } from 'react-native';
type AnimationState = 'off' | 'on';
export declare function useSwitchAnimation({ animationState, disabled, }: {
    animationState: AnimationState;
    disabled?: boolean;
}): {
    backgroundStyle: Animated.WithAnimatedObject<ViewStyle>;
    animatedKnobStyle: Animated.WithAnimatedObject<ViewStyle>;
    startSwitchOnAnimation: () => void;
    startSwitchOffAnoimation: () => void;
};
export {};
