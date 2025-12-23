import { TextButton } from '../text-button';
import type { ComponentProps } from 'react';
import type { TopSubtitleCommonProps } from './types';
type TextButtonProps = ComponentProps<typeof TextButton>;
type SubtitleTextButtonProps = Pick<TextButtonProps, 'children' | 'style' | 'onPress' | 'color' | 'variant'> & TopSubtitleCommonProps;
declare function SubtitleTextButton({ variant, size, typography: overridingTypography, fontWeight: overridingFontWeight, color: _color, ...props }: SubtitleTextButtonProps): import("react/jsx-runtime").JSX.Element;
export { SubtitleTextButton };
