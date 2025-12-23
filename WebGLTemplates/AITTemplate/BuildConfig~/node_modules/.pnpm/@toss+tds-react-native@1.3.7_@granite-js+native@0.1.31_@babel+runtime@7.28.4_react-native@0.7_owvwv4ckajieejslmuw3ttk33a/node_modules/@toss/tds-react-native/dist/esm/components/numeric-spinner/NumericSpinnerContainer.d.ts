import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import { View } from 'react-native';
import type { Size } from './type';
export declare const NumericSpinnerContainerBorderRadiusBySize: Record<Size, number>;
export interface NumericSpinnerContainerProps extends ViewProps {
    size: Size;
    children?: ReactNode;
    style?: ViewProps['style'];
}
export declare const NumericSpinnerContainer: import("react").ForwardRefExoticComponent<NumericSpinnerContainerProps & import("react").RefAttributes<View>>;
