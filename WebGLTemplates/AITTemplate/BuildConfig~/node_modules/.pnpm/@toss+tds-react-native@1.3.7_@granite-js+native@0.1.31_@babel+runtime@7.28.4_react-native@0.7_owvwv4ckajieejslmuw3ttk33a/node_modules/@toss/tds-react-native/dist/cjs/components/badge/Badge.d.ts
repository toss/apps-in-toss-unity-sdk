import type { ParagraphBadgeProps } from '../paragraph';
import type { TypographyKeys } from '../txt';
type Size = 'large' | 'medium' | 'small' | 'tiny';
export interface BadgeProps extends Omit<ParagraphBadgeProps, 'typography'> {
    /**
     * `Badge` 컴포넌트의 크기를 지정해요.
     * @default 'small'
     */
    size?: Size;
}
declare function Badge({ size, fontWeight: fontWeightProp, ...restProps }: BadgeProps): import("react/jsx-runtime").JSX.Element;
export declare const sizeVariant: Record<Size, TypographyKeys>;
export default Badge;
