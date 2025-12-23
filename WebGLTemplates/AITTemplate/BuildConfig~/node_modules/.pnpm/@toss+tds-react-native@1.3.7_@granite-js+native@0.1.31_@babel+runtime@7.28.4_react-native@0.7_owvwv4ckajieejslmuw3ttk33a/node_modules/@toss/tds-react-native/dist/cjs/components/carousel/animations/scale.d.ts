import { Animated } from 'react-native';
export declare function useTapScaleAnimation(): {
    gesture: import("react-native-gesture-handler/lib/typescript/handlers/gestures/panGesture").PanGesture;
    transforms: ({
        scaleX: Animated.Value;
        scaleY?: undefined;
    } | {
        scaleY: Animated.Value;
        scaleX?: undefined;
    })[];
};
