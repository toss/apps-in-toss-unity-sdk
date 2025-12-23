import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, TextStyle, View } from 'react-native';
import type { ParagraphTextProps } from '../paragraph';
export interface ListHeaderTitleSelectorProps {
    /**
     * `ListHeader.TitleSelector` 컴포넌트의 텍스트 스타일을 설정해요.
     */
    typography: 't4' | 't5' | 't7';
    /**
     * `ListHeader.TitleSelector` 컴포넌트의 텍스트 굵기를 설정해요.
     *
     * @default 'regular'
     */
    fontWeight: ParagraphTextProps['fontWeight'];
    /**
     * `ListHeader.TitleSelector` 컴포넌트의 텍스트 라인 수를 제한할 때 사용해요.
     */
    numberOfLines?: number;
    /**
     * `ListHeader.TitleSelector` 컴포넌트가 눌렸을 때 실행되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
    /**
     * `ListHeader.TitleSelector` 컴포넌트의 텍스트 색상을 설정해요.
     *
     * @default adaptive.grey900
     */
    color?: string;
    /**
     * `ListHeader.TitleSelector` 컴포넌트 내부에 표시될 내용을 설정해요.
     */
    children: ReactNode;
    /**
     * `ListHeader.TitleSelector` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<TextStyle>;
}
export declare const ListHeaderTitleSelector: import("react").ForwardRefExoticComponent<ListHeaderTitleSelectorProps & import("react").RefAttributes<View>>;
