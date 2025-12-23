import { TextButton } from '../text-button';
import type { ComponentProps } from 'react';
import type { TopTitleCommonProps } from './types';
type TextButtonProps = ComponentProps<typeof TextButton>;
type TitleTextButtonProps = Pick<TextButtonProps, 'children' | 'style' | 'onPress' | 'color' | 'variant'> & TopTitleCommonProps;
declare function TitleTextButton({ size, typography: overridingTypography, variant, fontWeight, color: _color, ...props }: TitleTextButtonProps): import("react/jsx-runtime").JSX.Element;
export { TitleTextButton };
