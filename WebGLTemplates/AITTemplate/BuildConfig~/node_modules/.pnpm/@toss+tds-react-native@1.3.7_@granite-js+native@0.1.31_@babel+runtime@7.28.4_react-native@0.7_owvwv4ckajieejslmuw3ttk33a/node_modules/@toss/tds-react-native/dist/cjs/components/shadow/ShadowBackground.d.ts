import { View, type ViewProps } from 'react-native';
import { type UseShadowProps } from './useShadow';
export type ShadowBackgroundProps = {
    shadow: UseShadowProps;
} & ViewProps;
export declare const ShadowBackground: import("react").ForwardRefExoticComponent<{
    shadow: UseShadowProps;
} & ViewProps & import("react").RefAttributes<View>>;
