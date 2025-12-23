import type { Animated as ReactNativeAnimated } from 'react-native';
import type { Layout } from '../types';
type SvgHighlightOverlayProps = {
    layout: Layout;
    opacity: ReactNativeAnimated.Value;
    holeStep: ReactNativeAnimated.Value;
};
export declare const SvgHighlightOverlay: ({ layout, opacity, holeStep }: SvgHighlightOverlayProps) => import("react/jsx-runtime").JSX.Element;
export {};
