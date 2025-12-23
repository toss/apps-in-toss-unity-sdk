import type { ReactElement, ReactNode } from 'react';
import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
import { View } from 'react-native';
import { PressableUnderlay } from './PressableUnderlay';
interface PressableEffectProps extends PressableProps {
    style?: StyleProp<ViewStyle>;
    underlay?: ReactElement | null;
    children?: ReactNode;
}
/**
 * onPress가 없다면, View로 렌더링됩니다.
 */
export declare const PressableEffect: import("react").ForwardRefExoticComponent<PressableEffectProps & import("react").RefAttributes<View>> & {
    Underlay: typeof PressableUnderlay;
};
export {};
