import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
export interface ItemProps {
    value: string;
    size?: 'small' | 'large';
    disabled?: boolean;
}
interface Props extends ItemProps {
    children: ReactNode;
    style?: ViewProps['style'];
}
export declare function Item({ style, children, value, disabled: _disabled, size, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export {};
