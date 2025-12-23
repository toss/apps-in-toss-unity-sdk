import type { TxtProps, TypographyKeys } from '../txt';
import type { GestureResponderHandlers, Text } from 'react-native';
export interface ParagraphTextProps extends Pick<TxtProps, 'children' | 'fontWeight' | 'color' | 'suppressHighlighting' | 'style' | 'onPress' | 'onPressIn' | 'onPressOut' | 'onLayout'>, GestureResponderHandlers {
    /**
     * @default 부모 Paragraph의 typography
     */
    typography?: TypographyKeys;
}
declare const ParagraphText: import("react").ForwardRefExoticComponent<ParagraphTextProps & import("react").RefAttributes<Text>>;
export declare const getLineHeightByFontSize: (fontSize: number) => number;
export { ParagraphText };
