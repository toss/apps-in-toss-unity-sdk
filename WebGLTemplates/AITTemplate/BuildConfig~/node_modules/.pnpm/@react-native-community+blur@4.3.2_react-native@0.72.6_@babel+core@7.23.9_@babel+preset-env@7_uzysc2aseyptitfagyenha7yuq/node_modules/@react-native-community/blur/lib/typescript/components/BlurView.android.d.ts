import React from 'react';
import { View, ViewProps } from 'react-native';
export declare type BlurViewProps = ViewProps & {
    blurAmount?: number;
    blurType?: 'dark' | 'light' | 'xlight';
    blurRadius?: number;
    downsampleFactor?: number;
    overlayColor?: string;
    enabled?: boolean;
    autoUpdate?: boolean;
};
declare const BlurView: React.ForwardRefExoticComponent<ViewProps & {
    blurAmount?: number | undefined;
    blurType?: "dark" | "light" | "xlight" | undefined;
    blurRadius?: number | undefined;
    downsampleFactor?: number | undefined;
    overlayColor?: string | undefined;
    enabled?: boolean | undefined;
    autoUpdate?: boolean | undefined;
} & React.RefAttributes<View>>;
export default BlurView;
