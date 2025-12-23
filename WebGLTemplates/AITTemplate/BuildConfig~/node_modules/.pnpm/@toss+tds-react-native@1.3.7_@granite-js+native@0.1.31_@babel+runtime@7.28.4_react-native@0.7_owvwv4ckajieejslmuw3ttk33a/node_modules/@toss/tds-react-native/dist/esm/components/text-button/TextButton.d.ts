import type { TypographyKeys } from '../txt';
import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
type TDSTextButtonType = 'arrow' | 'underline' | 'clear';
export interface TextButtonProps {
    /**
     * `TextButton` 컴포넌트의 형태를 결정해요.
     *
     * @default 'clear'
     */
    variant?: TDSTextButtonType;
    /**
     * `TextButton` 컴포넌트의 비활성화 여부를 나타내요.
     */
    disabled?: boolean;
    /**
     * `TextButton` 컴포넌트의 텍스트 스타일을 설정해요.
     */
    typography: TypographyKeys;
    /**
     * `TextButton` 컴포넌트의 텍스트 굵기를 설정해요.
     *
     * @default 'regular'
     */
    fontWeight?: 'regular' | 'medium' | 'semibold' | 'semiBold' | 'bold';
    /**
     * `TextButton` 컴포넌트의 텍스트 색상을 설정해요.
     *
     * @default adaptive.grey900
     */
    color?: string;
    /**
     * `TextButton` 컴포넌트가 눌렸을 때 실행되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
    /**
     * `TextButton` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<ViewStyle>;
    /**
     * `TextButton` 컴포넌트 내부에 표시될 내용을 설정해요.
     */
    children: ReactNode;
}
/**
 * @description tds 최신 TextButton 입니다.
 * React Native 에서 CSS Selector 가 없습니다.
 * <TextButton /> (=& + &) 사이의 Gap 은 8 입니다. TextButton.gap 을 이용해 간격을 조정해주세요.
 */
export declare function TextButton({ variant, typography, fontWeight, color: _color, style, disabled, onPress, children, }: TextButtonProps): import("react/jsx-runtime").JSX.Element;
export declare namespace TextButton {
    var gap: number;
}
export {};
