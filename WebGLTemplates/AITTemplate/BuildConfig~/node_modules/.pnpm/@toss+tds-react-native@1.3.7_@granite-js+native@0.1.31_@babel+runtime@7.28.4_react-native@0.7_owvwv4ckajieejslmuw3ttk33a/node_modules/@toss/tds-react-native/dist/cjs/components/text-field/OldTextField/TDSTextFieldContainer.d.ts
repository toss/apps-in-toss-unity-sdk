import React from 'react';
import type { StyleProp, TextStyle, ViewStyle } from 'react-native';
import type { TDSTextFieldSize } from './TDSTextField';
export interface TDSTextFieldContainerProps {
    label?: string;
    placeholder?: string;
    helpText?: string;
    error?: boolean;
    disabled?: boolean;
    size: TDSTextFieldSize;
    textInputStyle?: StyleProp<TextStyle>;
    focus?: boolean;
    children: React.ReactNode;
    clearable?: boolean;
    onClear?: () => void;
    style?: StyleProp<ViewStyle>;
}
export declare function TDSTextFieldContainer({ label, helpText, error, focus: isFocused, size, style, disabled, children, }: TDSTextFieldContainerProps): import("react/jsx-runtime").JSX.Element;
