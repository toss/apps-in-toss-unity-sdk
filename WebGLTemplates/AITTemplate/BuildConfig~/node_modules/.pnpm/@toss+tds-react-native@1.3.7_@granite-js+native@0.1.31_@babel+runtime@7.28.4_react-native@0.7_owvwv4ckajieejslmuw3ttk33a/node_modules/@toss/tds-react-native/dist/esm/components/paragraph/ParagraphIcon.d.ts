import type { IconProps } from '../icon';
import type { TypographyKeys } from '../txt';
export interface ParagraphIconProps extends IconProps {
    typography?: TypographyKeys;
}
declare function ParagraphIcon({ size: _size, typography: typographyProp, ...props }: ParagraphIconProps): import("react/jsx-runtime").JSX.Element;
export { ParagraphIcon };
