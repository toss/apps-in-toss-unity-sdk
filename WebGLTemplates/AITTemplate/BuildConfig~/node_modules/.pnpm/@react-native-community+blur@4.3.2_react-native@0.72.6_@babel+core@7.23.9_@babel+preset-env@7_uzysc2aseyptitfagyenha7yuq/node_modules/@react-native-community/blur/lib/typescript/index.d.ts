/// <reference types="react" />
import type { View } from 'react-native';
import type { BlurViewProps as BlurViewPropsIOS } from './components/BlurView.ios';
import type { BlurViewProps as BlurViewPropsAndroid } from './components/BlurView.android';
import type { VibrancyViewProps as VibrancyViewPropsIOS } from './components/VibrancyView.ios';
declare type BlurViewProps = BlurViewPropsIOS | BlurViewPropsAndroid;
declare type VibrancyViewProps = VibrancyViewPropsIOS;
declare const BlurView: import("react").ForwardRefExoticComponent<BlurViewProps & import("react").RefAttributes<View>>;
declare const VibrancyView: import("react").ForwardRefExoticComponent<import("react-native").ViewProps & {
    blurType?: ("dark" | "light" | "xlight" | "prominent" | "regular" | "extraDark" | "chromeMaterial" | "material" | "thickMaterial" | "thinMaterial" | "ultraThinMaterial" | "chromeMaterialDark" | "materialDark" | "thickMaterialDark" | "thinMaterialDark" | "ultraThinMaterialDark" | "chromeMaterialLight" | "materialLight" | "thickMaterialLight" | "thinMaterialLight" | "ultraThinMaterialLight") | undefined;
    blurAmount: number;
    reducedTransparencyFallbackColor?: string | undefined;
} & import("react").RefAttributes<View>>;
export { BlurView, VibrancyView };
export type { BlurViewProps, VibrancyViewProps };
