import type { Source } from '@granite-js/native/react-native-fast-image';
import type { ViewProps } from 'react-native';
import { View } from 'react-native';
export type IconProps = ({
    source: Source;
    name?: never;
} | {
    name: string;
    source?: never;
}) & ViewProps;
export declare const Icon: import("react").ForwardRefExoticComponent<IconProps & import("react").RefAttributes<View>>;
