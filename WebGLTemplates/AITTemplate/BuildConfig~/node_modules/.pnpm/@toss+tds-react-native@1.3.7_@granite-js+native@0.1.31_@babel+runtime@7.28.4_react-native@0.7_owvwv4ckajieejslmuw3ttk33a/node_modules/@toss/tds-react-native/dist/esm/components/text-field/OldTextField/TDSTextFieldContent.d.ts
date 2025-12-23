import React from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import type { TDSTextFieldSize } from './TDSTextField';
export interface TDSTextFieldContentProps {
    disabled?: boolean;
    focus?: boolean;
    error?: boolean;
    size: TDSTextFieldSize;
    leftItem?: React.ReactElement<{
        textfieldSize: TDSTextFieldSize;
    }> | null;
    rightItem?: React.ReactElement<{
        textfieldSize: TDSTextFieldSize;
    }> | null;
    children: React.ReactNode;
    style?: StyleProp<ViewStyle>;
}
export declare function TDSTextFieldContent({ disabled, focus, error, size, leftItem, rightItem, children, style, }: TDSTextFieldContentProps): import("react/jsx-runtime").JSX.Element;
