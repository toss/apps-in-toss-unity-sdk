import type { ReactNode } from 'react';
import { Component } from 'react';
import type { PressableProps, Animated as ReactNativeAnimated, ViewProps } from 'react-native';
import type { Size } from './type';
export declare const NumericSpinnerNumberBoxBorderRadiusBySize: Record<Size, number>;
export interface NumericSpinnerNumberBoxProps extends PressableProps {
    size: Size;
    children?: ReactNode;
    style?: ViewProps['style'];
    color?: ReactNativeAnimated.AnimatedInterpolation<string>;
}
/**
 * @remarks color prop을 style이 아닌 props로 받아서 AnimatedInterpolation을 받을 수 있도록 합니다.
 */
export declare class NumericSpinnerNumberBox extends Component<NumericSpinnerNumberBoxProps> {
    render(): import("react/jsx-runtime").JSX.Element;
}
