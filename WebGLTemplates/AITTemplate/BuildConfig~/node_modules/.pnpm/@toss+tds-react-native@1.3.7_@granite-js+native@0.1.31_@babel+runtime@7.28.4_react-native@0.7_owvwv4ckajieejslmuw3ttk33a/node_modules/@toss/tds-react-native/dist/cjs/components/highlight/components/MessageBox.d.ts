import { type ReactNode } from 'react';
import type { Animated as ReactNativeAnimated } from 'react-native';
import type { Layout, YAlignmentValue } from '../types';
type MessageBoxProps = {
    layout: Layout;
    yAlignment: YAlignmentValue;
    opacity: ReactNativeAnimated.Value;
    children: ReactNode;
};
export declare const MessageBox: ({ layout: { x, y, width, height }, yAlignment, opacity, children }: MessageBoxProps) => import("react/jsx-runtime").JSX.Element;
export {};
