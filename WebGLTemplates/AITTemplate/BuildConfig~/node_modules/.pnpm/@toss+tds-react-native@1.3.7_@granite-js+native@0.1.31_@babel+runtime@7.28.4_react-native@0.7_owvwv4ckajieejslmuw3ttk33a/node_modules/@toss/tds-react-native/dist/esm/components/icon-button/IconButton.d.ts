import type { ImageProps as SvgImageProps } from '@granite-js/native/react-native-svg';
import type { PressableProps, StyleProp, View, ViewStyle } from 'react-native';
type Variant = 'fill' | 'clear' | 'border';
type Asset = {
    source: SvgImageProps['href'];
    name?: never;
} | {
    source?: never;
    name: string;
};
interface IconButtonBaseProps extends PressableProps {
    color?: string;
    bgColor?: string;
    variant?: Variant;
    iconSize?: number;
    label?: string;
    style?: StyleProp<ViewStyle>;
}
export type IconButtonProps = IconButtonBaseProps & Asset;
export declare const IconButton: import("react").MemoExoticComponent<import("react").ForwardRefExoticComponent<IconButtonProps & import("react").RefAttributes<View>>>;
export {};
