import type { ParagraphTextProps } from '../paragraph';
import type { PressableProps } from 'react-native';
import type { TopTitleCommonProps } from './types';
interface TitleSelectorProps extends Pick<ParagraphTextProps, 'color' | 'style' | 'children'>, Pick<PressableProps, 'onPress'>, TopTitleCommonProps {
}
declare function TitleSelector({ color: _color, size, fontWeight, children, typography: overridingTypography, style, onPress, }: TitleSelectorProps): import("react/jsx-runtime").JSX.Element;
export { TitleSelector };
