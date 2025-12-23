import { Animated } from 'react-native';
import type { CarouselContext } from '../context';
export declare function useSnapAnimation(context: CarouselContext): {
    gesture: import("react-native-gesture-handler/lib/typescript/handlers/gestures/panGesture").PanGesture;
    transforms: {
        translateX: Animated.Value;
    }[];
    activeIndex: number;
};
