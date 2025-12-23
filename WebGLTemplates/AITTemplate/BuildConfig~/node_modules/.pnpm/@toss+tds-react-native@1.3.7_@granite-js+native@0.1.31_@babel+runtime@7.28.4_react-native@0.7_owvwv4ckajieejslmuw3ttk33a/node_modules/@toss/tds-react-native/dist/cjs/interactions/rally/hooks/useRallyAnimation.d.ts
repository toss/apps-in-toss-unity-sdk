import { Animated } from 'react-native';
import type { Layout, MotionInput } from '../types';
export declare function useRallyAnimation({ motionInputs, layout }: {
    motionInputs: MotionInput[];
    layout: Layout;
}): {
    forward: import("../types").AnimationController;
    backward: import("../types").AnimationController;
    style: Animated.WithAnimatedObject<import("react-native").ViewStyle | import("react-native").TextStyle> | undefined;
};
