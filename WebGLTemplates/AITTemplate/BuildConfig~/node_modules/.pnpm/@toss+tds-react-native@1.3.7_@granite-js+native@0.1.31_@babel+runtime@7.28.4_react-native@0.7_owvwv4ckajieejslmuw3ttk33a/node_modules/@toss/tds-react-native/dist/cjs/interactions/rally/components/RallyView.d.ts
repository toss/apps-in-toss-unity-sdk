import type { LayoutChangeEvent, TextStyle, View, ViewProps, ViewStyle } from 'react-native';
import { Animated } from 'react-native';
type RallyProps = {
    onLayout: (event: LayoutChangeEvent) => void;
    style?: Animated.WithAnimatedObject<ViewStyle | TextStyle>;
};
export interface RallyView extends ViewProps {
    rally: RallyProps;
}
export declare const RallyView: import("react").ForwardRefExoticComponent<RallyView & import("react").RefAttributes<View>>;
export {};
