import type { ParagraphTextProps } from './ParagraphText';
interface ParagraphLinkProps extends Pick<ParagraphTextProps, 'children' | 'onPress'> {
    fontWeight: 'medium' | 'bold';
    color?: string;
}
declare function ParagraphLink({ color, fontWeight, ...props }: ParagraphLinkProps): import("react/jsx-runtime").JSX.Element;
export { ParagraphLink };
