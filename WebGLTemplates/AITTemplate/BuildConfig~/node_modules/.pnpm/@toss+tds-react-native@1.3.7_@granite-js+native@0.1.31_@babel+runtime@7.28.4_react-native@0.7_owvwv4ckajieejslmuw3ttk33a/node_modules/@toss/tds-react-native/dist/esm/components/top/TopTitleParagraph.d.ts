import * as Paragraph from '../paragraph';
import type { TopTitleCommonProps } from './types';
interface TitleParagraphProps extends Pick<Paragraph.ParagraphTextProps, 'color' | 'style' | 'children'>, TopTitleCommonProps {
}
/**
 *
 * @example
 * <Top.TitleParagraph size={22}>타이틀</ListHeader.TitleParagraph>
 */
declare function TitleParagraph({ typography: overridingTypography, size, fontWeight, color, style, children, }: TitleParagraphProps): import("react/jsx-runtime").JSX.Element;
export { TitleParagraph };
