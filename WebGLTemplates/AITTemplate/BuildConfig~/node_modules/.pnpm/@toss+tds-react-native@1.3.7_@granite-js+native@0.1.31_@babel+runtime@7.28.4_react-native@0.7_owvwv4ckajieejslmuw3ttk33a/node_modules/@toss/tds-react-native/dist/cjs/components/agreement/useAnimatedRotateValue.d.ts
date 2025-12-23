import { Animated } from 'react-native';
export declare function useAnimatedRotateValue(initialValue: number, { min, max }: {
    min: number;
    max: number;
}): {
    startAnimation: (toValue: number) => {
        start: () => Promise<boolean>;
        stop: () => void;
        reset: () => void;
    };
    rotate: Animated.AnimatedInterpolation<string | number>;
};
