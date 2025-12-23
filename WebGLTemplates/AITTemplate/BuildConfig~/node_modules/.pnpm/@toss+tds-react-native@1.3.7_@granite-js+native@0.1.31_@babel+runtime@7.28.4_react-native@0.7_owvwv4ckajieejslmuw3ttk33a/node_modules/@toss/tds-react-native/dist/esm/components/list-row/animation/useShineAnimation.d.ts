import { ShineAnimationState } from './animationState';
type UseShineAnimationProps = {
    containerSize: {
        width: number;
        height: number;
    };
};
export declare const useShineAnimation: ({ containerSize }: UseShineAnimationProps) => {
    state: ShineAnimationState;
    light: {
        gradient: {
            transform: ({
                translateX: import("react-native").Animated.Value;
                translateY?: undefined;
            } | {
                translateY: number;
                translateX?: undefined;
            })[];
        };
        start: (playCount: number) => void;
    };
    dark: {
        gradient: {
            opacity: import("react-native").Animated.Value;
            transform: {
                translateX: import("react-native").Animated.Value;
            }[];
        };
        start: (playCount: number) => void;
    };
};
export {};
