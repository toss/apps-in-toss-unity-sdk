import type { ParagraphTextProps } from '../paragraph';
import type { ReactNode } from 'react';
import type { StyleProp, TextStyle } from 'react-native';
export interface ListHeaderTitleParagraphProps {
    /**
     * `ListHeader.TitleParagraph` 컴포넌트의 텍스트 크기를 설정해요.
     */
    size?: 20 | 17 | 13;
    /**
     * `ListHeader.TitleParagraph` 컴포넌트의 텍스트 스타일을 설정해요.
     *
     * @default 't5'
     */
    typography?: 't4' | 't5' | 't7';
    /**
     * `ListHeader.TitleParagraph` 컴포넌트의 텍스트 굵기를 설정해요.
     *
     * @default 'regular'
     */
    fontWeight?: ParagraphTextProps['fontWeight'];
    /**
     * `ListHeader.TitleParagraph` 컴포넌트의 텍스트 라인 수를 제한할 때 사용해요.
     */
    numberOfLines?: number;
    /**
     * `ListHeader.TitleParagraph` 컴포넌트의 텍스트 색상을 설정해요.
     *
     * @default adaptive.grey800
     */
    color?: string;
    /**
     * `ListHeader.TitleParagraph` 컴포넌트 내부에 표시될 내용을 설정해요.
     */
    children: ReactNode;
    /**
     * `ListHeader.TitleParagraph` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<TextStyle>;
}
/**
 *
 * @example
 * <ListHeader.TitleParagraph typography="t4" >타이틀</ListHeader.TitleParagraph>
 */
export declare function ListHeaderTitleParagraph({ size, typography: _typography, color, fontWeight, numberOfLines, children, style, }: ListHeaderTitleParagraphProps): import("react/jsx-runtime").JSX.Element;
