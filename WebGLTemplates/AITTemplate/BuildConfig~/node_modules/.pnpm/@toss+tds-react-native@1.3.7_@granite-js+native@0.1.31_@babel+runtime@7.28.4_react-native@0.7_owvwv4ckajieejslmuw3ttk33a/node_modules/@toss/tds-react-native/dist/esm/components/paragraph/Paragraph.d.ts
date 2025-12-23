import type { TypographyKeys } from '../txt';
import type { ReactNode } from 'react';
import type { TextProps } from 'react-native';
export interface ParagraphProps extends Pick<TextProps, 'style' | 'onTextLayout' | 'numberOfLines'> {
    typography: TypographyKeys;
    children: ReactNode;
    afterText?: ReactNode;
}
declare function Paragraph({ children, typography, afterText, ...textProps }: ParagraphProps): import("react/jsx-runtime").JSX.Element;
export { Paragraph };
