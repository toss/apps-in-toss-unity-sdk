import { Path } from '@granite-js/native/react-native-svg';
import type { ComponentProps } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import { Animated } from 'react-native';
declare const AnimatedPath: Animated.AnimatedComponent<typeof Path>;
export declare function OuterCircle(props: any): import("react/jsx-runtime").JSX.Element;
export declare function InnerCircle(props: any): import("react/jsx-runtime").JSX.Element;
/**
 * 실제론 style을 넘길 수 있는데 타입 정의가 안되어 있어서 임의로 추가
 */
type CheckProps = ComponentProps<typeof AnimatedPath> & {
    style?: StyleProp<ViewStyle>;
};
export declare function Check(props: CheckProps): import("react/jsx-runtime").JSX.Element;
export {};
