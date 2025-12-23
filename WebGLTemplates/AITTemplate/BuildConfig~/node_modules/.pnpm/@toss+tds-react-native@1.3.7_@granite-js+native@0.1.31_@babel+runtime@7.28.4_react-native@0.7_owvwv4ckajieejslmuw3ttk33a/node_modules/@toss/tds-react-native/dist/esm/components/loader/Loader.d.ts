import type { ComponentPropsWithoutRef } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
type Size = 'small' | 'medium' | 'large';
type Type = 'primary' | 'dark' | 'light';
export interface LoaderProps {
    size?: Size;
    type?: Type;
    style?: StyleProp<ViewStyle>;
    customStrokeColor?: string;
    customSize?: number;
}
declare function _Loader({ size, type, customStrokeColor, customSize }: LoaderProps): import("react/jsx-runtime").JSX.Element;
declare function DelayLoader({ delay, ...props }: {
    delay?: number;
} & ComponentPropsWithoutRef<typeof _Loader>): import("react/jsx-runtime").JSX.Element;
declare function Loader({ delay, label, size, type, style, ...props }: ComponentPropsWithoutRef<typeof DelayLoader> & {
    label?: string;
}): import("react/jsx-runtime").JSX.Element;
declare namespace Loader {
    var Delay: typeof DelayLoader;
    var FullScreen: typeof FullScreenLoader;
    var Centered: typeof CenteredLoader;
}
declare function CenteredLoader({ style, ...props }: ComponentPropsWithoutRef<typeof _Loader>): import("react/jsx-runtime").JSX.Element;
declare function FullScreenLoader({ style, ...props }: ComponentPropsWithoutRef<typeof Loader>): import("react/jsx-runtime").JSX.Element;
export default Loader;
