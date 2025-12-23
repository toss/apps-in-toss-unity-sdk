import type { View, ViewProps } from 'react-native';
import type { RallyProps } from '../types';
export interface RallySimpleProps extends ViewProps, RallyProps {
    play?: boolean;
}
export declare const RallySimple: import("react").ForwardRefExoticComponent<RallySimpleProps & import("react").RefAttributes<View>>;
