import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
interface Props {
    paddingTop?: number;
    paddingBottom?: number;
    style?: StyleProp<ViewStyle>;
    children?: ReactNode;
}
export declare function TextFieldContainer({ paddingTop, paddingBottom, style, children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
