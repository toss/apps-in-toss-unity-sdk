import * as Paragraph from '../paragraph';
import type { TopSubtitleCommonProps } from './types';
interface SubtitleParagraphProps extends Pick<Paragraph.ParagraphTextProps, 'color' | 'style' | 'children'>, TopSubtitleCommonProps {
}
/**
 *
 * @example
 * <Top.SubtitleParagraph typography="t7">타이틀</ListHeader.TitleParagraph>
 */
declare function SubtitleParagraph({ size, typography: overridingTypography, fontWeight: overridingFontWeight, color, style, children, }: SubtitleParagraphProps): import("react/jsx-runtime").JSX.Element;
export { SubtitleParagraph };
