import type { ParagraphTextProps } from '../paragraph';
export type TitleSize = 22 | 28;
export interface TopTitleCommonProps {
    typography?: ParagraphTextProps['typography'];
    fontWeight?: Extract<ParagraphTextProps['fontWeight'], 'bold' | 'semiBold' | 'semibold'>;
    size?: TitleSize;
}
export type SubTitleSize = 13 | 15 | 17;
export interface TopSubtitleCommonProps {
    typography?: ParagraphTextProps['typography'];
    fontWeight?: Extract<ParagraphTextProps['fontWeight'], 'regular' | 'medium'>;
    size?: SubTitleSize;
}
