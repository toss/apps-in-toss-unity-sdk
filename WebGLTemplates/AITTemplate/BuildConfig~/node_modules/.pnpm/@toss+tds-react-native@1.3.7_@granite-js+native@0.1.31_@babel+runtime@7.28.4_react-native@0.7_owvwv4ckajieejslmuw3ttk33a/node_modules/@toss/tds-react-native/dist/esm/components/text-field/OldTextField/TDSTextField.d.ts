import type { ComponentProps } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import { TextInput } from 'react-native';
import type { TDSTextFieldContainerProps } from './TDSTextFieldContainer';
import type { TDSTextFieldContentProps } from './TDSTextFieldContent';
export type TDSTextFieldSize = 'big' | 'medium' | 'classic';
export type TDSTextFieldState = 'default' | 'focused' | 'error' | 'disabled';
export type TDSTextFieldProps = Omit<ComponentProps<typeof TextInput>, 'clearButtonMode'> & Omit<TDSTextFieldContainerProps, 'focus' | 'children' | 'onPress'> & Pick<TDSTextFieldContentProps, 'leftItem' | 'rightItem'> & {
    contentStyle?: StyleProp<ViewStyle>;
};
export declare const TDSTextField: import("react").ForwardRefExoticComponent<Omit<import("react-native").TextInputProps, "clearButtonMode"> & Omit<TDSTextFieldContainerProps, "children" | "focus" | "onPress"> & Pick<TDSTextFieldContentProps, "leftItem" | "rightItem"> & {
    contentStyle?: StyleProp<ViewStyle>;
} & import("react").RefAttributes<TextInput>>;
