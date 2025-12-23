import type { CheckboxProps } from '../checkbox';
import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
interface AgreementButtonProps extends Pick<CheckboxProps, 'checked' | 'defaultChecked' | 'onCheckedChange'> {
    children?: ReactNode;
    style?: StyleProp<ViewStyle>;
}
export declare function AgreementButton({ children, style, checked: _checked, defaultChecked, onCheckedChange, }: AgreementButtonProps): import("react/jsx-runtime").JSX.Element;
export {};
