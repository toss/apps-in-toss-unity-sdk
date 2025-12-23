import type { PressableProps, ViewProps } from 'react-native';
import type { View } from 'react-native';
import type { Size } from './type';
export interface NumericSpinnerMinusIconButtonProps extends PressableProps {
    size: Size;
    disabled?: boolean;
    style?: ViewProps['style'];
    color?: string;
}
export declare const NumericSpinnerMinusIconButton: import("react").ForwardRefExoticComponent<NumericSpinnerMinusIconButtonProps & import("react").RefAttributes<View>>;
export interface NumericSpinnerPlusIconButtonProps extends PressableProps {
    size: Size;
    disabled?: boolean;
    style?: ViewProps['style'];
    color?: string;
}
export declare const NumericSpinnerPlusIconButton: import("react").ForwardRefExoticComponent<NumericSpinnerPlusIconButtonProps & import("react").RefAttributes<View>>;
