import type { ParagraphTextProps } from '../paragraph';
import type { PressableProps } from 'react-native';
import type { TopSubtitleCommonProps } from './types';
interface SubtitleSelectorProps extends Pick<ParagraphTextProps, 'color' | 'style' | 'children'>, Pick<PressableProps, 'onPress'>, TopSubtitleCommonProps {
}
declare function SubtitleSelector({ typography: overridingTypography, fontWeight: overridingFontWeight, color: _color, size, children, style, onPress, }: SubtitleSelectorProps): import("react/jsx-runtime").JSX.Element;
export { SubtitleSelector };
