import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
interface Props {
    icon?: ReactNode;
    children: string;
    style?: ViewProps['style'];
    iconViewStyle?: ViewProps['style'];
}
export declare function DropdownItem({ icon, children, style, iconViewStyle }: Props): import("react/jsx-runtime").JSX.Element;
export {};
