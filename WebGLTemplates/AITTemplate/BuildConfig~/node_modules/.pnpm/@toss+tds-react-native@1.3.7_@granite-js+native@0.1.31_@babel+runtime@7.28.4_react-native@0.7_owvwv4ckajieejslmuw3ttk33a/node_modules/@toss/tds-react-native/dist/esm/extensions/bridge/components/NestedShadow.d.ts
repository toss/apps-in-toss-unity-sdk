import type { ViewProps } from 'react-native';
export interface Shadow {
    opacity?: number;
    offset?: {
        x?: number;
        y?: number;
    };
    radius: number;
    color: string;
}
export interface NestedShadowProps extends ViewProps {
    shadows: Shadow[];
}
export declare function NestedShadow({ shadows, children, ...restProps }: NestedShadowProps): import("react/jsx-runtime").JSX.Element;
