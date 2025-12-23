import type { ViewProps } from 'react-native';
interface EdgeGradientProps extends Omit<ViewProps, 'children'> {
    exiting: boolean;
}
export declare function LeftEdgeGradient({ style, exiting, ...props }: EdgeGradientProps): import("react/jsx-runtime").JSX.Element;
export declare function RightEdgeGradient({ style, exiting, ...props }: EdgeGradientProps): import("react/jsx-runtime").JSX.Element;
export {};
