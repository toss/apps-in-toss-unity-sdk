import type { ReactNode } from 'react';
import type { AccessibilityProps, StyleProp, ViewStyle } from 'react-native';
interface Props extends AccessibilityProps {
    /**
     * @description 리스트의 `row` 엘리먼트의 기본 하단 구분자를 지정합니다.
     * @defualt intented
     */
    rowSeparator?: 'full' | 'indented' | 'none';
    children: ReactNode;
    style?: StyleProp<ViewStyle>;
}
export declare function List({ children, rowSeparator, style, ...restProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
