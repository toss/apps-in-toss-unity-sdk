import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, TextStyle, ViewStyle } from 'react-native';
export interface ListHeaderRightTextProps {
    /**
     * `ListHeader.RightText` 컴포넌트의 텍스트 스타일을 설정해요.
     */
    typography: 't6' | 't7';
    /**
     * `ListHeader.RightText` 컴포넌트 내부에 표시될 내용을 설정해요.
     */
    children: ReactNode;
    /**
     * `ListHeader.RightText` 컴포넌트의 텍스트 색상을 설정해요.
     *
     * @default adaptive.grey700
     */
    color?: string;
    /**
     * `ListHeader.RightText` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<TextStyle>;
}
export declare function ListHeaderRightText({ typography, color, style, ...textProps }: ListHeaderRightTextProps): import("react/jsx-runtime").JSX.Element;
export interface ListHeaderRightArrowProps {
    /**
     * `ListHeader.RightArrow` 컴포넌트의 텍스트 스타일을 설정해요.
     */
    typography: 't6' | 't7';
    /**
     * `ListHeader.RightArrow` 컴포넌트 내부에 표시될 내용을 설정해요.
     */
    children: ReactNode;
    /**
     * `ListHeader.RightArrow` 컴포넌트의 텍스트 색상을 설정해요.
     *
     * @default adaptive.grey700
     */
    color?: string;
    /**
     * `ListHeader.RightArrow` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<ViewStyle>;
    /**
     * `ListHeader.RightArrow` 컴포넌트를 눌렀을 때 실행되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
}
export declare function ListHeaderRightArrow(props: ListHeaderRightArrowProps): import("react/jsx-runtime").JSX.Element;
