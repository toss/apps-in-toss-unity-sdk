import { type ViewProps } from 'react-native';
import type { Layout } from '../types';
type PassClickEventThroughHoleOverlayProps = {
    layout: Layout;
    onPress?: () => void;
    onTargetPress?: () => void;
    style: ViewProps['style'];
};
export declare const PassClickEventThroughHoleOverlay: ({ layout, onPress, onTargetPress, style, }: PassClickEventThroughHoleOverlayProps) => import("react/jsx-runtime").JSX.Element;
export {};
