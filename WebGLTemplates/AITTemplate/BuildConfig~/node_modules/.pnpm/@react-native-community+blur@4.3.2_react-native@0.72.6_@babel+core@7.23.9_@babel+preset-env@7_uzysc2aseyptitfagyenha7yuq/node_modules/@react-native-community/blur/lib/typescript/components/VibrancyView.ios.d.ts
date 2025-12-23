import React from 'react';
import { ViewProps } from 'react-native';
import type { BlurViewProps } from './BlurView.ios';
export declare type VibrancyViewProps = ViewProps & {
    blurType?: BlurViewProps['blurType'];
    blurAmount: number;
    reducedTransparencyFallbackColor?: string;
};
declare const VibrancyView: React.ForwardRefExoticComponent<ViewProps & {
    blurType?: BlurViewProps['blurType'];
    blurAmount: number;
    reducedTransparencyFallbackColor?: string | undefined;
} & React.RefAttributes<any>>;
export default VibrancyView;
