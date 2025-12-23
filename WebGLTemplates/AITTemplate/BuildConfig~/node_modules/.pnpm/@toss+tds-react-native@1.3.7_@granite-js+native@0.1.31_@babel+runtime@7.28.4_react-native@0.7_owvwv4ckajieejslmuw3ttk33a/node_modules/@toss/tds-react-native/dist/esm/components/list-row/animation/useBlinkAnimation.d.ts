import { BlinkAnimationState } from './animationState';
type UseBlinkAnimationProps = {
    containerSize: {
        width: number;
        height: number;
    };
};
export declare const useBlinkAnimation: ({ containerSize }: UseBlinkAnimationProps) => {
    container: {
        transform: {
            scale: import("react-native").Animated.Value;
        }[];
    };
    dimmer: {
        opacity: import("react-native").Animated.Value;
    };
    gradient: {
        opacity: import("react-native").Animated.Value;
        transform: ({
            scale: import("react-native").Animated.Value;
            translateY?: undefined;
        } | {
            translateY: import("react-native").Animated.AnimatedDivision<string | number>;
            scale?: undefined;
        })[];
    };
    state: BlinkAnimationState;
    start: () => void;
    exit: () => void;
};
export {};
